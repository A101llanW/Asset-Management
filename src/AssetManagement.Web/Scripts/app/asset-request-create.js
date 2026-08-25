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
                var hint = byId("in-store-stock-hint");
                if (hint) {
                    if (!departmentId || !categoryId) {
                        hint.textContent = "Select department and category to see in-store availability.";
                    } else if (!items || items.length === 0) {
                        hint.textContent = "No in-store assets in this department and category. Submit an asset request for review, or create a purchase requisition if procurement is needed.";
                    } else {
                        hint.textContent = items.length + " in-store asset(s) available in this department and category.";
                    }
                }
            })
            .catch(function () {
                setSelectOptions(assetSelect, [], "Unable to load assets", null);
                var hint = byId("in-store-stock-hint");
                if (hint) {
                    hint.textContent = "Unable to load in-store availability.";
                }
            });
    }

    function initForm(form) {
        var departmentSelect = form.querySelector('select[name="DepartmentId"]');
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

        loadAssets(form);
    }

    document.addEventListener("DOMContentLoaded", function () {
        document.querySelectorAll("form[data-am-asset-request-form]").forEach(initForm);
    });
})();
