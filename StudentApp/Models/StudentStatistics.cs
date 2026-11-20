namespace StudentApp.Models;

/// <summary>Summary numbers shown on the dashboard.</summary>
public sealed record StudentStatistics(
    int Count,
    double AverageAge,
    double MedianAge,
    int YoungestAge,
    int OldestAge,
    int WithProfile,
    IReadOnlyList<AgeBand> AgeBands)
{
    public static StudentStatistics Empty { get; } = new(0, 0, 0, 0, 0, 0, []);
}

/// <summary>A labelled bucket in the age histogram.</summary>
public sealed record AgeBand(string Label, int Count);
