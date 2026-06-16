/* eslint-env browser */
/* global window, jQuery */
(function (global) {
    "use strict";

    var DEFAULT_POLL_INTERVAL_MS = 3000;
    var SUPPORT_FREEZE_POLL_INTERVAL_MS = 5000;

    function formatDuration(seconds) {
        var total = Math.max(0, parseInt(seconds, 10) || 0);
        var hours = Math.floor(total / 3600);
        var minutes = Math.floor((total % 3600) / 60);
        var secs = total % 60;

        if (hours > 0) {
            return hours + "h " + (minutes < 10 ? "0" : "") + minutes + "m " + (secs < 10 ? "0" : "") + secs + "s";
        }

        return (minutes < 10 ? "0" : "") + minutes + ":" + (secs < 10 ? "0" : "") + secs;
    }

    function startLocalCountdown(secondsLeft, onTick, onExpire) {
        var remaining = Math.max(0, parseInt(secondsLeft, 10) || 0);
        onTick(remaining);

        if (remaining <= 0) {
            if (onExpire) {
                onExpire();
            }
            return null;
        }

        var handle = global.setInterval(function () {
            remaining--;
            if (remaining <= 0) {
                onTick(0);
                global.clearInterval(handle);
                if (onExpire) {
                    onExpire();
                }
                return;
            }

            onTick(remaining);
        }, 1000);

        return handle;
    }

    function showBootstrapModal(modalElement) {
        if (!modalElement) {
            return null;
        }

        if (global.bootstrap && global.bootstrap.Modal) {
            var instance = global.bootstrap.Modal.getOrCreateInstance(modalElement);
            instance.show();
            return instance;
        }

        var $modal = jQuery(modalElement);
        if ($modal.length) {
            $modal.modal("show");
        }

        return null;
    }

    function isBootstrapModalVisible(modalElement) {
        if (!modalElement) {
            return false;
        }

        return modalElement.classList.contains("show");
    }

    function initSupportSessionCanvas() {
        if (!global.AmAntiGravityBubbles || !global.AmAntiGravityBubbles.init) {
            return null;
        }

        var canvas = global.document.getElementById("amSupportSessionCanvas");
        if (!canvas || canvas.amAntiGravityInitialized) {
            return canvas;
        }

        global.AmAntiGravityBubbles.init(canvas);
        canvas.amAntiGravityInitialized = true;
        return canvas;
    }

    function destroySupportSessionCanvas() {
        var canvas = global.document.getElementById("amSupportSessionCanvas");
        if (canvas && canvas.amAntiGravityDestroy) {
            canvas.amAntiGravityDestroy();
            canvas.amAntiGravityInitialized = false;
        }
    }

    function setSupportOverlayVisible(isVisible) {
        var overlay = global.document.getElementById("am-support-session-overlay");
        if (!overlay) {
            return;
        }

        overlay.classList.toggle("is-visible", !!isVisible);
        overlay.setAttribute("aria-hidden", isVisible ? "false" : "true");

        if (isVisible) {
            initSupportSessionCanvas();
        } else {
            destroySupportSessionCanvas();
        }
    }

    function hideImpersonationBar() {
        var bar = global.document.querySelector(".app-impersonation-bar");
        if (bar) {
            bar.parentNode.removeChild(bar);
        }
    }

    function redirectAfterImpersonationEnd(redirectUrl) {
        hideImpersonationBar();
        if (redirectUrl) {
            global.location.href = redirectUrl;
            return;
        }

        global.location.reload();
    }

    function initImpersonationCountdown(config) {
        var noopDestroy = function () { };

        if (!config || !config.statusUrl || !config.countdownSelector) {
            return noopDestroy;
        }

        var $countdown = jQuery(config.countdownSelector);
        if ($countdown.length === 0) {
            return noopDestroy;
        }

        var interval = null;
        var resyncTimer = null;
        var currentRedirectUrl = null;

        function syncStatus() {
            jQuery.getJSON(config.statusUrl)
                .done(function (data) {
                    var secondsLeft = parseInt(data.secondsLeft, 10) || 0;
                    currentRedirectUrl = data && data.redirectUrl ? data.redirectUrl : null;

                    if (interval) {
                        global.clearInterval(interval);
                        interval = null;
                    }

                    if (secondsLeft <= 0) {
                        $countdown.text(formatDuration(0));
                        redirectAfterImpersonationEnd(currentRedirectUrl);
                        return;
                    }

                    interval = startLocalCountdown(secondsLeft, function (remaining) {
                        $countdown.text(formatDuration(remaining));
                    }, function () {
                        redirectAfterImpersonationEnd(currentRedirectUrl);
                    });
                })
                .fail(function () {
                    redirectAfterImpersonationEnd(null);
                });
        }

        syncStatus();
        resyncTimer = global.setInterval(syncStatus, 15000);

        if (config.endSessionUrl) {
            global.addEventListener("pagehide", function () {
                if (global.navigator && global.navigator.sendBeacon) {
                    global.navigator.sendBeacon(config.endSessionUrl);
                }
            });
        }

        return function destroy() {
            if (interval) {
                global.clearInterval(interval);
            }
            if (resyncTimer) {
                global.clearInterval(resyncTimer);
            }
        };
    }

    function initSupportSessionFreeze(config) {
        if (!config || !config.statusUrl) {
            return;
        }

        var freezeInterval = null;
        var wasFrozen = false;
        var pollInterval = config.pollIntervalMs || SUPPORT_FREEZE_POLL_INTERVAL_MS;

        function checkSystemFreeze() {
            jQuery.getJSON(config.statusUrl, function (data) {
                if (data.isLocked) {
                    wasFrozen = true;
                    setSupportOverlayVisible(true);

                    if (freezeInterval) {
                        global.clearInterval(freezeInterval);
                        freezeInterval = null;
                    }

                    freezeInterval = startLocalCountdown(data.secondsLeft, function (remaining) {
                        jQuery("#freeze-countdown").text(formatDuration(remaining));
                    }, function () {
                        checkSystemFreeze();
                    });
                } else {
                    setSupportOverlayVisible(false);
                    if (freezeInterval) {
                        global.clearInterval(freezeInterval);
                        freezeInterval = null;
                    }

                    if (wasFrozen && data.unlockUrl) {
                        wasFrozen = false;
                        global.location.href = data.unlockUrl;
                    }
                }
            });
        }

        checkSystemFreeze();
        global.setInterval(checkSystemFreeze, pollInterval);
    }

    function initElevationRequests(config) {
        if (!config || !config.pendingUrl) {
            return;
        }

        var pollInterval = config.pollIntervalMs || DEFAULT_POLL_INTERVAL_MS;
        var modalElement = global.document.getElementById("elevationRequestModal");

        function checkElevationRequests() {
            jQuery.getJSON(config.pendingUrl, function (data) {
                if (!data || data.count <= 0) {
                    return;
                }

                var req = data.requests[0];
                jQuery("#elevationRequestDetails").html(
                    "<strong>Platform admin:</strong> " + req.requestedBy + "<br/>" +
                    "<strong>Time:</strong> " + req.requestDate + "<br/>" +
                    (req.reason ? "<strong>Reason:</strong> " + req.reason : "")
                );
                jQuery("#denyRequestId").val(req.id);
                jQuery("#approveRequestId").val(req.id);

                if (!isBootstrapModalVisible(modalElement)) {
                    showBootstrapModal(modalElement);
                }
            });
        }

        checkElevationRequests();
        global.setInterval(checkElevationRequests, pollInterval);
    }

    function initPlatformImpersonationRequest(config) {
        if (!config || !config.requestUrl || !config.statusUrl) {
            return;
        }

        var modalElement = global.document.getElementById("requestImpersonationModal");
        var form = global.document.getElementById("impersonationRequestForm");
        var formPanel = global.document.getElementById("impersonationRequestFormPanel");
        var waitingPanel = global.document.getElementById("impersonationRequestWaitingPanel");
        var outcomePanel = global.document.getElementById("impersonationRequestOutcomePanel");
        var waitingMessage = global.document.getElementById("impersonationRequestWaitingMessage");
        var outcomeMessage = global.document.getElementById("impersonationRequestOutcomeMessage");
        var cancelButton = global.document.getElementById("btnCancelPendingImpersonation");
        var closeButton = global.document.getElementById("btnCloseImpersonationModal");
        var pollInterval = config.pollIntervalMs || DEFAULT_POLL_INTERVAL_MS;
        var statusTimer = null;
        var activeRequestId = config.pendingRequestId || null;

        function setPanel(panel) {
            if (formPanel) {
                formPanel.classList.toggle("d-none", panel !== "form");
            }
            if (waitingPanel) {
                waitingPanel.classList.toggle("d-none", panel !== "waiting");
            }
            if (outcomePanel) {
                outcomePanel.classList.toggle("d-none", panel !== "outcome");
            }
            if (closeButton) {
                closeButton.classList.toggle("d-none", panel === "waiting");
            }
        }

        function resetToFormPanel() {
            clearStatusTimer();
            activeRequestId = null;
            if (outcomeMessage) {
                outcomeMessage.textContent = "";
            }
            setPanel("form");
        }

        function clearStatusTimer() {
            if (statusTimer) {
                global.clearInterval(statusTimer);
                statusTimer = null;
            }
        }

        function showWaitingState(requestId, message) {
            activeRequestId = requestId;
            if (waitingMessage) {
                waitingMessage.textContent = message || "Waiting for the company administrator to approve your request.";
            }
            setPanel("waiting");
            showBootstrapModal(modalElement);

            clearStatusTimer();
            statusTimer = global.setInterval(function () {
                pollRequestStatus(requestId);
            }, pollInterval);
        }

        function pollRequestStatus(requestId) {
            jQuery.getJSON(config.statusUrl, { requestId: requestId }, function (data) {
                var status = data && data.status ? data.status : "";
                if (status === "Approved" || status === "Active") {
                    clearStatusTimer();
                    global.location.reload();
                    return;
                }

                if (status === "Rejected" || status === "Cancelled" || status === "Expired") {
                    activeRequestId = null;
                    clearStatusTimer();
                    if (outcomeMessage) {
                        outcomeMessage.textContent = status === "Rejected"
                            ? "The company administrator rejected your impersonation request."
                            : (status === "Cancelled"
                                ? "The impersonation request was cancelled."
                                : "The impersonation request expired before approval.");
                    }
                    setPanel("outcome");
                }
            });
        }

        if (modalElement) {
            modalElement.addEventListener("show.bs.modal", function () {
                if (!statusTimer) {
                    resetToFormPanel();
                }
            });

            modalElement.addEventListener("hidden.bs.modal", function () {
                var shouldReload = outcomePanel && !outcomePanel.classList.contains("d-none");
                if (!statusTimer) {
                    resetToFormPanel();
                }
                if (shouldReload) {
                    global.location.reload();
                }
            });
        }

        var tryAgainButton = global.document.getElementById("btnRetryImpersonationRequest");
        if (tryAgainButton) {
            tryAgainButton.addEventListener("click", function () {
                resetToFormPanel();
            });
        }

        if (form) {
            form.addEventListener("submit", function (event) {
                event.preventDefault();

                if (outcomeMessage) {
                    outcomeMessage.textContent = "";
                }
                setPanel("form");

                var formData = new global.FormData(form);
                jQuery.ajax({
                    url: config.requestUrl,
                    method: "POST",
                    data: formData,
                    processData: false,
                    contentType: false,
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    success: function (response) {
                        if (response && response.success && response.requestId) {
                            showWaitingState(response.requestId, response.message);
                        } else {
                            global.alert((response && response.message) || "Unable to send impersonation request.");
                        }
                    },
                    error: function () {
                        global.alert("Unable to send impersonation request. Please try again.");
                    }
                });
            });
        }

        if (cancelButton) {
            cancelButton.addEventListener("click", function () {
                if (!activeRequestId) {
                    return;
                }

                var tokenInput = form ? form.querySelector("input[name='__RequestVerificationToken']") : null;
                var payload = new global.FormData();
                payload.append("requestId", activeRequestId);
                if (tokenInput) {
                    payload.append("__RequestVerificationToken", tokenInput.value);
                }

                jQuery.ajax({
                    url: config.cancelUrl,
                    method: "POST",
                    data: payload,
                    processData: false,
                    contentType: false,
                    headers: { "X-Requested-With": "XMLHttpRequest" },
                    success: function () {
                        clearStatusTimer();
                        setPanel("form");
                        if (global.bootstrap && global.bootstrap.Modal && modalElement) {
                            global.bootstrap.Modal.getOrCreateInstance(modalElement).hide();
                        }
                    }
                });
            });
        }

        if (activeRequestId) {
            showWaitingState(activeRequestId);
        }
    }

    global.AmImpersonationUi = {
        formatDuration: formatDuration,
        initImpersonationCountdown: initImpersonationCountdown,
        initSupportSessionFreeze: initSupportSessionFreeze,
        initElevationRequests: initElevationRequests,
        initPlatformImpersonationRequest: initPlatformImpersonationRequest
    };
})(window);
