(function (window) {
    "use strict";

    var config = {};
    var queue = [];
    var candidates = [];
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

    function buildZplUrl(baseUrl, codeType) {
        if (window.AssetLabelZebra && typeof window.AssetLabelZebra.buildZplUrl === "function") {
            return window.AssetLabelZebra.buildZplUrl(baseUrl, codeType);
        }

        if (!baseUrl) {
            return baseUrl;
        }

        var separator = baseUrl.indexOf("?") >= 0 ? "&" : "?";
        return baseUrl + separator + "codeType=" + encodeURIComponent(codeType || "Qr");
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

        var rows = candidates.map(function (asset) {
            var inQueue = !!findInQueue(asset.id);
            return (
                '<tr class="border-bottom border-light">' +
                '<td class="ps-3"><input type="checkbox" class="form-check-input" data-am-scan-batch-candidate-id="' + asset.id + '"' +
                (inQueue ? " checked disabled" : "") + " /></td>" +
                '<td><span class="am-tag-pill am-tag-pill--sm">' + escapeHtml(asset.tag) + "</span></td>" +
                '<td class="fw-semibold text-dark">' + escapeHtml(asset.name) + "</td>" +
                '<td class="text-muted small">' + escapeHtml(asset.department || "") + "</td>" +
                '<td><span class="badge rounded-pill bg-light border text-secondary">' + escapeHtml(asset.status || "") + "</span></td>" +
                '<td class="text-end pe-3">' +
                (inQueue
                    ? '<span class="small text-success">Queued</span>'
                    : '<button type="button" class="btn btn-sm btn-outline-primary" data-am-scan-batch-add-one="' + asset.id + '">Add</button>') +
                "</td></tr>"
            );
        }).join("");

        host.innerHTML =
            '<div class="table-responsive"><table class="table table-hover align-middle mb-0">' +
            '<thead><tr class="bg-light" style="font-size:0.82rem;">' +
            '<th class="ps-3"><input type="checkbox" class="form-check-input" data-am-scan-batch-candidate-master /></th>' +
            "<th>Tag</th><th>Name</th><th>Department</th><th>Status</th><th class=\"pe-3 text-end\">Action</th>" +
            "</tr></thead><tbody>" + rows + "</tbody></table></div>";

        syncActionButtons();
    }

    function normalizeAsset(raw) {
        var id = raw.Id || raw.id || raw.AssetId || raw.assetId;
        return {
            id: id,
            tag: raw.AssetTag || raw.assetTag || raw.tag,
            name: raw.AssetName || raw.assetName || raw.name,
            department: raw.DepartmentName || raw.departmentName || raw.department || "",
            status: raw.Status || raw.status || "",
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
        }
        if (status && status.value) {
            params.push("status=" + encodeURIComponent(status.value));
        }
        if (search && search.value.trim()) {
            params.push("search=" + encodeURIComponent(search.value.trim()));
        }
        params.push("take=100");

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
        setProgress(printSession.index, printSession.total, "Paused — resume to continue.", { paused: true });
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

    function fetchPrinterConfig() {
        if (!queue.length || !config.labelPrintConfigUrlTemplate) {
            return Promise.reject(new Error("Print queue is empty."));
        }

        if (!window.AssetLabelZebra || typeof window.AssetLabelZebra.fetchPrinterConfig !== "function") {
            return Promise.reject(new Error("Zebra print module is unavailable."));
        }

        var configUrl = resolveUrlTemplate(config.labelPrintConfigUrlTemplate, queue[0].id);
        return window.AssetLabelZebra.fetchPrinterConfig(configUrl);
    }

    function printQueue() {
        if (!queue.length || isPrinting) {
            return;
        }

        if (!window.AssetLabelZebra || typeof window.AssetLabelZebra.printZplDirectly !== "function") {
            window.alert("Zebra print module is unavailable.");
            return;
        }

        isPrinting = true;
        syncActionButtons();

        var codeType = getCodeType();
        var items = queue.slice();
        var total = items.length;

        printSession = {
            items: items,
            total: total,
            index: 0,
            printerConfig: null,
            codeType: codeType,
            paused: false,
            stopped: false,
            pauseWaiters: []
        };

        setProgress(0, total, "Preparing printer...");

        fetchPrinterConfig()
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
                        var zplUrl = buildZplUrl(item.labelZplUrl, printSession.codeType);

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

        var clickTarget = target.closest("[data-am-scan-batch-load], [data-am-scan-batch-select-all], [data-am-scan-batch-add-all], [data-am-scan-batch-add-selected], [data-am-scan-batch-run-print], [data-am-scan-batch-pause], [data-am-scan-batch-resume], [data-am-scan-batch-stop], [data-am-scan-batch-clear], [data-am-scan-batch-add-one], [data-am-scan-batch-remove]");
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
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-add-all]")) {
            for (var n = 0; n < candidates.length; n++) {
                addToQueue(candidates[n]);
            }
            return;
        }

        if (clickTarget.matches("[data-am-scan-batch-add-selected]")) {
            var ids = getSelectedCandidateIds();
            for (var j = 0; j < ids.length; j++) {
                for (var k = 0; k < candidates.length; k++) {
                    if (String(candidates[k].id) === String(ids[j])) {
                        addToQueue(candidates[k]);
                        break;
                    }
                }
            }
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

    function handlePanelChange(event) {
        var target = event.target;
        if (!target || !target.matches || !target.closest) {
            return;
        }

        var panel = target.closest("[data-am-scan-batch-panel]");
        if (!panel) {
            return;
        }

        if (target.matches("[data-am-scan-batch-candidate-master]")) {
            var boxes = panel.querySelectorAll("[data-am-scan-batch-candidate-id]");
            for (var i = 0; i < boxes.length; i++) {
                if (!boxes[i].disabled) {
                    boxes[i].checked = target.checked;
                }
            }
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
        bindSearchResultEvents();
        renderQueue();
        syncActionButtons();

        if (window.AssetLabelZebra && typeof window.AssetLabelZebra.initRoot === "function") {
            window.AssetLabelZebra.initRoot(panel);
        }
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
        init: init,
        addToQueue: addToQueue,
        removeFromQueue: removeFromQueue,
        getQueue: function () { return queue.slice(); }
    };
})(window);
