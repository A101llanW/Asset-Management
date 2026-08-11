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
            count: 160,
            magnetRadius: 10,
            ringRadius: 5,
            influenceRadius: 9,
            waveSpeed: 0.2,
            waveAmplitude: 0.5,
            particleSize: 0.68,
            lerpSpeed: 0.028,
            autoAnimate: false,
            particleVariance: 0.6,
            rotationSpeed: 0.03,
            depthFactor: 0.8,
            pulseSpeed: 1.5,
            particleShape: "asset-icon",
            fieldStrength: 12,
            hoverOnlyMagnet: true,
            mouseIdleMs: 450,
            idleDriftSpeed: 0.01,
            idleWanderAmplitude: 0.014,
            homeLerpSpeed: 0.014,
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
