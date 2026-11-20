using System.Globalization;
using System.Text;
using StudentApp.Models;

namespace StudentApp.Utils;

/// <summary>Writes students as RFC 4180 CSV that opens cleanly in Excel and is safe against formula injection.</summary>
public static class CsvExporter
{
    public const string Header = "Id,Name,Age,ProfileText,CreatedAtUtc,UpdatedAtUtc";

    /// <summary>Builds the CSV text (CRLF line endings, UTF-8 friendly quoting).</summary>
    public static string ToCsv(IEnumerable<Student> students)
    {
        var sb = new StringBuilder(Header).Append("\r\n");
        foreach (var s in students.OrderBy(s => s.Id))
        {
            sb.Append(s.Id.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Escape(s.Name)).Append(',')
              .Append(s.Age.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Escape(s.ProfileText)).Append(',')
              .Append(s.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append(',')
              .Append(s.UpdatedAtUtc.ToString("O", CultureInfo.InvariantCulture)).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>Writes the CSV to <paramref name="filePath"/>, creating the folder if needed.</summary>
    public static void Export(IEnumerable<Student> students, string filePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, ToCsv(students), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    /// <summary>
    /// Quotes the value and neutralises "CSV injection": a cell beginning with = + - @ (or a tab/CR)
    /// would be executed as a formula by Excel/Sheets, so such values get a leading apostrophe.
    /// </summary>
    internal static string Escape(string value)
    {
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r') value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
