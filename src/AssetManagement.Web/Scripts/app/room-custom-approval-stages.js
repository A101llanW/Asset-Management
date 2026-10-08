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
        var before = list ? list.children.length : 0;

        while (list && list.children.length < desired && addButton && !addButton.disabled) {
            addButton.click();
        }

        // Trim from the END: lowering the count drops the last stage(s) and keeps the earlier stages'
        // roles/persons. (It used to click the first enabled Remove button, which dropped Stage 1.)
        while (list && list.children.length > desired) {
            var lastStage = list.lastElementChild;
            var removeButton = lastStage ? lastStage.querySelector(".am-approval-remove-stage") : null;
            var countBefore = list.children.length;
            if (!removeButton || removeButton.disabled) {
                break;
            }
            removeButton.click();
            if (list.children.length >= countBefore) {
                break; // the matrix did not remove it; never loop forever
            }
        }

        // The count select sits outside .am-approval-process, so recompute the grey stage summary here
        // from the process itself once the rows have changed.
        if (list && list.children.length !== before && window.AmPersonPicker && window.AmPersonPicker.refreshStageSummary) {
            window.AmPersonPicker.refreshStageSummary(processEl);
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

        // A row's own Remove (or Add stage) changes the number of stages too: keep the count select in step
        // (it kept showing the old number after a row was removed).
        var root = getCustomStagesRoot();
        if (root) {
            root.addEventListener("click", function (event) {
                if (event.target && event.target.closest && event.target.closest(".am-approval-add-stage, .am-approval-remove-stage")) {
                    window.setTimeout(function () {
                        syncStageCountSelect(getProcessEl(getCustomStagesRoot()), countSelect);
                    }, 0);
                }
            });
        }
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
        var inheritPoSummary = document.getElementById("requisition-inherit-po-summary");
        if (inheritPoSummary) {
            inheritPoSummary.style.display = useCustom ? "none" : "";
        }
        if (useCustom) {
            syncMatrixFromCountSelect();
        }
    }

    // ---- Live "Requisition flow" / "Purchase order raised by" summary at the top of the panel (client-side only) ----
    var liveInitial = null;
    var inheritInitial = null;

    function inheritSourceValue() {
        var select = document.getElementById("requisition-inherit-source");
        return select ? (select.value || "") : "";
    }

    function textOf(select) {
        if (!select || !select.value || select.selectedIndex < 0) {
            return "";
        }
        return (select.options[select.selectedIndex].textContent || "").trim();
    }

    function parsePreviews(box) {
        if (!box.__amPreviews) {
            try {
                box.__amPreviews = JSON.parse(box.getAttribute("data-inherit-previews") || "[]") || [];
            } catch (e) {
                box.__amPreviews = [];
            }
        }
        return box.__amPreviews;
    }

    function customPurchaseOrderText() {
        var role = document.getElementById("PurchaseOrderRaiserRoleId");
        var person = document.getElementById("PurchaseOrderRaiserUserId");
        var roleName = textOf(role);
        if (!roleName) {
            return "PO: any user with Purchases permission";
        }
        var personName = textOf(person);
        return "PO: " + roleName + (personName ? " \u2014 " + personName : " (any user in role)");
    }

    function syncLiveSummary() {
        var box = document.getElementById("requisition-flow-live-summary");
        if (!box) {
            return;
        }
        var flowEl = box.querySelector("[data-am-live-flow]");
        var poEl = box.querySelector("[data-am-live-po]");
        var unsavedEl = box.querySelector("[data-am-live-unsaved]");
        var customRadio = document.getElementById("requisition-approval-custom");
        var flow;
        var po;

        if (customRadio && customRadio.checked) {
            var processEl = getProcessEl(getCustomStagesRoot());
            var stages = window.AmPersonPicker && window.AmPersonPicker.stageSummaryText
                ? window.AmPersonPicker.stageSummaryText(processEl)
                : "";
            flow = "Custom: " + (stages || "No approval stages configured.");
            po = customPurchaseOrderText();
        } else {
            var inheritSelect = document.getElementById("requisition-inherit-source");
            var stale = document.getElementById("requisition-inherit-source-stale");
            var isStale = stale && !stale.classList.contains("d-none");
            if (isStale) {
                flow = "Inherit: recalculated from the new parent when you save.";
                po = "PO: from the unit the flow is inherited from (recalculated when you save).";
            } else {
                var choice = inheritSelect && !inheritSelect.disabled ? (inheritSelect.value || "") : "";
                var previews = parsePreviews(box);
                var match = null;
                for (var i = 0; i < previews.length; i++) {
                    if ((previews[i].Choice || "") === choice) {
                        match = previews[i];
                        break;
                    }
                }
                flow = match && match.FlowSummary ? match.FlowSummary : (flowEl ? flowEl.textContent : "");
                po = match && match.PurchaseOrderRaiserSummary ? match.PurchaseOrderRaiserSummary : (poEl ? poEl.textContent : "");
            }
            var effectivePo = document.querySelector("#requisition-inherit-po-summary [data-am-effective-po]");
            if (effectivePo && po) {
                effectivePo.textContent = po.replace(/^PO:\s*/, "");
            }
        }

        if (flowEl) {
            flowEl.textContent = flow;
        }
        if (poEl) {
            poEl.textContent = po;
        }
        var current = flow + "|" + po;
        var inheritNow = inheritSourceValue();
        if (liveInitial === null) {
            liveInitial = current;
            inheritInitial = inheritNow;
        }
        if (unsavedEl) {
            // Also "changed" when the inherit source was switched but resolves to the same flow.
            unsavedEl.classList.toggle("d-none", current === liveInitial && inheritNow === inheritInitial);
        }
    }

    function scheduleLiveSummary() {
        window.setTimeout(syncLiveSummary, 0);
    }

    function wireLiveSummary() {
        var box = document.getElementById("requisition-flow-live-summary");
        if (!box || box.getAttribute("data-am-live-wired") === "true") {
            return;
        }
        box.setAttribute("data-am-live-wired", "true");
        var form = box.closest("form") || document;
        // Any change in the form (mode, inherit source, stage roles/persons, stage count, PO raiser, kind/parent)
        // and stage add/remove clicks; runs after the other handlers (person auto-select, stale flag).
        form.addEventListener("change", function () { window.setTimeout(syncLiveSummary, 10); });
        form.addEventListener("click", function (event) {
            if (event.target && event.target.closest && event.target.closest(".am-approval-add-stage, .am-approval-remove-stage")) {
                window.setTimeout(syncLiveSummary, 10);
            }
        });
        // First render after the person pickers have filled (they initialise on DOMContentLoaded).
        var first = function () {
            if (window.AmPersonPicker && window.AmPersonPicker.refreshStageSummary) {
                window.AmPersonPicker.refreshStageSummary(getProcessEl(getCustomStagesRoot()));
            }
            liveInitial = null;
            syncLiveSummary();
        };
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", function () { window.setTimeout(first, 0); });
        } else {
            window.setTimeout(first, 0);
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
        wireLiveSummary();

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
