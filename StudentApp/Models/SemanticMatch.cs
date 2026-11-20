namespace StudentApp.Models;

/// <summary>
/// One ranked semantic-search hit, including the evidence behind the score so the
/// result can be explained rather than being a bare percentage.
/// </summary>
/// <param name="Student">The matching student.</param>
/// <param name="Score">Cosine similarity between query and profile vectors (0..1).</param>
/// <param name="SharedTerms">Words the query and the profile have in common.</param>
/// <param name="SharedConcepts">Related-word groups (e.g. "programming") both texts touch on.</param>
public sealed record SemanticMatch(
    Student Student,
    double Score,
    IReadOnlyList<string> SharedTerms,
    IReadOnlyList<string> SharedConcepts);
