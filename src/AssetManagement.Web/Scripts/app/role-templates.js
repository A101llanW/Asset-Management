/* eslint-env browser */

(function () {
    var picker = document.getElementById("roleTemplatePicker");
    if (!picker) {
        return;
    }

    var templateUrl = picker.getAttribute("data-template-url");
    if (!templateUrl) {
        return;
    }

    picker.addEventListener("change", function () {
        var templateId = picker.value;
        if (!templateId) {
            if (window.RolePermissions && window.RolePermissions.applyPermissionIds) {
                window.RolePermissions.applyPermissionIds([]);
            }
            return;
        }

        fetch(templateUrl + "?id=" + encodeURIComponent(templateId), {
            credentials: "same-origin",
            headers: { Accept: "application/json" }
        })
            .then(function (response) {
                return response.json().then(function (payload) {
                    if (!response.ok) {
                        throw new Error((payload && payload.error) || "Unable to load template.");
                    }
                    return payload;
                });
            })
            .then(function (payload) {
                if (window.RolePermissions && window.RolePermissions.applyPermissionIds) {
                    window.RolePermissions.applyPermissionIds(payload.permissionIds || []);
                }
            })
            .catch(function (error) {
                window.alert(error.message || "Unable to load template.");
                picker.value = "";
            });
    });
})();
