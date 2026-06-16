/* eslint-env browser */
(function () {
    function parseMap(raw) {
        if (!raw) {
            return {};
        }

        try {
            return JSON.parse(raw);
        } catch (e) {
            return {};
        }
    }

    function readProp(object, camelName) {
        if (!object) {
            return undefined;
        }

        if (object[camelName] !== undefined && object[camelName] !== null) {
            return object[camelName];
        }

        var pascalName = camelName.charAt(0).toUpperCase() + camelName.slice(1);
        return object[pascalName];
    }

    function usersForDepartment(map, departmentId) {
        var key = departmentId ? String(departmentId) : "";
        return map[key] || [];
    }

    function allUsersFromMap(map) {
        var byId = {};
        Object.keys(map).forEach(function (departmentKey) {
            (map[departmentKey] || []).forEach(function (user) {
                var userId = user.id || user.Id;
                if (userId) {
                    byId[userId] = user;
                }
            });
        });

        return Object.keys(byId).map(function (userId) {
            return byId[userId];
        }).sort(function (a, b) {
            var nameA = (a.name || a.Name || "").toLowerCase();
            var nameB = (b.name || b.Name || "").toLowerCase();
            if (nameA < nameB) {
                return -1;
            }
            if (nameA > nameB) {
                return 1;
            }
            return 0;
        });
    }

    function buildUserDepartmentMap(map) {
        var userDepartmentMap = {};
        Object.keys(map).forEach(function (departmentKey) {
            (map[departmentKey] || []).forEach(function (user) {
                var userId = user.id || user.Id;
                if (userId && userDepartmentMap[userId] === undefined) {
                    userDepartmentMap[userId] = departmentKey;
                }
            });
        });
        return userDepartmentMap;
    }

    function isEditableDepartmentField(deptSelect) {
        return deptSelect && deptSelect.tagName === "SELECT" && !deptSelect.disabled;
    }

    function rebuildUserSelect(select, users, selectedValue, placeholder) {
        if (!select) {
            return;
        }

        var current = selectedValue || select.value;
        select.innerHTML = "";

        var empty = document.createElement("option");
        empty.value = "";
        empty.textContent = placeholder || "-- Select user --";
        select.appendChild(empty);

        users.forEach(function (user) {
            var option = document.createElement("option");
            option.value = user.id || user.Id;
            option.textContent = user.name || user.Name;
            if (current && current === option.value) {
                option.selected = true;
            }
            select.appendChild(option);
        });

        if (current) {
            var stillExists = users.some(function (user) {
                var userId = user.id || user.Id;
                return userId === current;
            });
            if (!stillExists) {
                select.value = "";
            }
        }
    }

    function applyLockedFields(form, lockedFields) {
        if (!lockedFields || !lockedFields.length) {
            return;
        }

        lockedFields.forEach(function (field) {
            var fieldId = readProp(field, "fieldId");
            var input = form.querySelector("#" + fieldId);
            if (!input) {
                return;
            }

            input.setAttribute("readonly", "readonly");
            input.setAttribute("aria-readonly", "true");
            if (input.tagName === "SELECT") {
                input.setAttribute("disabled", "disabled");
            }
        });
    }

    function wireDepartmentUserPair(form, map, pair) {
        var departmentFieldId = readProp(pair, "departmentFieldId");
        var userFieldId = readProp(pair, "userFieldId");
        var requireDepartmentForUsers = readProp(pair, "requireDepartmentForUsers");
        var deptSelect = form.querySelector("#" + departmentFieldId);
        var userSelect = form.querySelector("#" + userFieldId);
        if (!deptSelect || !userSelect) {
            return;
        }

        var placeholder = userSelect.getAttribute("data-am-user-placeholder") || "-- Select user --";
        var userDepartmentMap = buildUserDepartmentMap(map);
        var syncing = false;

        function refreshUsers() {
            if (syncing) {
                return;
            }

            var departmentId = deptSelect.value;
            var users = departmentId ? usersForDepartment(map, departmentId) : allUsersFromMap(map);

            if (requireDepartmentForUsers && !departmentId && users.length === 0) {
                rebuildUserSelect(userSelect, [], "", placeholder);
                userSelect.setAttribute("disabled", "disabled");
                return;
            }

            userSelect.removeAttribute("disabled");
            rebuildUserSelect(userSelect, users, userSelect.value, placeholder);
        }

        function syncDepartmentFromUser() {
            if (syncing) {
                return;
            }

            var userId = userSelect.value;
            if (!userId || !isEditableDepartmentField(deptSelect)) {
                return;
            }

            var departmentId = userDepartmentMap[userId];
            if (departmentId === undefined || deptSelect.value === departmentId) {
                return;
            }

            syncing = true;
            deptSelect.value = departmentId;
            syncing = false;
            refreshUsers();
        }

        deptSelect.addEventListener("change", refreshUsers);
        userSelect.addEventListener("change", syncDepartmentFromUser);
        refreshUsers();
    }

    document.querySelectorAll("form[data-am-workflow-form]").forEach(function (form) {
        var map = parseMap(form.getAttribute("data-am-users-by-dept"));
        var pairs = [];
        var lockedFields = [];

        try {
            pairs = JSON.parse(form.getAttribute("data-am-dept-user-pairs") || "[]");
        } catch (e) {
            pairs = [];
        }

        try {
            lockedFields = JSON.parse(form.getAttribute("data-am-locked-fields") || "[]");
        } catch (e) {
            lockedFields = [];
        }

        applyLockedFields(form, lockedFields);
        pairs.forEach(function (pair) {
            wireDepartmentUserPair(form, map, pair);
        });
    });
})();
