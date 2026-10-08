using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// Role ids are per company (org 11's "Department Head" is not org 13's). Source must resolve roles by name or
    /// from the organisation's own role list, never by a numeric literal. Fails on e.g. "RoleId == 45",
    /// "roleIds.Contains(41)", "RoleId = 3" in src (.cs/.cshtml/.js). Comparisons with 0 are allowed.
    /// </summary>
    [TestFixture]
    public class NoHardCodedRoleIdTests
    {
        private static readonly Regex[] Patterns =
        {
            new Regex(@"\broleids?\s*(==|!=|=)\s*[1-9][0-9]*\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\broleids?\s*\.\s*contains\s*\(\s*[1-9][0-9]*\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\broleid\s*:\s*[1-9][0-9]*\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\broleids?\s*=\s*new\s*(int)?\s*\[\s*\]\s*\{\s*[1-9]", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        [Test]
        public void Source_HasNoNumericRoleIdLiterals()
        {
            var root = FindRepositoryRoot();
            if (root == null)
            {
                Assert.Ignore("Repository root not found (set CODEXASSET_ROOT to the repo folder).");
            }

            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
                .Where(IsSourceFile))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (Patterns.Any(p => p.IsMatch(line)))
                    {
                        offenders.Add(file.Substring(root.Length).TrimStart('\\', '/') + ":" + (i + 1) + ": " + trimmed);
                    }
                }
            }

            Assert.IsEmpty(offenders, "Hard-coded role ids found (resolve roles by name per organisation):" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders.ToArray()));
        }

        private static bool IsSourceFile(string path)
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.Contains("/bin/") || normalized.Contains("/obj/") || normalized.Contains("/packages/")
                || normalized.Contains("/node_modules/") || normalized.EndsWith(".min.js", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("/Scripts/lib/") || normalized.Contains("/lib/"))
            {
                return false;
            }

            var extension = Path.GetExtension(path);
            return string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".cshtml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".js", StringComparison.OrdinalIgnoreCase);
        }

        private static string FindRepositoryRoot()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("CODEXASSET_ROOT");
            if (!string.IsNullOrWhiteSpace(fromEnvironment) && IsRepositoryRoot(fromEnvironment))
            {
                return fromEnvironment;
            }

            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (IsRepositoryRoot(directory.FullName))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private static bool IsRepositoryRoot(string path)
        {
            return Directory.Exists(Path.Combine(path, "src", "AssetManagement.Application"));
        }
    }
}
