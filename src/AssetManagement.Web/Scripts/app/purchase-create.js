/* Create purchase order: supplier price comparison for the linked requisition.
 * QA 2026-10-28 (finding 4, Allan 09:33 EAT "Fix"): choosing a supplier used to fill only line 1 (the script only
 * knew line 1's unit-cost input, id UnitCost). Now the supplier's price is applied to every PO line it has a price for:
 * - the comparison endpoint returns one comparison per requisition line (Lines[], matched by PurchaseRequestLineId);
 *   a PO line without its own comparison (legacy requisition without lines) uses the requisition-level rows;
 * - a unit cost the user typed is never overwritten by a supplier change or a late AJAX answer; only an explicit
 *   "Select" on a comparison row replaces it (for the lines that row covers). A re-rendered form keeps its values;
 * - a line the chosen supplier has no price for is left alone and says so.
 * IE11: var / function only.
 */
(function ($) {
    'use strict';

    var comparison = { Rows: [], Lines: [] };
    var EDITED = 'data-am-cost-edited';

    function formatMoney(value, currency) {
        var amount = parseFloat(value);
        if (isNaN(amount)) {
            return '';
        }
        return (currency || '') + ' ' + amount.toFixed(2);
    }

    function parseNumber(value) {
        var parsed = parseFloat(value);
        return isNaN(parsed) ? null : parsed;
    }

    function escapeHtml(value) {
        return String(value === undefined || value === null ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function poRows() {
        return $('#po-lines-table tbody tr');
    }

    function lineIdOf(tr) {
        var hidden = $(tr).find('input[name$=".PurchaseRequestLineId"]');
        var id = parseInt(hidden.val(), 10);
        return isNaN(id) ? null : id;
    }

    function findLineComparison(lineId) {
        var lines = comparison.Lines || [];
        if (lineId === null) {
            return null;
        }
        for (var i = 0; i < lines.length; i++) {
            if (lines[i].PurchaseRequestLineId === lineId) {
                return lines[i];
            }
        }
        return null;
    }

    /* The comparison rows that apply to one PO line. */
    function rowsForPoLine(tr) {
        var line = findLineComparison(lineIdOf(tr));
        if (line) {
            return line.Rows || [];
        }
        // No per-line data (legacy requisition without lines, or an older server): requisition-level rows, but only
        // when that cannot spread one item's price over different items.
        return poRows().length === 1 || !(comparison.Lines && comparison.Lines.length) ? (comparison.Rows || []) : [];
    }

    function findSupplierRow(rows, supplierId) {
        var id = parseInt(supplierId, 10);
        if (isNaN(id) || !rows) {
            return null;
        }
        for (var i = 0; i < rows.length; i++) {
            if (rows[i].SupplierId === id) {
                return rows[i];
            }
        }
        return null;
    }

    function fireRecalc() {
        var table = document.getElementById('po-lines-table');
        if (!table) {
            return;
        }
        var evt;
        try {
            evt = new CustomEvent('am:po-recalc');
        } catch (e) {
            evt = document.createEvent('CustomEvent');
            evt.initCustomEvent('am:po-recalc', false, false, null);
        }
        table.dispatchEvent(evt);
    }

    function setHint(tr, text) {
        var cell = $(tr).find('.am-po-cost').closest('td');
        var hint = cell.find('.am-po-cost-hint');
        if (!text) {
            hint.remove();
            return;
        }
        if (hint.length === 0) {
            hint = $('<div class="form-text small am-po-cost-hint"></div>');
            cell.append(hint);
        }
        hint.text(text);
    }

    /* Applies a supplier's prices to the PO lines. explicitLineId: the line a "Select" was pressed for (its typed
       cost is replaced); 'all' replaces typed costs on every covered line; null respects typed costs everywhere. */
    function applySupplier(supplierId, explicitLineId, currency) {
        if (supplierId) {
            $('#SupplierId').val(String(supplierId));
        }
        if (currency) {
            $('#Currency').val(currency);
        }
        poRows().each(function () {
            var tr = this;
            var input = $(tr).find('.am-po-cost');
            if (input.length === 0) {
                return;
            }
            if (!supplierId) {
                setHint(tr, '');
                return;
            }
            var offer = findSupplierRow(rowsForPoLine(tr), supplierId);
            if (!offer) {
                setHint(tr, 'No price from this supplier for this line.');
                return;
            }
            var explicit = explicitLineId === 'all' || (explicitLineId !== null && explicitLineId === lineIdOf(tr));
            var unit = parseNumber(offer.UnitPrice);
            if (unit !== null && (explicit || input.attr(EDITED) !== 'true')) {
                input.val(unit.toFixed(2));
                input.attr(EDITED, 'false');
            }
            setHint(tr, offer.IsHistorical ? 'Historical average for this supplier.' : '');
        });
        fireRecalc();
    }

    function lineLabel(line) {
        if (!line) {
            return '';
        }
        var text = line.ItemDescription || '';
        if (text.length > 40) {
            text = text.substring(0, 40) + '\u2026';
        }
        return line.LineNumber + (text ? ' \u00b7 ' + text : '');
    }

    function badgesFor(row) {
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
        return badges.join('');
    }

    function appendRow(tbody, row, line, showLine) {
        var tr = $('<tr></tr>');
        if (showLine) {
            tr.append($('<td class="am-cmp-line"></td>').text(lineLabel(line)));
        }
        tr.append($('<td></td>').html(escapeHtml(row.SupplierName) + badgesFor(row)));
        tr.append($('<td></td>').text(row.ItemLabel || ''));
        tr.append($('<td></td>').text(formatMoney(row.UnitPrice, row.Currency)));
        tr.append($('<td></td>').text(row.LeadTimeDays ? row.LeadTimeDays + ' days' : ''));
        tr.append($('<td></td>').html(
            '<button type="button" class="btn btn-sm btn-outline-primary select-offer"' +
            ' data-supplier-id="' + escapeHtml(row.SupplierId) + '"' +
            ' data-currency="' + escapeHtml(row.Currency || '') + '"' +
            ' data-line-id="' + (line ? escapeHtml(line.PurchaseRequestLineId) : '') + '">Select</button>'));
        tbody.append(tr);
    }

    function renderRows(data) {
        var panel = $('#supplier-comparison-panel');
        var table = $('#comparison-table');
        var tbody = table.find('tbody');
        var empty = $('#comparison-empty');
        tbody.empty();
        comparison = {
            Rows: (data && data.Rows) ? data.Rows : [],
            Lines: (data && data.Lines) ? data.Lines : [],
            Currency: data ? data.Currency : null
        };

        // Several lines: one block of offers per line (each line is its own item); otherwise the single list.
        var perLine = comparison.Lines.length > 1;
        var count = 0;
        if (perLine) {
            $.each(comparison.Lines, function (_, line) {
                $.each(line.Rows || [], function (__, row) {
                    appendRow(tbody, row, line, true);
                    count++;
                });
            });
        } else {
            var single = comparison.Lines.length === 1 ? comparison.Lines[0] : null;
            $.each(single ? (single.Rows || []) : comparison.Rows, function (_, row) {
                appendRow(tbody, row, single, false);
                count++;
            });
        }
        table.find('thead .am-cmp-line').toggle(perLine);

        if (count === 0) {
            empty.text('No catalog or historical supplier prices matched this item. Enter supplier and cost manually.');
            table.hide();
            empty.show();
            panel.show();
            return;
        }

        if (data.Currency) {
            $('#Currency').val(data.Currency);
        }

        var anyHistorical = data.HasHistoricalFallback;
        var anyCatalog = data.HasCatalogMatches;
        $.each(comparison.Lines, function (_, line) {
            anyHistorical = anyHistorical || line.HasHistoricalFallback;
            anyCatalog = anyCatalog || line.HasCatalogMatches;
        });
        var note = anyHistorical
            ? (anyCatalog ? 'Catalog quotes, and historical purchase averages where no quote matched.' : 'Showing historical purchase averages (not current catalog quotes).')
            : (anyCatalog ? 'Catalog quotes sorted lowest to highest.' : '');
        if (perLine) {
            note += (note ? ' ' : '') + 'Choosing a supplier fills its price on every line it has one for.';
        }
        if (note) {
            empty.text(note);
            empty.show();
        } else {
            empty.hide();
        }

        table.show();
        panel.show();

        var selectedSupplierId = $('#SupplierId').val();
        if (selectedSupplierId) {
            applySupplier(selectedSupplierId, null, null);
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
        var button = $(this);
        var lineId = parseInt(button.attr('data-line-id'), 10);
        // A per-line row replaces that line's typed cost; the single list (one item) replaces it on every covered line.
        applySupplier(button.attr('data-supplier-id'), isNaN(lineId) || comparison.Lines.length <= 1 ? 'all' : lineId, button.attr('data-currency'));
    });

    $('#SupplierId').on('change', function () {
        applySupplier($(this).val(), null, null);
    });

    $(document).on('input change', '#po-lines-table .am-po-cost', function () {
        $(this).attr(EDITED, $.trim($(this).val()) !== '' ? 'true' : 'false');
    });

    $('#refresh-comparison').on('click', loadComparison);
    $('#manual-item-description').on('change blur', loadComparison);

    $(function () {
        poRows().find('.am-po-cost').each(function () {
            var initial = parseNumber($(this).val());
            $(this).attr(EDITED, initial !== null && initial > 0 ? 'true' : 'false');
        });
        loadComparison();
    });
})(jQuery);
