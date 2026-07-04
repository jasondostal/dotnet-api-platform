namespace ApiPlatform.Platform.Auth;

/// <summary>
/// Host-agnostic scope evaluation logic. No ASP.NET or identity-model dependencies.
/// </summary>
public static class ScopeCheck
{
    /// <summary>
    /// Returns true when <paramref name="granted"/> contains <paramref name="required"/>
    /// using a case-sensitive exact match.
    /// </summary>
    public static bool HasScope(IEnumerable<string> granted, string required)
        => granted.Contains(required, StringComparer.Ordinal);
}
