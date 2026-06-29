(function (window) {
    'use strict';

    function ensurePdfRoot() {
        var pdfRoot = document.getElementById('amReportPdfSource');
        if (!pdfRoot) {
            pdfRoot = document.createElement('div');
            pdfRoot.id = 'amReportPdfSource';
            pdfRoot.className = 'am-report-pdf-source';
            pdfRoot.setAttribute('aria-hidden', 'true');
            document.body.appendChild(pdfRoot);
        }

        return pdfRoot;
    }

    function getPdfOptions(fileName) {
        return {
            margin: [0.35, 0.35, 0.35, 0.35],
            filename: fileName,
            image: { type: 'jpeg', quality: 0.98 },
            html2canvas: {
                scale: 2,
                useCORS: true,
                scrollY: 0,
                scrollX: 0,
                backgroundColor: '#ffffff',
                logging: false,
                width: 794
            },
            jsPDF: { unit: 'in', format: 'a4', orientation: 'portrait' },
            pagebreak: { mode: ['css', 'legacy'] }
        };
    }

    function waitForPaint() {
        return new Promise(function (resolve) {
            window.requestAnimationFrame(function () {
                window.requestAnimationFrame(function () {
                    window.setTimeout(resolve, 200);
                });
            });
        });
    }

    function attachEmbeddedStyles(pdfRoot) {
        var styleEl = pdfRoot.querySelector('style');
        if (!styleEl) {
            return null;
        }

        var styleClone = styleEl.cloneNode(true);
        styleClone.setAttribute('data-am-pdf-style', 'true');
        document.head.appendChild(styleClone);
        return styleClone;
    }

    function detachEmbeddedStyles(styleClone) {
        if (styleClone && styleClone.parentNode) {
            styleClone.parentNode.removeChild(styleClone);
        }
    }

    function renderPdfFromHtml(html, fileName) {
        if (!window.html2pdf) {
            window.alert('PDF engine not available.');
            return Promise.reject(new Error('PDF engine not available.'));
        }

        var pdfRoot = ensurePdfRoot();
        pdfRoot.innerHTML = html;
        var styleClone = attachEmbeddedStyles(pdfRoot);
        var target = pdfRoot.querySelector('.report-frame') || pdfRoot;

        return waitForPaint()
            .then(function () {
                return window.html2pdf().set(getPdfOptions(fileName)).from(target).save();
            })
            .then(function () {
                pdfRoot.innerHTML = '';
                detachEmbeddedStyles(styleClone);
            })
            .catch(function (err) {
                pdfRoot.innerHTML = '';
                detachEmbeddedStyles(styleClone);
                throw err;
            });
    }

    window.amDocumentPdf = {
        renderPdfFromHtml: renderPdfFromHtml
    };
})(window);
