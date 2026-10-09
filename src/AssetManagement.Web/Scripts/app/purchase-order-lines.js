/* rel1009i: PO lines from a requisition - clamp order qty to outstanding, line/grand totals, sub-type filter by type. */
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
        table.addEventListener("change", function (e) {
            if (e.target && e.target.classList.contains("am-po-type")) {
                filterSubTypes(e.target.closest("tr"));
            }
            recalc(table);
        });
        Array.prototype.forEach.call(table.querySelectorAll("tbody tr"), filterSubTypes);
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
