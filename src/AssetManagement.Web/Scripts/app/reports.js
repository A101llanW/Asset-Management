(function () {
    'use strict';

    var currentReportKey = null;
    var previewModal = null;

    function getConfig() {
        return window.amReportsConfig || {};
    }

    function getTemplateStorageKey() {
        return 'am.report.templates.' + (getConfig().userKey || 'anon');
    }

    function loadTemplates() {
        try {
            var raw = window.localStorage.getItem(getTemplateStorageKey());
            if (!raw) {
                return [];
            }
            var parsed = JSON.parse(raw);
            return Array.isArray(parsed) ? parsed : [];
        } catch (e) {
            return [];
        }
    }

    function persistTemplates(templates) {
        window.localStorage.setItem(getTemplateStorageKey(), JSON.stringify(templates));
    }

    function renderSavedTemplates() {
        var container = document.getElementById('amSavedTemplates');
        if (!container) {
            return;
        }

        var templates = loadTemplates();
        if (!templates.length) {
            container.innerHTML = '<p class="text-muted mb-0">Save filter combinations from any report to re-run them in one click.</p>';
            return;
        }

        container.innerHTML = '';
        templates.forEach(function (template) {
            var item = document.createElement('div');
            item.className = 'border rounded p-2 mb-2 bg-white d-flex justify-content-between align-items-start gap-2';
            item.innerHTML =
                '<div><strong>' + escapeHtml(template.name) + '</strong><br/>' +
                '<span class="text-muted">' + escapeHtml(template.reportType) + '</span></div>' +
                '<div class="btn-group btn-group-sm flex-shrink-0">' +
                '<button type="button" class="btn btn-outline-primary am-template-run" data-template-id="' + template.id + '">Run</button>' +
                '<button type="button" class="btn btn-outline-danger am-template-delete" data-template-id="' + template.id + '">&times;</button>' +
                '</div>';
            container.appendChild(item);
        });

        container.querySelectorAll('.am-template-run').forEach(function (btn) {
            btn.addEventListener('click', function () {
                runTemplate(btn.getAttribute('data-template-id'));
            });
        });

        container.querySelectorAll('.am-template-delete').forEach(function (btn) {
            btn.addEventListener('click', function () {
                deleteTemplate(btn.getAttribute('data-template-id'));
            });
        });
    }

    function escapeHtml(value) {
        return String(value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    function applyPayloadToForm(reportKey, payload) {
        toggleFilters(reportKey, true);

        // Older saved templates stored a single DepartmentId/CategoryId; both shapes are accepted.
        function setChecked(selector, values) {
            var box = document.querySelector(selector + '[data-report-key="' + reportKey + '"]');
            if (!box) {
                return;
            }
            var wanted = (values || []).map(String);
            box.querySelectorAll('input[type="checkbox"]').forEach(function (cb) {
                cb.checked = wanted.indexOf(cb.value) !== -1;
            });
        }

        function setValue(selector, value) {
            var el = document.querySelector(selector + '[data-report-key="' + reportKey + '"]');
            if (el && value !== undefined && value !== null && value !== '') {
                el.value = value;
            }
        }

        setValue('.am-report-period', payload.PeriodPreset);
        setValue('.am-report-from', payload.FromDate);
        setValue('.am-report-to', payload.ToDate);
        setChecked('.am-report-department', payload.DepartmentIds || (payload.DepartmentId ? [payload.DepartmentId] : []));
        setChecked('.am-report-category', payload.CategoryIds || (payload.CategoryId ? [payload.CategoryId] : []));
        setValue('.am-report-section', payload.Section);
        setValue('.am-report-status', payload.Status);
        setValue('.am-report-sort', payload.SortBy);
        setValue('.am-report-sort-direction', payload.SortDirection);

        var period = document.querySelector('.am-report-period[data-report-key="' + reportKey + '"]');
        if (period) {
            period.dispatchEvent(new Event('change'));
        }
    }

    function saveTemplate(reportKey) {
        var nameInput = document.querySelector('.am-report-template-name[data-report-key="' + reportKey + '"]');
        var name = nameInput ? nameInput.value.trim() : '';
        if (!name) {
            window.alert('Enter a template name first.');
            return;
        }

        var payload = collectPayload(reportKey);
        delete payload.__RequestVerificationToken;

        var templates = loadTemplates();
        templates.unshift({
            id: 'tpl_' + new Date().getTime(),
            name: name,
            reportType: reportKey,
            filters: payload,
            savedAt: new Date().toISOString()
        });

        if (templates.length > 20) {
            templates = templates.slice(0, 20);
        }

        persistTemplates(templates);
        if (nameInput) {
            nameInput.value = '';
        }
        renderSavedTemplates();
    }

    function deleteTemplate(templateId) {
        var templates = loadTemplates().filter(function (t) { return t.id !== templateId; });
        persistTemplates(templates);
        renderSavedTemplates();
    }

    function runTemplate(templateId) {
        var templates = loadTemplates();
        var template = null;
        for (var i = 0; i < templates.length; i++) {
            if (templates[i].id === templateId) {
                template = templates[i];
                break;
            }
        }

        if (!template) {
            return;
        }

        applyPayloadToForm(template.reportType, template.filters || {});
        previewReport(template.reportType);
    }

    function getToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function getDefinition(reportKey) {
        var defs = getConfig().definitions || [];
        for (var i = 0; i < defs.length; i++) {
            if (defs[i].Key === reportKey) {
                return defs[i];
            }
        }
        return null;
    }

    function checkedValues(selector, reportKey) {
        var box = document.querySelector(selector + '[data-report-key="' + reportKey + '"]');
        if (!box) {
            return [];
        }
        var values = [];
        box.querySelectorAll('input[type="checkbox"]:checked').forEach(function (cb) {
            values.push(cb.value);
        });
        return values;
    }

    function collectPayload(reportKey) {
        var def = getDefinition(reportKey) || {};
        var payload = {
            ReportType: reportKey,
            __RequestVerificationToken: getToken()
        };

        var period = document.querySelector('.am-report-period[data-report-key="' + reportKey + '"]');
        if (period) {
            payload.PeriodPreset = period.value;
        }

        var from = document.querySelector('.am-report-from[data-report-key="' + reportKey + '"]');
        var to = document.querySelector('.am-report-to[data-report-key="' + reportKey + '"]');
        if (from && from.value) {
            payload.FromDate = from.value;
        }
        if (to && to.value) {
            payload.ToDate = to.value;
        }

        var deptIds = checkedValues('.am-report-department', reportKey);
        if (deptIds.length) {
            payload.DepartmentIds = deptIds;
        }

        var section = document.querySelector('.am-report-section[data-report-key="' + reportKey + '"]');
        if (section && section.value) {
            payload.Section = section.value;
        }

        var catIds = checkedValues('.am-report-category', reportKey);
        if (catIds.length) {
            payload.CategoryIds = catIds;
        }

        var status = document.querySelector('.am-report-status[data-report-key="' + reportKey + '"]');
        if (status && status.value) {
            payload.Status = status.value;
        }

        var sort = document.querySelector('.am-report-sort[data-report-key="' + reportKey + '"]');
        if (sort && sort.value) {
            payload.SortBy = sort.value;
        }

        var sortDir = document.querySelector('.am-report-sort-direction[data-report-key="' + reportKey + '"]');
        if (sortDir && sortDir.value) {
            payload.SortDirection = sortDir.value;
        }

        if (!def.SupportsDateRange && !payload.PeriodPreset) {
            payload.PeriodPreset = 'last-3-months';
        }

        return payload;
    }

    // ---- Loading modal lifecycle -------------------------------------------------------------
    // Bootstrap 5.3 ignores hide() while a modal is still fading in (and show() while it fades
    // out). A fast Preview response (< ~450ms) used to call hide() mid fade-in, leaving the
    // loading modal and its backdrop on screen forever. We track the lifecycle, only act on
    // shown/hidden events, and run follow-up work (open preview, alert, download) only after the
    // loading modal and its backdrop are fully gone.
    var loadingState = 'hidden'; // hidden | showing | shown | hiding
    var loadingWanted = false;
    var loadingPendingShow = false;
    var loadingAfterHide = [];
    var loadingWatchdog = null;
    var previewAfterHide = [];
    var busy = false;

    function hasBootstrapModal() {
        return !!(window.bootstrap && window.bootstrap.Modal);
    }

    function runQueue(queue) {
        queue.forEach(function (fn) {
            try {
                fn();
            } catch (e) {
                if (window.console && window.console.error) {
                    window.console.error(e);
                }
            }
        });
    }

    // Remove leftovers (stray .modal-backdrop, body.modal-open, scroll lock) once no modal is open.
    function cleanupModalArtifacts() {
        if (document.querySelector('.modal.show')) {
            return;
        }
        document.querySelectorAll('.modal-backdrop').forEach(function (bd) {
            if (bd.parentNode) {
                bd.parentNode.removeChild(bd);
            }
        });
        document.body.classList.remove('modal-open');
        document.body.style.removeProperty('overflow');
        document.body.style.removeProperty('padding-right');
    }

    function getLoadingEl() {
        return document.getElementById('amReportLoadingModal');
    }

    function flushLoadingQueue() {
        if (loadingWatchdog) {
            window.clearTimeout(loadingWatchdog);
            loadingWatchdog = null;
        }
        var queue = loadingAfterHide;
        loadingAfterHide = [];
        runQueue(queue);
    }

    function getLoadingModal() {
        var el = getLoadingEl();
        if (!el || !hasBootstrapModal()) {
            return null;
        }
        if (!el.getAttribute('data-am-bound')) {
            el.setAttribute('data-am-bound', '1');
            el.addEventListener('shown.bs.modal', function () {
                loadingState = 'shown';
                if (!loadingWanted) {
                    requestLoadingHide();
                }
            });
            el.addEventListener('hide.bs.modal', function () {
                loadingState = 'hiding';
            });
            el.addEventListener('hidden.bs.modal', function () {
                loadingState = 'hidden';
                cleanupModalArtifacts();
                if (loadingPendingShow) {
                    loadingPendingShow = false;
                    startLoadingShow();
                    return;
                }
                loadingWanted = false;
                flushLoadingQueue();
            });
        }
        // Static backdrop: a click outside does not close it; Esc still can (keyboard default).
        return window.bootstrap.Modal.getOrCreateInstance(el, { backdrop: 'static' });
    }

    function startLoadingShow() {
        var modal = getLoadingModal();
        if (!modal) {
            loadingState = 'hidden';
            return;
        }
        loadingState = 'showing';
        modal.show();
    }

    function requestLoadingHide() {
        var modal = getLoadingModal();
        if (!modal) {
            loadingState = 'hidden';
            flushLoadingQueue();
            return;
        }
        loadingState = 'hiding';
        modal.hide();
    }

    // Last resort if Bootstrap never fires shown/hidden: tear the loading modal down by hand.
    function forceHideLoading() {
        var el = getLoadingEl();
        if (el) {
            var inst = hasBootstrapModal() ? window.bootstrap.Modal.getInstance(el) : null;
            if (inst) {
                try {
                    inst.dispose();
                } catch (e) {
                    // ignore
                }
            }
            el.classList.remove('show');
            el.style.display = 'none';
            el.setAttribute('aria-hidden', 'true');
            el.removeAttribute('aria-modal');
            el.removeAttribute('role');
        }
        loadingState = 'hidden';
        loadingPendingShow = false;
        cleanupModalArtifacts();
        flushLoadingQueue();
    }

    function armLoadingWatchdog() {
        if (loadingWatchdog) {
            window.clearTimeout(loadingWatchdog);
        }
        loadingWatchdog = window.setTimeout(function () {
            loadingWatchdog = null;
            if (!loadingWanted && loadingState !== 'hidden') {
                forceHideLoading();
            }
        }, 4000);
    }

    function showLoading() {
        loadingWanted = true;
        if (loadingState === 'hidden') {
            startLoadingShow();
        } else if (loadingState === 'hiding') {
            loadingPendingShow = true;
        }
        // 'showing' / 'shown': already on screen or on its way.
    }

    // Hide the loading modal; afterHidden runs once it (and its backdrop) are fully gone.
    function hideLoading(afterHidden) {
        loadingWanted = false;
        loadingPendingShow = false;
        if (typeof afterHidden === 'function') {
            loadingAfterHide.push(afterHidden);
        }
        if (loadingState === 'hidden') {
            flushLoadingQueue();
            return;
        }
        if (loadingState === 'shown') {
            requestLoadingHide();
        }
        // 'showing': the shown handler hides it. 'hiding': the hidden handler flushes the queue.
        armLoadingWatchdog();
    }

    function finishWith(fn) {
        return function () {
            busy = false;
            if (typeof fn === 'function') {
                fn();
            }
        };
    }

    function alertLater(message) {
        // Alert after the modal is gone so a blocking dialog never freezes a half-faded backdrop.
        return function () {
            window.alert(message);
        };
    }

    function renderPdfFromHtml(html, fileName) {
        if (!window.amDocumentPdf) {
            window.alert('PDF engine not available.');
            return Promise.reject(new Error('PDF engine not available.'));
        }

        return window.amDocumentPdf.renderPdfFromHtml(html, fileName);
    }

    // ---- Preview modal -------------------------------------------------------------------------
    function getPreviewModal() {
        var el = document.getElementById('amReportPreviewModal');
        if (!el || !hasBootstrapModal()) {
            return null;
        }
        if (!el.getAttribute('data-am-bound')) {
            el.setAttribute('data-am-bound', '1');
            el.addEventListener('hidden.bs.modal', function () {
                cleanupModalArtifacts();
                var queue = previewAfterHide;
                previewAfterHide = [];
                runQueue(queue);
            });
        }
        previewModal = window.bootstrap.Modal.getOrCreateInstance(el);
        return previewModal;
    }

    // On-screen preview shows at most this many body rows per table; laying out thousands of rows
    // (e.g. 2,343 in depreciation) froze the modal for ~10 s. PDF/CSV still use the full report.
    var PREVIEW_ROW_LIMIT = 200;
    var previewFullHtml = '';

    function capPreviewHtml(html) {
        var result = { html: html || '', shown: 0, total: 0 };
        if (!html || typeof document.createElement('template').content === 'undefined') {
            return result;
        }
        var template = document.createElement('template');
        template.innerHTML = html;
        var capped = false;
        Array.prototype.forEach.call(template.content.querySelectorAll('table'), function (table) {
            var rows = [];
            Array.prototype.forEach.call(table.tBodies, function (body) {
                Array.prototype.push.apply(rows, body.rows);
            });
            result.total += rows.length;
            result.shown += Math.min(rows.length, PREVIEW_ROW_LIMIT);
            for (var i = PREVIEW_ROW_LIMIT; i < rows.length; i++) {
                rows[i].parentNode.removeChild(rows[i]);
                capped = true;
            }
        });
        if (capped) {
            result.html = template.innerHTML;
        }
        return result;
    }

    function showPreview(html, title, rowCount) {
        previewFullHtml = html || '';
        var capped = capPreviewHtml(previewFullHtml);
        var meta = (rowCount || 0) + ' data rows';
        if (capped.total > capped.shown) {
            meta = 'Showing ' + capped.shown + ' of ' + capped.total + ' rows. Download PDF or CSV for all rows.';
        }
        document.getElementById('amReportPreviewTitle').textContent = title || 'Report preview';
        document.getElementById('amReportPreviewMeta').textContent = meta;
        document.getElementById('amReportPreviewContent').innerHTML = capped.html;

        var modal = getPreviewModal();
        if (modal) {
            modal.show();
        }
    }

    // Close the preview modal (if open) and run next only after it has fully hidden.
    function hidePreviewThen(next) {
        var el = document.getElementById('amReportPreviewModal');
        var modal = getPreviewModal();
        if (!el || !modal || !el.classList.contains('show')) {
            next();
            return;
        }
        var done = false;
        var once = function () {
            if (!done) {
                done = true;
                next();
            }
        };
        previewAfterHide.push(once);
        modal.hide();
        window.setTimeout(function () {
            if (!done) {
                previewAfterHide = previewAfterHide.filter(function (fn) { return fn !== once; });
                cleanupModalArtifacts();
                once();
            }
        }, 2000);
    }

    function postForm(url, payload) {
        var body = new URLSearchParams();
        Object.keys(payload).forEach(function (key) {
            var value = payload[key];
            if (Array.isArray(value)) {
                // MVC binds repeated keys (DepartmentIds=1&DepartmentIds=2) to List<int>.
                value.forEach(function (item) {
                    body.append(key, item);
                });
            } else if (value !== undefined && value !== null && value !== '') {
                body.append(key, value);
            }
        });

        return fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8'
            },
            body: body.toString(),
            credentials: 'same-origin'
        });
    }

    function previewReport(reportKey) {
        var previewUrl = getConfig().previewUrl;
        if (!previewUrl) {
            window.alert('Report preview is not configured. Refresh the page and try again.');
            return;
        }
        if (busy) {
            return;
        }

        busy = true;
        currentReportKey = reportKey;
        showLoading();
        postForm(previewUrl, collectPayload(reportKey))
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Preview failed (HTTP ' + response.status + ').');
                }
                return response.json();
            })
            .then(function (data) {
                hideLoading(finishWith(function () {
                    if (data && data.success) {
                        showPreview(data.html, data.title, data.rowCount);
                    } else {
                        window.alert((data && data.message) || 'Unable to build preview.');
                    }
                }));
            })
            .catch(function (err) {
                hideLoading(finishWith(alertLater(err && err.message ? err.message : 'Error connecting to the server.')));
            });
    }

    function downloadCsv(reportKey) {
        var exportUrl = getConfig().exportUrl;
        if (!exportUrl) {
            window.alert('Report export is not configured. Refresh the page and try again.');
            return;
        }
        if (busy) {
            return;
        }

        busy = true;
        showLoading();
        postForm(exportUrl, collectPayload(reportKey))
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Export failed');
                }
                var disposition = response.headers.get('Content-Disposition') || '';
                var match = /filename=\"?([^\";]+)\"?/i.exec(disposition);
                var fileName = match ? match[1] : (reportKey + '.csv');
                return response.blob().then(function (blob) {
                    return { blob: blob, fileName: fileName };
                });
            })
            .then(function (result) {
                hideLoading(finishWith(function () {
                    var url = URL.createObjectURL(result.blob);
                    var link = document.createElement('a');
                    link.href = url;
                    link.download = result.fileName;
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    window.setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
                    addRecentReport(result.fileName, reportKey);
                }));
            })
            .catch(function () {
                hideLoading(finishWith(alertLater('Error downloading CSV.')));
            });
    }

    function downloadPdf(reportKey) {
        var previewUrl = getConfig().previewUrl;
        if (!previewUrl) {
            window.alert('Report preview is not configured. Refresh the page and try again.');
            return;
        }
        if (busy) {
            return;
        }

        busy = true;
        currentReportKey = reportKey;
        showLoading();
        postForm(previewUrl, collectPayload(reportKey))
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Report build failed (HTTP ' + response.status + ').');
                }
                return response.json();
            })
            .then(function (data) {
                if (!data || !data.success) {
                    hideLoading(finishWith(alertLater((data && data.message) || 'Unable to build report.')));
                    return;
                }

                var fileName = (reportKey || 'report') + '_' + new Date().getTime() + '.pdf';
                return renderPdfFromHtml(data.html, fileName)
                    .then(function () {
                        hideLoading(finishWith(function () {
                            addRecentReport(fileName, reportKey);
                        }));
                    });
            })
            .catch(function () {
                hideLoading(finishWith(alertLater('Error creating PDF.')));
            });
    }

    function downloadPdfFromPreview() {
        var content = document.getElementById('amReportPreviewContent');
        if (!content || !content.innerHTML) {
            window.alert('Open a report preview first.');
            return;
        }
        if (busy) {
            return;
        }

        busy = true;
        // Full report, not the row-capped on-screen copy.
        var html = previewFullHtml || content.innerHTML;
        var reportKey = currentReportKey;
        var fileName = (reportKey || 'report') + '_' + new Date().getTime() + '.pdf';
        // Close the preview first, then show loading: never transition two modals at once.
        hidePreviewThen(function () {
            showLoading();
            renderPdfFromHtml(html, fileName)
                .then(function () {
                    hideLoading(finishWith(function () {
                        addRecentReport(fileName, reportKey);
                    }));
                })
                .catch(function () {
                    hideLoading(finishWith(alertLater('Error creating PDF. Try preview again.')));
                });
        });
    }

    function addRecentReport(fileName, reportKey) {
        var container = document.getElementById('amRecentReports');
        if (!container) {
            return;
        }

        if (container.querySelector('.text-muted')) {
            container.innerHTML = '';
        }

        var item = document.createElement('div');
        item.className = 'border rounded p-2 mb-2 bg-white';
        item.innerHTML = '<strong>' + (reportKey || 'Report') + '</strong><br/><span class="text-muted">' + fileName + '</span>';
        container.insertBefore(item, container.firstChild);
    }

    function toggleFilters(reportKey, forceOpen) {
        var panel = document.getElementById('filters-' + reportKey);
        if (!panel) {
            return;
        }

        var isHidden = panel.classList.contains('d-none');
        if (!forceOpen) {
            document.querySelectorAll('.am-report-filters').forEach(function (el) {
                el.classList.add('d-none');
            });
        }
        if (isHidden || forceOpen) {
            panel.classList.remove('d-none');
        }
    }

    function bindPeriodToggle(select) {
        select.addEventListener('change', function () {
            var reportKey = select.getAttribute('data-report-key');
            var customPanels = document.querySelectorAll('.am-report-custom-dates[data-report-key="' + reportKey + '"]');
            customPanels.forEach(function (el) {
                if (select.value === 'custom') {
                    el.classList.remove('d-none');
                } else {
                    el.classList.add('d-none');
                }
            });
        });
    }

    document.addEventListener('DOMContentLoaded', function () {
        document.querySelectorAll('.am-report-period').forEach(bindPeriodToggle);

        document.querySelectorAll('.am-report-check-all, .am-report-check-none').forEach(function (link) {
            link.addEventListener('click', function (e) {
                e.preventDefault();
                var check = link.classList.contains('am-report-check-all');
                var box = document.querySelector('.' + link.getAttribute('data-target')
                    + '[data-report-key="' + link.getAttribute('data-report-key') + '"]');
                if (box) {
                    box.querySelectorAll('input[type="checkbox"]').forEach(function (cb) {
                        cb.checked = check;
                    });
                }
            });
        });

        document.querySelectorAll('.am-report-configure').forEach(function (btn) {
            btn.addEventListener('click', function () {
                toggleFilters(btn.getAttribute('data-report-key'));
            });
        });

        document.querySelectorAll('.am-report-preview, .am-report-preview-quick').forEach(function (btn) {
            btn.addEventListener('click', function () {
                var key = btn.getAttribute('data-report-key');
                toggleFilters(key);
                var panel = document.getElementById('filters-' + key);
                if (panel && panel.classList.contains('d-none')) {
                    panel.classList.remove('d-none');
                }
                previewReport(key);
            });
        });

        document.querySelectorAll('.am-report-download-csv').forEach(function (btn) {
            btn.addEventListener('click', function () {
                downloadCsv(btn.getAttribute('data-report-key'));
            });
        });

        document.querySelectorAll('.am-report-download-pdf').forEach(function (btn) {
            btn.addEventListener('click', function () {
                downloadPdf(btn.getAttribute('data-report-key'));
            });
        });

        document.querySelectorAll('.am-report-save-template').forEach(function (btn) {
            btn.addEventListener('click', function () {
                saveTemplate(btn.getAttribute('data-report-key'));
            });
        });

        renderSavedTemplates();

        var pdfBtn = document.getElementById('amReportPreviewDownloadPdf');
        if (pdfBtn) {
            pdfBtn.addEventListener('click', downloadPdfFromPreview);
        }
    });
})();
