(function () {
    "use strict";

    // Sub-type details: edit one asset's own price. Blank = "Not set" (null); 0.00 is a real price.
    // The sub-type price is never used as a fallback here.
    var root = document.getElementById("amSubTypeDetails");
    var form = document.getElementById("amSubTypePriceForm");
    var modalEl = document.getElementById("amAssetPriceModal");
    if (!root || !form || !modalEl) {
        return;
    }

    var subTypeId = root.getAttribute("data-subtype-id");
    var input = document.getElementById("amAssetPriceInput");
    var tagLabel = document.getElementById("amAssetPriceTag");
    var errorEl = document.getElementById("amAssetPriceError");
    var saveBtn = document.getElementById("amAssetPriceSaveBtn");
    var clearBtn = document.getElementById("amAssetPriceClearBtn");
    var modal = window.bootstrap && window.bootstrap.Modal ? window.bootstrap.Modal.getOrCreateInstance(modalEl) : null;
    var currentAssetId = null;
    var busy = false;

    function showError(message) {
        if (!errorEl) {
            return;
        }
        errorEl.textContent = message || "";
        errorEl.classList.toggle("d-none", !message);
    }

    function setText(id, value) {
        var el = document.getElementById(id);
        if (el) {
            el.textContent = value;
        }
    }

    function openFor(button) {
        currentAssetId = button.getAttribute("data-asset-id");
        var row = button.closest("tr");
        var cell = row ? row.querySelector(".am-asset-price") : null;
        input.value = cell ? (cell.getAttribute("data-price") || "") : "";
        if (tagLabel) {
            tagLabel.textContent = button.getAttribute("data-asset-tag") || "";
        }
        showError("");
        if (modal) {
            modal.show();
            setTimeout(function () { input.focus(); input.select(); }, 200);
        }
    }

    function updateRow(data) {
        var row = root.querySelector('tr[data-asset-id="' + data.assetId + '"]');
        var cell = row ? row.querySelector(".am-asset-price") : null;
        if (cell) {
            cell.textContent = data.priceDisplay;
            cell.setAttribute("data-price", data.price || "");
            cell.classList.toggle("text-muted", !data.isSet);
            cell.classList.toggle("fst-italic", !data.isSet);
        }
        setText("amSubTypePricedCount", String(data.pricedCount));
        var unpriced = document.getElementById("amSubTypeUnpricedCount");
        if (unpriced && unpriced.firstChild) {
            unpriced.firstChild.nodeValue = String(data.unpricedCount) + " ";
        }
        setText("amSubTypePricedTotal", data.pricedTotalDisplay);
        setText("amSubTypeUnpricedNote", data.unpricedNote || "");
    }

    function save(rawValue) {
        if (busy || !currentAssetId) {
            return;
        }
        var value = String(rawValue == null ? "" : rawValue).replace(/,/g, "").trim();
        if (value !== "" && isNaN(Number(value))) {
            showError("Enter a number, or leave it blank for \"Not set\".");
            return;
        }
        if (value !== "" && Number(value) < 0) {
            showError("Price cannot be negative.");
            return;
        }

        var tokenInput = form.querySelector('input[name="__RequestVerificationToken"]');
        var body = new URLSearchParams();
        body.set("__RequestVerificationToken", tokenInput ? tokenInput.value : "");
        body.set("assetId", currentAssetId);
        body.set("subTypeId", subTypeId);
        body.set("price", value);

        busy = true;
        saveBtn.disabled = true;
        clearBtn.disabled = true;
        showError("");
        fetch(form.getAttribute("action"), {
            method: "POST",
            credentials: "same-origin",
            headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8", "X-Requested-With": "XMLHttpRequest" },
            body: body.toString()
        })
            .then(function (response) { return response.json(); })
            .then(function (data) {
                if (!data || !data.success) {
                    showError(data && data.message ? data.message : "Could not save the price.");
                    return;
                }
                updateRow(data);
                if (modal) {
                    modal.hide();
                }
            })
            .catch(function () {
                showError("Could not save the price. Check your connection and try again.");
            })
            .then(function () {
                busy = false;
                saveBtn.disabled = false;
                clearBtn.disabled = false;
            });
    }

    root.addEventListener("click", function (event) {
        var button = event.target.closest ? event.target.closest(".am-edit-price-btn") : null;
        if (button) {
            event.preventDefault();
            openFor(button);
        }
    });

    saveBtn.addEventListener("click", function () { save(input.value); });
    clearBtn.addEventListener("click", function () { input.value = ""; save(""); });
    input.addEventListener("keydown", function (event) {
        if (event.key === "Enter") {
            event.preventDefault();
            save(input.value);
        }
    });
})();
