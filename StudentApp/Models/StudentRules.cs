namespace StudentApp.Models;

/// <summary>
/// Single source of truth for the validation limits, shared by the service layer,
/// the repository (when loading existing data) and the console UI.
/// </summary>
public static class StudentRules
{
    public const int MinAge = 1;
    public const int MaxAge = 120;
    public const int MaxNameLength = 100;
    public const int MaxProfileLength = 1000;

    /// <summary>Returns an error message, or <c>null</c> when the values are valid.</summary>
    public static string? Check(int id, string? name, int age, string? profileText)
    {
        if (id <= 0) return "ID must be a positive integer.";
        if (string.IsNullOrWhiteSpace(name)) return "Name cannot be blank.";
        if (name.Trim().Length > MaxNameLength) return $"Name must be {MaxNameLength} characters or fewer.";
        if (age is < MinAge or > MaxAge) return $"Age must be between {MinAge} and {MaxAge}.";
        if (profileText is not null && profileText.Trim().Length > MaxProfileLength)
            return $"Profile text must be {MaxProfileLength:N0} characters or fewer.";
        return null;
    }
}
