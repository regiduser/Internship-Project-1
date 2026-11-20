using StudentApp.Models;
using StudentApp.Services;

namespace StudentApp.Tests;

public class StudentServiceTests
{
    private readonly InMemoryRepository _repo = new();
    private StudentService NewService() => new(_repo);

    // --------------------------------------------------------------------------------- add

    [Fact]
    public void Add_trims_values_and_persists()
    {
        var service = NewService();
        var student = service.Add(1, "  Ada Lovelace  ", 36, "  maths  ");

        Assert.Equal("Ada Lovelace", student.Name);
        Assert.Equal("maths", student.ProfileText);
        var persisted = Assert.Single(_repo.Persisted);
        Assert.Equal("Ada Lovelace", persisted.Name);
        Assert.Equal(1, NewService().Count);   // a "restart" sees the record
    }

    [Theory]
    [InlineData(0, "Ada", 20)]
    [InlineData(-5, "Ada", 20)]
    [InlineData(1, "", 20)]
    [InlineData(1, "   ", 20)]
    [InlineData(1, "Ada", 0)]
    [InlineData(1, "Ada", 121)]
    public void Add_rejects_invalid_input(int id, string name, int age)
    {
        var service = NewService();
        Assert.Throws<ArgumentException>(() => service.Add(id, name, age));
        Assert.Equal(0, service.Count);
        Assert.Equal(0, _repo.SaveCount);
    }

    [Fact]
    public void Add_rejects_overlong_name_and_profile()
    {
        var service = NewService();
        Assert.Throws<ArgumentException>(() => service.Add(1, new string('x', 101), 20));
        Assert.Throws<ArgumentException>(() => service.Add(1, "Ada", 20, new string('x', 1001)));
        service.Add(1, new string('x', 100), 20, new string('y', 1000));   // exactly at the limits is fine
    }

    [Fact]
    public void Add_rejects_duplicate_id()
    {
        var service = NewService();
        service.Add(1, "Ada", 20);
        Assert.Throws<InvalidOperationException>(() => service.Add(1, "Grace", 30));
        Assert.Equal(1, service.Count);
    }

    [Fact]
    public void Add_rolls_back_when_saving_fails()
    {
        var service = NewService();
        _repo.FailNextSave = true;

        Assert.Throws<IOException>(() => service.Add(1, "Ada", 20));

        Assert.Equal(0, service.Count);
        Assert.Null(service.FindById(1));
        service.Add(1, "Ada", 20);   // the ID is free again
    }

    // ------------------------------------------------------------------------------ update

    [Fact]
    public void Update_changes_fields_and_bumps_timestamp()
    {
        var service = NewService();
        var student = service.Add(1, "Ada", 20, "music");
        var before = student.UpdatedAtUtc;
        Thread.Sleep(5);

        service.Update(1, "Ada L.", 21, "football");

        var updated = service.FindById(1)!;
        Assert.Equal("Ada L.", updated.Name);
        Assert.Equal(21, updated.Age);
        Assert.Equal("football", updated.ProfileText);
        Assert.True(updated.UpdatedAtUtc > before);
        Assert.Equal(student.CreatedAtUtc, updated.CreatedAtUtc);
    }

    [Fact]
    public void Update_with_null_profile_keeps_it_and_empty_profile_clears_it()
    {
        var service = NewService();
        service.Add(1, "Ada", 20, "music");

        service.Update(1, "Ada", 20, null);
        Assert.Equal("music", service.FindById(1)!.ProfileText);

        service.Update(1, "Ada", 20, "");
        Assert.Equal("", service.FindById(1)!.ProfileText);
    }

    [Fact]
    public void Update_rejects_unknown_id_and_invalid_values()
    {
        var service = NewService();
        service.Add(1, "Ada", 20);
        Assert.Throws<InvalidOperationException>(() => service.Update(99, "Ada", 20));
        Assert.Throws<ArgumentException>(() => service.Update(1, "", 20));
        Assert.Throws<ArgumentException>(() => service.Update(1, "Ada", 500));
        Assert.Equal("Ada", service.FindById(1)!.Name);
    }

    [Fact]
    public void Update_rolls_back_when_saving_fails()
    {
        var service = NewService();
        service.Add(1, "Ada", 20, "music");
        _repo.FailNextSave = true;

        Assert.Throws<IOException>(() => service.Update(1, "Changed", 99, "other"));

        var student = service.FindById(1)!;
        Assert.Equal("Ada", student.Name);
        Assert.Equal(20, student.Age);
        Assert.Equal("music", student.ProfileText);
    }

    // ------------------------------------------------------------------------------ delete

    [Fact]
    public void Delete_removes_and_reports_whether_anything_was_deleted()
    {
        var service = NewService();
        service.Add(1, "Ada", 20);

        Assert.True(service.Delete(1));
        Assert.False(service.Delete(1));
        Assert.Empty(_repo.Persisted);
    }

    [Fact]
    public void Delete_rolls_back_when_saving_fails()
    {
        var service = NewService();
        service.Add(1, "Ada", 20);
        service.Add(2, "Grace", 30);
        _repo.FailNextSave = true;

        Assert.Throws<IOException>(() => service.Delete(1));

        Assert.Equal(new[] { 1, 2 }, service.GetAll().Select(s => s.Id));
    }

    // ------------------------------------------------------------------------------- search

    [Fact]
    public void Search_matches_names_case_insensitively()
    {
        var service = NewService();
        service.Add(1, "Ada Lovelace", 20);
        service.Add(2, "Grace Hopper", 30);

        var results = service.Search("  LOVE ");
        Assert.Equal(1, Assert.Single(results).Id);
    }

    [Fact]
    public void Search_by_id_is_exact_not_a_substring()
    {
        var service = NewService();
        service.Add(1, "Ada", 20);
        service.Add(10, "Grace", 20);
        service.Add(11, "Linus", 20);

        // The original matched any ID containing the digits, so "1" returned all three students.
        Assert.Equal(1, Assert.Single(service.Search("1")).Id);
        Assert.Equal(10, Assert.Single(service.Search("10")).Id);
    }

    [Fact]
    public void Blank_search_returns_nothing()
    {
        var service = NewService();
        service.Add(1, "Ada", 20);
        Assert.Empty(service.Search("   "));
    }

    [Fact]
    public void GetAll_supports_sorting()
    {
        var service = NewService();
        service.Add(3, "Charlie", 20);
        service.Add(1, "bob", 40);
        service.Add(2, "Alice", 30);

        Assert.Equal(new[] { 1, 2, 3 }, service.GetAll().Select(s => s.Id));
        Assert.Equal(new[] { 2, 1, 3 }, service.GetAll(StudentSort.Name).Select(s => s.Id));
        Assert.Equal(new[] { 3, 2, 1 }, service.GetAll(StudentSort.Age).Select(s => s.Id));
    }

    // ---------------------------------------------------------------------- semantic search

    private StudentService SeededService()
    {
        var service = NewService();
        service.Add(1, "Aarav", 21, "software development, Python coding and web applications");
        service.Add(2, "Meera", 22, "cybersecurity, ethical hacking and network defense");
        service.Add(3, "Rohan", 20, "football, fitness and team leadership");
        return service;
    }

    [Fact]
    public void SemanticSearch_ranks_the_related_student_first_with_evidence()
    {
        var service = SeededService();
        var results = service.SemanticSearch("programming");

        var top = results[0];
        Assert.Equal(1, top.Student.Id);
        Assert.Contains("programming", top.SharedConcepts);
        Assert.True(top.Score > 0.2);
        Assert.DoesNotContain(results, r => r.Student.Id == 3);   // football is not programming
    }

    [Fact]
    public void SemanticSearch_understands_related_words_without_exact_overlap()
    {
        var service = SeededService();

        var top = service.SemanticSearch("security")[0];
        Assert.Equal(2, top.Student.Id);
        Assert.Contains("security", top.SharedConcepts);

        Assert.Equal(3, service.SemanticSearch("sports")[0].Student.Id);
    }

    [Theory]
    [InlineData("programming")]
    [InlineData("music and guitar")]
    [InlineData("network security")]
    [InlineData("a student who likes sports and leadership")]
    [InlineData("quantum chemistry")]
    public void Every_semantic_result_can_be_explained(string query)
    {
        var service = SeededService();
        service.Add(4, "Isha", 22, "writing, literature and poetry");
        service.Add(5, "Vikram", 25, "entrepreneurship, startups, marketing and finance");

        foreach (var match in service.SemanticSearch(query))
            Assert.True(match.SharedTerms.Count + match.SharedConcepts.Count > 0,
                $"{match.Student.Name} matched '{query}' at {match.Score:P1} with no shared word or concept");
    }

    [Fact]
    public void SemanticSearch_does_not_match_on_age()
    {
        // The original appended "age {n}" to every profile, so a query containing "age" matched everyone.
        Assert.Empty(SeededService().SemanticSearch("age 21"));
    }

    [Fact]
    public void SemanticSearch_blank_query_and_result_limit()
    {
        var service = SeededService();
        Assert.Empty(service.SemanticSearch("   "));
        Assert.Single(service.SemanticSearch("programming security football", maxResults: 1));
    }

    [Fact]
    public void SemanticSearch_notices_updates_and_deletes()
    {
        var service = NewService();
        service.Add(1, "Ada", 20, "music and guitar");
        Assert.Empty(service.SemanticSearch("football"));   // fills the vector cache

        service.Update(1, "Ada", 20, "football and fitness");
        Assert.Equal(1, Assert.Single(service.SemanticSearch("football")).Student.Id);

        service.Delete(1);
        Assert.Empty(service.SemanticSearch("football"));
    }

    // ------------------------------------------------------------------------------- stats

    [Fact]
    public void Statistics_for_empty_service()
    {
        var stats = NewService().GetStatistics();
        Assert.Equal(0, stats.Count);
        Assert.Equal(0.0, stats.AverageAge);
    }

    [Fact]
    public void Statistics_are_computed_correctly()
    {
        var service = NewService();
        service.Add(1, "A", 17, "x");
        service.Add(2, "B", 20);
        service.Add(3, "C", 24, "y");
        service.Add(4, "D", 40);

        var stats = service.GetStatistics();
        Assert.Equal(4, stats.Count);
        Assert.Equal(25.25, stats.AverageAge, 6);
        Assert.Equal(22.0, stats.MedianAge, 6);   // even count: mean of 20 and 24
        Assert.Equal(17, stats.YoungestAge);
        Assert.Equal(40, stats.OldestAge);
        Assert.Equal(2, stats.WithProfile);
        Assert.Equal(4, stats.AgeBands.Sum(b => b.Count));
        Assert.Equal(1, stats.AgeBands.Single(b => b.Label == "Under 18").Count);
    }

    [Fact]
    public void Median_of_odd_count_is_the_middle_value()
    {
        var service = NewService();
        service.Add(1, "A", 20);
        service.Add(2, "B", 30);
        service.Add(3, "C", 25);
        Assert.Equal(25.0, service.GetStatistics().MedianAge, 6);
    }
}
