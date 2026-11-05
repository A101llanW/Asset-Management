(function (global) {
    "use strict";

    var DEBOUNCE_MS = 350;
    var HEADER_NAME = "X-Live-Filter";
    var HEADER_VALUE = "1";
    var RESULTS_SELECTOR = "[data-live-results]";
    var FORM_SELECTOR = "form[data-live-filter-form]";

    function debounce(fn, waitMs) {
        var timerId = null;
        var wrapped = function () {
            var args = arguments;
            var context = this;
            if (timerId) {
                clearTimeout(timerId);
            }
            timerId = setTimeout(function () {
                timerId = null;
                fn.apply(context, args);
            }, waitMs);
        };
        wrapped.cancel = function () {
            if (timerId) {
                clearTimeout(timerId);
                timerId = null;
            }
        };
        return wrapped;
    }

    function findResults(form) {
        var selector = form.getAttribute("data-live-results") || RESULTS_SELECTOR;
        var scoped = form.parentElement ? form.parentElement.querySelector(selector) : null;
        if (scoped) {
            return scoped;
        }
        return document.querySelector(selector);
    }

    function hideApplyButtons(form) {
        form.querySelectorAll("[data-live-filter-apply]").forEach(function (btn) {
            btn.classList.add("d-none");
            btn.setAttribute("aria-hidden", "true");
            btn.tabIndex = -1;
        });
    }

    function buildQuery(form, options) {
        options = options || {};
        var params = new URLSearchParams();
        var elements = form.elements;
        var i;
        for (i = 0; i < elements.length; i++) {
            var el = elements[i];
            if (!el || !el.name || el.disabled) {
                continue;
            }
            var tag = (el.tagName || "").toLowerCase();
            var type = (el.type || "").toLowerCase();
            if (type === "submit" || type === "button" || type === "file" || type === "reset") {
                continue;
            }
            if ((type === "checkbox" || type === "radio") && !el.checked) {
                continue;
            }
            if (tag === "select" && el.multiple) {
                Array.prototype.forEach.call(el.selectedOptions || [], function (opt) {
                    params.append(el.name, opt.value);
                });
                continue;
            }
            params.append(el.name, el.value == null ? "" : el.value);
        }

        if (options.resetPage !== false) {
            params.set("page", "1");
        }

        if (options.extraParams) {
            Object.keys(options.extraParams).forEach(function (key) {
                params.set(key, options.extraParams[key]);
            });
        }

        return params.toString();
    }

    function absoluteUrl(form, query) {
        var action = form.getAttribute("action");
        var base;
        if (action && action.length) {
            base = action;
        } else {
            base = window.location.pathname;
        }
        // Drop any existing query on the action.
        var qIndex = base.indexOf("?");
        if (qIndex >= 0) {
            base = base.substring(0, qIndex);
        }
        return query ? (base + "?" + query) : base;
    }

    function restoreFocus(form, focusName, selectionStart, selectionEnd) {
        if (!focusName) {
            return;
        }
        var input = form.querySelector('[name="' + focusName.replace(/"/g, '\\"') + '"]');
        if (!input || typeof input.focus !== "function") {
            return;
        }
        input.focus();
        if (typeof input.setSelectionRange === "function"
            && (input.type === "text" || input.type === "search" || input.tagName.toLowerCase() === "textarea"
                || input.type === "" || input.type === "tel" || input.type === "url" || input.type === "email")) {
            try {
                var len = (input.value || "").length;
                var start = selectionStart == null ? len : Math.min(selectionStart, len);
                var end = selectionEnd == null ? start : Math.min(selectionEnd, len);
                input.setSelectionRange(start, end);
            } catch (err) {
                // Some input types reject setSelectionRange; ignore.
            }
        }
    }

    function initForm(form) {
        if (form.getAttribute("data-live-filter-ready") === "1") {
            return;
        }
        form.setAttribute("data-live-filter-ready", "1");

        var results = findResults(form);
        if (!results) {
            return;
        }

        hideApplyButtons(form);

        var controller = null;
        var requestSeq = 0;

        function setBusy(busy) {
            results.setAttribute("aria-busy", busy ? "true" : "false");
            results.classList.toggle("am-live-filter-loading", !!busy);
            form.classList.toggle("am-live-filter-busy", !!busy);
        }

        function applyHtml(html, url, focusState) {
            var doc = new DOMParser().parseFromString(html, "text/html");
            var next = doc.querySelector(form.getAttribute("data-live-results") || RESULTS_SELECTOR);
            if (!next) {
                return false;
            }
            results.innerHTML = next.innerHTML;
            if (window.history && typeof window.history.replaceState === "function") {
                window.history.replaceState({ amLiveFilter: true }, "", url);
            }
            document.dispatchEvent(new CustomEvent("am:live-filter-updated", {
                detail: { form: form, results: results, url: url }
            }));
            if (focusState) {
                restoreFocus(form, focusState.name, focusState.start, focusState.end);
            }
            return true;
        }

        function fetchResults(opts) {
            opts = opts || {};
            var focusState = null;
            var active = document.activeElement;
            if (active && form.contains(active) && active.name) {
                focusState = {
                    name: active.name,
                    start: typeof active.selectionStart === "number" ? active.selectionStart : null,
                    end: typeof active.selectionEnd === "number" ? active.selectionEnd : null
                };
            }

            var query = opts.urlQuery;
            if (!query) {
                query = buildQuery(form, {
                    resetPage: opts.resetPage !== false,
                    extraParams: opts.extraParams
                });
            }
            var url = opts.fullUrl || absoluteUrl(form, query);
            var seq = ++requestSeq;

            if (controller && typeof controller.abort === "function") {
                try { controller.abort(); } catch (err) { /* ignore */ }
            }
            controller = typeof AbortController !== "undefined" ? new AbortController() : null;

            setBusy(true);

            var headers = {};
            headers[HEADER_NAME] = HEADER_VALUE;
            headers["X-Requested-With"] = "XMLHttpRequest";
            headers["Accept"] = "text/html";

            var fetchOpts = {
                method: "GET",
                credentials: "same-origin",
                headers: headers,
                cache: "no-store"
            };
            if (controller) {
                fetchOpts.signal = controller.signal;
            }

            fetch(url, fetchOpts).then(function (response) {
                if (!response.ok) {
                    throw new Error("Live filter request failed: " + response.status);
                }
                return response.text();
            }).then(function (html) {
                if (seq !== requestSeq) {
                    return;
                }
                applyHtml(html, url, focusState);
            }).catch(function (err) {
                if (err && err.name === "AbortError") {
                    return;
                }
                // Fallback: full navigation so the user is never stuck.
                if (opts.allowFallback !== false) {
                    window.location.href = url;
                }
            }).then(function () {
                if (seq === requestSeq) {
                    setBusy(false);
                }
            });
        }

        var debouncedFetch = debounce(function () {
            fetchResults({ resetPage: true });
        }, DEBOUNCE_MS);

        form.addEventListener("input", function (event) {
            var target = event.target;
            if (!target || !target.name) {
                return;
            }
            var type = (target.type || "").toLowerCase();
            var tag = (target.tagName || "").toLowerCase();
            if (tag === "select") {
                return;
            }
            if (type === "checkbox" || type === "radio" || type === "file" || type === "hidden") {
                return;
            }
            debouncedFetch();
        });

        form.addEventListener("change", function (event) {
            var target = event.target;
            if (!target || !target.name) {
                return;
            }
            var type = (target.type || "").toLowerCase();
            var tag = (target.tagName || "").toLowerCase();
            if (tag === "select" || type === "checkbox" || type === "radio" || type === "date" || type === "datetime-local" || type === "number" || type === "month" || type === "week") {
                debouncedFetch.cancel();
                fetchResults({ resetPage: true });
            }
        });

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            debouncedFetch.cancel();
            fetchResults({ resetPage: true });
        });

        results.addEventListener("click", function (event) {
            var anchor = event.target && event.target.closest ? event.target.closest("a") : null;
            if (!anchor || !anchor.href) {
                return;
            }
            if (anchor.getAttribute("target") === "_blank" || anchor.hasAttribute("download")) {
                return;
            }
            var isSort = anchor.classList.contains("am-sort-link");
            var isPage = !!(anchor.closest(".pagination") || anchor.closest(".am-pagination"));
            if (!isSort && !isPage) {
                return;
            }
            if (anchor.getAttribute("href") === "#") {
                event.preventDefault();
                return;
            }
            event.preventDefault();
            debouncedFetch.cancel();
            // Sort/pagination URLs already encode the full query (incl. page).
            fetchResults({ fullUrl: anchor.href, resetPage: false, urlQuery: "" });
        });
    }

    function boot() {
        document.querySelectorAll(FORM_SELECTOR).forEach(initForm);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", boot);
    } else {
        boot();
    }

    global.AmLiveFilter = {
        boot: boot,
        refresh: function (form) {
            if (form) {
                initForm(form);
            } else {
                boot();
            }
        }
    };
})(window);