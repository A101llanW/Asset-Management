(function (window) {
    var QR_SIZE = 280;

    function renderQr(container) {
        if (!container || !window.QRCode) {
            return;
        }

        var scanUrl = container.getAttribute("data-scan-url");
        if (!scanUrl) {
            return;
        }

        container.innerHTML = "";
        new QRCode(container, {
            text: scanUrl,
            width: QR_SIZE,
            height: QR_SIZE,
            correctLevel: QRCode.CorrectLevel.M
        });
    }

    function renderAll(root) {
        var scope = root || document;
        var nodes = scope.querySelectorAll("[data-am-asset-label-qr]");
        for (var i = 0; i < nodes.length; i++) {
            renderQr(nodes[i]);
        }
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
            renderAll(modal);
        });

        var printButton = modal.querySelector("[data-am-asset-label-print]");
        if (printButton) {
            printButton.addEventListener("click", printLabel);
        }
    }

    function boot() {
        renderAll(document.querySelector(".am-asset-label-sheet:not(.am-asset-label-sheet--modal)"));
        initModal("assetQrLabelModal");
    }

    window.AssetLabel = {
        renderAll: renderAll,
        print: printLabel,
        initModal: initModal
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})(window);
