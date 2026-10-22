// rel1022a: Asset register (grouped and by-room views) whole-group selection, bulk Move and "Send to printing".
// - A whole-group checkbox selects every unit of the group, including units not loaded on the page yet.
//   They are resolved on demand by paging the same JSON endpoints the page uses (Assets/GroupMembers,
//   Assets/RoomCategories + RoomCategoryAssets), so the role / department scope of the list applies.
// - Move: the existing Bulk action form (Assets/Bulk, Assets.Edit, AssetBulkService move rules) gets the ids.
// - Send to printing: posts the ids to AssetLabels/QueueFromRegister (one print batch), which opens Bulk Label
//   Print with that batch queued. Nothing is printed until the user prints there.
(function (window) {
    "use strict";

    var MAX_SELECTION = 500; // matches LabelPrintLogService.MaxBatchAssets; also keeps the form under ASP.NET's key limit
    var PAGE = 100;          // GroupMembers and RoomCategoryAssets clamp take to 100

    var toolbar = document.querySelector("[data-am-bulk-toolbar]");
    var roomRoot = document.querySelector("[data-am-room-groups]");
    var countNode = document.querySelector("[data-am-selection-count]");
    var printButton = document.querySelector("[data-am-send-to-print]");
    var printForm = document.getElementById("amSendToPrintForm");
    var busy = false;

    function groupBoxes() {
        return document.querySelectorAll("[data-am-group-select], [data-am-room-select]");
    }

    function panelFor(box) {
        var id = box.getAttribute("data-am-group-select") || box.getAttribute("data-am-room-panel-id");
        return id ? document.getElementById(id) : null;
    }

    function groupCount(box) {
        return parseInt(box.getAttribute("data-am-group-count") || "0", 10) || 0;
    }

    function fullySelectedGroups() {
        var result = [];
        groupBoxes().forEach(function (box) {
            if (box.checked && !box.indeterminate) {
                result.push(box);
            }
        });
        return result;
    }

    function memberBoxes(scope) {
        return (scope || document).querySelectorAll("[data-am-bulk-id]");
    }

    // Loaded member checkboxes that are not inside a fully-selected group (those are counted via the group).
    function looseCheckedIds() {
        var covered = fullySelectedGroups().map(panelFor).filter(Boolean);
        var ids = [];
        memberBoxes().forEach(function (box) {
            if (!box.checked) {
                return;
            }
            for (var i = 0; i < covered.length; i++) {
                if (covered[i].contains(box)) {
                    return;
                }
            }
            ids.push(String(box.value));
        });
        return ids;
    }

    function estimateCount() {
        var total = looseCheckedIds().length;
        fullySelectedGroups().forEach(function (box) {
            total += groupCount(box);
        });
        return total;
    }

    function hasSelection() {
        return estimateCount() > 0;
    }

    function syncUi() {
        var count = estimateCount();
        if (countNode) {
            countNode.textContent = count === 0 ? "" : count + (count === 1 ? " unit selected" : " units selected");
        }
        if (printButton) {
            printButton.disabled = busy || count === 0;
        }
        if (window.AmListBulk && typeof window.AmListBulk.refresh === "function") {
            window.AmListBulk.refresh();
        }
    }

    function setMembers(panel, checked) {
        if (!panel) {
            return;
        }
        memberBoxes(panel).forEach(function (box) {
            box.checked = checked;
        });
        panel.querySelectorAll("[data-am-bulk-group-master], [data-am-room-bulk-master]").forEach(function (master) {
            master.checked = checked;
        });
    }

    // Units loaded later into a fully-selected group come in checked.
    function watchPanel(box) {
        var panel = panelFor(box);
        if (!panel || panel.getAttribute("data-am-selection-watched") === "true" || !window.MutationObserver) {
            return;
        }
        panel.setAttribute("data-am-selection-watched", "true");
        new MutationObserver(function () {
            if (box.checked && !box.indeterminate) {
                setMembers(panel, true);
            }
        }).observe(panel, { childList: true, subtree: true });
    }

    document.addEventListener("change", function (event) {
        var target = event.target;
        if (!target || !target.matches) {
            return;
        }
        if (target.matches("[data-am-group-select], [data-am-room-select]")) {
            target.indeterminate = false;
            setMembers(panelFor(target), target.checked);
            syncUi();
            return;
        }
        if (target.matches("[data-am-bulk-id]") && !target.checked) {
            // Unticking one unit turns a whole-group selection into "only the ticked units".
            groupBoxes().forEach(function (box) {
                var panel = panelFor(box);
                if (box.checked && !box.indeterminate && panel && panel.contains(target)) {
                    box.indeterminate = true;
                }
            });
        }
        if (target.matches("[data-am-bulk-id], [data-am-bulk-group-master], [data-am-room-bulk-master]")) {
            // Let the page's own handlers run first (they tick the members of a master).
            window.setTimeout(syncUi, 0);
        }
    });

    function fetchJson(url) {
        return fetch(url, { credentials: "same-origin", headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" } })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Could not load the units of a group (HTTP " + response.status + ").");
                }
                return response.json();
            });
    }

    function pageAll(buildUrl, expected, sink) {
        function next(skip) {
            return fetchJson(buildUrl(skip)).then(function (payload) {
                var items = payload.items || [];
                items.forEach(function (item) { sink(String(item.id)); });
                var total = typeof payload.totalCount === "number" ? payload.totalCount : expected;
                var loaded = skip + items.length;
                if (items.length > 0 && loaded < total && sink.size() <= MAX_SELECTION) {
                    return next(loaded);
                }
                return null;
            });
        }
        return next(0);
    }

    function roomUrl(base, params) {
        var filterQuery = roomRoot ? (roomRoot.getAttribute("data-am-room-filter-query") || "") : "";
        var parts = filterQuery ? [filterQuery] : [];
        Object.keys(params).forEach(function (name) {
            parts.push(encodeURIComponent(name) + "=" + encodeURIComponent(String(params[name])));
        });
        parts.push("_=" + Date.now());
        return base + (base.indexOf("?") === -1 ? "?" : "&") + parts.join("&");
    }

    function resolveGroup(box, sink) {
        var panel = panelFor(box);
        if (box.hasAttribute("data-am-group-select")) {
            if (!panel || !window.AmAssetsGrouped || !window.AmAssetsGrouped.buildMembersQuery) {
                return Promise.reject(new Error("The grouped list is not ready yet. Reload the page and try again."));
            }
            return pageAll(function (skip) {
                return window.AmAssetsGrouped.buildMembersQuery(panel, skip, PAGE);
            }, groupCount(box), sink);
        }

        var roomId = box.getAttribute("data-am-room-select");
        var categoriesUrl = roomRoot ? roomRoot.getAttribute("data-am-room-categories-url") : "";
        var assetsUrl = roomRoot ? roomRoot.getAttribute("data-am-room-assets-url") : "";
        return fetchJson(roomUrl(categoriesUrl, { roomId: roomId })).then(function (payload) {
            var chain = Promise.resolve();
            (payload.items || []).forEach(function (cat) {
                chain = chain.then(function () {
                    if (sink.size() > MAX_SELECTION) {
                        return null;
                    }
                    return pageAll(function (skip) {
                        return roomUrl(assetsUrl, { roomId: roomId, roomCategoryId: cat.categoryId, skip: skip, take: PAGE });
                    }, cat.count || 0, sink);
                });
            });
            return chain;
        });
    }

    // Resolves to the unique selected asset ids (whole groups fully expanded), or rejects with a message.
    function resolveSelectedIds() {
        var seen = {};
        var ids = [];
        var sink = function (id) {
            if (!seen[id]) {
                seen[id] = true;
                ids.push(id);
            }
        };
        sink.size = function () { return ids.length; };
        looseCheckedIds().forEach(sink);

        var chain = Promise.resolve();
        fullySelectedGroups().forEach(function (box) {
            chain = chain.then(function () {
                return ids.length > MAX_SELECTION ? null : resolveGroup(box, sink);
            });
        });
        return chain.then(function () {
            if (!ids.length) {
                throw new Error("Select at least one unit or a whole group first.");
            }
            if (ids.length > MAX_SELECTION) {
                throw new Error("At most " + MAX_SELECTION + " units can be handled at once. Narrow the filters or select fewer groups.");
            }
            return ids;
        });
    }

    function selectedGroupLabels() {
        var labels = [];
        fullySelectedGroups().forEach(function (box) {
            var label = box.getAttribute("data-am-group-label");
            if (label) {
                labels.push(label);
            }
        });
        return labels;
    }

    function describeSelection() {
        var labels = selectedGroupLabels();
        var loose = looseCheckedIds().length;
        var groupLabel = labels.length ? labels.join(", ") : "";
        if (loose) {
            groupLabel += (groupLabel ? " + " : "") + loose + (loose === 1 ? " selected unit" : " selected units");
        }
        if (groupLabel.length > 380) {
            groupLabel = groupLabel.substring(0, 377) + "...";
        }
        return groupLabel || "Asset register selection";
    }

    // "Grouped by Sub-type; Search: x; Status: In Store" from the list filter form.
    function describeFilters() {
        var parts = [];
        var viewText = toolbar ? toolbar.getAttribute("data-am-register-view-label") : "";
        if (viewText) {
            parts.push(viewText);
        }
        var form = document.getElementById("amAssetListFilterForm");
        if (form) {
            Array.prototype.forEach.call(form.elements, function (el) {
                if (!el.name || el.type === "hidden" || el.type === "submit" || el.type === "button" || !el.value) {
                    return;
                }
                if ((el.type === "checkbox" || el.type === "radio") && !el.checked) {
                    return;
                }
                var name = el.name;
                if (/^(view|groupBy|sort|direction|page|pageSize)$/i.test(name)) {
                    return;
                }
                var label = form.querySelector('label[for="' + el.id + '"]');
                var text = el.tagName === "SELECT" && el.selectedIndex >= 0 ? el.options[el.selectedIndex].text : el.value;
                parts.push((label ? label.textContent.trim() : name) + ": " + String(text).trim());
            });
        }
        var result = parts.join("; ");
        return result.length > 1900 ? result.substring(0, 1897) + "..." : result;
    }

    function fillIds(holder, ids) {
        holder.innerHTML = "";
        ids.forEach(function (id) {
            var input = document.createElement("input");
            input.type = "hidden";
            input.name = holder.getAttribute("data-am-ids-name") || "AssetIds";
            input.value = id;
            holder.appendChild(input);
        });
    }

    function setBusy(value, message) {
        busy = value;
        var status = document.querySelector("[data-am-selection-status]");
        if (status) {
            status.textContent = message || "";
            status.className = "small " + (value ? "text-muted" : "text-danger");
        }
        syncUi();
    }

    if (printButton && printForm) {
        printButton.addEventListener("click", function () {
            if (busy) {
                return;
            }
            setBusy(true, "Collecting the selected units...");
            resolveSelectedIds().then(function (ids) {
                fillIds(printForm.querySelector("[data-am-send-to-print-ids]"), ids);
                printForm.querySelector('input[name="groupLabel"]').value = describeSelection();
                printForm.querySelector('input[name="filterDescription"]').value = describeFilters();
                printForm.querySelector('input[name="returnUrl"]').value = window.location.pathname + window.location.search;
                printForm.submit();
            }).catch(function (error) {
                setBusy(false, error && error.message ? error.message : "Could not collect the selection.");
            });
        });
    }

    // Bulk action (Move to department / status / maintenance): expand whole groups before the existing post.
    var bulkForm = document.getElementById("amBulkForm");
    if (bulkForm) {
        bulkForm.addEventListener("submit", function (event) {
            if (!fullySelectedGroups().length) {
                return; // nothing to expand: list-bulk.js already put the ticked ids in the form
            }
            event.preventDefault();
            if (busy) {
                return;
            }
            setBusy(true, "Collecting the selected units...");
            resolveSelectedIds().then(function (ids) {
                fillIds(document.getElementById("amBulkAssetIds"), ids);
                bulkForm.submit(); // native submit: does not re-run this handler
            }).catch(function (error) {
                setBusy(false, error && error.message ? error.message : "Could not collect the selection.");
            });
        });
    }

    groupBoxes().forEach(watchPanel);
    syncUi();

    window.AmAssetGroupSelection = {
        hasSelection: hasSelection,
        estimateCount: estimateCount,
        resolveSelectedIds: resolveSelectedIds,
        describeSelection: describeSelection,
        describeFilters: describeFilters,
        refresh: syncUi
    };
})(window);
