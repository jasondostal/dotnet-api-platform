using ApiPlatform.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace ApiPlatform.Analyzers.Tests;

/// <summary>
/// Tests for the platform governance analyzers. Each test compiles a self-contained snippet
/// (with inline stubs for the platform's DI + seam types) and asserts the analyzer's verdict.
/// The guardrails themselves are verified — a standard that fails the build is only trustworthy
/// if the rule that fails it is tested. Snippet types are fully qualified so the inline stub
/// namespaces can be declared in the same compilation unit (no top-level usings).
/// </summary>
public class GovernanceAnalyzerTests
{
    private const string Prelude = """
        namespace Microsoft.Extensions.DependencyInjection
        {
            public interface IServiceCollection { }
            public static class ServiceCollectionServiceExtensions
            {
                public static IServiceCollection AddSingleton<TService, TImpl>(this IServiceCollection s) where TImpl : class, TService => s;
                public static IServiceCollection AddSingleton<TService>(this IServiceCollection s, System.Func<System.IServiceProvider, TService> f) where TService : class => s;
            }
        }
        namespace ApiPlatform.Platform.Connectors
        {
            public interface IGovernedSource { }
            public interface IConnectorModule { void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection s); }
        }
        namespace ApiPlatform.Integration.Acl { public interface IAccountSource : ApiPlatform.Platform.Connectors.IGovernedSource { } }
        namespace Microsoft.AspNetCore.Http
        {
            public interface IResult { }
            public class ProblemDetails { public string? Type { get; set; } }
            public static class Results
            {
                public static IResult Problem(
                    string? detail = null,
                    string? instance = null,
                    int? statusCode = null,
                    string? title = null,
                    string? type = null) => null!;
                public static IResult Problem(ProblemDetails problemDetails) => null!;
            }
            public static class TypedResults
            {
                public static IResult Problem(
                    string? detail = null,
                    string? instance = null,
                    int? statusCode = null,
                    string? title = null,
                    string? type = null) => null!;
            }
        }
        """;

    private static CSharpAnalyzerTest<SourceRegistrationAnalyzer, DefaultVerifier> RegTest(string body) =>
        new() { TestCode = Prelude + body, ReferenceAssemblies = ReferenceAssemblies.Net.Net80 };

    private static CSharpAnalyzerTest<ConnectorModuleVisibilityAnalyzer, DefaultVerifier> ModTest(string body) =>
        new() { TestCode = Prelude + body, ReferenceAssemblies = ReferenceAssemblies.Net.Net80 };

    [Fact]
    public async Task APL0001_Fires_When_Source_Registered_Outside_A_Module()
    {
        await RegTest("""
            public sealed class Impl : ApiPlatform.Integration.Acl.IAccountSource { }
            public sealed class HostWiring
            {
                public void Wire(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                    => {|APL0001:Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ApiPlatform.Integration.Acl.IAccountSource, Impl>(services)|};
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0001_Silent_When_Source_Registered_Inside_A_Connector_Module()
    {
        await RegTest("""
            public sealed class Impl : ApiPlatform.Integration.Acl.IAccountSource { }
            public sealed class CoreBankingConnectorModule : ApiPlatform.Platform.Connectors.IConnectorModule
            {
                public void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                    => Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<ApiPlatform.Integration.Acl.IAccountSource, Impl>(services);
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0002_Fires_When_Connector_Module_Is_Not_Public()
    {
        await ModTest("""
            internal sealed class {|APL0002:HiddenModule|} : ApiPlatform.Platform.Connectors.IConnectorModule
            {
                public void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0002_Silent_When_Connector_Module_Is_Public()
    {
        await ModTest("""
            public sealed class VisibleModule : ApiPlatform.Platform.Connectors.IConnectorModule
            {
                public void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
            }
            """).RunAsync();
    }

    private static CSharpAnalyzerTest<DateTimeUsageAnalyzer, DefaultVerifier> DtTest(string body) =>
        new() { TestCode = Prelude + body, ReferenceAssemblies = ReferenceAssemblies.Net.Net80 };

    [Fact]
    public async Task APL0003_Fires_On_DateTime_UtcNow()
    {
        await DtTest("""
            public sealed class Example
            {
                public System.DateTime GetTimestamp() => {|APL0003:System.DateTime.UtcNow|};
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0003_Silent_On_TimeProvider_GetUtcNow()
    {
        await DtTest("""
            public sealed class Example
            {
                private readonly System.TimeProvider _tp;
                public Example(System.TimeProvider tp) => _tp = tp;
                public System.DateTimeOffset GetTimestamp() => _tp.GetUtcNow();
            }
            """).RunAsync();
    }

    private static CSharpAnalyzerTest<ConsoleUsageAnalyzer, DefaultVerifier> ConTest(string body) =>
        new() { TestCode = Prelude + body, ReferenceAssemblies = ReferenceAssemblies.Net.Net80 };

    [Fact]
    public async Task APL0004_Fires_On_Console_WriteLine()
    {
        await ConTest("""
            public sealed class Example
            {
                public void Log(string msg) => {|APL0004:System.Console.WriteLine(msg)|};
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0004_Fires_On_Console_Error_Property()
    {
        await ConTest("""
            public sealed class Example
            {
                public void Log(string msg) => {|APL0004:System.Console.Error|}.WriteLine(msg);
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0004_Silent_On_ILogger_Log()
    {
        await ConTest("""
            namespace Microsoft.Extensions.Logging
            {
                public interface ILogger<out TCategoryName>
                {
                    void LogInformation(string message);
                }
            }
            public sealed class Example
            {
                private readonly Microsoft.Extensions.Logging.ILogger<Example> _logger;
                public Example(Microsoft.Extensions.Logging.ILogger<Example> logger) => _logger = logger;
                public void Log(string msg) => _logger.LogInformation(msg);
            }
            """).RunAsync();
    }

    private static CSharpAnalyzerTest<ProblemTypeAnalyzer, DefaultVerifier> PtTest(string body) =>
        new() { TestCode = Prelude + body, ReferenceAssemblies = ReferenceAssemblies.Net.Net80 };

    [Fact]
    public async Task APL0005_Fires_When_Type_Omitted()
    {
        await PtTest("""
            public sealed class Example
            {
                public Microsoft.AspNetCore.Http.IResult Get()
                    => {|APL0005:Microsoft.AspNetCore.Http.Results.Problem("something went wrong")|};
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0005_Silent_When_Type_Provided()
    {
        await PtTest("""
            public sealed class Example
            {
                public Microsoft.AspNetCore.Http.IResult Get()
                    => Microsoft.AspNetCore.Http.Results.Problem(
                        detail: "something went wrong",
                        type: "https://apiplatform.dev/problems/not-found");
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0005_Silent_For_ProblemDetails_Overload()
    {
        await PtTest("""
            public sealed class Example
            {
                public Microsoft.AspNetCore.Http.IResult Get()
                {
                    var pd = new Microsoft.AspNetCore.Http.ProblemDetails
                    {
                        Type = "https://apiplatform.dev/problems/not-found"
                    };
                    return Microsoft.AspNetCore.Http.Results.Problem(pd);
                }
            }
            """).RunAsync();
    }

    [Fact]
    public async Task APL0005_Fires_When_Type_Omitted_On_TypedResults()
    {
        await PtTest("""
            public sealed class Example
            {
                public Microsoft.AspNetCore.Http.IResult Get()
                    => {|APL0005:Microsoft.AspNetCore.Http.TypedResults.Problem(detail: "error", statusCode: 400)|};
            }
            """).RunAsync();
    }
}
