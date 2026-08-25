(function () {
    document.querySelectorAll(".js-select-all-module").forEach(function (checkbox) {
        checkbox.addEventListener("change", function () {
            var module = checkbox.getAttribute("data-module");
            document.querySelectorAll('input[data-module-item="' + module + '"]').forEach(function (item) {
                item.checked = checkbox.checked;
            });
        });
    });
})();
