# dotnet-api-platform — Architecture Guide

> A public reference implementation. All institution and vendor specifics are fictional:
> the institution is **Northwind Credit Union** and source systems are generic ("Core Banking",
> "Loan Origination", "Data Warehouse"). Adopt it by mapping these placeholders to your own
> systems.

A consistent, secured, auditable REST API platform for a financial institution, built on
.NET 10 and Azure. It is the single front door for every integration — internal or external —
and the synchronous face of the same canonical domain an event-driven backbone carries
asynchronously.

---

## 1. North star

- **Design-first.** The TypeSpec specification (`spec/`) is the source of truth. `make spec`
  compiles it to OpenAPI 3.1 at `spec/tsp-output/openapi.v1.yaml`. `make drift` verifies that
  the running API's emitted OpenAPI matches the spec. Spec, linter, mock, and drift check all
  point at the same document.
- **Machine-verifiable conformance.** Every house rule is a deterministic pass/fail signal —
  a Roslyn analyzer, a banned-API rule, a NetArchTest fact, or a CI exit code. An agent
  self-corrects to green without a human in the loop.
- **Canonical, not vendor-shaped.** Vendor specs are wrapped in platform contracts, platform
  error model, and platform vocabulary behind a stable `/v1`. A vendor can change behind the
  scenes; consumers never feel it.
- **Open-banking-grade domain.** FDX / open-banking is the reference model for accounts and
  transactions. Internal consumers first; partner orgs and member-permissioned data later, on
  the same engine.

---

## 2. Solution layout

The solution (`src/ApiPlatform.slnx`) contains 12 source projects and 5 test projects:

```
src/
  ApiPlatform.Contracts          # pure canonical types (Account, Customer, WorkItem, …)
  ApiPlatform.Platform           # governance core: audit, PII redaction, results, seam interfaces
  ApiPlatform.Platform.AspNetCore # web-layer wiring: auth, idempotency, error handler
  ApiPlatform.Platform.Telemetry # OTel setup for off-web hosts
  ApiPlatform.Analyzers          # five custom Roslyn analyzers (APL0001–APL0005)
  ApiPlatform.Integration        # ACL: vendor adapters + routing aggregator
  ApiPlatform.Api                # HTTP host: endpoints, eventing, formatters
  ApiPlatform.AppHost            # Aspire dev orchestration host
  ApiPlatform.ServiceDefaults    # shared OTel + resilience defaults
  ApiPlatform.Mcp                # MCP server projecting the API catalog as agent tools
  ApiPlatform.EventSource        # background worker: work-item change feed → event sink
  ApiPlatform.Poller             # background worker: creation-feed poll → audit events

tests/
  ApiPlatform.Tests              # integration (WebApplicationFactory) + architecture tests
  ApiPlatform.Analyzers.Tests    # analyzer unit tests
  ApiPlatform.EventSource.Tests
  ApiPlatform.Poller.Tests
  ApiPlatform.Mcp.Tests
```

**Dependency direction** (enforced by NetArchTest):
`Contracts` ← `Platform` ← `Platform.AspNetCore` ← `Api`
`Contracts` ← `Integration` (which also references `Platform`)
Neither `Contracts` nor `Platform` references ASP.NET Core. `Integration` does not reference
the web or host layer.

---

## 3. Layered architecture

```
   Consumers:  colleague apps · partner orgs · MCP server / agents · (later) FDX
        │        workforce JWT │ client-credentials (M2M) │ (later) consent grants
   ─────┼──────────────────────────────────────────────────────────────────────────────
   [ APIM gateway (optional) ]  authN/Z, scope enforcement, rate-limit, route, observe
   ─────┼──────────────────────────────────────────────────────────────────────────────
   [ ApiPlatform.Api ]  /v1/accounts, /v1/customers …  platform contracts, error model,
        │               versioning, multi-format output. Runtime OpenAPI at /openapi/v1.json
   ─────┼──────────────────────────────────────────────────────────────────────────────
   [ ApiPlatform.Integration — ACL ]            ← the load-bearing wall
        │   maps vendor shapes → one canonical vocabulary (Account, Customer, …)
        │   DynamicProxy audit interceptor wraps every governed seam here
   ─────┴──────────────────────────────────────────────────────────────────────────────
   Source systems:  Core Banking · Loan Origination · file/blob landing · Data Warehouse
```

**Background hosts** (`ApiPlatform.EventSource`, `ApiPlatform.Poller`) run off-path. They
pull from work-item change feeds and creation feeds, emit domain events, and write audit
records. They share the same canonical types, `IPlatformAudit` seam, and `IPiiRedactor`
as the API host — no separate audit pipeline for background work.

The synchronous REST API and the event backbone share the same canonical `Account` /
`Customer` objects and the same ACL. Building `/v1/accounts` correctly produces the canonical
model and the core-banking adapter that any async consumer also uses.

---

## 4. Three-layer governance

Governance is the spine of this repo. Three enforcement layers apply at different points in
the development cycle.

### Layer 1 — Compile-time (analyzers + banned APIs)

Five custom Roslyn analyzers ship in `ApiPlatform.Analyzers`. `RS0030` is wired via
`BannedApiAnalyzers`. All six rules are promoted to `error` in `.editorconfig` — they fail
the build in the IDE and in CI, not just at code review.

| Rule | What it prevents |
|---|---|
| **APL0001** | A governed source (`IGovernedSource` implementor) registered outside an `IConnectorModule.Register` block — a governance bypass. |
| **APL0002** | An `IConnectorModule` that is not `public` — silently never discovered by the connector registry. |
| **APL0003** | `DateTime.Now` / `UtcNow` / `DateTimeOffset.Now` / `UtcNow` — non-deterministic clock reads. Use `TimeProvider.GetUtcNow()`. |
| **APL0004** | `Console.Write*` — bypasses `ILogger`, loses correlation IDs and structured output. |
| **APL0005** | A `TypedResults.Problem(...)` call missing the `type:` argument — forces consumers to match on unstable title strings. |
| **RS0030** | `HttpClient`, `SqlConnection`, `DbContext` used outside `ApiPlatform.Integration` — reaches member data without going through the governed seam. |

### Layer 2 — Runtime (DynamicProxy audit interceptor)

Every interface that extends `IGovernedSource`
(`ApiPlatform.Platform.Connectors.IGovernedSource`) is wrapped at DI registration time in a
Castle DynamicProxy carrying the single `AuditInterceptor`
(`src/ApiPlatform.Integration/Acl/Governance/AuditInterceptor.cs`).

The interceptor:
- opens an OpenTelemetry activity around each async operation (ops telemetry);
- writes a compliance `AccessAuditRecord` to `IPlatformAudit` (audit trail);
- masks sensitive id/PII scalars via `IPiiRedactor`.

The governance is keyed on `typeof(IGovernedSource).IsAssignableFrom(type)` — a type
relationship, not a namespace — so a seam is governed wherever it is declared. A new vendor
adapter wired through an `IConnectorModule` inherits all three behaviors automatically;
the developer writes zero audit code.

### Layer 3 — Test-time (architecture facts)

`tests/ApiPlatform.Tests/ArchitectureTests.cs` uses NetArchTest to assert layer purity:

- `ApiPlatform.Platform` has no ASP.NET Core dependency.
- `ApiPlatform.Contracts` has no web, cloud, or integration dependencies.
- `ApiPlatform.Integration` has no ASP.NET Core or endpoint dependency.
- Vendor `*Source` implementation classes are `internal`.
- `IConnectorModule` implementations are `public`.
- Every canonical seam interface implements `IGovernedSource`.

These tests catch restructuring that defeats the compile-time rules — e.g. a refactoring
that accidentally makes a vendor class public, or a project reference that short-circuits a
layer boundary.

---

## 5. Canonical domain + anti-corruption layer

The ACL (`ApiPlatform.Integration`) is the stability boundary between vendor reality and
the canonical model. Two principles keep it manageable:

- **Borrow the vocabulary.** FDX data shapes for accounts and transactions; BIAN service-domain
  naming for service concepts. No inventing from scratch.
- **Grow per real consumer demand.** Model only what a real consumer needs. Coverage gaps
  in a vendor (fields the vendor does not supply) are simply absent from the canonical
  response, not zeroed or fabricated.

Multiple vendor adapters can sit behind one canonical seam. A routing aggregator
(`RoutingAccountSource`) picks the right adapter per request; the consumer cannot tell which
vendor backed which account. Vendor field names exist only inside the adapter class; canonical
names are used everywhere above.

---

## 6. API conventions (enforced by analyzers and linting)

- **Resources:** plural nouns — `/accounts`, `/customers`, `/work-items`.
- **Versioning:** in the path — `/v1`, `/v2` — bumped only on breaking change. Pair with
  an N-1 support + deprecation policy (`Deprecation` / `Sunset` headers).
- **Errors:** RFC 9457 Problem Details (`application/problem+json`). One error shape across
  the platform. No bespoke error envelopes. Type URI required (APL0005).
- **Upstream faults:** a vendor outage or timeout propagates through `Result<T>` /
  `UpstreamOutcome` and surfaces at the HTTP edge as 503 (transient / retryable) or 502
  (vendor error), always with a canonical RFC 9457 response. It never surfaces as a 404,
  an empty body, or a generic 500.
- **Format vs version are separate axes:** version in the path, format in `Accept`.
  Multi-format output: one canonical model → N representations via ASP.NET Core output
  formatters / content negotiation.
- **Pagination:** cursor-based — `?cursor=&limit=` with `nextCursor` in responses.
- **Idempotency:** `Idempotency-Key` required on writes. The middleware enforces
  principal-scoped, atomic set-if-absent semantics. Keys are scoped to the authenticated
  principal; two different principals with the same key and route are fully isolated.
  `IDEMPOTENCY_STORE=Memory` (default, single-process) or `Distributed`
  (`IDistributedCache`-backed; swap to Redis / SQL for multi-instance).
- **Scopes:** `resource.action` — `account.read`, `account.write`, `customer.read` —
  declared in the TypeSpec spec and enforced at the endpoint.

---

## 7. Auth model

Three modes, selected by the `AUTH_MODE` environment variable:

| Mode | When to use | How it works |
|---|---|---|
| `Header` (default) | Local dev and tests | Reads scopes from an `X-Scopes` header. No token, no signing key. Never use in production. |
| `LocalJwt` | Enforced auth without cloud | HS256 JWT Bearer, self-minted. In `Development` with no `AUTH_SIGNING_KEY`: falls back to the committed non-secret dev key and logs a startup warning. In any non-Development environment with no key: refuses to start (`InvalidOperationException` at composition). Mint tokens with `LocalDevJwt.Mint()`. |
| `Entra` | Staging and production | Microsoft Entra ID JWT Bearer. Requires `AUTH_AUTHORITY` and `AUTH_AUDIENCE`. |

All three modes resolve to the same scope-based authorization policies
(`PlatformScopes.ScopeClaimType`), so endpoint authorization code is identical across modes.

`LocalJwt` makes a fresh clone genuinely fail-closed: no cloud tenant, no signing key required,
but real JWT validation with real scope enforcement. A developer cloning the repo and running
with `AUTH_MODE=LocalJwt` gets enforced auth immediately.

---

## 8. Eventing

Two background workers handle event emission:

**`ApiPlatform.EventSource`** — streams work-item changes from `IWorkItemChangeFeed` and
emits them to `IEventSink`. Position tracking: `EVENTSOURCE_POSITION_STORE=Memory` (default,
zero-config, loses position on restart) or `Durable` (file-backed, resumes after restart).
Changes are committed group-atomically: a group (all changes sharing one timestamp) is advanced
only after every member is emitted — at-least-once delivery.

**`ApiPlatform.Api` event publisher** — the HTTP host publishes events on write operations.
`EVENT_PUBLISHER_TYPE=InMemory` (default, zero-config) or `EventGrid` (requires
`EVENTGRID_TOPIC_ENDPOINT` + `EVENTGRID_TOPIC_KEY`). With `EventGrid` and absent config in a
non-Development environment: startup fails fast rather than silently dropping events.

**`ApiPlatform.Poller`** — polls a creation feed on a fixed interval, masks PII, and records
audit events. `CURSOR_STORE=Memory` (default) or `Durable` (file-backed). Shares the same
`IPlatformAudit` and `IPiiRedactor` as the API host.

---

## 9. Observability — two faces, two sinks

- **Ops face:** OpenTelemetry .NET → Azure Monitor exporter. Auto-instruments ASP.NET Core,
  `HttpClient` in the ACL, and background activities. W3C-correlated end to end.
- **Audit face:** distinct concern, distinct store. Who-did-what-to-which-member-when,
  examiner-grade retention, append-only. `IPlatformAudit` seam with a pluggable sink —
  AOT-safe `JsonFileAuditSink` default (used by the Poller AOT host); `Audit.NET`
  (`Audit.WebApi`) optional for the API host. Audit is not tracing; the two are separate
  outputs of one middleware stack.

The `AuditInterceptor` writes audit records for every async call through a governed seam.
The `IPlatformAudit` seam is registered in core DI so any host — API, Poller, EventSource —
uses it without repeating the wiring.

---

## 10. Toolchain

| Concern | Tool | Notes |
|---|---|---|
| Contract source | **TypeSpec** (`spec/`) | `make spec` → `spec/tsp-output/openapi.v1.yaml` |
| Spec lint | **Spectral** | `make lint` compiles TypeSpec then lints the emitted contract |
| Contract drift detection | `tools/drift-check.{sh,js}` | `make drift` — boots the API, captures `/openapi/v1.json`, compares to TypeSpec output |
| Runtime OpenAPI | **ASP.NET Core built-in** (`MapOpenApi`) | `/openapi/v1.json` at runtime |
| Mock from spec | **Prism** | `make mock` — runs against `openapi/accounts.v1.yaml` |
| Static dev portal | **Redocly CLI** | `make docs` / `npm run docs:build` |
| Versioning | **Asp.Versioning.*** | URL-path versioning, deprecation headers |
| Error model | **RFC 9457 Problem Details** | Built into ASP.NET Core (`AddProblemDetails`); APL0005 enforces `type` URI |
| Idempotency | Platform middleware (`IdempotencyMiddleware`) | Principal-scoped, atomic set-if-absent; in-memory default, `IDistributedCache` option |
| Multi-format output | ASP.NET Core output formatters | Content negotiation + media-type profiles |
| Ops telemetry | **OpenTelemetry .NET** → Azure Monitor | W3C-correlated; `AddServiceDefaults` wires it |
| Audit trail | **`IPlatformAudit`** seam | Pluggable sink; `JsonFileAuditSink` (AOT-safe default) or `Audit.NET` |
| Compile-time governance | **Custom Roslyn analyzers** (`ApiPlatform.Analyzers`) | APL0001–APL0005, promoted to `error` via `.editorconfig` |
| Banned primitives | **BannedApiAnalyzers** (RS0030) | No raw `HttpClient` / `SqlConnection` / `DbContext` outside the ACL |
| Architecture fitness | **NetArchTest** | Dependency-direction + vendor-privacy as unit tests |
| Vendor mocking | **WireMock.Net** | Offline-testable ACL adapters |
| Integration testing | **WebApplicationFactory** | In-process; `ApiPlatform.Tests` |
| Sanitize gate | `tools/sanitize.sh` | Two-layer: structural patterns (CI) + local literal denylist |

---

## 11. Infrastructure (PoC)

Self-contained Bicep, Makefile-driven, one resource group, scale-to-zero.

- **API tier compute:** Azure **Container Apps** (Consumption, scale-to-zero → ~$0 at rest).
  Blue/green via Container Apps revisions + traffic split. Image built in-cloud by
  `az acr build` — no local Docker required.
- **Gateway (optional add-on):** APIM Consumption for scope enforcement / rate-limit /
  catalog; omitted from the default cheap PoC, where the app is reached directly via
  Container Apps ingress.
- **Observability:** Log Analytics workspace + Application Insights (workspace-based),
  connection string injected post-deploy.
- **Dev portal:** Static OpenAPI docs (Redocly `build-docs`) in Blob Storage static website.
- **IaC:** Bicep.

### CI pipeline (`.github/workflows/ci.yml`)

Stages, fail-fast left to right:

```
sanitize (structural patterns) → build → test
```

The sanitize job runs first and blocks the build if structural patterns match. Build and test
target `src/ApiPlatform.slnx` in Release configuration with locked-mode restore.

---

## 12. Decisions (locked 2026-06-24)

1. **Repo identity** — public sanitized reference on GitHub (MIT), `dotnet-api-platform`;
   fictional Northwind CU, generic source systems, no real institution specifics.
2. **API-tier compute** — Azure **Container Apps** (Consumption, scale-to-zero); blue/green
   via revisions + traffic split. Chosen over App Service slots for cost (no S1 floor).
3. **CI/CD platform** — GitHub Actions (`.github/workflows/ci.yml`) with Azure DevOps
   pipeline (`azure-pipelines.yml`) also present. Shift-left: sanitize gate before build.

Defaults locked unless vetoed: **Bicep** IaC, the §10 toolchain. APIM is an optional gateway.
