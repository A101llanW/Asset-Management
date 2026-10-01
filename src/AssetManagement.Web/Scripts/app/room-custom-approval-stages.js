/* eslint-env browser */
(function () {
    function parseIntSafe(value, fallback) {
        var parsed = parseInt(value, 10);
        return isNaN(parsed) ? fallback : parsed;
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

    function initRoomCustomApprovalStages() {
        var root = getCustomStagesRoot();
        var countSelect = document.getElementById("custom-flow-stage-count");
        if (!root || !countSelect) {
            return;
        }

        var processEl = getProcessEl(root);
        forceRequiresApproval(processEl);
        setStageCount(processEl, parseIntSafe(countSelect.value, 1));
        syncStageCountSelect(processEl, countSelect);

        countSelect.addEventListener("change", function () {
            var target = parseIntSafe(countSelect.value, 1);
            setStageCount(processEl, target);
            syncStageCountSelect(processEl, countSelect);
            forceRequiresApproval(processEl);
        });
    }

    function scheduleInit() {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", initRoomCustomApprovalStages);
            return;
        }

        window.setTimeout(initRoomCustomApprovalStages, 0);
    }

    scheduleInit();
})();
