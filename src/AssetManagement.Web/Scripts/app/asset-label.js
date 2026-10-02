(function (window) {
    'use strict';

    var QR_SIZE = 72;
    var DOTS_PER_MM = 8;
    var CODE_TYPE_QR = 'Qr';
    var CODE_TYPE_BARCODE = 'Barcode';

    function getSelectedCodeType(root) {
        var scope = root || document;
        var selector = scope.querySelector('[data-am-asset-label-code-type]');
        if (!selector) {
            return CODE_TYPE_QR;
        }

        return selector.value === CODE_TYPE_BARCODE ? CODE_TYPE_BARCODE : CODE_TYPE_QR;
    }

    function getCodeTypeSelector(root) {
        return (root || document).querySelector('[data-am-asset-label-code-type]');
    }

    function fetchJson(url) {
        return fetch(url, { credentials: 'same-origin' }).then(function (response) {
            if (!response.ok) {
                throw new Error('Unable to load label print configuration.');
            }
            return response.json();
        });
    }

    function getShellData(shell) {
        return {
            assetTag: shell.getAttribute('data-asset-tag') || '',
            assetName: shell.getAttribute('data-asset-name') || '',
            departmentName: shell.getAttribute('data-department-name') || '',
            serialNumber: shell.getAttribute('data-serial-number') || '',
            scanUrl: shell.getAttribute('data-scan-url') || '',
            barcodePayload: shell.getAttribute('data-barcode-payload') || shell.getAttribute('data-asset-tag') || ''
        };
    }

    function getTextForElement(elementId, data) {
        if (elementId === 'AssetTag') {
            return data.assetTag;
        }

        if (elementId === 'AssetName') {
            return data.assetName;
        }

        if (elementId === 'DepartmentName') {
            return data.departmentName;
        }

        if (elementId === 'SerialNumber') {
            if (!data.serialNumber) {
                return '';
            }

            return data.serialNumber.indexOf('S/N:') === 0 ? data.serialNumber : 'S/N: ' + data.serialNumber;
        }

        return '';
    }

    function estimateQrSizeMm(magnification) {
        var mag = parseInt(magnification, 10);
        if (isNaN(mag) || mag <= 0) {
            mag = 3;
        }

        return (25 * mag + 30) / DOTS_PER_MM;
    }

    function fontHeightToMm(fontHeightDots, fallbackMm) {
        if (!fontHeightDots) {
            return fallbackMm || 2.5;
        }

        return fontHeightDots / DOTS_PER_MM;
    }

    function renderQr(container, scanUrl, sizePx) {
        if (!container || !window.QRCode || !scanUrl) {
            return;
        }

        container.innerHTML = '';
        new window.QRCode(container, {
            text: scanUrl,
            width: sizePx || QR_SIZE,
            height: sizePx || QR_SIZE,
            correctLevel: window.QRCode.CorrectLevel.M
        });
    }

    function renderBarcode(container, payload) {
        if (!container || !window.JsBarcode || !payload) {
            return;
        }

        container.innerHTML = '';
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('class', 'am-asset-label-barcode-svg');
        container.appendChild(svg);
        window.JsBarcode(svg, payload, {
            format: 'CODE128',
            displayValue: true,
            fontSize: 12,
            height: 48,
            margin: 0,
            width: 1.5
        });
    }

    function renderDesignedLayout(shell, config, codeType) {
        var root = shell.querySelector('[data-am-asset-label-root]');
        if (!root || !config || !config.layoutDesign || !config.layoutDesign.Elements) {
            return;
        }

        var data = getShellData(shell);
        var widthMm = config.labelWidthMm || 76;
        var heightMm = config.labelHeightMm || 37;
        var qrSizeMm = estimateQrSizeMm(config.qrMagnification || 3);
        root.innerHTML = '';
        root.className = 'am-asset-label am-asset-label--layout am-asset-label--designed';
        root.style.width = widthMm + 'mm';
        root.style.height = heightMm + 'mm';
        root.style.setProperty('--am-label-qr-size', qrSizeMm + 'mm');

        for (var i = 0; i < config.layoutDesign.Elements.length; i++) {
            var element = config.layoutDesign.Elements[i];
            if (!element || !element.Enabled) {
                continue;
            }

            var field = document.createElement('div');
            field.className = 'am-asset-label-field';
            field.style.left = element.XMm + 'mm';
            field.style.top = element.YMm + 'mm';

            if (element.Id === 'ScanCode') {
                field.className += ' am-asset-label-field--code';
                var codeHost = document.createElement('div');
                codeHost.className = 'am-asset-label-code-host';
                field.appendChild(codeHost);
                if (codeType === CODE_TYPE_BARCODE) {
                    renderBarcode(codeHost, data.barcodePayload);
                } else {
                    renderQr(codeHost, data.scanUrl);
                }
            } else {
                var text = getTextForElement(element.Id, data);
                if (!text) {
                    continue;
                }

                field.textContent = text;
                field.style.fontSize = fontHeightToMm(element.FontHeight, element.Id === 'AssetTag' ? 3 : 2.25) + 'mm';
                field.style.lineHeight = '1.15';
                if (element.Id === 'AssetTag') {
                    field.className += ' am-asset-label-field--tag';
                }
            }

            root.appendChild(field);
        }
    }

    function renderLegacyLayout(shell, codeType) {
        var data = getShellData(shell);
        var root = shell.querySelector('[data-am-asset-label-root]');
        if (!root) {
            return;
        }

        root.className = 'am-asset-label';
        root.innerHTML =
            '<div class="am-asset-label-code" data-am-asset-label-code="true" aria-hidden="true"></div>' +
            '<div class="am-asset-label-meta">' +
            '<div class="am-asset-label-tag"></div>' +
            '<div class="am-asset-label-name"></div>' +
            '<div class="am-asset-label-dept"></div>' +
            '<div class="am-asset-label-serial"></div>' +
            '<div class="am-asset-label-hint" data-am-asset-label-hint="true">Scan to view full asset details</div>' +
            '</div>';

        root.querySelector('.am-asset-label-tag').textContent = data.assetTag;
        root.querySelector('.am-asset-label-name').textContent = data.assetName;

        var dept = root.querySelector('.am-asset-label-dept');
        if (data.departmentName) {
            dept.textContent = data.departmentName;
        } else {
            dept.style.display = 'none';
        }

        var serial = root.querySelector('.am-asset-label-serial');
        if (data.serialNumber) {
            serial.textContent = data.serialNumber.indexOf('S/N:') === 0 ? data.serialNumber : 'S/N: ' + data.serialNumber;
        } else {
            serial.style.display = 'none';
        }

        var codeNode = root.querySelector('[data-am-asset-label-code]');
        if (codeType === CODE_TYPE_BARCODE) {
            renderBarcode(codeNode, data.barcodePayload);
        } else {
            renderQr(codeNode, data.scanUrl);
        }

        updateHint(shell, codeType);
    }

    function updateHint(root, codeType) {
        var hint = (root || document).querySelector('[data-am-asset-label-hint]');
        if (!hint) {
            return;
        }

        hint.textContent = codeType === CODE_TYPE_BARCODE
            ? 'Scan barcode to look up this asset tag'
            : 'Scan to view full asset details';
    }

    function renderShell(shell, config, codeType) {
        if (config && config.usesCustomLayout && config.layoutDesign) {
            renderDesignedLayout(shell, config, codeType);
            return;
        }

        renderLegacyLayout(shell, codeType);
    }

    function renderScope(root, config) {
        var scope = root || document;
        var codeType = getSelectedCodeType(scope);
        var shells = scope.querySelectorAll('[data-am-asset-label-shell]');
        for (var i = 0; i < shells.length; i++) {
            renderShell(shells[i], config, codeType);
        }
    }

    function bindCodeTypeSelector(root, config) {
        var selector = getCodeTypeSelector(root);
        if (!selector || selector.getAttribute('data-am-asset-label-code-type-bound') === 'true') {
            return;
        }

        selector.setAttribute('data-am-asset-label-code-type-bound', 'true');
        selector.addEventListener('change', function () {
            renderScope(root, config);
        });
    }

    function initSheet(sheet) {
        if (!sheet) {
            return;
        }

        var configUrl = sheet.getAttribute('data-am-asset-label-config-url');
        if (!configUrl) {
            renderScope(sheet, null);
            bindCodeTypeSelector(sheet, null);
            return;
        }

        fetchJson(configUrl).then(function (config) {
            renderScope(sheet, config);
            bindCodeTypeSelector(sheet, config);
        }).catch(function () {
            renderScope(sheet, null);
            bindCodeTypeSelector(sheet, null);
        });
    }

    function printLabel() {
        document.body.classList.add('am-printing-asset-label');
        window.onafterprint = function () {
            document.body.classList.remove('am-printing-asset-label');
            window.onafterprint = null;
        };
        window.print();
    }

    function initModal(modalId) {
        var modal = document.getElementById(modalId);
        if (!modal) {
            return;
        }

        modal.addEventListener('shown.bs.modal', function () {
            initSheet(modal.querySelector('.am-asset-label-sheet'));
        });

        var printButton = modal.querySelector('[data-am-asset-label-print]');
        if (printButton) {
            printButton.addEventListener('click', printLabel);
        }
    }

    function boot() {
        var sheet = document.querySelector('.am-asset-label-sheet:not(.am-asset-label-sheet--modal)');
        initSheet(sheet);
        initModal('assetQrLabelModal');
    }

    window.AssetLabel = {
        renderScope: renderScope,
        print: printLabel,
        initModal: initModal,
        initSheet: initSheet,
        getSelectedCodeType: getSelectedCodeType
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(window);
