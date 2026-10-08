(function (window) {
    "use strict";

    var config = {};
    var scanner = null;
    var isActive = false;
    var isProcessingScan = false;

    function getElement(selector) {
        return selector ? document.querySelector(selector) : null;
    }

    function getHtml5QrcodeClass() {
        if (window.Html5Qrcode) {
            return window.Html5Qrcode;
        }

        if (window.__Html5QrcodeLibrary__ && window.__Html5QrcodeLibrary__.Html5Qrcode) {
            return window.__Html5QrcodeLibrary__.Html5Qrcode;
        }

        return null;
    }

    function setCameraMessage(text, isError) {
        var message = getElement(config.messageSelector);
        if (!message) {
            return;
        }

        message.textContent = text || "";
        message.classList.toggle("am-scan-camera-message--error", !!isError);
        message.hidden = !text;
    }

    function setToggleLabel(active) {
        var toggle = getElement(config.toggleSelector);
        if (!toggle) {
            return;
        }

        toggle.setAttribute("aria-pressed", active ? "true" : "false");

        var label = toggle.querySelector(".am-scan-camera-toggle-label");
        if (label) {
            label.textContent = active ? "Stop camera" : "Use phone camera";
        }
    }

    function showWrap(show) {
        var wrap = getElement(config.wrapSelector);
        if (!wrap) {
            return;
        }

        wrap.hidden = !show;
        wrap.classList.toggle("d-none", !show);
        wrap.classList.toggle("is-active", show);
    }

    function canUseCamera() {
        return window.isSecureContext
            && !!getHtml5QrcodeClass()
            && !!(navigator.mediaDevices && navigator.mediaDevices.getUserMedia);
    }

    function scannerConfig() {
        return {
            fps: 10,
            qrbox: function (viewfinderWidth, viewfinderHeight) {
                var edge = Math.min(viewfinderWidth, viewfinderHeight);
                var size = Math.max(180, Math.floor(edge * 0.72));
                return { width: size, height: size };
            },
            aspectRatio: 1.777778,
            disableFlip: false
        };
    }

    function onScanSuccess(decodedText) {
        if (isProcessingScan || !decodedText) {
            return;
        }

        isProcessingScan = true;
        var input = getElement(config.inputSelector);

        stopCamera()
            .finally(function () {
                if (input) {
                    input.value = decodedText;
                }

                if (window.AmAssetScan && typeof window.AmAssetScan.lookup === "function") {
                    window.AmAssetScan.lookup(decodedText);
                }

                isProcessingScan = false;
            });
    }

    function onScanFailure() {
        // html5-qrcode calls this frequently while searching; ignore noise.
    }

    function startWithFacingMode(Html5QrcodeClass, hostElement) {
        scanner = new Html5QrcodeClass(hostElement.id, { verbose: false });
        return scanner.start(
            { facingMode: "environment" },
            scannerConfig(),
            onScanSuccess,
            onScanFailure
        );
    }

    function startWithDiscoveredCamera(Html5QrcodeClass, hostElement) {
        return Html5QrcodeClass.getCameras()
            .then(function (devices) {
                if (!devices || !devices.length) {
                    throw new Error("No camera found");
                }

                var selectedCamera = devices[devices.length - 1];
                for (var i = 0; i < devices.length; i++) {
                    var label = (devices[i].label || "").toLowerCase();
                    if (label.indexOf("back") >= 0 || label.indexOf("rear") >= 0 || label.indexOf("environment") >= 0) {
                        selectedCamera = devices[i];
                        break;
                    }
                }

                scanner = new Html5QrcodeClass(hostElement.id, { verbose: false });
                return scanner.start(selectedCamera.id, scannerConfig(), onScanSuccess, onScanFailure);
            });
    }

    function describeCameraError(error) {
        if (window.isSecureContext === false) {
            return "The camera needs a secure (https) connection. Open this page over https, or type or paste the tag instead.";
        }

        if (!error) {
            return "Could not access the camera.";
        }

        // html5-qrcode often rejects with a plain string such as "NotAllowedError: Permission denied",
        // so match the DOMException name in the text as well as on error.name.
        var name = typeof error === "string" ? "" : (error.name || "");
        var text = typeof error === "string" ? error : (error.message || String(error));
        function is(names) {
            for (var i = 0; i < names.length; i++) {
                if (name === names[i] || text.indexOf(names[i]) >= 0) {
                    return true;
                }
            }
            return false;
        }

        if (is(["NotAllowedError", "PermissionDeniedError", "Permission denied"])) {
            return "Camera permission denied. Allow camera access for this site in your browser settings, then try again.";
        }

        if (is(["NotFoundError", "DevicesNotFoundError", "OverconstrainedError", "No camera", "Requested device not found"])) {
            return "No camera was found on this device.";
        }

        if (is(["NotReadableError", "TrackStartError", "Could not start video source"])) {
            return "The camera is in use by another app or tab. Close it, then try again.";
        }

        return text ? "Could not access the camera: " + text : "Could not access the camera.";
    }

    // html5-qrcode throws synchronously from stop() ("Cannot stop, scanner is not running or
    // paused.") and clear() when the scanner never started. Inside a .catch() handler that
    // synchronous throw replaced the real start error (permission denied, no camera, in use).
    // Only stop a scanner that is SCANNING (2) or PAUSED (3), and run both calls inside a
    // promise so nothing escapes.
    function isScannerRunning(activeScanner) {
        if (!activeScanner || typeof activeScanner.getState !== "function") {
            return true;
        }

        try {
            var state = activeScanner.getState();
            return state === 2 || state === 3;
        } catch (e) {
            return false;
        }
    }

    function safeScannerCall(activeScanner, methodName) {
        return Promise.resolve()
            .then(function () {
                if (activeScanner && typeof activeScanner[methodName] === "function") {
                    return activeScanner[methodName]();
                }
            })
            .catch(function () { });
    }

    function stopAndClearScanner(activeScanner) {
        var stopped = isScannerRunning(activeScanner)
            ? safeScannerCall(activeScanner, "stop")
            : Promise.resolve();
        return stopped.then(function () {
            return safeScannerCall(activeScanner, "clear");
        });
    }

    function resetScannerInstance() {
        if (!scanner) {
            return Promise.resolve();
        }

        var activeScanner = scanner;
        scanner = null;

        return stopAndClearScanner(activeScanner);
    }

    function startCamera() {
        var Html5QrcodeClass = getHtml5QrcodeClass();
        if (isActive || !canUseCamera()) {
            if (!window.isSecureContext) {
                setCameraMessage("Camera access requires HTTPS or localhost.", true);
            } else if (!Html5QrcodeClass) {
                setCameraMessage("Camera scanner failed to load. Refresh and try again.", true);
            } else {
                setCameraMessage("Camera is not available on this device or browser.", true);
            }
            return Promise.resolve();
        }

        var hostElement = getElement(config.hostSelector);
        if (!hostElement) {
            return Promise.resolve();
        }

        setCameraMessage("Starting camera\u2026", false);
        showWrap(true);
        setToggleLabel(true);
        isActive = true;

        function onStarted() {
            setCameraMessage("Point your camera at the asset QR label.", false);

            var input = getElement(config.inputSelector);
            if (input) {
                input.blur();
            }
        }

        function onFailed(error) {
            isActive = false;
            scanner = null;
            showWrap(false);
            setToggleLabel(false);
            setCameraMessage(describeCameraError(error), true);
        }

        return startWithFacingMode(Html5QrcodeClass, hostElement)
            .then(onStarted)
            .catch(function (facingModeError) {
                return resetScannerInstance().then(function () {
                    return startWithDiscoveredCamera(Html5QrcodeClass, hostElement).then(onStarted);
                }).catch(function () {
                    throw facingModeError;
                });
            })
            .catch(onFailed);
    }

    function stopCamera() {
        if (!scanner || !isActive) {
            isActive = false;
            showWrap(false);
            setToggleLabel(false);
            setCameraMessage("", false);
            return Promise.resolve();
        }

        var activeScanner = scanner;
        scanner = null;
        isActive = false;

        // Stop/clear errors are ignored (stream already closed); see stopAndClearScanner.
        return stopAndClearScanner(activeScanner)
            .then(function () {
                showWrap(false);
                setToggleLabel(false);
                setCameraMessage("", false);
            });
    }

    function toggleCamera() {
        if (isActive) {
            return stopCamera();
        }

        return startCamera();
    }

    function init(options) {
        config = options || {};

        var toggle = getElement(config.toggleSelector);
        if (!toggle) {
            return;
        }

        if (!canUseCamera()) {
            toggle.disabled = true;
            toggle.title = "Camera scanning requires HTTPS and a device with a camera.";
            if (!window.isSecureContext) {
                setCameraMessage("Open this page over HTTPS to use your phone camera.", true);
            } else if (!getHtml5QrcodeClass()) {
                setCameraMessage("Camera scanner failed to load. Refresh and try again.", true);
            }
            return;
        }

        toggle.addEventListener("click", function () {
            toggleCamera();
        });

        var stopButton = getElement(config.stopSelector);
        if (stopButton) {
            stopButton.addEventListener("click", function () {
                stopCamera();
            });
        }

        document.addEventListener("visibilitychange", function () {
            if (document.hidden && isActive) {
                stopCamera();
            }
        });

        window.addEventListener("pagehide", function () {
            if (isActive) {
                stopCamera();
            }
        });
    }

    window.AmAssetScanCamera = {
        init: init,
        start: startCamera,
        stop: stopCamera,
        canUseCamera: canUseCamera
    };
})(window);
