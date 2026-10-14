/* eslint-env browser */
/*
 * Requisition line classification (catalog requisition flows).
 * - Cascading Category > Type > Sub-type pickers per line (and the optional header pick), fed by the org's
 *   existing asset categories (JSON on #purchase-line-items[data-am-catalog]). Picking a child fills its parents.
 * - Rows added/removed by purchase-request-create.js get a picker block and Lines[i].* names (MutationObserver).
 * - Live per-line approval path preview: POSTs the form (no file) to PreviewLinePaths; the server applies the same
 *   classification + resolution rules as submit. The server re-validates everything on submit.
 */
(function () {
    "use strict";

    var container = document.getElementById("purchase-line-items");
    var form = document.getElementById("purchase-request-form");
    if (!container || !form) {
        return;
    }

    var catalog;
    try {
        catalog = JSON.parse(container.getAttribute("data-am-catalog") || "{}");
    } catch (e) {
        catalog = {};
    }
    catalog.categories = catalog.categories || [];
    catalog.types = catalog.types || [];
    catalog.subTypes = catalog.subTypes || [];

    var previewUrl = container.getAttribute("data-am-preview-lines-url") || "";
    var pathsPanel = document.getElementById("purchase-line-paths");
    var template = document.getElementById("am-line-classification-template");
    var previewTimer = null;
    var previewSeq = 0;

    function byId(list, id) {
        for (var i = 0; i < list.length; i++) {
            if (String(list[i].id) === String(id)) {
                return list[i];
            }
        }
        return null;
    }

    function fillSelect(select, items, emptyLabel, selected) {
        select.innerHTML = "";
        var empty = document.createElement("option");
        empty.value = "";
        empty.textContent = emptyLabel;
        select.appendChild(empty);
        var found = false;
        items.forEach(function (item) {
            var opt = document.createElement("option");
            opt.value = String(item.id);
            opt.textContent = item.name;
            if (selected && String(selected) === String(item.id)) {
                opt.selected = true;
                found = true;
            }
            select.appendChild(opt);
        });
        if (!found) {
            select.value = "";
        }
    }

    function render(block, categoryId, typeId, subTypeId) {
        var cat = block.querySelector(".am-pick-category");
        var type = block.querySelector(".am-pick-type");
        var sub = block.querySelector(".am-pick-subtype");
        if (!cat || !type || !sub) {
            return;
        }

        // Child picks imply parents (sub-type > type > category).
        var subItem = subTypeId ? byId(catalog.subTypes, subTypeId) : null;
        if (subItem) {
            typeId = subItem.parentId;
        }
        var typeItem = typeId ? byId(catalog.types, typeId) : null;
        if (typeItem) {
            categoryId = typeItem.parentId;
        }

        fillSelect(cat, catalog.categories, "Category (optional)", categoryId);
        fillSelect(type, catalog.types.filter(function (t) {
            return !categoryId || String(t.parentId) === String(categoryId);
        }), "Type", typeItem ? typeItem.id : null);
        fillSelect(sub, catalog.subTypes.filter(function (s) {
            return typeItem ? String(s.parentId) === String(typeItem.id) : false;
        }), typeItem ? "Sub-type (optional)" : "Sub-type (choose a type first)", subItem ? subItem.id : null);
        sub.disabled = !typeItem;
    }

    function wire(block) {
        if (!block || block.__amWired) {
            return;
        }
        block.__amWired = true;
        var cat = block.querySelector(".am-pick-category");
        var type = block.querySelector(".am-pick-type");
        var sub = block.querySelector(".am-pick-subtype");
        render(block, cat.getAttribute("data-selected"), type.getAttribute("data-selected"), sub.getAttribute("data-selected"));
        cat.addEventListener("change", function () {
            render(block, cat.value, null, null);
            schedulePreview();
        });
        type.addEventListener("change", function () {
            render(block, cat.value, type.value, null);
            schedulePreview();
        });
        sub.addEventListener("change", function () {
            render(block, cat.value, type.value, sub.value);
            schedulePreview();
        });
    }

    function syncRows() {
        var rows = container.querySelectorAll(".purchase-line-item");
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i];
            var block = row.querySelector(".am-line-classification");
            if (!block && template) {
                var fragment = document.importNode(template.content, true);
                row.appendChild(fragment);
                block = row.querySelector(".am-line-classification");
            }
            if (!block) {
                continue;
            }
            Array.prototype.forEach.call(block.querySelectorAll("select[data-field]"), function (select) {
                select.name = "Lines[" + i + "]." + select.getAttribute("data-field");
            });
            wire(block);
        }
    }

    function setPathText(lineNumber, text, warn) {
        var rows = container.querySelectorAll(".purchase-line-item");
        var row = rows[lineNumber - 1];
        var el = row ? row.querySelector(".am-line-path") : null;
        if (!el) {
            return;
        }
        el.textContent = text || "";
        el.classList.toggle("text-warning", !!warn);
        el.classList.toggle("text-muted", !warn);
    }

    function clearPaths() {
        Array.prototype.forEach.call(container.querySelectorAll(".am-line-path"), function (el) {
            el.textContent = "";
        });
        if (pathsPanel) {
            pathsPanel.classList.add("d-none");
            pathsPanel.textContent = "";
        }
    }

    function showPanel(className, nodes) {
        if (!pathsPanel) {
            return;
        }
        pathsPanel.className = "alert small mt-2 " + className;
        pathsPanel.innerHTML = "";
        nodes.forEach(function (n) {
            pathsPanel.appendChild(n);
        });
    }

    function textNode(tag, text) {
        var el = document.createElement(tag);
        el.textContent = text;
        return el;
    }

    function applyPreview(data) {
        clearPaths();
        if (!data) {
            return;
        }
        if (data.error) {
            showPanel("alert-warning", [textNode("div", data.error)]);
            return;
        }
        var groups = data.groups || [];
        var mixed = groups.length > 1;
        (data.lines || []).forEach(function (line) {
            setPathText(line.lineNumber, "Approval: " + line.label + (mixed ? " (requisition " + line.groupNumber + ")" : ""), mixed && data.willBlock);
        });
        if (!mixed) {
            return;
        }
        var nodes = [];
        nodes.push(textNode("strong", data.willBlock
            ? "These items use different approval paths. Your organisation does not allow mixing them: remove lines or submit them separately."
            : "These items use different approval paths, so submitting creates " + groups.length + " linked requisitions:"));
        var list = document.createElement("ul");
        list.className = "mb-0 mt-1";
        groups.forEach(function (g) {
            list.appendChild(textNode("li", "Requisition " + g.groupNumber + " (lines " + g.lineNumbers.join(", ") + "): " + g.label + " - " + g.summary));
        });
        nodes.push(list);
        showPanel(data.willBlock ? "alert-danger" : "alert-info", nodes);
    }

    function buildBody() {
        var params = new URLSearchParams();
        Array.prototype.forEach.call(form.elements, function (el) {
            if (!el.name || el.disabled || el.type === "file" || el.type === "submit" || el.type === "button") {
                return;
            }
            if ((el.type === "checkbox" || el.type === "radio") && !el.checked) {
                return;
            }
            params.append(el.name, el.value);
        });
        return params.toString();
    }

    function runPreview() {
        if (!previewUrl || !window.fetch) {
            return;
        }
        var seq = ++previewSeq;
        fetch(previewUrl, {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8", Accept: "application/json" },
            body: buildBody()
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Request failed");
                }
                return response.json();
            })
            .then(function (data) {
                if (seq === previewSeq) {
                    applyPreview(data);
                }
            })
            .catch(function () {
                if (seq === previewSeq) {
                    clearPaths();
                }
            });
    }

    function schedulePreview() {
        if (previewTimer) {
            clearTimeout(previewTimer);
        }
        previewTimer = setTimeout(runPreview, 300);
    }

    var header = document.getElementById("purchase-header-classification");
    if (header) {
        wire(header);
    }

    syncRows();
    if (window.MutationObserver) {
        new MutationObserver(function () {
            syncRows();
            schedulePreview();
        }).observe(container, { childList: true });
    }

    var department = document.getElementById("DepartmentId");
    if (department) {
        department.addEventListener("change", schedulePreview);
    }
    var targetAsset = document.getElementById("TargetAssetId");
    if (targetAsset) {
        targetAsset.addEventListener("change", schedulePreview);
    }

    schedulePreview();
})();
