// Assets Index "Group by room" (view=room): lazy level 2 (categories in a room) and level 3
// (assets in a room + category). Nothing loads until a row is expanded; level 3 pages on the server.
// Expanded rooms/categories (and the asset page within each) are remembered per URL in
// sessionStorage, so Back from an asset's Details page or a refresh restores the open rows.
(function () {
    var root = document.querySelector("[data-am-room-groups]");
    if (!root) {
        return;
    }

    var categoriesUrl = root.getAttribute("data-am-room-categories-url") || "";
    var assetsUrl = root.getAttribute("data-am-room-assets-url") || "";
    var filterQuery = root.getAttribute("data-am-room-filter-query") || "";
    var canBulk = root.getAttribute("data-am-room-can-bulk") === "true";
    var assetPageSize = parseInt(root.getAttribute("data-am-room-asset-page-size") || "25", 10) || 25;
    var storageKey = "am-assets-room-expanded:" + window.location.pathname + window.location.search;

    // Details links carry this list URL (view, filters, page) as returnUrl so "Back to Assets" returns here;
    // the server only accepts a local, tenant-aware path and falls back to Assets/Index.
    function withListReturnUrl(url) {
        if (!url) {
            return url;
        }
        var back = window.location.pathname + window.location.search;
        return url + (url.indexOf("?") >= 0 ? "&" : "?") + "returnUrl=" + encodeURIComponent(back);
    }

    function escapeHtml(value) {
        if (value === null || value === undefined) {
            return "";
        }
        return String(value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function readState() {
        try {
            var parsed = JSON.parse(sessionStorage.getItem(storageKey) || "{}");
            return {
                rooms: Array.isArray(parsed.rooms) ? parsed.rooms : [],
                cats: parsed.cats && typeof parsed.cats === "object" ? parsed.cats : {}
            };
        } catch (e) {
            return { rooms: [], cats: {} };
        }
    }

    function writeState(state) {
        try {
            sessionStorage.setItem(storageKey, JSON.stringify(state));
        } catch (e) {
            // sessionStorage may be unavailable
        }
    }

    function rememberRoom(roomId, expanded) {
        var state = readState();
        var key = String(roomId);
        var index = state.rooms.indexOf(key);
        if (expanded && index === -1) {
            state.rooms.push(key);
        } else if (!expanded && index !== -1) {
            state.rooms.splice(index, 1);
            Object.keys(state.cats).forEach(function (catKey) {
                if (catKey.indexOf(key + ":") === 0) {
                    delete state.cats[catKey];
                }
            });
        }
        writeState(state);
    }

    function rememberCategory(roomId, categoryId, skip) {
        var state = readState();
        var key = roomId + ":" + categoryId;
        if (skip === null) {
            delete state.cats[key];
        } else {
            state.cats[key] = skip;
        }
        writeState(state);
    }

    function buildUrl(base, params) {
        var parts = [];
        if (filterQuery) {
            parts.push(filterQuery);
        }
        Object.keys(params).forEach(function (name) {
            parts.push(encodeURIComponent(name) + "=" + encodeURIComponent(String(params[name])));
        });
        parts.push("_=" + Date.now());
        return base + (base.indexOf("?") === -1 ? "?" : "&") + parts.join("&");
    }

    function fetchJson(url) {
        return fetch(url, {
            credentials: "same-origin",
            headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" }
        }).then(function (response) {
            var type = response.headers.get("content-type") || "";
            if (!response.ok || type.indexOf("application/json") === -1) {
                throw new Error("Request failed (" + response.status + ")");
            }
            return response.json();
        });
    }

    function setToggle(button, expanded) {
        if (!button) {
            return;
        }
        button.setAttribute("aria-expanded", expanded ? "true" : "false");
        button.textContent = expanded ? "Collapse" : "Expand";
    }

    function showMessage(container, selector, text) {
        var el = container.querySelector(selector);
        if (!el) {
            return;
        }
        if (text) {
            el.textContent = text;
            el.classList.remove("d-none");
        } else {
            el.textContent = "";
            el.classList.add("d-none");
        }
    }

    // ---- Level 2: categories in a room ----

    function renderCategories(panel, roomId, payload) {
        var holder = panel.querySelector("[data-am-room-categories]");
        var items = payload.items || [];
        if (!items.length) {
            holder.innerHTML = '<div class="text-muted small py-1">No assets in this room match the current filters.</div>';
            return;
        }

        var html = '<table class="table table-sm align-middle mb-0 am-room-category-table" data-am-room-category-table>'
            + '<thead class="text-muted small"><tr>'
            + '<th class="fw-normal">Category</th>'
            + '<th class="fw-normal text-end">Assets</th>'
            + '<th class="fw-normal text-end">Value (own prices)</th>'
            + '<th class="fw-normal text-end"></th>'
            + "</tr></thead><tbody>";
        items.forEach(function (cat) {
            var panelId = "am-room-cat-" + roomId + "-" + cat.categoryId;
            var valueHtml = cat.pricedCount > 0
                ? '<span class="fw-medium text-primary">' + escapeHtml(cat.pricedTotalDisplay) + "</span>"
                : '<span class="text-muted fst-italic">' + escapeHtml(cat.pricedTotalDisplay) + "</span>";
            if (cat.pricedCount > 0 && cat.unpricedCount > 0) {
                valueHtml += '<div class="text-muted small">' + escapeHtml(cat.unpricedCount) + " unpriced (excluded)</div>";
            }
            html += '<tr class="am-room-category-row" data-am-room-category-row data-am-room-id="' + escapeHtml(roomId) + '" data-am-category-id="' + escapeHtml(cat.categoryId) + '" data-am-category-count="' + escapeHtml(cat.count) + '">'
                + '<td class="fw-medium" data-am-category-name>' + escapeHtml(cat.name) + "</td>"
                + '<td class="text-end" data-am-category-count-cell>' + escapeHtml(cat.count) + "</td>"
                + '<td class="text-end">' + valueHtml + "</td>"
                + '<td class="text-end text-nowrap"><button type="button" class="btn btn-sm btn-outline-secondary py-0 px-2" data-am-category-toggle aria-expanded="false" aria-controls="' + panelId + '">Expand</button></td>'
                + "</tr>"
                + '<tr class="am-room-category-detail-row" id="' + panelId + '" hidden="hidden" data-am-category-panel>'
                + '<td colspan="4" class="bg-white px-2 py-2">'
                + '<div class="text-muted small py-1 d-none" data-am-category-loading>Loading assets...</div>'
                + '<div class="text-danger small py-1 d-none" data-am-category-error></div>'
                + "<div data-am-category-assets></div>"
                + "</td></tr>";
        });
        html += "</tbody></table>";
        holder.innerHTML = html;
    }

    function loadRoom(panel, roomId) {
        if (panel.getAttribute("data-am-loaded") === "true") {
            return Promise.resolve();
        }
        showMessage(panel, "[data-am-room-error]", "");
        showMessage(panel, "[data-am-room-loading]", "Loading categories...");
        return fetchJson(buildUrl(categoriesUrl, { roomId: roomId }))
            .then(function (payload) {
                renderCategories(panel, roomId, payload);
                panel.setAttribute("data-am-loaded", "true");
                panel.setAttribute("data-am-room-total", String(payload.totalCount || 0));
            })
            .catch(function () {
                showMessage(panel, "[data-am-room-error]", "Could not load categories. Collapse and expand to retry.");
            })
            .then(function () {
                showMessage(panel, "[data-am-room-loading]", "");
            });
    }

    function toggleRoom(button, forceExpand) {
        var panel = document.getElementById(button.getAttribute("aria-controls"));
        if (!panel) {
            return Promise.resolve();
        }
        var roomId = panel.getAttribute("data-am-room-id");
        var expand = forceExpand === true ? true : button.getAttribute("aria-expanded") !== "true";
        setToggle(button, expand);
        panel.hidden = !expand;
        rememberRoom(roomId, expand);
        return expand ? loadRoom(panel, roomId) : Promise.resolve();
    }

    // ---- Level 3: assets in a room + category ----

    function renderAssets(panel, roomId, categoryId, payload) {
        var holder = panel.querySelector("[data-am-category-assets]");
        var items = payload.items || [];
        var total = payload.totalCount || 0;
        var skip = payload.skip || 0;
        var take = payload.take || assetPageSize;
        if (!items.length) {
            holder.innerHTML = '<div class="text-muted small py-1">No assets.</div>';
            return;
        }

        var html = '<table class="table table-sm table-hover align-middle mb-1 am-room-asset-table" data-am-room-asset-table>'
            + '<thead class="text-muted small"><tr>';
        if (canBulk) {
            html += '<th class="fw-normal am-bulk-col"><input type="checkbox" class="form-check-input" data-am-room-bulk-master aria-label="Select all assets on this page" /></th>';
        }
        html += '<th class="fw-normal">Tag</th>'
            + '<th class="fw-normal">Name</th>'
            + '<th class="fw-normal">Sub-type</th>'
            + '<th class="fw-normal">Custodian</th>'
            + '<th class="fw-normal">Status</th>'
            + '<th class="fw-normal text-end">Price</th>'
            + '<th class="fw-normal text-end">Actions</th>'
            + "</tr></thead><tbody>";
        items.forEach(function (item) {
            html += '<tr class="am-room-asset-row" data-am-room-asset-row data-asset-id="' + escapeHtml(item.id) + '">';
            if (canBulk) {
                html += '<td class="am-bulk-col"><input type="checkbox" data-am-bulk-id value="' + escapeHtml(item.id) + '" aria-label="Select ' + escapeHtml(item.assetTag) + '" class="form-check-input" /></td>';
            }
            html += '<td><span class="am-tag-pill" data-am-asset-tag>' + escapeHtml(item.assetTag) + "</span></td>"
                + '<td class="fw-medium text-dark">' + escapeHtml(item.assetName) + "</td>"
                + '<td class="text-muted small">' + escapeHtml(item.assetSubTypeName) + "</td>"
                + '<td class="text-muted small"><span class="' + (item.custodianIsPerson ? "" : "am-empty-custodian") + '">' + escapeHtml(item.custodianName) + "</span></td>"
                + '<td><span class="badge bg-' + escapeHtml(item.statusBadgeClass) + ' badge-status">' + escapeHtml(item.statusLabel) + "</span></td>"
                + '<td class="text-end ' + (item.price === null || item.price === undefined ? "text-muted fst-italic" : "fw-medium text-primary") + '" data-am-asset-price>' + escapeHtml(item.priceDisplay) + "</td>"
                + '<td class="text-end text-nowrap">'
                + '<a class="btn btn-sm btn-outline-primary py-0 px-2" href="' + escapeHtml(withListReturnUrl(item.detailsUrl)) + '">Details</a>';
            if (item.editUrl) {
                html += '<a class="btn btn-sm btn-outline-secondary py-0 px-2 ms-1" href="' + escapeHtml(item.editUrl) + '">Edit</a>';
            }
            html += "</td></tr>";
        });
        html += "</tbody></table>";

        var from = skip + 1;
        var to = skip + items.length;
        html += '<div class="d-flex flex-wrap justify-content-between align-items-center gap-2 small" data-am-room-asset-pager>'
            + '<span class="text-muted" data-am-room-asset-range>Showing ' + from + "&ndash;" + to + " of " + total + "</span>";
        if (total > take) {
            html += '<div class="btn-group btn-group-sm" role="group" aria-label="Asset pages">'
                + '<button type="button" class="btn btn-outline-secondary" data-am-room-asset-page="' + Math.max(0, skip - take) + '"' + (skip <= 0 ? " disabled" : "") + ">Previous</button>"
                + '<button type="button" class="btn btn-outline-secondary" data-am-room-asset-page="' + (skip + take) + '"' + (to >= total ? " disabled" : "") + ">Next</button>"
                + "</div>";
        }
        html += "</div>";
        holder.innerHTML = html;
        if (window.AmListBulk && typeof window.AmListBulk.refresh === "function") {
            window.AmListBulk.refresh();
        }
    }

    function loadAssets(panel, roomId, categoryId, skip) {
        showMessage(panel, "[data-am-category-error]", "");
        showMessage(panel, "[data-am-category-loading]", "Loading assets...");
        return fetchJson(buildUrl(assetsUrl, { roomId: roomId, roomCategoryId: categoryId, skip: skip || 0, take: assetPageSize }))
            .then(function (payload) {
                renderAssets(panel, roomId, categoryId, payload);
                panel.setAttribute("data-am-loaded-skip", String(payload.skip || 0));
                rememberCategory(roomId, categoryId, payload.skip || 0);
            })
            .catch(function () {
                showMessage(panel, "[data-am-category-error]", "Could not load assets. Collapse and expand to retry.");
            })
            .then(function () {
                showMessage(panel, "[data-am-category-loading]", "");
            });
    }

    function toggleCategory(button, forceSkip) {
        var row = button.closest("[data-am-room-category-row]");
        var panel = document.getElementById(button.getAttribute("aria-controls"));
        if (!row || !panel) {
            return Promise.resolve();
        }
        var roomId = row.getAttribute("data-am-room-id");
        var categoryId = row.getAttribute("data-am-category-id");
        var expand = forceSkip !== undefined ? true : button.getAttribute("aria-expanded") !== "true";
        setToggle(button, expand);
        panel.hidden = !expand;
        if (!expand) {
            rememberCategory(roomId, categoryId, null);
            return Promise.resolve();
        }
        var skip = forceSkip !== undefined ? forceSkip : 0;
        if (forceSkip === undefined && panel.hasAttribute("data-am-loaded-skip")) {
            rememberCategory(roomId, categoryId, parseInt(panel.getAttribute("data-am-loaded-skip"), 10) || 0);
            return Promise.resolve();
        }
        return loadAssets(panel, roomId, categoryId, skip);
    }

    root.addEventListener("click", function (event) {
        var target = event.target;
        if (!target || !target.closest) {
            return;
        }

        var roomToggle = target.closest("[data-am-room-toggle]");
        if (roomToggle) {
            toggleRoom(roomToggle);
            return;
        }

        var categoryToggle = target.closest("[data-am-category-toggle]");
        if (categoryToggle) {
            toggleCategory(categoryToggle);
            return;
        }

        var pageButton = target.closest("[data-am-room-asset-page]");
        if (pageButton && !pageButton.disabled) {
            var panel = pageButton.closest("[data-am-category-panel]");
            var row = panel ? panel.previousElementSibling : null;
            if (panel && row) {
                loadAssets(panel, row.getAttribute("data-am-room-id"), row.getAttribute("data-am-category-id"),
                    parseInt(pageButton.getAttribute("data-am-room-asset-page"), 10) || 0);
            }
            return;
        }

        if (target.closest("[data-am-room-collapse-all]")) {
            root.querySelectorAll("[data-am-room-toggle]").forEach(function (button) {
                if (button.getAttribute("aria-expanded") === "true") {
                    toggleRoom(button);
                }
            });
            writeState({ rooms: [], cats: {} });
        }
    });

    root.addEventListener("change", function (event) {
        var master = event.target;
        if (!master || !master.matches || !master.matches("[data-am-room-bulk-master]")) {
            return;
        }
        var table = master.closest("[data-am-room-asset-table]");
        if (table) {
            table.querySelectorAll("[data-am-bulk-id]").forEach(function (checkbox) {
                checkbox.checked = master.checked;
            });
        }
        if (window.AmListBulk && typeof window.AmListBulk.refresh === "function") {
            window.AmListBulk.refresh();
        }
    });

    // Restore rows that were open for this exact URL (Back / refresh).
    (function restore() {
        var state = readState();
        state.rooms.forEach(function (roomId) {
            var button = root.querySelector('[data-am-room-toggle][aria-controls="am-room-panel-' + roomId + '"]');
            if (!button) {
                return;
            }
            toggleRoom(button, true).then(function () {
                Object.keys(state.cats).forEach(function (catKey) {
                    var parts = catKey.split(":");
                    if (parts[0] !== String(roomId)) {
                        return;
                    }
                    var catButton = root.querySelector('[data-am-category-toggle][aria-controls="am-room-cat-' + roomId + "-" + parts[1] + '"]');
                    if (catButton) {
                        toggleCategory(catButton, parseInt(state.cats[catKey], 10) || 0);
                    }
                });
            });
        });
    })();
})();
