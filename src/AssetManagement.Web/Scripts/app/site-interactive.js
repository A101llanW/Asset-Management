(function () {
    function normalize(value) {
        return (value || "").toLowerCase().trim();
    }

    document.querySelectorAll("[data-am-table-filter]").forEach(function (input) {
        var table = input.closest("form")
            ? document.querySelector("table[data-am-filterable], table.am-filterable-table")
            : null;
        if (!table) {
            return;
        }

        input.addEventListener("input", function () {
            var term = normalize(input.value);
            table.querySelectorAll("tbody tr").forEach(function (row) {
                var text = normalize(row.textContent);
                row.style.display = !term || text.indexOf(term) >= 0 ? "" : "none";
            });
        });
    });

    document.querySelectorAll(".am-kpi-card.am-fade-in").forEach(function (card, index) {
        card.style.animationDelay = (index * 0.06) + "s";
    });

    var selectAll = document.getElementById("amSelectAllAssets");
    if (selectAll) {
        selectAll.addEventListener("change", function () {
            document.querySelectorAll(".am-asset-bulk-select").forEach(function (box) {
                box.checked = selectAll.checked;
            });
        });
    }

    document.querySelectorAll("form").forEach(function (form) {
        var method = (form.getAttribute("method") || "get").toLowerCase();
        if (method !== "post") {
            return;
        }

        form.addEventListener("submit", function (event) {
            if (form.getAttribute("data-am-submitting") === "true") {
                event.preventDefault();
                return;
            }

            form.setAttribute("data-am-submitting", "true");
            form.querySelectorAll('button[type="submit"], input[type="submit"]').forEach(function (control) {
                control.disabled = true;
                control.setAttribute("aria-busy", "true");
            });
        });
    });
})();
