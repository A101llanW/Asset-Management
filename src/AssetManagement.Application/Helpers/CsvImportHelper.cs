using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AssetManagement.Application.Helpers
{
    public static class CsvImportHelper
    {
        public static IList<string[]> Parse(Stream stream)
        {
            var rows = new List<string[]>();
            if (stream == null)
            {
                return rows;
            }

            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    rows.Add(ParseLine(line));
                }
            }

            return rows;
        }

        private static string[] ParseLine(string line)
        {
            var fields = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (inQuotes)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current.Append(ch);
                    }
                }
                else if (ch == '"')
                {
                    inQuotes = true;
                }
                else if (ch == ',')
                {
                    fields.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }

            fields.Add(current.ToString().Trim());
            return fields.ToArray();
        }
    }
}
