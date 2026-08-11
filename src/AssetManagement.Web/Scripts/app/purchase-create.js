(function ($) {
    'use strict';

    var comparisonRows = [];

    function formatMoney(value, currency) {
        var amount = parseFloat(value);
        if (isNaN(amount)) {
            return '';
        }
        return (currency || '') + ' ' + amount.toFixed(2);
    }

    function parsePositiveNumber(value) {
        var parsed = parseFloat(value);
        return isNaN(parsed) ? null : parsed;
    }

    function recalcTotalCost() {
        var qty = parsePositiveNumber($('#Quantity').val());
        var unit = parsePositiveNumber($('#UnitCost').val());
        if (qty !== null && unit !== null && qty > 0 && unit >= 0) {
            $('#TotalCost').val((qty * unit).toFixed(2));
        }
    }

    function applySupplierOffer(supplierId, unitPrice, currency) {
        if (supplierId) {
            $('#SupplierId').val(String(supplierId));
        }
        if (unitPrice !== undefined && unitPrice !== null && unitPrice !== '') {
            var unit = parsePositiveNumber(unitPrice);
            if (unit !== null) {
                $('#UnitCost').val(unit.toFixed(2));
            }
        }
        if (currency) {
            $('#Currency').val(currency);
        }
        recalcTotalCost();
    }

    function findComparisonRow(supplierId) {
        var id = parseInt(supplierId, 10);
        if (isNaN(id)) {
            return null;
        }
        for (var i = 0; i < comparisonRows.length; i++) {
            if (comparisonRows[i].SupplierId === id) {
                return comparisonRows[i];
            }
        }
        return null;
    }

    function applyRequisitionQuantity() {
        var panel = $('#supplier-comparison-panel');
        var reqQty = parseInt(panel.data('am-requisition-quantity'), 10);
        if (isNaN(reqQty) || reqQty <= 0) {
            return;
        }

        var current = parsePositiveNumber($('#Quantity').val());
        if (current === null || current <= 0) {
            $('#Quantity').val(reqQty);
        }
    }

    function renderRows(data) {
        var panel = $('#supplier-comparison-panel');
        var table = $('#comparison-table');
        var tbody = table.find('tbody');
        var empty = $('#comparison-empty');
        tbody.empty();
        comparisonRows = (data && data.Rows) ? data.Rows : [];

        if (comparisonRows.length === 0) {
            empty.text('No catalog or historical supplier prices matched this item. Enter supplier and cost manually.');
            table.hide();
            empty.show();
            panel.show();
            return;
        }

        if (data.Currency) {
            $('#Currency').val(data.Currency);
        }

        var note = data.HasHistoricalFallback
            ? 'Showing historical purchase averages (not current catalog quotes).'
            : (data.HasCatalogMatches ? 'Catalog quotes sorted lowest to highest.' : '');
        if (note) {
            empty.text(note);
            empty.show();
        } else {
            empty.hide();
        }

        $.each(comparisonRows, function (_, row) {
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
                'data-supplier-id="' + row.SupplierId + '" data-unit-price="' + row.UnitPrice + '" data-currency="' + (row.Currency || '') + '">Select</button>'));
            tbody.append(tr);
        });

        table.show();
        panel.show();

        var selectedSupplierId = $('#SupplierId').val();
        if (selectedSupplierId) {
            var selectedRow = findComparisonRow(selectedSupplierId);
            if (selectedRow) {
                applySupplierOffer(selectedRow.SupplierId, selectedRow.UnitPrice, selectedRow.Currency);
            }
        }
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
        applySupplierOffer(
            $(this).data('supplierId') || $(this).attr('data-supplier-id'),
            $(this).data('unitPrice') || $(this).attr('data-unit-price'),
            $(this).data('currency') || $(this).attr('data-currency'));
    });

    $('#SupplierId').on('change', function () {
        var row = findComparisonRow($(this).val());
        if (row) {
            applySupplierOffer(row.SupplierId, row.UnitPrice, row.Currency);
        } else {
            recalcTotalCost();
        }
    });

    $('#Quantity, #UnitCost').on('input change', recalcTotalCost);

    $('#refresh-comparison').on('click', loadComparison);
    $('#manual-item-description').on('change blur', loadComparison);

    $(function () {
        applyRequisitionQuantity();
        recalcTotalCost();
        loadComparison();
    });
})(jQuery);
