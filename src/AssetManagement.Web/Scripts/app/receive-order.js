/* rel1009i: receiving screen - per line destination / tag mode toggles, serial and tag counts vs quantity, qty clamp. */
(function () {
    "use strict";

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

        // Sub-type is required: the type comes from the override select or, when a source set it, from data-am-type-id.
        var typeSelect = card.querySelector(".am-po-type");
        var subSelect = card.querySelector(".am-po-subtype");
        if (typeSelect) {
            typeSelect.required = qty > 0;
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
            subSelect.disabled = !typeId || available === 0;
            subSelect.required = qty > 0 && available > 0;
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

    function flagMissingSubTypes(form) {
        var missing = 0;
        Array.prototype.forEach.call(form.querySelectorAll(".am-po-subtype"), function (sel) {
            if (sel.required && !sel.disabled && !sel.value) {
                sel.classList.add("is-invalid");
                missing++;
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
            updateLine(card);
        });
        form.addEventListener("submit", function (e) {
            if (flagMissingSubTypes(form) > 0) {
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
