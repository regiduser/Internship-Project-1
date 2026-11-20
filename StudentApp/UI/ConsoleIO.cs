using System.Globalization;

namespace StudentApp.UI;

/// <summary>Raised when the input stream ends (Ctrl+Z / Ctrl+D / closed pipe) so prompts can never spin forever.</summary>
public sealed class InputClosedException : Exception
{
    public InputClosedException() : base("Input ended.") { }
}

/// <summary>
/// All reading and writing goes through this class. It takes plain <see cref="TextReader"/> /
/// <see cref="TextWriter"/> instances so the complete menu can be driven by automated tests.
/// </summary>
public sealed class ConsoleIO(TextReader input, TextWriter output, bool interactive)
{
    /// <summary>Real terminal: colours, screen clearing and "press Enter" pauses are on only when a person is attached.</summary>
    public static ConsoleIO ForConsole() =>
        new(Console.In, Console.Out, interactive: !Console.IsInputRedirected && !Console.IsOutputRedirected);

    public bool Interactive => interactive;

    // ------------------------------------------------------------------------------- output

    public void Write(string text) => output.Write(text);

    public void Line(string text = "") => output.WriteLine(text);

    public void Line(string text, ConsoleColor color)
    {
        if (interactive) Console.ForegroundColor = color;
        output.WriteLine(text);
        if (interactive) Console.ResetColor();
    }

    public void Success(string text) => Line(text, ConsoleColor.Green);
    public void Warn(string text) => Line(text, ConsoleColor.Yellow);
    public void Error(string text) => Line(text, ConsoleColor.Red);

    public void Clear()
    {
        if (interactive) Console.Clear();
    }

    public void Pause()
    {
        if (!interactive) return;
        Write("\nPress Enter to return to the menu...");
        ReadLine();
    }

    // -------------------------------------------------------------------------------- input

    public string ReadLine()
    {
        var line = input.ReadLine() ?? throw new InputClosedException();
        return line;
    }

    /// <summary>Reads a whole number in [min, max]. Empty input returns <paramref name="defaultValue"/> when one is given.</summary>
    public int ReadInt(string prompt, int min, int max, int? defaultValue = null)
    {
        while (true)
        {
            Write(prompt);
            var text = ReadLine().Trim();
            if (text.Length == 0 && defaultValue is { } fallback) return fallback;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                && value >= min && value <= max) return value;
            Warn($"Enter a whole number between {min:N0} and {max:N0}.");
        }
    }

    /// <summary>Reads non-blank text of at most <paramref name="maxLength"/> characters. Empty input returns <paramref name="defaultValue"/> when one is given.</summary>
    public string ReadRequired(string prompt, string label, int maxLength, string? defaultValue = null)
    {
        while (true)
        {
            Write(prompt);
            var text = ReadLine().Trim();
            if (text.Length == 0 && defaultValue is not null) return defaultValue;
            if (text.Length > 0 && text.Length <= maxLength) return text;
            Warn($"{label} is required and must be {maxLength} characters or fewer.");
        }
    }

    /// <summary>Reads optional text; empty input returns <paramref name="defaultValue"/>.</summary>
    public string ReadOptional(string prompt, string maxLabel, int maxLength, string defaultValue = "")
    {
        while (true)
        {
            Write(prompt);
            var text = ReadLine().Trim();
            if (text.Length == 0) return defaultValue;
            if (text.Length <= maxLength) return text;
            Warn($"{maxLabel} must be {maxLength:N0} characters or fewer (you entered {text.Length:N0}).");
        }
    }

    /// <summary>Yes/no question; anything other than y/yes means no.</summary>
    public bool Confirm(string prompt)
    {
        Write(prompt + " [y/N]: ");
        var answer = ReadLine().Trim();
        return answer.Equals("y", StringComparison.OrdinalIgnoreCase)
            || answer.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }
}
