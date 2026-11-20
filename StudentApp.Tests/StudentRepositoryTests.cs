using StudentApp.Models;
using StudentApp.Services;

namespace StudentApp.Tests;

public class StudentRepositoryTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private static Student Sample(int id, string name = "Ada", int age = 30, string profile = "") =>
        new() { Id = id, Name = name, Age = age, ProfileText = profile };

    [Fact]
    public void Load_returns_empty_list_when_file_does_not_exist()
    {
        Assert.Empty(new StudentRepository(_dir.File("none.json")).Load());
    }

    [Fact]
    public void Save_then_load_round_trips_all_fields()
    {
        var repo = new StudentRepository(_dir.File("students.json"));
        var original = Sample(2, "Grace \"Amazing\" Hopper", 45, "compilers, navy, études");
        repo.Save([Sample(5), original]);

        var loaded = repo.Load();
        Assert.Equal(new[] { 2, 5 }, loaded.Select(s => s.Id));   // saved in ID order
        Assert.Equal(original.Name, loaded[0].Name);
        Assert.Equal(original.ProfileText, loaded[0].ProfileText);
        Assert.Equal(original.CreatedAtUtc, loaded[0].CreatedAtUtc);
    }

    [Fact]
    public void Save_creates_missing_directories()
    {
        var path = Path.Combine(_dir.Path, "nested", "deeper", "students.json");
        new StudentRepository(path).Save([Sample(1)]);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Second_save_keeps_the_previous_version_as_backup_and_leaves_no_temp_file()
    {
        var path = _dir.File("students.json");
        var repo = new StudentRepository(path);
        repo.Save([Sample(1, "First")]);
        repo.Save([Sample(1, "Second")]);

        Assert.Equal("Second", repo.Load()[0].Name);
        Assert.Contains("First", File.ReadAllText(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void Corrupt_json_is_reported_with_a_helpful_message_and_never_overwritten()
    {
        var path = _dir.File("students.json");
        File.WriteAllText(path, "{ this is not json");

        var ex = Assert.Throws<InvalidDataException>(() => new StudentRepository(path).Load());

        Assert.Contains("students.json", ex.Message);
        Assert.Equal("{ this is not json", File.ReadAllText(path));
    }

    [Fact]
    public void Duplicate_ids_in_the_file_are_rejected()
    {
        var path = _dir.File("students.json");
        File.WriteAllText(path, """[{"Id":1,"Name":"A","Age":20},{"Id":1,"Name":"B","Age":21}]""");
        var ex = Assert.Throws<InvalidDataException>(() => new StudentRepository(path).Load());
        Assert.Contains("duplicate", ex.Message);
    }

    [Theory]
    [InlineData("""[{"Id":0,"Name":"A","Age":20}]""")]
    [InlineData("""[{"Id":1,"Name":"","Age":20}]""")]
    [InlineData("""[{"Id":1,"Name":"A","Age":999}]""")]
    [InlineData("[null]")]
    public void Invalid_records_in_the_file_are_rejected(string json)
    {
        var path = _dir.File("students.json");
        File.WriteAllText(path, json);
        Assert.Throws<InvalidDataException>(() => new StudentRepository(path).Load());
    }

    [Fact]
    public void Older_files_without_profile_text_or_with_null_profile_still_load()
    {
        var path = _dir.File("students.json");
        File.WriteAllText(path, """[{"Id":1,"Name":"Old","Age":20},{"Id":2,"Name":"Null","Age":21,"ProfileText":null}]""");

        var loaded = new StudentRepository(path).Load();

        Assert.Equal(2, loaded.Count);
        Assert.All(loaded, s => Assert.Equal(string.Empty, s.ProfileText));
    }
}
