/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    var STORAGE_KEY = "amAuthTheme";
    var DEFAULT_THEME = "light";

    function getStoredTheme() {
        var stored = global.localStorage ? global.localStorage.getItem(STORAGE_KEY) : null;
        return stored === "dark" ? "dark" : DEFAULT_THEME;
    }

    function applyTheme(theme, persist) {
        var nextTheme = theme === "dark" ? "dark" : DEFAULT_THEME;
        var body = global.document.body;
        if (!body || !body.classList.contains("am-login-page")) {
            return nextTheme;
        }

        body.classList.remove("am-auth-theme-light", "am-auth-theme-dark");
        body.classList.add(nextTheme === "dark" ? "am-auth-theme-dark" : "am-auth-theme-light");

        if (persist !== false && global.localStorage) {
            global.localStorage.setItem(STORAGE_KEY, nextTheme);
            global.document.documentElement.setAttribute("data-am-auth-theme", nextTheme);
        }

        var toggle = global.document.getElementById("amAuthThemeToggle");
        if (toggle) {
            var isDark = nextTheme === "dark";
            toggle.setAttribute("aria-pressed", isDark ? "true" : "false");
            toggle.setAttribute("aria-label", isDark ? "Switch to light mode" : "Switch to dark mode");
            var label = toggle.querySelector(".am-auth-theme-toggle__label");
            if (label) {
                label.textContent = isDark ? "Dark" : "Light";
            }
        }

        if (global.AmAntiGravityBubbles && global.AmAntiGravityBubbles.refreshTheme) {
            global.AmAntiGravityBubbles.refreshTheme();
        }

        return nextTheme;
    }

    function toggleTheme() {
        var current = global.document.body.classList.contains("am-auth-theme-dark") ? "dark" : DEFAULT_THEME;
        return applyTheme(current === "dark" ? DEFAULT_THEME : "dark");
    }

    function initAuthTheme() {
        if (!global.document.body || !global.document.body.classList.contains("am-login-page")) {
            return;
        }

        applyTheme(getStoredTheme(), false);

        var toggle = global.document.getElementById("amAuthThemeToggle");
        if (toggle) {
            toggle.addEventListener("click", toggleTheme);
        }
    }

    global.AmAuthTheme = {
        apply: applyTheme,
        toggle: toggleTheme,
        init: initAuthTheme,
        getStored: getStoredTheme
    };

    global.document.addEventListener("DOMContentLoaded", initAuthTheme);
})(window);
