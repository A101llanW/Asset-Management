(function () {
    "use strict";

    var config = window.amPriceApplyConfig || {};
    var modalEl = document.getElementById("amAcquisitionCostApplyModal");
    if (!modalEl) {
        return;
    }

    var form = document.querySelector("form");
    var costInput = document.getElementById("AcquisitionCost");
    var previousCostInput = document.getElementById("PreviousAcquisitionCost");
    var scopeInput = document.getElementById("AcquisitionCostApplyScope");
    var subTypeRadio = document.getElementById("amPriceApplySubType");
    var filteredRadio = document.getElementById("amPriceApplyFiltered");
    var filterPanel = document.getElementById("amPriceApplyFilterPanel");
    var confirmBtn = document.getElementById("amPriceApplyConfirmBtn");
    var amountLabel = document.getElementById("amPriceApplyAmountLabel");
    var subTypeCount = document.getElementById("amPriceApplySubTypeCount");
    var filteredCount = document.getElementById("amPriceApplyFilteredCount");
    var modal = window.bootstrap && window.bootstrap.Modal ? new window.bootstrap.Modal(modalEl) : null;
    var pendingSubmit = false;

    function parseCost(value) {
        if (!value) {
            return 0;
        }
        var normalized = String(value).replace(/,/g, "").trim();
        var parsed = parseFloat(normalized);
        return isNaN(parsed) ? 0 : parsed;
    }

    function formatCost(value) {
        return value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function getSelectedScope() {
        var selected = document.querySelector('input[name="amPriceApplyScopeChoice"]:checked');
        return selected ? selected.value : "individual";
    }

    function readFilterParams() {
        var params = new URLSearchParams();
        params.set("id", String(config.assetId || 0));
        params.set("scope", getSelectedScope());

        var fields = document.querySelectorAll(".am-price-apply-filter");
        for (var i = 0; i < fields.length; i++) {
            var field = fields[i];
            if (!field.name || !field.value) {
                continue;
            }
            params.set(field.name, field.value);
        }

        if (getSelectedScope() === "subtype") {
            var subTypeField = document.getElementById("AssetSubTypeId");
            if (subTypeField && subTypeField.value) {
                params.set("assetSubTypeId", subTypeField.value);
            }
        }

        return params;
    }

    function refreshPreview() {
        if (!config.previewUrl) {
            return;
        }

        var scope = getSelectedScope();
        if (scope === "individual") {
            return;
        }

        var params = readFilterParams();
        fetch(config.previewUrl + "?" + params.toString(), { credentials: "same-origin" })
            .then(function (response) { return response.json(); })
            .then(function (data) {
                var label = scope === "subtype" ? subTypeCount : filteredCount;
                if (!label) {
                    return;
                }
                if (!data || !data.success) {
                    label.textContent = data && data.message ? data.message : "";
                    return;
                }
                label.textContent = data.count + " asset(s) will be updated.";
            })
            .catch(function () {
                if (filteredCount) {
                    filteredCount.textContent = "Unable to preview matches.";
                }
            });
    }

    function refreshSubTypeAvailability() {
        var subTypeField = document.getElementById("AssetSubTypeId");
        var subTypeNameField = document.getElementById("AssetSubTypeName");
        config.hasSubType = !!(subTypeField && subTypeField.value);
        if (subTypeRadio) {
            subTypeRadio.disabled = !config.hasSubType;
        }
        var subTypeLabel = document.getElementById("amPriceApplySubTypeLabel");
        if (subTypeLabel) {
            subTypeLabel.textContent = subTypeNameField && subTypeNameField.value
                ? subTypeNameField.value
                : (config.hasSubType ? "selected sub-type" : "(not set)");
        }
    }

    function syncScopeUi() {
        refreshSubTypeAvailability();
        var scope = getSelectedScope();
        if (filterPanel) {
            filterPanel.style.display = scope === "filtered" ? "" : "none";
        }
        refreshPreview();
    }

    function shouldPromptForApply() {
        if (!costInput || !config.assetId) {
            return false;
        }
        var current = parseCost(costInput.value);
        if (current <= 0) {
            return false;
        }
        if (!previousCostInput) {
            return false;
        }
        var previous = parseCost(previousCostInput.value);
        return Math.abs(current - previous) > 0.001;
    }

    function stripMonetaryInputs(targetForm) {
        if (window.AssetMonetaryInput && typeof window.AssetMonetaryInput.stripForSubmit === "function") {
            window.AssetMonetaryInput.stripForSubmit(targetForm);
            return;
        }

        if (!targetForm) {
            return;
        }

        var inputs = targetForm.querySelectorAll("[data-monetary-input]");
        for (var i = 0; i < inputs.length; i++) {
            inputs[i].value = String(inputs[i].value || "").replace(/,/g, "").trim();
        }
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
            stripMonetaryInputs(form);
            form.submit();
        }
    }

    if (form) {
        form.addEventListener("submit", function (event) {
            if (pendingSubmit) {
                return;
            }
            if (!shouldPromptForApply()) {
                if (scopeInput) {
                    scopeInput.value = "individual";
                }
                stripMonetaryInputs(form);
                return;
            }
            event.preventDefault();
            if (amountLabel && costInput) {
                amountLabel.textContent = formatCost(parseCost(costInput.value));
            }
            syncScopeUi();
            if (modal) {
                modal.show();
            } else {
                applyScopeAndSubmit();
            }
        });
    }

    document.querySelectorAll('input[name="amPriceApplyScopeChoice"]').forEach(function (radio) {
        radio.addEventListener("change", syncScopeUi);
    });

    document.querySelectorAll(".am-price-apply-filter").forEach(function (field) {
        field.addEventListener("change", refreshPreview);
    });

    if (confirmBtn) {
        confirmBtn.addEventListener("click", applyScopeAndSubmit);
    }
})();
