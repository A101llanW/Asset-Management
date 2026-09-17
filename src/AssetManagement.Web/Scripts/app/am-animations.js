(function (global) {
    "use strict";

    var sparkCanvas = null;
    var sparkCtx = null;
    var sparks = [];
    var sparkAnimId = null;
    var prefersReducedMotion = false;
    var motionPaused = false;
    var STAGGER_CAP = 12;

    var defaultSpark = {
        color: "hsla(205, 90%, 45%, 0.85)",
        size: 10,
        radius: 15,
        count: 8,
        duration: 400
    };

    function readReducedMotion() {
        try {
            return global.matchMedia("(prefers-reduced-motion: reduce)").matches;
        } catch (e) {
            return false;
        }
    }

    function easeOutCubic(t) {
        return 1 - Math.pow(1 - t, 3);
    }

    function easeOut(t) {
        return t * (2 - t);
    }

    function isMotionAllowed() {
        return !prefersReducedMotion && !motionPaused;
    }

    function initMotionPause() {
        function syncPauseState() {
            motionPaused = global.document.hidden;
            global.document.documentElement.classList.toggle("am-motion-paused", motionPaused);
        }

        global.document.addEventListener("visibilitychange", syncPauseState);
        syncPauseState();
    }

    function ensureSparkCanvas() {
        if (sparkCanvas || prefersReducedMotion) {
            return;
        }

        sparkCanvas = global.document.createElement("canvas");
        sparkCanvas.className = "am-click-spark-layer";
        sparkCanvas.setAttribute("aria-hidden", "true");
        global.document.body.appendChild(sparkCanvas);
        sparkCtx = sparkCanvas.getContext("2d");
        resizeSparkCanvas();
        global.addEventListener("resize", resizeSparkCanvas);
    }

    function resizeSparkCanvas() {
        if (!sparkCanvas) {
            return;
        }

        sparkCanvas.width = global.innerWidth;
        sparkCanvas.height = global.innerHeight;
    }

    function parseSparkOptions(el) {
        var dataset = el.dataset || {};
        return {
            color: dataset.amSparkColor || defaultSpark.color,
            size: parseFloat(dataset.amSparkSize) || defaultSpark.size,
            radius: parseFloat(dataset.amSparkRadius) || defaultSpark.radius,
            count: parseInt(dataset.amSparkCount, 10) || defaultSpark.count,
            duration: parseInt(dataset.amSparkDuration, 10) || defaultSpark.duration
        };
    }

    function burstSparks(x, y, options) {
        if (!isMotionAllowed()) {
            return;
        }

        ensureSparkCanvas();
        if (!sparkCtx) {
            return;
        }

        var now = performance.now();
        var i;

        for (i = 0; i < options.count; i++) {
            sparks.push({
                x: x,
                y: y,
                angle: (2 * Math.PI * i) / options.count,
                startTime: now,
                color: options.color,
                size: options.size,
                radius: options.radius,
                duration: options.duration
            });
        }

        if (!sparkAnimId) {
            sparkAnimId = global.requestAnimationFrame(drawSparks);
        }
    }

    function drawSparks(timestamp) {
        if (!sparkCtx || !sparkCanvas || motionPaused) {
            sparkAnimId = null;
            return;
        }

        sparkCtx.clearRect(0, 0, sparkCanvas.width, sparkCanvas.height);

        sparks = sparks.filter(function (spark) {
            var elapsed = timestamp - spark.startTime;
            if (elapsed >= spark.duration) {
                return false;
            }

            var progress = elapsed / spark.duration;
            var eased = easeOut(progress);
            var distance = eased * spark.radius;
            var lineLength = spark.size * (1 - eased);
            var x1 = spark.x + distance * Math.cos(spark.angle);
            var y1 = spark.y + distance * Math.sin(spark.angle);
            var x2 = spark.x + (distance + lineLength) * Math.cos(spark.angle);
            var y2 = spark.y + (distance + lineLength) * Math.sin(spark.angle);

            sparkCtx.strokeStyle = spark.color;
            sparkCtx.lineWidth = 2;
            sparkCtx.beginPath();
            sparkCtx.moveTo(x1, y1);
            sparkCtx.lineTo(x2, y2);
            sparkCtx.stroke();
            return true;
        });

        if (sparks.length) {
            sparkAnimId = global.requestAnimationFrame(drawSparks);
        } else {
            sparkAnimId = null;
        }
    }

    function addRipple(el, clientX, clientY) {
        if (prefersReducedMotion) {
            return;
        }

        var rect = el.getBoundingClientRect();
        var size = Math.max(rect.width, rect.height) * 1.6;
        var x = clientX - rect.left - size / 2;
        var y = clientY - rect.top - size / 2;
        var wave = global.document.createElement("span");
        wave.className = "am-click-ripple__wave";
        wave.style.width = size + "px";
        wave.style.height = size + "px";
        wave.style.left = x + "px";
        wave.style.top = y + "px";
        el.appendChild(wave);
        global.setTimeout(function () {
            if (wave.parentNode) {
                wave.parentNode.removeChild(wave);
            }
        }, 500);
    }

    function initClickSpark() {
        global.document.addEventListener("click", function (event) {
            var target = event.target.closest(".am-click-spark, [data-am-click-spark]");
            if (!target || target.disabled || target.getAttribute("aria-disabled") === "true") {
                return;
            }

            if (prefersReducedMotion) {
                if (!target.classList.contains("am-click-ripple")) {
                    target.classList.add("am-click-ripple");
                }
                addRipple(target, event.clientX, event.clientY);
                return;
            }

            burstSparks(event.clientX, event.clientY, parseSparkOptions(target));
        });
    }

    function assignStaggerIndexes(root) {
        root.querySelectorAll(".am-stagger, .am-stagger-list").forEach(function (group) {
            var children = group.children;
            var i;
            for (i = 0; i < children.length; i++) {
                children[i].style.setProperty("--am-stagger-index", String(i));
            }
        });
    }

    function initAnimatedList(root) {
        root.querySelectorAll(".am-animated-list").forEach(function (list) {
            var rows = list.querySelectorAll("tr");
            var i;
            for (i = 0; i < rows.length; i++) {
                if (i < STAGGER_CAP && isMotionAllowed()) {
                    rows[i].classList.add("am-list-animate");
                    rows[i].style.setProperty("--am-stagger-index", String(i));
                } else {
                    rows[i].classList.remove("am-list-animate");
                    rows[i].style.opacity = "1";
                    rows[i].style.transform = "none";
                }
            }
        });
    }

    function initFadeContent(root) {
        var nodes = root.querySelectorAll(".am-fade-content");

        if (prefersReducedMotion) {
            nodes.forEach(function (el) {
                el.classList.add("is-visible");
            });
            return;
        }

        if (!("IntersectionObserver" in global)) {
            nodes.forEach(function (el) {
                el.classList.add("is-visible");
            });
            return;
        }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add("is-visible");
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.12, rootMargin: "0px 0px -5% 0px" });

        nodes.forEach(function (el) {
            observer.observe(el);
        });
    }

    function parseCountValue(raw) {
        var normalized = String(raw || "0").replace(/,/g, "").trim();
        var parsed = parseFloat(normalized);
        return isNaN(parsed) ? 0 : parsed;
    }

    function formatCountValue(value, decimals, suffix, prefix) {
        var formatted = decimals > 0
            ? value.toFixed(decimals)
            : Math.round(value).toString();
        return (prefix || "") + formatted + (suffix || "");
    }

    function initCountUp(root) {
        if (prefersReducedMotion) {
            root.querySelectorAll("[data-am-count-up]").forEach(function (el) {
                var target = parseCountValue(el.getAttribute("data-am-count-value") || el.textContent);
                var decimals = parseInt(el.getAttribute("data-am-count-decimals") || "0", 10);
                var suffix = el.getAttribute("data-am-count-suffix") || "";
                var prefix = el.getAttribute("data-am-count-prefix") || "";
                el.textContent = formatCountValue(target, decimals, suffix, prefix);
                el.classList.add("am-count-up");
            });
            return;
        }

        if (!("IntersectionObserver" in global)) {
            return;
        }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) {
                    return;
                }

                var el = entry.target;
                observer.unobserve(el);

                var target = parseCountValue(el.getAttribute("data-am-count-value") || el.textContent);
                var decimals = parseInt(el.getAttribute("data-am-count-decimals") || "0", 10);
                var suffix = el.getAttribute("data-am-count-suffix") || "";
                var prefix = el.getAttribute("data-am-count-prefix") || "";
                var duration = parseInt(el.getAttribute("data-am-count-duration") || "1200", 10);
                var start = performance.now();

                el.classList.add("am-count-up");

                function tick(now) {
                    if (motionPaused) {
                        global.requestAnimationFrame(tick);
                        return;
                    }

                    var progress = Math.min(1, (now - start) / duration);
                    var current = target * easeOutCubic(progress);
                    el.textContent = formatCountValue(current, decimals, suffix, prefix);

                    if (progress < 1) {
                        global.requestAnimationFrame(tick);
                    } else {
                        el.textContent = formatCountValue(target, decimals, suffix, prefix);
                    }
                }

                global.requestAnimationFrame(tick);
            });
        }, { threshold: 0.35 });

        root.querySelectorAll("[data-am-count-up]").forEach(function (el) {
            observer.observe(el);
        });
    }

    function initSplitText(root) {
        root.querySelectorAll(".am-split-text").forEach(function (el) {
            if (el.dataset.amSplitReady === "true") {
                return;
            }

            var text = el.getAttribute("data-am-split-text") || el.textContent;
            text = (text || "").trim();
            if (!text) {
                return;
            }

            el.dataset.amSplitReady = "true";
            el.setAttribute("aria-label", text);
            el.textContent = "";

            var words = text.split(/\s+/);
            var i;

            for (i = 0; i < words.length; i++) {
                var span = global.document.createElement("span");
                span.className = "am-split-word";
                span.style.setProperty("--am-split-index", String(i));
                span.textContent = words[i];
                el.appendChild(span);
                if (i < words.length - 1) {
                    el.appendChild(global.document.createTextNode(" "));
                }
            }

            if (prefersReducedMotion) {
                el.querySelectorAll(".am-split-word").forEach(function (word) {
                    word.style.opacity = "1";
                    word.style.filter = "none";
                    word.style.transform = "none";
                });
            }
        });
    }

    function initSpotlightCards(root) {
        root.querySelectorAll(".am-spotlight-card").forEach(function (card) {
            if (card.dataset.amSpotlightReady === "true") {
                return;
            }

            card.dataset.amSpotlightReady = "true";

            card.addEventListener("mousemove", function (event) {
                if (!isMotionAllowed()) {
                    return;
                }

                var rect = card.getBoundingClientRect();
                var x = ((event.clientX - rect.left) / rect.width) * 100;
                var y = ((event.clientY - rect.top) / rect.height) * 100;
                card.style.setProperty("--am-spotlight-x", x + "%");
                card.style.setProperty("--am-spotlight-y", y + "%");
            });
        });
    }

    function initTextType(root) {
        if (prefersReducedMotion) {
            root.querySelectorAll(".am-text-type").forEach(function (el) {
                el.textContent = el.getAttribute("data-am-text-type") || el.textContent;
                el.classList.add("is-complete");
            });
            return;
        }

        root.querySelectorAll(".am-text-type").forEach(function (el) {
            if (el.dataset.amTextTypeReady === "true") {
                return;
            }

            var fullText = el.getAttribute("data-am-text-type") || el.textContent || "";
            fullText = fullText.trim();
            if (!fullText) {
                return;
            }

            el.dataset.amTextTypeReady = "true";
            el.textContent = "";
            el.classList.remove("is-complete");

            var index = 0;
            var delay = parseInt(el.getAttribute("data-am-text-type-delay") || "28", 10);

            function typeNext() {
                if (motionPaused) {
                    global.setTimeout(typeNext, delay);
                    return;
                }

                if (index <= fullText.length) {
                    el.textContent = fullText.slice(0, index);
                    index += 1;
                    global.setTimeout(typeNext, delay);
                } else {
                    el.classList.add("is-complete");
                }
            }

            if ("IntersectionObserver" in global) {
                var observer = new IntersectionObserver(function (entries) {
                    entries.forEach(function (entry) {
                        if (entry.isIntersecting) {
                            observer.unobserve(el);
                            typeNext();
                        }
                    });
                }, { threshold: 0.4 });
                observer.observe(el);
            } else {
                typeNext();
            }
        });
    }

    function positionLineSidebarIndicator(sidebar, indicator, link) {
        if (!sidebar || !indicator || !link) {
            return;
        }

        var sidebarRect = sidebar.getBoundingClientRect();
        var linkRect = link.getBoundingClientRect();
        var top = linkRect.top - sidebarRect.top + sidebar.scrollTop;
        indicator.style.transform = "translateY(" + top + "px)";
        indicator.style.height = linkRect.height + "px";
        sidebar.classList.add("has-active");
    }

    function initLineSidebar(root) {
        var sidebarNav = root.querySelector(".am-sidebar-nav");
        if (!sidebarNav) {
            return;
        }

        var menu = sidebarNav.querySelector(".am-sidebar-menu");
        if (!menu) {
            return;
        }

        menu.classList.add("am-line-sidebar");

        var indicator = menu.querySelector(".am-line-sidebar-indicator");
        if (!indicator) {
            indicator = global.document.createElement("span");
            indicator.className = "am-line-sidebar-indicator";
            indicator.setAttribute("aria-hidden", "true");
            menu.insertBefore(indicator, menu.firstChild);
        }

        function syncIndicator() {
            var active = menu.querySelector(".nav-link.active");
            if (active) {
                positionLineSidebarIndicator(menu, indicator, active);
            } else {
                menu.classList.remove("has-active");
                indicator.style.opacity = "0";
            }
        }

        syncIndicator();
        menu.addEventListener("click", function (event) {
            var link = event.target.closest(".nav-link");
            if (link) {
                global.setTimeout(syncIndicator, 0);
            }
        });
        global.addEventListener("resize", syncIndicator);
    }

    function initStepper(root) {
        root.querySelectorAll(".am-stepper[data-am-wizard], .am-wizard.am-stepper").forEach(function (wizard) {
            var steps = wizard.querySelectorAll("[data-am-wizard-step-indicator]");
            if (!steps.length) {
                return;
            }

            if (!wizard.querySelector(".am-stepper-track")) {
                var track = global.document.createElement("div");
                track.className = "am-stepper-track";
                track.setAttribute("aria-hidden", "true");
                var progress = global.document.createElement("div");
                progress.className = "am-stepper-progress";
                progress.setAttribute("aria-hidden", "true");
                track.appendChild(progress);
                wizard.insertBefore(track, wizard.firstChild);
            }

            var progressBar = wizard.querySelector(".am-stepper-progress");

            function syncProgress() {
                var activeIndex = 0;
                var i;
                for (i = 0; i < steps.length; i++) {
                    if (steps[i].classList.contains("active")) {
                        activeIndex = i;
                    }
                    steps[i].classList.toggle("is-complete", i < activeIndex);
                }

                if (progressBar && steps.length > 1) {
                    var pct = (activeIndex / (steps.length - 1)) * 100;
                    progressBar.style.width = pct + "%";
                }
            }

            syncProgress();
            wizard.addEventListener("click", function (event) {
                if (event.target.closest("[data-am-wizard-next], [data-am-wizard-prev]")) {
                    global.setTimeout(syncProgress, 0);
                }
            });
        });
    }

    function initKpiStagger(root) {
        root.querySelectorAll(".am-dashboard-kpi-grid").forEach(function (grid) {
            if (grid.classList.contains("am-stagger")) {
                return;
            }
            grid.classList.add("am-stagger");
            assignStaggerIndexes(grid.parentElement || root);
        });
    }

    function init(root) {
        prefersReducedMotion = readReducedMotion();
        root = root || global.document;

        initMotionPause();
        assignStaggerIndexes(root);
        initKpiStagger(root);
        initAnimatedList(root);
        initFadeContent(root);
        initCountUp(root);
        initSplitText(root);
        initSpotlightCards(root);
        initTextType(root);
        initLineSidebar(root);
        initStepper(root);
        initClickSpark();
    }

    global.AmAnimations = {
        init: init,
        burstSparks: burstSparks
    };

    if (global.document.readyState === "loading") {
        global.document.addEventListener("DOMContentLoaded", function () {
            init(global.document);
        });
    } else {
        init(global.document);
    }
})(window);
