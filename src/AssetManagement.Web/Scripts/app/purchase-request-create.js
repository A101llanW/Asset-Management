/* eslint-env browser */
(function () {
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

    function getModal() {
        return byId("targetAssetPickerModal");
    }

    function getHiddenInput() {
        return byId("TargetAssetId");
    }

    function getDisplayInput() {
        return byId("target-asset-display");
    }

    function ensureModalInBody(modal) {
        if (!modal || modal.parentElement === document.body) {
            return;
        }

        document.body.appendChild(modal);
    }

    function cleanupStaleModalState() {
        var openModals = document.querySelectorAll(".modal.show");
        if (openModals.length > 0) {
            return;
        }

        document.querySelectorAll(".modal-backdrop").forEach(function (backdrop) {
            backdrop.remove();
        });
        document.body.classList.remove("modal-open");
        document.body.style.removeProperty("overflow");
        document.body.style.removeProperty("padding-right");
    }

    function showModal(modal) {
        if (!modal) {
            return;
        }

        ensureModalInBody(modal);
        cleanupStaleModalState();

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

    function buildQuery(state) {
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

    function initTargetAssetPicker() {
        var modal = getModal();
        if (!modal) {
            return;
        }

        ensureModalInBody(modal);

        var searchUrl = modal.getAttribute("data-am-target-asset-search-url") || "";
        var hiddenInput = getHiddenInput();
        var displayInput = getDisplayInput();
        var filterForm = byId("target-asset-picker-filters");
        var tbody = byId("target-asset-picker-body");
        var loading = byId("target-asset-picker-loading");
        var errorBox = byId("target-asset-picker-error");
        var emptyState = byId("target-asset-picker-empty");
        var table = byId("target-asset-picker-table");
        var pagination = byId("target-asset-picker-pagination");
        var rangeLabel = byId("target-asset-picker-range");
        var prevBtn = byId("target-asset-picker-prev");
        var nextBtn = byId("target-asset-picker-next");
        var clearBtn = byId("clear-target-asset");

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
            var buttons = modal.querySelectorAll(".am-target-asset-sort");
            for (var i = 0; i < buttons.length; i++) {
                var button = buttons[i];
                var sortKey = button.getAttribute("data-sort");
                var label = button.textContent.replace(/\s+[▲▼]$/, "");
                if (sortKey === state.sort) {
                    button.textContent = label + (state.direction === "desc" ? " ▼" : " ▲");
                } else {
                    button.textContent = label;
                }
            }
        }

        function appendCell(tr, html, className) {
            var td = document.createElement("td");
            if (className) {
                td.className = className;
            }
            td.innerHTML = html;
            tr.appendChild(td);
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

            for (var i = 0; i < items.length; i++) {
                var item = items[i];
                var tr = document.createElement("tr");
                tr.className = "border-bottom border-light";

                appendCell(tr, '<span class="am-tag-pill">' + escapeHtml(item.assetTag) + "</span>", "ps-3");
                appendCell(tr, '<span class="fw-bold text-dark">' + escapeHtml(item.assetName) + "</span>");
                appendCell(tr, escapeHtml(item.categoryName));
                appendCell(tr, escapeHtml(item.subTypeName || "—"));
                appendCell(tr, '<span class="text-secondary small fw-medium">' + escapeHtml(item.departmentName) + "</span>");
                appendCell(tr, '<span class="text-muted" style="font-size: 0.85rem;">' + escapeHtml(item.custodianName) + "</span>");
                appendCell(
                    tr,
                    '<span class="badge bg-' + escapeHtml(item.statusBadge) + ' badge-status px-2 py-1.5 rounded-pill text-capitalize">' +
                    escapeHtml(item.status) + "</span>"
                );
                appendCell(tr, '<span class="fw-bold text-primary">' + escapeHtml(item.acquisitionCostDisplay) + "</span>");

                var actionCell = document.createElement("td");
                actionCell.className = "text-end pe-3";
                var selectButton = document.createElement("button");
                selectButton.type = "button";
                selectButton.className = "btn btn-sm btn-primary am-target-asset-select";
                selectButton.setAttribute("data-asset-id", item.id);
                selectButton.setAttribute("data-asset-label", item.label || "");
                if (item.departmentId) {
                    selectButton.setAttribute("data-department-id", item.departmentId);
                }
                if (item.departmentName) {
                    selectButton.setAttribute("data-department-name", item.departmentName);
                }
                if (item.itemDescription) {
                    selectButton.setAttribute("data-item-description", item.itemDescription);
                }
                if (item.quantityInStock !== undefined && item.quantityInStock !== null) {
                    selectButton.setAttribute("data-quantity-in-stock", item.quantityInStock);
                }
                selectButton.textContent = "Select";
                selectButton.addEventListener("click", function (event) {
                    event.preventDefault();
                    event.stopPropagation();
                    selectAsset(event.currentTarget);
                });
                actionCell.appendChild(selectButton);
                tr.appendChild(actionCell);

                tbody.appendChild(tr);
            }
        }

        function updatePagination(meta) {
            state.page = meta.page || 1;
            state.totalPages = meta.totalPages || 1;

            if (pagination) {
                pagination.style.display = meta.totalCount > 0 ? "flex" : "none";
            }
            if (rangeLabel) {
                if (meta.totalCount > 0) {
                    rangeLabel.textContent = "Showing " + meta.startItem + "–" + meta.endItem + " of " + meta.totalCount;
                } else {
                    rangeLabel.textContent = "";
                }
            }
            if (prevBtn) {
                prevBtn.disabled = state.page <= 1;
            }
            if (nextBtn) {
                nextBtn.disabled = state.page >= state.totalPages;
            }
        }

        function readFiltersFromForm() {
            var searchInput = byId("target-asset-picker-search");
            var departmentSelect = byId("target-asset-picker-department");
            var statusSelect = byId("target-asset-picker-status");
            var pageSizeSelect = byId("target-asset-picker-page-size");

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

            fetch(searchUrl + "?" + buildQuery(state), {
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
                .catch(function (error) {
                    setLoading(false);
                    renderRows([]);
                    var message = "Could not load assets. Try again.";
                    if (error && error.message && error.message.indexOf("(404)") >= 0) {
                        message += " The asset search endpoint was not found — rebuild and restart the web app.";
                    }
                    showError(message);
                });
        }

        function openModal(event) {
            if (event) {
                event.preventDefault();
            }
            showModal(modal);
        }

        function isDepartmentLocked() {
            var form = byId("purchase-request-form");
            return form && form.getAttribute("data-am-lock-department") === "true";
        }

        function applyAssetToForm(button) {
            if (!button) {
                return;
            }

            var itemDescription = button.getAttribute("data-item-description") || "";
            var descriptionInput = byId("ItemDescription");
            if (descriptionInput && itemDescription) {
                descriptionInput.value = itemDescription;
            }

            var quantityInStock = button.getAttribute("data-quantity-in-stock");
            var quantityInput = byId("QuantityInStock");
            if (quantityInput && quantityInStock !== null && quantityInStock !== "") {
                quantityInput.value = quantityInStock;
            }

            if (isDepartmentLocked()) {
                return;
            }

            var departmentId = button.getAttribute("data-department-id");
            var departmentSelect = byId("DepartmentId");
            if (!departmentSelect || !departmentId) {
                return;
            }

            var hasOption = false;
            for (var i = 0; i < departmentSelect.options.length; i++) {
                if (departmentSelect.options[i].value === String(departmentId)) {
                    hasOption = true;
                    break;
                }
            }

            if (hasOption) {
                departmentSelect.value = String(departmentId);
            }
        }

        function selectAsset(button) {
            if (!button) {
                return;
            }

            var assetId = button.getAttribute("data-asset-id");
            var label = button.getAttribute("data-asset-label") || "";

            if (hiddenInput) {
                hiddenInput.value = assetId;
            }
            if (displayInput) {
                displayInput.value = label;
            }

            applyAssetToForm(button);
            hideModal(modal);
        }

        function clearSelection() {
            if (hiddenInput) {
                hiddenInput.value = "";
            }
            if (displayInput) {
                displayInput.value = "";
            }
        }

        if (filterForm) {
            filterForm.addEventListener("submit", function (event) {
                event.preventDefault();
                readFiltersFromForm();
                state.page = 1;
                loadAssets();
            });
        }

        var sortButtons = modal.querySelectorAll(".am-target-asset-sort");
        for (var s = 0; s < sortButtons.length; s++) {
            sortButtons[s].addEventListener("click", function () {
                var sortKey = this.getAttribute("data-sort");
                if (state.sort === sortKey) {
                    state.direction = state.direction === "asc" ? "desc" : "asc";
                } else {
                    state.sort = sortKey;
                    state.direction = "asc";
                }
                state.page = 1;
                loadAssets();
            });
        }

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

        modal.addEventListener("click", function (event) {
            var button = event.target && event.target.closest
                ? event.target.closest(".am-target-asset-select")
                : null;
            if (!button) {
                return;
            }
            event.preventDefault();
            selectAsset(button);
        });

        modal.addEventListener("hidden.bs.modal", cleanupStaleModalState);

        modal.addEventListener("shown.bs.modal", function () {
            readFiltersFromForm();
            state.page = 1;
            loadAssets();
        });

        if (displayInput) {
            displayInput.addEventListener("click", openModal);
        }
        var openBtn = byId("open-target-asset-picker");
        if (openBtn) {
            openBtn.addEventListener("click", openModal);
        }
        if (clearBtn) {
            clearBtn.addEventListener("click", clearSelection);
        }
    }

    function boot() {
        initTargetAssetPicker();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();
