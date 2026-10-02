/* eslint-env browser */
(function () {
    function parseIntSafe(value, fallback) {
        var parsed = parseInt(value, 10);
        return isNaN(parsed) ? fallback : parsed;
    }

    function getCustomPanel() {
        return document.getElementById("custom-flow-approval-custom-panel");
    }

    function getCustomStagesRoot() {
        return document.getElementById("custom-flow-stages");
    }

    function getProcessEl(root) {
        if (!root) {
            return null;
        }
        return root.querySelector(".am-approval-process");
    }

    function getStagesList(processEl) {
        return processEl ? processEl.querySelector(".am-approval-stages-list") : null;
    }

    function getStageCount(processEl) {
        var list = getStagesList(processEl);
        return list ? list.children.length : 0;
    }

    function forceRequiresApproval(processEl) {
        if (!processEl) {
            return;
        }
        var checkbox = processEl.querySelector(".am-approval-requires-approval");
        var panel = processEl.querySelector(".am-approval-stages-panel");
        if (checkbox) {
            checkbox.checked = true;
        }
        if (panel) {
            panel.classList.remove("d-none");
        }
    }

    function setStageCount(processEl, targetCount) {
        if (!processEl) {
            return;
        }

        var maxStages = parseIntSafe(processEl.getAttribute("data-max-stages"), 10);
        var desired = Math.max(1, Math.min(maxStages, targetCount));
        var addButton = processEl.querySelector(".am-approval-add-stage");
        var list = getStagesList(processEl);

        while (list && list.children.length < desired && addButton && !addButton.disabled) {
            addButton.click();
        }

        while (list && list.children.length > desired) {
            var removeButton = list.querySelector(".am-approval-remove-stage:not([disabled])");
            if (!removeButton) {
                break;
            }
            removeButton.click();
        }
    }

    function syncStageCountSelect(processEl, countSelect) {
        if (!countSelect || !processEl) {
            return;
        }
        countSelect.value = String(getStageCount(processEl));
    }

    function syncMatrixFromCountSelect() {
        var root = getCustomStagesRoot();
        var countSelect = document.getElementById("custom-flow-stage-count");
        if (!root || !countSelect) {
            return;
        }

        var processEl = getProcessEl(root);
        forceRequiresApproval(processEl);
        setStageCount(processEl, parseIntSafe(countSelect.value, 1));
        syncStageCountSelect(processEl, countSelect);
    }

    function wireStageCountSelect() {
        var countSelect = document.getElementById("custom-flow-stage-count");
        if (!countSelect || countSelect.getAttribute("data-am-room-stage-count-wired") === "true") {
            return;
        }

        countSelect.setAttribute("data-am-room-stage-count-wired", "true");
        countSelect.addEventListener("change", syncMatrixFromCountSelect);
    }

    function syncApprovalMode() {
        var orgRadio = document.getElementById("requisition-approval-org");
        var customRadio = document.getElementById("requisition-approval-custom");
        var hiddenFlag = document.getElementById("use-custom-requisition-approval");
        var customPanel = getCustomPanel();
        var useCustom = customRadio && customRadio.checked;

        if (hiddenFlag) {
            hiddenFlag.value = useCustom ? "true" : "false";
        }
        if (customPanel) {
            customPanel.classList.toggle("d-none", !useCustom);
        }
        if (useCustom) {
            syncMatrixFromCountSelect();
        }
    }

    function syncPanelVisibility() {
        var approvalPanel = document.getElementById("room-requisition-approval-panel");
        var requisitionTarget = document.getElementById("IsRequisitionTarget");
        if (!approvalPanel || !requisitionTarget) {
            return;
        }
        approvalPanel.classList.toggle("d-none", !requisitionTarget.checked);
    }

    function initRoomCustomApprovalStages() {
        var approvalPanel = document.getElementById("room-requisition-approval-panel");
        if (!approvalPanel) {
            return;
        }

        wireStageCountSelect();
        syncApprovalMode();
        syncPanelVisibility();
        syncMatrixFromCountSelect();

        var orgRadio = document.getElementById("requisition-approval-org");
        var customRadio = document.getElementById("requisition-approval-custom");
        var requisitionTarget = document.getElementById("IsRequisitionTarget");

        if (orgRadio && orgRadio.getAttribute("data-am-room-approval-wired") !== "true") {
            orgRadio.setAttribute("data-am-room-approval-wired", "true");
            orgRadio.addEventListener("change", syncApprovalMode);
        }
        if (customRadio && customRadio.getAttribute("data-am-room-approval-wired") !== "true") {
            customRadio.setAttribute("data-am-room-approval-wired", "true");
            customRadio.addEventListener("change", syncApprovalMode);
        }
        if (requisitionTarget && requisitionTarget.getAttribute("data-am-room-approval-wired") !== "true") {
            requisitionTarget.setAttribute("data-am-room-approval-wired", "true");
            requisitionTarget.addEventListener("change", syncPanelVisibility);
        }
    }

    function scheduleInit() {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", function () {
                window.setTimeout(initRoomCustomApprovalStages, 0);
            });
            return;
        }

        window.setTimeout(initRoomCustomApprovalStages, 0);
    }

    window.AmRoomCustomApprovalStages = {
        init: initRoomCustomApprovalStages
    };

    scheduleInit();
})();
