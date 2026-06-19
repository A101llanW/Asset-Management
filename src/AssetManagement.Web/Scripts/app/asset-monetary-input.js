(function () {
    var displayCulture = 'en-KE';

    function parseNumber(value) {
        if (value === null || value === undefined) {
            return 0;
        }

        var normalized = String(value).replace(/,/g, '').trim();
        if (normalized === '') {
            return 0;
        }

        var parsed = parseFloat(normalized);
        return isNaN(parsed) ? 0 : parsed;
    }

    function formatAmount(value) {
        var amount = parseNumber(value);
        return amount.toLocaleString(displayCulture, {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function stripFormatting(input) {
        if (!input) {
            return;
        }

        input.value = String(input.value || '').replace(/,/g, '').trim();
    }

    function bindInput(input) {
        if (!input || input.getAttribute('data-monetary-bound') === 'true') {
            return;
        }

        input.setAttribute('data-monetary-bound', 'true');
        input.setAttribute('inputmode', 'decimal');

        if (input.value) {
            input.value = formatAmount(input.value);
        }

        input.addEventListener('blur', function () {
            if (String(input.value || '').trim() === '') {
                return;
            }

            input.value = formatAmount(input.value);
        });

        input.addEventListener('focus', function () {
            stripFormatting(input);
        });
    }

    function bindForm(form) {
        if (!form || form.getAttribute('data-monetary-form-bound') === 'true') {
            return;
        }

        form.setAttribute('data-monetary-form-bound', 'true');
        form.addEventListener('submit', function () {
            var inputs = form.querySelectorAll('[data-monetary-input]');
            for (var i = 0; i < inputs.length; i++) {
                stripFormatting(inputs[i]);
            }
        });
    }

    function init(root) {
        var inputs = root.querySelectorAll('[data-monetary-input]');
        for (var i = 0; i < inputs.length; i++) {
            bindInput(inputs[i]);
        }

        var forms = root.querySelectorAll('form');
        for (var j = 0; j < forms.length; j++) {
            bindForm(forms[j]);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            init(document);
        });
    } else {
        init(document);
    }

    window.AssetMonetaryInput = {
        parseNumber: parseNumber,
        formatAmount: formatAmount
    };
})();
