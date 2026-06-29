(function ($) {
    'use strict';

    function formatMoney(value, currency) {
        var amount = parseFloat(value);
        if (isNaN(amount)) {
            return '';
        }
        return (currency || '') + ' ' + amount.toFixed(2);
    }

    function renderRows(data) {
        var panel = $('#supplier-comparison-panel');
        var table = $('#comparison-table');
        var tbody = table.find('tbody');
        var empty = $('#comparison-empty');
        tbody.empty();

        if (!data || !data.Rows || data.Rows.length === 0) {
            empty.text('No catalog or historical supplier prices matched this item. Enter supplier and cost manually.');
            table.hide();
            empty.show();
            panel.show();
            return;
        }

        var note = data.HasHistoricalFallback
            ? 'Showing historical purchase averages (not current catalog quotes).'
            : (data.HasCatalogMatches ? 'Catalog quotes sorted lowest to highest.' : '');
        empty.text(note);
        empty.show();

        $.each(data.Rows, function (_, row) {
            var badges = [];
            if (row.IsPreferred) {
                badges.push('<span class="badge bg-primary ms-1">Preferred</span>');
            }
            if (row.IsCheapest) {
                badges.push('<span class="badge bg-success ms-1">Lowest price</span>');
            }
            if (row.IsMostExpensive) {
                badges.push('<span class="badge bg-warning text-dark ms-1">Highest price</span>');
            }
            if (row.IsHistorical) {
                badges.push('<span class="badge bg-secondary ms-1">Historical</span>');
            }

            var tr = $('<tr></tr>');
            tr.append($('<td></td>').html(row.SupplierName + badges.join('')));
            tr.append($('<td></td>').text(row.ItemLabel || ''));
            tr.append($('<td></td>').text(formatMoney(row.UnitPrice, row.Currency)));
            tr.append($('<td></td>').text(row.LeadTimeDays ? row.LeadTimeDays + ' days' : '—'));
            tr.append($('<td></td>').html(
                '<button type="button" class="btn btn-sm btn-outline-primary select-offer" ' +
                'data-supplier-id="' + row.SupplierId + '" data-unit-price="' + row.UnitPrice + '">Select</button>'));
            tbody.append(tr);
        });

        table.show();
        panel.show();
    }

    function loadComparison() {
        var panel = $('#supplier-comparison-panel');
        var url = panel.data('am-comparison-url');
        if (!url) {
            return;
        }

        var purchaseRequestId = panel.data('am-purchase-request-id');
        var itemDescription = panel.data('am-item-description') || $('#manual-item-description').val();

        $.getJSON(url, {
            purchaseRequestId: purchaseRequestId || '',
            itemDescription: itemDescription || ''
        }).done(renderRows).fail(function () {
            $('#comparison-empty').text('Could not load supplier comparison.');
            panel.show();
        });
    }

    $(document).on('click', '.select-offer', function () {
        var supplierId = $(this).data('supplier-id');
        var unitPrice = $(this).data('unit-price');
        $('#SupplierId').val(supplierId);
        $('#UnitCost').val(unitPrice);
        var qty = parseFloat($('#Quantity').val());
        if (!isNaN(qty)) {
            $('#TotalCost').val((qty * unitPrice).toFixed(2));
        }
    });

    $('#refresh-comparison').on('click', loadComparison);
    $('#manual-item-description').on('change blur', loadComparison);

    $(function () {
        loadComparison();
    });
})(jQuery);
