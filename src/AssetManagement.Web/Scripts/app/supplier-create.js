/* eslint-env browser */
(function () {
    var activeCatalogContainer = null;

    function byId(id) {
        return document.getElementById(id);
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;");
    }

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

    function loadTaggedAssets(taggedAssetsUrl, categoryId, assetTypeId, taggedSelect, selectedId) {
        if (!taggedAssetsUrl || !taggedSelect) {
            return Promise.resolve();
        }

        var url = buildTaggedAssetsUrl(taggedAssetsUrl, categoryId, assetTypeId);
        return fetch(url, { credentials: "same-origin" })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Failed to load assets");
                }
                return response.json();
            })
            .then(function (items) {
                populateTaggedAssets(taggedSelect, items || [], selectedId);
            })
            .catch(function () {
                populateTaggedAssets(taggedSelect, [], selectedId);
            });
    }

    function setInputValue(container, selector, value) {
        var input = container.querySelector(selector);
        if (input && value !== undefined && value !== null) {
            input.value = value;
        }
    }

    function applyCatalogAsset(container, asset, allTypeOptions, taggedAssetsUrl) {
        if (!container || !asset) {
            return;
        }

        var categorySelect = container.querySelector(".catalog-category");
        var typeSelect = container.querySelector(".catalog-asset-type");
        var taggedSelect = container.querySelector(".catalog-tagged-asset");
        if (!categorySelect || !typeSelect || !taggedSelect) {
            return;
        }

        setInputValue(container, ".catalog-item-name", asset.itemName || asset.assetName || "");
        setInputValue(container, 'input[name$=".Sku"], input[name="Sku"]', asset.serialNumber || "");
        setInputValue(container, 'input[name$=".ItemDescription"], input[name="ItemDescription"]', asset.itemDescription || "");

        if (asset.acquisitionCost && Number(asset.acquisitionCost) > 0) {
            setInputValue(container, ".catalog-unit-price, input[name='UnitPrice']", Number(asset.acquisitionCost).toFixed(2));
        }

        if (asset.categoryId) {
            categorySelect.value = String(asset.categoryId);
        }

        filterAssetTypes(categorySelect, typeSelect, allTypeOptions);

        if (asset.assetTypeId) {
            typeSelect.value = String(asset.assetTypeId);
        }

        loadTaggedAssets(
            taggedAssetsUrl,
            categorySelect.value,
            typeSelect.value,
            taggedSelect,
            asset.id ? String(asset.id) : null
        ).then(function () {
            if (asset.id) {
                taggedSelect.value = String(asset.id);
            }
            if (!taggedSelect.value && asset.label) {
                var option = document.createElement("option");
                option.value = String(asset.id);
                option.textContent = asset.label;
                taggedSelect.appendChild(option);
                taggedSelect.value = String(asset.id);
            }
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
            "<td><div class=\"input-group input-group-sm\">" +
            "<input class=\"form-control form-control-sm catalog-item-name\" name=\"CatalogItems[" + index + "].ItemName\" placeholder=\"e.g. Dell Latitude laptop\" />" +
            "<button type=\"button\" class=\"btn btn-outline-secondary catalog-pick-asset\" title=\"Pick from asset register\">Pick</button></div></td>" +
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
        bindPickAssetButtons(document, allTypeOptions, taggedAssetsUrl);
    }

    function ensureModalInBody(modal) {
        if (!modal || modal.parentElement === document.body) {
            return;
        }
        document.body.appendChild(modal);
    }

    function showModal(modal) {
        if (!modal) {
            return;
        }
        ensureModalInBody(modal);
        if (window.bootstrap && window.bootstrap.Modal) {
            window.bootstrap.Modal.getOrCreateInstance(modal).show();
            return;
        }
        if (window.jQuery) {
            window.jQuery(modal).modal("show");
        }
    }

    function hideModal(modal) {
        if (!modal) {
            return;
        }
        if (window.bootstrap && window.bootstrap.Modal) {
            window.bootstrap.Modal.getOrCreateInstance(modal).hide();
            return;
        }
        if (window.jQuery) {
            window.jQuery(modal).modal("hide");
        }
    }

    function buildCatalogAssetQuery(state) {
        var params = [];
        if (state.search) {
            params.push("search=" + encodeURIComponent(state.search));
        }
        if (state.departmentId) {
            params.push("departmentId=" + encodeURIComponent(state.departmentId));
        }
        if (state.status) {
            params.push("status=" + encodeURIComponent(state.status));
        }
        params.push("sort=" + encodeURIComponent(state.sort || "tag"));
        params.push("direction=" + encodeURIComponent(state.direction || "asc"));
        params.push("page=" + encodeURIComponent(state.page || 1));
        params.push("pageSize=" + encodeURIComponent(state.pageSize || 10));
        return params.join("&");
    }

    function initCatalogAssetPicker(allTypeOptions, taggedAssetsUrl) {
        var modal = byId("catalogAssetPickerModal");
        if (!modal || modal.getAttribute("data-am-catalog-picker-bound") === "true") {
            return;
        }

        modal.setAttribute("data-am-catalog-picker-bound", "true");
        ensureModalInBody(modal);

        var searchUrl = modal.getAttribute("data-am-catalog-asset-search-url") || "";
        var filterForm = byId("catalog-asset-picker-filters");
        var tbody = byId("catalog-asset-picker-body");
        var loading = byId("catalog-asset-picker-loading");
        var errorBox = byId("catalog-asset-picker-error");
        var emptyState = byId("catalog-asset-picker-empty");
        var table = byId("catalog-asset-picker-table");
        var pagination = byId("catalog-asset-picker-pagination");
        var rangeLabel = byId("catalog-asset-picker-range");
        var prevBtn = byId("catalog-asset-picker-prev");
        var nextBtn = byId("catalog-asset-picker-next");

        var state = {
            search: "",
            departmentId: "",
            status: "",
            sort: "tag",
            direction: "asc",
            page: 1,
            pageSize: 10,
            totalPages: 1
        };

        function setLoading(isLoading) {
            if (loading) {
                loading.style.display = isLoading ? "block" : "none";
            }
            if (table) {
                table.style.display = isLoading ? "none" : "";
            }
        }

        function showError(message) {
            if (!errorBox) {
                return;
            }
            errorBox.textContent = message;
            errorBox.style.display = message ? "block" : "none";
        }

        function updateSortIndicators() {
            modal.querySelectorAll(".am-catalog-asset-sort").forEach(function (button) {
                var sortKey = button.getAttribute("data-sort");
                var label = button.textContent.replace(/\s+[▲▼]$/, "");
                if (sortKey === state.sort) {
                    button.textContent = label + (state.direction === "desc" ? " ▼" : " ▲");
                } else {
                    button.textContent = label;
                }
            });
        }

        function renderRows(items) {
            if (!tbody) {
                return;
            }

            tbody.innerHTML = "";
            if (!items || !items.length) {
                if (emptyState) {
                    emptyState.style.display = "block";
                }
                if (table) {
                    table.style.display = "none";
                }
                if (pagination) {
                    pagination.style.display = "none";
                }
                return;
            }

            if (emptyState) {
                emptyState.style.display = "none";
            }
            if (table) {
                table.style.display = "";
            }

            items.forEach(function (item) {
                var tr = document.createElement("tr");
                tr.className = "border-bottom border-light";
                tr.innerHTML =
                    "<td class=\"ps-3\"><span class=\"am-tag-pill\">" + escapeHtml(item.assetTag) + "</span></td>" +
                    "<td><span class=\"fw-bold text-dark\">" + escapeHtml(item.assetName) + "</span></td>" +
                    "<td>" + escapeHtml(item.categoryName) + "</td>" +
                    "<td>" + escapeHtml(item.subTypeName || "—") + "</td>" +
                    "<td>" + escapeHtml(item.serialNumber || "—") + "</td>" +
                    "<td><span class=\"fw-bold text-primary\">" + escapeHtml(item.acquisitionCostDisplay) + "</span></td>" +
                    "<td class=\"text-end pe-3\"><button type=\"button\" class=\"btn btn-sm btn-primary am-catalog-asset-select\">Select</button></td>";

                tr.querySelector(".am-catalog-asset-select").addEventListener("click", function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    if (activeCatalogContainer) {
                        applyCatalogAsset(activeCatalogContainer, item, allTypeOptions, taggedAssetsUrl);
                    }
                    hideModal(modal);
                });

                tbody.appendChild(tr);
            });
        }

        function updatePagination(meta) {
            state.page = meta.page || 1;
            state.totalPages = meta.totalPages || 1;

            if (pagination) {
                pagination.style.display = meta.totalCount > 0 ? "flex" : "none";
            }
            if (rangeLabel) {
                rangeLabel.textContent = meta.totalCount > 0
                    ? "Showing " + meta.startItem + "–" + meta.endItem + " of " + meta.totalCount
                    : "";
            }
            if (prevBtn) {
                prevBtn.disabled = state.page <= 1;
            }
            if (nextBtn) {
                nextBtn.disabled = state.page >= state.totalPages;
            }
        }

        function readFiltersFromForm() {
            var searchInput = byId("catalog-asset-picker-search");
            var departmentSelect = byId("catalog-asset-picker-department");
            var statusSelect = byId("catalog-asset-picker-status");
            var pageSizeSelect = byId("catalog-asset-picker-page-size");

            state.search = searchInput ? searchInput.value.trim() : "";
            state.departmentId = departmentSelect ? departmentSelect.value : "";
            state.status = statusSelect ? statusSelect.value : "";
            state.pageSize = pageSizeSelect ? parseInt(pageSizeSelect.value, 10) || 10 : 10;
        }

        function loadAssets() {
            if (!searchUrl) {
                showError("Asset search is not configured.");
                return;
            }

            showError("");
            setLoading(true);

            fetch(searchUrl + "?" + buildCatalogAssetQuery(state), {
                credentials: "same-origin",
                headers: { Accept: "application/json" }
            })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error("Request failed (" + response.status + ")");
                    }
                    return response.json();
                })
                .then(function (data) {
                    setLoading(false);
                    renderRows(data.items || []);
                    updatePagination(data);
                    updateSortIndicators();
                })
                .catch(function () {
                    setLoading(false);
                    renderRows([]);
                    showError("Could not load assets. Try again.");
                });
        }

        function openPickerLoad() {
            readFiltersFromForm();
            state.page = 1;
            loadAssets();
        }

        if (filterForm) {
            filterForm.addEventListener("submit", function (event) {
                event.preventDefault();
                readFiltersFromForm();
                state.page = 1;
                loadAssets();
            });
        }

        modal.querySelectorAll(".am-catalog-asset-sort").forEach(function (button) {
            button.addEventListener("click", function () {
                var sortKey = button.getAttribute("data-sort");
                if (state.sort === sortKey) {
                    state.direction = state.direction === "asc" ? "desc" : "asc";
                } else {
                    state.sort = sortKey;
                    state.direction = "asc";
                }
                state.page = 1;
                loadAssets();
            });
        });

        if (prevBtn) {
            prevBtn.addEventListener("click", function () {
                if (state.page > 1) {
                    state.page -= 1;
                    loadAssets();
                }
            });
        }

        if (nextBtn) {
            nextBtn.addEventListener("click", function () {
                if (state.page < state.totalPages) {
                    state.page += 1;
                    loadAssets();
                }
            });
        }

        modal.addEventListener("shown.bs.modal", openPickerLoad);
        if (window.jQuery) {
            window.jQuery(modal).on("shown.bs.modal", openPickerLoad);
        }
    }

    function bindPickAssetButtons(root, allTypeOptions, taggedAssetsUrl) {
        root.querySelectorAll(".catalog-pick-asset").forEach(function (button) {
            if (button.getAttribute("data-am-bound") === "true") {
                return;
            }
            button.setAttribute("data-am-bound", "true");
            button.addEventListener("click", function () {
                activeCatalogContainer = button.closest(".catalog-item-row")
                    || button.closest("form[data-am-supplier-catalog-add]");
                initCatalogAssetPicker(allTypeOptions, taggedAssetsUrl);
                showModal(byId("catalogAssetPickerModal"));
            });
        });
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
        bindPickAssetButtons(document, allTypeOptions, taggedAssetsUrl);

        if (addBtn) {
            addBtn.addEventListener("click", function () {
                addRow(form, tbody, allTypeOptions, taggedAssetsUrl);
            });
        }
    }

    function initCatalogAddPanelToggle() {
        var panel = document.getElementById("supplier-catalog-add-panel");
        var showBtn = document.getElementById("show-supplier-catalog-add");
        var cancelBtn = document.getElementById("cancel-supplier-catalog-add");
        var form = document.getElementById("supplier-catalog-add-form");

        if (!panel || !showBtn) {
            return;
        }

        function showPanel() {
            panel.classList.remove("d-none");
            panel.removeAttribute("hidden");
            showBtn.classList.add("d-none");
            var itemNameInput = form && form.querySelector(".catalog-item-name");
            if (itemNameInput) {
                itemNameInput.focus();
            }
        }

        function hidePanel() {
            panel.classList.add("d-none");
            panel.setAttribute("hidden", "hidden");
            showBtn.classList.remove("d-none");
            if (form) {
                form.reset();
            }
        }

        showBtn.addEventListener("click", showPanel);
        if (cancelBtn) {
            cancelBtn.addEventListener("click", hidePanel);
        }
    }

    function initCatalogAddForm() {
        var form = document.getElementById("supplier-catalog-add-form")
            || document.querySelector("form[data-am-supplier-catalog-add]");
        if (!form) {
            return;
        }

        var taggedAssetsUrl = form.getAttribute("data-am-tagged-assets-url") || "";
        var typeSelect = form.querySelector(".catalog-asset-type");
        var allTypeOptions = typeSelect ? readTypeOptions(typeSelect) : [];
        bindCatalogRow(form, allTypeOptions, taggedAssetsUrl);
        bindPickAssetButtons(document, allTypeOptions, taggedAssetsUrl);
    }

    function boot() {
        initCreateForm();
        initCatalogAddPanelToggle();
        initCatalogAddForm();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();
