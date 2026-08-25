(function () {
    'use strict';

    document.addEventListener('DOMContentLoaded', function () {
        var button = document.getElementById('amDownloadRequisitionPdf');
        if (!button) {
            return;
        }

        button.addEventListener('click', function () {
            var url = button.getAttribute('data-url');
            if (!url) {
                window.alert('Download is not configured. Refresh the page and try again.');
                return;
            }

            if (!window.amDocumentPdf || !window.html2pdf) {
                window.alert('PDF engine not available.');
                return;
            }

            var originalLabel = button.textContent;
            button.disabled = true;
            button.textContent = 'Preparing PDF…';

            fetch(url, { credentials: 'same-origin' })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error('Unable to build requisition document (HTTP ' + response.status + ').');
                    }

                    return response.json();
                })
                .then(function (data) {
                    if (!data.success) {
                        throw new Error(data.message || 'Unable to build requisition document.');
                    }

                    return window.amDocumentPdf.renderPdfFromHtml(data.html, data.fileName);
                })
                .catch(function (err) {
                    window.alert(err && err.message ? err.message : 'Error creating PDF.');
                })
                .then(function () {
                    button.disabled = false;
                    button.textContent = originalLabel;
                });
        });
    });
})();
