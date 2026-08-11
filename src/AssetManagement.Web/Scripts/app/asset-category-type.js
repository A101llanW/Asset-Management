/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    function readTypeGroups(select) {
        var groups = [];
        var childNodes = select.childNodes;

        for (var i = 0; i < childNodes.length; i++) {
            var node = childNodes[i];
            if (!node || node.nodeName !== "OPTGROUP") {
                continue;
            }

            var categoryId = node.getAttribute("data-category-id") || "";
            var options = [];
            var optionNodes = node.childNodes;

            for (var j = 0; j < optionNodes.length; j++) {
                var optionNode = optionNodes[j];
                if (!optionNode || optionNode.nodeName !== "OPTION" || !optionNode.value) {
                    continue;
                }

                options.push({
                    value: optionNode.value,
                    text: optionNode.textContent,
                    categoryId: optionNode.getAttribute("data-category-id") || categoryId
                });
            }

            if (options.length > 0) {
                groups.push({
                    label: node.label,
                    categoryId: categoryId,
                    options: options
                });
            }
        }

        return groups;
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

    function clearTypeOptions(select) {
        while (select.options.length > 1) {
            select.remove(1);
        }

        var optgroups = select.querySelectorAll("optgroup");
        for (var i = 0; i < optgroups.length; i++) {
            select.removeChild(optgroups[i]);
        }
    }

    function rebuildTypeSelect(select, groups, filterCategoryId, selectedValue) {
        clearTypeOptions(select);

        groups.forEach(function (group) {
            if (filterCategoryId && group.categoryId !== filterCategoryId) {
                return;
            }

            var optgroup = document.createElement("optgroup");
            optgroup.label = group.label;
            optgroup.setAttribute("data-category-id", group.categoryId);

            group.options.forEach(function (opt) {
                var option = document.createElement("option");
                option.value = opt.value;
                option.textContent = opt.text;
                option.setAttribute("data-category-id", opt.categoryId);
                optgroup.appendChild(option);
            });

            select.appendChild(optgroup);
        });

        select.value = optionExists(select, selectedValue) ? selectedValue : "";
    }

    function getSelectedTypeCategoryId(assetType) {
        var selectedOption = assetType.options[assetType.selectedIndex];
        if (!selectedOption || !selectedOption.value) {
            return "";
        }

        return selectedOption.getAttribute("data-category-id") || "";
    }

    function initCategoryAssetTypeSync() {
        var category = document.getElementById("CategoryId");
        var assetType = document.getElementById("AssetTypeId");
        if (!category || !assetType) {
            return;
        }

        var allGroups = readTypeGroups(assetType);
        if (allGroups.length === 0) {
            return;
        }

        function syncTypesFromCategory() {
            var selectedCategoryId = category.value;
            var previousTypeId = assetType.value;

            if (!selectedCategoryId) {
                rebuildTypeSelect(assetType, allGroups, "", previousTypeId);
                return;
            }

            var typeStillValid = false;
            if (previousTypeId) {
                for (var i = 0; i < allGroups.length; i++) {
                    if (allGroups[i].categoryId !== selectedCategoryId) {
                        continue;
                    }

                    for (var j = 0; j < allGroups[i].options.length; j++) {
                        if (allGroups[i].options[j].value === previousTypeId) {
                            typeStillValid = true;
                            break;
                        }
                    }

                    if (typeStillValid) {
                        break;
                    }
                }
            }

            rebuildTypeSelect(
                assetType,
                allGroups,
                selectedCategoryId,
                typeStillValid ? previousTypeId : ""
            );
        }

        function syncCategoryFromType() {
            var categoryId = getSelectedTypeCategoryId(assetType);
            if (!categoryId) {
                return;
            }

            if (category.value !== categoryId) {
                category.value = categoryId;
                syncTypesFromCategory();
            }
        }

        category.addEventListener("change", syncTypesFromCategory);
        assetType.addEventListener("change", syncCategoryFromType);

        syncCategoryFromType();
        syncTypesFromCategory();
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
