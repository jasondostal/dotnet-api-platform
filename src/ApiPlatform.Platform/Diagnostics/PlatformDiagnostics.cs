using System.Diagnostics;

namespace ApiPlatform.Platform.Diagnostics;

/// <summary>
/// Central OpenTelemetry activity source for platform-emitted traces.
/// </summary>
public static class PlatformDiagnostics
{
    public const string ActivitySourceName = "ApiPlatform.Platform";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
