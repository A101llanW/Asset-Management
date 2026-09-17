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

    function usersForDepartment(map, departmentId) {
        var key = departmentId ? String(departmentId) : "";
        if (map[key] && map[key].length) {
            return map[key];
        }

        var all = [];
        Object.keys(map).forEach(function (departmentKey) {
            (map[departmentKey] || []).forEach(function (user) {
                all.push(user);
            });
        });
        return all;
    }

    function rebuildUserSelect(select, users, selectedValue, placeholder) {
        if (!select) {
            return;
        }

        var current = selectedValue || select.value;
        while (select.options.length > 0) {
            select.remove(0);
        }

        var emptyOption = document.createElement("option");
        emptyOption.value = "";
        emptyOption.textContent = placeholder || "-- Select custodian --";
        select.appendChild(emptyOption);

        users.forEach(function (user) {
            var option = document.createElement("option");
            option.value = user.id || user.Id || "";
            option.textContent = user.name || user.Name || option.value;
            if (current && option.value === current) {
                option.selected = true;
            }
            select.appendChild(option);
        });
    }

    function refreshBatchUserSelects(form, map) {
        var deptSelect = form.querySelector("#ToDepartmentId");
        var departmentId = deptSelect ? deptSelect.value : "";
        var users = usersForDepartment(map, departmentId);
        var selects = form.querySelectorAll(".batch-assign-user-select");
        for (var i = 0; i < selects.length; i++) {
            var select = selects[i];
            rebuildUserSelect(
                select,
                users,
                select.value,
                select.getAttribute("data-am-user-placeholder"));
        }
    }

    document.addEventListener("DOMContentLoaded", function () {
        var form = document.querySelector("[data-am-batch-assign-form]");
        if (!form) {
            return;
        }

        var map = parseMap(form.getAttribute("data-am-users-by-dept"));
        refreshBatchUserSelects(form, map);

        var deptSelect = form.querySelector("#ToDepartmentId");
        if (deptSelect) {
            deptSelect.addEventListener("change", function () {
                refreshBatchUserSelects(form, map);
            });
        }
    });
})();
