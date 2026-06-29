/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    var ASSET_EMOJIS = [
        "\uD83D\uDCBB", // laptop
        "\uD83D\uDDA5\uFE0F", // desktop monitor
        "\uD83D\uDCF1", // mobile phone
        "\u2328\uFE0F", // keyboard
        "\uD83D\uDDB1\uFE0F", // mouse
        "\uD83D\uDDA8\uFE0F", // printer
        "\uD83D\uDCFA", // television / display
        "\uD83D\uDCE1", // network / router
        "\uD83D\uDD0B", // battery / UPS
        "\uD83D\uDD27", // maintenance tool
        "\uD83D\uDEE0\uFE0F", // tools
        "\uD83E\uDDF0", // toolbox
        "\u2699\uFE0F", // equipment / machinery
        "\uD83D\uDD2C", // lab microscope
        "\uD83D\uDCCF", // measuring equipment
        "\uD83D\uDCF7", // camera
        "\uD83D\uDCA1", // light fixture
        "\uD83E\uDE91", // chair
        "\uD83D\uDECB\uFE0F", // sofa / furniture
        "\uD83D\uDDC3\uFE0F", // filing cabinet
        "\uD83D\uDCE6", // boxed inventory item
        "\uD83D\uDCCB", // clipboard / records
        "\uD83C\uDFF7\uFE0F", // asset tag / label
        "\uD83D\uDE97", // fleet car
        "\uD83D\uDE9B", // fleet truck
        "\uD83D\uDE8B", // fleet bus
        "\uD83D\uDE9C"  // tractor / heavy equipment
    ];

    var EMOJI_FONT = "\"Segoe UI Emoji\", \"Apple Color Emoji\", \"Noto Color Emoji\", \"Twemoji Mozilla\", sans-serif";
    var EDGE_PADDING = 36;
    var FAR_EDGE = 160;

    function pickEmoji() {
        return ASSET_EMOJIS[Math.floor(Math.random() * ASSET_EMOJIS.length)];
    }

    function isDarkTheme() {
        return global.document.body
            && global.document.body.classList.contains("am-auth-theme-dark");
    }

    function getThemePalette() {
        if (isDarkTheme()) {
            return {
                baseFill: "#0b0f1a",
                trailFill: "rgba(11, 15, 26, 0.18)"
            };
        }

        return {
            baseFill: "#eef3f9",
            trailFill: "rgba(238, 243, 249, 0.42)"
        };
    }

    function prefersReducedMotion() {
        return global.matchMedia
            && global.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

    function getAmbientScale() {
        return prefersReducedMotion() ? 0.75 : 1;
    }

    function drawEmoji(ctx, emoji, x, y, size, opacity, rotation) {
        ctx.save();
        ctx.translate(x, y);
        ctx.rotate(rotation || 0);
        ctx.globalAlpha = opacity;
        ctx.font = size + "px " + EMOJI_FONT;
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillText(emoji, 0, 0);
        ctx.restore();
    }

    function randomInView(width, height, padding) {
        return {
            x: padding + Math.random() * (width - padding * 2),
            y: padding + Math.random() * (height - padding * 2)
        };
    }

    function initAnimatedIcons(canvas) {
        if (!canvas || !canvas.getContext) {
            return null;
        }

        var ctx = canvas.getContext("2d");
        var mouse = { x: 0, y: 0, active: false };
        var particles = [];
        var animationId = null;
        var particleCount = 80;
        var ambientScale = getAmbientScale();

        function AssetEmoji() {
            var spawn = randomInView(canvas.width, canvas.height, 72);
            this.x = spawn.x;
            this.y = spawn.y;
            this.emoji = pickEmoji();
            this.size = Math.random() * 18 + 26;
            this.opacity = Math.random() * 0.25 + 0.75;
            this.rotation = (Math.random() - 0.5) * 0.35;
            this.rotationSpeed = (Math.random() - 0.5) * 0.004 * ambientScale;
            this.heading = Math.random() * Math.PI * 2;
            this.cruiseSpeed = (0.42 + Math.random() * 0.48) * ambientScale;
            this.wanderBias = (Math.random() - 0.5) * 0.016;
            this.vx = Math.cos(this.heading) * this.cruiseSpeed;
            this.vy = Math.sin(this.heading) * this.cruiseSpeed;
        }

        AssetEmoji.prototype.enterFromEdge = function () {
            var width = canvas.width;
            var height = canvas.height;
            var edge = Math.floor(Math.random() * 4);

            if (edge === 0) {
                this.x = Math.random() * width;
                this.y = -24;
                this.heading = Math.PI / 2 + (Math.random() - 0.5) * 0.9;
            } else if (edge === 1) {
                this.x = width + 24;
                this.y = Math.random() * height;
                this.heading = Math.PI + (Math.random() - 0.5) * 0.9;
            } else if (edge === 2) {
                this.x = Math.random() * width;
                this.y = height + 24;
                this.heading = -Math.PI / 2 + (Math.random() - 0.5) * 0.9;
            } else {
                this.x = -24;
                this.y = Math.random() * height;
                this.heading = (Math.random() - 0.5) * 0.9;
            }

            this.cruiseSpeed = (0.42 + Math.random() * 0.48) * ambientScale;
            this.vx = Math.cos(this.heading) * this.cruiseSpeed;
            this.vy = Math.sin(this.heading) * this.cruiseSpeed;
        };

        AssetEmoji.prototype.applyWander = function () {
            this.heading += this.wanderBias + (Math.random() - 0.5) * 0.014;

            var desiredVx = Math.cos(this.heading) * this.cruiseSpeed;
            var desiredVy = Math.sin(this.heading) * this.cruiseSpeed;
            this.vx += (desiredVx - this.vx) * 0.035;
            this.vy += (desiredVy - this.vy) * 0.035;
        };

        AssetEmoji.prototype.applyPointerRepulsion = function () {
            if (!mouse.active) {
                return;
            }

            var dx = this.x - mouse.x;
            var dy = this.y - mouse.y;
            var dist = Math.sqrt(dx * dx + dy * dy);
            if (dist <= 0) {
                return;
            }

            var maxDist = 190;
            var force = (maxDist - dist) / maxDist;
            if (force <= 0) {
                return;
            }

            var push = force * 3.2;
            this.vx += (dx / dist) * push;
            this.vy += (dy / dist) * push;
        };

        AssetEmoji.prototype.clampSpeed = function () {
            var speed = Math.sqrt(this.vx * this.vx + this.vy * this.vy);
            var minSpeed = 0.28 * ambientScale;
            var maxSpeed = 2.6;

            if (speed > maxSpeed) {
                this.vx = (this.vx / speed) * maxSpeed;
                this.vy = (this.vy / speed) * maxSpeed;
                speed = maxSpeed;
            }

            if (speed < minSpeed) {
                if (speed > 0.001) {
                    this.vx = (this.vx / speed) * minSpeed;
                    this.vy = (this.vy / speed) * minSpeed;
                    this.heading = Math.atan2(this.vy, this.vx);
                } else {
                    this.heading = Math.random() * Math.PI * 2;
                    this.vx = Math.cos(this.heading) * minSpeed;
                    this.vy = Math.sin(this.heading) * minSpeed;
                }
            } else {
                this.heading = Math.atan2(this.vy, this.vx);
            }
        };

        AssetEmoji.prototype.enforceBounds = function () {
            var width = canvas.width;
            var height = canvas.height;
            var minX = EDGE_PADDING;
            var maxX = width - EDGE_PADDING;
            var minY = EDGE_PADDING;
            var maxY = height - EDGE_PADDING;

            if (this.x < minX) {
                this.x = minX;
                if (this.vx < 0) {
                    this.vx = Math.abs(this.vx) * 0.72;
                }
            } else if (this.x > maxX) {
                this.x = maxX;
                if (this.vx > 0) {
                    this.vx = -Math.abs(this.vx) * 0.72;
                }
            }

            if (this.y < minY) {
                this.y = minY;
                if (this.vy < 0) {
                    this.vy = Math.abs(this.vy) * 0.72;
                }
            } else if (this.y > maxY) {
                this.y = maxY;
                if (this.vy > 0) {
                    this.vy = -Math.abs(this.vy) * 0.72;
                }
            }

            var farOff = this.x < -FAR_EDGE
                || this.x > width + FAR_EDGE
                || this.y < -FAR_EDGE
                || this.y > height + FAR_EDGE;

            if (farOff) {
                this.enterFromEdge();
            }
        };

        AssetEmoji.prototype.update = function () {
            this.applyWander();
            this.applyPointerRepulsion();

            this.vx *= 0.988;
            this.vy *= 0.988;
            this.clampSpeed();

            this.x += this.vx;
            this.y += this.vy;
            this.rotation += this.rotationSpeed;
            this.enforceBounds();
        };

        AssetEmoji.prototype.draw = function () {
            drawEmoji(ctx, this.emoji, this.x, this.y, this.size, this.opacity, this.rotation);
        };

        function resizeCanvas() {
            canvas.width = global.innerWidth;
            canvas.height = global.innerHeight;

            for (var i = 0; i < particles.length; i++) {
                var particle = particles[i];
                if (particle.x > canvas.width + FAR_EDGE || particle.y > canvas.height + FAR_EDGE) {
                    particle.enterFromEdge();
                }
            }
        }

        function animate() {
            var palette = getThemePalette();
            ctx.fillStyle = palette.trailFill;
            ctx.fillRect(0, 0, canvas.width, canvas.height);

            for (var i = 0; i < particles.length; i++) {
                particles[i].update();
                particles[i].draw();
            }

            ctx.globalAlpha = 1;
            animationId = global.requestAnimationFrame(animate);
        }

        function onPointerMove(clientX, clientY) {
            mouse.x = clientX;
            mouse.y = clientY;
            mouse.active = true;
        }

        function onMouseMove(event) {
            onPointerMove(event.clientX, event.clientY);
        }

        function onTouchMove(event) {
            if (!event.touches || !event.touches.length) {
                return;
            }

            onPointerMove(event.touches[0].clientX, event.touches[0].clientY);
        }

        resizeCanvas();
        particles = [];
        for (var i = 0; i < particleCount; i++) {
            particles.push(new AssetEmoji());
        }

        var palette = getThemePalette();
        ctx.fillStyle = palette.baseFill;
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        global.addEventListener("mousemove", onMouseMove);
        global.addEventListener("touchmove", onTouchMove, { passive: true });
        global.addEventListener("resize", resizeCanvas);
        animate();

        return function destroy() {
            if (animationId) {
                global.cancelAnimationFrame(animationId);
                animationId = null;
            }
            global.removeEventListener("mousemove", onMouseMove);
            global.removeEventListener("touchmove", onTouchMove);
            global.removeEventListener("resize", resizeCanvas);
        };
    }

    function initAntiGravityBubbles(canvas) {
        if (!canvas || canvas.amAntiGravityInitialized) {
            return;
        }

        if (canvas.amAntiGravityDestroy) {
            canvas.amAntiGravityDestroy();
        }

        canvas.amAntiGravityDestroy = initAnimatedIcons(canvas);
        canvas.amAntiGravityInitialized = true;
    }

    function destroyCanvas(canvas) {
        if (!canvas) {
            return;
        }

        if (canvas.amAntiGravityDestroy) {
            canvas.amAntiGravityDestroy();
            canvas.amAntiGravityDestroy = null;
        }

        canvas.amAntiGravityInitialized = false;
    }

    function refreshTheme() {
        destroyCanvas(global.document.getElementById("amAntiGravityCanvas"));

        var supportCanvas = global.document.getElementById("amSupportSessionCanvas");
        var supportVisible = supportCanvas
            && supportCanvas.closest(".am-support-session-overlay.is-visible");
        destroyCanvas(supportCanvas);

        bootLoginCanvas();

        if (supportVisible && supportCanvas) {
            initAntiGravityBubbles(supportCanvas);
        }
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
