(function () {
    "use strict";
    function parseIntSafe(value, fallback) {
        var parsed = parseInt(String(value).trim(), 10);
        return isNaN(parsed) ? fallback : parsed;
    }
    function init() {
        var form = document.getElementById("receive-form");
        var modalEl = document.getElementById("receiveSerialWizardModal");
        if (!form || !modalEl || form.getAttribute("data-am-receive-mode") !== "true") {
            return;
        }
        var openBtn = document.getElementById("receive-open-serial-wizard");
        var submitBtn = document.getElementById("receive-submit-btn");
        var qtyInput = document.getElementById("receive-quantity-input")
            || form.querySelector('input[name="QuantityReceived"]');
        var conditionInput = document.getElementById("ConditionOnReceipt");
        var hiddenContainer = document.getElementById("receive-serial-hidden-fields");
        var statusEl = document.getElementById("receive-serial-status");
        var serialInput = document.getElementById("receive-serial-input");
        var nextBtn = document.getElementById("receive-serial-next");
        var backBtn = document.getElementById("receive-serial-back");
        var errorEl = document.getElementById("receive-serial-error");
        if (!openBtn || !hiddenContainer || !serialInput || !qtyInput) {
            return;
        }
        var modal = window.bootstrap && window.bootstrap.Modal
            ? new window.bootstrap.Modal(modalEl)
            : null;
        var capturedSerials = [];
        var currentIndex = 0;
        function getRemainingCap() {
            return parseIntSafe(modalEl.getAttribute("data-am-remaining"), 1);
        }
        function readQuantityFromField() {
            return Math.max(1, parseIntSafe(qtyInput.value, 1));
        }
        function clampQuantityField() {
            var qty = readQuantityFromField();
            var cap = getRemainingCap();
            if (qty > cap) {
                qtyInput.value = String(cap);
                qty = cap;
            }
            return qty;
        }
        function formatCost() {
            var cost = modalEl.getAttribute("data-am-unit-cost") || "";
            var currency = modalEl.getAttribute("data-am-currency") || "";
            if (!cost) {
                return "—";
            }
            return currency ? currency + " " + cost : cost;
        }
        function fillContextPanel() {
            document.getElementById("receive-serial-po").textContent = modalEl.getAttribute("data-am-po") || "—";
            document.getElementById("receive-serial-supplier").textContent = modalEl.getAttribute("data-am-supplier") || "—";
            var item = modalEl.getAttribute("data-am-item") || modalEl.getAttribute("data-am-asset-name") || "—";
            document.getElementById("receive-serial-item").textContent = item;
            document.getElementById("receive-serial-brand").textContent = modalEl.getAttribute("data-am-brand") || "—";
            document.getElementById("receive-serial-model").textContent = modalEl.getAttribute("data-am-model") || "—";
            document.getElementById("receive-serial-subtype").textContent = modalEl.getAttribute("data-am-subtype") || "—";
            document.getElementById("receive-serial-cost").textContent = formatCost();
        }
        function resizeCapturedSerials(targetCount) {
            var next = [];
            var i;
            for (i = 0; i < targetCount; i += 1) {
                next[i] = capturedSerials[i] ? capturedSerials[i].trim() : "";
            }
            capturedSerials = next;
        }
        function updateStepUi(totalUnits) {
            document.getElementById("receive-serial-step-label").textContent = "Unit " + (currentIndex + 1) + " of " + totalUnits;
            serialInput.value = capturedSerials[currentIndex] || "";
            serialInput.classList.remove("is-invalid");
            if (errorEl) {
                errorEl.textContent = "";
            }
            backBtn.disabled = currentIndex === 0;
            nextBtn.textContent = currentIndex === totalUnits - 1 ? "Done" : "Next";
            window.setTimeout(function () { serialInput.focus(); }, 150);
        }
        function renderHiddenFields() {
            hiddenContainer.innerHTML = "";
            capturedSerials.forEach(function (serial, index) {
                if (!serial) {
                    return;
                }
                var input = document.createElement("input");
                input.type = "hidden";
                input.name = "NewAssetUnits[" + index + "].SerialNumber";
                input.value = serial;
                hiddenContainer.appendChild(input);
            });
        }
        function serialsAreComplete(expectedCount) {
            if (expectedCount < 1 || capturedSerials.length !== expectedCount) {
                return false;
            }
            var i;
            for (i = 0; i < expectedCount; i += 1) {
                if (!capturedSerials[i] || !capturedSerials[i].trim()) {
                    return false;
                }
            }
            return true;
        }
        function hasAnySerial() {
            var i;
            for (i = 0; i < capturedSerials.length; i += 1) {
                if (capturedSerials[i] && capturedSerials[i].trim()) {
                    return true;
                }
            }
            return false;
        }
        function updateStatus() {
            if (!statusEl) {
                return;
            }
            var expected = clampQuantityField();
            if (serialsAreComplete(expected)) {
                statusEl.innerHTML = "<span class=\"text-success\">Serial numbers captured for all " + expected + " unit(s).</span>";
            } else if (hasAnySerial()) {
                statusEl.innerHTML = "<span class=\"text-warning\">Finish entering serial numbers for all units, or clear them to receive without serials.</span>";
            } else {
                statusEl.innerHTML = "<span class=\"text-muted\">Serial numbers are optional. Submit without them to create assets with tags only.</span>";
            }
        }
        function resetCapture() {
            capturedSerials = [];
            currentIndex = 0;
            renderHiddenFields();
            updateStatus();
        }
        function validateCurrentSerial() {
            var value = (serialInput.value || "").trim();
            if (!value) {
                serialInput.classList.add("is-invalid");
                if (errorEl) {
                    errorEl.textContent = "Serial number is required for this step, or cancel and submit without serials.";
                }
                return null;
            }
            var duplicateIndex = -1;
            var i;
            for (i = 0; i < capturedSerials.length; i += 1) {
                if (i !== currentIndex && capturedSerials[i] && capturedSerials[i].toLowerCase() === value.toLowerCase()) {
                    duplicateIndex = i;
                    break;
                }
            }
            if (duplicateIndex >= 0) {
                serialInput.classList.add("is-invalid");
                if (errorEl) {
                    errorEl.textContent = "This serial number was already entered for unit " + (duplicateIndex + 1) + ".";
                }
                return null;
            }
            serialInput.classList.remove("is-invalid");
            if (errorEl) {
                errorEl.textContent = "";
            }
            return value;
        }
        openBtn.addEventListener("click", function () {
            if (!conditionInput || !conditionInput.value) {
                if (conditionInput) {
                    conditionInput.classList.add("is-invalid");
                    conditionInput.focus();
                }
                return;
            }
            conditionInput.classList.remove("is-invalid");
            var totalUnits = clampQuantityField();
            resizeCapturedSerials(totalUnits);
            currentIndex = 0;
            fillContextPanel();
            updateStepUi(totalUnits);
            if (modal) {
                modal.show();
            }
        });
        nextBtn.addEventListener("click", function () {
            var totalUnits = clampQuantityField();
            resizeCapturedSerials(totalUnits);
            if (currentIndex >= totalUnits) {
                currentIndex = Math.max(0, totalUnits - 1);
            }
            var serial = validateCurrentSerial();
            if (!serial) {
                return;
            }
            capturedSerials[currentIndex] = serial;
            if (currentIndex < totalUnits - 1) {
                currentIndex += 1;
                updateStepUi(totalUnits);
                return;
            }
            renderHiddenFields();
            updateStatus();
            if (modal) {
                modal.hide();
            }
        });
        backBtn.addEventListener("click", function () {
            var totalUnits = clampQuantityField();
            resizeCapturedSerials(totalUnits);
            var serial = (serialInput.value || "").trim();
            if (serial) {
                capturedSerials[currentIndex] = serial;
            }
            if (currentIndex > 0) {
                currentIndex -= 1;
                updateStepUi(totalUnits);
            }
        });
        serialInput.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                nextBtn.click();
            }
        });
        qtyInput.addEventListener("change", resetCapture);
        qtyInput.addEventListener("input", resetCapture);
        form.addEventListener("submit", function (event) {
            var expected = clampQuantityField();
            if (hasAnySerial() && !serialsAreComplete(expected)) {
                event.preventDefault();
                updateStatus();
                if (statusEl) {
                    statusEl.innerHTML = "<span class=\"text-danger\">Finish serial numbers for all " + expected + " unit(s), or clear them before submitting.</span>";
                }
                openBtn.focus();
            }
        });
        if (submitBtn) {
            submitBtn.disabled = false;
        }
        updateStatus();
    }
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
