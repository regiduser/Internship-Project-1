using StudentApp.Models;

namespace StudentApp.Services;

/// <summary>Storage abstraction so the service layer can be tested without touching the disk.</summary>
public interface IStudentRepository
{
    /// <summary>Location of the backing store, for display purposes.</summary>
    string DataFilePath { get; }

    List<Student> Load();

    void Save(IReadOnlyCollection<Student> students);
}
