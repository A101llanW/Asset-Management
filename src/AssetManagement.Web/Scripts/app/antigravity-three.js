/* eslint-env browser */
/* global window */
(function (global) {
    "use strict";

    var DEFAULT_ASSET_ICONS = [
        "\uD83D\uDCBB", // laptop
        "\uD83D\uDDA5\uFE0F", // desktop
        "\uD83E\uDE91", // chair
        "\uD83C\uDFC0", // basketball
        "\uD83D\uDCF1", // phone
        "\uD83D\uDE97", // car
        "\uD83D\uDD27", // wrench
        "\uD83D\uDECB\uFE0F", // couch
        "\u26BD", // soccer
        "\uD83C\uDFBE", // tennis
        "\uD83D\uDCF7", // camera
        "\uD83D\uDDA8\uFE0F", // printer
        "\u2328\uFE0F", // keyboard
        "\uD83D\uDDB1\uFE0F", // mouse
        "\uD83D\uDCFA", // tv
        "\uD83D\uDEB2", // bicycle
        "\uD83C\uDFD3", // ping pong
        "\uD83C\uDFAF", // target
        "\uD83D\uDD28", // hammer
        "\uD83D\uDCE6", // package
        "\uD83D\uDDC4\uFE0F", // filing cabinet
        "\uD83D\uDCBA", // seat
        "\uD83D\uDD8A\uFE0F", // pen
        "\uD83D\uDCFB" // radio
    ];

    var DEFAULTS = {
        count: 300,
        magnetRadius: 6,
        ringRadius: 7,
        waveSpeed: 0.25,
        waveAmplitude: 0.6,
        particleSize: 0.65,
        lerpSpeed: 0.022,
        color: "#f5c26b",
        autoAnimate: true,
        particleVariance: 1,
        rotationSpeed: 0,
        depthFactor: 1,
        pulseSpeed: 2,
        particleShape: "capsule",
        fieldStrength: 10,
        icons: null,
        iconPixelSize: 64,
        hoverOnlyMagnet: true,
        mouseIdleMs: 400,
        idleDriftSpeed: 0.012,
        idleWanderAmplitude: 0.018,
        influenceRadius: 9,
        homeLerpSpeed: 0.012
    };

    function prefersReducedMotion() {
        return global.matchMedia
            && global.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }

    function mergeOptions(options) {
        var merged = {};
        var key;

        for (key in DEFAULTS) {
            if (Object.prototype.hasOwnProperty.call(DEFAULTS, key)) {
                merged[key] = DEFAULTS[key];
            }
        }

        if (options) {
            for (key in options) {
                if (Object.prototype.hasOwnProperty.call(options, key)) {
                    merged[key] = options[key];
                }
            }
        }

        if (prefersReducedMotion()) {
            merged.count = Math.min(merged.count, 120);
            merged.lerpSpeed = Math.min(merged.lerpSpeed, 0.04);
            merged.autoAnimate = true;
        }

        return merged;
    }

    function resolveAssetIcons(opts) {
        if (opts.icons && opts.icons.length) {
            return opts.icons;
        }

        return DEFAULT_ASSET_ICONS;
    }

    function usesAssetIcons(opts) {
        return opts.particleShape === "asset-icon"
            || (opts.icons && opts.icons.length);
    }

    function createParticleGeometry(THREE, shape) {
        if (shape === "sphere") {
            return new THREE.SphereGeometry(0.15, 8, 8);
        }

        if (shape === "box") {
            return new THREE.BoxGeometry(0.15, 0.15, 0.15);
        }

        if (shape === "tetrahedron") {
            return new THREE.TetrahedronGeometry(0.15);
        }

        return new THREE.CapsuleGeometry(0.08, 0.22, 4, 8);
    }

    function createEmojiTexture(THREE, emoji, pixelSize) {
        var canvas = global.document.createElement("canvas");
        canvas.width = pixelSize;
        canvas.height = pixelSize;
        var ctx = canvas.getContext("2d");

        ctx.clearRect(0, 0, pixelSize, pixelSize);
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.font = Math.round(pixelSize * 0.72) + "px \"Segoe UI Emoji\", \"Apple Color Emoji\", \"Noto Color Emoji\", sans-serif";
        ctx.fillText(emoji, pixelSize / 2, pixelSize / 2 + pixelSize * 0.04);

        var texture = new THREE.CanvasTexture(canvas);
        texture.needsUpdate = true;
        return texture;
    }

    function buildIconTextureCache(THREE, icons, pixelSize) {
        var cache = {};
        var i;

        for (i = 0; i < icons.length; i++) {
            if (!Object.prototype.hasOwnProperty.call(cache, icons[i])) {
                cache[icons[i]] = createEmojiTexture(THREE, icons[i], pixelSize);
            }
        }

        return cache;
    }

    function createAssetIconSprites(THREE, scene, opts, particles) {
        var icons = resolveAssetIcons(opts);
        var textureCache = buildIconTextureCache(THREE, icons, opts.iconPixelSize);
        var sprites = [];
        var i;
        var iconIndex;
        var icon;
        var material;
        var sprite;

        for (i = 0; i < particles.length; i++) {
            iconIndex = i % icons.length;
            if (Math.random() < 0.35) {
                iconIndex = Math.floor(Math.random() * icons.length);
            }

            icon = icons[iconIndex];
            material = new THREE.SpriteMaterial({
                map: textureCache[icon],
                transparent: true,
                depthWrite: false,
                color: 0xffffff
            });
            sprite = new THREE.Sprite(material);
            sprite.userData.icon = icon;
            scene.add(sprite);
            sprites.push(sprite);
        }

        return {
            sprites: sprites,
            textureCache: textureCache
        };
    }

    function initAntigravity(canvas, options) {
        if (!canvas || !global.THREE) {
            return null;
        }

        var THREE = global.THREE;
        var opts = mergeOptions(options);
        var assetIconMode = usesAssetIcons(opts);
        var viewport = { width: 100, height: 100 };
        var pointer = { x: 0, y: 0 };
        var lastMousePos = { x: 0, y: 0 };
        var lastMouseMoveTime = 0;
        var isHovering = false;
        var lastClientX = null;
        var lastClientY = null;
        var virtualMouse = { x: 0, y: 0 };
        var animationId = null;
        var clock = new THREE.Clock();
        var dummy = new THREE.Object3D();
        var trackTarget = global.document;

        var scene = new THREE.Scene();
        var camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 1000);
        camera.position.z = 50;

        var renderer = new THREE.WebGLRenderer({
            canvas: canvas,
            alpha: true,
            antialias: true,
            powerPreference: "high-performance"
        });
        renderer.setPixelRatio(Math.min(global.devicePixelRatio || 1, 2));
        renderer.setClearColor(0x000000, 0);

        var geometry = null;
        var material = null;
        var mesh = null;
        var iconLayer = null;

        if (!assetIconMode) {
            geometry = createParticleGeometry(THREE, opts.particleShape);
            material = new THREE.MeshBasicMaterial({ color: opts.color });
            mesh = new THREE.InstancedMesh(geometry, material, opts.count);
            scene.add(mesh);
        }

        var particles = [];
        var i;

        function createParticles() {
            particles = [];
            for (i = 0; i < opts.count; i++) {
                var x = (Math.random() - 0.5) * viewport.width;
                var y = (Math.random() - 0.5) * viewport.height;
                var z = (Math.random() - 0.5) * 20;

                particles.push({
                    t: Math.random() * 100,
                    speed: 0.004 + Math.random() / 400,
                    mx: x,
                    my: y,
                    mz: z,
                    cx: x,
                    cy: y,
                    cz: z,
                    vx: (Math.random() - 0.5) * opts.idleDriftSpeed * 2,
                    vy: (Math.random() - 0.5) * opts.idleDriftSpeed * 2,
                    wanderPhase: Math.random() * Math.PI * 2,
                    wanderSpeed: 0.2 + Math.random() * 0.45,
                    randomRadiusOffset: (Math.random() - 0.5) * 2
                });
            }

            if (assetIconMode) {
                iconLayer = createAssetIconSprites(THREE, scene, opts, particles);
            }
        }

        function updateViewport() {
            var width = canvas.clientWidth || global.innerWidth;
            var height = canvas.clientHeight || global.innerHeight;
            var aspect = width / height;
            var viewHeight = 20;

            camera.left = -viewHeight * aspect / 2;
            camera.right = viewHeight * aspect / 2;
            camera.top = viewHeight / 2;
            camera.bottom = -viewHeight / 2;
            camera.updateProjectionMatrix();

            renderer.setSize(width, height, false);
            viewport.width = viewHeight * aspect;
            viewport.height = viewHeight;
        }

        function setPointerFromClient(clientX, clientY) {
            var rect = canvas.getBoundingClientRect();
            if (!rect.width || !rect.height) {
                return;
            }

            if (lastClientX !== null && lastClientY !== null) {
                var pixelDist = Math.sqrt(
                    Math.pow(clientX - lastClientX, 2) + Math.pow(clientY - lastClientY, 2)
                );

                if (pixelDist > 0.5) {
                    lastMouseMoveTime = Date.now();
                }
            } else {
                lastMouseMoveTime = Date.now();
            }

            lastClientX = clientX;
            lastClientY = clientY;

            pointer.x = ((clientX - rect.left) / rect.width) * 2 - 1;
            pointer.y = -((clientY - rect.top) / rect.height) * 2 + 1;
            lastMousePos.x = pointer.x;
            lastMousePos.y = pointer.y;
        }

        function onMouseEnter() {
            isHovering = true;
        }

        function onMouseLeave(event) {
            if (event.relatedTarget) {
                return;
            }

            isHovering = false;
            lastMouseMoveTime = 0;
        }

        function onMouseMove(event) {
            isHovering = true;
            setPointerFromClient(event.clientX, event.clientY);
        }

        function onTouchStart(event) {
            isHovering = true;
            if (event.touches && event.touches.length) {
                setPointerFromClient(event.touches[0].clientX, event.touches[0].clientY);
            }
        }

        function onTouchMove(event) {
            if (!event.touches || !event.touches.length) {
                return;
            }

            isHovering = true;
            setPointerFromClient(event.touches[0].clientX, event.touches[0].clientY);
        }

        function onTouchEnd() {
            isHovering = false;
            lastMouseMoveTime = 0;
        }

        function isMouseActive() {
            if (!isHovering) {
                return false;
            }

            if (!lastMouseMoveTime) {
                return false;
            }

            return Date.now() - lastMouseMoveTime < opts.mouseIdleMs;
        }

        function isInMagnetRange(distToCursor, distHomeToCursor) {
            var influenceLimit = opts.influenceRadius || opts.magnetRadius;
            return distToCursor < influenceLimit || distHomeToCursor < opts.magnetRadius;
        }

        function magnetStrength(distToCursor, distHomeToCursor) {
            var influenceLimit = opts.influenceRadius || opts.magnetRadius;
            var cursorFactor = distToCursor < influenceLimit
                ? 1 - (distToCursor / influenceLimit)
                : 0;
            var homeFactor = distHomeToCursor < opts.magnetRadius
                ? 1 - (distHomeToCursor / opts.magnetRadius)
                : 0;

            return Math.max(0.2, Math.min(1, Math.max(cursorFactor, homeFactor * 0.85)));
        }

        function applyReturnHome(particle, elapsed) {
            var wanderX = Math.sin(elapsed * particle.wanderSpeed + particle.wanderPhase)
                * opts.idleWanderAmplitude * 0.4;
            var wanderY = Math.cos(elapsed * particle.wanderSpeed * 0.73 + particle.wanderPhase * 1.3)
                * opts.idleWanderAmplitude * 0.4;
            var homeX = particle.mx + wanderX;
            var homeY = particle.my + wanderY;
            var homeZ = particle.mz * opts.depthFactor
                + Math.sin(elapsed * particle.wanderSpeed * 0.5 + particle.wanderPhase) * 0.15;

            particle.cx += (homeX - particle.cx) * opts.homeLerpSpeed;
            particle.cy += (homeY - particle.cy) * opts.homeLerpSpeed;
            particle.cz += (homeZ - particle.cz) * opts.homeLerpSpeed;
        }

        function applyIdleDrift(particle, elapsed) {
            var halfW = viewport.width / 2;
            var halfH = viewport.height / 2;
            var wanderX = Math.sin(elapsed * particle.wanderSpeed + particle.wanderPhase)
                * opts.idleWanderAmplitude;
            var wanderY = Math.cos(elapsed * particle.wanderSpeed * 0.73 + particle.wanderPhase * 1.3)
                * opts.idleWanderAmplitude;

            particle.cx += particle.vx + wanderX;
            particle.cy += particle.vy + wanderY;

            if (particle.cx > halfW || particle.cx < -halfW) {
                particle.vx *= -1;
                particle.cx = Math.max(-halfW, Math.min(halfW, particle.cx));
            }

            if (particle.cy > halfH || particle.cy < -halfH) {
                particle.vy *= -1;
                particle.cy = Math.max(-halfH, Math.min(halfH, particle.cy));
            }

            particle.cz += Math.sin(elapsed * particle.wanderSpeed * 0.5 + particle.wanderPhase) * 0.004;
            particle.cz = Math.max(-8, Math.min(8, particle.cz));
        }

        function updateParticleVisual(particle, t, projectedTargetX, projectedTargetY, sprite) {
            var currentDistToMouse = Math.sqrt(
                Math.pow(particle.cx - projectedTargetX, 2)
                + Math.pow(particle.cy - projectedTargetY, 2)
            );
            var distFromRing = Math.abs(currentDistToMouse - opts.ringRadius);
            var scaleFactor = 1 - distFromRing / 10;
            scaleFactor = Math.max(0, Math.min(1, scaleFactor));

            var pulse = assetIconMode
                ? 0.92 + Math.sin(t * opts.pulseSpeed) * 0.06 * opts.particleVariance
                : 0.8 + Math.sin(t * opts.pulseSpeed) * 0.2 * opts.particleVariance;
            var finalScale = (assetIconMode ? opts.particleSize : scaleFactor * opts.particleSize)
                * (assetIconMode ? pulse : pulse * scaleFactor);

            if (sprite) {
                sprite.position.set(particle.cx, particle.cy, particle.cz);
                sprite.scale.set(finalScale, finalScale, 1);
                sprite.material.opacity = assetIconMode
                    ? Math.max(0.5, Math.min(0.92, 0.72 + scaleFactor * 0.2))
                    : Math.max(0.35, Math.min(1, 0.35 + scaleFactor * 0.65));
                return;
            }

            dummy.position.set(particle.cx, particle.cy, particle.cz);
            dummy.lookAt(projectedTargetX, projectedTargetY, particle.cz);
            dummy.rotateX(Math.PI / 2);
            dummy.scale.set(finalScale, finalScale, finalScale);
            dummy.updateMatrix();
        }

        function animate() {
            var elapsed = clock.getElapsedTime();
            var magnetActive = !opts.hoverOnlyMagnet || isMouseActive();
            var globalRotation = magnetActive ? elapsed * opts.rotationSpeed : 0;
            var destX = (pointer.x * viewport.width) / 2;
            var destY = (pointer.y * viewport.height) / 2;
            var activeLerp = magnetActive ? opts.lerpSpeed : opts.lerpSpeed * 0.35;
            var targetX;
            var targetY;

            if (magnetActive) {
                virtualMouse.x += (destX - virtualMouse.x) * 0.035;
                virtualMouse.y += (destY - virtualMouse.y) * 0.035;
                targetX = virtualMouse.x;
                targetY = virtualMouse.y;
            } else if (opts.autoAnimate && !opts.hoverOnlyMagnet && Date.now() - lastMouseMoveTime > 2000) {
                destX = Math.sin(elapsed * 0.5) * (viewport.width / 4);
                destY = Math.cos(elapsed * 0.5 * 2) * (viewport.height / 4);
                virtualMouse.x += (destX - virtualMouse.x) * 0.035;
                virtualMouse.y += (destY - virtualMouse.y) * 0.035;
                targetX = virtualMouse.x;
                targetY = virtualMouse.y;
            } else {
                targetX = virtualMouse.x;
                targetY = virtualMouse.y;
            }

            for (i = 0; i < particles.length; i++) {
                var particle = particles[i];
                var t = particle.t + particle.speed / 2;
                particle.t = t;

                var projectionFactor = 1 - particle.mz / 50;
                var projectedTargetX = targetX * projectionFactor;
                var projectedTargetY = targetY * projectionFactor;
                var dxCurrent = particle.cx - projectedTargetX;
                var dyCurrent = particle.cy - projectedTargetY;
                var distToCursor = Math.sqrt(dxCurrent * dxCurrent + dyCurrent * dyCurrent);
                var dxHome = particle.mx - projectedTargetX;
                var dyHome = particle.my - projectedTargetY;
                var distHomeToCursor = Math.sqrt(dxHome * dxHome + dyHome * dyHome);

                if (magnetActive && isInMagnetRange(distToCursor, distHomeToCursor)) {
                    var angle = Math.atan2(dyHome, dxHome) + globalRotation;
                    var wave = Math.sin(t * opts.waveSpeed + angle) * (0.5 * opts.waveAmplitude);
                    var deviation = particle.randomRadiusOffset * (3 / (opts.fieldStrength + 0.1));
                    var currentRingRadius = opts.ringRadius + wave + deviation;
                    var influence = magnetStrength(distToCursor, distHomeToCursor);
                    var effectiveLerp = activeLerp * influence;
                    var targetPosX = projectedTargetX + currentRingRadius * Math.cos(angle);
                    var targetPosY = projectedTargetY + currentRingRadius * Math.sin(angle);
                    var targetPosZ = particle.mz * opts.depthFactor
                        + Math.sin(t) * (0.5 * opts.waveAmplitude * opts.depthFactor);

                    particle.cx += (targetPosX - particle.cx) * effectiveLerp;
                    particle.cy += (targetPosY - particle.cy) * effectiveLerp;
                    particle.cz += (targetPosZ - particle.cz) * effectiveLerp;
                } else if (magnetActive) {
                    applyReturnHome(particle, elapsed);
                } else {
                    applyIdleDrift(particle, elapsed);
                }

                var sprite = assetIconMode && iconLayer ? iconLayer.sprites[i] : null;
                updateParticleVisual(particle, t, projectedTargetX, projectedTargetY, sprite);

                if (mesh) {
                    mesh.setMatrixAt(i, dummy.matrix);
                }
            }

            if (mesh) {
                mesh.instanceMatrix.needsUpdate = true;
            }

            renderer.render(scene, camera);
            animationId = global.requestAnimationFrame(animate);
        }

        function disposeIconLayer() {
            var textureKey;

            if (!iconLayer) {
                return;
            }

            iconLayer.sprites.forEach(function (sprite) {
                scene.remove(sprite);
                if (sprite.material) {
                    sprite.material.dispose();
                }
            });

            for (textureKey in iconLayer.textureCache) {
                if (Object.prototype.hasOwnProperty.call(iconLayer.textureCache, textureKey)) {
                    iconLayer.textureCache[textureKey].dispose();
                }
            }

            iconLayer = null;
        }

        function onResize() {
            updateViewport();
        }

        updateViewport();
        createParticles();

        trackTarget.addEventListener("mouseenter", onMouseEnter);
        trackTarget.addEventListener("mouseleave", onMouseLeave);
        trackTarget.addEventListener("mousemove", onMouseMove);
        trackTarget.addEventListener("touchstart", onTouchStart, { passive: true });
        trackTarget.addEventListener("touchmove", onTouchMove, { passive: true });
        trackTarget.addEventListener("touchend", onTouchEnd);
        global.addEventListener("resize", onResize);
        animate();

        return function destroy() {
            if (animationId) {
                global.cancelAnimationFrame(animationId);
                animationId = null;
            }

            trackTarget.removeEventListener("mouseenter", onMouseEnter);
            trackTarget.removeEventListener("mouseleave", onMouseLeave);
            trackTarget.removeEventListener("mousemove", onMouseMove);
            trackTarget.removeEventListener("touchstart", onTouchStart);
            trackTarget.removeEventListener("touchmove", onTouchMove);
            trackTarget.removeEventListener("touchend", onTouchEnd);
            global.removeEventListener("resize", onResize);

            disposeIconLayer();

            if (mesh) {
                scene.remove(mesh);
                geometry.dispose();
                material.dispose();
            }

            renderer.dispose();
        };
    }

    function mount(canvas, options) {
        if (!canvas) {
            return null;
        }

        if (canvas.amAntigravityDestroy) {
            canvas.amAntigravityDestroy();
        }

        canvas.amAntigravityDestroy = initAntigravity(canvas, options);
        canvas.amAntigravityInitialized = true;
        return canvas.amAntigravityDestroy;
    }

    function destroyCanvas(canvas) {
        if (!canvas) {
            return;
        }

        if (canvas.amAntigravityDestroy) {
            canvas.amAntigravityDestroy();
            canvas.amAntigravityDestroy = null;
        }

        canvas.amAntigravityInitialized = false;
    }

    global.AmAntigravityThree = {
        init: mount,
        destroy: destroyCanvas,
        defaultAssetIcons: DEFAULT_ASSET_ICONS.slice()
    };
})(window);
