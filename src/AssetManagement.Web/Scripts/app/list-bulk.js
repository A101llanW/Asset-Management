(function () {
    var documentBound = false;

    function selectedIds() {
        var ids = [];
        var checkboxes = document.querySelectorAll("[data-am-bulk-id]");
        for (var i = 0; i < checkboxes.length; i++) {
            if (checkboxes[i].checked) {
                ids.push(checkboxes[i].value);
            }
        }
        return ids;
    }

    function syncHiddenIds() {
        var holder = document.getElementById("amBulkAssetIds");
        var toolbar = document.querySelector("[data-am-bulk-toolbar]");
        var actionSelect = toolbar ? toolbar.querySelector("[data-am-bulk-action]") : null;
        var submitBtn = toolbar ? toolbar.querySelector("[data-am-bulk-submit]") : null;
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
            var groupSelected = !!(window.AmAssetGroupSelection && window.AmAssetGroupSelection.hasSelection());
            submitBtn.disabled = (ids.length === 0 && !groupSelected) || !actionSelect || !actionSelect.value;
        }
    }

    function syncActionFields() {
        var toolbar = document.querySelector("[data-am-bulk-toolbar]");
        if (!toolbar) {
            return;
        }
        var actionSelect = toolbar.querySelector("[data-am-bulk-action]");
        var deptBlock = toolbar.querySelector("[data-am-bulk-dept]");
        var statusBlock = toolbar.querySelector("[data-am-bulk-status]");
        var classBlock = toolbar.querySelector("[data-am-bulk-class]");
        var action = actionSelect ? actionSelect.value : "";
        if (deptBlock) {
            deptBlock.classList.toggle("d-none", action !== "department");
        }
        if (statusBlock) {
            statusBlock.classList.toggle("d-none", action !== "status");
        }
        if (classBlock) {
            classBlock.classList.toggle("d-none", action !== "classroom");
        }
        syncHiddenIds();
    }

    function bindToolbarOnce() {
        if (documentBound) {
            return;
        }
        documentBound = true;

        document.addEventListener("change", function (event) {
            var target = event.target;
            if (!target || !target.matches) {
                return;
            }
            if (target.matches("[data-am-bulk-id]")) {
                syncHiddenIds();
                return;
            }
            if (target.matches("[data-am-bulk-master]")) {
                var checkboxes = document.querySelectorAll("[data-am-bulk-id]");
                for (var j = 0; j < checkboxes.length; j++) {
                    checkboxes[j].checked = target.checked;
                }
                syncHiddenIds();
                return;
            }
            if (target.matches("[data-am-bulk-action]")) {
                syncActionFields();
            }
        });

        document.addEventListener("submit", function (event) {
            var form = event.target;
            if (!form || form.id !== "amBulkForm") {
                return;
            }
            var groupSelected = !!(window.AmAssetGroupSelection && window.AmAssetGroupSelection.hasSelection());
            if (selectedIds().length === 0 && !groupSelected) {
                event.preventDefault();
            }
        });
    }

    function init() {
        bindToolbarOnce();
        syncActionFields();
    }

    init();
    document.addEventListener("am:live-filter-updated", init);

    window.AmListBulk = {
        refresh: syncHiddenIds,
        init: init
    };
})();