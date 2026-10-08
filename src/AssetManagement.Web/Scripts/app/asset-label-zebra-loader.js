(function (window, document) {
    'use strict';

    var loadPromise = null;

    function resolveScriptUrl() {
        if (window.AssetLabelZebraScriptUrl) {
            return window.AssetLabelZebraScriptUrl;
        }
        var tip = document.querySelector('script[data-am-zebra-loader]');
        if (tip && tip.getAttribute('data-zebra-src')) {
            return tip.getAttribute('data-zebra-src');
        }
        return null;
    }

    function ensureAssetLabelZebra() {
        if (window.AssetLabelZebra) {
            return Promise.resolve(window.AssetLabelZebra);
        }
        if (loadPromise) {
            return loadPromise;
        }

        var src = resolveScriptUrl();
        if (!src) {
            return Promise.reject(new Error('Zebra label helper URL is not configured.'));
        }

        loadPromise = new Promise(function (resolve, reject) {
            var script = document.createElement('script');
            script.src = src;
            script.async = true;
            script.setAttribute('data-am-asset-label-zebra', 'true');
            script.onload = function () {
                if (window.AssetLabelZebra) {
                    resolve(window.AssetLabelZebra);
                } else {
                    loadPromise = null;
                    reject(new Error('Zebra label helper loaded but AssetLabelZebra is unavailable.'));
                }
            };
            script.onerror = function () {
                loadPromise = null;
                reject(new Error('Failed to load the Zebra label helper. Check your network and try Print again.'));
            };
            (document.head || document.documentElement).appendChild(script);
        });

        return loadPromise;
    }

    window.AssetLabelZebraEnsure = ensureAssetLabelZebra;

    function onZebraPrintClick(event) {
        var button = event.target && event.target.closest
            ? event.target.closest('[data-am-asset-label-zebra-print]')
            : null;
        if (!button || button.getAttribute('data-am-zebra-bound') === 'true') {
            return;
        }
        if (window.AssetLabelZebra) {
            return;
        }

        event.preventDefault();
        event.stopPropagation();
        button.disabled = true;
        ensureAssetLabelZebra()
            .then(function (zebra) {
                var root = button.closest('.modal') || button.closest('.am-asset-label-sheet') || document;
                if (zebra && typeof zebra.initRoot === 'function') {
                    zebra.initRoot(root);
                }
                button.disabled = false;
                button.click();
            })
            .catch(function (err) {
                button.disabled = false;
                window.alert((err && err.message) ? err.message : 'Unable to load Zebra printing.');
            });
    }

    document.addEventListener('click', onZebraPrintClick, true);

    function warmOnPrintUi(root) {
        ensureAssetLabelZebra()
            .then(function (zebra) {
                if (zebra && typeof zebra.initRoot === 'function') {
                    zebra.initRoot(root || document);
                }
            })
            .catch(function () { /* click path shows errors */ });
    }

    window.AssetLabelZebraWarm = warmOnPrintUi;

    // Details "Print label" opens this modal — load helper then, not on page load.
    // Regression note: 6bd73ca already stopped pollPrinterStatus/getLocalDevices on initRoot;
    // this loader also keeps asset-label-zebra.js itself off the Details network waterfall until Print/Tag.
    document.addEventListener('DOMContentLoaded', function () {
        var modal = document.getElementById('assetQrLabelModal');
        if (modal) {
            modal.addEventListener('shown.bs.modal', function () {
                warmOnPrintUi(modal);
            });
        }
    });
})(window, document);