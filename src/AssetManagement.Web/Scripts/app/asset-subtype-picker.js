/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    var suppressTypeChangeClear = false;

    function byId(id) {
        return document.getElementById(id);
    }

    function getAntiForgeryToken() {
        var input = document.querySelector("input[name='__RequestVerificationToken']");
        return input ? input.value : "";
    }

    function showModal(modal) {
        if (!modal) {
            return;
        }

        if (global.bootstrap && global.bootstrap.Modal) {
            global.bootstrap.Modal.getOrCreateInstance(modal).show();
            return;
        }

        if (global.jQuery) {
            global.jQuery(modal).modal("show");
        }
    }

    function hideModal(modal) {
        if (!modal) {
            return;
        }

        if (global.bootstrap && global.bootstrap.Modal) {
            global.bootstrap.Modal.getOrCreateInstance(modal).hide();
            return;
        }

        if (global.jQuery) {
            global.jQuery(modal).modal("hide");
        }
    }

    function setSubTypeDisplay(name) {
        var display = byId(isReceiveMode() ? "receive-subtype-display" : "asset-subtype-display");
        if (!display) {
            return;
        }

        if (!name) {
            display.textContent = "Not assigned";
            display.className = "form-control-plaintext text-muted";
            return;
        }

        display.textContent = name;
        display.className = "form-control-plaintext fw-medium";
    }

    function setHiddenSubTypeId(id) {
        var hidden = byId("AssetSubTypeId");
        if (hidden) {
            hidden.value = id || "";
        }
    }

    function isReceiveMode() {
        return !!byId("receive-form");
    }

    function getMainCategoryId() {
        if (isReceiveMode()) {
            return "";
        }

        var category = byId("CategoryId");
        return category ? category.value : "";
    }

    function getMainAssetTypeId() {
        if (isReceiveMode()) {
            var receiveType = byId("ReceiveAssetTypeId");
            return receiveType ? receiveType.value : "";
        }

        var assetType = byId("AssetTypeId");
        return assetType ? assetType.value : "";
    }

    function getPickerCategoryId() {
        var categorySelect = byId("subtype-select-category");
        return categorySelect ? categorySelect.value : "";
    }

    function getPickerAssetTypeId() {
        var typeSelect = byId("subtype-select-type");
        return typeSelect ? typeSelect.value : "";
    }

    function getBrand() {
        if (isReceiveMode()) {
            var receiveBrand = byId("ReceiveBrand");
            return receiveBrand ? receiveBrand.value.trim() : "";
        }

        var brand = byId("Brand");
        return brand ? brand.value.trim() : "";
    }

    function getModel() {
        if (isReceiveMode()) {
            var receiveModel = byId("ReceiveModel");
            return receiveModel ? receiveModel.value.trim() : "";
        }

        var model = byId("Model");
        return model ? model.value.trim() : "";
    }

    function buildSuggestedName(brand, model) {
        if (brand && model) {
            return brand + " – " + model;
        }

        return brand || model || "Unspecified item";
    }

    function syncMainFormClassification(categoryId, assetTypeId, afterSync) {
        if (isReceiveMode()) {
            var receiveType = byId("ReceiveAssetTypeId");
            if (receiveType && assetTypeId) {
                receiveType.value = String(assetTypeId);
            }
            if (afterSync) {
                afterSync();
            }
            return;
        }

        var category = byId("CategoryId");
        var assetType = byId("AssetTypeId");

        if (category && categoryId) {
            category.value = String(categoryId);
            if (typeof category.dispatchEvent === "function") {
                category.dispatchEvent(new Event("change", { bubbles: true }));
            }
        }

        // Allow category→type rebuild to finish before setting type.
        global.setTimeout(function () {
            if (assetType && assetTypeId) {
                suppressTypeChangeClear = true;
                assetType.value = String(assetTypeId);
                if (typeof assetType.dispatchEvent === "function") {
                    assetType.dispatchEvent(new Event("change", { bubbles: true }));
                }
                suppressTypeChangeClear = false;
            }

            if (afterSync) {
                afterSync();
            }
        }, 0);
    }

    function renderTypeOptions(categoryId) {
        var typeSelect = byId("subtype-select-type");
        if (!typeSelect) {
            return;
        }

        var options = typeSelect.querySelectorAll("option[data-category-id]");
        var selectedStillVisible = false;
        for (var i = 0; i < options.length; i++) {
            var option = options[i];
            var visible = !categoryId || option.getAttribute("data-category-id") === categoryId;
            option.hidden = !visible;
            option.disabled = !visible;
            if (visible && option.value === typeSelect.value) {
                selectedStillVisible = true;
            }
        }

        if (categoryId && !selectedStillVisible) {
            typeSelect.value = "";
        }
    }

    function loadSubTypesForType(typeId) {
        var modal = byId("assetSubTypePickerModal");
        var select = byId("subtype-select-item");
        var meta = byId("subtype-select-meta");
        if (!modal || !select) {
            return Promise.resolve([]);
        }

        var baseUrl = modal.getAttribute("data-am-subtype-by-type-url") || "";
        if (!baseUrl || !typeId) {
            select.innerHTML = "<option value=\"\">-- Select sub-type --</option>";
            if (meta) {
                meta.textContent = "";
            }
            return Promise.resolve([]);
        }

        return fetch(baseUrl + "?assetTypeId=" + encodeURIComponent(typeId), {
            credentials: "same-origin"
        })
            .then(function (response) {
                return response.json();
            })
            .then(function (items) {
                var html = "<option value=\"\">-- Select sub-type --</option>";
                for (var i = 0; i < items.length; i++) {
                    var item = items[i];
                    html += "<option value=\"" + item.id + "\" data-brand=\"" + (item.brand || "") + "\" data-model=\"" + (item.model || "") + "\" data-stock=\"" + (item.stockCount || 0) + "\">" + item.name + "</option>";
                }

                select.innerHTML = html;
                if (meta) {
                    meta.textContent = "";
                }
                return items;
            });
    }

    function tryAutoMatch() {
        var modal = byId("assetSubTypePickerModal");
        var assetTypeId = getMainAssetTypeId();
        var brand = getBrand();
        var model = getModel();
        if (!modal || !assetTypeId || (!brand && !model)) {
            return;
        }

        var baseUrl = modal.getAttribute("data-am-subtype-lookup-url") || "";
        var url = baseUrl + "?assetTypeId=" + encodeURIComponent(assetTypeId)
            + "&brand=" + encodeURIComponent(brand)
            + "&model=" + encodeURIComponent(model);

        fetch(url, { credentials: "same-origin" })
            .then(function (response) {
                return response.json();
            })
            .then(function (result) {
                if (!result || !result.matched) {
                    return;
                }

                setHiddenSubTypeId(result.id);
                setSubTypeDisplay(result.name);
            });
    }

    function resolveCategoryIdForType(typeSelect, assetTypeId) {
        if (!typeSelect || !assetTypeId) {
            return "";
        }

        for (var i = 0; i < typeSelect.options.length; i++) {
            var option = typeSelect.options[i];
            if (option.value === String(assetTypeId)) {
                return option.getAttribute("data-category-id") || "";
            }
        }

        return "";
    }

    function openPickerModal() {
        var modal = byId("assetSubTypePickerModal");
        if (!modal) {
            return;
        }

        var brand = getBrand();
        var model = getModel();
        var nameField = byId("subtype-create-name");
        var brandField = byId("subtype-create-brand");
        var modelField = byId("subtype-create-model");
        if (nameField) {
            nameField.value = buildSuggestedName(brand, model);
        }
        if (brandField) {
            brandField.value = brand;
        }
        if (modelField) {
            modelField.value = model;
        }

        var createError = byId("subtype-create-error");
        var selectError = byId("subtype-select-error");
        if (createError) {
            createError.style.display = "none";
            createError.textContent = "";
        }
        if (selectError) {
            selectError.style.display = "none";
            selectError.textContent = "";
        }

        var categorySelect = byId("subtype-select-category");
        var typeSelect = byId("subtype-select-type");
        var mainCategoryId = getMainCategoryId();
        var mainAssetTypeId = getMainAssetTypeId();

        if (categorySelect) {
            categorySelect.value = mainCategoryId || "";
            renderTypeOptions(categorySelect.value);
        }

        if (typeSelect) {
            if (mainAssetTypeId) {
                var categoryForType = resolveCategoryIdForType(typeSelect, mainAssetTypeId);
                if (categorySelect && categoryForType && !categorySelect.value) {
                    categorySelect.value = categoryForType;
                    renderTypeOptions(categoryForType);
                }
                typeSelect.value = mainAssetTypeId;
                loadSubTypesForType(mainAssetTypeId);
            } else {
                typeSelect.value = "";
                loadSubTypesForType("");
            }
        }

        showModal(modal);
    }

    function requirePlacement(errorEl) {
        var categoryId = getPickerCategoryId();
        var assetTypeId = getPickerAssetTypeId();

        if (!categoryId || !assetTypeId) {
            if (errorEl) {
                errorEl.textContent = "Select a category and asset type so this sub-type can be saved in the right place.";
                errorEl.style.display = "block";
            }
            return null;
        }

        return { categoryId: categoryId, assetTypeId: assetTypeId };
    }

    function assignFromCreate(modal) {
        var createUrl = modal.getAttribute("data-am-subtype-create-url") || "";
        var error = byId("subtype-create-error");
        var placement = requirePlacement(error);
        if (!placement) {
            return;
        }

        var name = (byId("subtype-create-name") || {}).value || "";
        var brand = (byId("subtype-create-brand") || {}).value || "";
        var model = (byId("subtype-create-model") || {}).value || "";

        if (error) {
            error.style.display = "none";
            error.textContent = "";
        }

        if (!name.trim() && !brand.trim() && !model.trim()) {
            if (error) {
                error.textContent = "Enter a display name, or brand and model, for the new sub-type.";
                error.style.display = "block";
            }
            return;
        }

        var body = "__RequestVerificationToken=" + encodeURIComponent(getAntiForgeryToken())
            + "&AssetTypeId=" + encodeURIComponent(placement.assetTypeId)
            + "&Name=" + encodeURIComponent(name)
            + "&Brand=" + encodeURIComponent(brand)
            + "&Model=" + encodeURIComponent(model);

        fetch(createUrl, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
            },
            body: body
        })
            .then(function (response) {
                return response.json();
            })
            .then(function (result) {
                if (!result || !result.success) {
                    if (error) {
                        error.textContent = (result && result.message) || "Unable to create sub-type.";
                        error.style.display = "block";
                    }
                    return;
                }

                var categoryId = result.categoryId || placement.categoryId;
                var assetTypeId = result.assetTypeId || placement.assetTypeId;
                syncMainFormClassification(categoryId, assetTypeId, function () {
                    setHiddenSubTypeId(result.id);
                    setSubTypeDisplay(result.name);

                    var brandInput = byId("Brand");
                    var modelInput = byId("Model");
                    if (brandInput) {
                        brandInput.value = result.brand || brand;
                    }
                    if (modelInput) {
                        modelInput.value = result.model || model;
                    }
                });
                hideModal(modal);
            });
    }

    function assignFromSelect(modal) {
        var error = byId("subtype-select-error");
        var select = byId("subtype-select-item");
        var placement = requirePlacement(error);
        if (!placement) {
            return;
        }

        if (error) {
            error.style.display = "none";
            error.textContent = "";
        }

        if (!select || !select.value) {
            if (error) {
                error.textContent = "Select a sub-type to assign.";
                error.style.display = "block";
            }
            return;
        }

        var option = select.options[select.selectedIndex];
        syncMainFormClassification(placement.categoryId, placement.assetTypeId, function () {
            setHiddenSubTypeId(select.value);
            setSubTypeDisplay(option.text);

            var brandInput = byId("Brand");
            var modelInput = byId("Model");
            if (brandInput) {
                brandInput.value = option.getAttribute("data-brand") || "";
            }
            if (modelInput) {
                modelInput.value = option.getAttribute("data-model") || "";
            }
        });
        hideModal(modal);
    }

    function initAssetSubTypePicker() {
        var modal = byId("assetSubTypePickerModal");
        var changeButton = byId("asset-subtype-change") || byId("receive-subtype-change");
        var brand = byId("Brand");
        var model = byId("Model");
        var assetType = byId("AssetTypeId");
        var confirm = byId("asset-subtype-picker-confirm");
        var categorySelect = byId("subtype-select-category");
        var typeSelect = byId("subtype-select-type");
        var subtypeSelect = byId("subtype-select-item");

        if (changeButton) {
            changeButton.addEventListener("click", openPickerModal);
        }

        if (brand) {
            brand.addEventListener("blur", tryAutoMatch);
        }
        if (model) {
            model.addEventListener("blur", tryAutoMatch);
        }
        if (assetType) {
            assetType.addEventListener("change", function () {
                if (suppressTypeChangeClear) {
                    return;
                }

                setHiddenSubTypeId("");
                setSubTypeDisplay("");
                tryAutoMatch();
            });
        }

        if (confirm && modal) {
            confirm.addEventListener("click", function () {
                var createTabButton = byId("subtype-create-tab");
                if (createTabButton && createTabButton.classList.contains("active")) {
                    assignFromCreate(modal);
                    return;
                }

                assignFromSelect(modal);
            });
        }

        if (categorySelect) {
            categorySelect.addEventListener("change", function () {
                renderTypeOptions(categorySelect.value);
                if (typeSelect) {
                    typeSelect.value = "";
                }
                loadSubTypesForType("");
            });
        }

        if (typeSelect) {
            typeSelect.addEventListener("change", function () {
                var selectedOption = typeSelect.options[typeSelect.selectedIndex];
                var categoryId = selectedOption ? (selectedOption.getAttribute("data-category-id") || "") : "";
                if (categorySelect && categoryId && categorySelect.value !== categoryId) {
                    categorySelect.value = categoryId;
                    renderTypeOptions(categoryId);
                    typeSelect.value = selectedOption.value;
                }

                loadSubTypesForType(typeSelect.value);
            });
        }

        if (subtypeSelect) {
            subtypeSelect.addEventListener("change", function () {
                var meta = byId("subtype-select-meta");
                var option = subtypeSelect.options[subtypeSelect.selectedIndex];
                if (!meta || !option || !option.value) {
                    if (meta) {
                        meta.textContent = "";
                    }
                    return;
                }

                meta.textContent = (option.getAttribute("data-stock") || "0")
                    + " in stock (org-wide)";
            });
        }
    }

    global.AmAssetSubTypePicker = {
        init: initAssetSubTypePicker
    };

    if (global.document.readyState === "loading") {
        global.document.addEventListener("DOMContentLoaded", initAssetSubTypePicker);
    } else {
        initAssetSubTypePicker();
    }
})(window);
