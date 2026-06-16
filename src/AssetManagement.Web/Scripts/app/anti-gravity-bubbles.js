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

    function initAntiGravityBubbles(canvas) {
        if (!canvas || !canvas.getContext) {
            return;
        }

        if (global.matchMedia && global.matchMedia("(prefers-reduced-motion: reduce)").matches) {
            return;
        }

        var ctx = canvas.getContext("2d");
        var mouse = { x: 0, y: 0 };
        var particles = [];
        var animationId = null;
        var particleCount = 80;

        function pickEmoji() {
            return ASSET_EMOJIS[Math.floor(Math.random() * ASSET_EMOJIS.length)];
        }

        function resizeCanvas() {
            canvas.width = global.innerWidth;
            canvas.height = global.innerHeight;
            mouse.x = canvas.width / 2;
            mouse.y = canvas.height / 2;
        }

        function AssetEmoji() {
            this.x = Math.random() * canvas.width;
            this.y = Math.random() * canvas.height;
            this.emoji = pickEmoji();
            this.size = Math.random() * 18 + 26;
            this.opacity = Math.random() * 0.25 + 0.75;
            this.rotation = (Math.random() - 0.5) * 0.35;
            this.rotationSpeed = (Math.random() - 0.5) * 0.006;
            this.vx = (Math.random() - 0.5) * 1.4;
            this.vy = (Math.random() - 0.5) * 1.4;
        }

        AssetEmoji.prototype.update = function () {
            var dx = this.x - mouse.x;
            var dy = this.y - mouse.y;
            var dist = Math.sqrt(dx * dx + dy * dy);

            if (dist > 0) {
                var forceDirectionX = dx / dist;
                var forceDirectionY = dy / dist;
                var maxDist = 150;
                var force = (maxDist - dist) / maxDist;

                if (force < 0) {
                    force = 0;
                }

                this.vx += forceDirectionX * force * 2.4;
                this.vy += forceDirectionY * force * 2.4;
            }

            this.vx *= 0.91;
            this.vy *= 0.91;
            this.x += this.vx;
            this.y += this.vy;
            this.rotation += this.rotationSpeed;

            if (this.x < -40) {
                this.x = canvas.width + 40;
            }
            if (this.x > canvas.width + 40) {
                this.x = -40;
            }
            if (this.y < -40) {
                this.y = canvas.height + 40;
            }
            if (this.y > canvas.height + 40) {
                this.y = -40;
            }
        };

        AssetEmoji.prototype.draw = function () {
            ctx.save();
            ctx.translate(this.x, this.y);
            ctx.rotate(this.rotation);
            ctx.globalAlpha = this.opacity;
            ctx.font = this.size + "px " + EMOJI_FONT;
            ctx.textAlign = "center";
            ctx.textBaseline = "middle";
            ctx.fillText(this.emoji, 0, 0);
            ctx.restore();
        };

        function animate() {
            ctx.fillStyle = "rgba(11, 15, 26, 0.18)";
            ctx.fillRect(0, 0, canvas.width, canvas.height);

            for (var i = 0; i < particles.length; i++) {
                particles[i].update();
                particles[i].draw();
            }

            ctx.globalAlpha = 1;
            animationId = global.requestAnimationFrame(animate);
        }

        function onMouseMove(event) {
            mouse.x = event.clientX;
            mouse.y = event.clientY;
        }

        function destroy() {
            if (animationId) {
                global.cancelAnimationFrame(animationId);
                animationId = null;
            }
            global.removeEventListener("mousemove", onMouseMove);
            global.removeEventListener("resize", resizeCanvas);
        }

        resizeCanvas();
        particles = [];
        for (var i = 0; i < particleCount; i++) {
            particles.push(new AssetEmoji());
        }

        ctx.fillStyle = "#0b0f1a";
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        global.addEventListener("mousemove", onMouseMove);
        global.addEventListener("resize", resizeCanvas);
        animate();

        canvas.amAntiGravityDestroy = destroy;
    }

    global.AmAntiGravityBubbles = {
        init: initAntiGravityBubbles
    };

    global.document.addEventListener("DOMContentLoaded", function () {
        var canvas = global.document.getElementById("amAntiGravityCanvas");
        if (canvas) {
            initAntiGravityBubbles(canvas);
        }
    });
})(window);
