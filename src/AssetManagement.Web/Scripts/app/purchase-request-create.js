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

    function getLineItemsContainer() {
        return byId("purchase-line-items");
    }

    function getAssetNameMax() {
        var container = getLineItemsContainer();
        var max = container ? parseInt(container.getAttribute("data-am-asset-name-max"), 10) : NaN;
        return isNaN(max) || max <= 0 ? 200 : max;
    }

    function setHidden(el, hidden) {
        if (!el) {
            return;
        }
        if (hidden) {
            el.classList.add("d-none");
        } else {
            el.classList.remove("d-none");
        }
    }

    /*
     * Tagging rule (mirrors RequisitionTargetRules.DecideTargetOnTag): while no target is chosen, tagging an asset
     * pre-fills the target as a ROOM (the asset's room / department); a room or person already chosen (or a locked
     * department) is kept, with an inline notice when the asset sits somewhere else.
     */
    function decideTargetOnTag(kind, departmentId, userId, assetDepartmentId, locked) {
        var hasDept = !!departmentId && String(departmentId) !== "0";
        var hasAssetDept = !!assetDepartmentId && String(assetDepartmentId) !== "0";
        var chosen = locked || (kind === "Person" && !!userId) || (kind === "Room" && hasDept);
        if (chosen) {
            return hasAssetDept && (kind === "Person" || String(departmentId) !== String(assetDepartmentId)) ? "KeepWithNotice" : "Keep";
        }
        return hasAssetDept ? "PrefillRoom" : "Keep";
    }

    function taggedAssetKeptNotice(location) {
        return "The tagged asset sits in " + (location && String(location).trim() ? String(location).trim() : "another room") +
            "; the requisition target you chose was kept.";
    }

    var requisitionTarget = null;

    /*
     * "Requisition target (room or person)" (migration 110): Room shows the grouped room / department list; Person shows
     * the organisation's users. A person with a room or classroom assigned asks where the asset goes (their room, or
     * company custody held by them); a person without one keeps it in company custody held by them, without asking.
     * In Person mode the approval department is the person's routing department (posted as DepartmentId), unless the
     * requester's department is locked. The server re-resolves all of it on submit.
     */
    function initRequisitionTarget() {
        var root = byId("requisition-target");
        if (!root) {
            return null;
        }
        var form = byId("purchase-request-form");
        var locked = !!form && form.getAttribute("data-am-lock-department") === "true";
        var roomPanel = byId("target-room-panel");
        var personPanel = byId("target-person-panel");
        var prompt = byId("target-kind-prompt");
        var personSelect = byId("TargetUserId");
        var placementPanel = byId("target-person-placement");
        var noRoom = byId("target-person-no-room");
        var noRoomValue = byId("target-placement-no-room");
        var routing = byId("target-person-routing");
        var noRouting = byId("target-person-no-routing");
        var notice = byId("target-tag-notice");
        var placementRadios = root.querySelectorAll(".am-target-placement");
        var kindRadios = root.querySelectorAll(".am-target-kind");

        function kind() {
            for (var i = 0; i < kindRadios.length; i++) {
                if (kindRadios[i].checked) {
                    return kindRadios[i].value;
                }
            }
            return "";
        }

        function selectedPerson() {
            if (!personSelect || personSelect.selectedIndex < 0) {
                return null;
            }
            var opt = personSelect.options[personSelect.selectedIndex];
            return opt && opt.value ? opt : null;
        }

        function roomSelect() {
            return locked ? null : byId("DepartmentId");
        }

        function setText(selector, text) {
            var nodes = root.querySelectorAll(selector);
            for (var i = 0; i < nodes.length; i++) {
                nodes[i].textContent = text;
            }
        }

        function update() {
            var k = kind();
            var person = k === "Person";
            setHidden(roomPanel, k !== "Room");
            setHidden(personPanel, !person);
            setHidden(prompt, !!k);
            var rooms = roomSelect();
            if (rooms) {
                rooms.disabled = k !== "Room";
                rooms.required = k === "Room";
            }
            if (personSelect) {
                personSelect.disabled = !person;
                personSelect.required = person;
            }
            var opt = person ? selectedPerson() : null;
            var roomId = opt ? (opt.getAttribute("data-room-id") || "") : "";
            var hasRoom = !!opt && !!roomId;
            setHidden(placementPanel, !hasRoom);
            for (var i = 0; i < placementRadios.length; i++) {
                placementRadios[i].disabled = !hasRoom;
                placementRadios[i].required = hasRoom;
            }
            setHidden(noRoom, !(opt && !hasRoom));
            if (noRoomValue) {
                noRoomValue.disabled = !(opt && !hasRoom);
            }
            var routingId = opt ? (opt.getAttribute("data-routing-department-id") || "") : "";
            if (routing) {
                routing.disabled = !person;
                routing.value = routingId;
            }
            setHidden(noRouting, !(opt && !locked && !routingId));
            setText(".am-target-person-name", opt ? opt.getAttribute("data-display-name") || opt.textContent : "");
            setText(".am-target-person-room", opt ? (opt.getAttribute("data-room-label") || "") : "");
        }

        function departmentId() {
            if (locked) {
                var lockedInput = byId("DepartmentId");
                return lockedInput ? lockedInput.value : "";
            }
            if (kind() === "Person") {
                return routing ? routing.value : "";
            }
            var rooms = roomSelect();
            return rooms && !rooms.disabled ? rooms.value : "";
        }

        function showNotice(text) {
            if (!notice) {
                return;
            }
            notice.textContent = text || "";
            setHidden(notice, !text);
        }

        function setRoom(id) {
            var rooms = roomSelect();
            if (!rooms) {
                return false;
            }
            var found = false;
            for (var i = 0; i < rooms.options.length; i++) {
                if (rooms.options[i].value === String(id)) {
                    found = true;
                    break;
                }
            }
            if (!found) {
                return false;
            }
            for (var k = 0; k < kindRadios.length; k++) {
                kindRadios[k].checked = kindRadios[k].value === "Room";
            }
            update();
            rooms.value = String(id);
            // Same as a user pick: the approval and per-line path previews refresh.
            var evt = document.createEvent("HTMLEvents");
            evt.initEvent("change", true, false);
            rooms.dispatchEvent(evt);
            return true;
        }

        root.addEventListener("change", function (e) {
            if (e && e.target && e.target.id !== "target-tag-notice") {
                showNotice("");
            }
            update();
            refreshApprovalPathPreview();
        });
        update();

        return {
            kind: kind,
            departmentId: departmentId,
            roomDepartmentId: function () {
                var rooms = roomSelect();
                return kind() === "Room" && rooms ? rooms.value : (locked ? departmentId() : "");
            },
            personId: function () {
                var opt = kind() === "Person" ? selectedPerson() : null;
                return opt ? opt.value : "";
            },
            setRoom: setRoom,
            showNotice: showNotice,
            locked: locked
        };
    }

    function isRowTagged(row) {
        var hidden = row ? row.querySelector(".am-line-target-id") : null;
        return !!(hidden && String(hidden.value || "").trim().length > 0);
    }

    /* Read-only Category / Type / Sub-type of the tagged asset (QA 2026-10-28); blank when untagged. */
    function setRowAssetClassification(row, classification) {
        var c = classification || {};
        var fields = [
            [".am-line-asset-category", c.category],
            [".am-line-asset-type", c.type],
            [".am-line-asset-subtype", c.subType]
        ];
        for (var i = 0; i < fields.length; i++) {
            var input = row.querySelector(fields[i][0]);
            if (input) {
                input.value = fields[i][1] || "";
            }
        }
    }

    /* Tagged line: name optional (defaults to the asset's name, editable); classification from the asset. */
    function setRowTagged(row, tagged, classification) {
        if (!row) {
            return;
        }
        setRowAssetClassification(row, tagged ? classification : null);
        var nameInput = row.querySelector(".am-line-item-name");
        if (nameInput) {
            nameInput.required = !tagged;
        }
        var star = row.querySelector(".am-line-name-required");
        if (star) {
            setHidden(star, tagged);
        }
        var block = row.querySelector(".am-line-classification");
        if (block) {
            // IE11 ignores classList.toggle's second argument: explicit add / remove.
            if (tagged) {
                block.classList.add("am-line-classification-from-asset");
            } else {
                block.classList.remove("am-line-classification-from-asset");
            }
            var evt;
            try {
                evt = new CustomEvent("am:line-tag-changed");
            } catch (e) {
                evt = document.createEvent("CustomEvent");
                evt.initCustomEvent("am:line-tag-changed", false, false, null);
            }
            block.dispatchEvent(evt);
        }
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

    function updateQtyInStockVisibility() {
        var group = byId("qty-in-stock-group");
        if (!group) {
            return;
        }

        var hasAsset = Array.prototype.some.call(document.querySelectorAll(".purchase-line-item"), isRowTagged);
        if (hasAsset) {
            group.classList.remove("d-none");
        } else {
            group.classList.add("d-none");
        }
    }

    function reindexLineItems() {
        var container = getLineItemsContainer();
        if (!container) {
            return;
        }

        var rows = container.querySelectorAll(".purchase-line-item");
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i];
            row.setAttribute("data-line-index", String(i));
            var itemName = row.querySelector(".am-line-item-name");
            var targetId = row.querySelector(".am-line-target-id");
            var description = row.querySelector(".am-line-description");
            var quantity = row.querySelector(".am-line-quantity");
            var removeButton = row.querySelector(".am-remove-line-item");
            if (itemName) {
                itemName.name = "Lines[" + i + "].ItemName";
                itemName.id = "Lines_" + i + "__ItemName";
            }
            if (targetId) {
                targetId.name = "Lines[" + i + "].TargetAssetId";
                targetId.id = "Lines_" + i + "__TargetAssetId";
            }
            if (description) {
                description.name = "Lines[" + i + "].Description";
                description.id = "Lines_" + i + "__Description";
            }
            if (quantity) {
                quantity.name = "Lines[" + i + "].Quantity";
                quantity.id = "Lines_" + i + "__Quantity";
            }
            if (removeButton) {
                removeButton.disabled = i === 0;
            }
        }
    }

    function addLineItem() {
        var container = getLineItemsContainer();
        if (!container) {
            return;
        }

        var index = container.querySelectorAll(".purchase-line-item").length;
        var row = document.createElement("div");
        row.className = "purchase-line-item row g-2 align-items-end border rounded p-2";
        row.setAttribute("data-line-index", String(index));
        // The per-line tag sits above the line name box (Allan 2026-10-28).
        row.innerHTML =
            "<div class=\"col-12 am-line-target\">" +
            "<input type=\"hidden\" name=\"Lines[" + index + "].TargetAssetId\" id=\"Lines_" + index + "__TargetAssetId\" class=\"am-line-target-id\" value=\"\" />" +
            "<div class=\"input-group input-group-sm\">" +
            "<span class=\"input-group-text\">Tag existing asset (optional)</span>" +
            "<input type=\"text\" class=\"form-control am-line-target-display\" readonly=\"readonly\" placeholder=\"No asset tagged\" role=\"button\" style=\"cursor: pointer;\" />" +
            "<button type=\"button\" class=\"btn btn-outline-primary am-line-target-browse\">Browse assets</button>" +
            "<button type=\"button\" class=\"btn btn-outline-secondary am-line-target-clear\" title=\"Clear tag\">&times;</button>" +
            "</div>" +
            "</div>" +
            "<div class=\"col-md-4\">" +
            "<label class=\"form-label small mb-1\">Item / asset name <span class=\"text-danger am-line-name-required\">*</span></label>" +
            "<input type=\"text\" name=\"Lines[" + index + "].ItemName\" id=\"Lines_" + index + "__ItemName\" " +
            "class=\"form-control am-line-item-name\" required=\"required\" maxlength=\"" + getAssetNameMax() + "\" />" +
            "</div>" +
            "<div class=\"col-md-4\">" +
            "<label class=\"form-label small mb-1\">Description / what you need</label>" +
            "<input type=\"text\" name=\"Lines[" + index + "].Description\" id=\"Lines_" + index + "__Description\" " +
            "class=\"form-control am-line-description\" required=\"required\" maxlength=\"2000\" />" +
            "</div>" +
            "<div class=\"col-md-2\">" +
            "<label class=\"form-label small mb-1\">Qty to order</label>" +
            "<input type=\"number\" name=\"Lines[" + index + "].Quantity\" id=\"Lines_" + index + "__Quantity\" " +
            "class=\"form-control am-line-quantity\" value=\"1\" min=\"1\" required=\"required\" />" +
            "</div>" +
            "<div class=\"col-md-2 text-end\">" +
            "<button type=\"button\" class=\"btn btn-outline-secondary btn-sm am-remove-line-item\" title=\"Remove line\">Remove</button>" +
            "</div>";
        container.appendChild(row);
        reindexLineItems();
    }

    function initLineItems() {
        var container = getLineItemsContainer();
        var addButton = byId("add-purchase-line-item");
        if (!container) {
            return;
        }

        reindexLineItems();

        if (addButton) {
            addButton.addEventListener("click", function (event) {
                event.preventDefault();
                addLineItem();
            });
        }

        container.addEventListener("click", function (event) {
            var target = event.target;
            if (!target || !target.classList || !target.classList.contains("am-remove-line-item")) {
                return;
            }
            event.preventDefault();
            var row = target.closest(".purchase-line-item");
            if (!row || container.querySelectorAll(".purchase-line-item").length <= 1) {
                return;
            }
            row.remove();
            reindexLineItems();
            updateQtyInStockVisibility();
        });
    }

    function refreshApprovalPathPreview() {
        var banner = byId("purchase-approval-path-banner");
        var summaryEl = byId("purchase-approval-path-summary");
        if (!banner || !summaryEl) {
            return;
        }

        var url = banner.getAttribute("data-am-preview-approval-url");
        if (!url) {
            return;
        }

        var departmentSelect = byId("DepartmentId");
        var departmentId = requisitionTarget ? requisitionTarget.departmentId() : (departmentSelect ? departmentSelect.value : "");
        var query = departmentId ? ("?departmentId=" + encodeURIComponent(departmentId)) : "";

        fetch(url + query, {
            credentials: "same-origin",
            headers: { Accept: "application/json" }
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Request failed");
                }
                return response.json();
            })
            .then(function (data) {
                if (data && data.summary) {
                    summaryEl.textContent = data.summary;
                }
            })
            .catch(function () {
                /* keep server-rendered summary */
            });
    }

    function initApprovalPathPreview() {
        var departmentSelect = byId("DepartmentId");
        refreshApprovalPathPreview();
        if (departmentSelect) {
            departmentSelect.addEventListener("change", refreshApprovalPathPreview);
        }
    }

    function initTargetAssetPicker() {
        var modal = getModal();
        if (!modal) {
            return;
        }

        ensureModalInBody(modal);

        var searchUrl = modal.getAttribute("data-am-target-asset-search-url") || "";
        var container = getLineItemsContainer();
        var activeRow = null; // the line the picker was opened from (tags are per line)
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
                var label = button.textContent.replace(/\s+[\u25b2\u25bc]$/, "");
                if (sortKey === state.sort) {
                    button.textContent = label + (state.direction === "desc" ? " \u25bc" : " \u25b2");
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
                appendCell(tr, escapeHtml(item.subTypeName || ""));
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
                selectButton.setAttribute("data-asset-name", item.assetName || "");
                selectButton.setAttribute("data-asset-category", item.categoryName || "");
                selectButton.setAttribute("data-asset-type", item.assetTypeName || "");
                selectButton.setAttribute("data-asset-subtype", item.subTypeName || "");
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
                    rangeLabel.textContent = "Showing " + meta.startItem + "\u2013" + meta.endItem + " of " + meta.totalCount;
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
                        message += " The asset search endpoint was not found \u2014 rebuild and restart the web app.";
                    }
                    showError(message);
                });
        }

        function openModal(event, row) {
            if (event) {
                event.preventDefault();
            }
            activeRow = row || null;
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

            // Pre-fill the tagged line: name = the asset's name (editable), description only when still blank.
            var row = activeRow;
            var assetName = button.getAttribute("data-asset-name") || "";
            var nameInput = row ? row.querySelector(".am-line-item-name") : null;
            if (nameInput && assetName) {
                nameInput.value = assetName.length > getAssetNameMax() ? assetName.substring(0, getAssetNameMax()) : assetName;
            }
            var itemDescription = button.getAttribute("data-item-description") || "";
            var descriptionInput = row ? row.querySelector(".am-line-description") : null;
            if (descriptionInput && itemDescription && !String(descriptionInput.value || "").trim()) {
                descriptionInput.value = itemDescription;
            }

            var quantityInStock = button.getAttribute("data-quantity-in-stock");
            var quantityInput = byId("QuantityInStock");
            if (quantityInput && quantityInStock !== null && quantityInStock !== "") {
                quantityInput.value = quantityInStock;
            }

            var departmentId = button.getAttribute("data-department-id");
            if (requisitionTarget) {
                var decision = decideTargetOnTag(
                    requisitionTarget.kind(),
                    requisitionTarget.roomDepartmentId(),
                    requisitionTarget.personId(),
                    departmentId,
                    isDepartmentLocked());
                if (decision === "PrefillRoom") {
                    if (requisitionTarget.setRoom(departmentId)) {
                        requisitionTarget.showNotice("");
                        refreshApprovalPathPreview();
                    }
                } else if (decision === "KeepWithNotice") {
                    requisitionTarget.showNotice(taggedAssetKeptNotice(button.getAttribute("data-department-name")));
                }
            }

            updateQtyInStockVisibility();
        }

        function selectAsset(button) {
            if (!button) {
                return;
            }

            var assetId = button.getAttribute("data-asset-id");
            var label = button.getAttribute("data-asset-label") || "";
            var row = activeRow || document.querySelector(".purchase-line-item");
            activeRow = row;
            var hiddenInput = row ? row.querySelector(".am-line-target-id") : null;
            var displayInput = row ? row.querySelector(".am-line-target-display") : null;

            if (hiddenInput) {
                hiddenInput.value = assetId;
            }
            if (displayInput) {
                displayInput.value = label;
            }

            setRowTagged(row, true, {
                category: button.getAttribute("data-asset-category") || "",
                type: button.getAttribute("data-asset-type") || "",
                subType: button.getAttribute("data-asset-subtype") || ""
            });
            applyAssetToForm(button);
            hideModal(modal);
        }

        function clearSelection(row) {
            if (!row) {
                return;
            }
            var hiddenInput = row.querySelector(".am-line-target-id");
            var displayInput = row.querySelector(".am-line-target-display");
            if (hiddenInput) {
                hiddenInput.value = "";
            }
            if (displayInput) {
                displayInput.value = "";
            }
            setRowTagged(row, false);
            updateQtyInStockVisibility();
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

        if (container) {
            // Delegated: rows added later get the same per-line Browse / clear behaviour.
            container.addEventListener("click", function (event) {
                var target = event.target;
                if (!target || !target.closest) {
                    return;
                }
                var row = target.closest(".purchase-line-item");
                if (target.closest(".am-line-target-browse") || target.closest(".am-line-target-display")) {
                    openModal(event, row);
                } else if (target.closest(".am-line-target-clear")) {
                    event.preventDefault();
                    clearSelection(row);
                }
            });
        }
    }

    function boot() {
        requisitionTarget = initRequisitionTarget();
        initLineItems();
        initApprovalPathPreview();
        initTargetAssetPicker();
        updateQtyInStockVisibility();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})();
