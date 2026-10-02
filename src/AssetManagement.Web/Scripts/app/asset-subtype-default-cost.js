(function () {
    "use strict";

    var form = document.querySelector("form");
    var costInput = document.getElementById("DefaultAcquisitionCost");
    var previousCostInput = document.getElementById("PreviousDefaultAcquisitionCost");
    var scopeInput = document.getElementById("AcquisitionCostApplyScope");
    var modalEl = document.getElementById("amSubTypeDefaultCostModal");
    var confirmBtn = document.getElementById("amSubTypeDefaultCostConfirmBtn");
    var assetCountLabel = document.getElementById("amSubTypeDefaultCostAssetCount");
    var modal = modalEl && window.bootstrap && window.bootstrap.Modal
        ? new window.bootstrap.Modal(modalEl)
        : null;
    var pendingSubmit = false;

    function parseCost(value) {
        if (!value) {
            return null;
        }
        var normalized = String(value).replace(/,/g, "").trim();
        if (!normalized) {
            return null;
        }
        var parsed = parseFloat(normalized);
        return isNaN(parsed) ? null : parsed;
    }

    function costsDiffer(current, previous) {
        if (current == null && previous == null) {
            return false;
        }
        if (current == null || previous == null) {
            return true;
        }
        return Math.abs(current - previous) > 0.001;
    }

    function shouldPrompt() {
        var current = parseCost(costInput ? costInput.value : null);
        if (current == null || current <= 0) {
            return false;
        }
        var previous = parseCost(previousCostInput ? previousCostInput.value : null);
        return costsDiffer(current, previous);
    }

    function getSelectedScope() {
        var selected = document.querySelector('input[name="amSubTypeDefaultCostScope"]:checked');
        return selected && selected.value === "subtype" ? "subtype" : "individual";
    }

    function applyScopeAndSubmit() {
        if (scopeInput) {
            scopeInput.value = getSelectedScope();
        }
        pendingSubmit = true;
        if (modal) {
            modal.hide();
        }
        if (form) {
            form.submit();
        }
    }

    if (form) {
        form.addEventListener("submit", function (event) {
            if (pendingSubmit) {
                return;
            }
            if (!shouldPrompt()) {
                if (scopeInput) {
                    scopeInput.value = "individual";
                }
                return;
            }
            if (assetCountLabel && window.amSubTypeDefaultCostConfig) {
                var count = window.amSubTypeDefaultCostConfig.assetCount || 0;
                assetCountLabel.textContent = count > 0
                    ? count + " active asset(s) can be updated."
                    : "No other active assets are linked to this sub-type yet.";
            }
            if (modal) {
                event.preventDefault();
                modal.show();
            }
        });
    }

    if (confirmBtn) {
        confirmBtn.addEventListener("click", applyScopeAndSubmit);
    }
})();
