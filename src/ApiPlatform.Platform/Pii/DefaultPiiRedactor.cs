namespace ApiPlatform.Platform.Pii;

/// <summary>
/// Deterministic partial-masking implementation of <see cref="IPiiRedactor"/>.
/// </summary>
public sealed class DefaultPiiRedactor : IPiiRedactor
{
    /// <summary>
    /// Masks an email address, preserving only the first character of the local part
    /// and the first character + TLD of the domain. Example: jane@example.com → j***@e***.com
    /// </summary>
    public string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "***";

        var atIdx = email.IndexOf('@');
        if (atIdx <= 0) return "***";

        var local = email[..atIdx];
        var domain = email[(atIdx + 1)..];

        var maskedLocal = local[0] + "***";

        var dotIdx = domain.LastIndexOf('.');
        var maskedDomain = dotIdx > 0
            ? domain[0] + "***" + domain[dotIdx..]
            : domain[0] + "***";

        return $"{maskedLocal}@{maskedDomain}";
    }

    /// <summary>
    /// Masks a phone number, keeping only the last 4 digits. Example: 555-123-4567 → ***-***-4567
    /// </summary>
    public string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return "***";

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        if (digits.Length < 4) return "***";

        var last4 = digits[^4..];
        return $"***-***-{last4}";
    }

    /// <inheritdoc />
    public string Mask(string? value) => "***";
}
