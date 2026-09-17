/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    var config = null;
    var modalEl = null;
    var draft = null;
    var subTypes = [];
    var searchQuery = "";

    function byId(id) {
        return document.getElementById(id);
    }

    function parseConfig() {
        var node = byId("am-classification-data");
        if (!node || !node.textContent) {
            return { categories: [], types: [], selected: {} };
        }

        try {
            return JSON.parse(node.textContent);
        } catch (e) {
            return { categories: [], types: [], selected: {} };
        }
    }

    function ensureModalInBody(modal) {
        if (!modal || modal.parentElement === global.document.body) {
            return;
        }

        global.document.body.appendChild(modal);
    }

    function cleanupStaleModalState() {
        var openModals = global.document.querySelectorAll(".modal.show");
        if (openModals.length > 0) {
            return;
        }

        global.document.querySelectorAll(".modal-backdrop").forEach(function (backdrop) {
            backdrop.remove();
        });
        global.document.body.classList.remove("modal-open");
        global.document.body.style.removeProperty("overflow");
        global.document.body.style.removeProperty("padding-right");
    }

    function showModal(modal) {
        if (!modal) {
            return;
        }

        ensureModalInBody(modal);
        cleanupStaleModalState();

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

    function getAntiForgeryToken() {
        var input = document.querySelector("input[name='__RequestVerificationToken']");
        return input ? input.value : "";
    }

    function normalizeId(value) {
        var parsed = parseInt(String(value || ""), 10);
        return isNaN(parsed) || parsed <= 0 ? 0 : parsed;
    }

    function cloneSelection(source) {
        source = source || {};
        return {
            categoryId: normalizeId(source.categoryId),
            categoryName: source.categoryName || "",
            typeId: normalizeId(source.typeId),
            typeName: source.typeName || "",
            subTypeId: normalizeId(source.subTypeId),
            subTypeName: source.subTypeName || ""
        };
    }

    function readAppliedSelection() {
        var categoryInput = byId("CategoryId");
        var typeInput = byId("AssetTypeId");
        var subTypeInput = byId("AssetSubTypeId");
        var categoryId = normalizeId(categoryInput ? categoryInput.value : 0);
        var typeId = normalizeId(typeInput ? typeInput.value : 0);
        var subTypeId = normalizeId(subTypeInput ? subTypeInput.value : 0);
        var category = findCategory(categoryId);
        var type = findType(typeId);
        var subTypeName = byId("asset-subtype-display") ? byId("asset-subtype-display").textContent : "";

        return {
            categoryId: categoryId,
            categoryName: category ? category.name : "",
            typeId: typeId,
            typeName: type ? type.name : "",
            subTypeId: subTypeId,
            subTypeName: subTypeId > 0 ? subTypeName : ""
        };
    }

    function findCategory(categoryId) {
        if (!config || !categoryId) {
            return null;
        }

        for (var i = 0; i < config.categories.length; i++) {
            if (config.categories[i].id === categoryId) {
                return config.categories[i];
            }
        }

        return null;
    }

    function findType(typeId) {
        if (!config || !typeId) {
            return null;
        }

        for (var i = 0; i < config.types.length; i++) {
            if (config.types[i].id === typeId) {
                return config.types[i];
            }
        }

        return null;
    }

    function matchesSearch(text) {
        if (!searchQuery) {
            return true;
        }

        return String(text || "").toLowerCase().indexOf(searchQuery) >= 0;
    }

    function setHiddenValue(id, value) {
        var input = byId(id);
        if (input) {
            input.value = value > 0 ? String(value) : "";
        }
    }

    function applyDefaultAcquisitionCost(cost) {
        var costInput = byId("AcquisitionCost");
        if (!costInput || cost == null || cost === "") {
            return;
        }

        var parsed = parseFloat(String(cost).replace(/,/g, ""));
        if (isNaN(parsed) || parsed <= 0) {
            return;
        }

        var current = parseFloat(String(costInput.value).replace(/,/g, "")) || 0;
        if (current > 0.01) {
            return;
        }

        costInput.value = parsed.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        if (typeof costInput.dispatchEvent === "function") {
            costInput.dispatchEvent(new Event("input", { bubbles: true }));
        }
    }

    function updateSummary(selection) {
        var summary = byId("asset-classification-summary");
        var emptyState = byId("asset-classification-empty");
        var trail = byId("asset-classification-trail");
        var categoryDisplay = byId("asset-category-display");
        var typeDisplay = byId("asset-type-display");
        var subTypeDisplay = byId("asset-subtype-display");
        var changeButton = byId("asset-classification-change");
        var hasCore = selection.categoryId > 0 && selection.typeId > 0;
        var hasComplete = hasCore && selection.subTypeId > 0;

        if (summary) {
            summary.classList.toggle("is-set", hasComplete);
            summary.classList.toggle("is-empty", !hasComplete);
        }

        if (emptyState) {
            emptyState.classList.toggle("d-none", hasComplete);
        }

        if (trail) {
            trail.classList.toggle("d-none", !hasCore);
        }

        if (categoryDisplay) {
            categoryDisplay.textContent = selection.categoryName || "Category";
        }

        if (typeDisplay) {
            typeDisplay.textContent = selection.typeName || "Type";
        }

        if (subTypeDisplay) {
            if (selection.subTypeId > 0 && selection.subTypeName) {
                subTypeDisplay.textContent = selection.subTypeName;
                subTypeDisplay.classList.remove("am-classification-chip-muted");
            } else {
                subTypeDisplay.textContent = "Not selected — required";
                subTypeDisplay.classList.add("am-classification-chip-muted");
            }
        }

        if (changeButton) {
            changeButton.textContent = hasComplete ? "Change" : "Choose classification";
        }
    }

    function applySelection(selection, options) {
        options = options || {};
        selection = cloneSelection(selection);

        setHiddenValue("CategoryId", selection.categoryId);
        setHiddenValue("AssetTypeId", selection.typeId);
        setHiddenValue("AssetSubTypeId", selection.subTypeId);

        if (config && config.selected) {
            config.selected = selection;
        }

        updateSummary(selection);

        if (options.applyCost && options.defaultCost) {
            applyDefaultAcquisitionCost(options.defaultCost);
        }
    }

    function renderOptionButton(item, isSelected, metaText) {
        var button = document.createElement("button");
        button.type = "button";
        button.className = "am-classification-option" + (isSelected ? " is-selected" : "");
        button.setAttribute("data-id", String(item.id));
        button.setAttribute("role", "option");
        button.setAttribute("aria-selected", isSelected ? "true" : "false");

        var label = document.createElement("span");
        label.className = "am-classification-option-label";
        label.textContent = item.name;
        button.appendChild(label);

        if (metaText) {
            var meta = document.createElement("span");
            meta.className = "am-classification-option-meta";
            meta.textContent = metaText;
            button.appendChild(meta);
        }

        return button;
    }

    function renderCategories() {
        var list = byId("classifier-category-list");
        if (!list || !config) {
            return;
        }

        list.innerHTML = "";
        var visibleCount = 0;

        config.categories.forEach(function (category) {
            if (!matchesSearch(category.name)) {
                return;
            }

            visibleCount += 1;
            var button = renderOptionButton(category, draft.categoryId === category.id);
            button.addEventListener("click", function () {
                selectCategory(category);
            });
            list.appendChild(button);
        });

        if (visibleCount === 0) {
            list.innerHTML = "<li class=\"am-classification-empty-panel\">No categories match your search.</li>";
        }
    }

    function renderTypes() {
        var list = byId("classifier-type-list");
        var placeholder = byId("classifier-type-placeholder");
        if (!list || !config) {
            return;
        }

        list.innerHTML = "";
        if (!draft.categoryId) {
            list.classList.add("d-none");
            if (placeholder) {
                placeholder.classList.remove("d-none");
                placeholder.textContent = "Select a category to see asset types.";
            }
            return;
        }

        var visibleCount = 0;
        config.types.forEach(function (type) {
            if (type.categoryId !== draft.categoryId) {
                return;
            }

            if (!matchesSearch(type.name)) {
                return;
            }

            visibleCount += 1;
            var button = renderOptionButton(type, draft.typeId === type.id);
            button.addEventListener("click", function () {
                selectType(type);
            });
            list.appendChild(button);
        });

        if (placeholder) {
            placeholder.classList.toggle("d-none", visibleCount > 0);
            placeholder.textContent = visibleCount > 0
                ? ""
                : "No asset types match your search for this category.";
        }

        list.classList.toggle("d-none", visibleCount === 0);
    }

    function renderSubTypes() {
        var list = byId("classifier-subtype-list");
        var placeholder = byId("classifier-subtype-placeholder");
        var createPanel = byId("classifier-create-panel");
        if (!list) {
            return;
        }

        list.innerHTML = "";
        if (!draft.typeId) {
            list.classList.add("d-none");
            if (createPanel) {
                createPanel.classList.add("d-none");
            }
            if (placeholder) {
                placeholder.classList.remove("d-none");
                placeholder.textContent = "Select an asset type to browse or create sub-types.";
            }
            return;
        }

        if (createPanel) {
            createPanel.classList.remove("d-none");
        }

        var visibleCount = 0;
        subTypes.forEach(function (item) {
            if (!matchesSearch(item.name)) {
                return;
            }

            visibleCount += 1;
            var meta = item.stockCount ? item.stockCount + " in stock" : "";
            var button = renderOptionButton(item, draft.subTypeId === item.id, meta);
            button.setAttribute("data-default-cost", item.defaultAcquisitionCost || "");
            button.addEventListener("click", function () {
                selectSubType(item);
            });
            list.appendChild(button);
        });

        if (placeholder) {
            placeholder.classList.toggle("d-none", true);
        }

        list.classList.remove("d-none");
        if (visibleCount === 0 && searchQuery) {
            var empty = document.createElement("li");
            empty.className = "am-classification-empty-panel";
            empty.textContent = "No sub-types match your search. Create one below.";
            list.appendChild(empty);
        }
    }

    function updatePreview() {
        var preview = byId("classifier-preview-text");
        if (!preview) {
            return;
        }

        if (!draft.categoryId || !draft.typeId) {
            preview.textContent = "None yet";
            return;
        }

        var text = draft.categoryName + " > " + draft.typeName;
        if (draft.subTypeId > 0 && draft.subTypeName) {
            text += " > " + draft.subTypeName;
        } else {
            text += " > (select sub-type)";
        }

        preview.textContent = text;
    }

    function updateApplyState() {
        var applyButton = byId("asset-classification-apply");
        if (applyButton) {
            applyButton.disabled = !(draft.categoryId > 0 && draft.typeId > 0 && draft.subTypeId > 0);
        }
    }

    function selectCategory(category) {
        draft.categoryId = category.id;
        draft.categoryName = category.name;
        if (draft.typeId) {
            var type = findType(draft.typeId);
            if (!type || type.categoryId !== category.id) {
                draft.typeId = 0;
                draft.typeName = "";
                draft.subTypeId = 0;
                draft.subTypeName = "";
                subTypes = [];
            }
        }
        renderCategories();
        renderTypes();
        renderSubTypes();
        updatePreview();
        updateApplyState();
    }

    function selectType(type) {
        var category = findCategory(type.categoryId);
        draft.categoryId = type.categoryId;
        draft.categoryName = category ? category.name : draft.categoryName;
        draft.typeId = type.id;
        draft.typeName = type.name;
        draft.subTypeId = 0;
        draft.subTypeName = "";
        subTypes = [];
        renderCategories();
        renderTypes();
        loadSubTypes(type.id).then(function () {
            renderSubTypes();
            updatePreview();
            updateApplyState();
        });
    }

    function selectSubType(item) {
        draft.subTypeId = item.id;
        draft.subTypeName = item.name;
        draft.pendingDefaultCost = item.defaultAcquisitionCost || "";
        renderSubTypes();
        updatePreview();
        updateApplyState();
    }

    function loadSubTypes(typeId) {
        if (!modalEl || !typeId) {
            subTypes = [];
            return Promise.resolve([]);
        }

        var baseUrl = modalEl.getAttribute("data-am-subtype-by-type-url") || "";
        if (!baseUrl) {
            subTypes = [];
            return Promise.resolve([]);
        }

        return fetch(baseUrl + "?assetTypeId=" + encodeURIComponent(typeId), {
            credentials: "same-origin"
        })
            .then(function (response) {
                return response.json();
            })
            .then(function (items) {
                subTypes = items || [];
                return subTypes;
            })
            .catch(function () {
                subTypes = [];
                return [];
            });
    }

    function prefillCreateForm() {
        var nameField = byId("classifier-create-name");
        var assetName = byId("AssetName");
        if (nameField) {
            nameField.value = assetName && assetName.value.trim()
                ? assetName.value.trim()
                : "";
        }
    }

    function clearCreateForm() {
        var nameField = byId("classifier-create-name");
        if (nameField) {
            nameField.value = "";
        }
        var error = byId("classifier-create-error");
        if (error) {
            error.style.display = "none";
            error.textContent = "";
        }
    }

    function createSubType() {
        var error = byId("classifier-create-error");
        if (!modalEl || !draft.typeId) {
            return;
        }

        if (error) {
            error.style.display = "none";
            error.textContent = "";
        }

        var name = (byId("classifier-create-name") || {}).value || "";

        if (!name.trim()) {
            if (error) {
                error.textContent = "Enter a display name for the new sub-type.";
                error.style.display = "block";
            }
            return;
        }

        var createUrl = modalEl.getAttribute("data-am-subtype-create-url") || "";
        if (!createUrl) {
            if (error) {
                error.textContent = "Sub-type create URL is not configured on this page.";
                error.style.display = "block";
            }
            return;
        }

        var body = "__RequestVerificationToken=" + encodeURIComponent(getAntiForgeryToken())
            + "&assetTypeId=" + encodeURIComponent(draft.typeId)
            + "&name=" + encodeURIComponent(name)
            + "&brand=" + encodeURIComponent("")
            + "&model=" + encodeURIComponent("");

        fetch(createUrl, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: body
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Request failed (" + response.status + ").");
                }
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

                draft.subTypeId = result.id;
                draft.subTypeName = result.name;
                if (result.categoryId) {
                    var category = findCategory(result.categoryId);
                    draft.categoryId = result.categoryId;
                    draft.categoryName = category ? category.name : draft.categoryName;
                }
                if (result.assetTypeId) {
                    var type = findType(result.assetTypeId);
                    draft.typeId = result.assetTypeId;
                    draft.typeName = type ? type.name : draft.typeName;
                }

                loadSubTypes(draft.typeId).then(function () {
                    draft.pendingDefaultCost = result.defaultAcquisitionCost || "";
                    renderCategories();
                    renderTypes();
                    renderSubTypes();
                    updatePreview();
                    updateApplyState();
                    applySelection(draft, {
                        applyCost: true,
                        defaultCost: draft.pendingDefaultCost
                    });
                    hideModal(modalEl);
                });
            })
            .catch(function (err) {
                if (error) {
                    error.textContent = (err && err.message) || "Unable to create sub-type.";
                    error.style.display = "block";
                }
            });
    }

    function openModal() {
        if (!modalEl) {
            return;
        }

        draft = cloneSelection(readAppliedSelection());
        searchQuery = "";
        var searchInput = byId("classifier-search");
        if (searchInput) {
            searchInput.value = "";
        }

        var applyError = byId("classifier-apply-error");
        if (applyError) {
            applyError.style.display = "none";
            applyError.textContent = "";
        }

        clearCreateForm();
        prefillCreateForm();
        renderCategories();
        renderTypes();

        if (draft.typeId) {
            loadSubTypes(draft.typeId).then(function () {
                renderSubTypes();
                updatePreview();
                updateApplyState();
            });
        } else {
            renderSubTypes();
            updatePreview();
            updateApplyState();
        }

        showModal(modalEl);
    }

    function confirmApply() {
        var applyError = byId("classifier-apply-error");
        if (!(draft.categoryId > 0 && draft.typeId > 0 && draft.subTypeId > 0)) {
            if (applyError) {
                applyError.textContent = "Choose a category, asset type, and sub-type before applying.";
                applyError.style.display = "block";
            }
            return;
        }

        if (applyError) {
            applyError.style.display = "none";
            applyError.textContent = "";
        }

        applySelection(draft, {
            applyCost: true,
            defaultCost: draft.pendingDefaultCost
        });
        hideModal(modalEl);
    }

    function initAssetClassificationPicker() {
        modalEl = byId("assetClassificationPickerModal");
        if (!modalEl) {
            return;
        }

        ensureModalInBody(modalEl);
        modalEl.addEventListener("hidden.bs.modal", cleanupStaleModalState);

        config = parseConfig();
        draft = cloneSelection(config.selected || readAppliedSelection());
        updateSummary(readAppliedSelection());

        var changeButton = byId("asset-classification-change");
        if (changeButton) {
            changeButton.addEventListener("click", openModal);
        }

        var applyButton = byId("asset-classification-apply");
        if (applyButton) {
            applyButton.addEventListener("click", confirmApply);
        }

        var searchInput = byId("classifier-search");
        if (searchInput) {
            searchInput.addEventListener("input", function () {
                searchQuery = searchInput.value.trim().toLowerCase();
                renderCategories();
                renderTypes();
                renderSubTypes();
            });
        }

        var createButton = byId("classifier-create-btn");
        if (createButton) {
            createButton.addEventListener("click", createSubType);
        }

        var clearCreateButton = byId("classifier-create-clear");
        if (clearCreateButton) {
            clearCreateButton.addEventListener("click", clearCreateForm);
        }
    }

    global.AmAssetClassificationPicker = {
        init: initAssetClassificationPicker
    };

    if (global.document.readyState === "loading") {
        global.document.addEventListener("DOMContentLoaded", initAssetClassificationPicker);
    } else {
        initAssetClassificationPicker();
    }
})(window);
