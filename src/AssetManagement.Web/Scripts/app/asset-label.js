(function (window) {
    var QR_SIZE = 280;
    var CODE_TYPE_QR = "Qr";
    var CODE_TYPE_BARCODE = "Barcode";

    function getSelectedCodeType(root) {
        var scope = root || document;
        var selector = scope.querySelector("[data-am-asset-label-code-type]");
        if (!selector) {
            return CODE_TYPE_QR;
        }

        return selector.value === CODE_TYPE_BARCODE ? CODE_TYPE_BARCODE : CODE_TYPE_QR;
    }

    function getCodeTypeSelector(root) {
        return (root || document).querySelector("[data-am-asset-label-code-type]");
    }

    function updateHint(root, codeType) {
        var scope = root || document;
        var hint = scope.querySelector("[data-am-asset-label-hint]");
        if (!hint) {
            return;
        }

        if (codeType === CODE_TYPE_BARCODE) {
            hint.textContent = "Scan barcode to look up this asset tag";
            return;
        }

        hint.textContent = "Scan to view full asset details";
    }

    function renderQr(container) {
        if (!container || !window.QRCode) {
            return;
        }

        var scanUrl = container.getAttribute("data-scan-url");
        if (!scanUrl) {
            return;
        }

        container.innerHTML = "";
        container.classList.remove("am-asset-label-code--barcode");
        container.classList.add("am-asset-label-code--qr");
        new QRCode(container, {
            text: scanUrl,
            width: QR_SIZE,
            height: QR_SIZE,
            correctLevel: QRCode.CorrectLevel.M
        });
    }

    function renderBarcode(container) {
        if (!container || !window.JsBarcode) {
            return;
        }

        var payload = container.getAttribute("data-barcode-payload");
        if (!payload) {
            return;
        }

        container.innerHTML = "";
        container.classList.remove("am-asset-label-code--qr");
        container.classList.add("am-asset-label-code--barcode");
        var svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
        svg.setAttribute("class", "am-asset-label-barcode-svg");
        container.appendChild(svg);

        window.JsBarcode(svg, payload, {
            format: "CODE128",
            displayValue: true,
            fontSize: 16,
            height: 80,
            margin: 0,
            width: 2
        });
    }

    function renderCode(container, codeType) {
        if (!container) {
            return;
        }

        if (codeType === CODE_TYPE_BARCODE) {
            renderBarcode(container);
            return;
        }

        renderQr(container);
    }

    function renderAll(root) {
        var scope = root || document;
        var codeType = getSelectedCodeType(scope);
        var nodes = scope.querySelectorAll("[data-am-asset-label-code]");
        for (var i = 0; i < nodes.length; i++) {
            renderCode(nodes[i], codeType);
        }

        updateHint(scope, codeType);
    }

    function bindCodeTypeSelector(root) {
        var selector = getCodeTypeSelector(root);
        if (!selector || selector.getAttribute("data-am-asset-label-code-type-bound") === "true") {
            return;
        }

        selector.setAttribute("data-am-asset-label-code-type-bound", "true");
        selector.addEventListener("change", function () {
            renderAll(root);
        });
    }

    function printLabel() {
        document.body.classList.add("am-printing-asset-label");
        window.onafterprint = function () {
            document.body.classList.remove("am-printing-asset-label");
            window.onafterprint = null;
        };
        window.print();
    }

    function initModal(modalId) {
        var modal = document.getElementById(modalId);
        if (!modal) {
            return;
        }

        modal.addEventListener("shown.bs.modal", function () {
            bindCodeTypeSelector(modal);
            renderAll(modal);
        });

        var printButton = modal.querySelector("[data-am-asset-label-print]");
        if (printButton) {
            printButton.addEventListener("click", printLabel);
        }
    }

    function boot() {
        bindCodeTypeSelector(document);
        renderAll(document.querySelector(".am-asset-label-sheet:not(.am-asset-label-sheet--modal)"));
        initModal("assetQrLabelModal");
    }

    window.AssetLabel = {
        renderAll: renderAll,
        print: printLabel,
        initModal: initModal,
        getSelectedCodeType: getSelectedCodeType
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})(window);
