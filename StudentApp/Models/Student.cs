namespace StudentApp.Models;

/// <summary>A single student record as stored in the JSON data file.</summary>
public sealed class Student
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }

    /// <summary>Optional interests, skills, goals, or other searchable profile text.</summary>
    public string ProfileText { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
