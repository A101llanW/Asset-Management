/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    function readTypeOptions(select) {
        return Array.prototype.slice.call(select.options)
            .filter(function (option, index) {
                return index > 0 && option.value;
            })
            .map(function (option) {
                return {
                    value: option.value,
                    text: option.text,
                    categoryId: option.getAttribute("data-category-id") || ""
                };
            });
    }

    function optionExists(select, value) {
        if (!value) {
            return false;
        }

        for (var i = 0; i < select.options.length; i++) {
            if (select.options[i].value === value) {
                return true;
            }
        }

        return false;
    }

    function renderAssetTypes(category, assetType, allOptions) {
        var selectedCategoryId = category.value;
        var previousValue = assetType.value;

        while (assetType.options.length > 1) {
            assetType.remove(1);
        }

        if (!selectedCategoryId) {
            allOptions.forEach(function (opt) {
                var option = document.createElement("option");
                option.value = opt.value;
                option.textContent = opt.text;
                option.setAttribute("data-category-id", opt.categoryId);
                assetType.appendChild(option);
            });

            assetType.value = optionExists(assetType, previousValue) ? previousValue : "";
            return;
        }

        allOptions.forEach(function (opt) {
            if (opt.categoryId !== selectedCategoryId) {
                return;
            }

            var option = document.createElement("option");
            option.value = opt.value;
            option.textContent = opt.text;
            option.setAttribute("data-category-id", opt.categoryId);
            assetType.appendChild(option);
        });

        assetType.value = optionExists(assetType, previousValue) ? previousValue : "";
    }

    function initCategoryAssetTypeSync() {
        var category = document.getElementById("CategoryId");
        var assetType = document.getElementById("AssetTypeId");
        if (!category || !assetType) {
            return;
        }

        var allOptions = readTypeOptions(assetType);

        function syncAssetTypes() {
            renderAssetTypes(category, assetType, allOptions);
        }

        function syncCategoryFromAssetType() {
            var selectedOption = assetType.options[assetType.selectedIndex];
            if (!selectedOption || !selectedOption.value) {
                return;
            }

            var categoryId = selectedOption.getAttribute("data-category-id");
            if (categoryId && category.value !== categoryId) {
                category.value = categoryId;
                syncAssetTypes();
            }
        }

        category.addEventListener("change", syncAssetTypes);
        assetType.addEventListener("change", syncCategoryFromAssetType);
        syncCategoryFromAssetType();
        syncAssetTypes();
    }

    global.AmAssetCategoryTypeSync = {
        init: initCategoryAssetTypeSync
    };

    if (global.document.readyState === "loading") {
        global.document.addEventListener("DOMContentLoaded", initCategoryAssetTypeSync);
    } else {
        initCategoryAssetTypeSync();
    }
})(window);
