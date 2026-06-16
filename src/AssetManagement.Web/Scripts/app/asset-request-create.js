(function () {
    function byId(id) {
        return document.getElementById(id);
    }

    function getSelectedValue(select) {
        return select && select.value ? select.value : "";
    }

    function getDepartmentId(form) {
        var departmentSelect = form.querySelector('select[name="DepartmentId"]');
        if (departmentSelect) {
            return getSelectedValue(departmentSelect);
        }

        var departmentInput = form.querySelector('input[name="DepartmentId"]');
        return departmentInput ? departmentInput.value : "";
    }

    function getDepartmentSelect(form) {
        return form.querySelector('select[name="DepartmentId"]');
    }

    function setSelectOptions(select, items, placeholder, selectedValue) {
        if (!select) {
            return;
        }

        var html = '<option value="">' + placeholder + "</option>";
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            var selected = selectedValue && String(item.id) === String(selectedValue) ? ' selected="selected"' : "";
            html += '<option value="' + item.id + '"' + selected + ">" + item.name + "</option>";
        }

        select.innerHTML = html;
        select.disabled = items.length === 0;
    }

    function toggleRequestedForUser(form) {
        var requestForSelf = form.querySelector('input[name="RequestForSelf"]:checked');
        var group = byId("requested-for-user-group");
        if (!group) {
            return;
        }

        var isSelf = !requestForSelf || requestForSelf.value === "True" || requestForSelf.value === "true";
        group.style.display = isSelf ? "none" : "";
    }

    function loadAssets(form) {
        var categorySelect = form.querySelector('select[name="CategoryId"]');
        var assetSelect = form.querySelector("[data-am-asset-select]");
        var assetsUrl = form.getAttribute("data-am-assets-url");
        if (!categorySelect || !assetSelect || !assetsUrl) {
            return;
        }

        var departmentId = getDepartmentId(form);
        var categoryId = getSelectedValue(categorySelect);
        var currentAssetId = getSelectedValue(assetSelect);

        if (!departmentId || !categoryId) {
            setSelectOptions(assetSelect, [], "-- Select department and category first --", null);
            return;
        }

        assetSelect.disabled = true;
        setSelectOptions(assetSelect, [], "Loading assets...", null);

        var url = assetsUrl + "?departmentId=" + encodeURIComponent(departmentId) + "&categoryId=" + encodeURIComponent(categoryId);
        fetch(url, { credentials: "same-origin" })
            .then(function (response) {
                return response.json();
            })
            .then(function (items) {
                var placeholder = items.length ? "-- Select asset --" : "No in-store assets available";
                setSelectOptions(assetSelect, items || [], placeholder, currentAssetId);
            })
            .catch(function () {
                setSelectOptions(assetSelect, [], "Unable to load assets", null);
            });
    }

    function parseUserDepartments(form) {
        var raw = form.getAttribute("data-am-user-departments");
        if (!raw) {
            return {};
        }

        try {
            return JSON.parse(raw);
        } catch (e) {
            return {};
        }
    }

    function syncDepartmentForSelectedUser(form, userDepartments) {
        var requestForSelf = form.querySelector('input[name="RequestForSelf"]:checked');
        var isSelf = !requestForSelf || requestForSelf.value === "True" || requestForSelf.value === "true";
        if (isSelf) {
            return;
        }

        var userSelect = form.querySelector('select[name="RequestedForUserId"]');
        var departmentSelect = getDepartmentSelect(form);
        if (!userSelect || !departmentSelect || departmentSelect.disabled) {
            return;
        }

        var departmentId = userDepartments[userSelect.value];
        if (departmentId) {
            departmentSelect.value = departmentId;
        }
    }

    function initForm(form) {
        var userDepartments = parseUserDepartments(form);

        form.querySelectorAll('input[name="RequestForSelf"]').forEach(function (radio) {
            radio.addEventListener("change", function () {
                toggleRequestedForUser(form);
            });
        });

        var userSelect = form.querySelector('select[name="RequestedForUserId"]');
        if (userSelect) {
            userSelect.addEventListener("change", function () {
                syncDepartmentForSelectedUser(form, userDepartments);
                loadAssets(form);
            });
        }

        var departmentSelect = getDepartmentSelect(form);
        var categorySelect = form.querySelector('select[name="CategoryId"]');
        if (departmentSelect) {
            departmentSelect.addEventListener("change", function () {
                loadAssets(form);
            });
        }

        if (categorySelect) {
            categorySelect.addEventListener("change", function () {
                loadAssets(form);
            });
        }

        toggleRequestedForUser(form);
        loadAssets(form);
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("form[data-am-asset-request-form]").forEach(initForm);
    });
})();
