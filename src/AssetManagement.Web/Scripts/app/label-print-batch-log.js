// rel1022a: "Print batch log" drawer on Bulk Label Print. Collapsed and not loaded until the user opens it.
// Reprint group: loads the batch's assets into the print queue as a reprint (the user still clicks Print).
// Clear: soft-hides the entry (who/when kept on the batch); the per-asset print audit trail is never touched.
// View assets: read-only list of the batch's assets (tag, name, department, prints) under the row. Never prints or queues.
(function (window) {
    "use strict";

    var root = null;
    var loaded = false;

    function escapeHtml(text) {
        if (text === null || text === undefined) {
            return "";
        }
        var div = document.createElement("div");
        div.textContent = String(text);
        return div.innerHTML;
    }

    function setStatus(message, kind) {
        var node = root.querySelector("[data-am-print-batch-log-status]");
        if (!node) {
            return;
        }
        node.className = "small mb-2" + (kind === "error" ? " text-danger" : kind === "ok" ? " text-success" : " text-muted");
        node.textContent = message || "";
    }

    function statusBadge(batch) {
        if (batch.isCleared) {
            return '<span class="badge bg-secondary">Cleared</span>';
        }
        var map = { Completed: "bg-success", Printing: "bg-info text-dark", Stopped: "bg-warning text-dark", Queued: "bg-light text-dark border" };
        return '<span class="badge ' + (map[batch.status] || "bg-light text-dark border") + '">' + escapeHtml(batch.status) + "</span>";
    }

    function renderBatches(batches) {
        var list = root.querySelector("[data-am-print-batch-log-list]");
        if (!batches || !batches.length) {
            list.innerHTML = '<p class="text-muted small mb-0">No print batches yet.</p>';
            return;
        }
        var rows = batches.map(function (b) {
            var source = escapeHtml(b.sourceLabel || b.source) + (b.isReprint ? ' <span class="badge bg-warning text-dark">Reprint</span>' : "");
            var group = "<div>" + escapeHtml(b.groupLabel || "(no group)") + "</div>"
                + (b.filterDescription ? '<div class="text-muted">' + escapeHtml(b.filterDescription) + "</div>" : "");
            var cleared = b.isCleared ? '<div class="text-muted">Cleared by ' + escapeHtml(b.clearedByName || "") + " " + escapeHtml(b.clearedAt || "") + "</div>" : "";
            var actions = '<button type="button" class="btn btn-sm btn-outline-secondary py-0 px-2" data-am-print-batch-view="' + b.id + '" aria-expanded="false" aria-controls="amPrintBatchAssets' + b.id + '">View assets</button>'
                + ' <button type="button" class="btn btn-sm btn-outline-primary py-0 px-2" data-am-print-batch-reprint="' + b.id + '">Reprint group</button>';
            if (b.canClear && !b.isCleared) {
                actions += ' <button type="button" class="btn btn-sm btn-outline-danger py-0 px-2" data-am-print-batch-clear="' + b.id + '">Clear</button>';
            }
            return '<tr data-am-print-batch-row="' + b.id + '"' + (b.isCleared ? ' class="text-muted"' : "") + ">"
                + "<td>#" + b.id + "</td>"
                + '<td class="text-nowrap">' + escapeHtml(b.createdAt) + "</td>"
                + "<td>" + escapeHtml(b.createdByName) + "</td>"
                + "<td>" + source + "</td>"
                + "<td>" + group + cleared + "</td>"
                + '<td class="text-nowrap">' + b.printedCount + " / " + b.assetCount + "</td>"
                + "<td>" + statusBadge(b) + "</td>"
                + '<td class="text-nowrap">' + actions + "</td>"
                + "</tr>";
        }).join("");
        list.innerHTML = '<div class="table-responsive"><table class="table table-sm table-hover small mb-0">'
            + "<thead><tr><th>Batch</th><th>Sent</th><th>By</th><th>Source</th><th>Group / filter</th><th>Printed</th><th>Status</th><th></th></tr></thead>"
            + "<tbody>" + rows + "</tbody></table></div>";
    }

    function load() {
        var showCleared = root.querySelector("[data-am-print-batch-log-show-cleared]");
        setStatus("Loading print batches...");
        return window.AmLabelPrintLog.get(root.getAttribute("data-batches-url"), {
            includeCleared: showCleared && showCleared.checked ? "true" : "false",
            take: 25
        }).then(function (body) {
            loaded = true;
            setStatus("");
            renderBatches(body.batches || []);
        }).catch(function (error) {
            setStatus(error && error.message ? error.message : "Could not load the print batch log.", "error");
        });
    }

    function renderAssets(batch) {
        var items = batch && batch.items ? batch.items : [];
        if (!items.length) {
            return '<p class="text-muted small mb-0">This batch has no assets.</p>';
        }
        var rows = items.map(function (item, index) {
            var name = escapeHtml(item.name || "") + (item.isActive === false ? ' <span class="badge bg-secondary">Inactive</span>' : "");
            return "<tr" + (item.isActive === false ? ' class="text-muted"' : "") + ">"
                + "<td>" + (index + 1) + "</td>"
                + '<td class="text-nowrap"><code>' + escapeHtml(item.tag || "") + "</code></td>"
                + "<td>" + name + "</td>"
                + "<td>" + escapeHtml(item.department || "") + "</td>"
                + '<td class="text-nowrap">' + (item.printCount || 0) + (item.lastPrintedAt ? ' <span class="text-muted">(' + escapeHtml(item.lastPrintedAt) + ")</span>" : "") + "</td>"
                + "</tr>";
        }).join("");
        return '<div class="small fw-semibold mb-1">Assets in batch #' + batch.id + " (" + items.length + ")</div>"
            + '<table class="table table-sm table-bordered small mb-0 bg-white" data-am-print-batch-assets-table>'
            + "<thead><tr><th>#</th><th>Asset tag</th><th>Name</th><th>Department</th><th>Printed</th></tr></thead>"
            + "<tbody>" + rows + "</tbody></table>";
    }

    function toggleAssets(batchId, button) {
        var row = root.querySelector('[data-am-print-batch-row="' + batchId + '"]');
        if (!row) {
            return;
        }
        var existing = root.querySelector('[data-am-print-batch-assets-row="' + batchId + '"]');
        if (existing) {
            existing.parentNode.removeChild(existing);
            if (button) {
                button.setAttribute("aria-expanded", "false");
                button.textContent = "View assets";
            }
            return;
        }
        var detail = document.createElement("tr");
        detail.setAttribute("data-am-print-batch-assets-row", batchId);
        detail.id = "amPrintBatchAssets" + batchId;
        detail.className = "am-print-batch-assets";
        detail.innerHTML = '<td colspan="8" class="bg-light"><span class="text-muted small">Loading assets...</span></td>';
        row.parentNode.insertBefore(detail, row.nextSibling);
        if (button) {
            button.setAttribute("aria-expanded", "true");
            button.textContent = "Hide assets";
        }
        window.AmLabelPrintLog.get(root.getAttribute("data-batch-assets-url"), { id: batchId }).then(function (body) {
            detail.firstChild.innerHTML = renderAssets(body.batch);
        }).catch(function (error) {
            var message = error && error.message && error.message.indexOf("HTTP 404") >= 0
                ? "Batch #" + batchId + " was not found."
                : (error && error.message ? error.message : "Could not load the batch's assets.");
            detail.firstChild.innerHTML = '<span class="text-danger small">' + escapeHtml(message) + "</span>";
        });
    }

    function reprint(batchId) {
        if (window.AmScanBatchPrint && window.AmScanBatchPrint.isPrinting && window.AmScanBatchPrint.isPrinting()) {
            setStatus("Wait for the current print run to finish first.", "error");
            return;
        }
        if (window.AmScanBatchPrint && window.AmScanBatchPrint.getQueue().length
            && !window.confirm("Replace the current print queue with the assets of batch #" + batchId + "?")) {
            return;
        }
        setStatus("Loading batch #" + batchId + "...");
        window.AmLabelPrintLog.get(root.getAttribute("data-batch-items-url"), { id: batchId }).then(function (body) {
            var batch = body.batch;
            if (!batch || !batch.items || !batch.items.length) {
                setStatus("Batch #" + batchId + " has no active assets left to print.", "error");
                return;
            }
            if (!window.AmScanBatchPrint || !window.AmScanBatchPrint.loadBatch(batch, { reprint: true })) {
                setStatus("The print queue is busy. Try again when printing has finished.", "error");
                return;
            }
            setStatus(batch.items.length + " label(s) from batch #" + batchId + " are in the queue as a reprint. Select Print queue to Zebra to print them.", "ok");
            var queuePanel = document.querySelector("[data-am-scan-batch-queue]");
            if (queuePanel && queuePanel.scrollIntoView) {
                queuePanel.scrollIntoView({ behavior: "smooth", block: "center" });
            }
        }).catch(function (error) {
            setStatus(error && error.message ? error.message : "Could not load the batch.", "error");
        });
    }

    function clearBatch(batchId, button) {
        if (!window.confirm("Clear batch #" + batchId + " from the print batch log?\n\nThe printed labels stay on each asset's audit trail.")) {
            return;
        }
        if (button) {
            button.disabled = true;
        }
        window.AmLabelPrintLog.post(root.getAttribute("data-clear-batch-url"), { id: batchId }, root).then(function (body) {
            setStatus(body.cleared ? "Batch #" + batchId + " cleared." : "Batch #" + batchId + " was already cleared.", "ok");
            return load().then(function () {
                setStatus(body.cleared ? "Batch #" + batchId + " cleared." : "Batch #" + batchId + " was already cleared.", "ok");
            });
        }).catch(function (error) {
            if (button) {
                button.disabled = false;
            }
            setStatus(error && error.message ? error.message : "Could not clear the batch.", "error");
        });
    }

    function init() {
        root = document.querySelector("[data-am-print-batch-log]");
        if (!root || !window.AmLabelPrintLog) {
            return;
        }
        var body = root.querySelector("[data-am-print-batch-log-body]");
        if (body) {
            body.addEventListener("shown.bs.collapse", function () {
                if (!loaded) {
                    load();
                }
            });
        }
        root.addEventListener("click", function (event) {
            var target = event.target && event.target.closest ? event.target.closest("[data-am-print-batch-view], [data-am-print-batch-reprint], [data-am-print-batch-clear], [data-am-print-batch-log-refresh]") : null;
            if (!target) {
                return;
            }
            if (target.hasAttribute("data-am-print-batch-log-refresh")) {
                load();
                return;
            }
            var viewId = target.getAttribute("data-am-print-batch-view");
            if (viewId) {
                toggleAssets(viewId, target);
                return;
            }
            var reprintId = target.getAttribute("data-am-print-batch-reprint");
            if (reprintId) {
                reprint(reprintId);
                return;
            }
            var clearId = target.getAttribute("data-am-print-batch-clear");
            if (clearId) {
                clearBatch(clearId, target);
            }
        });
        root.addEventListener("change", function (event) {
            if (event.target && event.target.hasAttribute && event.target.hasAttribute("data-am-print-batch-log-show-cleared")) {
                load();
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})(window);
