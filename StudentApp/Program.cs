using System.Text;
using StudentApp.Services;
using StudentApp.UI;

namespace StudentApp;

internal static class Program
{
    private const string Usage = """
        Student Management System

        Usage: dotnet run --project StudentApp -- [options]

          --data <file>   Use a different JSON data file (handy for demos and testing,
                          keeps your real records untouched)
          -h, --help      Show this help
        """;

    private static int Main(string[] args)
    {
        if (args.Contains("-h") || args.Contains("--help"))
        {
            Console.WriteLine(Usage);
            return 0;
        }

        string? dataFile = null;
        var dataIndex = Array.IndexOf(args, "--data");
        if (dataIndex >= 0)
        {
            if (dataIndex + 1 >= args.Length)
            {
                Console.Error.WriteLine("--data needs a file path.\n\n" + Usage);
                return 64;
            }
            dataFile = args[dataIndex + 1];
        }

        var io = ConsoleIO.ForConsole();
        if (io.Interactive)
        {
            // Box-drawing characters and "…" are garbled on some Windows code pages without this.
            try { Console.OutputEncoding = Encoding.UTF8; } catch (IOException) { }
        }

        try
        {
            var repository = new StudentRepository(dataFile);
            var service = new StudentService(repository);
            new StudentMenu(service, io, repository.DataFilePath).Run();
            return 0;
        }
        catch (InvalidDataException ex)
        {
            io.Error($"Data error: {ex.Message}");
            io.Line("The program stopped to protect your existing student records.");
            return 2;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            io.Error($"Could not access the data file: {ex.Message}");
            return 3;
        }
    }
}
