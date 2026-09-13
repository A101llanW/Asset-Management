/* eslint-env browser */
/* global window */
/**
 * Login-page bootstrap for React Bits Antigravity (see antigravity-three.js).
 */
(function (global) {
    "use strict";

    function isDarkTheme() {
        return global.document.body
            && global.document.body.classList.contains("am-auth-theme-dark");
    }

    function getLoginOptions() {
        return {
            color: isDarkTheme() ? "#00c8ff" : "#6366f1",
            count: 140,
            magnetRadius: 12,
            ringRadius: 6,
            influenceRadius: 11,
            waveSpeed: 0.15,
            waveAmplitude: 0.35,
            particleSize: 0.72,
            lerpSpeed: 0.018,
            autoAnimate: false,
            particleVariance: 0.5,
            rotationSpeed: 0.02,
            depthFactor: 0.75,
            pulseSpeed: 1.2,
            particleShape: "asset-icon",
            fieldStrength: 14,
            hoverOnlyMagnet: true,
            mouseIdleMs: 600,
            idleDriftSpeed: 0.006,
            idleWanderAmplitude: 0.01,
            homeLerpSpeed: 0.01,
            iconPixelSize: 56
        };
    }

    function initAntiGravityBubbles(canvas) {
        if (!canvas || !global.AmAntigravityThree || !global.AmAntigravityThree.init) {
            return;
        }

        global.AmAntigravityThree.init(canvas, getLoginOptions());
    }

    function destroyCanvas(canvas) {
        if (canvas && global.AmAntigravityThree) {
            global.AmAntigravityThree.destroy(canvas);
        }
    }

    function refreshTheme() {
        destroyCanvas(global.document.getElementById("amAntiGravityCanvas"));
        bootLoginCanvas();
    }

    function bootLoginCanvas() {
        var canvas = global.document.getElementById("amAntiGravityCanvas");
        if (canvas) {
            initAntiGravityBubbles(canvas);
        }
    }

    function boot() {
        bootLoginCanvas();
    }

    global.AmAntiGravityBubbles = {
        init: initAntiGravityBubbles,
        refreshTheme: refreshTheme
    };

    if (global.document.readyState === "loading") {
        global.document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }
})(window);
