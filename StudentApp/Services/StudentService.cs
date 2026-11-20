using StudentApp.Models;

namespace StudentApp.Services;

public enum StudentSort { Id, Name, Age, RecentlyUpdated }

/// <summary>
/// Business rules for student CRUD, searching and statistics. Every mutation is all-or-nothing:
/// if the repository cannot save, the in-memory list is rolled back so memory and disk never disagree.
/// </summary>
public sealed class StudentService
{
    /// <summary>Hits scoring below this are treated as noise and dropped.</summary>
    public const double MinSemanticScore = 0.05;

    private static readonly (string Label, int Min, int Max)[] AgeBandDefinitions =
    [
        ("Under 18", 0, 17), ("18-22", 18, 22), ("23-30", 23, 30), ("31-45", 31, 45), ("46+", 46, int.MaxValue)
    ];

    private readonly IStudentRepository _repository;
    private readonly List<Student> _students;
    private readonly Dictionary<int, float[]> _vectorCache = [];

    public StudentService(IStudentRepository repository)
    {
        _repository = repository;
        _students = repository.Load();
    }

    public int Count => _students.Count;

    // ----------------------------------------------------------------------------- queries

    public IReadOnlyList<Student> GetAll(StudentSort sort = StudentSort.Id) => (sort switch
    {
        StudentSort.Name => _students.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ThenBy(s => s.Id),
        StudentSort.Age => _students.OrderBy(s => s.Age).ThenBy(s => s.Id),
        StudentSort.RecentlyUpdated => _students.OrderByDescending(s => s.UpdatedAtUtc).ThenBy(s => s.Id),
        _ => _students.OrderBy(s => s.Id)
    }).ToList();

    public Student? FindById(int id) => _students.FirstOrDefault(s => s.Id == id);

    /// <summary>Case-insensitive name substring match; a numeric term also matches that exact ID.</summary>
    public IReadOnlyList<Student> Search(string term)
    {
        term = (term ?? string.Empty).Trim();
        if (term.Length == 0) return [];
        var hasId = int.TryParse(term, out var id);
        return _students
            .Where(s => (hasId && s.Id == id) || s.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Id)
            .ToList();
    }

    /// <summary>
    /// Ranks students by cosine similarity between the query vector and each student's
    /// name + profile vector. Each hit carries the shared words/concepts that explain it.
    /// </summary>
    public IReadOnlyList<SemanticMatch> SemanticSearch(string query, int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var queryVector = EmbeddingService.Embed(query);
        var queryTerms = EmbeddingService.ExtractTerms(query);
        var queryConcepts = EmbeddingService.ExtractConcepts(query);

        return _students
            .Select(student => (Student: student, Score: EmbeddingService.CosineSimilarity(queryVector, VectorFor(student))))
            .Where(r => r.Score >= MinSemanticScore)
            .Select(r =>
            {
                var text = SearchableText(r.Student);
                return new SemanticMatch(
                    r.Student,
                    r.Score,
                    queryTerms.Intersect(EmbeddingService.ExtractTerms(text)).ToList(),
                    queryConcepts.Intersect(EmbeddingService.ExtractConcepts(text)).ToList());
            })
            // Hashing into 512 slots means unrelated words can occasionally collide and produce a small
            // non-zero score. Requiring at least one shared word or concept removes those false positives
            // and guarantees every result can be explained.
            .Where(m => m.SharedTerms.Count > 0 || m.SharedConcepts.Count > 0)
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Student.Id)
            .Take(Math.Clamp(maxResults, 1, 100))
            .ToList();
    }

    public StudentStatistics GetStatistics()
    {
        if (_students.Count == 0) return StudentStatistics.Empty;

        var ages = _students.Select(s => s.Age).Order().ToList();
        var middle = ages.Count / 2;
        var median = ages.Count % 2 == 1 ? ages[middle] : (ages[middle - 1] + ages[middle]) / 2.0;
        var bands = AgeBandDefinitions
            .Select(b => new AgeBand(b.Label, ages.Count(a => a >= b.Min && a <= b.Max)))
            .ToList();

        return new StudentStatistics(
            _students.Count, ages.Average(), median, ages[0], ages[^1],
            _students.Count(s => s.ProfileText.Length > 0), bands);
    }

    // ----------------------------------------------------------------------------- commands

    public Student Add(int id, string name, int age, string profileText = "")
    {
        profileText ??= string.Empty;
        Require(StudentRules.Check(id, name, age, profileText));
        if (FindById(id) is not null) throw new InvalidOperationException($"Student ID {id} already exists.");

        var student = new Student { Id = id, Name = name.Trim(), Age = age, ProfileText = profileText.Trim() };
        _students.Add(student);
        try { Save(); }
        catch { _students.Remove(student); throw; }
        return student;
    }

    /// <param name="profileText">New profile text, or <c>null</c> to leave the profile unchanged
    /// (pass an empty string to clear it).</param>
    public void Update(int id, string name, int age, string? profileText = null)
    {
        var student = FindById(id) ?? throw new InvalidOperationException("Student not found.");
        Require(StudentRules.Check(id, name, age, profileText));

        var (oldName, oldAge, oldProfile, oldUpdated) = (student.Name, student.Age, student.ProfileText, student.UpdatedAtUtc);
        student.Name = name.Trim();
        student.Age = age;
        if (profileText is not null) student.ProfileText = profileText.Trim();
        student.UpdatedAtUtc = DateTime.UtcNow;
        try { Save(); }
        catch
        {
            (student.Name, student.Age, student.ProfileText, student.UpdatedAtUtc) = (oldName, oldAge, oldProfile, oldUpdated);
            throw;
        }
        finally { _vectorCache.Remove(id); }
    }

    public bool Delete(int id)
    {
        var index = _students.FindIndex(s => s.Id == id);
        if (index < 0) return false;

        var student = _students[index];
        _students.RemoveAt(index);
        try { Save(); }
        catch { _students.Insert(index, student); throw; }
        _vectorCache.Remove(id);
        return true;
    }

    // ----------------------------------------------------------------------------- helpers

    /// <summary>Vectors are cached per student and dropped whenever that student changes.</summary>
    private float[] VectorFor(Student student)
    {
        if (!_vectorCache.TryGetValue(student.Id, out var vector))
            _vectorCache[student.Id] = vector = EmbeddingService.Embed(SearchableText(student));
        return vector;
    }

    private static string SearchableText(Student student) => $"{student.Name} {student.ProfileText}";

    private static void Require(string? problem)
    {
        if (problem is not null) throw new ArgumentException(problem);
    }

    private void Save() => _repository.Save(_students);
}
