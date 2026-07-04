# Scaling Guide — small → big, and how you know

> Public reference. The institution (**Northwind Credit Union**) and source systems
> ("Core Banking", "Loan Origination", "Data Warehouse") are fictional placeholders.
> Companion to [ARCHITECTURE.md](ARCHITECTURE.md).

This platform is built as a **modular monolith**: one deployable .NET API on Azure
Container Apps, but internally segmented along domain seams (`Endpoints/` per resource,
`Acl/` per vendor, a shared canonical `Domain` model, one observability + audit pipeline).
That shape is deliberate. It buys a long runway on a single deployable, and it pre-cuts the
lines you would later split along **if** you ever have to.

The core thesis, and the thing to internalize if you come from an AWS/microservices reflex:

> **Scale *out* (more replicas) for a long time before you scale *apart* (more services).**
> Raw request volume is a replica-count problem, not an architecture problem. You only
> split the monolith for *organizational* and *blast-radius* reasons, never for RPS.

---

## The ladder

Each rung is a threshold. For each: the **signal** that you've hit the edge (something you
can read off an instrument, not a vibe), and the **move** to the next rung. You climb rungs
independently — you do not have to be on rung 3 to do rung 5.

| Rung | State | Signal you've hit the edge | Move | Rough cost |
|---|---|---|---|---|
| **0** | PoC: scale-to-zero, direct ingress, no APIM, header-auth shim, file audit | *(you are here)* — building, no real consumers, no real data | — | ~$5/mo (ACR) |
| **1** | Internal GA hardening | First real consumer, OR first real member data flows | Real JWT auth · durable audit sink · always-on App Insights · `minReplicas: 1` | +Log Analytics ingest |
| **2** | Horizontal scale-out | p95 latency climbs under load · single-replica CPU/concurrency saturates | Raise KEDA `maxReplicas`, tune concurrency. **No code change.** | linear w/ traffic |
| **3** | Gateway in front (APIM) | First consumer you don't control · need per-consumer quota/rate-limit · need a published catalog · need one auth policy across many APIs | Standard v2 APIM, import OpenAPI, internal ingress behind it | +APIM tier floor |
| **4** | Split the monolith | Teams block each other on deploys · one resource's scale/availability profile diverges · blast-radius isolation · CI too slow | Extract along the ACL/domain seam into a second Container App; APIM routes by path | +1 app, +CI |
| **5** | Owned state / caching | Vendor rate-limits or latency force caching · financial writes need idempotency · read models needed | Azure Cache (Redis), Cosmos/SQL for owned projections, idempotency store | +data services |
| **6** | Multi-region / DR | Business or examiner RTO/RPO requirement | APIM Premium multi-region (or Front Door) + regional Container Apps + geo-replicated audit | 2× regional + Premium |

The jump that matters most and is most often gotten wrong: **rung 2 → rung 4.** People feel
load pain and reach for "microservices." The fix for load is rung 2 (replicas), which is a
config line. Rung 4 is for *people and failure-domain* problems, not load.

---

## How you know — the instrument panel

"How do I know the threshold is there?" Every rung maps to a concrete reading. Wire these
once (Application Insights + Container Apps metrics) and the ladder becomes legible.

| Question | Where you read it | Threshold that means "next rung" |
|---|---|---|
| Are we CPU/concurrency bound on one replica? | Container Apps replica CPU %, active request count vs. `concurrentRequests` target | Sustained >70% CPU or queueing at the concurrency cap → **rung 2** |
| Is latency degrading under load? | App Insights `requests` p95/p99 by operation | p95 drifting up while RPS rises (not flat) → **rung 2** |
| Are we paying cold-start tax? | App Insights request duration distribution, first-call-after-idle spikes | Real consumers hitting cold starts → **rung 1** (`minReplicas: 1`) |
| Is one resource hot relative to others? | App Insights `requests` grouped by `operation_Name` / route | One route is 10–100× the rest and scales differently → candidate for **rung 4** split |
| Is one vendor dragging the whole app? | App Insights `dependencies` by target (the ACL's `HttpClient` calls), failure rate / duration | One vendor's failures/latency bleeding into unrelated routes → **rung 4** (blast-radius split) |
| Are deploys serializing teams? | DevOps cycle time, "waiting on another team's merge" | Two teams blocked on one pipeline → **rung 4** (org split) |
| Is the vendor rate-limiting us? | App Insights dependency 429s / throttle responses | Repeated vendor throttling → **rung 5** (cache) |
| Do we need a quota/contract per consumer? | (qualitative) onboarding a party you don't control | Any external/partner consumer → **rung 3** (APIM) |

If an edge isn't visible on this panel, you can't manage it — which is why rung 1 (always-on
observability + durable audit) comes *before* you have scale problems, not after.

---

## Monolith → services: where the seams already are

You do not refactor to split. The seams are pre-cut:

```
Endpoints/AccountEndpoints.cs   ─┐
Acl/IAccountSource.cs            │  one domain slice. Extract this whole
Acl/RoutingAccountSource.cs      ├─ column and it is a standalone service.
Acl/*AccountSource.cs (vendors)  │  The canonical Domain model + the contract
Domain/Models.cs  (shared)       ─┘  (OpenAPI /v1/accounts) do NOT change.
```

When you extract `/v1/accounts`:

1. New Container App hosts the accounts slice + its vendor adapters.
2. APIM routes `/accounts/*` to the new backend; `/customers/*` stays on the old one.
3. The canonical `Account` contract is byte-identical — **consumers feel nothing.**
4. Anything async between slices rides the event backbone (Event Grid / the ESB), not a
   chatty synchronous mesh. *Smart endpoints, dumb pipes.*

**The four — and only — real triggers to do this:**

1. **Team autonomy** — two teams can't ship without coordinating on one codebase/pipeline.
2. **Divergent scale profile** — one resource needs 100 replicas while the rest need 2;
   co-scaling wastes money or co-locating risks starvation.
3. **Blast radius** — one vendor adapter's instability must not be able to take down the
   single front door for every resource.
4. **Build/CI gravity** — the monolith's build+test loop got slow enough to hurt velocity.

Note what's **not** on that list: request volume. Replicas (rung 2) handle volume. If your
only reason to split is "we get a lot of traffic," you're about to add distributed-systems
tax to solve a problem a config line already solved.

---

## APIM: when, and how it plumbs in

APIM is **not** the same layer as the in-app router. The router (`RoutingAccountSource`)
fans one canonical request across vendors *inside* the app. APIM is the *contract + policy
edge* in *front* of the app. The data path is:

```
consumer → [ APIM: authN/Z, scope, rate-limit, quota, route, observe ] → Container Apps
           internal ingress → Kestrel → minimal-API endpoints → ACL router → vendors
```

APIM never sees the router; it sees `/v1/accounts`. Plumbing:

1. Container App ingress → **internal** (no public IP).
2. APIM **backend** entity → the Container App's internal FQDN.
3. **Import the OpenAPI document** into APIM — operations are generated, not hand-built.
   This is the design-first payoff: the gateway surface is the same source of truth.
4. Policies: `validate-jwt` · `rate-limit-by-key` · `quota` · `cors` ·
   `set-header X-Authenticated-User-Id` (so the app's audit can record the gateway-
   authenticated principal).
5. **Products** (Internal / Partner / Public) bundle APIs + subscription + policy.

**Tier gotcha for an internal-first platform:** APIM **Consumption cannot be VNet-injected**
— it's a shared multi-tenant gateway with a public endpoint. "Internal-only" + Consumption
is a contradiction. For true internal isolation use **Standard v2** (supports VNet
integration) or Premium; if you stay Consumption, lock the Container App ingress to APIM by
another means (auth header / mTLS / IP allowlist).

**Defense in depth:** keep in-app scope authorization live even after APIM validates the
JWT. The gateway authenticating does not excuse the service from re-verifying. Zero trust:
the edge is not the only gate.

---

## Observability & audit across the gateway hop

Adding APIM adds a second observation point. Divide the responsibilities; don't duplicate:

| Concern | Owner | What it captures |
|---|---|---|
| **Ops / tracing** | APIM **and** app, same App Insights, **W3C-correlated** | one `operation_Id` spanning gateway → app → vendor `HttpClient` |
| **Access logging** | APIM | envelope: subscription key, IP, endpoint, status — API analytics |
| **Business audit** | App (Audit.NET), durable append-only store | who-did-what-to-which-member-when — examiner grade |

The non-negotiable: turn on APIM's App Insights integration with the **W3C** correlation
protocol so traces span the gateway→app hop as one operation. (Same cross-process
correlation pattern already proven in `azure-playground`, with APIM as the first span.)

**Audit stays in the app.** APIM sees the HTTP envelope; it does not know "colleague X read
member Y's account." Only the app layer has the business context. APIM forwards
`X-Authenticated-User-Id`; the app records it. Correlate the two by request id — they are
complementary, not interchangeable. Do **not** relocate audit to the gateway.

---

## AWS → Azure translation (you came from AWS; here's the map)

| You know (AWS) | Here (Azure) | Caveat for this platform |
|---|---|---|
| ECS Fargate / App Runner | Container Apps | scale-to-zero is real; cold start is the tradeoff (rung 1 sets `minReplicas: 1`) |
| Application Auto Scaling | KEDA (built into Container Apps) | scale on HTTP concurrency, CPU, or queue depth |
| API Gateway | API Management | heavier — closer to **Apigee** (gateway + portal + policy engine); tiering matters |
| EventBridge | Event Grid | the event face of the canonical model |
| SQS / SNS | Service Bus | queues/topics for the async backbone |
| Cognito (workforce) / IAM | Entra ID | workforce JWT; client-credentials for M2M |
| Cognito app clients | Entra app registrations + APIM subscriptions | per-consumer M2M onboarding |
| CloudWatch + X-Ray | Azure Monitor + Application Insights | one product; OTel-native |
| IAM roles for tasks | Managed identity | the killer feature — no secrets in config |
| CloudFormation | Bicep | (Terraform stays Terraform) |
| ElastiCache | Azure Cache for Redis | rung 5 |
| DynamoDB / Aurora | Cosmos DB / Azure SQL | rung 5 owned state |

The one reflex to unlearn: in AWS the instinct is often "Lambda-per-thing, service-per-
thing." Here the modular monolith on Container Apps is the correct default, and it scales
out a long way before any split earns its distributed-systems tax.
