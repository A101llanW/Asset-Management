(function (window) {
    'use strict';

    var MARGIN_MM = 2;
    var DOTS_PER_MM = 8;
    var TARGET_CANVAS_WIDTH_PX = 760;
    var TARGET_CANVAS_HEIGHT_PX = 440;
    var MIN_DISPLAY_SCALE = 3;
    var MAX_DISPLAY_SCALE = 12;
    var MM_PX = 96 / 25.4;

    var FIELD_META = {
        ScanCode: { label: 'Scan code', sample: 'QR', isCode: true, fontHeight: 0 },
        AssetTag: { label: 'Asset tag', sample: 'AST-1001', fontHeight: 24 },
        AssetName: { label: 'Asset name', sample: 'Sample Laptop', fontHeight: 18 },
        DepartmentName: { label: 'Department', sample: 'IT Department', fontHeight: 14 },
        SerialNumber: { label: 'Serial number', sample: 'S/N: SN-123456', fontHeight: 14 }
    };

    var dragState = null;
    var uiState = {
        displayScale: MIN_DISPLAY_SCALE,
        qrMagnification: 3
    };

    function parseDesign(jsonValue, widthMm, heightMm) {
        try {
            if (!jsonValue) {
                return defaultDesign(widthMm, heightMm);
            }

            var parsed = JSON.parse(jsonValue);
            if (!parsed || !parsed.Elements) {
                return defaultDesign(widthMm, heightMm);
            }

            return normalizeDesign(parsed, widthMm, heightMm);
        } catch (e) {
            return defaultDesign(widthMm, heightMm);
        }
    }

    function defaultDesign(widthMm, heightMm) {
        return normalizeDesign({ Elements: [] }, widthMm, heightMm);
    }

    function estimateQrSizeMm(magnification) {
        var mag = parseInt(magnification, 10);
        if (isNaN(mag) || mag <= 0) {
            mag = 3;
        }

        return (25 * mag + 30) / DOTS_PER_MM;
    }

    function estimateTextColumnX(widthMm, qrMagnification) {
        var qrSizeMm = estimateQrSizeMm(qrMagnification);
        return clamp(MARGIN_MM + qrSizeMm + MARGIN_MM, MARGIN_MM, widthMm - MARGIN_MM);
    }

    function defaultPosition(id, widthMm, heightMm, qrMagnification) {
        var textX = Math.round(estimateTextColumnX(widthMm, qrMagnification) * 10) / 10;

        if (id === 'ScanCode') {
            return { x: MARGIN_MM, y: MARGIN_MM };
        }

        if (id === 'AssetTag') {
            return { x: textX, y: MARGIN_MM };
        }

        if (id === 'AssetName') {
            return { x: textX, y: Math.min(heightMm - MARGIN_MM - 4, MARGIN_MM + 8) };
        }

        if (id === 'DepartmentName') {
            return { x: textX, y: Math.min(heightMm - MARGIN_MM - 4, MARGIN_MM + 16) };
        }

        return { x: textX, y: Math.min(heightMm - MARGIN_MM - 4, MARGIN_MM + 24) };
    }

    function normalizeDesign(design, widthMm, heightMm) {
        var ids = ['ScanCode', 'AssetTag', 'AssetName', 'DepartmentName', 'SerialNumber'];
        var byId = {};
        var i;

        for (i = 0; i < (design.Elements || []).length; i++) {
            byId[design.Elements[i].Id] = design.Elements[i];
        }

        var elements = [];
        for (i = 0; i < ids.length; i++) {
            var id = ids[i];
            var source = byId[id] || {};
            var fallback = defaultPosition(id, widthMm, heightMm, uiState.qrMagnification);

            elements.push({
                Id: id,
                Enabled: source.Enabled !== false,
                XMm: clampPosition(source.XMm != null ? source.XMm : fallback.x, widthMm),
                YMm: clampPosition(source.YMm != null ? source.YMm : fallback.y, heightMm),
                FontHeight: source.FontHeight || (FIELD_META[id] ? FIELD_META[id].fontHeight : 0)
            });
        }

        return { Elements: elements };
    }

    function clampPosition(value, maxMm) {
        return clamp(value, MARGIN_MM, Math.max(MARGIN_MM, maxMm - MARGIN_MM));
    }

    function clamp(value, min, max) {
        if (value < min) {
            return min;
        }

        if (value > max) {
            return max;
        }

        return value;
    }

    function snapMm(value) {
        return Math.round(value * 2) / 2;
    }

    function serializeDesign(design) {
        return JSON.stringify(design);
    }

    function getDimensions(widthField, heightField) {
        var widthMm = parseInt(widthField.value, 10);
        var heightMm = parseInt(heightField.value, 10);
        if (isNaN(widthMm) || widthMm <= 0) {
            widthMm = 76;
        }

        if (isNaN(heightMm) || heightMm <= 0) {
            heightMm = 37;
        }

        return { widthMm: widthMm, heightMm: heightMm };
    }

    function getQrMagnification(field) {
        if (!field) {
            return 3;
        }

        var value = parseInt(field.value, 10);
        if (isNaN(value) || value <= 0) {
            return 3;
        }

        return clamp(value, 1, 10);
    }

    function computeDisplayScale(widthMm, heightMm) {
        var nativeWidthPx = widthMm * MM_PX;
        var nativeHeightPx = heightMm * MM_PX;
        var scale = Math.min(
            TARGET_CANVAS_WIDTH_PX / nativeWidthPx,
            TARGET_CANVAS_HEIGHT_PX / nativeHeightPx,
            MAX_DISPLAY_SCALE
        );

        return Math.max(MIN_DISPLAY_SCALE, scale);
    }

    function getPxPerMm(canvas, widthMm) {
        var rect = canvas.getBoundingClientRect();
        if (!rect.width || !widthMm) {
            return MM_PX;
        }

        return rect.width / widthMm;
    }

    function fontHeightToMm(fontHeightDots) {
        if (!fontHeightDots) {
            return 2.5;
        }

        return fontHeightDots / DOTS_PER_MM;
    }

    function renderFieldControls(fieldsRoot, design, onToggle) {
        fieldsRoot.innerHTML = '';
        for (var i = 0; i < design.Elements.length; i++) {
            (function (element) {
                var meta = FIELD_META[element.Id] || { label: element.Id };
                var wrapper = document.createElement('div');
                wrapper.className = 'form-check form-check-inline me-3 mb-2';

                var input = document.createElement('input');
                input.type = 'checkbox';
                input.className = 'form-check-input';
                input.id = 'label-field-' + element.Id;
                input.checked = element.Enabled;
                input.addEventListener('change', function () {
                    element.Enabled = input.checked;
                    onToggle();
                });

                var label = document.createElement('label');
                label.className = 'form-check-label';
                label.setAttribute('for', input.id);
                label.textContent = meta.label;

                wrapper.appendChild(input);
                wrapper.appendChild(label);
                fieldsRoot.appendChild(wrapper);
            })(design.Elements[i]);
        }
    }

    function applyCanvasScale(scaler, canvas, meta, dimensions) {
        var displayScale = computeDisplayScale(dimensions.widthMm, dimensions.heightMm);
        uiState.displayScale = displayScale;

        var nativeWidthPx = dimensions.widthMm * MM_PX;
        var nativeHeightPx = dimensions.heightMm * MM_PX;

        scaler.style.width = Math.round(nativeWidthPx * displayScale) + 'px';
        scaler.style.height = Math.round(nativeHeightPx * displayScale) + 'px';
        canvas.style.width = dimensions.widthMm + 'mm';
        canvas.style.height = dimensions.heightMm + 'mm';
        canvas.style.transform = 'scale(' + displayScale + ')';

        if (meta) {
            meta.textContent = dimensions.widthMm + ' x ' + dimensions.heightMm + ' mm'
                + ' · ' + MARGIN_MM + ' mm margin'
                + ' · preview x' + displayScale.toFixed(1);
        }
    }

    function renderCanvas(scaler, canvas, meta, design, dimensions, sampleData, qrMagnification) {
        canvas.innerHTML = '';

        applyCanvasScale(scaler, canvas, meta, dimensions);

        var marginGuide = document.createElement('div');
        marginGuide.className = 'am-label-designer-margin-guide';
        marginGuide.style.left = MARGIN_MM + 'mm';
        marginGuide.style.top = MARGIN_MM + 'mm';
        marginGuide.style.width = Math.max(0, dimensions.widthMm - (MARGIN_MM * 2)) + 'mm';
        marginGuide.style.height = Math.max(0, dimensions.heightMm - (MARGIN_MM * 2)) + 'mm';
        canvas.appendChild(marginGuide);

        var qrSizeMm = estimateQrSizeMm(qrMagnification);

        for (var i = 0; i < design.Elements.length; i++) {
            var element = design.Elements[i];
            if (!element.Enabled) {
                continue;
            }

            var metaField = FIELD_META[element.Id] || { label: element.Id, sample: element.Id };
            var node = document.createElement('div');
            node.className = 'am-label-designer-item';
            node.setAttribute('data-element-id', element.Id);
            node.style.left = element.XMm + 'mm';
            node.style.top = element.YMm + 'mm';

            if (metaField.isCode) {
                node.className += ' am-label-designer-item--code';
                var codeHost = document.createElement('div');
                codeHost.className = 'am-label-designer-code-host';
                codeHost.style.width = qrSizeMm + 'mm';
                codeHost.style.height = qrSizeMm + 'mm';
                node.appendChild(codeHost);
                renderSampleCode(codeHost, sampleData.scanUrl, sampleData.barcodePayload, 'Qr', qrSizeMm, canvas, dimensions);
            } else {
                var fontHeight = element.FontHeight || metaField.fontHeight || 14;
                node.style.fontSize = fontHeightToMm(fontHeight) + 'mm';
                node.style.lineHeight = '1.15';
                node.textContent = getSampleText(element.Id, sampleData, metaField.sample);
            }

            bindDrag(node, element, canvas, dimensions);
            canvas.appendChild(node);
        }
    }

    function getSampleText(elementId, sampleData, fallback) {
        if (elementId === 'AssetTag') {
            return sampleData.assetTag || fallback;
        }

        if (elementId === 'AssetName') {
            return sampleData.assetName || fallback;
        }

        if (elementId === 'DepartmentName') {
            return sampleData.departmentName || fallback;
        }

        if (elementId === 'SerialNumber') {
            return sampleData.serialNumber || fallback;
        }

        return fallback;
    }

    function renderSampleCode(container, scanUrl, barcodePayload, codeType, qrSizeMm, canvas, dimensions) {
        container.innerHTML = '';
        var pxPerMm = getPxPerMm(canvas, dimensions.widthMm);
        var qrPx = Math.max(32, Math.round(qrSizeMm * pxPerMm));

        if (codeType === 'Barcode') {
            if (!window.JsBarcode || !barcodePayload) {
                container.textContent = 'Barcode';
                return;
            }

            var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            container.appendChild(svg);
            window.JsBarcode(svg, barcodePayload, {
                format: 'CODE128',
                displayValue: false,
                height: Math.round(qrPx * 0.55),
                margin: 0,
                width: 1.5
            });
            return;
        }

        if (!window.QRCode || !scanUrl) {
            container.textContent = 'QR';
            return;
        }

        new window.QRCode(container, {
            text: scanUrl,
            width: qrPx,
            height: qrPx,
            correctLevel: window.QRCode.CorrectLevel.M
        });
    }

    function bindDrag(node, element, canvas, dimensions) {
        function startDrag(clientX, clientY) {
            dragState = {
                node: node,
                element: element,
                canvas: canvas,
                dimensions: dimensions,
                startX: clientX,
                startY: clientY,
                originXMm: element.XMm,
                originYMm: element.YMm
            };
            node.classList.add('am-label-designer-item--dragging');
        }

        node.addEventListener('mousedown', function (event) {
            if (event.button !== 0) {
                return;
            }

            event.preventDefault();
            startDrag(event.clientX, event.clientY);
        });

        node.addEventListener('touchstart', function (event) {
            if (!event.touches || !event.touches.length) {
                return;
            }

            event.preventDefault();
            startDrag(event.touches[0].clientX, event.touches[0].clientY);
        }, { passive: false });
    }

    function onPointerMove(clientX, clientY) {
        if (!dragState) {
            return;
        }

        var pxPerMm = getPxPerMm(dragState.canvas, dragState.dimensions.widthMm);
        var deltaX = (clientX - dragState.startX) / pxPerMm;
        var deltaY = (clientY - dragState.startY) / pxPerMm;

        dragState.element.XMm = clampPosition(dragState.originXMm + deltaX, dragState.dimensions.widthMm);
        dragState.element.YMm = clampPosition(dragState.originYMm + deltaY, dragState.dimensions.heightMm);
        dragState.node.style.left = dragState.element.XMm + 'mm';
        dragState.node.style.top = dragState.element.YMm + 'mm';
    }

    function onMouseMove(event) {
        onPointerMove(event.clientX, event.clientY);
    }

    function onTouchMove(event) {
        if (!dragState || !event.touches || !event.touches.length) {
            return;
        }

        event.preventDefault();
        onPointerMove(event.touches[0].clientX, event.touches[0].clientY);
    }

    function onPointerUp() {
        if (!dragState) {
            return;
        }

        dragState.element.XMm = snapMm(dragState.element.XMm);
        dragState.element.YMm = snapMm(dragState.element.YMm);
        dragState.element.XMm = clampPosition(dragState.element.XMm, dragState.dimensions.widthMm);
        dragState.element.YMm = clampPosition(dragState.element.YMm, dragState.dimensions.heightMm);
        dragState.node.style.left = dragState.element.XMm + 'mm';
        dragState.node.style.top = dragState.element.YMm + 'mm';
        dragState.node.classList.remove('am-label-designer-item--dragging');
        dragState = null;
    }

    function getAntiForgeryToken() {
        var input = document.querySelector("input[name='__RequestVerificationToken']");
        return input ? input.value : '';
    }

    function postTemplateAction(url, payload) {
        var body = [];
        body.push('__RequestVerificationToken=' + encodeURIComponent(getAntiForgeryToken()));
        for (var key in payload) {
            if (!payload.hasOwnProperty(key) || payload[key] == null) {
                continue;
            }

            body.push(encodeURIComponent(key) + '=' + encodeURIComponent(payload[key]));
        }

        return window.fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
            body: body.join('&'),
            credentials: 'same-origin'
        }).then(function (response) {
            return response.json();
        });
    }

    function showTemplateMessage(messageRoot, text, isError) {
        if (!messageRoot) {
            return;
        }

        messageRoot.textContent = text || '';
        messageRoot.classList.toggle('text-danger', !!isError);
        messageRoot.classList.toggle('text-success', !!text && !isError);
    }

    function formatTemplateMeta(template) {
        return template.WidthMm + ' x ' + template.HeightMm + ' mm · QR x' + template.QrMagnification;
    }

    function renderTemplateList(listRoot, templates, onLoad, onDelete) {
        if (!listRoot) {
            return;
        }

        listRoot.querySelectorAll('[data-am-label-template-item]').forEach(function (node) {
            node.remove();
        });

        var emptyNode = listRoot.querySelector('[data-am-label-template-empty]');
        if (!templates || !templates.length) {
            if (emptyNode) {
                emptyNode.classList.remove('d-none');
            }

            return;
        }

        if (emptyNode) {
            emptyNode.classList.add('d-none');
        }

        for (var i = 0; i < templates.length; i++) {
            (function (template) {
                var item = document.createElement('div');
                item.className = 'am-label-designer-template-item';
                item.setAttribute('data-am-label-template-item', template.Id);

                var title = document.createElement('div');
                title.className = 'am-label-designer-template-item__title';
                title.textContent = template.Name;

                var meta = document.createElement('div');
                meta.className = 'am-label-designer-template-item__meta text-muted small';
                meta.textContent = formatTemplateMeta(template);

                var actions = document.createElement('div');
                actions.className = 'am-label-designer-template-item__actions';

                var loadBtn = document.createElement('button');
                loadBtn.type = 'button';
                loadBtn.className = 'btn btn-sm btn-outline-primary';
                loadBtn.textContent = 'Load';
                loadBtn.addEventListener('click', function () {
                    onLoad(template);
                });

                var deleteBtn = document.createElement('button');
                deleteBtn.type = 'button';
                deleteBtn.className = 'btn btn-sm btn-outline-danger';
                deleteBtn.textContent = 'Delete';
                deleteBtn.addEventListener('click', function () {
                    onDelete(template);
                });

                actions.appendChild(loadBtn);
                actions.appendChild(deleteBtn);
                item.appendChild(title);
                item.appendChild(meta);
                item.appendChild(actions);
                listRoot.appendChild(item);
            })(templates[i]);
        }
    }

    function initTemplates(options, getState, applyState) {
        var listRoot = document.getElementById(options.templateListId);
        var nameField = document.getElementById(options.templateNameFieldId);
        var saveBtn = document.getElementById(options.templateSaveBtnId);
        var messageRoot = document.getElementById(options.templateMessageId);
        var saveUrl = listRoot ? listRoot.getAttribute('data-save-url') : null;
        var deleteUrl = listRoot ? listRoot.getAttribute('data-delete-url') : null;
        var templates = (options.initialTemplates && options.initialTemplates.Templates) || [];
        var loadedTemplateId = null;

        function setTemplates(nextTemplates) {
            templates = nextTemplates || [];
            renderTemplateList(listRoot, templates, loadTemplate, deleteTemplate);
        }

        function loadTemplate(template) {
            if (!template) {
                return;
            }

            loadedTemplateId = template.Id;
            if (nameField) {
                nameField.value = template.Name || '';
            }

            applyState({
                widthMm: template.WidthMm,
                heightMm: template.HeightMm,
                qrMagnification: template.QrMagnification,
                layoutDesignJson: template.LayoutDesignJson
            });

            showTemplateMessage(messageRoot, 'Loaded template "' + template.Name + '". Click Save label layout to use it for printing.', false);
        }

        function deleteTemplate(template) {
            if (!template || !deleteUrl) {
                return;
            }

            if (!window.confirm('Delete template "' + template.Name + '"?')) {
                return;
            }

            showTemplateMessage(messageRoot, 'Deleting template...', false);
            postTemplateAction(deleteUrl, { templateId: template.Id })
                .then(function (result) {
                    if (!result || !result.success) {
                        showTemplateMessage(messageRoot, (result && result.message) || 'Could not delete template.', true);
                        return;
                    }

                    if (loadedTemplateId === template.Id) {
                        loadedTemplateId = null;
                    }

                    setTemplates(result.templates || []);
                    showTemplateMessage(messageRoot, result.message || 'Template deleted.', false);
                })
                .catch(function () {
                    showTemplateMessage(messageRoot, 'Could not delete template.', true);
                });
        }

        if (saveBtn && saveUrl) {
            saveBtn.addEventListener('click', function () {
                var state = getState();
                var name = nameField ? nameField.value : '';
                if (!name || !name.trim()) {
                    showTemplateMessage(messageRoot, 'Enter a template name before saving.', true);
                    return;
                }

                showTemplateMessage(messageRoot, 'Saving template...', false);
                postTemplateAction(saveUrl, {
                    name: name,
                    layoutDesignJson: state.layoutDesignJson,
                    widthMm: state.widthMm,
                    heightMm: state.heightMm,
                    qrMagnification: state.qrMagnification,
                    templateId: loadedTemplateId
                })
                    .then(function (result) {
                        if (!result || !result.success) {
                            showTemplateMessage(messageRoot, (result && result.message) || 'Could not save template.', true);
                            return;
                        }

                        if (result.template) {
                            loadedTemplateId = result.template.Id;
                        }

                        setTemplates(result.templates || []);
                        showTemplateMessage(messageRoot, result.message || 'Template saved.', false);
                    })
                    .catch(function () {
                        showTemplateMessage(messageRoot, 'Could not save template.', true);
                    });
            });
        }

        setTemplates(templates);
    }

    function init(options) {
        var root = document.getElementById(options.rootId);
        var scaler = document.getElementById(options.scalerId);
        var canvas = document.getElementById(options.canvasId);
        var meta = document.getElementById(options.metaId);
        var fieldsRoot = document.getElementById(options.fieldsId);
        var jsonField = document.getElementById(options.jsonFieldId);
        var widthField = document.getElementById(options.widthFieldId);
        var heightField = document.getElementById(options.heightFieldId);
        var qrField = document.getElementById(options.qrMagnificationFieldId);
        var form = document.getElementById('labelDesignerForm');

        if (!root || !scaler || !canvas || !fieldsRoot || !jsonField || !widthField || !heightField) {
            return;
        }

        var sampleData = {
            assetTag: root.getAttribute('data-sample-tag') || 'AST-1001',
            assetName: root.getAttribute('data-sample-name') || 'Sample asset',
            departmentName: root.getAttribute('data-sample-dept') || 'Department',
            serialNumber: 'S/N: ' + (root.getAttribute('data-sample-serial') || 'SN-123456'),
            scanUrl: window.location.protocol + '//' + window.location.host + '/nanosoft/AssetScan/Lookup?code=AST-1001',
            barcodePayload: root.getAttribute('data-sample-tag') || 'AST-1001'
        };

        uiState.qrMagnification = getQrMagnification(qrField);
        var design = parseDesign(jsonField.value, getDimensions(widthField, heightField).widthMm, getDimensions(widthField, heightField).heightMm);

        function getState() {
            var dimensions = getDimensions(widthField, heightField);
            uiState.qrMagnification = getQrMagnification(qrField);
            design = normalizeDesign(design, dimensions.widthMm, dimensions.heightMm);
            jsonField.value = serializeDesign(design);

            return {
                widthMm: dimensions.widthMm,
                heightMm: dimensions.heightMm,
                qrMagnification: uiState.qrMagnification,
                layoutDesignJson: jsonField.value
            };
        }

        function applyState(state) {
            if (!state) {
                return;
            }

            widthField.value = state.widthMm;
            heightField.value = state.heightMm;
            if (qrField) {
                qrField.value = state.qrMagnification;
            }

            uiState.qrMagnification = getQrMagnification(qrField);
            design = parseDesign(state.layoutDesignJson, state.widthMm, state.heightMm);
            refresh();
        }

        function refresh() {
            var dimensions = getDimensions(widthField, heightField);
            uiState.qrMagnification = getQrMagnification(qrField);
            design = normalizeDesign(design, dimensions.widthMm, dimensions.heightMm);
            renderFieldControls(fieldsRoot, design, refresh);
            renderCanvas(scaler, canvas, meta, design, dimensions, sampleData, uiState.qrMagnification);
            jsonField.value = serializeDesign(design);
        }

        widthField.addEventListener('change', refresh);
        heightField.addEventListener('change', refresh);
        if (qrField) {
            qrField.addEventListener('change', refresh);
        }

        if (form) {
            form.addEventListener('submit', function () {
                jsonField.value = serializeDesign(normalizeDesign(design, getDimensions(widthField, heightField).widthMm, getDimensions(widthField, heightField).heightMm));
            });
        }

        if (window.ResizeObserver) {
            var resizeTimer = null;
            var resizeObserver = new window.ResizeObserver(function () {
                if (dragState) {
                    return;
                }

                if (resizeTimer) {
                    window.clearTimeout(resizeTimer);
                }

                resizeTimer = window.setTimeout(function () {
                    var dimensions = getDimensions(widthField, heightField);
                    applyCanvasScale(scaler, canvas, meta, dimensions);
                }, 120);
            });
            resizeObserver.observe(scaler.parentElement || scaler);
        }

        window.addEventListener('resize', function () {
            if (!dragState) {
                refresh();
            }
        });

        document.addEventListener('mousemove', onMouseMove);
        document.addEventListener('mouseup', onPointerUp);
        document.addEventListener('touchmove', onTouchMove, { passive: false });
        document.addEventListener('touchend', onPointerUp);
        document.addEventListener('touchcancel', onPointerUp);

        if (options.templateListId) {
            initTemplates(options, getState, applyState);
        }

        refresh();
    }

    window.LabelDesigner = {
        init: init,
        estimateQrSizeMm: estimateQrSizeMm,
        marginMm: MARGIN_MM
    };
})(window);
