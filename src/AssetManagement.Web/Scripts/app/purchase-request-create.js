(function () {
    function byId(id) {
        return document.getElementById(id);
    }

    function getSelectedValue(select) {
        return select && select.value ? select.value : "";
    }

    function normalizeItem(item) {
        if (!item) {
            return null;
        }

        var id = item.id != null ? item.id : item.Id;
        var name = item.name != null ? item.name : item.Name;
        if (id == null || !name) {
            return null;
        }

        return { id: id, name: name };
    }

    function setSelectOptions(select, items, placeholder, selectedValue) {
        if (!select) {
            return;
        }

        var html = '<option value="">' + placeholder + "</option>";
        for (var i = 0; i < items.length; i++) {
            var item = normalizeItem(items[i]);
            if (!item) {
                continue;
            }

            var selected = selectedValue && String(item.id) === String(selectedValue) ? ' selected="selected"' : "";
            html += '<option value="' + item.id + '"' + selected + ">" + item.name + "</option>";
        }

        select.innerHTML = html;
        select.disabled = false;
    }

    function loadTargetAssets(form) {
        var assetSelect = form.querySelector("[data-am-target-asset-select]");
        var assetsUrl = form.getAttribute("data-am-target-assets-url");
        if (!assetSelect || !assetsUrl) {
            return;
        }

        var currentAssetId = getSelectedValue(assetSelect);
        var existingOptions = assetSelect.options ? assetSelect.options.length : 0;
        if (existingOptions > 1) {
            return;
        }

        assetSelect.disabled = true;
        setSelectOptions(assetSelect, [], "Loading assets...", currentAssetId);

        fetch(assetsUrl, { credentials: "same-origin" })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Request failed");
                }

                return response.json();
            })
            .then(function (items) {
                var placeholder = items.length ? "-- None --" : "No assets registered yet";
                setSelectOptions(assetSelect, items || [], placeholder, currentAssetId);
                var hint = byId("target-asset-hint");
                if (hint) {
                    hint.textContent = items.length
                        ? "Optional: link any asset registered in the organization for easier assignment after purchase."
                        : "No assets are registered yet. You can still submit without tagging.";
                }
            })
            .catch(function () {
                if (existingOptions > 1) {
                    assetSelect.disabled = false;
                    return;
                }

                setSelectOptions(assetSelect, [], "Could not load assets", currentAssetId);
                assetSelect.disabled = true;
            });
    }

    function initForm(form) {
        loadTargetAssets(form);
    }

    function boot() {
        var form = document.querySelector("[data-am-purchase-request-form]");
        if (form) {
            initForm(form);
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();
