using StudentApp.Services;
using StudentApp.UI;

namespace StudentApp.Tests;

/// <summary>End-to-end tests: a scripted "user" types into the real menu and we inspect what it prints.</summary>
public class StudentMenuTests : IDisposable
{
    private readonly InMemoryRepository _repo = new();
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    /// <summary>Feeds the lines to the menu (non-interactive: no colours, no pauses) and returns everything it printed.</summary>
    private string Run(params string[] lines)
    {
        var output = new StringWriter();
        var io = new ConsoleIO(new StringReader(string.Join("\n", lines) + "\n"), output, interactive: false);
        new StudentMenu(new StudentService(_repo), io, "(test data)").Run();
        return output.ToString();
    }

    [Fact]
    public void Exit_says_goodbye()
    {
        Assert.Contains("Goodbye", Run("0"));
    }

    [Fact]
    public void Closed_input_ends_the_program_instead_of_hanging()
    {
        // Regression: the original looped forever in ReadInt when stdin reached end-of-file.
        string? output = null;
        var task = Task.Run(() =>
        {
            var writer = new StringWriter();
            var io = new ConsoleIO(new StringReader(""), writer, interactive: false);
            new StudentMenu(new StudentService(_repo), io, "x").Run();
            output = writer.ToString();
        });

        Assert.True(task.Wait(TimeSpan.FromSeconds(10)), "menu did not stop when input ended");
        Assert.Contains("Input closed", output);
    }

    [Fact]
    public void Closed_input_in_the_middle_of_a_prompt_also_ends_cleanly()
    {
        var task = Task.Run(() => Run("4"));   // "Delete student" then input ends at "ID to delete:"
        Assert.True(task.Wait(TimeSpan.FromSeconds(10)));
        Assert.Contains("Input closed", task.Result);
    }

    [Fact]
    public void Adding_a_student_saves_it_and_it_shows_up_in_the_list()
    {
        var output = Run("1", "7", "Ada Lovelace", "36", "mathematics and algorithms", "2", "", "0");

        Assert.Contains("added successfully", output);
        Assert.Contains("Ada Lovelace", output);
        Assert.Equal(7, Assert.Single(_repo.Persisted).Id);
    }

    [Fact]
    public void Bad_numbers_are_re_prompted_until_valid()
    {
        var output = Run("1", "abc", "0", "5", "Bob", "old", "200", "25", "", "0");

        Assert.Contains("Enter a whole number between 1 and", output);
        Assert.Contains("Enter a whole number between 1 and 120", output);
        Assert.Equal(25, Assert.Single(_repo.Persisted).Age);
    }

    [Fact]
    public void Duplicate_id_is_reported_before_asking_for_the_rest()
    {
        var output = Run("1", "1", "Ada", "20", "", "1", "1", "0");

        Assert.Contains("already used by Ada", output);
        Assert.Equal("Ada", Assert.Single(_repo.Persisted).Name);
    }

    [Fact]
    public void Update_keeps_values_when_enter_is_pressed_and_dash_clears_the_profile()
    {
        Run("1", "1", "Ada", "20", "music");

        Run("3", "1", "", "", "", "0");   // press Enter at every prompt
        var unchanged = Assert.Single(_repo.Persisted);
        Assert.Equal(("Ada", 20, "music"), (unchanged.Name, unchanged.Age, unchanged.ProfileText));

        Run("3", "1", "Ada L.", "21", "-", "0");
        var changed = Assert.Single(_repo.Persisted);
        Assert.Equal(("Ada L.", 21, ""), (changed.Name, changed.Age, changed.ProfileText));
    }

    [Fact]
    public void Delete_only_happens_when_the_word_DELETE_is_typed()
    {
        Run("1", "1", "Ada", "20", "");

        Assert.Contains("cancelled", Run("4", "1", "delete", "0"));
        Assert.Single(_repo.Persisted);

        Assert.Contains("Student deleted", Run("4", "1", "DELETE", "0"));
        Assert.Empty(_repo.Persisted);
    }

    [Fact]
    public void Unknown_menu_choice_shows_a_warning_and_keeps_running()
    {
        var output = Run("banana", "0");
        Assert.Contains("valid menu option", output);
        Assert.Contains("Goodbye", output);
    }

    [Fact]
    public void Storage_failures_are_reported_without_crashing()
    {
        _repo.FailNextSave = true;
        var output = Run("1", "1", "Ada", "20", "", "0");

        Assert.Contains("File error: disk full", output);
        Assert.Contains("Goodbye", output);
        Assert.Empty(_repo.Persisted);
    }

    [Fact]
    public void Demo_data_makes_semantic_search_work_out_of_the_box()
    {
        var output = Run("9", "y", "8", "programming", "0");

        Assert.Contains("Added 9 sample students", output);
        Assert.Contains("Aarav Sharma", output);
        Assert.Contains("related topic: programming", output);
        Assert.Equal(9, _repo.Persisted.Count);
    }

    [Fact]
    public void Loading_demo_data_twice_does_not_duplicate_it()
    {
        Run("9", "y", "0");
        Assert.Contains("already loaded", Run("9", "0"));
        Assert.Equal(9, _repo.Persisted.Count);
    }

    [Fact]
    public void Semantic_search_with_no_students_gives_guidance()
    {
        Assert.Contains("load the demo data", Run("8", "0"));
    }

    [Fact]
    public void Dashboard_shows_statistics_and_age_distribution()
    {
        var output = Run("9", "y", "6", "0");
        Assert.Contains("Total students : 9", output);
        Assert.Contains("Median age", output);
        Assert.Contains("Age distribution", output);
    }

    [Fact]
    public void Export_writes_the_csv_file_and_asks_before_overwriting()
    {
        Run("9", "y", "0");
        var path = _dir.File("export.csv");

        Assert.Contains("Exported 9 student(s)", Run("7", path, "0"));
        Assert.True(File.Exists(path));

        File.WriteAllText(path, "keep me");
        Assert.Contains("cancelled", Run("7", path, "n", "0"));
        Assert.Equal("keep me", File.ReadAllText(path));
    }

    [Fact]
    public void Banner_box_lines_are_all_the_same_width()
    {
        // Regression: the original title line was two characters narrower than the borders.
        var lines = Run("0").Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .Where(l => l.StartsWith('╔') || l.StartsWith('║') || l.StartsWith('╚'))
            .ToList();

        Assert.Equal(3, lines.Count);
        Assert.Single(lines.Select(l => l.Length).Distinct());
    }
}
