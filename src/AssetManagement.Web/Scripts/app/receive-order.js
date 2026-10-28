/* rel1009i: receiving screen - per line destination / tag mode toggles, serial and tag counts vs quantity, qty clamp.
   C3 (migration 108): a classified line shows Edit; the editor's type / sub-type selects are disabled (not posted)
   until Edit, and while editing the read-only sub-type select is disabled so only one value is posted. */
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

    function init() {
        var form = document.getElementById("receive-order-form");
        if (!form) {
            return;
        }
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
