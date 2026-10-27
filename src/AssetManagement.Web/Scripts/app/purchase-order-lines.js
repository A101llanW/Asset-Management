/* rel1009i: PO lines from a requisition - clamp order qty to outstanding, line/grand totals, sub-type filter by type.
   C3 (migration 108): a line pre-filled from the requisition shows its classification read-only with an Edit button;
   the type / sub-type selects stay disabled (not posted) until Edit, and Cancel restores the prefill. */
(function () {
    "use strict";

    function num(value) {
        var parsed = parseFloat(value);
        return isNaN(parsed) ? 0 : parsed;
    }

    function filterSubTypes(row) {
        var typeSelect = row.querySelector(".am-po-type");
        var subSelect = row.querySelector(".am-po-subtype");
        if (!typeSelect || !subSelect) {
            return;
        }
        if (typeSelect.disabled) {
            subSelect.disabled = true; // prefilled line not in Edit mode: nothing is posted
            return;
        }
        var typeId = typeSelect.value;
        Array.prototype.forEach.call(subSelect.options, function (opt) {
            if (!opt.value) {
                return;
            }
            var show = typeId && opt.getAttribute("data-type-id") === typeId;
            opt.hidden = !show;
            opt.disabled = !show;
            if (!show && opt.selected) {
                subSelect.value = "";
            }
        });
        subSelect.disabled = !typeId;
        subSelect.required = subSelect.getAttribute("data-am-subtype-required") === "true" && !subSelect.disabled;
    }

    function setEditing(row, editing) {
        var editor = row.querySelector(".am-po-class-editor");
        var readonly = row.querySelector(".am-po-class-readonly");
        if (!editor || editor.getAttribute("data-am-prefilled") !== "true") {
            return;
        }
        var typeSelect = editor.querySelector(".am-po-type");
        var subSelect = editor.querySelector(".am-po-subtype");
        if (!editing) {
            // Back to the requisition's classification; disabled selects are not posted.
            if (typeSelect) {
                typeSelect.value = editor.getAttribute("data-am-prefill-type") || "";
            }
            if (subSelect) {
                subSelect.value = editor.getAttribute("data-am-prefill-subtype") || "";
                subSelect.required = false;
            }
        }
        if (typeSelect) {
            typeSelect.disabled = !editing;
            typeSelect.required = editing;
        }
        editor.classList.toggle("d-none", !editing);
        if (readonly) {
            readonly.classList.toggle("d-none", editing);
        }
        filterSubTypes(row);
        if (editing && typeSelect) {
            typeSelect.focus();
        }
    }

    function recalc(table) {
        var totalQty = 0;
        var totalCost = 0;
        Array.prototype.forEach.call(table.querySelectorAll("tbody tr"), function (row) {
            var qtyInput = row.querySelector(".am-po-qty");
            var costInput = row.querySelector(".am-po-cost");
            if (!qtyInput) {
                return;
            }
            var max = parseInt(qtyInput.getAttribute("max"), 10);
            var qty = Math.max(0, Math.floor(num(qtyInput.value)));
            if (!isNaN(max) && qty > max) {
                qty = max;
                qtyInput.value = String(max);
            }
            var cost = Math.max(0, num(costInput ? costInput.value : 0));
            var lineTotal = qty * cost;
            totalQty += qty;
            totalCost += lineTotal;
            var cell = row.querySelector(".am-po-line-total");
            if (cell) {
                cell.textContent = lineTotal.toFixed(2);
            }
        });
        var qtyCell = document.getElementById("po-total-qty");
        var costCell = document.getElementById("po-total-cost");
        if (qtyCell) {
            qtyCell.textContent = String(totalQty);
        }
        if (costCell) {
            costCell.textContent = totalCost.toFixed(2);
        }
    }

    function init() {
        var table = document.getElementById("po-lines-table");
        if (!table) {
            return;
        }
        table.addEventListener("input", function () { recalc(table); });
        table.addEventListener("click", function (e) {
            var target = e.target;
            if (!target || !target.closest) {
                return;
            }
            if (target.closest(".am-po-class-edit")) {
                setEditing(target.closest("tr"), true);
            } else if (target.closest(".am-po-class-cancel")) {
                setEditing(target.closest("tr"), false);
            }
        });
        table.addEventListener("change", function (e) {
            if (e.target && e.target.classList.contains("am-po-type")) {
                filterSubTypes(e.target.closest("tr"));
            }
            recalc(table);
        });
        Array.prototype.forEach.call(table.querySelectorAll("tbody tr"), function (row) {
            var editor = row.querySelector(".am-po-class-editor");
            var typeSelect = editor ? editor.querySelector(".am-po-type") : null;
            if (typeSelect && editor.getAttribute("data-am-prefilled") === "true" && !typeSelect.disabled) {
                typeSelect.required = true; // redisplay with Edit open
            }
            filterSubTypes(row);
        });
        recalc(table);
        // A supplier offer "Select" (purchase-create.js) writes #UnitCost via jQuery without an input event.
        var observerTarget = document.getElementById("UnitCost");
        if (observerTarget && window.jQuery) {
            window.jQuery(observerTarget).on("change", function () { recalc(table); });
        }
        document.addEventListener("click", function () { window.setTimeout(function () { recalc(table); }, 0); });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
}());
