/* eslint-env browser */
/*
 * Requisition line classification (catalog requisition flows).
 * - Cascading Category > Type > Sub-type pickers per line (and the optional header pick), fed by the org's
 *   existing asset categories (JSON on #purchase-line-items[data-am-catalog]). Picking a child fills its parents.
 * - Rows added/removed by purchase-request-create.js get a picker block and Lines[i].* names (MutationObserver).
 * - Line pickers show Type > Sub-type only (both required on an untagged line); the category follows from the type
 *   (the server derives it again and ignores any posted category). A line tagged to an existing asset takes its
 *   classification from the asset: its pickers are hidden and disabled (not posted; the server re-resolves the
 *   classification from the tag) and the asset's Category / Type / Sub-type are shown read-only instead
 *   (purchase-request-create.js fills them from the picked asset).
 * - Rule C5 (QA 2026-10-28): a type with no active sub-types is never offered (the server already leaves it out of
 *   the JSON; filtered again here so a stale page cannot offer it). The server still rejects one with a clear message.
 * - Live per-line approval path preview: POSTs the form (no file) to PreviewLinePaths; the server applies the same
 *   classification + resolution rules as submit. The server re-validates everything on submit.
 */
(function () {
    "use strict";

    var container = document.getElementById("purchase-line-items");
    var form = document.getElementById("purchase-request-form");
    if (!container || !form) {
        return;
    }

    var catalog;
    try {
        catalog = JSON.parse(container.getAttribute("data-am-catalog") || "{}");
    } catch (e) {
        catalog = {};
    }
    catalog.categories = catalog.categories || [];
    catalog.types = catalog.types || [];
    catalog.subTypes = catalog.subTypes || [];
    catalog.types = catalog.types.filter(function (t) {
        for (var i = 0; i < catalog.subTypes.length; i++) {
            if (String(catalog.subTypes[i].parentId) === String(t.id)) {
                return true;
            }
        }
        return false;
    });

    var previewUrl = container.getAttribute("data-am-preview-lines-url") || "";
    // RequiredHeader: a line without its own type inherits the header type / sub-type, so line pickers stay optional.
    var linesInheritHeader = (container.getAttribute("data-am-classification-mode") || "") === "RequiredHeader";
    var pathsPanel = document.getElementById("purchase-line-paths");
    var template = document.getElementById("am-line-classification-template");
    var previewTimer = null;
    var previewSeq = 0;

    function byId(list, id) {
        for (var i = 0; i < list.length; i++) {
            if (String(list[i].id) === String(id)) {
                return list[i];
            }
        }
        return null;
    }

    function fillSelect(select, items, emptyLabel, selected) {
        select.innerHTML = "";
        var empty = document.createElement("option");
        empty.value = "";
        empty.textContent = emptyLabel;
        select.appendChild(empty);
        var found = false;
        items.forEach(function (item) {
            var opt = document.createElement("option");
            opt.value = String(item.id);
            opt.textContent = item.name;
            if (selected && String(selected) === String(item.id)) {
                opt.selected = true;
                found = true;
            }
            select.appendChild(opt);
        });
        if (!found) {
            select.value = "";
        }
    }

    function isLineBlock(block) {
        return block.classList.contains("am-line-classification");
    }

    function isFromAsset(block) {
        return block.classList.contains("am-line-classification-from-asset");
    }

    function setHidden(el, hidden) {
        if (!el) {
            return;
        }
        if (hidden) {
            el.classList.add("d-none");
        } else {
            el.classList.remove("d-none");
        }
    }

    function typeLabel(typeItem) {
        var category = byId(catalog.categories, typeItem.parentId);
        return category ? category.name + " \u203a " + typeItem.name : typeItem.name;
    }

    function render(block, categoryId, typeId, subTypeId) {
        var cat = block.querySelector(".am-pick-category");
        var type = block.querySelector(".am-pick-type");
        var sub = block.querySelector(".am-pick-subtype");
        if (!type || !sub) {
            return;
        }

        // Child picks imply parents (sub-type > type > category).
        var subItem = subTypeId ? byId(catalog.subTypes, subTypeId) : null;
        if (subItem) {
            typeId = subItem.parentId;
        }
        var typeItem = typeId ? byId(catalog.types, typeId) : null;
        if (typeItem) {
            categoryId = typeItem.parentId;
        }

        var lineBlock = isLineBlock(block);
        if (cat) {
            fillSelect(cat, catalog.categories, "Category (optional)", categoryId);
        }
        var types = catalog.types.filter(function (t) {
            return !cat || !categoryId || String(t.parentId) === String(categoryId);
        });
        if (!cat) {
            // No category picker: show "Category > Type" so the category is visible but never picked separately.
            types = types.map(function (t) {
                return { id: t.id, name: typeLabel(t) };
            });
        }
        fillSelect(type, types, lineBlock ? "Type *" : "Type", typeItem ? typeItem.id : null);
        fillSelect(sub, catalog.subTypes.filter(function (s) {
            return typeItem ? String(s.parentId) === String(typeItem.id) : false;
        }), typeItem ? (lineBlock ? "Sub-type *" : "Sub-type (optional)") : "Sub-type (choose a type first)", subItem ? subItem.id : null);

        if (lineBlock) {
            var fromAsset = isFromAsset(block);
            type.disabled = fromAsset;
            sub.disabled = fromAsset || !typeItem;
            type.required = !fromAsset && !linesInheritHeader;
            sub.required = !fromAsset && !linesInheritHeader;
            setHidden(block.querySelector(".am-line-from-asset"), !fromAsset);
            setHidden(block.querySelector(".am-line-pickers"), fromAsset);
            setHidden(block.querySelector(".am-line-asset-classification"), !fromAsset);
        } else {
            sub.disabled = !typeItem;
        }
    }

    function selectedOf(select) {
        return select ? (select.value || select.getAttribute("data-selected")) : null;
    }

    function wire(block) {
        if (!block || block.__amWired) {
            return;
        }
        block.__amWired = true;
        var cat = block.querySelector(".am-pick-category");
        var type = block.querySelector(".am-pick-type");
        var sub = block.querySelector(".am-pick-subtype");
        render(block, cat ? cat.getAttribute("data-selected") : null, type.getAttribute("data-selected"), sub.getAttribute("data-selected"));
        if (cat) {
            cat.addEventListener("change", function () {
                render(block, cat.value, null, null);
                schedulePreview();
            });
        }
        type.addEventListener("change", function () {
            render(block, cat ? cat.value : null, type.value, null);
            schedulePreview();
        });
        sub.addEventListener("change", function () {
            render(block, cat ? cat.value : null, type.value, sub.value);
            schedulePreview();
        });
        // purchase-request-create.js fires this when the line's asset tag is set or cleared.
        block.addEventListener("am:line-tag-changed", function () {
            render(block, cat ? selectedOf(cat) : null, selectedOf(type), selectedOf(sub));
            schedulePreview();
        });
    }

    function syncRows() {
        var rows = container.querySelectorAll(".purchase-line-item");
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i];
            var block = row.querySelector(".am-line-classification");
            if (!block && template) {
                var fragment = document.importNode(template.content, true);
                row.appendChild(fragment);
                block = row.querySelector(".am-line-classification");
            }
            if (!block) {
                continue;
            }
            Array.prototype.forEach.call(block.querySelectorAll("select[data-field]"), function (select) {
                select.name = "Lines[" + i + "]." + select.getAttribute("data-field");
            });
            wire(block);
        }
    }

    function setPathText(lineNumber, text, warn) {
        var rows = container.querySelectorAll(".purchase-line-item");
        var row = rows[lineNumber - 1];
        var el = row ? row.querySelector(".am-line-path") : null;
        if (!el) {
            return;
        }
        el.textContent = text || "";
        el.classList.toggle("text-warning", !!warn);
        el.classList.toggle("text-muted", !warn);
    }

    function clearPaths() {
        Array.prototype.forEach.call(container.querySelectorAll(".am-line-path"), function (el) {
            el.textContent = "";
        });
        if (pathsPanel) {
            pathsPanel.classList.add("d-none");
            pathsPanel.textContent = "";
        }
    }

    function showPanel(className, nodes) {
        if (!pathsPanel) {
            return;
        }
        pathsPanel.className = "alert small mt-2 " + className;
        pathsPanel.innerHTML = "";
        nodes.forEach(function (n) {
            pathsPanel.appendChild(n);
        });
    }

    function textNode(tag, text) {
        var el = document.createElement(tag);
        el.textContent = text;
        return el;
    }

    function applyPreview(data) {
        clearPaths();
        if (!data) {
            return;
        }
        if (data.error) {
            showPanel("alert-warning", [textNode("div", data.error)]);
            return;
        }
        var groups = data.groups || [];
        var mixed = groups.length > 1;
        (data.lines || []).forEach(function (line) {
            setPathText(line.lineNumber, "Approval: " + line.label + (mixed ? " (requisition " + line.groupNumber + ")" : ""), mixed && data.willBlock);
        });
        if (!mixed) {
            return;
        }
        var nodes = [];
        nodes.push(textNode("strong", data.willBlock
            ? "These items use different approval paths. Your organisation does not allow mixing them: remove lines or submit them separately."
            : "These items use different approval paths, so submitting creates " + groups.length + " linked requisitions:"));
        var list = document.createElement("ul");
        list.className = "mb-0 mt-1";
        groups.forEach(function (g) {
            list.appendChild(textNode("li", "Requisition " + g.groupNumber + " (lines " + g.lineNumbers.join(", ") + "): " + g.label + " - " + g.summary));
        });
        nodes.push(list);
        showPanel(data.willBlock ? "alert-danger" : "alert-info", nodes);
    }

    function buildBody() {
        var params = new URLSearchParams();
        Array.prototype.forEach.call(form.elements, function (el) {
            if (!el.name || el.disabled || el.type === "file" || el.type === "submit" || el.type === "button") {
                return;
            }
            if ((el.type === "checkbox" || el.type === "radio") && !el.checked) {
                return;
            }
            params.append(el.name, el.value);
        });
        return params.toString();
    }

    function runPreview() {
        if (!previewUrl || !window.fetch) {
            return;
        }
        var seq = ++previewSeq;
        fetch(previewUrl, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8", Accept: "application/json" },
            body: buildBody()
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Request failed");
                }
                return response.json();
            })
            .then(function (data) {
                if (seq === previewSeq) {
                    applyPreview(data);
                }
            })
            .catch(function () {
                if (seq === previewSeq) {
                    clearPaths();
                }
            });
    }

    function schedulePreview() {
        if (previewTimer) {
            clearTimeout(previewTimer);
        }
        previewTimer = setTimeout(runPreview, 300);
    }

    var header = document.getElementById("purchase-header-classification");
    if (header) {
        wire(header);
    }

    syncRows();
    if (window.MutationObserver) {
        new MutationObserver(function () {
            syncRows();
            schedulePreview();
        }).observe(container, { childList: true });
    }

    var department = document.getElementById("DepartmentId");
    if (department) {
        department.addEventListener("change", schedulePreview);
    }

    schedulePreview();
})();
