/* rel1009i: receiving screen - per line destination / tag mode toggles, serial and tag counts vs quantity, qty clamp.
   C3 (migration 108): a classified line shows Edit; the editor's type / sub-type selects are disabled (not posted)
   until Edit, and while editing the read-only sub-type select is disabled so only one value is posted.
   Inline create (Allan 2026-10-28): with Assets.Receive the type / sub-type selects also offer "+ Add new type..." /
   "+ Add new sub-type..." (classification-inline-create.js); a new type always comes with its first sub-type, and
   the new items are added to every line's selects and selected on the line that asked. */
(function () {
    "use strict";

    // IE11: classList.toggle ignores its second (force) argument, so show / hide with explicit add / remove.
    function setHidden(el, hidden) {
        if (hidden) {
            el.classList.add("d-none");
        } else {
            el.classList.remove("d-none");
        }
    }

    var inline = window.AmClassificationInline && window.AmClassificationInline.enabled ? window.AmClassificationInline : null;
    var ADD = inline ? inline.addValue : "__am_add_new__";

    function isAddOption(opt) {
        return !!opt && opt.value === ADD;
    }

    function countLines(text) {
        return String(text || "").split(/[\r\n,;\t]+/).filter(function (x) { return x.trim().length > 0; }).length;
    }

    function updateLine(card) {
        var qtyInput = card.querySelector(".am-receive-qty");
        var max = parseInt(card.getAttribute("data-am-outstanding"), 10);
        var qty = parseInt(qtyInput.value, 10);
        if (isNaN(qty) || qty < 0) {
            qty = 0;
        }
        if (!isNaN(max) && qty > max) {
            qty = max;
            qtyInput.value = String(max);
        }

        var kind = card.querySelector(".am-dest-kind");
        var dept = card.querySelector(".am-dest-department");
        if (kind && dept) {
            dept.disabled = kind.value !== "Department";
            dept.required = !dept.disabled && qty > 0;
        }

        var tagMode = card.querySelector(".am-tag-mode");
        var tags = card.querySelector(".am-tags");
        var tagCount = card.querySelector(".am-tag-count");
        if (tagMode && tags) {
            tags.disabled = tagMode.value !== "Enter";
            if (tagCount) {
                tagCount.textContent = tags.disabled ? "" : (countLines(tags.value) + " of " + qty + " tag(s) entered");
                tagCount.className = "form-text am-tag-count" + (!tags.disabled && countLines(tags.value) !== qty ? " text-danger" : "");
            }
        }

        var serials = card.querySelector(".am-serials");
        var serialCount = card.querySelector(".am-serial-count");
        if (serials && serialCount) {
            var n = countLines(serials.value);
            serialCount.textContent = n === 0 ? "No serials (allowed)." : (n + " of " + qty + " serial(s) entered");
            serialCount.className = "form-text am-serial-count" + (n > 0 && n !== qty ? " text-danger" : "");
        }

        updateEditor(card, qty);

        // Sub-type is required: the type comes from the override select or, when a source set it, from data-am-type-id.
        var typeSelect = card.querySelector(".am-po-type");
        var subSelect = card.querySelector(".am-po-subtype");
        if (subSelect && isEditing(card)) {
            subSelect.disabled = true; // the Edit panel posts the classification instead
            subSelect.required = false;
            subSelect.classList.remove("is-invalid");
            subSelect = null;
        }
        if (typeSelect) {
            typeSelect.required = qty > 0;
            if (!typeSelect.required || typeSelect.value) {
                typeSelect.classList.remove("is-invalid");
            }
        }
        if (subSelect) {
            var typeId = typeSelect ? typeSelect.value : (subSelect.getAttribute("data-am-type-id") || "");
            var available = 0;
            Array.prototype.forEach.call(subSelect.options, function (opt) {
                if (isAddOption(opt)) {
                    opt.hidden = !typeId;
                    opt.disabled = !typeId;
                    return;
                }
                if (!opt.value) {
                    return;
                }
                var show = !!typeId && opt.getAttribute("data-type-id") === typeId;
                opt.hidden = !show;
                opt.disabled = !show;
                if (show) {
                    available++;
                } else if (opt.selected) {
                    subSelect.value = "";
                }
            });
            // C5 (Allan 2026-10-27 21:20 EAT): category, type and sub-type are all required. A type with no
            // sub-types keeps the (empty) select enabled and required, so the submit is blocked until one is added.
            subSelect.disabled = !typeId;
            subSelect.required = qty > 0 && !!typeId;
            if (!subSelect.required || subSelect.value) {
                subSelect.classList.remove("is-invalid");
            }
            var none = card.querySelector(".am-subtype-none");
            if (none) {
                if (typeId && available === 0) {
                    none.classList.remove("d-none");
                } else {
                    none.classList.add("d-none");
                }
            }
        }
    }

    function isEditing(card) {
        var editor = card.querySelector(".am-recv-class-editor");
        return !!editor && !editor.classList.contains("d-none");
    }

    function updateEditor(card, qty) {
        var editor = card.querySelector(".am-recv-class-editor");
        if (!editor) {
            return;
        }
        var editing = isEditing(card);
        var typeSelect = editor.querySelector(".am-edit-type");
        var subSelect = editor.querySelector(".am-edit-subtype");
        // Decision B: tells the server the panel is open, so blanks are rejected instead of falling back to the PO's.
        var opened = editor.querySelector(".am-edit-opened");
        if (opened) {
            opened.disabled = !editing;
        }
        typeSelect.disabled = !editing;
        typeSelect.required = editing && qty > 0;
        var typeId = typeSelect.value;
        var available = 0;
        Array.prototype.forEach.call(subSelect.options, function (opt) {
            if (isAddOption(opt)) {
                opt.hidden = !typeId;
                opt.disabled = !typeId;
                return;
            }
            if (!opt.value) {
                return;
            }
            var show = !!typeId && opt.getAttribute("data-type-id") === typeId;
            opt.hidden = !show;
            opt.disabled = !show;
            if (show) {
                available++;
            } else if (opt.selected) {
                subSelect.value = "";
            }
        });
        var none = editor.querySelector(".am-edit-subtype-none");
        if (none) {
            setHidden(none, !(editing && typeId && available === 0));
        }
        // Sub-type is always required after Edit; the server rejects a type with no active sub-types.
        subSelect.disabled = !editing || !typeId;
        subSelect.required = !subSelect.disabled && qty > 0;
        if (!subSelect.required || subSelect.value) {
            subSelect.classList.remove("is-invalid");
        }
        if (!typeSelect.required || typeSelect.value) {
            typeSelect.classList.remove("is-invalid");
        }
        var cleared = editor.querySelector(".am-edit-class-missing");
        if (cleared && (!editing || (typeSelect.value && subSelect.value))) {
            setHidden(cleared, true);
        }
    }

    function setEditing(card, editing) {
        var editor = card.querySelector(".am-recv-class-editor");
        var readonly = card.querySelector(".am-recv-class-readonly");
        if (!editor) {
            return;
        }
        if (!editing) {
            editor.querySelector(".am-edit-type").value = editor.getAttribute("data-am-prefill-type") || "";
            editor.querySelector(".am-edit-subtype").value = editor.getAttribute("data-am-prefill-subtype") || "";
        }
        setHidden(editor, !editing);
        if (readonly) {
            setHidden(readonly, editing);
            var ro = readonly.querySelector(".am-po-subtype");
            if (ro && !editing) {
                ro.disabled = false; // updateLine re-applies its rules
            }
        }
        updateLine(card);
    }

    // C5: blocks the submit while any received line lacks its type or sub-type (category follows the type).
    function flagMissingSubTypes(form) {
        var missing = 0;
        Array.prototype.forEach.call(form.querySelectorAll(".am-po-type, .am-edit-type, .am-po-subtype, .am-edit-subtype"), function (sel) {
            if (sel.required && !sel.disabled && !sel.value) {
                sel.classList.add("is-invalid");
                missing++;
            }
        });
        return missing;
    }

    // Decision B (Allan 2026-10-28 08:39 EAT): an open Edit panel with a blank type or sub-type blocks the submit on
    // a line being received (both selects are flagged, even the sub-type that a blank type disables).
    function flagClearedEditors(form) {
        var missing = 0;
        Array.prototype.forEach.call(form.querySelectorAll(".am-receive-line"), function (card) {
            var editor = card.querySelector(".am-recv-class-editor");
            if (!editor) {
                return;
            }
            var qtyInput = card.querySelector(".am-receive-qty");
            var qty = qtyInput ? parseInt(qtyInput.value, 10) : 0;
            var typeSelect = editor.querySelector(".am-edit-type");
            var subSelect = editor.querySelector(".am-edit-subtype");
            var cleared = isEditing(card) && qty > 0 && (!typeSelect.value || !subSelect.value);
            if (cleared) {
                if (!typeSelect.value) {
                    typeSelect.classList.add("is-invalid");
                }
                if (!subSelect.value) {
                    subSelect.classList.add("is-invalid");
                }
                missing++;
            }
            var message = editor.querySelector(".am-edit-class-missing");
            if (message) {
                setHidden(message, !cleared);
            }
        });
        return missing;
    }

    function addOption(select, label) {
        var opt = document.createElement("option");
        opt.value = ADD;
        opt.textContent = label;
        opt.className = "am-add-new";
        select.appendChild(opt);
    }

    function insertBeforeAdd(parent, opt) {
        var add = null;
        for (var i = 0; i < parent.children.length; i++) {
            if (isAddOption(parent.children[i])) {
                add = parent.children[i];
            }
        }
        parent.insertBefore(opt, add);
    }

    function hasOption(select, value) {
        for (var i = 0; i < select.options.length; i++) {
            if (select.options[i].value === String(value)) {
                return true;
            }
        }
        return false;
    }

    function readCategories() {
        var config = document.getElementById("am-classification-inline");
        try {
            return JSON.parse((config && config.getAttribute("data-categories")) || "[]");
        } catch (e) {
            return [];
        }
    }

    // Adds a created type (in its category's optgroup) and its first / new sub-type to every line's selects.
    function addCreatedToSelects(form, data) {
        Array.prototype.forEach.call(form.querySelectorAll(".am-po-type, .am-edit-type"), function (select) {
            if (!data.assetTypeId || hasOption(select, data.assetTypeId)) {
                return;
            }
            var group = null;
            Array.prototype.forEach.call(select.querySelectorAll("optgroup"), function (g) {
                if (g.getAttribute("label") === data.categoryName) {
                    group = g;
                }
            });
            if (!group) {
                group = document.createElement("optgroup");
                group.setAttribute("label", data.categoryName || "Other");
                insertBeforeAdd(select, group);
            }
            var opt = document.createElement("option");
            opt.value = String(data.assetTypeId);
            opt.textContent = data.assetTypeName;
            group.appendChild(opt);
        });
        Array.prototype.forEach.call(form.querySelectorAll(".am-po-subtype, .am-edit-subtype"), function (select) {
            if (!data.subTypeId || hasOption(select, data.subTypeId)) {
                return;
            }
            var opt = document.createElement("option");
            opt.value = String(data.subTypeId);
            opt.textContent = data.subTypeName;
            opt.setAttribute("data-type-id", String(data.assetTypeId));
            insertBeforeAdd(select, opt);
        });
    }

    // The type select that goes with a sub-type select (same editor / override row), or null for the read-only one.
    function typeSelectFor(subSelect) {
        var editor = subSelect.closest ? subSelect.closest(".am-recv-class-editor") : null;
        if (editor) {
            return editor.querySelector(".am-edit-type");
        }
        if (subSelect.classList.contains("am-required-subtype")) {
            return null;
        }
        var card = subSelect.closest ? subSelect.closest(".am-receive-line") : null;
        return card ? card.querySelector(".am-po-type") : null;
    }

    function subSelectFor(typeSelect) {
        var editor = typeSelect.closest ? typeSelect.closest(".am-recv-class-editor") : null;
        if (editor) {
            return editor.querySelector(".am-edit-subtype");
        }
        var card = typeSelect.closest ? typeSelect.closest(".am-receive-line") : null;
        return card ? card.querySelector(".am-po-subtype:not(.am-required-subtype)") : null;
    }

    function initInlineCreate(form) {
        if (!inline) {
            return;
        }
        var categories = readCategories();
        Array.prototype.forEach.call(form.querySelectorAll(".am-po-type, .am-edit-type"), function (select) {
            addOption(select, "+ Add new type...");
            select.__amLast = select.value;
        });
        Array.prototype.forEach.call(form.querySelectorAll(".am-po-subtype, .am-edit-subtype"), function (select) {
            addOption(select, "+ Add new sub-type...");
            select.__amLast = select.value;
        });
        form.addEventListener("change", function (e) {
            var select = e.target;
            if (!select || select.tagName !== "SELECT" || !select.classList) {
                return;
            }
            var isType = select.classList.contains("am-po-type") || select.classList.contains("am-edit-type");
            var isSub = select.classList.contains("am-po-subtype") || select.classList.contains("am-edit-subtype");
            if (!isType && !isSub) {
                return;
            }
            if (select.value !== ADD) {
                select.__amLast = select.value;
                return;
            }
            select.value = select.__amLast || "";
            var card = select.closest ? select.closest(".am-receive-line") : null;
            if (card) {
                updateLine(card);
            }
            var typeSelect = isType ? select : typeSelectFor(select);
            var typeId = isType ? null : (typeSelect ? typeSelect.value : (select.getAttribute("data-am-type-id") || ""));
            var typeName = "";
            if (typeSelect && typeSelect.selectedIndex >= 0 && typeId) {
                typeName = typeSelect.options[typeSelect.selectedIndex].textContent;
            }
            if (isSub && !typeId) {
                return;
            }
            inline.open({
                anchor: select.parentNode,
                level: isType ? "type" : "subtype",
                categories: categories,
                assetTypeId: typeId,
                typeName: typeName,
                onCreated: function (data) {
                    if (data.categoryCreated) {
                        categories.push({ id: data.categoryId, name: data.categoryName });
                    }
                    addCreatedToSelects(form, data);
                    if (isType) {
                        select.value = String(data.assetTypeId);
                        select.__amLast = select.value;
                        var sub = subSelectFor(select);
                        if (card) {
                            updateLine(card);
                        }
                        if (sub) {
                            sub.value = String(data.subTypeId);
                            sub.__amLast = sub.value;
                        }
                    } else {
                        select.value = String(data.subTypeId);
                        select.__amLast = select.value;
                    }
                    if (card) {
                        updateLine(card);
                    }
                }
            });
        }, true); // capture: restore the select before the line's own change handler re-filters its sub-types
    }

    function init() {
        var form = document.getElementById("receive-order-form");
        if (!form) {
            return;
        }
        initInlineCreate(form);
        var cards = form.querySelectorAll(".am-receive-line");
        Array.prototype.forEach.call(cards, function (card) {
            card.addEventListener("input", function () { updateLine(card); });
            card.addEventListener("change", function () { updateLine(card); });
            card.addEventListener("click", function (e) {
                var target = e.target;
                if (!target || !target.closest) {
                    return;
                }
                if (target.closest(".am-recv-class-edit")) {
                    setEditing(card, true);
                } else if (target.closest(".am-recv-class-cancel")) {
                    setEditing(card, false);
                }
            });
            updateLine(card);
        });
        form.addEventListener("submit", function (e) {
            var missing = flagMissingSubTypes(form) + flagClearedEditors(form);
            if (missing > 0) {
                e.preventDefault();
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
}());
