using StudentApp.Models;
using StudentApp.Services;
using StudentApp.Utils;

namespace StudentApp.UI;

/// <summary>The interactive console menu. Contains presentation logic only; all rules live in <see cref="StudentService"/>.</summary>
public sealed class StudentMenu(StudentService service, ConsoleIO io, string dataFilePath)
{
    private const int BoxWidth = 54;

    /// <summary>Runs until the user chooses Exit or the input stream ends.</summary>
    public void Run()
    {
        try
        {
            while (true)
            {
                ShowHeader();
                io.Line("  1  Add student");
                io.Line("  2  View all students");
                io.Line("  3  Update student");
                io.Line("  4  Delete student");
                io.Line("  5  Search students by name or ID");
                io.Line("  6  Dashboard / statistics");
                io.Line("  7  Export to CSV");
                io.Line("  8  Semantic search (embedding vectors)");
                io.Line("  9  Load sample students (demo data)");
                io.Line("  0  Exit");
                io.Write("\nSelect an option: ");

                switch (io.ReadLine().Trim())
                {
                    case "1": Guard(AddStudent); break;
                    case "2": Guard(ViewStudents); break;
                    case "3": Guard(UpdateStudent); break;
                    case "4": Guard(DeleteStudent); break;
                    case "5": Guard(SearchStudents); break;
                    case "6": Guard(ShowDashboard); break;
                    case "7": Guard(ExportCsv); break;
                    case "8": Guard(SemanticSearch); break;
                    case "9": Guard(LoadSampleData); break;
                    case "0":
                        io.Success("Goodbye. Your records are saved.\n");
                        return;
                    default:
                        io.Warn("Please select a valid menu option.");
                        io.Pause();
                        break;
                }
            }
        }
        catch (InputClosedException)
        {
            io.Line();
            io.Line("Input closed - exiting. Your records are saved.");
        }
    }

    /// <summary>Runs one menu action; expected failures become a friendly message instead of a crash.</summary>
    private void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            io.Error(ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            io.Error($"File error: {ex.Message}");
            io.Line($"Data file: {dataFilePath}");
        }
        io.Pause();
    }

    // ------------------------------------------------------------------------------ actions

    private void AddStudent()
    {
        var id = io.ReadInt("Student ID: ", 1, int.MaxValue);
        if (service.FindById(id) is { } existing)
        {
            io.Warn($"ID {id} is already used by {existing.Name}. Choose another ID.");
            return;
        }
        var name = io.ReadRequired("Full name: ", "Name", StudentRules.MaxNameLength);
        var age = io.ReadInt("Age: ", StudentRules.MinAge, StudentRules.MaxAge);
        var profile = io.ReadOptional("Interests / skills / goals (optional): ", "Profile text", StudentRules.MaxProfileLength);

        var student = service.Add(id, name, age, profile);
        io.Success($"Student '{student.Name}' added successfully.");
    }

    private void ViewStudents()
    {
        if (service.Count == 0)
        {
            io.Warn("No student records yet. Add one, or load the demo data with option 9.");
            return;
        }
        io.Write("Sort by [I]D, [N]ame, [A]ge or [R]ecently updated (Enter = ID): ");
        var sort = io.ReadLine().Trim().ToLowerInvariant() switch
        {
            "n" or "name" => StudentSort.Name,
            "a" or "age" => StudentSort.Age,
            "r" or "recent" => StudentSort.RecentlyUpdated,
            _ => StudentSort.Id
        };
        PrintTable(service.GetAll(sort));
    }

    private void UpdateStudent()
    {
        var id = io.ReadInt("ID to update: ", 1, int.MaxValue);
        if (service.FindById(id) is not { } student)
        {
            io.Warn("Student not found.");
            return;
        }

        io.Line("Press Enter to keep the current value.");
        var name = io.ReadRequired($"Name [{student.Name}]: ", "Name", StudentRules.MaxNameLength, student.Name);
        var age = io.ReadInt($"Age [{student.Age}]: ", StudentRules.MinAge, StudentRules.MaxAge, student.Age);
        var shown = student.ProfileText.Length == 0 ? "none" : Truncate(student.ProfileText, 40);
        io.Write($"Profile [{shown}] (Enter = keep, '-' = clear): ");
        var profileInput = io.ReadLine().Trim();
        string? profile = profileInput switch
        {
            "" => null,
            "-" => string.Empty,
            _ => profileInput
        };

        service.Update(id, name, age, profile);
        io.Success("Student updated successfully.");
    }

    private void DeleteStudent()
    {
        var id = io.ReadInt("ID to delete: ", 1, int.MaxValue);
        if (service.FindById(id) is not { } student)
        {
            io.Warn("Student not found.");
            return;
        }

        io.Write($"Delete {student.Name} (ID {student.Id})? Type DELETE to confirm: ");
        if (string.Equals(io.ReadLine().Trim(), "DELETE", StringComparison.Ordinal))
        {
            service.Delete(id);
            io.Success("Student deleted.");
        }
        else io.Warn("Deletion cancelled.");
    }

    private void SearchStudents()
    {
        var term = io.ReadRequired("Search by student name or ID: ", "Search term", 100);
        var results = service.Search(term);
        if (results.Count == 0) io.Warn("No matching students found.");
        else PrintTable(results);
    }

    private void SemanticSearch()
    {
        if (service.Count == 0)
        {
            io.Warn("No students to search yet. Add some, or load the demo data with option 9.");
            return;
        }

        var query = io.ReadRequired("Describe the student or interests you want to find: ", "Search text", 200);
        var results = service.SemanticSearch(query);
        if (results.Count == 0)
        {
            io.Warn("No relevant student profiles found. Try different words, or add profile text to students.");
        }
        else
        {
            io.Line($"\n {results.Count} ranked match(es) for \"{query}\"\n");
            for (var i = 0; i < results.Count; i++)
            {
                var m = results[i];
                io.Line($" {i + 1,2}. {m.Student.Name} (ID {m.Student.Id}, age {m.Student.Age})   similarity {m.Score * 100:F1}%");
                var evidence = new List<string>();
                if (m.SharedConcepts.Count > 0) evidence.Add("related topic: " + string.Join(", ", m.SharedConcepts));
                if (m.SharedTerms.Count > 0) evidence.Add("shared words: " + string.Join(", ", m.SharedTerms));
                io.Line($"     why: {string.Join("; ", evidence)}");
                if (m.Student.ProfileText.Length > 0) io.Line($"     profile: {Truncate(m.Student.ProfileText, 80)}");
            }
        }
        io.Line("\nSimilarity is a local hashed-vector score (cosine), not a probability and not an AI-model response.");
    }

    private void ShowDashboard()
    {
        var stats = service.GetStatistics();
        io.Line("\n STUDENT DASHBOARD\n" + new string('-', 40));
        if (stats.Count == 0)
        {
            io.Warn("No data yet.");
            return;
        }

        io.Line($"Total students : {stats.Count}");
        io.Line($"With profile   : {stats.WithProfile} ({stats.WithProfile * 100.0 / stats.Count:F0}%)");
        io.Line($"Average age    : {stats.AverageAge:F1}");
        io.Line($"Median age     : {stats.MedianAge:F1}");
        io.Line($"Youngest / oldest : {stats.YoungestAge} / {stats.OldestAge}");

        io.Line("\nAge distribution");
        var max = Math.Max(1, stats.AgeBands.Max(b => b.Count));
        foreach (var band in stats.AgeBands)
            io.Line($"  {band.Label,-9} {new string('#', (int)Math.Round(band.Count * 24.0 / max)),-24} {band.Count}");
    }

    private void ExportCsv()
    {
        var defaultName = $"students-{DateTime.Now:yyyyMMdd-HHmm}.csv";
        var path = io.ReadOptional($"Output file [{defaultName}]: ", "File path", 260, defaultName);
        if (File.Exists(path) && !io.Confirm($"'{path}' already exists. Overwrite?"))
        {
            io.Warn("Export cancelled.");
            return;
        }
        var students = service.GetAll();
        CsvExporter.Export(students, path);
        io.Success($"Exported {students.Count} student(s) to {Path.GetFullPath(path)}");
    }

    private void LoadSampleData()
    {
        var missing = SampleData.Students.Where(s => service.FindById(s.Id) is null).ToList();
        if (missing.Count == 0)
        {
            io.Warn("The sample students are already loaded.");
            return;
        }
        io.Line($"This adds {missing.Count} fictional students (IDs 1001-1009) to {dataFilePath}.");
        if (!io.Confirm("Add them?"))
        {
            io.Warn("Nothing added.");
            return;
        }
        foreach (var s in missing) service.Add(s.Id, s.Name, s.Age, s.Profile);
        io.Success($"Added {missing.Count} sample students. Try option 8 and search 'programming' or 'security'.");
    }

    // ------------------------------------------------------------------------------ display

    private void PrintTable(IReadOnlyList<Student> students)
    {
        io.Line($"\n {students.Count} record(s)\n");
        io.Line($" {"ID",-7} {"NAME",-26} {"AGE",-4} {"UPDATED (UTC)",-16} PROFILE");
        io.Line(new string('-', 100));
        foreach (var s in students)
            io.Line($" {s.Id,-7} {Truncate(s.Name, 24),-26} {s.Age,-4} {s.UpdatedAtUtc:yyyy-MM-dd HH:mm}    {Truncate(s.ProfileText, 40)}");
    }

    private void ShowHeader()
    {
        io.Clear();
        var inner = BoxWidth - 2;
        const string title = "STUDENT MANAGEMENT SYSTEM";
        var left = (inner - title.Length) / 2;
        io.Line("╔" + new string('═', inner) + "╗", ConsoleColor.Cyan);
        io.Line("║" + new string(' ', left) + title + new string(' ', inner - left - title.Length) + "║", ConsoleColor.Cyan);
        io.Line("╚" + new string('═', inner) + "╝", ConsoleColor.Cyan);
        io.Line($"  Records: {service.Count}     |     {DateTime.Now:ddd, dd MMM yyyy HH:mm}\n");
    }

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..(max - 1)] + "…";
}
