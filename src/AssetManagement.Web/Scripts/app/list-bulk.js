(function () {
    var toolbar = document.querySelector("[data-am-bulk-toolbar]");
    if (!toolbar) {
        return;
    }

    var form = document.getElementById("amBulkForm");
    var holder = document.getElementById("amBulkAssetIds");
    var actionSelect = toolbar.querySelector("[data-am-bulk-action]");
    var submitBtn = toolbar.querySelector("[data-am-bulk-submit]");
    var deptBlock = toolbar.querySelector("[data-am-bulk-dept]");
    var statusBlock = toolbar.querySelector("[data-am-bulk-status]");
    var classBlock = toolbar.querySelector("[data-am-bulk-class]");
    var master = document.querySelector("[data-am-bulk-master]");

    function getCheckboxes() {
        return document.querySelectorAll("[data-am-bulk-id]");
    }

    function selectedIds() {
        var ids = [];
        var checkboxes = getCheckboxes();
        for (var i = 0; i < checkboxes.length; i++) {
            if (checkboxes[i].checked) {
                ids.push(checkboxes[i].value);
            }
        }
        return ids;
    }

    function syncHiddenIds() {
        if (!holder) {
            return;
        }

        holder.innerHTML = "";
        var ids = selectedIds();
        for (var i = 0; i < ids.length; i++) {
            var input = document.createElement("input");
            input.type = "hidden";
            input.name = "AssetIds";
            input.value = ids[i];
            holder.appendChild(input);
        }

        if (submitBtn) {
            // rel1022a: a ticked whole group (asset-group-selection.js) counts even before its units are loaded.
            var groupSelected = !!(window.AmAssetGroupSelection && window.AmAssetGroupSelection.hasSelection());
            submitBtn.disabled = (ids.length === 0 && !groupSelected) || !actionSelect || !actionSelect.value;
        }
    }

    function syncActionFields() {
        var action = actionSelect ? actionSelect.value : "";
        if (deptBlock) {
            deptBlock.classList.toggle("d-none", action !== "department");
        }
        if (statusBlock) {
            statusBlock.classList.toggle("d-none", action !== "status");
        }
        if (classBlock) {
            // Flat list bulk "Move to classroom" (2026-10-27).
            classBlock.classList.toggle("d-none", action !== "classroom");
        }
        syncHiddenIds();
    }

    document.addEventListener("change", function (event) {
        var target = event.target;
        if (!target || !target.matches || !target.matches("[data-am-bulk-id]")) {
            return;
        }
        syncHiddenIds();
    });

    if (master) {
        master.addEventListener("change", function () {
            var checkboxes = getCheckboxes();
            for (var j = 0; j < checkboxes.length; j++) {
                checkboxes[j].checked = master.checked;
            }
            syncHiddenIds();
        });
    }

    if (actionSelect) {
        actionSelect.addEventListener("change", syncActionFields);
    }

    if (form) {
        form.addEventListener("submit", function (event) {
            var groupSelected = !!(window.AmAssetGroupSelection && window.AmAssetGroupSelection.hasSelection());
            if (selectedIds().length === 0 && !groupSelected) {
                event.preventDefault();
            }
        });
    }

    window.AmListBulk = {
        refresh: syncHiddenIds
    };

    syncActionFields();
})();
