/* eslint-env browser */
/*
 * Inline "Add new..." for a missing asset Category / Type / Sub-type (Allan 2026-10-28), used by the requisition form
 * (purchase-request-classification.js) and the receiving screen (receive-order.js).
 * - Only rendered for users allowed by ClassificationInlineCreatePolicy (the page includes this file and the
 *   #am-classification-inline config only then); the server checks the same permission again.
 * - A new sub-type is created under the chosen type; a new type always comes with its first sub-type; a new category
 *   always comes with its first type and sub-type (one transaction on the server, duplicates refused case-insensitively).
 * - IE11-safe: no arrow functions, no block-scoped declarations, explicit classList add / remove, XMLHttpRequest.
 */
(function () {
    "use strict";

    var config = document.getElementById("am-classification-inline");
    var url = config ? (config.getAttribute("data-url") || "") : "";
    var openPanel = null;

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) {
            node.className = className;
        }
        if (text) {
            node.textContent = text;
        }
        return node;
    }

    function setHidden(node, hidden) {
        if (!node) {
            return;
        }
        if (hidden) {
            node.classList.add("d-none");
        } else {
            node.classList.remove("d-none");
        }
    }

    function field(labelText, maxLength) {
        var wrap = el("div", "col-md-4");
        var label = el("label", "form-label small mb-1 d-block", labelText);
        var input = el("input", "form-control form-control-sm");
        input.type = "text";
        input.maxLength = maxLength || 200;
        // No name attribute: never posted with the page form.
        label.appendChild(input);
        wrap.appendChild(label);
        return { wrap: wrap, input: input };
    }

    function token() {
        var input = document.querySelector("input[name=\"__RequestVerificationToken\"]");
        return input ? input.value : "";
    }

    function post(values, done) {
        var xhr = new XMLHttpRequest();
        xhr.open("POST", url, true);
        xhr.setRequestHeader("Content-Type", "application/x-www-form-urlencoded; charset=UTF-8");
        xhr.setRequestHeader("Accept", "application/json");
        xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");
        xhr.onreadystatechange = function () {
            if (xhr.readyState !== 4) {
                return;
            }
            var data = null;
            try {
                data = JSON.parse(xhr.responseText);
            } catch (e) {
                data = null;
            }
            if (xhr.status !== 200 || !data) {
                done({ success: false, message: xhr.status === 403 || xhr.status === 401 ? "You do not have permission to add categories, types or sub-types." : "Could not save. Try again." });
                return;
            }
            done(data);
        };
        var parts = ["__RequestVerificationToken=" + encodeURIComponent(token())];
        for (var key in values) {
            if (Object.prototype.hasOwnProperty.call(values, key) && values[key] !== null && values[key] !== undefined && values[key] !== "") {
                parts.push(encodeURIComponent(key) + "=" + encodeURIComponent(values[key]));
            }
        }
        xhr.send(parts.join("&"));
    }

    function close() {
        if (openPanel && openPanel.parentNode) {
            openPanel.parentNode.removeChild(openPanel);
        }
        openPanel = null;
    }

    /*
     * opts.anchor     element the form is inserted after
     * opts.level      "subtype" | "type" | "category" (what is missing)
     * opts.categories [{ id, name }] existing categories (type level: pick one or add a new one)
     * opts.categoryId preselected category (type level)
     * opts.assetTypeId / opts.typeName  the type a new sub-type goes under (subtype level)
     * opts.onCreated  function (data) with the server JSON (categoryId, assetTypeId, subTypeId, names, *Created flags)
     */
    function open(opts) {
        if (!url || !opts || !opts.anchor || !opts.anchor.parentNode) {
            return;
        }
        close();
        var level = opts.level === "category" || opts.level === "type" ? opts.level : "subtype";
        var panel = el("div", "border rounded p-2 mt-2 bg-light am-classification-inline-panel");
        panel.setAttribute("role", "group");
        var heading = el("div", "small fw-semibold mb-2");
        panel.appendChild(heading);
        var row = el("div", "row g-2");
        panel.appendChild(row);

        var categorySelect = null;
        var categoryName = null;
        var typeName = null;
        var subTypeName = null;

        if (level === "type") {
            heading.textContent = "Add a new type (with its first sub-type)";
            var catWrap = el("div", "col-md-4");
            var catLabel = el("label", "form-label small mb-1 d-block", "Category");
            categorySelect = el("select", "form-select form-select-sm");
            var categories = opts.categories || [];
            for (var i = 0; i < categories.length; i++) {
                var opt = el("option", null, categories[i].name);
                opt.value = String(categories[i].id);
                if (opts.categoryId && String(opts.categoryId) === String(categories[i].id)) {
                    opt.selected = true;
                }
                categorySelect.appendChild(opt);
            }
            var addCat = el("option", null, "New category...");
            addCat.value = "";
            categorySelect.appendChild(addCat);
            if (!opts.categoryId && categories.length === 0) {
                categorySelect.value = "";
            }
            catLabel.appendChild(categorySelect);
            catWrap.appendChild(catLabel);
            row.appendChild(catWrap);
            categoryName = field("New category name");
            row.appendChild(categoryName.wrap);
            categorySelect.addEventListener("change", function () {
                setHidden(categoryName.wrap, !!categorySelect.value);
            });
            if (!opts.categoryId && categories.length > 0) {
                categorySelect.selectedIndex = 0;
            }
            setHidden(categoryName.wrap, !!categorySelect.value);
        } else if (level === "category") {
            heading.textContent = "Add a new category (with its first type and sub-type)";
            categoryName = field("Category name");
            row.appendChild(categoryName.wrap);
        } else {
            heading.textContent = "Add a new sub-type" + (opts.typeName ? " under " + opts.typeName : "");
        }

        if (level !== "subtype") {
            typeName = field(level === "type" ? "Type name" : "First type name");
            row.appendChild(typeName.wrap);
        }
        subTypeName = field(level === "subtype" ? "Sub-type name" : "First sub-type name");
        row.appendChild(subTypeName.wrap);

        var error = el("div", "text-danger small mt-1 d-none");
        error.setAttribute("aria-live", "polite");
        panel.appendChild(error);
        var actions = el("div", "mt-2");
        var save = el("button", "btn btn-sm btn-primary me-2", "Save");
        save.type = "button";
        var cancel = el("button", "btn btn-sm btn-outline-secondary", "Cancel");
        cancel.type = "button";
        actions.appendChild(save);
        actions.appendChild(cancel);
        panel.appendChild(actions);

        function fail(message) {
            error.textContent = message;
            setHidden(error, false);
            save.disabled = false;
        }

        function submit() {
            var values = { subTypeName: subTypeName.input.value.trim() };
            if (level === "subtype") {
                values.assetTypeId = opts.assetTypeId;
            } else {
                values.typeName = typeName.input.value.trim();
                if (level === "type" && categorySelect && categorySelect.value) {
                    values.categoryId = categorySelect.value;
                } else {
                    values.categoryName = categoryName.input.value.trim();
                    if (!values.categoryName) {
                        fail("Enter the category name.");
                        return;
                    }
                }
                if (!values.typeName) {
                    fail("Enter the type name.");
                    return;
                }
            }
            if (!values.subTypeName) {
                fail("Enter the sub-type name: every type needs at least one sub-type.");
                return;
            }
            save.disabled = true;
            setHidden(error, true);
            post(values, function (data) {
                if (!data.success) {
                    fail(data.message || "Could not save.");
                    return;
                }
                close();
                if (opts.onCreated) {
                    opts.onCreated(data);
                }
            });
        }

        save.addEventListener("click", submit);
        cancel.addEventListener("click", function () {
            close();
            if (opts.onCancel) {
                opts.onCancel();
            }
        });
        // Enter inside the panel saves it instead of submitting the page form.
        panel.addEventListener("keydown", function (e) {
            if ((e.key === "Enter" || e.keyCode === 13) && e.target && e.target.tagName === "INPUT") {
                e.preventDefault();
                submit();
            }
        });

        opts.anchor.parentNode.insertBefore(panel, opts.anchor.nextSibling);
        openPanel = panel;
        var first;
        if (categoryName && !categoryName.wrap.classList.contains("d-none")) {
            first = categoryName.input;
        } else if (typeName) {
            first = typeName.input;
        } else {
            first = subTypeName.input;
        }
        if (first && first.focus) {
            first.focus();
        }
    }

    window.AmClassificationInline = {
        enabled: !!url,
        addValue: "__am_add_new__",
        open: open,
        close: close
    };
})();
