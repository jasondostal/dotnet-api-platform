namespace ApiPlatform.Platform.Pii;

/// <summary>
/// Masks personally-identifiable values before they reach logs or audit trails.
/// </summary>
public interface IPiiRedactor
{
    string MaskEmail(string? email);
    string MaskPhone(string? phone);
    string Mask(string? value);
}
