using StudentApp.Models;
using StudentApp.Services;

namespace StudentApp.Tests;

/// <summary>A throw-away folder that is deleted when the test finishes.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "studentapp-tests-" + Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}

/// <summary>
/// Repository that keeps "persisted" copies in memory and can be told to fail, so rollback
/// behaviour can be tested without a real disk.
/// </summary>
internal sealed class InMemoryRepository : IStudentRepository
{
    private List<Student> _stored = [];

    public string DataFilePath => "(memory)";
    public bool FailNextSave { get; set; }
    public int SaveCount { get; private set; }

    /// <summary>What a fresh application start would load.</summary>
    public IReadOnlyList<Student> Persisted => _stored.Select(Clone).ToList();

    public List<Student> Load() => _stored.Select(Clone).ToList();

    public void Save(IReadOnlyCollection<Student> students)
    {
        if (FailNextSave)
        {
            FailNextSave = false;
            throw new IOException("disk full");
        }
        SaveCount++;
        _stored = students.Select(Clone).ToList();
    }

    private static Student Clone(Student s) => new()
    {
        Id = s.Id, Name = s.Name, Age = s.Age, ProfileText = s.ProfileText,
        CreatedAtUtc = s.CreatedAtUtc, UpdatedAtUtc = s.UpdatedAtUtc
    };
}
