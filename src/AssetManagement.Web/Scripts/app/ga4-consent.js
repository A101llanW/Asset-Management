/*
 * GA4 anonymous usage analytics with Consent Mode v2 (Nanosoft Asset Management).
 * Loaded only when the server rendered window.amGa4Config (valid Ga4MeasurementId + production host or local test flag).
 * - consent default (all denied) is set inline before this file runs.
 * - Nothing is sent and gtag.js is not loaded until the user clicks Accept. Decline / close keeps everything denied.
 * - On Accept only analytics_storage is granted; ad_storage, ad_user_data and ad_personalization stay denied.
 * - page_location / page_path / page_title / page_referrer come pre-sanitized from the server (no ids, tags, org slug,
 *   query string or record names). No user_id, no user properties, Google signals and ad personalization off.
 * - Optional feature events: data-ga-event="asset_create" on a form (fires on submit) or a button/link (fires on
 *   click). Only the event name is sent, never form values.
 */
(function () {
    "use strict";
    var cfg = window.amGa4Config;
    if (!cfg || !cfg.id || typeof window.gtag !== "function") {
        return;
    }

    var host = (window.location.hostname || "").toLowerCase();
    var hostOk = !!cfg.allowAnyHost;
    for (var h = 0; !hostOk && cfg.hosts && h < cfg.hosts.length; h++) {
        if (host === String(cfg.hosts[h]).toLowerCase()) {
            hostOk = true;
        }
    }
    if (!hostOk) {
        return;
    }

    var STORAGE_KEY = cfg.storageKey || "amGa4Consent";
    var EVENT_RX = /^[a-z][a-z0-9_]{0,39}$/;
    var started = false;

    function readChoice() {
        try { return window.localStorage.getItem(STORAGE_KEY); } catch (e) { return null; }
    }

    function saveChoice(value) {
        try { window.localStorage.setItem(STORAGE_KEY, value); } catch (e) { }
    }

    function pageLocation() {
        return window.location.protocol + "//" + window.location.host + cfg.pagePath;
    }

    function start() {
        if (started) {
            return;
        }
        started = true;
        window.gtag("consent", "update", { analytics_storage: "granted" });
        window.gtag("js", new Date());
        window.gtag("config", cfg.id, {
            send_page_view: false,
            allow_google_signals: false,
            allow_ad_personalization_signals: false,
            page_location: pageLocation(),
            page_path: cfg.pagePath,
            page_title: cfg.pageTitle,
            page_referrer: cfg.pageReferrer || ""
        });
        window.gtag("event", "page_view", {
            page_location: pageLocation(),
            page_path: cfg.pagePath,
            page_title: cfg.pageTitle,
            page_referrer: cfg.pageReferrer || ""
        });
        var s = document.createElement("script");
        s.async = true;
        s.src = "https://www.googletagmanager.com/gtag/js?id=" + encodeURIComponent(cfg.id);
        document.head.appendChild(s);
    }

    function sendFeatureEvent(name) {
        if (!started || !EVENT_RX.test(name || "")) {
            return;
        }
        window.gtag("event", name);
    }

    function removeBanner(banner) {
        if (banner && banner.parentNode) {
            banner.parentNode.removeChild(banner);
        }
    }

    function showBanner() {
        if (document.getElementById("amGa4Consent")) {
            return;
        }
        var style = document.createElement("style");
        style.textContent =
            "#amGa4Consent{position:fixed;left:12px;bottom:12px;z-index:1080;max-width:340px;background:#fff;color:#1f2937;" +
            "border:1px solid #d1d5db;border-radius:10px;box-shadow:0 6px 24px rgba(0,0,0,.15);padding:10px 34px 10px 12px;" +
            "font-size:13px;line-height:1.4}" +
            "#amGa4Consent p{margin:0 0 8px}" +
            "#amGa4Consent .am-ga4-actions{display:flex;gap:6px}" +
            "#amGa4Consent .am-ga4-close{position:absolute;top:4px;right:6px;border:0;background:transparent;font-size:18px;line-height:1;color:#6b7280;cursor:pointer;padding:4px}" +
            "@media (max-width:480px){#amGa4Consent{left:8px;right:8px;max-width:none}}";
        document.head.appendChild(style);

        var banner = document.createElement("div");
        banner.id = "amGa4Consent";
        banner.setAttribute("role", "dialog");
        banner.setAttribute("aria-live", "polite");
        banner.setAttribute("aria-label", "Analytics consent");
        banner.innerHTML =
            '<button type="button" class="am-ga4-close" data-am-ga4="decline" aria-label="Close">&times;</button>' +
            "<p>Allow anonymous usage analytics? It helps us improve the app. No names, emails or record details are sent.</p>" +
            '<div class="am-ga4-actions">' +
            '<button type="button" class="btn btn-primary btn-sm" data-am-ga4="accept">Accept</button>' +
            '<button type="button" class="btn btn-outline-secondary btn-sm" data-am-ga4="decline">Decline</button>' +
            "</div>";
        banner.addEventListener("click", function (ev) {
            var t = ev.target && ev.target.closest ? ev.target.closest("[data-am-ga4]") : null;
            if (!t) {
                return;
            }
            if (t.getAttribute("data-am-ga4") === "accept") {
                saveChoice("granted");
                removeBanner(banner);
                start();
            } else {
                saveChoice("denied");
                removeBanner(banner);
            }
        });
        document.body.appendChild(banner);
    }

    document.addEventListener("submit", function (ev) {
        var form = ev.target;
        if (form && form.getAttribute) {
            sendFeatureEvent(form.getAttribute("data-ga-event"));
        }
    }, true);

    document.addEventListener("click", function (ev) {
        var el = ev.target && ev.target.closest ? ev.target.closest("[data-ga-event]") : null;
        if (el && el.tagName !== "FORM") {
            sendFeatureEvent(el.getAttribute("data-ga-event"));
        }
    }, true);

    var choice = readChoice();
    if (choice === "granted") {
        start();
    } else if (choice !== "denied") {
        if (document.readyState === "loading") {
            document.addEventListener("DOMContentLoaded", showBanner);
        } else {
            showBanner();
        }
    }
})();
