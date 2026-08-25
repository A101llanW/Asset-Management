/* eslint-env browser */

(function () {
    function applyPermissionIds(permissionIds) {
        if (window.RolePermissions && window.RolePermissions.applyPermissionIds) {
            window.RolePermissions.applyPermissionIds(permissionIds || []);
        }
    }

    function fetchPermissionIds(url, id) {
        return fetch(url + "?id=" + encodeURIComponent(id), {
            credentials: "same-origin",
            headers: { Accept: "application/json" }
        }).then(function (response) {
            return response.json().then(function (payload) {
                if (!response.ok) {
                    throw new Error((payload && payload.error) || "Unable to load permissions.");
                }

                return payload;
            });
        });
    }

    function getSelectedStartMode() {
        var selected = document.querySelector(".js-permission-start-mode:checked");
        return selected ? selected.value : "blank";
    }

    function syncStartModePanels() {
        var mode = getSelectedStartMode();
        var rolePanel = document.getElementById("roleCopyPanel");
        var templatePanel = document.getElementById("roleTemplatePanel");
        var rolePicker = document.getElementById("roleCopyPicker");
        var templatePicker = document.getElementById("roleTemplatePicker");

        if (rolePanel) {
            rolePanel.hidden = mode !== "role";
        }

        if (templatePanel) {
            templatePanel.hidden = mode !== "template";
        }

        if (mode === "blank") {
            if (rolePicker) {
                rolePicker.value = "";
            }

            if (templatePicker) {
                templatePicker.value = "";
            }

            applyPermissionIds([]);
        }
    }

    document.querySelectorAll(".js-permission-start-mode").forEach(function (radio) {
        radio.addEventListener("change", syncStartModePanels);
    });

    var rolePicker = document.getElementById("roleCopyPicker");
    if (rolePicker) {
        var roleUrl = rolePicker.getAttribute("data-role-url");
        rolePicker.addEventListener("change", function () {
            if (getSelectedStartMode() !== "role") {
                return;
            }

            var roleId = rolePicker.value;
            if (!roleId || !roleUrl) {
                applyPermissionIds([]);
                return;
            }

            fetchPermissionIds(roleUrl, roleId)
                .then(function (payload) {
                    applyPermissionIds(payload.permissionIds || []);
                })
                .catch(function (error) {
                    window.alert(error.message || "Unable to load role permissions.");
                    rolePicker.value = "";
                    applyPermissionIds([]);
                });
        });
    }

    var templatePicker = document.getElementById("roleTemplatePicker");
    if (templatePicker) {
        var templateUrl = templatePicker.getAttribute("data-template-url");
        templatePicker.addEventListener("change", function () {
            if (getSelectedStartMode() !== "template") {
                return;
            }

            var templateId = templatePicker.value;
            if (!templateId || !templateUrl) {
                applyPermissionIds([]);
                return;
            }

            fetchPermissionIds(templateUrl, templateId)
                .then(function (payload) {
                    applyPermissionIds(payload.permissionIds || []);
                })
                .catch(function (error) {
                    window.alert(error.message || "Unable to load template.");
                    templatePicker.value = "";
                    applyPermissionIds([]);
                });
        });
    }

    syncStartModePanels();
})();
