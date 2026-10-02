(function () {
    var filterDebounceMs = 250;

    function normalize(value) {
        return (value || "").toLowerCase().trim();
    }

    function debounce(fn, waitMs) {
        var timerId = null;
        return function () {
            var args = arguments;
            var context = this;
            if (timerId) {
                clearTimeout(timerId);
            }
            timerId = setTimeout(function () {
                timerId = null;
                fn.apply(context, args);
            }, waitMs);
        };
    }

    function findFilterableTable(input) {
        if (!input || !input.closest("form")) {
            return null;
        }

        var targetSelector = input.getAttribute("data-am-filter-target");
        if (targetSelector) {
            return document.querySelector(targetSelector);
        }

        var container = input.closest(".am-list-toolbar");
        if (container && container.parentElement) {
            var scopedTable = container.parentElement.querySelector("table[data-am-filterable], table.am-filterable-table");
            if (scopedTable) {
                return scopedTable;
            }
        }

        return document.querySelector("table[data-am-filterable], table.am-filterable-table");
    }

    function ensureFilterEmptyRow(table) {
        var tbody = table.querySelector("tbody");
        if (!tbody) {
            return null;
        }

        var emptyRow = tbody.querySelector("tr[data-am-filter-empty]");
        if (!emptyRow) {
            emptyRow = document.createElement("tr");
            emptyRow.setAttribute("data-am-filter-empty", "true");
            emptyRow.style.display = "none";
            var cell = document.createElement("td");
            var columnCount = table.querySelectorAll("thead th").length || 1;
            cell.colSpan = columnCount;
            cell.className = "text-muted text-center py-4";
            cell.textContent = "No items match your search.";
            emptyRow.appendChild(cell);
            tbody.appendChild(emptyRow);
        }

        return emptyRow;
    }

    function applyTableFilter(input, table) {
        var term = normalize(input.value);
        var visibleCount = 0;
        var emptyRow = ensureFilterEmptyRow(table);

        table.querySelectorAll("tbody tr").forEach(function (row) {
            if (row.getAttribute("data-am-filter-empty") === "true") {
                return;
            }

            var text = normalize(row.textContent);
            var matches = !term || text.indexOf(term) >= 0;
            row.style.display = matches ? "" : "none";
            if (matches) {
                visibleCount += 1;
            }
        });

        if (emptyRow) {
            emptyRow.style.display = term && visibleCount === 0 ? "" : "none";
        }
    }

    document.querySelectorAll("[data-am-table-filter]").forEach(function (input) {
        var table = findFilterableTable(input);
        if (!table) {
            return;
        }

        var runFilter = debounce(function () {
            applyTableFilter(input, table);
        }, filterDebounceMs);

        input.addEventListener("input", runFilter);
        if (normalize(input.value)) {
            applyTableFilter(input, table);
        }
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
