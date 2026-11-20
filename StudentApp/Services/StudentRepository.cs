using System.Text.Json;
using StudentApp.Models;

namespace StudentApp.Services;

/// <summary>
/// Persists student records as JSON. Every save writes a temporary file, flushes it to disk and then
/// swaps it in, keeping the previous version as <c>students.json.bak</c>, so a crash or power loss
/// mid-save can never leave a half-written data file.
/// </summary>
public sealed class StudentRepository : IStudentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string DataFilePath { get; }

    public StudentRepository(string? filePath = null)
    {
        DataFilePath = Path.GetFullPath(filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StudentApp", "students.json"));
    }

    public List<Student> Load()
    {
        if (!File.Exists(DataFilePath)) return [];

        List<Student> students;
        try
        {
            using var stream = File.OpenRead(DataFilePath);
            students = JsonSerializer.Deserialize<List<Student>>(stream, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"Student data file is not valid JSON: {DataFilePath}. " +
                $"A previous copy may exist at {DataFilePath}.bak — keep a backup before replacing anything.", ex);
        }

        return Validate(students);
    }

    public void Save(IReadOnlyCollection<Student> students)
    {
        var directory = Path.GetDirectoryName(DataFilePath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

        var temporaryPath = DataFilePath + ".tmp";
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, students.OrderBy(s => s.Id), JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(DataFilePath))
                File.Replace(temporaryPath, DataFilePath, DataFilePath + ".bak", ignoreMetadataErrors: true);
            else
                File.Move(temporaryPath, DataFilePath);
        }
        catch
        {
            TryDelete(temporaryPath);
            throw;
        }
    }

    /// <summary>
    /// Hand-edited or corrupted files should fail loudly at start-up, not surface as odd behaviour
    /// later. Also repairs the one benign case: <c>"ProfileText": null</c> from older files.
    /// </summary>
    private List<Student> Validate(List<Student> students)
    {
        var seen = new HashSet<int>();
        foreach (var student in students)
        {
            if (student is null)
                throw new InvalidDataException($"{DataFilePath} contains an empty (null) record.");

            student.Name ??= string.Empty;
            student.ProfileText ??= string.Empty;

            var problem = StudentRules.Check(student.Id, student.Name, student.Age, student.ProfileText);
            if (problem is not null)
                throw new InvalidDataException($"Record with ID {student.Id} in {DataFilePath} is invalid: {problem}");
            if (!seen.Add(student.Id))
                throw new InvalidDataException($"{DataFilePath} contains duplicate student ID {student.Id}.");
        }
        return students;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
