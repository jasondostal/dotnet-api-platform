using System.Globalization;
using CsvHelper;
using Microsoft.Net.Http.Headers;

namespace ApiPlatform.Api.Formatters;

/// <summary>
/// Writes list responses as CSV when the client sends Accept: text/csv.
/// Works for any IEnumerable — CsvHelper reflects the public properties.
/// </summary>
public static class CsvOutputFormatter
{
    public const string CsvMediaType = "text/csv";
    public const string InternalJsonProfile = "application/vnd.northwind.account.internal.v1+json";

    /// <summary>
    /// Writes <paramref name="items"/> as CSV to the response and sets content-type to text/csv.
    /// </summary>
    public static async Task WriteCsvAsync<T>(HttpContext ctx, IEnumerable<T> items)
    {
        ctx.Response.ContentType = CsvMediaType;
        await using var writer = new StreamWriter(ctx.Response.Body, leaveOpen: true);
        await using var csv    = new CsvWriter(writer, CultureInfo.InvariantCulture);
        await csv.WriteRecordsAsync(items);
    }

    /// <summary>
    /// Returns true when the request's first accepted media type is text/csv.
    /// </summary>
    public static bool RequestedCsv(HttpRequest request)
    {
        var accept = request.Headers.Accept.ToString();
        if (string.IsNullOrEmpty(accept)) return false;
        var types = MediaTypeHeaderValue.ParseList(
            request.Headers.Accept.ToArray()!);
        return types.Any(t => t.MediaType == CsvMediaType);
    }
}
