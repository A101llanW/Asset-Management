(function () {
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

    function filterAssetTypes(categorySelect, typeSelect, allTypeOptions) {
        var selectedCategoryId = categorySelect.value;
        var previousValue = typeSelect.value;

        while (typeSelect.options.length > 1) {
            typeSelect.remove(1);
        }

        allTypeOptions.forEach(function (opt) {
            if (selectedCategoryId && opt.categoryId !== selectedCategoryId) {
                return;
            }

            var option = document.createElement("option");
            option.value = opt.value;
            option.textContent = opt.text;
            option.setAttribute("data-category-id", opt.categoryId);
            typeSelect.appendChild(option);
        });

        typeSelect.value = optionExists(typeSelect, previousValue) ? previousValue : "";
    }

    function buildTaggedAssetsUrl(baseUrl, categoryId, assetTypeId) {
        var params = [];
        if (categoryId) {
            params.push("categoryId=" + encodeURIComponent(categoryId));
        }
        if (assetTypeId) {
            params.push("assetTypeId=" + encodeURIComponent(assetTypeId));
        }
        return params.length ? baseUrl + (baseUrl.indexOf("?") >= 0 ? "&" : "?") + params.join("&") : baseUrl;
    }

    function populateTaggedAssets(taggedSelect, items, selectedId) {
        var previousValue = selectedId || taggedSelect.value;
        while (taggedSelect.options.length > 1) {
            taggedSelect.remove(1);
        }

        items.forEach(function (item) {
            var option = document.createElement("option");
            option.value = String(item.id);
            option.textContent = item.name;
            taggedSelect.appendChild(option);
        });

        taggedSelect.value = optionExists(taggedSelect, previousValue) ? previousValue : "";
    }

    function loadTaggedAssets(taggedAssetsUrl, categoryId, assetTypeId, taggedSelect) {
        if (!taggedAssetsUrl || !taggedSelect) {
            return;
        }

        var url = buildTaggedAssetsUrl(taggedAssetsUrl, categoryId, assetTypeId);
        fetch(url, { credentials: "same-origin" })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Failed to load assets");
                }
                return response.json();
            })
            .then(function (items) {
                populateTaggedAssets(taggedSelect, items || [], null);
            })
            .catch(function () {
                populateTaggedAssets(taggedSelect, [], null);
            });
    }

    function bindCatalogRow(row, allTypeOptions, taggedAssetsUrl) {
        if (!row || row.getAttribute("data-am-catalog-bound") === "true") {
            return;
        }

        row.setAttribute("data-am-catalog-bound", "true");
        var categorySelect = row.querySelector(".catalog-category");
        var typeSelect = row.querySelector(".catalog-asset-type");
        var taggedSelect = row.querySelector(".catalog-tagged-asset");
        if (!categorySelect || !typeSelect || !taggedSelect) {
            return;
        }

        function syncTypes() {
            filterAssetTypes(categorySelect, typeSelect, allTypeOptions);
            loadTaggedAssets(taggedAssetsUrl, categorySelect.value, typeSelect.value, taggedSelect);
        }

        categorySelect.addEventListener("change", syncTypes);
        typeSelect.addEventListener("change", function () {
            loadTaggedAssets(taggedAssetsUrl, categorySelect.value, typeSelect.value, taggedSelect);
        });

        filterAssetTypes(categorySelect, typeSelect, allTypeOptions);
        if (categorySelect.value || typeSelect.value) {
            loadTaggedAssets(taggedAssetsUrl, categorySelect.value, typeSelect.value, taggedSelect);
        }
    }

    function getCategoryOptionsHtml() {
        var firstSelect = document.querySelector(".catalog-category");
        if (!firstSelect) {
            return "<option value=\"\"></option>";
        }
        return firstSelect.innerHTML;
    }

    function getAssetTypeOptionsHtml() {
        var firstSelect = document.querySelector(".catalog-asset-type");
        if (!firstSelect) {
            return "<option value=\"\"></option>";
        }
        return firstSelect.innerHTML;
    }

    function reindexRows(tbody) {
        var rows = tbody.querySelectorAll(".catalog-item-row");
        rows.forEach(function (row, index) {
            row.querySelectorAll("[name^='CatalogItems']").forEach(function (input) {
                input.name = input.name.replace(/CatalogItems\[\d+\]/, "CatalogItems[" + index + "]");
            });
        });
    }

    function bindRemoveButtons(tbody) {
        tbody.querySelectorAll(".remove-catalog-row").forEach(function (btn) {
            if (btn.getAttribute("data-am-bound") === "true") {
                return;
            }
            btn.setAttribute("data-am-bound", "true");
            btn.addEventListener("click", function () {
                var rows = tbody.querySelectorAll(".catalog-item-row");
                if (rows.length <= 1) {
                    rows[0].querySelectorAll("input, select").forEach(function (input) {
                        if (input.type === "number") {
                            input.value = "";
                        } else if (input.tagName === "SELECT") {
                            input.selectedIndex = 0;
                        } else {
                            input.value = "";
                        }
                    });
                    return;
                }
                btn.closest("tr").remove();
                reindexRows(tbody);
            });
        });
    }

    function addRow(form, tbody, allTypeOptions, taggedAssetsUrl) {
        var currency = form.getAttribute("data-am-default-currency") || "KES";
        var index = tbody.querySelectorAll(".catalog-item-row").length;
        var row = document.createElement("tr");
        row.className = "catalog-item-row";
        row.innerHTML =
            "<td><input class=\"form-control form-control-sm\" name=\"CatalogItems[" + index + "].ItemName\" placeholder=\"e.g. Dell Latitude laptop\" /></td>" +
            "<td><select class=\"form-select form-select-sm catalog-category\" name=\"CatalogItems[" + index + "].AssetCategoryId\">" + getCategoryOptionsHtml() + "</select></td>" +
            "<td><select class=\"form-select form-select-sm catalog-asset-type\" name=\"CatalogItems[" + index + "].AssetTypeId\">" + getAssetTypeOptionsHtml() + "</select></td>" +
            "<td><select class=\"form-select form-select-sm catalog-tagged-asset\" name=\"CatalogItems[" + index + "].TaggedAssetId\"><option value=\"\">—</option></select></td>" +
            "<td><input class=\"form-control form-control-sm\" name=\"CatalogItems[" + index + "].Sku\" placeholder=\"SKU\" /></td>" +
            "<td><input class=\"form-control form-control-sm catalog-unit-price\" name=\"CatalogItems[" + index + "].UnitPrice\" type=\"number\" step=\"0.01\" min=\"0.01\" placeholder=\"0.00\" /></td>" +
            "<td><input class=\"form-control form-control-sm catalog-currency\" name=\"CatalogItems[" + index + "].Currency\" value=\"" + currency + "\" maxlength=\"10\" /></td>" +
            "<td><input class=\"form-control form-control-sm\" name=\"CatalogItems[" + index + "].MinimumOrderQuantity\" type=\"number\" min=\"1\" /></td>" +
            "<td><input class=\"form-control form-control-sm\" name=\"CatalogItems[" + index + "].LeadTimeDays\" type=\"number\" min=\"0\" /></td>" +
            "<td><input class=\"form-control form-control-sm\" name=\"CatalogItems[" + index + "].ItemDescription\" placeholder=\"Keywords for requisition match\" /></td>" +
            "<td class=\"text-end\"><button type=\"button\" class=\"btn btn-sm btn-outline-danger remove-catalog-row\" title=\"Remove line\">&times;</button></td>";
        tbody.appendChild(row);
        bindCatalogRow(row, allTypeOptions, taggedAssetsUrl);
        bindRemoveButtons(tbody);
    }

    function initCreateForm() {
        var form = document.querySelector("form[data-am-supplier-create]");
        if (!form) {
            return;
        }

        var tbody = document.getElementById("catalog-items-body");
        var addBtn = document.getElementById("add-catalog-row");
        if (!tbody) {
            return;
        }

        var taggedAssetsUrl = form.getAttribute("data-am-tagged-assets-url") || "";
        var templateTypeSelect = document.querySelector(".catalog-asset-type");
        var allTypeOptions = templateTypeSelect ? readTypeOptions(templateTypeSelect) : [];

        tbody.querySelectorAll(".catalog-item-row").forEach(function (row) {
            bindCatalogRow(row, allTypeOptions, taggedAssetsUrl);
        });
        bindRemoveButtons(tbody);

        if (addBtn) {
            addBtn.addEventListener("click", function () {
                addRow(form, tbody, allTypeOptions, taggedAssetsUrl);
            });
        }
    }

    function initCatalogAddForm() {
        var form = document.querySelector("form[data-am-supplier-catalog-add]");
        if (!form) {
            return;
        }

        var taggedAssetsUrl = form.getAttribute("data-am-tagged-assets-url") || "";
        var typeSelect = form.querySelector(".catalog-asset-type");
        var allTypeOptions = typeSelect ? readTypeOptions(typeSelect) : [];
        bindCatalogRow(form, allTypeOptions, taggedAssetsUrl);
    }

    document.addEventListener("DOMContentLoaded", function () {
        initCreateForm();
        initCatalogAddForm();
    });
})();
