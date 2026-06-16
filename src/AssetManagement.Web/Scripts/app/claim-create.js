(function () {
    function byId(id) {
        return document.getElementById(id);
    }

    var form = document.querySelector("[data-am-claim-create-form]");
    var select = byId("ClaimTypeSelect");
    var hiddenType = byId("ClaimType");
    var modal = byId("otherClaimTypeModal");
    if (!form || !select || !hiddenType || !modal) {
        return;
    }

    var customInput = byId("OtherClaimTypeText");
    var confirmBtn = byId("otherClaimTypeConfirm");
    var summary = byId("otherClaimTypeSummary");
    var summaryText = byId("otherClaimTypeSummaryText");
    var modalInstance = window.bootstrap && window.bootstrap.Modal
        ? window.bootstrap.Modal.getOrCreateInstance(modal)
        : null;

    var previousValue = select.value || "";
    var customClaimType = (form.getAttribute("data-am-other-claim-type") || "").trim();

    function syncHiddenClaimType() {
        if (select.value === "Other") {
            hiddenType.value = customClaimType;
            return;
        }

        hiddenType.value = select.value;
        customClaimType = "";
        if (summary) {
            summary.hidden = true;
        }
    }

    function showCustomSummary(text) {
        if (!summary || !summaryText) {
            return;
        }

        summaryText.textContent = text;
        summary.hidden = !text;
    }

    function openOtherModal() {
        if (customInput) {
            customInput.value = customClaimType;
        }

        if (modalInstance) {
            modalInstance.show();
            if (customInput) {
                window.setTimeout(function () {
                    customInput.focus();
                }, 200);
            }
        }
    }

    if (select.value === "Other" && customClaimType) {
        showCustomSummary(customClaimType);
        syncHiddenClaimType();
    } else {
        syncHiddenClaimType();
    }

    select.addEventListener("change", function () {
        if (select.value === "Other") {
            openOtherModal();
            return;
        }

        previousValue = select.value;
        syncHiddenClaimType();
    });

    if (confirmBtn) {
        confirmBtn.addEventListener("click", function () {
            var text = customInput ? customInput.value.trim() : "";
            if (!text) {
                if (customInput) {
                    customInput.focus();
                }
                return;
            }

            customClaimType = text;
            showCustomSummary(text);
            syncHiddenClaimType();
            if (modalInstance) {
                modalInstance.hide();
            }
        });
    }

    modal.addEventListener("hidden.bs.modal", function () {
        if (select.value !== "Other") {
            return;
        }

        if (!customClaimType) {
            select.value = previousValue || "";
            syncHiddenClaimType();
        }
    });

    form.addEventListener("submit", function (event) {
        if (select.value === "Other") {
            if (!customClaimType) {
                event.preventDefault();
                openOtherModal();
                return;
            }

            hiddenType.value = customClaimType;
            return;
        }

        hiddenType.value = select.value;
    });
})();
