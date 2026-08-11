(function (global) {
    "use strict";

    var sparkCanvas = null;
    var sparkCtx = null;
    var sparks = [];
    var sparkAnimId = null;
    var prefersReducedMotion = false;

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

    function easeOut(t) {
        return t * (2 - t);
    }

    function ensureSparkCanvas() {
        if (sparkCanvas || prefersReducedMotion) {
            return;
        }

        sparkCanvas = document.createElement("canvas");
        sparkCanvas.className = "am-click-spark-layer";
        sparkCanvas.setAttribute("aria-hidden", "true");
        document.body.appendChild(sparkCanvas);
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
        if (prefersReducedMotion) {
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
        if (!sparkCtx || !sparkCanvas) {
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
        var wave = document.createElement("span");
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
        document.addEventListener("click", function (event) {
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

    function initFadeContent() {
        if (prefersReducedMotion) {
            document.querySelectorAll(".am-fade-content").forEach(function (el) {
                el.classList.add("is-visible");
            });
            return;
        }

        if (!("IntersectionObserver" in global)) {
            document.querySelectorAll(".am-fade-content").forEach(function (el) {
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

        document.querySelectorAll(".am-fade-content").forEach(function (el) {
            observer.observe(el);
        });
    }

    function initKpiStagger() {
        document.querySelectorAll(".am-dashboard-kpi-grid").forEach(function (grid) {
            if (grid.classList.contains("am-stagger")) {
                return;
            }
            grid.classList.add("am-stagger");
            assignStaggerIndexes(grid.parentElement || document);
        });
    }

    function init(root) {
        prefersReducedMotion = readReducedMotion();
        root = root || document;
        assignStaggerIndexes(root);
        initKpiStagger();
        initFadeContent();
        initClickSpark();
    }

    global.AmAnimations = {
        init: init,
        burstSparks: burstSparks
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            init(document);
        });
    } else {
        init(document);
    }
})(window);
