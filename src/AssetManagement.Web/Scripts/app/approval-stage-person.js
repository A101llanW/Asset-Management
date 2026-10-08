/* eslint-env browser */
/*
 * Optional "Approver (person)" / "Purchase order raised by" person picker.
 * Markup: an element .am-person-pair holding a role <select class="am-person-role"> and a person
 * <select class="am-person-select">; options JSON ([{roleId, users:[{id,label}]}], live org-scoped
 * active users) comes from the closest [data-person-options]. Rules (server re-validates on save):
 *  - the list shows only active users holding the selected role, first option "Any user in role";
 *  - on role change: refresh; exactly one holder = auto-select, several = keep "Any user in role";
 *  - on load: keep the saved person; if they no longer hold the role, reset to "Any" with a note.
 */
(function () {
    var ANY_LABEL = "Any user in role";

    function parseOptions(raw) {
        if (!raw) {
            return [];
        }
        try {
            var parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : [];
        } catch (e) {
            return [];
        }
    }

    function optionsFor(el) {
        var host = el && el.closest ? el.closest("[data-person-options]") : null;
        if (!host) {
            return [];
        }
        if (!host.__amPersonOptions) {
            host.__amPersonOptions = parseOptions(host.getAttribute("data-person-options"));
        }
        return host.__amPersonOptions;
    }

    function usersForRole(options, roleId) {
        if (!roleId) {
            return [];
        }
        for (var i = 0; i < options.length; i++) {
            if (String(options[i].roleId) === String(roleId)) {
                return options[i].users || [];
            }
        }
        return [];
    }

    function setNote(pair, text) {
        var note = pair.querySelector(".am-person-note");
        if (!note) {
            return;
        }
        note.textContent = text || "";
        note.classList.toggle("d-none", !text);
    }

    // mode: "load" keeps the saved person; "role-change" applies the auto-select rule.
    function fill(pair, mode) {
        var roleSelect = pair.querySelector(".am-person-role");
        var personSelect = pair.querySelector(".am-person-select");
        if (!roleSelect || !personSelect) {
            return;
        }

        var roleId = roleSelect.value;
        var users = usersForRole(optionsFor(pair), roleId);
        var previous = mode === "load"
            ? (personSelect.getAttribute("data-saved-user-id") || personSelect.value || "")
            : "";

        personSelect.innerHTML = "";
        var any = document.createElement("option");
        any.value = "";
        any.textContent = ANY_LABEL;
        personSelect.appendChild(any);
        var found = false;
        users.forEach(function (u) {
            var opt = document.createElement("option");
            opt.value = String(u.id);
            opt.textContent = u.label || String(u.id);
            if (previous && String(u.id).toLowerCase() === previous.toLowerCase()) {
                found = true;
            }
            personSelect.appendChild(opt);
        });

        personSelect.disabled = !roleId;
        setNote(pair, "");
        if (mode === "load") {
            if (previous && found) {
                personSelect.value = users.filter(function (u) { return String(u.id).toLowerCase() === previous.toLowerCase(); })[0].id;
            } else {
                personSelect.value = "";
                if (previous) {
                    setNote(pair, "The previously selected person is no longer an active holder of this role; reset to \"" + ANY_LABEL + "\". Save to keep this.");
                }
            }
        } else {
            // Auto-select only when exactly one active user holds the role.
            personSelect.value = users.length === 1 ? String(users[0].id) : "";
            if (!roleId) {
                setNote(pair, "");
            } else if (users.length === 0) {
                setNote(pair, "No active user holds this role yet.");
            }
        }
        personSelect.setAttribute("data-saved-user-id", personSelect.value || "");
    }

    function init(root) {
        var scope = root || document;
        Array.prototype.forEach.call(scope.querySelectorAll(".am-person-pair"), function (pair) {
            if (pair.getAttribute("data-am-person-wired") === "true") {
                return;
            }
            pair.setAttribute("data-am-person-wired", "true");
            fill(pair, "load");
        });
    }

    document.addEventListener("change", function (event) {
        var target = event.target;
        if (!target || !target.classList || !target.classList.contains("am-person-role")) {
            return;
        }
        var pair = target.closest(".am-person-pair");
        if (pair) {
            fill(pair, "role-change");
        }
    });

    // ---- Live stage summary (the grey line under the process name), client-side only. ----
    function selectedText(select) {
        if (!select || !select.value || select.selectedIndex < 0) {
            return "";
        }
        return (select.options[select.selectedIndex].textContent || "").trim();
    }

    // Same wording as the server summary: "Role -> Role — person", or the "no stages" text.
    function stageSummaryText(processEl) {
        if (!processEl) {
            return "";
        }
        var requires = processEl.querySelector(".am-approval-requires-approval");
        if (requires && !requires.checked) {
            return "This process completes immediately without a separate approval step.";
        }
        var parts = [];
        Array.prototype.forEach.call(processEl.querySelectorAll(".am-approval-stages-list .am-approval-stage"), function (row) {
            var roleSelect = row.querySelector("select[name$='.RoleId']");
            var roleName = selectedText(roleSelect);
            if (!roleName) {
                return;
            }
            var personName = selectedText(row.querySelector("select.am-person-select"));
            parts.push(personName ? roleName + " \u2014 " + personName : roleName);
        });
        return parts.length ? parts.join(" -> ") : "No approval stages configured.";
    }

    function refreshStageSummary(processEl) {
        if (!processEl) {
            return;
        }
        var matrix = processEl.closest(".am-approval-matrix");
        if (matrix && matrix.getAttribute("data-use-approver-user-picker") === "true") {
            return; // the dropdown approver picker keeps its own labels
        }
        var summary = processEl.querySelector(".am-approval-stage-summary");
        if (summary) {
            summary.textContent = stageSummaryText(processEl);
        }
        if (typeof window.CustomEvent === "function") {
            processEl.dispatchEvent(new CustomEvent("am:stage-summary-changed", { bubbles: true }));
        }
    }

    function scheduleSummaryFor(target) {
        var processEl = target && target.closest ? target.closest(".am-approval-process") : null;
        if (processEl) {
            window.setTimeout(function () { refreshStageSummary(processEl); }, 0);
        }
    }

    document.addEventListener("change", function (event) {
        scheduleSummaryFor(event.target);
    });
    // Capture phase on purpose: approval-process-matrix.js removes the row in its own (bubble) click handler
    // on .am-approval-matrix, which runs before a bubble handler here. Once the row is detached, the Remove
    // button has no .am-approval-process ancestor and the grey summary was never refreshed. Resolving the
    // process while the button is still attached fixes the per-row Remove and the stage-count select
    // (whose programmatic Remove clicks take the same path).
    document.addEventListener("click", function (event) {
        var button = event.target && event.target.closest
            ? event.target.closest(".am-approval-add-stage, .am-approval-remove-stage")
            : null;
        if (button) {
            scheduleSummaryFor(button);
        }
    }, true);

    window.AmPersonPicker = {
        init: init,
        fill: fill,
        anyLabel: ANY_LABEL,
        stageSummaryText: stageSummaryText,
        refreshStageSummary: refreshStageSummary
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () { init(document); });
    } else {
        init(document);
    }
})();
