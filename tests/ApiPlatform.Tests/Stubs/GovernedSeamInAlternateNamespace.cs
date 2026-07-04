// Stubs used only by SourceGovernanceTests.SeamInDifferentNamespace_StillWrappedByProxy_WhenItImplementsIGovernedSource.
// They live in a namespace that is deliberately NOT ApiPlatform.Integration.Acl to prove that the
// old namespace-string predicate has been replaced by a type-relationship (IGovernedSource) predicate.

namespace SomeOther.Place;

using ApiPlatform.Contracts;
using ApiPlatform.Platform.Connectors;

/// <summary>A governed seam declared outside the canonical Acl namespace.</summary>
public interface IWidgetSource : IGovernedSource
{
    Task<IReadOnlyList<Account>> GetWidgetsAsync(CancellationToken ct = default);
}

internal sealed class WidgetSourceStub : IWidgetSource
{
    public Task<IReadOnlyList<Account>> GetWidgetsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Account>>(Array.Empty<Account>());
}
