namespace ApiPlatform.Platform.Connectors;

/// <summary>
/// Marker interface that designates a canonical governed seam.
/// Any interface that extends <see cref="IGovernedSource"/> is automatically audited, traced, and
/// PII-masked by construction — the governance proxy wraps every implementation the moment it is
/// registered, with no per-interface or per-vendor audit code required.
///
/// Apply this marker to the five canonical seam interfaces
/// (<c>IAccountSource</c>, <c>ICustomerSource</c>, <c>IWorkItemSource</c>, <c>IInsightSource</c>,
/// <c>IAccountWriter</c>). Inner vendor-facing contracts such as <c>IAccountVendor</c> must NOT
/// extend this marker — they sit behind the governed facade and must remain un-proxied to avoid
/// double-audit.
/// </summary>
public interface IGovernedSource { }
