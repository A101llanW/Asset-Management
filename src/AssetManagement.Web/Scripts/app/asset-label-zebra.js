(function (window) {
    'use strict';

    var BROWSER_PRINT_SDK_FILE = 'BrowserPrint-2.0.0.75.min.js';
    var MODE_ZEBRA = 'ZebraBrowserPrint';
    var LOAD_TIMEOUT_MS = 10000;
    var STATUS_CHECKING = 'checking';
    var STATUS_READY = 'ready';
    var STATUS_WARNING = 'warning';
    var STATUS_ERROR = 'error';

    function getConfigUrl(root) {
        var scope = root || document;
        var node = scope.querySelector('[data-am-asset-label-zebra-print]');
        return node ? node.getAttribute('data-config-url') : null;
    }

    function getSelectedCodeType(root) {
        if (window.AssetLabel && typeof window.AssetLabel.getSelectedCodeType === 'function') {
            return window.AssetLabel.getSelectedCodeType(root);
        }

        return 'Qr';
    }

    function buildZplUrl(baseUrl, codeType) {
        if (!baseUrl) {
            return baseUrl;
        }

        var separator = baseUrl.indexOf('?') >= 0 ? '&' : '?';
        return baseUrl + separator + 'codeType=' + encodeURIComponent(codeType || 'Qr');
    }

    function getStatusBadges(root) {
        return (root || document).querySelectorAll('[data-am-zebra-printer-status]');
    }

    function setStatusBadges(badges, status, message) {
        var classNames = [
            'am-zebra-printer-status--checking',
            'am-zebra-printer-status--ready',
            'am-zebra-printer-status--warning',
            'am-zebra-printer-status--error'
        ];

        for (var i = 0; i < badges.length; i++) {
            var badge = badges[i];
            badge.classList.remove('d-none');
            for (var j = 0; j < classNames.length; j++) {
                badge.classList.remove(classNames[j]);
            }
            badge.classList.add('am-zebra-printer-status--' + status);

            var textNode = badge.querySelector('.am-zebra-printer-status__text');
            if (textNode) {
                textNode.textContent = message;
            }
        }
    }

    function hideStatusBadges(root) {
        var badges = getStatusBadges(root);
        for (var i = 0; i < badges.length; i++) {
            badges[i].classList.add('d-none');
        }
    }

    function resolveBrowserPrintSdkUrl() {
        if (window.AssetLabelZebraSdkUrl) {
            return window.AssetLabelZebraSdkUrl;
        }

        var zebraScript = document.querySelector('script[src*="asset-label-zebra.js"]');
        if (zebraScript) {
            var src = zebraScript.getAttribute('src') || '';
            var marker = '/Scripts/app/';
            var idx = src.indexOf(marker);
            if (idx >= 0) {
                return src.substring(0, idx) + '/Scripts/vendor/' + BROWSER_PRINT_SDK_FILE;
            }
        }

        return '/Scripts/vendor/' + BROWSER_PRINT_SDK_FILE;
    }

    function loadScript(src) {
        return new Promise(function (resolve, reject) {
            var settled = false;
            function finish(handler) {
                if (settled) {
                    return;
                }

                settled = true;
                clearTimeout(timer);
                handler();
            }

            if (window.BrowserPrint) {
                resolve(window.BrowserPrint);
                return;
            }

            var existing = document.querySelector('script[data-am-zebra-browser-print]');
            if (existing) {
                if (window.BrowserPrint) {
                    resolve(window.BrowserPrint);
                    return;
                }

                existing.parentNode.removeChild(existing);
            }

            var timer = setTimeout(function () {
                finish(function () {
                    reject(new Error('Timed out loading Zebra Browser Print SDK. Install and start Browser Print on this PC.'));
                });
            }, LOAD_TIMEOUT_MS);

            var script = document.createElement('script');
            script.src = src;
            script.async = true;
            script.setAttribute('data-am-zebra-browser-print', 'true');
            script.onload = function () {
                if (window.BrowserPrint) {
                    finish(function () { resolve(window.BrowserPrint); });
                    return;
                }

                finish(function () {
                    reject(new Error('Zebra Browser Print SDK loaded but BrowserPrint is unavailable.'));
                });
            };
            script.onerror = function () {
                finish(function () {
                    reject(new Error('Failed to load Zebra Browser Print SDK. Install Browser Print on this PC.'));
                });
            };
            document.head.appendChild(script);
        });
    }

    function fetchJson(url) {
        return fetch(url, { credentials: 'same-origin' }).then(function (response) {
            if (!response.ok) {
                throw new Error('Unable to load label print configuration.');
            }
            return response.json();
        });
    }

    function fetchText(url) {
        return fetch(url, { credentials: 'same-origin' }).then(function (response) {
            if (!response.ok) {
                throw new Error('Unable to load ZPL label data.');
            }
            return response.text();
        });
    }

    function showError(message) {
        window.alert(message);
    }

    function listPrinters(browserPrint) {
        return new Promise(function (resolve, reject) {
            var settled = false;
            var timer = setTimeout(function () {
                if (settled) {
                    return;
                }

                settled = true;
                reject(new Error('Timed out discovering printers via Zebra Browser Print.'));
            }, LOAD_TIMEOUT_MS);

            browserPrint.getLocalDevices(function (devices) {
                if (settled) {
                    return;
                }

                settled = true;
                clearTimeout(timer);

                var printers = [];
                if (devices && devices.printer) {
                    printers = devices.printer;
                } else if (Array.isArray(devices)) {
                    printers = devices;
                }
                resolve(printers || []);
            }, function (error) {
                if (settled) {
                    return;
                }

                settled = true;
                clearTimeout(timer);
                reject(error || new Error('Unable to discover printers via Zebra Browser Print.'));
            }, 'printer');
        });
    }

    function findMatchingPrinter(printers, deviceName) {
        if (!printers || printers.length === 0) {
            return { printer: null, matched: false };
        }

        if (deviceName) {
            for (var i = 0; i < printers.length; i++) {
                if (printers[i].name && printers[i].name.indexOf(deviceName) >= 0) {
                    return { printer: printers[i], matched: true };
                }
            }
            return { printer: printers[0], matched: false };
        }

        return { printer: printers[0], matched: true };
    }

    function selectDevice(browserPrint, deviceName) {
        return listPrinters(browserPrint).then(function (printers) {
            if (!printers.length) {
                throw new Error('No printers found. Check that Zebra Browser Print is running and the ZD421 is connected.');
            }

            var match = findMatchingPrinter(printers, deviceName);
            return match.printer;
        });
    }

    function sendToPrinter(device, zpl) {
        return new Promise(function (resolve, reject) {
            device.send(zpl, function () { resolve(); }, function (error) {
                reject(error || new Error('The printer rejected the label job.'));
            });
        });
    }

    function isBrowserPrintIssue(message) {
        if (!message) {
            return false;
        }

        return message.indexOf('Install Browser Print') >= 0
            || message.indexOf('Browser Print SDK') >= 0
            || message.indexOf('BrowserPrint is unavailable') >= 0
            || message.indexOf('discover printers via Zebra Browser Print') >= 0;
    }

    function pollPrinterStatus(root, config) {
        var badges = getStatusBadges(root);
        if (!badges.length) {
            return Promise.resolve();
        }

        setStatusBadges(badges, STATUS_CHECKING, 'Checking printer...');

        return loadScript(resolveBrowserPrintSdkUrl())
            .then(function (browserPrint) {
                return listPrinters(browserPrint).then(function (printers) {
                    if (!printers.length) {
                        setStatusBadges(badges, STATUS_ERROR, 'No printer found — check USB/cable and driver.');
                        return;
                    }

                    var match = findMatchingPrinter(printers, config.deviceName);
                    var printerName = (match.printer && match.printer.name) ? match.printer.name : 'Zebra printer';

                    if (config.deviceName && !match.matched) {
                        setStatusBadges(
                            badges,
                            STATUS_WARNING,
                            'Configured printer not found. Using ' + printerName + '.');
                        return;
                    }

                    setStatusBadges(badges, STATUS_READY, printerName + ' ready');
                });
            })
            .catch(function (error) {
                var message = error && error.message ? error.message : 'Browser Print not detected.';
                if (isBrowserPrintIssue(message)) {
                    setStatusBadges(badges, STATUS_WARNING, 'Browser Print not detected — install and start it on this PC.');
                    return;
                }

                setStatusBadges(badges, STATUS_ERROR, message);
            });
    }

    function printZplDirectly(zpl, config, browserPrint) {
        var load = browserPrint
            ? Promise.resolve(browserPrint)
            : loadScript(resolveBrowserPrintSdkUrl());

        return load.then(function (resolvedBrowserPrint) {
            return selectDevice(resolvedBrowserPrint, config.deviceName).then(function (device) {
                return sendToPrinter(device, zpl);
            });
        });
    }

    function printToZebra(button, config, root) {
        if (!button) {
            return;
        }

        button.disabled = true;
        var zplUrl = buildZplUrl(config.zplUrl, getSelectedCodeType(root));
        loadScript(resolveBrowserPrintSdkUrl())
            .then(function (browserPrint) {
                return fetchText(zplUrl).then(function (zpl) {
                    return printZplDirectly(zpl, config, browserPrint);
                });
            })
            .then(function () {
                button.disabled = false;
                pollPrinterStatus(root, config);
            })
            .catch(function (error) {
                button.disabled = false;
                pollPrinterStatus(root, config);
                showError(error && error.message ? error.message : 'Zebra print failed.');
            });
    }

    function bindButton(button, config, root) {
        if (button.getAttribute('data-am-zebra-bound') === 'true') {
            return;
        }

        button.setAttribute('data-am-zebra-bound', 'true');
        button.addEventListener('click', function () {
            printToZebra(button, config, root);
        });
    }

    function initRoot(root) {
        var configUrl = getConfigUrl(root);
        if (!configUrl) {
            return;
        }

        fetchJson(configUrl).then(function (config) {
            if (!config || !config.enabled || config.mode !== MODE_ZEBRA) {
                hideStatusBadges(root);
                return;
            }

            var buttons = (root || document).querySelectorAll('[data-am-asset-label-zebra-print]');
            for (var i = 0; i < buttons.length; i++) {
                buttons[i].classList.remove('d-none');
                bindButton(buttons[i], config, root);
            }

            pollPrinterStatus(root, config);
        }).catch(function () {
            hideStatusBadges(root);
        });
    }

    function boot() {
        initRoot(document);
        var modal = document.getElementById('assetQrLabelModal');
        if (modal) {
            modal.addEventListener('shown.bs.modal', function () {
                initRoot(modal);
            });
        }
    }

    window.AssetLabelZebra = {
        initRoot: initRoot,
        pollPrinterStatus: pollPrinterStatus,
        printToZebra: printToZebra,
        printZplDirectly: printZplDirectly,
        fetchPrinterConfig: fetchJson,
        buildZplUrl: buildZplUrl
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', boot);
    } else {
        boot();
    }
})(window);
