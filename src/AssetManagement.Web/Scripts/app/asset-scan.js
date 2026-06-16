(function (window) {
    var config = {};

    function normalizeCode(raw) {
        if (!raw) {
            return "";
        }

        var trimmed = raw.trim();
        if (trimmed.indexOf("://") === -1 && trimmed.indexOf("?") === -1 && trimmed.indexOf("/") !== 0) {
            return trimmed;
        }

        try {
            var url = trimmed.indexOf("://") >= 0
                ? new URL(trimmed)
                : new URL(trimmed, window.location.origin);
            var code = url.searchParams.get("code");
            return code ? code.trim() : trimmed;
        } catch (e) {
            return trimmed;
        }
    }

    function toLookupKey(raw) {
        var normalized = normalizeCode(raw);
        if (!normalized) {
            return "";
        }

        var key = "";
        for (var i = 0; i < normalized.length; i++) {
            var character = normalized.charAt(i);
            if (character === " " || character === "\t" || character === "\n" || character === "\r" || character === "-") {
                continue;
            }

            key += character.toUpperCase();
        }

        return key;
    }

    function escapeHtml(text) {
        if (!text) {
            return "";
        }

        var div = document.createElement("div");
        div.textContent = text;
        return div.innerHTML;
    }

    function displayText(value, emptyLabel) {
        if (value && String(value).trim()) {
            return escapeHtml(value);
        }

        return escapeHtml(emptyLabel || "");
    }

    function renderResult(data) {
        if (data.Found) {
            var actions = "";
            if (data.CanManageAsset) {
                actions =
                    '<div class="am-scan-result-actions mt-4">' +
                    '<a class="btn btn-primary btn-lg w-100" href="' + escapeHtml(data.QuickActionsUrl) + '">' +
                    '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" style="margin-right:0.4rem;vertical-align:-2px;" aria-hidden="true"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"/></svg>' +
                    "Quick actions</a>" +
                    '<a class="btn btn-outline-secondary w-100 mt-2" href="' + escapeHtml(data.DetailsUrl) + '">' +
                    '<svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" style="margin-right:0.4rem;vertical-align:-2px;" aria-hidden="true"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/></svg>' +
                    "Full details</a>" +
                    "</div>";
            }

            return (
                '<div class="am-scan-result card card-kpi mt-4 am-fade-in border-0">' +
                '<div class="am-scan-result-header">' +
                '<div class="am-scan-result-icon" aria-hidden="true"><svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"/></svg></div>' +
                '<div class="am-scan-result-title-wrap">' +
                '<h5 class="am-scan-result-name mb-0">' + escapeHtml(data.AssetName) + "</h5>" +
                '<span class="am-tag-pill am-tag-pill--sm mt-1">' + escapeHtml(data.AssetTag) + "</span>" +
                "</div>" +
                '<span class="badge bg-' + escapeHtml(data.StatusBadgeClass) + ' badge-status ms-auto">' + escapeHtml(data.CurrentStatus) + "</span>" +
                "</div>" +
                '<div class="am-scan-result-body">' +
                '<dl class="am-scan-details-grid">' +
                '<div class="am-scan-detail-row"><dt>Custodian</dt><dd>' + displayText(data.CustodianName, data.EmptyDisplay) + "</dd></div>" +
                '<div class="am-scan-detail-row"><dt>Department</dt><dd>' + displayText(data.DepartmentName, data.EmptyDisplay) + "</dd></div>" +
                '<div class="am-scan-detail-row"><dt>Category</dt><dd>' + displayText(data.CategoryName, data.EmptyDisplay) + "</dd></div>" +
                '<div class="am-scan-detail-row"><dt>Serial No.</dt><dd>' + displayText(data.SerialNumber, data.EmptyDisplay) + "</dd></div>" +
                '<div class="am-scan-detail-row"><dt>Brand / Model</dt><dd>' + displayText(data.BrandModelDisplay, data.EmptyDisplay) + "</dd></div>" +
                "</dl>" +
                actions +
                "</div></div>"
            );
        }

        if (data.Message) {
            return (
                '<div class="am-scan-no-result mt-4 am-fade-in">' +
                '<div class="am-scan-no-result-icon" aria-hidden="true"><svg xmlns="http://www.w3.org/2000/svg" width="28" height="28" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/><line x1="8" y1="11" x2="14" y2="11"/></svg></div>' +
                '<p class="am-scan-no-result-text">' + escapeHtml(data.Message) + "</p>" +
                '<p class="am-scan-no-result-hint">Try scanning again or check the asset tag manually.</p>' +
                "</div>"
            );
        }

        return "";
    }

    function setStatus(text, state) {
        var status = document.querySelector(config.statusSelector);
        if (!status) {
            return;
        }

        var textEl = status.querySelector(".am-scan-status-text");
        if (textEl) {
            textEl.textContent = text;
        } else {
            status.textContent = text;
        }
        status.className = "am-scan-status am-scan-status--" + (state || "ready");
    }

    function setListening(isListening) {
        var panel = document.querySelector(".am-scan-panel");
        if (!panel) {
            return;
        }

        if (isListening) {
            panel.classList.add("am-scan-panel--listening");
        } else {
            panel.classList.remove("am-scan-panel--listening");
        }
    }

    function refocusInput(clearValue) {
        var input = document.querySelector(config.inputSelector);
        if (!input) {
            return;
        }

        if (clearValue) {
            input.value = "";
        }

        input.focus();
        input.select();
    }

    function lookup(rawCode) {
        var lookupKey = toLookupKey(rawCode);
        if (!lookupKey) {
            setStatus("Enter or scan a code", "ready");
            return;
        }

        setStatus("Looking up asset…", "busy");

        var url = config.lookupJsonUrl + "?code=" + encodeURIComponent(rawCode.trim());
        fetch(url, {
            credentials: "same-origin",
            headers: { Accept: "application/json" }
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Lookup failed");
                }

                return response.json();
            })
            .then(function (data) {
                var host = document.querySelector(config.resultHostSelector);
                if (host) {
                    host.innerHTML = renderResult(data);
                }

                refocusInput(!!data.Found);

                if (data.Found) {
                    setStatus("Asset found — scan next code", "success");
                } else if (data.Message) {
                    setStatus("No match — scan again", "warning");
                } else {
                    setStatus("Ready to scan", "ready");
                }
            })
            .catch(function () {
                setStatus("Lookup failed — try again", "error");
            });
    }

    function init(options) {
        config = options || {};

        var form = document.querySelector(config.formSelector);
        var input = document.querySelector(config.inputSelector);
        if (!form || !input) {
            return;
        }

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            lookup(input.value);
        });

        input.addEventListener("focus", function () {
            setListening(true);
            if (!config.hasInitialResult) {
                setStatus("Ready to scan", "ready");
            }
        });

        input.addEventListener("blur", function () {
            setListening(false);
        });

        input.addEventListener("keydown", function (event) {
            if (event.key === "Enter") {
                event.preventDefault();
                lookup(input.value);
            }
        });

        if (config.hasInitialResult) {
            refocusInput(true);
            setStatus("Asset loaded — scan next code", "success");
        } else {
            var preferManualFocus = !window.matchMedia("(max-width: 767.98px)").matches
                && !("ontouchstart" in window);
            if (preferManualFocus) {
                input.focus();
            }
            setStatus("Ready to scan", "ready");
        }
    }

    window.AmAssetScan = {
        init: init,
        lookup: lookup,
        normalizeCode: normalizeCode,
        toLookupKey: toLookupKey
    };
})(window);
