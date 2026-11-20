using StudentApp.Models;
using StudentApp.Utils;

namespace StudentApp.Tests;

public class CsvExporterTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private static readonly DateTime Stamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static Student Sample(int id, string name, string profile = "") =>
        new() { Id = id, Name = name, Age = 20, ProfileText = profile, CreatedAtUtc = Stamp, UpdatedAtUtc = Stamp };

    [Fact]
    public void Produces_header_and_quoted_rows_in_id_order()
    {
        var csv = CsvExporter.ToCsv([Sample(2, "Bob"), Sample(1, "Ada", "maths")]);
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(CsvExporter.Header, lines[0]);
        Assert.Equal("1,\"Ada\",20,\"maths\",2026-01-02T03:04:05.0000000Z,2026-01-02T03:04:05.0000000Z", lines[1]);
        Assert.StartsWith("2,\"Bob\"", lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void Commas_quotes_and_newlines_stay_inside_one_quoted_cell()
    {
        var csv = CsvExporter.ToCsv([Sample(1, "Smith, \"Jo\"", "line one\nline two")]);
        Assert.Contains("\"Smith, \"\"Jo\"\"\"", csv);
        Assert.Contains("\"line one\nline two\"", csv);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void Spreadsheet_formulas_are_neutralised(string malicious)
    {
        var csv = CsvExporter.ToCsv([Sample(1, malicious)]);
        Assert.Contains(",\"'" + malicious.Replace("\"", "\"\"") + "\",", csv);
    }

    [Fact]
    public void Normal_text_is_not_modified()
    {
        Assert.Equal("\"Ada\"", CsvExporter.Escape("Ada"));
        Assert.Equal("\"\"", CsvExporter.Escape(""));
    }

    [Fact]
    public void Export_creates_folder_and_writes_a_utf8_file_with_bom()
    {
        var path = Path.Combine(_dir.Path, "out", "students.csv");
        CsvExporter.Export([Sample(1, "Zoë")], path);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        Assert.Contains("Zoë", File.ReadAllText(path));
    }
}
