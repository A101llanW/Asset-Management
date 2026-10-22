// rel1022a: label print log (client). Records on the server that a label was printed or sent to the print dialog,
// and talks to the print-batch endpoints. It never prints: Zebra printing stays in asset-label-zebra.js
// (Zebra Browser Print), unchanged. A failed log call never blocks or retries a print.
(function (window) {
    "use strict";

    function findToken(scope) {
        var node = (scope && scope.querySelector && scope.querySelector('input[name="__RequestVerificationToken"]'))
            || document.querySelector('[data-am-print-log-token] input[name="__RequestVerificationToken"]')
            || document.querySelector('input[name="__RequestVerificationToken"]');
        return node ? node.value : "";
    }

    function encode(data) {
        var parts = [];
        Object.keys(data || {}).forEach(function (key) {
            var value = data[key];
            if (value === null || value === undefined || value === "") {
                return;
            }
            if (Object.prototype.toString.call(value) === "[object Array]") {
                value.forEach(function (item) {
                    parts.push(encodeURIComponent(key) + "=" + encodeURIComponent(String(item)));
                });
                return;
            }
            parts.push(encodeURIComponent(key) + "=" + encodeURIComponent(String(value)));
        });
        return parts.join("&");
    }

    function readJson(response) {
        var contentType = response.headers.get("content-type") || "";
        if (!response.ok || contentType.indexOf("application/json") === -1) {
            throw new Error("Print log request failed (HTTP " + response.status + ").");
        }
        return response.json().then(function (body) {
            if (body && body.success === false) {
                throw new Error(body.message || "Print log request failed.");
            }
            return body;
        });
    }

    function post(url, data, scope) {
        if (!url) {
            return Promise.reject(new Error("Print logging is not configured on this page."));
        }
        var payload = Object.assign({}, data || {});
        payload.__RequestVerificationToken = findToken(scope);
        return fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "Accept": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: encode(payload)
        }).then(readJson);
    }

    function get(url, data) {
        if (!url) {
            return Promise.reject(new Error("Print logging is not configured on this page."));
        }
        var query = encode(data || {});
        return fetch(url + (query ? (url.indexOf("?") >= 0 ? "&" : "?") + query : ""), {
            credentials: "same-origin",
            headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
        }).then(readJson);
    }

    // Single label (Details modal / PrintLabel page). Reads data-asset-id and data-print-log-url from the button.
    function recordFromButton(button, mode, selection) {
        if (!button) {
            return Promise.resolve(null);
        }
        var url = button.getAttribute("data-print-log-url");
        var assetId = button.getAttribute("data-asset-id");
        if (!url || !assetId) {
            return Promise.resolve(null);
        }
        var scope = button.closest ? (button.closest(".am-asset-label-sheet") || button.closest(".modal") || document) : document;
        var templateSelect = scope.querySelector ? scope.querySelector("[data-am-asset-label-template]") : null;
        var codeSelect = scope.querySelector ? scope.querySelector("[data-am-asset-label-code-type]") : null;
        return post(url, {
            assetIds: [assetId],
            source: "Single",
            mode: mode || "Zebra",
            templateId: selection && selection.templateId ? selection.templateId : (templateSelect ? templateSelect.value : ""),
            codeType: selection && selection.codeType ? selection.codeType : (codeSelect ? codeSelect.value : "")
        }, scope).catch(function (error) {
            if (window.console && window.console.warn) {
                window.console.warn("Label print was not logged: " + (error && error.message ? error.message : error));
            }
            return null;
        });
    }

    window.AmLabelPrintLog = {
        post: post,
        get: get,
        recordFromButton: recordFromButton
    };
})(window);
