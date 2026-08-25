using System;
using System.Collections.Generic;
using System.IO;
using AssetManagement.Application.DTOs;
using Excel;

namespace AssetManagement.Application.Helpers
{
    public static class SpreadsheetImportHelper
    {
        public const int MaxFileSizeBytes = 10 * 1024 * 1024;
        public const int MaxRowCount = 2000;

        public static IList<string[]> ReadRows(Stream stream, string fileName)
        {
            if (stream == null)
            {
                throw new BusinessException("Import file is required.");
            }

            if (stream.CanSeek && stream.Length > MaxFileSizeBytes)
            {
                throw new BusinessException("Import file exceeds the maximum allowed size of 10 MB.");
            }

            var extension = (Path.GetExtension(fileName) ?? string.Empty).ToLowerInvariant();
            IList<string[]> rows;
            if (extension == ".csv")
            {
                rows = CsvImportHelper.Parse(stream);
            }
            else if (extension == ".xlsx")
            {
                rows = ReadExcelRows(stream, false);
            }
            else if (extension == ".xls")
            {
                rows = ReadExcelRows(stream, true);
            }
            else
            {
                throw new BusinessException("Unsupported file type. Upload a .csv, .xlsx, or .xls file.");
            }

            if (rows == null || rows.Count == 0)
            {
                throw new BusinessException("The import file is empty.");
            }

            if (rows.Count - 1 > MaxRowCount)
            {
                throw new BusinessException("Import file exceeds the maximum of " + MaxRowCount + " data rows.");
            }

            return rows;
        }

        public static IList<IDictionary<string, string>> ToRowMaps(IList<string[]> rows)
        {
            if (rows == null || rows.Count < 2)
            {
                throw new BusinessException("The import file must include a header row and at least one data row.");
            }

            var headers = NormalizeHeaders(rows[0]);
            if (headers.Count == 0)
            {
                throw new BusinessException("The header row is missing or invalid.");
            }

            var maps = new List<IDictionary<string, string>>();
            // Official template: row 1 headers, row 2 required/optional legend, row 3 sample, row 4+ data.
            var dataStartIndex = rows.Count > 1 && IsRequirementLegendRow(rows[1]) ? 3 : 1;
            for (var rowIndex = dataStartIndex; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex] ?? new string[0];
                if (IsBlankRow(row))
                {
                    continue;
                }

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var col = 0; col < headers.Count; col++)
                {
                    var header = headers[col];
                    if (string.IsNullOrWhiteSpace(header))
                    {
                        continue;
                    }

                    map[header] = col < row.Length ? (row[col] ?? string.Empty).Trim() : string.Empty;
                }

                maps.Add(map);
            }

            if (maps.Count == 0)
            {
                throw new BusinessException("No data rows were found in the import file.");
            }

            return maps;
        }

        private static IList<string> NormalizeHeaders(string[] headerRow)
        {
            var headers = new List<string>();
            foreach (var cell in headerRow ?? new string[0])
            {
                var header = (cell ?? string.Empty).Trim();
                if (header.StartsWith("*"))
                {
                    header = header.Substring(1).Trim();
                }

                headers.Add(header);
            }

            return headers;
        }

        private static bool IsRequirementLegendRow(string[] row)
        {
            if (row == null || row.Length == 0)
            {
                return false;
            }

            var hasContent = false;
            foreach (var cell in row)
            {
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                hasContent = true;
                var normalized = cell.Trim();
                if (!normalized.StartsWith("Required", StringComparison.OrdinalIgnoreCase)
                    && !normalized.StartsWith("Optional", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return hasContent;
        }

        private static bool IsBlankRow(string[] row)
        {
            if (row == null || row.Length == 0)
            {
                return true;
            }

            foreach (var cell in row)
            {
                if (!string.IsNullOrWhiteSpace(cell))
                {
                    return false;
                }
            }

            return true;
        }

        private static IList<string[]> ReadExcelRows(Stream stream, bool isBinary)
        {
            if (stream == null)
            {
                return new List<string[]>();
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            if (isBinary)
            {
                return ReadRowsFromReader(ExcelReaderFactory.CreateBinaryReader(stream));
            }

            using (var reader = ExcelReaderFactory.CreateOpenXmlReader(stream))
            {
                do
                {
                    if (string.Equals(reader.Name, "Import", StringComparison.OrdinalIgnoreCase))
                    {
                        return ReadRowsFromReader(reader);
                    }
                }
                while (reader.NextResult());
            }

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using (var reader = ExcelReaderFactory.CreateOpenXmlReader(stream))
            {
                return ReadRowsFromReader(reader);
            }
        }

        private static IList<string[]> ReadRowsFromReader(IExcelDataReader reader)
        {
            var rows = new List<string[]>();
            if (reader == null)
            {
                return rows;
            }

            // ExcelDataReader reports FieldCount = -1 before the first Read() when
            // the worksheet has no <dimension> element (our generated templates).
            // Resolve column count per row after Read().
            while (reader.Read())
            {
                var fieldCount = reader.FieldCount;
                if (fieldCount < 0)
                {
                    fieldCount = 0;
                }

                var row = new string[fieldCount];
                for (var i = 0; i < fieldCount; i++)
                {
                    var value = reader.GetValue(i);
                    row[i] = value == null ? string.Empty : Convert.ToString(value).Trim();
                }

                rows.Add(row);
            }

            return rows;
        }
    }
}
