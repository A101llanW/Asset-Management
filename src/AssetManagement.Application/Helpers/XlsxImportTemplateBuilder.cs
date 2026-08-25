using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ICSharpCode.SharpZipLib.Zip;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Builds a minimal .xlsx workbook with optional data-validation dropdowns.
    /// Uses SharpZipLib (already referenced) — no extra NuGet packages.
    /// </summary>
    public static class XlsxImportTemplateBuilder
    {
        private const int DataStartRow = 4;
        private const int DataEndRow = 2002;
        private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace OfficeRelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace ContentTypesNs = "http://schemas.openxmlformats.org/package/2006/content-types";

        public static byte[] Build(
            IList<string> headers,
            IList<string> requirementLabels,
            IList<string> sampleRow,
            IDictionary<string, string[]> dropdownsByHeader,
            IList<string> instructionLines = null,
            ISet<string> manualEntryHeaders = null)
        {
            if (headers == null || headers.Count == 0)
            {
                throw new ArgumentException("Headers are required.", "headers");
            }

            var sharedStrings = new List<string>();
            var stringIndex = new Dictionary<string, int>(StringComparer.Ordinal);

            Func<string, int> intern = value =>
            {
                var text = value ?? string.Empty;
                int index;
                if (stringIndex.TryGetValue(text, out index))
                {
                    return index;
                }

                index = sharedStrings.Count;
                sharedStrings.Add(text);
                stringIndex[text] = index;
                return index;
            };

            var importRows = new List<IList<string>>
            {
                headers,
                requirementLabels ?? new string[headers.Count],
                sampleRow ?? new string[headers.Count]
            };

            var listColumns = new List<KeyValuePair<string, string[]>>();
            if (dropdownsByHeader != null)
            {
                foreach (var pair in dropdownsByHeader)
                {
                    if (pair.Value == null || pair.Value.Length == 0)
                    {
                        continue;
                    }

                    listColumns.Add(new KeyValuePair<string, string[]>(pair.Key, pair.Value));
                }
            }

            var listSheetRows = BuildListSheetRows(listColumns);
            var validations = BuildValidations(headers, listColumns, manualEntryHeaders);

            var importSheetXml = BuildSheetXml(importRows, intern, validations, false);
            var listsSheetXml = BuildSheetXml(listSheetRows, intern, null, true);
            var instructionRows = BuildInstructionRows(instructionLines);
            var instructionsSheetXml = instructionRows.Count > 0
                ? BuildSheetXml(instructionRows, intern, null, false)
                : null;
            var sharedStringsXml = BuildSharedStringsXml(sharedStrings);
            var workbookXml = BuildWorkbookXml(instructionRows.Count > 0);
            var workbookRelsXml = BuildWorkbookRelsXml(instructionRows.Count > 0);
            var contentTypesXml = BuildContentTypesXml(instructionRows.Count > 0);
            var packageRelsXml = BuildPackageRelsXml();
            var stylesXml = BuildStylesXml();

            using (var stream = new MemoryStream())
            {
                using (var zip = new ZipOutputStream(stream))
                {
                    zip.IsStreamOwner = false;
                    zip.SetLevel(6);
                    WriteEntry(zip, "[Content_Types].xml", contentTypesXml);
                    WriteEntry(zip, "_rels/.rels", packageRelsXml);
                    WriteEntry(zip, "xl/workbook.xml", workbookXml);
                    WriteEntry(zip, "xl/_rels/workbook.xml.rels", workbookRelsXml);
                    WriteEntry(zip, "xl/styles.xml", stylesXml);
                    WriteEntry(zip, "xl/sharedStrings.xml", sharedStringsXml);
                    if (instructionsSheetXml != null)
                    {
                        WriteEntry(zip, "xl/worksheets/sheet1.xml", instructionsSheetXml);
                        WriteEntry(zip, "xl/worksheets/sheet2.xml", importSheetXml);
                        WriteEntry(zip, "xl/worksheets/sheet3.xml", listsSheetXml);
                    }
                    else
                    {
                        WriteEntry(zip, "xl/worksheets/sheet1.xml", importSheetXml);
                        WriteEntry(zip, "xl/worksheets/sheet2.xml", listsSheetXml);
                    }
                    zip.Finish();
                }

                return stream.ToArray();
            }
        }

        private static IList<IList<string>> BuildInstructionRows(IList<string> instructionLines)
        {
            var rows = new List<IList<string>>();
            if (instructionLines == null || instructionLines.Count == 0)
            {
                return rows;
            }

            foreach (var line in instructionLines)
            {
                rows.Add(new[] { line ?? string.Empty });
            }

            return rows;
        }

        private static IList<IList<string>> BuildListSheetRows(IList<KeyValuePair<string, string[]>> listColumns)
        {
            var rows = new List<IList<string>>();
            if (listColumns.Count == 0)
            {
                rows.Add(new[] { "Empty" });
                return rows;
            }

            var header = listColumns.Select(x => x.Key).ToArray();
            rows.Add(header);

            var maxLen = listColumns.Max(x => x.Value.Length);
            for (var i = 0; i < maxLen; i++)
            {
                var row = new string[listColumns.Count];
                for (var col = 0; col < listColumns.Count; col++)
                {
                    var values = listColumns[col].Value;
                    row[col] = i < values.Length ? values[i] : string.Empty;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static IList<XElement> BuildValidations(
            IList<string> headers,
            IList<KeyValuePair<string, string[]>> listColumns,
            ISet<string> manualEntryHeaders)
        {
            var validations = new List<XElement>();
            for (var col = 0; col < headers.Count; col++)
            {
                var headerKey = NormalizeHeaderKey(headers[col]);
                var listIndex = -1;
                for (var i = 0; i < listColumns.Count; i++)
                {
                    if (string.Equals(listColumns[i].Key, headerKey, StringComparison.OrdinalIgnoreCase))
                    {
                        listIndex = i;
                        break;
                    }
                }

                if (listIndex < 0)
                {
                    continue;
                }

                var listColLetter = ToColumnLetter(listIndex + 1);
                var valueCount = Math.Max(1, listColumns[listIndex].Value.Length);
                var formula = "Lists!$" + listColLetter + "$2:$" + listColLetter + "$" + (valueCount + 1);
                var dataColLetter = ToColumnLetter(col + 1);
                var sqref = dataColLetter + DataStartRow + ":" + dataColLetter + DataEndRow;
                var allowManualEntry = manualEntryHeaders != null
                    && manualEntryHeaders.Contains(headerKey);

                var validation = new XElement(SpreadsheetNs + "dataValidation",
                    new XAttribute("type", "list"),
                    new XAttribute("allowBlank", "1"),
                    new XAttribute("showDropDown", "0"),
                    new XAttribute("sqref", sqref),
                    new XElement(SpreadsheetNs + "formula1", formula));

                if (allowManualEntry)
                {
                    validation.Add(new XAttribute("errorStyle", "warning"));
                    validation.Add(new XAttribute("showErrorMessage", "1"));
                    validation.Add(new XAttribute("errorTitle", "Not in reference list"));
                    validation.Add(new XAttribute("error", "You can type your own value or pick from the list."));
                }
                else
                {
                    validation.Add(new XAttribute("showErrorMessage", "1"));
                    validation.Add(new XAttribute("errorTitle", "Invalid value"));
                    validation.Add(new XAttribute("error", "Please select a value from the list."));
                }

                validations.Add(validation);
            }

            return validations;
        }

        private static string NormalizeHeaderKey(string header)
        {
            var value = (header ?? string.Empty).Trim();
            if (value.StartsWith("*", StringComparison.Ordinal))
            {
                value = value.Substring(1).Trim();
            }

            return value;
        }

        private static string BuildSheetXml(
            IList<IList<string>> rows,
            Func<string, int> intern,
            IList<XElement> validations,
            bool hidden)
        {
            var sheetData = new XElement(SpreadsheetNs + "sheetData");
            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var rowValues = rows[rowIndex] ?? new string[0];
                var rowElement = new XElement(SpreadsheetNs + "row", new XAttribute("r", rowIndex + 1));
                for (var col = 0; col < rowValues.Count; col++)
                {
                    var cellRef = ToColumnLetter(col + 1) + (rowIndex + 1);
                    var text = rowValues[col] ?? string.Empty;
                    var index = intern(text);
                    rowElement.Add(new XElement(SpreadsheetNs + "c",
                        new XAttribute("r", cellRef),
                        new XAttribute("t", "s"),
                        new XElement(SpreadsheetNs + "v", index.ToString(CultureInfo.InvariantCulture))));
                }

                sheetData.Add(rowElement);
            }

            var lastCol = 1;
            var lastRow = Math.Max(1, rows.Count);
            foreach (var rowValues in rows)
            {
                if (rowValues != null && rowValues.Count > lastCol)
                {
                    lastCol = rowValues.Count;
                }
            }

            var dimensionRef = "A1:" + ToColumnLetter(lastCol) + lastRow.ToString(CultureInfo.InvariantCulture);
            var worksheet = new XElement(SpreadsheetNs + "worksheet",
                new XAttribute("xmlns", SpreadsheetNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "r", OfficeRelationshipNs.NamespaceName),
                new XElement(SpreadsheetNs + "dimension", new XAttribute("ref", dimensionRef)),
                sheetData);

            if (hidden)
            {
                // Visibility is controlled on workbook sheet entry; keep worksheet normal.
            }

            if (validations != null && validations.Count > 0)
            {
                worksheet.Add(new XElement(SpreadsheetNs + "dataValidations",
                    new XAttribute("count", validations.Count.ToString(CultureInfo.InvariantCulture)),
                    validations));
            }

            return Declaration + worksheet;
        }

        private static string BuildSharedStringsXml(IList<string> sharedStrings)
        {
            var root = new XElement(SpreadsheetNs + "sst",
                new XAttribute("xmlns", SpreadsheetNs.NamespaceName),
                new XAttribute("count", sharedStrings.Count.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("uniqueCount", sharedStrings.Count.ToString(CultureInfo.InvariantCulture)));

            foreach (var value in sharedStrings)
            {
                root.Add(new XElement(SpreadsheetNs + "si",
                    new XElement(SpreadsheetNs + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        value ?? string.Empty)));
            }

            return Declaration + root;
        }

        private static string BuildWorkbookXml(bool includeInstructionsSheet)
        {
            var sheets = new XElement(SpreadsheetNs + "sheets");
            if (includeInstructionsSheet)
            {
                sheets.Add(new XElement(SpreadsheetNs + "sheet",
                    new XAttribute("name", "Instructions"),
                    new XAttribute("sheetId", "1"),
                    new XAttribute(OfficeRelationshipNs + "id", "rId1")));
                sheets.Add(new XElement(SpreadsheetNs + "sheet",
                    new XAttribute("name", "Import"),
                    new XAttribute("sheetId", "2"),
                    new XAttribute(OfficeRelationshipNs + "id", "rId2")));
                sheets.Add(new XElement(SpreadsheetNs + "sheet",
                    new XAttribute("name", "Lists"),
                    new XAttribute("sheetId", "3"),
                    new XAttribute("state", "hidden"),
                    new XAttribute(OfficeRelationshipNs + "id", "rId3")));
            }
            else
            {
                sheets.Add(new XElement(SpreadsheetNs + "sheet",
                    new XAttribute("name", "Import"),
                    new XAttribute("sheetId", "1"),
                    new XAttribute(OfficeRelationshipNs + "id", "rId1")));
                sheets.Add(new XElement(SpreadsheetNs + "sheet",
                    new XAttribute("name", "Lists"),
                    new XAttribute("sheetId", "2"),
                    new XAttribute("state", "hidden"),
                    new XAttribute(OfficeRelationshipNs + "id", "rId2")));
            }

            var workbook = new XElement(SpreadsheetNs + "workbook",
                new XAttribute("xmlns", SpreadsheetNs.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "r", OfficeRelationshipNs.NamespaceName),
                sheets);

            return Declaration + workbook;
        }

        private static string BuildWorkbookRelsXml(bool includeInstructionsSheet)
        {
            var root = new XElement(RelationshipNs + "Relationships",
                new XAttribute("xmlns", RelationshipNs.NamespaceName));

            if (includeInstructionsSheet)
            {
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml")));
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet2.xml")));
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId3"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet3.xml")));
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId4"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"),
                    new XAttribute("Target", "sharedStrings.xml")));
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId5"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml")));
            }
            else
            {
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet1.xml")));
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId2"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                    new XAttribute("Target", "worksheets/sheet2.xml")));
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId3"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"),
                    new XAttribute("Target", "sharedStrings.xml")));
                root.Add(new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId4"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                    new XAttribute("Target", "styles.xml")));
            }

            return Declaration + root;
        }

        private static string BuildPackageRelsXml()
        {
            var root = new XElement(RelationshipNs + "Relationships",
                new XAttribute("xmlns", RelationshipNs.NamespaceName),
                new XElement(RelationshipNs + "Relationship",
                    new XAttribute("Id", "rId1"),
                    new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new XAttribute("Target", "xl/workbook.xml")));

            return Declaration + root;
        }

        private static string BuildContentTypesXml(bool includeInstructionsSheet)
        {
            var root = new XElement(ContentTypesNs + "Types",
                new XAttribute("xmlns", ContentTypesNs.NamespaceName),
                new XElement(ContentTypesNs + "Default",
                    new XAttribute("Extension", "rels"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ContentTypesNs + "Default",
                    new XAttribute("Extension", "xml"),
                    new XAttribute("ContentType", "application/xml")),
                new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/workbook.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/worksheets/sheet2.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));

            if (includeInstructionsSheet)
            {
                root.Add(new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/worksheets/sheet3.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
            }

            root.Add(new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/sharedStrings.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml")));
            root.Add(new XElement(ContentTypesNs + "Override",
                    new XAttribute("PartName", "/xl/styles.xml"),
                    new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")));

            return Declaration + root;
        }

        private static string BuildStylesXml()
        {
            var root = new XElement(SpreadsheetNs + "styleSheet",
                new XAttribute("xmlns", SpreadsheetNs.NamespaceName),
                new XElement(SpreadsheetNs + "fonts", new XAttribute("count", "1"),
                    new XElement(SpreadsheetNs + "font",
                        new XElement(SpreadsheetNs + "sz", new XAttribute("val", "11")),
                        new XElement(SpreadsheetNs + "name", new XAttribute("val", "Calibri")))),
                new XElement(SpreadsheetNs + "fills", new XAttribute("count", "1"),
                    new XElement(SpreadsheetNs + "fill",
                        new XElement(SpreadsheetNs + "patternFill", new XAttribute("patternType", "none")))),
                new XElement(SpreadsheetNs + "borders", new XAttribute("count", "1"),
                    new XElement(SpreadsheetNs + "border")),
                new XElement(SpreadsheetNs + "cellStyleXfs", new XAttribute("count", "1"),
                    new XElement(SpreadsheetNs + "xf",
                        new XAttribute("numFmtId", "0"),
                        new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"),
                        new XAttribute("borderId", "0"))),
                new XElement(SpreadsheetNs + "cellXfs", new XAttribute("count", "1"),
                    new XElement(SpreadsheetNs + "xf",
                        new XAttribute("numFmtId", "0"),
                        new XAttribute("fontId", "0"),
                        new XAttribute("fillId", "0"),
                        new XAttribute("borderId", "0"),
                        new XAttribute("xfId", "0"))));

            return Declaration + root;
        }

        private static void WriteEntry(ZipOutputStream zip, string entryName, string content)
        {
            var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            var entry = new ZipEntry(entryName);
            entry.DateTime = DateTime.UtcNow;
            entry.Size = bytes.Length;
            zip.PutNextEntry(entry);
            zip.Write(bytes, 0, bytes.Length);
            zip.CloseEntry();
        }

        private static string ToColumnLetter(int columnNumber)
        {
            if (columnNumber <= 0)
            {
                return "A";
            }

            var dividend = columnNumber;
            var columnName = string.Empty;
            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }

            return columnName;
        }

        private static readonly string Declaration = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" + Environment.NewLine;
    }
}
