(function () {
    var form = document.querySelector("[data-am-document-upload-form]");
    var typeSelect = document.getElementById("DocumentTypeSelect");
    var typeHidden = document.getElementById("DocumentType");
    var customWrap = document.getElementById("DocumentTypeCustomWrap");
    var customInput = document.getElementById("DocumentTypeCustom");
    var photoWrap = document.getElementById("DocumentPhotoNameWrap");
    var photoInput = document.getElementById("DocumentPhotoName");
    var documentsTab = document.querySelector("[data-am-documents-tab]");
    var requirementModal = document.getElementById("assetDocumentRequirementUploadModal");
    var requirementForm = document.querySelector("[data-am-requirement-upload-form]");
    var requirementIdInput = document.getElementById("RequirementId");
    var requirementSummary = document.getElementById("RequirementUploadSummary");

    if (form && typeSelect && typeHidden) {
        var customValue = "__custom__";

        function selectedOption() {
            return typeSelect.options[typeSelect.selectedIndex];
        }

        function isCustomSelected() {
            return typeSelect.value === customValue;
        }

        function requiresPhotoName() {
            var option = selectedOption();
            return !!(option && option.getAttribute("data-am-requires-photo-name") === "true");
        }

        function syncFieldVisibility() {
            var showCustom = isCustomSelected();
            var showPhotoName = !showCustom && requiresPhotoName();

            if (customWrap) {
                customWrap.hidden = !showCustom;
            }

            if (customInput) {
                customInput.required = showCustom;
                if (!showCustom) {
                    customInput.value = "";
                }
            }

            if (photoWrap) {
                photoWrap.hidden = !showPhotoName;
            }

            if (photoInput) {
                photoInput.required = showPhotoName;
                if (!showPhotoName) {
                    photoInput.value = "";
                } else {
                    photoInput.focus();
                }
            }
        }

        function resolveDocumentType() {
            if (isCustomSelected()) {
                return customInput ? customInput.value.trim() : "";
            }

            var baseType = typeSelect.value.trim();
            if (!baseType) {
                return "";
            }

            if (requiresPhotoName()) {
                return photoInput ? photoInput.value.trim() : "";
            }

            return baseType;
        }

        typeSelect.addEventListener("change", syncFieldVisibility);

        form.addEventListener("submit", function (event) {
            var documentType = resolveDocumentType();
            if (!documentType) {
                event.preventDefault();
                if (isCustomSelected() && customInput) {
                    customInput.focus();
                } else if (requiresPhotoName() && photoInput) {
                    photoInput.focus();
                } else {
                    typeSelect.focus();
                }
                return;
            }

            typeHidden.value = documentType;
        });

        var modal = document.getElementById("assetDocumentUploadModal");
        if (modal) {
            modal.addEventListener("hidden.bs.modal", function () {
                form.reset();
                typeHidden.value = "";
                syncFieldVisibility();
            });
            modal.addEventListener("shown.bs.modal", function () {
                typeSelect.focus();
            });
        }

        syncFieldVisibility();
    }

    if (requirementModal && requirementForm && requirementIdInput) {
        requirementModal.addEventListener("show.bs.modal", function (event) {
            var trigger = event.relatedTarget;
            if (!trigger) {
                return;
            }

            var requirementId = trigger.getAttribute("data-am-requirement-id");
            var documentType = trigger.getAttribute("data-am-document-type") || "Process photo";
            var processLabel = trigger.getAttribute("data-am-process-label") || "Linked process";
            requirementIdInput.value = requirementId || "";
            if (requirementSummary) {
                requirementSummary.textContent = processLabel + " — " + documentType;
            }
        });

        requirementModal.addEventListener("hidden.bs.modal", function () {
            requirementForm.reset();
            requirementIdInput.value = "";
            if (requirementSummary) {
                requirementSummary.textContent = "";
            }
        });
    }

    if (documentsTab && requirementModal && requirementIdInput) {
        var pendingRequirementId = documentsTab.getAttribute("data-am-pending-requirement-id");
        if (pendingRequirementId) {
            var uploadButton = documentsTab.querySelector('[data-am-requirement-id="' + pendingRequirementId + '"]');
            if (uploadButton && window.bootstrap && window.bootstrap.Modal) {
                uploadButton.click();
            }
        }
    }
})();
