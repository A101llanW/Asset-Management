(function () {
    var STAGE_CORE = 0;
    var STAGE_SUBS = 1;
    var STAGE_ROOMS = 2;
    var stage = STAGE_CORE;
    var cycleBtn = document.getElementById("am-dept-expand-cycle");
    var hint = document.getElementById("am-dept-expand-hint");

    function rows(level) {
        return Array.prototype.slice.call(document.querySelectorAll('tr[data-am-dept-row="' + level + '"]'));
    }

    function setHidden(els, hidden) {
        els.forEach(function (el) {
            el.hidden = !!hidden;
        });
    }

    function updateHint() {
        if (!hint || !cycleBtn) { return; }
        if (stage === STAGE_CORE) {
            hint.textContent = "Showing core departments. Expand to reveal sub-departments, then rooms.";
            cycleBtn.textContent = "Expand hierarchy";
        } else if (stage === STAGE_SUBS) {
            hint.textContent = "Showing departments and sub-departments. Expand again to reveal rooms.";
            cycleBtn.textContent = "Show rooms";
        } else {
            hint.textContent = "Showing departments, sub-departments, and rooms. Expand again to collapse.";
            cycleBtn.textContent = "Collapse hierarchy";
        }
    }

    function applyStage() {
        var subs = rows("sub");
        var rooms = rows("room");
        if (stage === STAGE_CORE) {
            setHidden(subs, true);
            setHidden(rooms, true);
            document.querySelectorAll("[data-am-dept-toggle]").forEach(function (btn) {
                btn.setAttribute("aria-expanded", "false");
                btn.textContent = "▸";
            });
        } else if (stage === STAGE_SUBS) {
            setHidden(subs, false);
            setHidden(rooms, true);
            document.querySelectorAll('tr[data-am-dept-row="core"] [data-am-dept-toggle]').forEach(function (btn) {
                btn.setAttribute("aria-expanded", "true");
                btn.textContent = "▾";
            });
            document.querySelectorAll('tr[data-am-dept-row="sub"] [data-am-dept-toggle]').forEach(function (btn) {
                btn.setAttribute("aria-expanded", "false");
                btn.textContent = "▸";
            });
        } else {
            setHidden(subs, false);
            setHidden(rooms, false);
            document.querySelectorAll("[data-am-dept-toggle]").forEach(function (btn) {
                btn.setAttribute("aria-expanded", "true");
                btn.textContent = "▾";
            });
        }
        updateHint();
    }

    function cycle() {
        stage = (stage + 1) % 3;
        applyStage();
    }

    function toggleBranch(id) {
        var btn = document.querySelector('[data-am-dept-toggle="' + id + '"]');
        var row = btn ? btn.closest("tr") : null;
        if (!row) { return; }
        var level = row.getAttribute("data-am-dept-row");
        var expanded = btn.getAttribute("aria-expanded") === "true";
        var next = !expanded;
        btn.setAttribute("aria-expanded", next ? "true" : "false");
        btn.textContent = next ? "▾" : "▸";

        if (level === "core") {
            var children = document.querySelectorAll('tr[data-am-dept-parent="' + id + '"]');
            Array.prototype.forEach.call(children, function (child) {
                var childLevel = child.getAttribute("data-am-dept-row");
                if (childLevel === "sub") {
                    child.hidden = !next;
                    if (!next) {
                        // collapse nested rooms under this sub
                        var childId = child.getAttribute("data-am-dept-id");
                        document.querySelectorAll('tr[data-am-dept-parent="' + childId + '"]').forEach(function (room) {
                            room.hidden = true;
                        });
                        var childToggle = child.querySelector("[data-am-dept-toggle]");
                        if (childToggle) {
                            childToggle.setAttribute("aria-expanded", "false");
                            childToggle.textContent = "▸";
                        }
                    }
                } else if (childLevel === "room") {
                    // direct rooms under admin/grade
                    child.hidden = !next;
                }
            });
        } else if (level === "sub") {
            document.querySelectorAll('tr[data-am-dept-parent="' + id + '"]').forEach(function (room) {
                room.hidden = !next;
            });
        }
    }

    if (cycleBtn) {
        cycleBtn.addEventListener("click", cycle);
    }
    document.querySelectorAll("[data-am-dept-toggle]").forEach(function (btn) {
        btn.addEventListener("click", function () {
            toggleBranch(btn.getAttribute("data-am-dept-toggle"));
        });
    });
    applyStage();
})();
