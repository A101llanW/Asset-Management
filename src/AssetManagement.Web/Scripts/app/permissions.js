/* eslint-env browser */

(function () {
    function getPermissionCheckboxes() {
        return document.querySelectorAll(".js-permission-checkbox");
    }

    function setModuleSelectAllState(module) {
        var moduleCheckbox = document.querySelector('.js-select-all-module[data-module="' + module + '"]');
        if (!moduleCheckbox) {
            return;
        }

        var items = document.querySelectorAll('input.js-permission-checkbox[data-module-item="' + module + '"]');
        if (items.length === 0) {
            moduleCheckbox.checked = false;
            moduleCheckbox.indeterminate = false;
            return;
        }

        var checkedCount = 0;
        items.forEach(function (item) {
            if (item.checked) {
                checkedCount += 1;
            }
        });

        moduleCheckbox.checked = checkedCount === items.length;
        moduleCheckbox.indeterminate = checkedCount > 0 && checkedCount < items.length;
    }

    function syncModuleSelectAllStates() {
        document.querySelectorAll(".js-select-all-module").forEach(function (checkbox) {
            setModuleSelectAllState(checkbox.getAttribute("data-module"));
        });
    }

    function applyPermissionIds(permissionIds) {
        var selected = {};
        (permissionIds || []).forEach(function (id) {
            selected[String(id)] = true;
        });

        getPermissionCheckboxes().forEach(function (item) {
            item.checked = !!selected[item.value];
        });

        syncModuleSelectAllStates();
    }

    document.querySelectorAll(".js-select-all-module").forEach(function (checkbox) {
        checkbox.addEventListener("change", function () {
            var module = checkbox.getAttribute("data-module");
            document.querySelectorAll('input.js-permission-checkbox[data-module-item="' + module + '"]').forEach(function (item) {
                item.checked = checkbox.checked;
            });
            checkbox.indeterminate = false;
        });
    });

    document.querySelectorAll(".js-select-all-permissions").forEach(function (button) {
        button.addEventListener("click", function () {
            getPermissionCheckboxes().forEach(function (item) {
                item.checked = true;
            });
            document.querySelectorAll(".js-select-all-module").forEach(function (checkbox) {
                checkbox.checked = true;
                checkbox.indeterminate = false;
            });
        });
    });

    getPermissionCheckboxes().forEach(function (checkbox) {
        checkbox.addEventListener("change", function () {
            setModuleSelectAllState(checkbox.getAttribute("data-module-item"));
        });
    });

    syncModuleSelectAllStates();

    window.RolePermissions = {
        applyPermissionIds: applyPermissionIds
    };
})();
