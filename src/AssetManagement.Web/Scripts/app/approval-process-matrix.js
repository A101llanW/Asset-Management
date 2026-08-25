/* eslint-env browser */

(function () {

    function parseIntSafe(value, fallback) {

        var parsed = parseInt(value, 10);

        return isNaN(parsed) ? fallback : parsed;

    }



    function cloneSelectOptions(sourceSelect, targetSelect, selectedValue) {

        if (!sourceSelect || !targetSelect) {

            return;

        }



        targetSelect.innerHTML = sourceSelect.innerHTML;

        if (selectedValue) {

            targetSelect.value = selectedValue;

        }

    }



    function usesApproverUserPicker(matrixEl) {

        return matrixEl && matrixEl.getAttribute("data-use-approver-user-picker") === "true";

    }



    function getApproverOptions(matrixEl) {

        if (!window.ApprovalApproverPicker) {

            return [];

        }



        return window.ApprovalApproverPicker.parseOptions(matrixEl.getAttribute("data-approver-options"));

    }



    function reindexProcess(processEl, fieldPrefix, useUserPicker) {

        var processIndex = processEl.getAttribute("data-process-index");

        var stages = processEl.querySelectorAll(".am-approval-stage");

        stages.forEach(function (stageEl, stageIndex) {

            var label = stageEl.querySelector(".am-approval-stage-label");

            if (label) {

                label.textContent = "Stage " + (stageIndex + 1);

            }



            if (useUserPicker) {

                var roleInput = stageEl.querySelector(".am-approver-role-input");

                var userInput = stageEl.querySelector(".am-approver-user-input");

                if (roleInput) {

                    roleInput.name = fieldPrefix + "[" + processIndex + "].Stages[" + stageIndex + "].RoleId";

                }

                if (userInput) {

                    userInput.name = fieldPrefix + "[" + processIndex + "].Stages[" + stageIndex + "].UserId";

                }

            } else {

                var select = stageEl.querySelector("select");

                if (select) {

                    select.name = fieldPrefix + "[" + processIndex + "].Stages[" + stageIndex + "].RoleId";

                }

            }



            var removeButton = stageEl.querySelector(".am-approval-remove-stage");

            if (removeButton) {

                removeButton.disabled = stages.length <= 1;

            }

        });



        var addButton = processEl.querySelector(".am-approval-add-stage");

        if (addButton) {

            addButton.disabled = stages.length >= parseIntSafe(processEl.getAttribute("data-max-stages"), 10);

        }

    }



    function toggleProcessStages(processEl) {

        var requiresApproval = processEl.querySelector(".am-approval-requires-approval");

        var stagesPanel = processEl.querySelector(".am-approval-stages-panel");

        if (!requiresApproval || !stagesPanel) {

            return;

        }



        var enabled = requiresApproval.checked;

        stagesPanel.classList.toggle("d-none", !enabled);

    }



    function addStage(processEl, fieldPrefix, roleTemplate, matrixEl) {

        var maxStages = parseIntSafe(processEl.getAttribute("data-max-stages"), 10);

        var stagesList = processEl.querySelector(".am-approval-stages-list");

        if (!stagesList || stagesList.children.length >= maxStages) {

            return;

        }



        var useUserPicker = usesApproverUserPicker(matrixEl);

        var stageEl = document.createElement("div");

        stageEl.className = "am-approval-stage row g-2 align-items-center mb-2";



        var labelCol = document.createElement("div");

        labelCol.className = "col-md-2";

        var label = document.createElement("span");

        label.className = "am-approval-stage-label fw-semibold";

        labelCol.appendChild(label);

        stageEl.appendChild(labelCol);



        var selectCol = document.createElement("div");

        selectCol.className = "col-md-8";

        if (useUserPicker && window.ApprovalApproverPicker) {

            selectCol.appendChild(window.ApprovalApproverPicker.createPickerElement(getApproverOptions(matrixEl)));

        } else {

            var select = document.createElement("select");

            select.className = "form-select";

            cloneSelectOptions(roleTemplate, select, "");

            selectCol.appendChild(select);

        }

        stageEl.appendChild(selectCol);



        var actionCol = document.createElement("div");

        actionCol.className = "col-md-2";

        var removeButton = document.createElement("button");

        removeButton.type = "button";

        removeButton.className = "btn btn-outline-secondary btn-sm am-approval-remove-stage";

        removeButton.textContent = "Remove";

        actionCol.appendChild(removeButton);

        stageEl.appendChild(actionCol);



        stagesList.appendChild(stageEl);

        reindexProcess(processEl, fieldPrefix, useUserPicker);

    }



    function removeStage(processEl, stageEl, fieldPrefix, matrixEl) {

        var stagesList = processEl.querySelector(".am-approval-stages-list");

        if (!stagesList || !stageEl || stagesList.children.length <= 1) {

            return;

        }



        stageEl.parentNode.removeChild(stageEl);

        reindexProcess(processEl, fieldPrefix, usesApproverUserPicker(matrixEl));

    }



    function wireMatrix(matrixEl) {

        var fieldPrefix = matrixEl.getAttribute("data-field-prefix") || "ApprovalProcesses";

        var roleTemplate = matrixEl.querySelector(".am-approval-role-template");

        var processes = matrixEl.querySelectorAll(".am-approval-process");

        var useUserPicker = usesApproverUserPicker(matrixEl);



        processes.forEach(function (processEl) {

            reindexProcess(processEl, fieldPrefix, useUserPicker);

            toggleProcessStages(processEl);



            var requiresApproval = processEl.querySelector(".am-approval-requires-approval");

            if (requiresApproval) {

                requiresApproval.addEventListener("change", function () {

                    toggleProcessStages(processEl);

                });

            }



            var addButton = processEl.querySelector(".am-approval-add-stage");

            if (addButton) {

                addButton.addEventListener("click", function () {

                    addStage(processEl, fieldPrefix, roleTemplate, matrixEl);

                });

            }

        });



        matrixEl.addEventListener("click", function (event) {

            var target = event.target;

            if (!target || !target.classList || !target.classList.contains("am-approval-remove-stage")) {

                return;

            }



            var stageEl = target.closest(".am-approval-stage");

            var processEl = target.closest(".am-approval-process");

            if (!stageEl || !processEl) {

                return;

            }



            removeStage(processEl, stageEl, fieldPrefix, matrixEl);

        });

    }



    document.querySelectorAll(".am-approval-matrix").forEach(wireMatrix);

})();

