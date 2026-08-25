using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Helpers
{
    public static class SchoolClassCodeHelper
    {
        public const int MaxGrade = 6;

        private static readonly Regex ClassPattern = new Regex(
            @"^\s*(?<grade>\d{1,2})\s*(?<stream>[A-Da-d]{1,2})\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryParseClass(string classValue, out int grade, out string stream)
        {
            grade = 0;
            stream = null;
            if (string.IsNullOrWhiteSpace(classValue))
            {
                return false;
            }

            var match = ClassPattern.Match(classValue.Trim());
            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups["grade"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out grade))
            {
                return false;
            }

            stream = match.Groups["stream"].Value.Trim().ToUpperInvariant();
            return grade >= 1 && grade <= MaxGrade && !string.IsNullOrWhiteSpace(stream);
        }

        public static string BuildClassDepartmentCode(int grade, string stream)
        {
            if (grade < 1 || grade > MaxGrade)
            {
                throw new ArgumentOutOfRangeException("grade", "Grade must be between 1 and " + MaxGrade + ".");
            }

            var normalizedStream = (stream ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalizedStream))
            {
                throw new ArgumentException("Stream is required.", "stream");
            }

            return "G" + grade.ToString("00", CultureInfo.InvariantCulture) + normalizedStream;
        }

        public static string BuildClassDepartmentCode(string classValue)
        {
            int grade;
            string stream;
            if (!TryParseClass(classValue, out grade, out stream))
            {
                return null;
            }

            return BuildClassDepartmentCode(grade, stream);
        }

        public static string BuildGradeDepartmentCode(int grade)
        {
            if (grade < 1 || grade > MaxGrade)
            {
                throw new ArgumentOutOfRangeException("grade", "Grade must be between 1 and " + MaxGrade + ".");
            }

            return "G" + grade.ToString("00", CultureInfo.InvariantCulture);
        }

        public static string BuildClassDepartmentName(int grade, string stream)
        {
            return "Grade " + grade + stream.Trim().ToUpperInvariant();
        }

        public static string BuildGradeDepartmentName(int grade)
        {
            return "Grade " + grade;
        }

        public static bool IsClassroomDepartment(string departmentName)
        {
            return string.Equals((departmentName ?? string.Empty).Trim(), "Classroom", StringComparison.OrdinalIgnoreCase);
        }
    }
}
