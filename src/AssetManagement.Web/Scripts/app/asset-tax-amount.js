(function () {
    function parseNumber(value) {
        if (value === null || value === undefined || value === '') {
            return 0;
        }

        var parsed = parseFloat(value);
        return isNaN(parsed) ? 0 : parsed;
    }

    function roundCurrency(value) {
        return Math.round(value * 100) / 100;
    }

    function init(root) {
        var container = root.getElementById('assetTaxFields');
        if (!container) {
            return;
        }

        var mode = container.querySelector('#TaxInputMode');
        var input = container.querySelector('#TaxInputValue');
        var acquisition = root.getElementById('AcquisitionCost');
        var taxAmount = container.querySelector('#TaxAmount');
        var display = container.querySelector('#TaxAmountDisplay');

        if (!mode || !input || !taxAmount) {
            return;
        }

        function updatePlaceholder() {
            var isPercentage = mode.value === 'Percentage';
            input.placeholder = isPercentage
                ? (input.getAttribute('data-placeholder-percentage') || 'Enter tax %')
                : (input.getAttribute('data-placeholder-amount') || 'Enter tax amount');
        }

        function syncTax() {
            var value = parseNumber(input.value);
            var cost = parseNumber(acquisition ? acquisition.value : 0);
            var tax = 0;

            if (value > 0) {
                tax = mode.value === 'Percentage'
                    ? roundCurrency(cost * value / 100)
                    : roundCurrency(value);
            }

            taxAmount.value = tax.toFixed(2);
            if (display) {
                display.textContent = tax.toFixed(2);
            }
        }

        mode.addEventListener('change', function () {
            updatePlaceholder();
            syncTax();
        });
        input.addEventListener('input', syncTax);
        if (acquisition) {
            acquisition.addEventListener('input', syncTax);
        }

        updatePlaceholder();
        syncTax();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            init(document);
        });
    } else {
        init(document);
    }
})();
