using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AssetManagement.Application.Helpers
{
    public static class CsvExportHelper
    {
        public static byte[] ToUtf8Bytes(IEnumerable<string[]> rows)
        {
            var content = Build(rows);
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray();
        }

        public static string Build(IEnumerable<string[]> rows)
        {
            var sb = new StringBuilder();
            if (rows == null)
            {
                return sb.ToString();
            }

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",", (row ?? new string[0]).Select(EscapeField)));
            }

            return sb.ToString();
        }

        private static string EscapeField(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\r") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }
}
