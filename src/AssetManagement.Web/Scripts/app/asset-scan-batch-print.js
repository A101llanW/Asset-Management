(function (window) {
    "use strict";

    var config = {};
    var queue = [];
    var candidates = [];
    var collapsedDays = {};
    var isPrinting = false;
    var printSession = null;
    var panelEventsBound = false;

    function escapeHtml(text) {
        if (!text) {
            return "";
        }

        var div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    function getPanel() {
        return document.querySelector("[data-am-scan-batch-panel]");
    }

    function readConfigFromPanel(panel) {
        if (!panel) {
            return { enabled: false };
        }

        return {
            enabled: true,
            printCandidatesUrl: panel.getAttribute("data-print-candidates-url"),
            labelPrintConfigUrlTemplate: panel.getAttribute("data-label-print-config-url-template"),
            labelZplUrlTemplate: panel.getAttribute("data-label-zpl-url-template")
        };
    }

    function getCodeType() {
        var select = document.querySelector("[data-am-scan-batch-code-type]");
        return select ? select.value : "Qr";
    }

    // Saved Label designer template chosen on the panel ("" = active layout).
    function getTemplateId() {
        var panel = getPanel();
        var select = panel ? panel.querySelector("[data-am-scan-batch-template]") : null;
        return select && select.value ? select.value : "";
    }

    function appendTemplateId(url, templateId) {
        if (!url || !templateId) {
            return url;
        }

        return url + (url.indexOf("?") >= 0 ? "&" : "?") + "templateId=" + encodeURIComponent(templateId);
    }

    function buildZplUrl(baseUrl, codeType, templateId) {
        if (window.AssetLabelZebra && typeof window.AssetLabelZebra.buildZplUrl === "function") {
            return window.AssetLabelZebra.buildZplUrl(baseUrl, codeType, templateId);
        }

        if (!baseUrl) {
            return baseUrl;
        }

        var separator = baseUrl.indexOf("?") >= 0 ? "&" : "?";
        return appendTemplateId(baseUrl + separator + "codeType=" + encodeURIComponent(codeType || "Qr"), templateId);
    }

    function resolveUrlTemplate(template, assetId) {
        if (!template) {
            return null;
        }

        return template.replace("__id__", String(assetId));
    }

    function findInQueue(assetId) {
        for (var i = 0; i < queue.length; i++) {
            if (String(queue[i].id) === String(assetId)) {
                return queue[i];
            }
        }

        return null;
    }

    function syncCountBadge() {
        var badge = document.querySelector("[data-am-scan-batch-count]");
        if (badge) {
            badge.textContent = queue.length + " queued";
        }
    }

    function syncPrintControls() {
        var panel = getPanel();
        if (!panel) {
            return;
        }

        var pauseBtn = panel.querySelector("[data-am-scan-batch-pause]");
        var resumeBtn = panel.querySelector("[data-am-scan-batch-resume]");
        var stopBtn = panel.querySelector("[data-am-scan-batch-stop]");
        var isPaused = printSession && printSession.paused;

        if (pauseBtn) {
            pauseBtn.classList.toggle("d-none", !isPrinting || isPaused);
        }
        if (resumeBtn) {
            resumeBtn.classList.toggle("d-none", !isPrinting || !isPaused);
        }
        if (stopBtn) {
            stopBtn.classList.toggle("d-none", !isPrinting);
        }
    }

    function syncActionButtons() {
        var panel = getPanel();
        if (!panel) {
            return;
        }

        var hasCandidates = candidates.length > 0;
        var hasQueue = queue.length > 0;
        var selectAllBtn = panel.querySelector("[data-am-scan-batch-select-all]");
        var addAllBtn = panel.querySelector("[data-am-scan-batch-add-all]");
        var addSelectedBtn = panel.querySelector("[data-am-scan-batch-add-selected]");
        var printSelectedQrBtn = panel.querySelector("[data-am-scan-batch-print-selected-qr]");
        var printBtn = panel.querySelector("[data-am-scan-batch-run-print]");
        var clearBtn = panel.querySelector("[data-am-scan-batch-clear]");

        if (selectAllBtn) {
            selectAllBtn.disabled = !hasCandidates || isPrinting;
        }
        if (addAllBtn) {
            addAllBtn.disabled = !hasCandidates || isPrinting;
        }
        if (addSelectedBtn) {
            addSelectedBtn.disabled = !hasCandidates || isPrinting;
        }
        if (printSelectedQrBtn) {
            printSelectedQrBtn.disabled = !hasCandidates || isPrinting;
        }
        if (printBtn) {
            printBtn.disabled = !hasQueue || isPrinting;
        }
        if (clearBtn) {
            clearBtn.disabled = !hasQueue || isPrinting;
        }

        syncPrintControls();
    }

    function renderQueue() {
        var list = document.querySelector("[data-am-scan-batch-queue]");
        if (!list) {
            return;
        }

        if (!queue.length) {
            list.innerHTML = '<li class="text-muted small" data-am-scan-batch-queue-empty>No labels queued yet.</li>';
            syncCountBadge();
            syncActionButtons();
            return;
        }

        list.innerHTML = queue.map(function (item) {
            return (
                '<li class="d-flex align-items-center justify-content-between gap-2 py-1 border-bottom border-light">' +
                '<span class="small"><span class="am-tag-pill am-tag-pill--sm me-1">' + escapeHtml(item.tag) + "</span>" +
                escapeHtml(item.name) + "</span>" +
                '<button type="button" class="btn btn-sm btn-link text-danger p-0" data-am-scan-batch-remove="' + item.id + '" aria-label="Remove from queue">&times;</button>' +
                "</li>"
            );
        }).join("");

        syncCountBadge();
        syncActionButtons();
    }

    function renderCandidates() {
        var host = document.querySelector("[data-am-scan-batch-candidates]");
        if (!host) {
            return;
        }

        if (!candidates.length) {
            host.innerHTML = '<p class="text-muted small mb-0">No assets matched these filters.</p>';
            syncActionButtons();
            return;
        }

        // rel1014a: group by registration day (EAT), keeping the server order (newest first by default).
        var groups = [];
        var groupIndex = {};
        candidates.forEach(function (asset) {
            var key = asset.dayKey || "unknown";
            if (!Object.prototype.hasOwnProperty.call(groupIndex, key)) {
                groupIndex[key] = groups.length;
                groups.push({ key: key, label: asset.dayLabel || (key === "unknown" ? "Entry date unknown" : key), items: [] });
            }
            groups[groupIndex[key]].items.push(asset);
        });

        var bodies = groups.map(function (group) {
            var collapsed = !!collapsedDays[group.key];
            var safeKey = escapeHtml(group.key);
            var header =
                '<tr class="am-scan-batch-day-header bg-light" data-am-scan-batch-day-header="' + safeKey + '">' +
                '<td class="ps-3"><input type="checkbox" class="form-check-input" data-am-scan-batch-day-select="' + safeKey + '" aria-label="Select all assets registered on ' + escapeHtml(group.label) + '" /></td>' +
                '<td colspan="8"><button type="button" class="btn btn-sm btn-link p-0 text-decoration-none fw-semibold" data-am-scan-batch-day-toggle="' + safeKey + '" aria-expanded="' + (collapsed ? "false" : "true") + '">' +
                '<span class="am-scan-batch-day-caret" aria-hidden="true">' + (collapsed ? "&#9656;" : "&#9662;") + '</span> Registered ' + escapeHtml(group.label) +
                '</button> <span class="text-muted small">(' + group.items.length + (group.items.length === 1 ? " asset" : " assets") + ')</span></td></tr>';
            return '<tbody data-am-scan-batch-day-group="' + safeKey + '">' + header + group.items.map(function (asset) { return renderCandidateRow(asset, group.key, collapsed); }).join("") + "</tbody>";
        }).join("");

        host.innerHTML =
            '<div class="table-responsive"><table class="table table-hover align-middle mb-0">' +
            '<thead><tr class="bg-light" style="font-size:0.82rem;">' +
            '<th class="ps-3"><input type="checkbox" class="form-check-input" data-am-scan-batch-candidate-master aria-label="Select all loaded" /></th>' +
            "<th>Tag</th><th>Name</th><th>Department</th><th>Status</th><th>Entered</th><th>Last assigned</th><th>Last activity</th><th class=\"pe-3 text-end\">Action</th>" +
            "</tr></thead>" + bodies + "</table></div>";

        syncSelectionStates();
        syncActionButtons();
    }

    function renderCandidateRow(asset, dayKey, collapsed) {
            var inQueue = !!findInQueue(asset.id);
            return (
                '<tr class="border-bottom border-light' + (collapsed ? " d-none" : "") + '" data-am-scan-batch-day-row="' + escapeHtml(dayKey) + '">' +
                '<td class="ps-3"><input type="checkbox" class="form-check-input" data-am-scan-batch-candidate-id="' + asset.id + '" data-am-scan-batch-day="' + escapeHtml(dayKey) + '"' +
                (inQueue ? " checked disabled" : "") + " /></td>" +
                '<td><span class="am-tag-pill am-tag-pill--sm">' + escapeHtml(asset.tag) + "</span></td>" +
                '<td class="fw-semibold text-dark">' + escapeHtml(asset.name) + "</td>" +
                '<td class="text-muted small">' + escapeHtml(asset.department || "") + "</td>" +
                '<td><span class="badge rounded-pill bg-light border text-secondary">' + escapeHtml(asset.status || "") + "</span></td>" +
                '<td class="text-muted small text-nowrap">' + escapeHtml(asset.enteredAt || "-") + "</td>" +
                '<td class="text-muted small text-nowrap">' + escapeHtml(asset.lastAssignedAt || "-") + "</td>" +
                '<td class="text-muted small text-nowrap">' + escapeHtml(asset.lastActivityAt || "-") + "</td>" +
                '<td class="text-end pe-3">' +
                (inQueue
                    ? '<span class="small text-success">Queued</span>'
                    : '<button type="button" class="btn btn-sm btn-outline-primary" data-am-scan-batch-add-one="' + asset.id + '">Add</button>') +
                "</td></tr>"
            );
    }

    // Tri-state: a day header (and the master box) is checked when every selectable row is checked,
    // indeterminate when some are, unchecked when none are. Rows already queued are disabled and ignored.
    function triState(boxes) {
        var total = 0;
        var checked = 0;
        for (var i = 0; i < boxes.length; i++) {
            if (boxes[i].disabled) {
                continue;
            }
            total++;
            if (boxes[i].checked) {
                checked++;
            }
        }
        return { total: total, checked: checked };
    }

    function applyTriState(box, state) {
        if (!box) {
            return;
        }
        box.indeterminate = state.checked > 0 && state.checked < state.total;
        box.checked = state.total > 0 && state.checked === state.total;
        box.disabled = state.total === 0;
    }

    function syncSelectionStates() {
        var host = document.querySelector("[data-am-scan-batch-candidates]");
        if (!host) {
            return;
        }
        var dayBoxes = host.querySelectorAll("[data-am-scan-batch-day-select]");
        for (var i = 0; i < dayBoxes.length; i++) {
            var key = dayBoxes[i].getAttribute("data-am-scan-batch-day-select");
            applyTriState(dayBoxes[i], triState(rowBoxesForDay(host, key)));
        }
        applyTriState(host.querySelector("[data-am-scan-batch-candidate-master]"), triState(host.querySelectorAll("[data-am-scan-batch-candidate-id]")));
    }

    function rowBoxesForDay(scope, key) {
        var all = scope.querySelectorAll("[data-am-scan-batch-candidate-id]");
        var out = [];
        for (var i = 0; i < all.length; i++) {
            if (all[i].getAttribute("data-am-scan-batch-day") === key) {
                out.push(all[i]);
            }
        }
        return out;
    }

    function setDayCollapsed(scope, key, collapsed) {
        collapsedDays[key] = collapsed;
        var rows = scope.querySelectorAll("[data-am-scan-batch-day-row]");
        for (var i = 0; i < rows.length; i++) {
            if (rows[i].getAttribute("data-am-scan-batch-day-row") === key) {
                rows[i].classList.toggle("d-none", collapsed);
            }
        }
        var toggles = scope.querySelectorAll("[data-am-scan-batch-day-toggle]");
        for (var j = 0; j < toggles.length; j++) {
            if (toggles[j].getAttribute("data-am-scan-batch-day-toggle") === key) {
                toggles[j].setAttribute("aria-expanded", collapsed ? "false" : "true");
                var caret = toggles[j].querySelector(".am-scan-batch-day-caret");
                if (caret) {
                    caret.innerHTML = collapsed ? "&#9656;" : "&#9662;";
                }
            }
        }
    }

    function normalizeAsset(raw) {
        var id = raw.Id || raw.id || raw.AssetId || raw.assetId;
        return {
            id: id,
            tag: raw.AssetTag || raw.assetTag || raw.tag,
            name: raw.AssetName || raw.assetName || raw.name,
            department: raw.DepartmentName || raw.departmentName || raw.department || "",
            status: raw.Status || raw.status || "",
            enteredAt: raw.EnteredAt || raw.enteredAt || "",
            lastAssignedAt: raw.LastAssignedAt || raw.lastAssignedAt || "",
            lastActivityAt: raw.LastActivityAt || raw.lastActivityAt || "",
            dayKey: raw.EntryDay || raw.entryDay || "unknown",
            dayLabel: raw.EntryDayLabel || raw.entryDayLabel || "",
            labelZplUrl: raw.LabelZplUrl || raw.labelZplUrl || resolveUrlTemplate(config.labelZplUrlTemplate, id)
        };
    }

    function addToQueue(asset) {
        if (!asset || !asset.id || findInQueue(asset.id)) {
            return false;
        }

        queue.push({
            id: asset.id,
            tag: asset.tag,
            name: asset.name,
            labelZplUrl: asset.labelZplUrl || resolveUrlTemplate(config.labelZplUrlTemplate, asset.id)
        });
        renderQueue();
        renderCandidates();
        markSearchResultQueued(asset.id);
        return true;
    }

    function removeFromQueue(assetId) {
        queue = queue.filter(function (item) {
            return String(item.id) !== String(assetId);
        });
        renderQueue();
        renderCandidates();
        unmarkSearchResultQueued(assetId);
    }

    function markSearchResultQueued(assetId) {
        var checkbox = document.querySelector('[data-am-scan-search-id="' + assetId + '"]');
        if (checkbox) {
            checkbox.checked = true;
            checkbox.disabled = true;
        }

        var addBtn = document.querySelector('[data-am-scan-search-add="' + assetId + '"]');
        if (addBtn) {
            addBtn.textContent = "Queued";
            addBtn.disabled = true;
            addBtn.classList.remove("btn-outline-primary");
            addBtn.classList.add("btn-outline-success");
        }
    }

    function unmarkSearchResultQueued(assetId) {
        var checkbox = document.querySelector('[data-am-scan-search-id="' + assetId + '"]');
        if (checkbox) {
            checkbox.checked = false;
            checkbox.disabled = false;
        }

        var addBtn = document.querySelector('[data-am-scan-search-add="' + assetId + '"]');
        if (addBtn) {
            addBtn.textContent = "Queue";
            addBtn.disabled = false;
            addBtn.classList.remove("btn-outline-success");
            addBtn.classList.add("btn-outline-primary");
        }
    }

    function getSelectedCandidateIds() {
        var ids = [];
        var checkboxes = document.querySelectorAll("[data-am-scan-batch-candidate-id]");
        for (var i = 0; i < checkboxes.length; i++) {
            if (checkboxes[i].checked && !checkboxes[i].disabled) {
                ids.push(checkboxes[i].getAttribute("data-am-scan-batch-candidate-id"));
            }
        }
        return ids;
    }

    function formatInputDate(date) {
        var month = String(date.getMonth() + 1);
        var day = String(date.getDate());
        return date.getFullYear() + "-" + (month.length < 2 ? "0" + month : month) + "-" + (day.length < 2 ? "0" + day : day);
    }

    // Presets use the browser's calendar day; the server reads the dates as EAT days.
    function applyDatePreset(preset) {
        var fromInput = document.querySelector("[data-am-scan-batch-from]");
        var toInput = document.querySelector("[data-am-scan-batch-to]");
        if (!fromInput || !toInput) {
            return;
        }

        if (preset === "clear") {
            fromInput.value = "";
            toInput.value = "";
            return;
        }

        var today = new Date();
        var from = new Date(today.getFullYear(), today.getMonth(), today.getDate());
        var days = preset === "today" ? 1 : parseInt(preset, 10);
        if (days > 1) {
            from.setDate(from.getDate() - (days - 1));
        }
        fromInput.value = formatInputDate(from);
        toInput.value = formatInputDate(today);
    }

    function queueSelectedCandidates() {
        var ids = getSelectedCandidateIds();
        for (var j = 0; j < ids.length; j++) {
            for (var k = 0; k < candidates.length; k++) {
                if (String(candidates[k].id) === String(ids[j])) {
                    addToQueue(candidates[k]);
                    break;
                }
            }
        }
        return ids.length;
    }

    function loadCandidates() {
        if (!config.printCandidatesUrl) {
            var host = document.querySelector("[data-am-scan-batch-candidates]");
            if (host) {
                host.innerHTML = '<p class="text-danger small mb-0">Batch print is not configured for this page.</p>';
            }
            return;
        }

        var dept = document.querySelector("[data-am-scan-batch-dept]");
        var status = document.querySelector("[data-am-scan-batch-status]");
        var search = document.querySelector("[data-am-scan-batch-search]");
        var params = [];

        if (dept && dept.value) {
            params.push("departmentId=" + encodeURIComponent(dept.value));
            var includeSub = document.querySelector("[data-am-scan-batch-include-sub]");
            params.push("includeSubDepartments=" + (includeSub && !includeSub.checked ? "false" : "true"));
        }

        // Category > type > sub-type (AND with every other filter, applied server-side).
        var category = document.querySelector("[data-am-scan-batch-category]");
        var assetType = document.querySelector("[data-am-scan-batch-type]");
        var subType = document.querySelector("[data-am-scan-batch-subtype]");
        if (category && category.value) {
            params.push("categoryId=" + encodeURIComponent(category.value));
        }
        if (assetType && assetType.value) {
            params.push("assetTypeId=" + encodeURIComponent(assetType.value));
        }
        if (subType && subType.value) {
            params.push("assetSubTypeId=" + encodeURIComponent(subType.value));
        }
        if (status && status.value) {
            params.push("status=" + encodeURIComponent(status.value));
        }
        if (search && search.value.trim()) {
            params.push("search=" + encodeURIComponent(search.value.trim()));
        }

        var po = document.querySelector("[data-am-scan-batch-po]");
        if (po && po.value) {
            params.push("purchaseId=" + encodeURIComponent(po.value));
        }

        var sortSelect = document.querySelector("[data-am-scan-batch-sort]");
        if (sortSelect && sortSelect.value) {
            var sortParts = sortSelect.value.split("|");
            params.push("sort=" + encodeURIComponent(sortParts[0]));
            if (sortParts[1]) {
                params.push("direction=" + encodeURIComponent(sortParts[1]));
            }
        }

        var dateField = document.querySelector("[data-am-scan-batch-date-field]");
        var fromInput = document.querySelector("[data-am-scan-batch-from]");
        var toInput = document.querySelector("[data-am-scan-batch-to]");
        var datePrefix = dateField && dateField.value === "assigned" ? "assigned" : "created";
        if (fromInput && fromInput.value) {
            params.push(datePrefix + "From=" + encodeURIComponent(fromInput.value));
        }
        if (toInput && toInput.value) {
            params.push(datePrefix + "To=" + encodeURIComponent(toInput.value));
        }

        var takeInput = document.querySelector("[data-am-scan-batch-take]");
        var take = takeInput ? parseInt(takeInput.value, 10) : 100;
        if (!take || take < 1) {
            take = 50;
        }
        params.push("take=" + Math.min(take, 100));

        var url = config.printCandidatesUrl + (params.length ? "?" + params.join("&") : "");
        var host = document.querySelector("[data-am-scan-batch-candidates]");
        if (host) {
            host.innerHTML = '<p class="text-muted small mb-0">Loading assets...</p>';
        }

        fetch(url, {
            credentials: "same-origin",
            headers: {
                Accept: "application/json",
                "X-Requested-With": "XMLHttpRequest"
            }
        })
            .then(function (response) {
                var contentType = response.headers.get("content-type") || "";
                if (!response.ok) {
                    if (response.status === 400 && contentType.indexOf("application/json") >= 0) {
                        return response.json().then(function (body) {
                            throw new Error(body && body.Error ? body.Error : "Invalid filter.");
                        });
                    }
                    throw new Error("Unable to load assets (HTTP " + response.status + ").");
                }
                if (contentType.indexOf("application/json") === -1) {
                    throw new Error("Unexpected response while loading assets.");
                }
                return response.json();
            })
            .then(function (data) {
                var assets = data.Assets || data.assets || [];
                candidates = assets.map(normalizeAsset);
                renderCandidates();
            })
            .catch(function (error) {
                candidates = [];
                if (host) {
                    host.innerHTML = '<p class="text-danger small mb-0">' +
                        escapeHtml(error && error.message ? error.message : "Unable to load assets. Try again.") +
                        "</p>";
                }
                syncActionButtons();
            });
    }

    function setProgress(current, total, message, options) {
        var wrap = document.querySelector("[data-am-scan-batch-progress]");
        var text = document.querySelector("[data-am-scan-batch-progress-text]");
        var count = document.querySelector("[data-am-scan-batch-progress-count]");
        var bar = document.querySelector("[data-am-scan-batch-progress-bar]");
        var opts = options || {};

        if (!wrap) {
            return;
        }

        if (total <= 0) {
            wrap.classList.add("d-none");
            return;
        }

        wrap.classList.remove("d-none");
        if (text) {
            text.textContent = message || "Printing...";
        }
        if (count) {
            count.textContent = current + " / " + total;
        }
        if (bar) {
            bar.style.width = Math.round((current / total) * 100) + "%";
            if (opts.paused) {
                bar.classList.remove("progress-bar-animated");
            } else {
                bar.classList.add("progress-bar-animated");
            }
        }
    }

    function waitIfPaused(session) {
        if (!session || session.stopped || !session.paused) {
            return Promise.resolve();
        }

        return new Promise(function (resolve) {
            session.pauseWaiters.push(resolve);
        });
    }

    function resumePausedPrint() {
        if (!printSession) {
            return;
        }

        printSession.paused = false;
        var waiters = printSession.pauseWaiters.slice();
        printSession.pauseWaiters = [];
        for (var i = 0; i < waiters.length; i++) {
            waiters[i]();
        }

        setProgress(printSession.index, printSession.total, "Printing next label...");
        syncPrintControls();
    }

    function pausePrint() {
        if (!printSession || printSession.paused || printSession.stopped) {
            return;
        }

        printSession.paused = true;
        setProgress(printSession.index, printSession.total, "Paused \u2014 resume to continue.", { paused: true });
        syncPrintControls();
    }

    function stopPrint() {
        if (!printSession) {
            return;
        }

        printSession.stopped = true;
        printSession.paused = false;
        resumePausedPrint();
    }

    function finishPrintSession(stoppedEarly) {
        var session = printSession;
        var printedCount = session ? session.index : 0;
        var total = session ? session.total : 0;
        var printerConfig = session ? session.printerConfig : null;

        printSession = null;
        isPrinting = false;

        if (stoppedEarly && printedCount < total) {
            setProgress(printedCount, total, "Stopped after " + printedCount + " of " + total + " labels.");
        } else if (printedCount >= total && total > 0) {
            setProgress(total, total, "Print queue complete.");
        } else {
            setProgress(0, 0, "");
        }

        syncActionButtons();

        if (window.AssetLabelZebra && window.AssetLabelZebra.pollPrinterStatus && printerConfig) {
            window.AssetLabelZebra.pollPrinterStatus(getPanel(), printerConfig);
        }
    }

    function fetchZpl(url) {
        return fetch(url, { credentials: "same-origin" }).then(function (response) {
            if (!response.ok) {
                throw new Error("Unable to load label data.");
            }
            return response.text();
        });
    }

    function fetchPrinterConfig(templateId) {
        if (!queue.length || !config.labelPrintConfigUrlTemplate) {
            return Promise.reject(new Error("Print queue is empty."));
        }

        if (!window.AssetLabelZebra || typeof window.AssetLabelZebra.fetchPrinterConfig !== "function") {
            return Promise.reject(new Error("Zebra print module is unavailable."));
        }

        var configUrl = appendTemplateId(resolveUrlTemplate(config.labelPrintConfigUrlTemplate, queue[0].id), templateId);
        return window.AssetLabelZebra.fetchPrinterConfig(configUrl);
    }

    function printQueue() {
        if (!queue.length || isPrinting) {
            return;
        }

        if (!window.AssetLabelZebra || typeof window.AssetLabelZebra.printZplDirectly !== "function") {
            if (window.AssetLabelZebraEnsure && !printQueue._loading) {
                printQueue._loading = true;
                window.AssetLabelZebraEnsure()
                    .then(function () {
                        printQueue._loading = false;
                        printQueue();
                    })
                    .catch(function (err) {
                        printQueue._loading = false;
                        window.alert((err && err.message) ? err.message : "Zebra print module is unavailable.");
                    });
                return;
            }
            window.alert("Zebra print module is unavailable.");
            return;
        }

        isPrinting = true;
        syncActionButtons();

        var codeType = getCodeType();
        // One template for the whole run (picked once, even if the picker changes mid-print).
        var templateId = getTemplateId();
        var items = queue.slice();
        var total = items.length;

        printSession = {
            items: items,
            total: total,
            index: 0,
            printerConfig: null,
            codeType: codeType,
            templateId: templateId,
            paused: false,
            stopped: false,
            pauseWaiters: []
        };

        setProgress(0, total, "Preparing printer...");

        fetchPrinterConfig(templateId)
            .then(function (printerConfig) {
                if (!printSession) {
                    return;
                }

                if (!printerConfig || !printerConfig.enabled || printerConfig.mode !== "ZebraBrowserPrint") {
                    throw new Error("Zebra Browser Print is not enabled. Configure it under Settings.");
                }

                printSession.printerConfig = printerConfig;

                function printNext() {
                    if (!printSession || printSession.stopped) {
                        finishPrintSession(true);
                        return Promise.resolve();
                    }

                    if (printSession.index >= printSession.total) {
                        finishPrintSession(false);
                        return Promise.resolve();
                    }

                    return waitIfPaused(printSession).then(function () {
                        if (!printSession || printSession.stopped) {
                            finishPrintSession(true);
                            return;
                        }

                        var item = printSession.items[printSession.index];
                        setProgress(printSession.index, printSession.total, "Printing " + item.tag + "...");
                        var zplUrl = buildZplUrl(item.labelZplUrl, printSession.codeType, printSession.templateId);

                        return fetchZpl(zplUrl)
                            .then(function (zpl) {
                                if (!printSession || printSession.stopped) {
                                    finishPrintSession(true);
                                    return;
                                }

                                return window.AssetLabelZebra.printZplDirectly(zpl, printSession.printerConfig);
                            })
                            .then(function () {
                                if (!printSession || printSession.stopped) {
                                    finishPrintSession(true);
                                    return;
                                }

                                removeFromQueue(item.id);
                                printSession.index += 1;
                                setProgress(
                                    printSession.index,
                                    printSession.total,
                                    printSession.index >= printSession.total ? "Print queue complete." : "Printing next label..."
                                );
                                return printNext();
                            });
                    });
                }

                return printNext();
            })
            .catch(function (error) {
                finishPrintSession(true);
                window.alert(error && error.message ? error.message : "Batch print failed.");
            });
    }

    function handlePanelClick(event) {
        var target = event.target;
        if (!target || !target.closest) {
            return;
        }

        var panel = target.closest("[data-am-scan-batch-panel]");
        if (!panel) {
            return;
        }

        var clickTarget = target.closest("[data-am-scan-batch-load], [data-am-scan-batch-select-all], [data-am-scan-batch-add-all], [data-am-scan-batch-add-selected], [data-am-scan-batch-print-selected-qr], [data-am-scan-batch-preset], [data-am-scan-batch-run-print], [data-am-scan-batch-pause], [data-am-scan-batch-resume], [data-am-scan-batch-stop], [data-am-scan-batch-clear], [data-am-scan-batch-add-one], [data-am-scan-batch-remove], [data-am-scan-batch-day-toggle]");
        if (!clickTarget) {
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-load]")) {
            loadCandidates();
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-select-all]")) {
            var master = panel.querySelector("[data-am-scan-batch-candidate-master]");
            if (master) {
                master.checked = true;
            }
            var boxes = panel.querySelectorAll("[data-am-scan-batch-candidate-id]");
            for (var i = 0; i < boxes.length; i++) {
                if (!boxes[i].disabled) {
                    boxes[i].checked = true;
                }
            }
            syncSelectionStates();
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-add-all]")) {
            for (var n = 0; n < candidates.length; n++) {
                addToQueue(candidates[n]);
            }
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-add-selected]")) {
            queueSelectedCandidates();
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-print-selected-qr]")) {
            if (!queueSelectedCandidates() && !queue.length) {
                window.alert("Select at least one asset first.");
                return;
            }
            var codeType = panel.querySelector("[data-am-scan-batch-code-type]");
            if (codeType && codeType.options.length) {
                codeType.value = codeType.options[0].value; // first option is QR code
            }
            printQueue();
            return;
        }

        var dayToggle = clickTarget.getAttribute("data-am-scan-batch-day-toggle");
        if (dayToggle !== null) {
            setDayCollapsed(panel, dayToggle, !collapsedDays[dayToggle]);
            return;
        }

        var preset = clickTarget.getAttribute("data-am-scan-batch-preset");
        if (preset) {
            applyDatePreset(preset);
            loadCandidates();
            return;
        }

        var addOneId = clickTarget.getAttribute("data-am-scan-batch-add-one");
        if (addOneId) {
            for (var m = 0; m < candidates.length; m++) {
                if (String(candidates[m].id) === String(addOneId)) {
                    addToQueue(candidates[m]);
                    break;
                }
            }
            return;
        }

        var removeId = clickTarget.getAttribute("data-am-scan-batch-remove");
        if (removeId) {
            removeFromQueue(removeId);
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-run-print]")) {
            printQueue();
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-pause]")) {
            pausePrint();
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-resume]")) {
            resumePausedPrint();
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-stop]")) {
            stopPrint();
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-clear]")) {
            while (queue.length) {
                removeFromQueue(queue[0].id);
            }
        }
    }

    // Cascading Category > Type > Sub-type pickers from the org catalog embedded on the panel.
    var classification = null;

    function readClassification(panel) {
        if (classification) {
            return classification;
        }

        try {
            classification = JSON.parse(panel.getAttribute("data-am-scan-batch-classification") || "{}") || {};
        } catch (e) {
            classification = {};
        }

        classification.Categories = classification.Categories || [];
        classification.Types = classification.Types || [];
        classification.SubTypes = classification.SubTypes || [];
        return classification;
    }

    function fillOptions(select, items, allText, parentId) {
        if (!select) {
            return;
        }

        var previous = select.value;
        var html = '<option value="">' + escapeHtml(allText) + "</option>";
        var keep = false;
        for (var i = 0; i < items.length; i++) {
            var item = items[i];
            if (parentId !== undefined && parentId !== null && String(item.ParentId) !== String(parentId)) {
                continue;
            }

            if (String(item.Id) === previous) {
                keep = true;
            }

            html += '<option value="' + escapeHtml(String(item.Id)) + '">' + escapeHtml(item.Name || "") + "</option>";
        }

        select.innerHTML = html;
        select.value = keep ? previous : "";
    }

    // A chosen category limits types; a chosen type limits sub-types. With no parent chosen, all children of the org show.
    function syncClassificationOptions(panel, changed) {
        var data = readClassification(panel);
        var category = panel.querySelector("[data-am-scan-batch-category]");
        var assetType = panel.querySelector("[data-am-scan-batch-type]");
        var subType = panel.querySelector("[data-am-scan-batch-subtype]");
        if (!category || !assetType || !subType) {
            return;
        }

        if (!changed) {
            fillOptions(category, data.Categories, "All categories", null);
        }

        if (!changed || changed === "category") {
            fillOptions(assetType, data.Types, "All types", category.value || null);
        }

        if (!changed || changed === "category" || changed === "type") {
            var typeIds = null;
            if (!assetType.value && category.value) {
                typeIds = {};
                for (var t = 0; t < data.Types.length; t++) {
                    if (String(data.Types[t].ParentId) === category.value) {
                        typeIds[String(data.Types[t].Id)] = true;
                    }
                }
            }

            var subs = typeIds
                ? data.SubTypes.filter(function (s) { return typeIds[String(s.ParentId)]; })
                : data.SubTypes;
            fillOptions(subType, subs, "All sub-types", assetType.value || null);
        }
    }

    function handlePanelChange(event) {
        var target = event.target;
        if (!target || !target.matches || !target.closest) {
            return;
        }

        var panel = target.closest("[data-am-scan-batch-panel]");
        if (!panel) {
            return;
        }

        if (target.matches("[data-am-scan-batch-category]")) {
            syncClassificationOptions(panel, "category");
            return;
        }

        if (target.matches("[data-am-scan-batch-type]")) {
            syncClassificationOptions(panel, "type");
            return;
        }

        if (target.matches("[data-am-scan-batch-candidate-master]")) {
            var boxes = panel.querySelectorAll("[data-am-scan-batch-candidate-id]");
            for (var i = 0; i < boxes.length; i++) {
                if (!boxes[i].disabled) {
                    boxes[i].checked = target.checked;
                }
            }
            syncSelectionStates();
            return;
        }

        if (target.matches("[data-am-scan-batch-day-select]")) {
            var dayBoxes = rowBoxesForDay(panel, target.getAttribute("data-am-scan-batch-day-select"));
            for (var d = 0; d < dayBoxes.length; d++) {
                if (!dayBoxes[d].disabled) {
                    dayBoxes[d].checked = target.checked;
                }
            }
            syncSelectionStates();
            return;
        }

        if (target.matches("[data-am-scan-batch-candidate-id]")) {
            syncSelectionStates();
        }
    }

    function bindPanelEvents() {
        if (panelEventsBound) {
            return;
        }

        panelEventsBound = true;
        document.addEventListener("click", handlePanelClick);
        document.addEventListener("change", handlePanelChange);
    }

    function bindSearchResultEvents() {
        document.addEventListener("click", function (event) {
            var target = event.target;
            if (!target || !target.getAttribute) {
                return;
            }

            var addId = target.getAttribute("data-am-scan-search-add");
            if (addId) {
                addToQueue({
                    id: addId,
                    tag: target.getAttribute("data-am-scan-search-tag") || "",
                    name: target.getAttribute("data-am-scan-search-name") || "",
                    labelZplUrl: target.getAttribute("data-am-scan-search-zpl") || resolveUrlTemplate(config.labelZplUrlTemplate, addId)
                });
            }
        });

        document.addEventListener("change", function (event) {
            var target = event.target;
            if (!target || !target.matches || !target.matches("[data-am-scan-search-id]")) {
                return;
            }

            if (target.checked && !target.disabled) {
                addToQueue({
                    id: target.value,
                    tag: target.getAttribute("data-am-scan-search-tag") || "",
                    name: target.getAttribute("data-am-scan-search-name") || "",
                    labelZplUrl: target.getAttribute("data-am-scan-search-zpl") || resolveUrlTemplate(config.labelZplUrlTemplate, target.value)
                });
            }
        });
    }

    function init(options) {
        var panel = getPanel();
        var panelConfig = readConfigFromPanel(panel);
        config = options || panelConfig || {};
        if (panelConfig && panelConfig.enabled) {
            config.enabled = true;
            config.printCandidatesUrl = config.printCandidatesUrl || panelConfig.printCandidatesUrl;
            config.labelPrintConfigUrlTemplate = config.labelPrintConfigUrlTemplate || panelConfig.labelPrintConfigUrlTemplate;
            config.labelZplUrlTemplate = config.labelZplUrlTemplate || panelConfig.labelZplUrlTemplate;
        }

        if (!config.enabled || !panel) {
            return;
        }

        bindPanelEvents();
        syncClassificationOptions(panel, null);
        renderQueue();
        syncActionButtons();

        // rel1014a: "Send to Bulk Label Print" from a purchase receipt arrives with ?purchaseId= preselected.
        if (panel.getAttribute("data-am-scan-batch-autoload") === "true") {
            loadCandidates();
        }

        // Do not load Zebra helper or contact BrowserPrint on Lookup page load.
        // Helper loads on first Print click (printQueue -> AssetLabelZebraEnsure).
    }

    function boot() {
        init();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }

    window.AmScanBatchPrint = {
        syncClassificationOptions: syncClassificationOptions,
        init: init,
        addToQueue: addToQueue,
        removeFromQueue: removeFromQueue,
        getQueue: function () { return queue.slice(); }
    };
})(window);
