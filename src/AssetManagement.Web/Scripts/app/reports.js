(function () {
    'use strict';

    var currentReportKey = null;
    var loadingModal = null;
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

        function setValue(selector, value) {
            var el = document.querySelector(selector + '[data-report-key="' + reportKey + '"]');
            if (el && value !== undefined && value !== null && value !== '') {
                el.value = value;
            }
        }

        setValue('.am-report-period', payload.PeriodPreset);
        setValue('.am-report-from', payload.FromDate);
        setValue('.am-report-to', payload.ToDate);
        setValue('.am-report-department', payload.DepartmentId);
        setValue('.am-report-category', payload.CategoryId);
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

        var dept = document.querySelector('.am-report-department[data-report-key="' + reportKey + '"]');
        if (dept && dept.value) {
            payload.DepartmentId = dept.value;
        }

        var cat = document.querySelector('.am-report-category[data-report-key="' + reportKey + '"]');
        if (cat && cat.value) {
            payload.CategoryId = cat.value;
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

    function showLoading() {
        if (window.bootstrap && window.bootstrap.Modal) {
            loadingModal = window.bootstrap.Modal.getOrCreateInstance(document.getElementById('amReportLoadingModal'));
            loadingModal.show();
        }
    }

    function hideLoading() {
        if (loadingModal) {
            loadingModal.hide();
        }
    }

    function showPreview(html, title, rowCount) {
        document.getElementById('amReportPreviewTitle').textContent = title || 'Report preview';
        document.getElementById('amReportPreviewMeta').textContent = (rowCount || 0) + ' data rows';
        document.getElementById('amReportPreviewContent').innerHTML = html;

        if (window.bootstrap && window.bootstrap.Modal) {
            previewModal = window.bootstrap.Modal.getOrCreateInstance(document.getElementById('amReportPreviewModal'));
            previewModal.show();
        }
    }

    function postForm(url, payload) {
        var body = new URLSearchParams();
        Object.keys(payload).forEach(function (key) {
            if (payload[key] !== undefined && payload[key] !== null && payload[key] !== '') {
                body.append(key, payload[key]);
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
        currentReportKey = reportKey;
        var previewUrl = getConfig().previewUrl;
        if (!previewUrl) {
            window.alert('Report preview is not configured. Refresh the page and try again.');
            return;
        }

        showLoading();
        postForm(previewUrl, collectPayload(reportKey))
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Preview failed (HTTP ' + response.status + ').');
                }
                return response.json();
            })
            .then(function (data) {
                hideLoading();
                if (data.success) {
                    showPreview(data.html, data.title, data.rowCount);
                } else {
                    window.alert(data.message || 'Unable to build preview.');
                }
            })
            .catch(function (err) {
                hideLoading();
                window.alert(err && err.message ? err.message : 'Error connecting to the server.');
            });
    }

    function downloadCsv(reportKey) {
        var exportUrl = getConfig().exportUrl;
        if (!exportUrl) {
            window.alert('Report export is not configured. Refresh the page and try again.');
            return;
        }

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
                hideLoading();
                var link = document.createElement('a');
                link.href = URL.createObjectURL(result.blob);
                link.download = result.fileName;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                addRecentReport(result.fileName, reportKey);
            })
            .catch(function () {
                hideLoading();
                window.alert('Error downloading CSV.');
            });
    }

    function downloadPdf(reportKey) {
        currentReportKey = reportKey;
        var previewUrl = getConfig().previewUrl;
        if (!previewUrl) {
            window.alert('Report preview is not configured. Refresh the page and try again.');
            return;
        }

        showLoading();
        postForm(previewUrl, collectPayload(reportKey))
            .then(function (response) {
                if (!response.ok) {
                    throw new Error('Report build failed (HTTP ' + response.status + ').');
                }
                return response.json();
            })
            .then(function (data) {
                if (!data.success) {
                    hideLoading();
                    window.alert(data.message || 'Unable to build report.');
                    return;
                }

                document.getElementById('amReportPreviewContent').innerHTML = data.html;
                var content = document.getElementById('amReportPreviewContent');
                if (!content || !window.html2pdf) {
                    hideLoading();
                    window.alert('PDF engine not available.');
                    return;
                }

                var fileName = (reportKey || 'report') + '_' + new Date().getTime() + '.pdf';
                var opt = {
                    margin: 0.5,
                    filename: fileName,
                    image: { type: 'jpeg', quality: 0.98 },
                    html2canvas: { scale: 2, useCORS: true },
                    jsPDF: { unit: 'in', format: 'a4', orientation: 'portrait' }
                };

                return window.html2pdf().set(opt).from(content).save()
                    .then(function () {
                        hideLoading();
                        addRecentReport(fileName, reportKey);
                    });
            })
            .catch(function () {
                hideLoading();
                window.alert('Error creating PDF.');
            });
    }
    function downloadPdfFromPreview() {
        var content = document.getElementById('amReportPreviewContent');
        if (!content || !window.html2pdf) {
            window.alert('PDF engine not available.');
            return;
        }

        showLoading();
        if (previewModal) {
            previewModal.hide();
        }

        var fileName = (currentReportKey || 'report') + '_' + new Date().getTime() + '.pdf';
        var opt = {
            margin: 0.5,
            filename: fileName,
            image: { type: 'jpeg', quality: 0.98 },
            html2canvas: { scale: 2, useCORS: true },
            jsPDF: { unit: 'in', format: 'a4', orientation: 'portrait' }
        };

        window.html2pdf().set(opt).from(content).save()
            .then(function () {
                hideLoading();
                addRecentReport(fileName, currentReportKey);
            })
            .catch(function () {
                hideLoading();
                window.alert('Error creating PDF. Try preview again.');
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
