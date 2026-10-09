/* eslint-env browser */
/* rel1009h: show the Move / Keep department choice while the form is being filled (not after submit).
   Config (data-am-dept-disposition on #am-dept-disposition-prompt), built by the server:
   { assetDepartmentId: 12, assetDepartmentName: "...", departments: { "34": { name: "...", pool: true, moveBlocked: "..." } } }
   pool = a department-pool (no person) into this department also needs Move/Keep (sub-department / department / grade).
   Mirrors CrossDepartmentDisposition.NeedsChoice; the server re-validates on submit. */
(function () {
    "use strict";

    function init() {
        var prompt = document.getElementById("am-dept-disposition-prompt");
        if (!prompt) {
            return;
        }

        var raw = prompt.getAttribute("data-am-dept-disposition");
        if (!raw) {
            return;
        }

        var config;
        try {
            config = JSON.parse(raw);
        } catch (e) {
            return;
        }

        var form = prompt.closest("form") || document;
        var poolEnabled = prompt.getAttribute("data-am-dept-disposition-pool") === "true";
        var move = document.getElementById("deptDispositionMove");
        var keep = document.getElementById("deptDispositionKeep");
        var blocked = document.getElementById("deptDispositionMoveBlocked");
        var reason = document.getElementById("am-dept-disposition-reason");

        function field(name) {
            return form.querySelector("[name='" + name + "']:not([type='hidden'])") || form.querySelector("[name='" + name + "']");
        }

        function text(el, value) {
            if (el) {
                el.textContent = value;
            }
        }

        function setAll(selector, value) {
            var nodes = prompt.querySelectorAll(selector);
            for (var i = 0; i < nodes.length; i++) {
                nodes[i].textContent = value;
            }
        }

        function evaluate() {
            var deptField = field("ToDepartmentId");
            var userField = field("ToUserId");
            var deptId = deptField && deptField.value ? String(deptField.value) : "";
            var userId = userField && userField.value ? String(userField.value) : "";
            var assetDeptId = config.assetDepartmentId ? String(config.assetDepartmentId) : "";
            var target = deptId && config.departments ? config.departments[deptId] : null;

            var needs = false;
            var personCase = false;
            if (deptId && assetDeptId && deptId !== assetDeptId) {
                if (userId) {
                    needs = true;
                    personCase = true;
                } else if (poolEnabled && target && target.pool) {
                    needs = true;
                }
            }

            prompt.classList.toggle("d-none", !needs);
            if (!needs) {
                move.checked = false;
                keep.checked = false;
                move.removeAttribute("required");
                keep.removeAttribute("required");
                return;
            }

            var targetName = target && target.name ? target.name : "the selected department";
            var assetName = config.assetDepartmentName || "its current department";
            if (reason) {
                reason.textContent = personCase
                    ? "The selected person is in " + targetName + ", but this asset is currently in " + assetName + "."
                    : targetName + " is a different department from this asset's (" + assetName + ").";
            }
            setAll(".am-dd-selected-name", targetName);
            setAll(".am-dd-asset-name", assetName);

            var blockedMessage = target && target.moveBlocked ? target.moveBlocked : "";
            move.disabled = !!blockedMessage;
            if (blockedMessage) {
                move.checked = false;
                move.removeAttribute("required");
            } else {
                move.setAttribute("required", "required");
            }
            keep.setAttribute("required", "required");
            if (blocked) {
                blocked.classList.toggle("d-none", !blockedMessage);
                text(blocked.querySelector(".am-dd-blocked-text"), blockedMessage);
            }
        }

        form.addEventListener("change", function (e) {
            var name = e.target && e.target.name;
            if (name === "ToDepartmentId" || name === "ToUserId") {
                // The workflow script may repopulate the user list after a department change.
                evaluate();
                window.setTimeout(evaluate, 50);
            }
        });

        evaluate();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
}());
