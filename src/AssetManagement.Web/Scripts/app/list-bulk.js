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
    var checkboxes = document.querySelectorAll("[data-am-bulk-id]");

    function selectedIds() {
        var ids = [];
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
            submitBtn.disabled = ids.length === 0 || !actionSelect || !actionSelect.value;
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
        syncHiddenIds();
    }

    for (var i = 0; i < checkboxes.length; i++) {
        checkboxes[i].addEventListener("change", syncHiddenIds);
    }

    var master = document.querySelector("[data-am-bulk-master]");
    if (master) {
        master.addEventListener("change", function () {
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
            if (selectedIds().length === 0) {
                event.preventDefault();
            }
        });
    }

    syncActionFields();
})();
