# AGENTS.md — conventions for agents working in this repo

The contract source is TypeSpec (`spec/`). `make spec` compiles it and emits the OpenAPI 3.1
document at `spec/tsp-output/openapi.v1.yaml`. Spectral, the mock server, and the drift check
all read from that emitted file. Work from the TypeSpec source, not the emitted YAML.

Every standard below is enforced by a machine check — Roslyn analyzers, banned-API rules,
architecture tests, or the CI pipeline. `dotnet build src/ApiPlatform.slnx` and
`dotnet test src/ApiPlatform.slnx` both green, `make sanitize` exits 0: the implementation
is conformant.

## The golden path — how to add a resource

1. **Define the contract in TypeSpec.**
   Add or extend resource operations in `spec/<resource>.tsp`. Shared model types go in
   `spec/models.tsp`. Run `make spec` to verify it compiles. Run `make lint` to compile and
   then check the emitted contract against the platform ruleset (`.spectral.yaml`). Fix every
   error and warning before proceeding.

2. **Implement the vendor connector in `ApiPlatform.Integration`.**
   Define the canonical seam interface (e.g. `IWorkItemSource`) in
   `src/ApiPlatform.Integration/Acl/`. The interface must extend `IGovernedSource`
   (`ApiPlatform.Platform.Connectors.IGovernedSource`) — that marker wires it into the audit
   proxy at registration time. Write the vendor adapter class in
   `src/ApiPlatform.Integration/Acl/<Vendor>/`. Vendor field names exist only here; canonical
   names are used everywhere above this layer. Package the adapter in a `public`
   `IConnectorModule` implementation and register it there. APL0001 and APL0002 enforce
   the structure.

3. **Expose the endpoint in `ApiPlatform.Api`.**
   Map routes in `src/ApiPlatform.Api/Endpoints/`. Resolve the canonical seam interface from
   DI — never a vendor adapter directly. Every problem response must carry an RFC 9457 `type`
   URI from `ProblemTypes.*`; APL0005 enforces this at build time.

4. **Build.** `dotnet build src/ApiPlatform.slnx -c Release` must exit 0. All six
   compile-time rules (APL0001–APL0005, RS0030) are promoted to `error` in `.editorconfig`;
   any violation fails the build before it leaves the IDE.

5. **Test.** `dotnet test src/ApiPlatform.slnx -c Release`. The suite covers
   WebApplicationFactory integration tests, architecture facts, analyzer unit tests, and
   eventing.

6. **Sanitize.** `make sanitize` must exit 0.

Use `IAccountSource` + `src/ApiPlatform.Integration/Acl/CoreBanking/` +
`src/ApiPlatform.Api/Endpoints/AccountEndpoints.cs` as the reference pattern for any new
resource.

## Standards (all enforced)

### API conventions

- **Resources** are plural nouns, kebab-case: `/accounts`, `/customers`, `/work-items`.
- **Versioning** is in the path: `/v1`, `/v2`. Bump the major only on a breaking change.
  Never break a published version in place.
- **Errors** are RFC 9457 Problem Details (`application/problem+json`). Every problem response
  must carry a canonical `type` URI. APL0005 rejects omissions at build time.
- **Format is separate from version.** Version in the path; format in the `Accept` header.
  Multi-format output is handled by output formatters — one canonical model, many
  representations.
- **Pagination** is cursor-based: `?cursor=&limit=`; responses carry `nextCursor`.
- **Writes** require an `Idempotency-Key` header. The middleware enforces principal-scoped,
  atomic set-if-absent semantics automatically.
- **Scopes** follow `resource.action` — `account.read`, `account.write`, `customer.read`.
  Declare them in the TypeSpec spec; enforce at the endpoint.
- **Canonical, not vendor-shaped.** Consumers never see a source-system field name. The
  Integration ACL (`ApiPlatform.Integration`) is the only place vendor vocabulary exists.
  Platform spelling: `accountId`, `customerId`, etc.

### Compile-time governance (all promoted to build error)

Five custom Roslyn analyzers live in `ApiPlatform.Analyzers`. `RS0030` is wired via
`BannedApiAnalyzers`. All six rules are set to `error` in `.editorconfig`.

- **APL0001** — a governed source (`IGovernedSource` implementor) registered outside an
  `IConnectorModule.Register` block bypasses audit → build error.
- **APL0002** — an `IConnectorModule` that is not `public` is never discovered by the
  connector registry → build error.
- **APL0003** — `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`, or
  `DateTimeOffset.UtcNow` in platform code → build error. Inject `TimeProvider` and call
  `GetUtcNow()`.
- **APL0004** — `Console.Write*` in platform code → build error. Use `ILogger`.
- **APL0005** — a `TypedResults.Problem(...)` call missing the `type:` argument →
  build error. Pass a `ProblemTypes.*` URI.
- **RS0030** — `HttpClient`, `SqlConnection`, or `DbContext` used outside
  `ApiPlatform.Integration` → build error. All data access goes through the governed seam.

### Runtime governance

Every interface that extends `IGovernedSource` is wrapped at registration time in a Castle
DynamicProxy carrying the single `AuditInterceptor`
(`src/ApiPlatform.Integration/Acl/Governance/AuditInterceptor.cs`). The interceptor opens
an OpenTelemetry activity and writes a compliance audit record around every async operation.
PII scalars are masked by `IPiiRedactor`. The governance is keyed on the type relationship
(`typeof(IGovernedSource).IsAssignableFrom(type)`), not on namespace, so a seam is governed
wherever it is declared. A new vendor adapter inherits audit automatically — no per-interface
audit code required.

### Test-time governance

`tests/ApiPlatform.Tests/ArchitectureTests.cs` uses NetArchTest to assert:

- `ApiPlatform.Platform` has no ASP.NET Core dependency.
- `ApiPlatform.Contracts` has no web, cloud, or integration dependencies.
- `ApiPlatform.Integration` has no ASP.NET Core or endpoint dependency.
- Vendor `*Source` implementation classes are `internal`.
- `IConnectorModule` implementations are `public`.
- Every canonical seam interface implements `IGovernedSource`.

## What "done" means

`make spec` green · `make lint` green · `dotnet build src/ApiPlatform.slnx` 0 errors ·
`dotnet test src/ApiPlatform.slnx` all pass · `make sanitize` exit 0.
