(function () {
    var config = window.AmGroupedAssetsConfig || {};
    var storageKey = "am-assets-grouped-expanded";
    var memberPageSize = config.memberPageSize || 10;

    function readExpandedKeys() {
        try {
            var raw = sessionStorage.getItem(storageKey);
            if (!raw) {
                return [];
            }
            var parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : [];
        } catch (e) {
            return [];
        }
    }

    function writeExpandedKeys(keys) {
        try {
            sessionStorage.setItem(storageKey, JSON.stringify(keys));
        } catch (e) {
            // sessionStorage may be unavailable
        }
    }

    function rememberExpanded(panel, expanded) {
        if (!panel) {
            return;
        }

        var key = panel.getAttribute("data-am-group-key");
        if (!key) {
            return;
        }

        var keys = readExpandedKeys();
        var index = keys.indexOf(key);
        if (expanded && index === -1) {
            keys.push(key);
        } else if (!expanded && index !== -1) {
            keys.splice(index, 1);
        }
        writeExpandedKeys(keys);
    }

    function getToggleForPanel(panel) {
        if (!panel || !panel.id) {
            return null;
        }
        return document.querySelector('[data-am-group-toggle][data-bs-target="#' + panel.id + '"]');
    }

    function showDetailRow(toggleBtn) {
        var detailRow = toggleBtn ? toggleBtn.closest("tr") : null;
        detailRow = detailRow ? detailRow.nextElementSibling : null;
        if (detailRow && detailRow.hasAttribute("data-am-group-detail-row")) {
            detailRow.hidden = false;
        }
    }

    function hideDetailRow(toggleBtn) {
        var detailRow = toggleBtn ? toggleBtn.closest("tr") : null;
        detailRow = detailRow ? detailRow.nextElementSibling : null;
        if (detailRow && detailRow.hasAttribute("data-am-group-detail-row")) {
            detailRow.hidden = true;
        }
    }

    function getMemberColspan(panel) {
        return config.canBulkEdit ? 6 : 5;
    }

    function buildMembersQuery(panel, skip, take) {
        var params = [];
        var listFilter = config.listFilter || {};

        params.push("assetName=" + encodeURIComponent(panel.getAttribute("data-am-group-asset-name") || ""));
        params.push("assetSubTypeId=" + encodeURIComponent(panel.getAttribute("data-am-group-sub-type-id") || ""));
        params.push("groupDepartmentId=" + encodeURIComponent(panel.getAttribute("data-am-group-department-id") || ""));
        params.push("groupStatus=" + encodeURIComponent(panel.getAttribute("data-am-group-status") || ""));
        params.push("skip=" + encodeURIComponent(String(skip || 0)));
        params.push("take=" + encodeURIComponent(String(take || memberPageSize)));

        if (listFilter.search) {
            params.push("Search=" + encodeURIComponent(listFilter.search));
        }
        if (listFilter.departmentId != null) {
            params.push("DepartmentId=" + encodeURIComponent(String(listFilter.departmentId)));
        }
        if (listFilter.status != null) {
            params.push("Status=" + encodeURIComponent(String(listFilter.status)));
        }

        return (config.groupMembersUrl || "") + "?" + params.join("&");
    }

    function renderMemberRow(item) {
        var row = document.createElement("tr");
        row.className = "am-group-member-row";

        if (config.canBulkEdit) {
            var bulkCell = document.createElement("td");
            bulkCell.className = "align-middle am-bulk-col";
            bulkCell.innerHTML = '<input type="checkbox" data-am-bulk-id value="' + item.id + '" aria-label="Select ' + item.assetTag + '" class="form-check-input" />';
            row.appendChild(bulkCell);
        }

        var tagCell = document.createElement("td");
        tagCell.className = "align-middle";
        tagCell.innerHTML = '<span class="am-tag-pill">' + item.assetTag + "</span>";
        row.appendChild(tagCell);

        var nameCell = document.createElement("td");
        nameCell.className = "align-middle";
        var nameHtml = '<div class="fw-medium text-dark">' + item.assetName + "</div>";
        if (item.brandModel) {
            nameHtml += '<div class="text-muted small">' + item.brandModel + "</div>";
        }
        nameCell.innerHTML = nameHtml;
        row.appendChild(nameCell);

        var custodianCell = document.createElement("td");
        custodianCell.className = "align-middle text-muted small";
        var custodianLabel = item.custodianName || config.unassignedLabel || "Unassigned";
        var custodianClass = !item.custodianName ? "am-empty-custodian" : "";
        custodianCell.innerHTML = custodianClass
            ? '<span class="' + custodianClass + '">' + custodianLabel + "</span>"
            : custodianLabel;
        row.appendChild(custodianCell);

        var costCell = document.createElement("td");
        costCell.className = "align-middle text-end fw-medium text-primary";
        costCell.textContent = item.acquisitionCostDisplay || "";
        row.appendChild(costCell);

        var actionsCell = document.createElement("td");
        actionsCell.className = "text-end text-nowrap align-middle";
        var actionsHtml = '<a class="btn btn-sm btn-outline-primary py-0 px-2" href="' + item.detailsUrl + '">Details</a>';
        if (item.canMove) {
            actionsHtml += '<button type="button" class="btn btn-sm btn-outline-secondary py-0 px-2 ms-1" data-am-relocate-open data-asset-id="' + item.id + '" data-asset-tag="' + item.assetTag + '">Move</button>';
        }
        actionsCell.innerHTML = actionsHtml;
        row.appendChild(actionsCell);

        return row;
    }

    function updateViewMoreButton(panel, payload) {
        var button = panel.querySelector("[data-am-view-more]");
        if (!button) {
            return;
        }

        if (payload.hasMore) {
            button.textContent = "View more (" + payload.remainingCount + " more)";
            button.classList.remove("d-none");
        } else {
            button.classList.add("d-none");
            button.textContent = "";
        }
    }

    function wireRelocateButtons(scope) {
        var relocateModal = document.getElementById("amRelocateClassModal");
        if (!relocateModal || !window.bootstrap) {
            return;
        }

        var modalInstance = bootstrap.Modal.getOrCreateInstance(relocateModal);
        var assetIdInput = document.getElementById("amRelocateAssetId");
        var assetTagLabel = document.getElementById("amRelocateAssetTag");
        var targetSelect = document.getElementById("targetDepartmentId");

        (scope || document).querySelectorAll("[data-am-relocate-open]").forEach(function (btn) {
            if (btn.getAttribute("data-am-relocate-wired") === "true") {
                return;
            }
            btn.setAttribute("data-am-relocate-wired", "true");
            btn.addEventListener("click", function () {
                if (assetIdInput) {
                    assetIdInput.value = btn.getAttribute("data-asset-id") || "";
                }
                if (assetTagLabel) {
                    assetTagLabel.textContent = btn.getAttribute("data-asset-tag") || "";
                }
                if (targetSelect) {
                    targetSelect.selectedIndex = 0;
                }
                modalInstance.show();
            });
        });
    }

    function wireGroupMaster(panel) {
        var master = panel.querySelector("[data-am-bulk-group-master]");
        if (!master || master.getAttribute("data-am-bulk-group-wired") === "true") {
            return;
        }

        master.setAttribute("data-am-bulk-group-wired", "true");
        master.addEventListener("change", function () {
            panel.querySelectorAll("[data-am-bulk-id]").forEach(function (checkbox) {
                checkbox.checked = master.checked;
            });
            if (window.AmListBulk && typeof window.AmListBulk.refresh === "function") {
                window.AmListBulk.refresh();
            }
        });
    }

    function loadGroupMembers(panel, append) {
        if (!panel || !config.groupMembersUrl) {
            return Promise.resolve();
        }

        var count = parseInt(panel.getAttribute("data-am-group-count") || "0", 10);
        if (count === 0) {
            return Promise.resolve();
        }

        if (panel.getAttribute("data-am-group-loading") === "true") {
            return Promise.resolve();
        }

        var tbody = panel.querySelector("[data-am-group-members]");
        if (!tbody) {
            return Promise.resolve();
        }

        var skip = append ? parseInt(panel.getAttribute("data-am-group-loaded-count") || "0", 10) : 0;
        panel.setAttribute("data-am-group-loading", "true");

        if (!append) {
            tbody.innerHTML = '<tr class="am-group-member-loading"><td colspan="' + getMemberColspan(panel) + '" class="text-muted small py-2">Loading units…</td></tr>';
        }

        return fetch(buildMembersQuery(panel, skip, memberPageSize), { credentials: "same-origin" })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Failed to load group members.");
                }
                return response.json();
            })
            .then(function (payload) {
                if (!append) {
                    tbody.innerHTML = "";
                } else {
                    tbody.querySelectorAll(".am-group-member-loading").forEach(function (row) {
                        row.remove();
                    });
                }

                if (!payload.items || payload.items.length === 0) {
                    if (!append) {
                        tbody.innerHTML = '<tr class="am-group-member-empty"><td colspan="' + getMemberColspan(panel) + '" class="text-muted small py-2">No units in this group.</td></tr>';
                    }
                } else {
                    payload.items.forEach(function (item) {
                        tbody.appendChild(renderMemberRow(item));
                    });
                }

                var loadedCount = skip + (payload.items ? payload.items.length : 0);
                panel.setAttribute("data-am-group-loaded-count", String(loadedCount));
                panel.setAttribute("data-am-group-loaded", loadedCount > 0 ? "true" : "false");
                updateViewMoreButton(panel, payload);
                wireRelocateButtons(panel);
                wireGroupMaster(panel);

                if (window.AmListBulk && typeof window.AmListBulk.refresh === "function") {
                    window.AmListBulk.refresh();
                }
            })
            .catch(function () {
                if (!append) {
                    tbody.innerHTML = '<tr class="am-group-member-empty"><td colspan="' + getMemberColspan(panel) + '" class="text-muted small py-2">Unable to load units for this group.</td></tr>';
                }
            })
            .then(function () {
                panel.removeAttribute("data-am-group-loading");
            });
    }

    function ensureGroupMembersLoaded(panel) {
        if (!panel || panel.getAttribute("data-am-group-loaded") === "true") {
            return Promise.resolve();
        }
        return loadGroupMembers(panel, false);
    }

    function expandPanel(panel, toggleBtn) {
        if (!panel || !window.bootstrap) {
            return Promise.resolve();
        }

        showDetailRow(toggleBtn);
        return ensureGroupMembersLoaded(panel).then(function () {
            var instance = bootstrap.Collapse.getOrCreateInstance(panel, { toggle: false });
            instance.show();
        });
    }

    function collapsePanel(panel, toggleBtn) {
        if (!panel || !window.bootstrap) {
            return;
        }

        var instance = bootstrap.Collapse.getOrCreateInstance(panel, { toggle: false });
        instance.hide();
        hideDetailRow(toggleBtn);
    }

    function wireGroupToggle(btn) {
        var targetSelector = btn.getAttribute("data-bs-target");
        if (!targetSelector) {
            return;
        }

        var target = document.querySelector(targetSelector);
        if (!target) {
            return;
        }

        target.addEventListener("show.bs.collapse", function () {
            btn.textContent = "Collapse";
            btn.setAttribute("aria-expanded", "true");
            showDetailRow(btn);
            ensureGroupMembersLoaded(target).then(function () {
                rememberExpanded(target, true);
            });
        });

        target.addEventListener("hide.bs.collapse", function () {
            btn.textContent = "Expand";
            btn.setAttribute("aria-expanded", "false");
            hideDetailRow(btn);
            rememberExpanded(target, false);
        });
    }

    function restoreExpandedGroups() {
        var keys = readExpandedKeys();
        if (!keys.length) {
            return;
        }

        document.querySelectorAll("[data-am-group-panel]").forEach(function (panel) {
            var key = panel.getAttribute("data-am-group-key");
            if (keys.indexOf(key) === -1) {
                return;
            }

            expandPanel(panel, getToggleForPanel(panel));
        });
    }

    document.querySelectorAll("[data-am-group-toggle]").forEach(wireGroupToggle);

    document.addEventListener("click", function (event) {
        var target = event.target;
        if (!target || !target.closest) {
            return;
        }

        var viewMoreBtn = target.closest("[data-am-view-more]");
        if (viewMoreBtn) {
            var panel = viewMoreBtn.closest("[data-am-group-panel]");
            if (panel) {
                loadGroupMembers(panel, true);
            }
        }
    });

    var expandAllBtn = document.querySelector("[data-am-group-expand-all]");
    if (expandAllBtn) {
        expandAllBtn.addEventListener("click", function () {
            var panels = document.querySelectorAll("[data-am-group-panel]");
            var chain = Promise.resolve();
            panels.forEach(function (panel) {
                chain = chain.then(function () {
                    return expandPanel(panel, getToggleForPanel(panel));
                });
            });
        });
    }

    var collapseAllBtn = document.querySelector("[data-am-group-collapse-all]");
    if (collapseAllBtn) {
        collapseAllBtn.addEventListener("click", function () {
            document.querySelectorAll("[data-am-group-panel]").forEach(function (panel) {
                collapsePanel(panel, getToggleForPanel(panel));
            });
            writeExpandedKeys([]);
        });
    }

    wireRelocateButtons(document);

    var relocateGroupModal = document.getElementById("amRelocateGroupModal");
    if (relocateGroupModal && window.bootstrap) {
        var groupModalInstance = bootstrap.Modal.getOrCreateInstance(relocateGroupModal);
        var groupAssetNameInput = document.getElementById("amRelocateGroupAssetName");
        var groupAssetSubTypeIdInput = document.getElementById("amRelocateGroupAssetSubTypeId");
        var groupDepartmentIdInput = document.getElementById("amRelocateGroupDepartmentId");
        var groupStatusInput = document.getElementById("amRelocateGroupStatus");
        var groupLabel = document.getElementById("amRelocateGroupLabel");
        var groupTargetSelect = document.getElementById("amRelocateGroupTargetDepartmentId");

        document.querySelectorAll("[data-am-relocate-group-open]").forEach(function (btn) {
            btn.addEventListener("click", function () {
                if (groupAssetNameInput) {
                    groupAssetNameInput.value = btn.getAttribute("data-asset-name") || "";
                }
                if (groupAssetSubTypeIdInput) {
                    groupAssetSubTypeIdInput.value = btn.getAttribute("data-asset-sub-type-id") || "";
                }
                if (groupDepartmentIdInput) {
                    groupDepartmentIdInput.value = btn.getAttribute("data-group-department-id") || "";
                }
                if (groupStatusInput) {
                    groupStatusInput.value = btn.getAttribute("data-group-status") || "";
                }
                if (groupLabel) {
                    groupLabel.textContent = btn.getAttribute("data-group-label") || "";
                }
                if (groupTargetSelect) {
                    groupTargetSelect.selectedIndex = 0;
                }
                groupModalInstance.show();
            });
        });
    }

    document.querySelectorAll("[data-am-group-panel]").forEach(wireGroupMaster);
    restoreExpandedGroups();
})();
