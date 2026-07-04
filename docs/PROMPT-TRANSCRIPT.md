# How this repo was prompted — a spec-driven, agent-built walkthrough

This repo was designed and built in one collaborative session between an engineer and an
AI coding agent. It's published as a reference for a specific question a lot of people are
asking right now: **how do you prompt an agent toward a large, spec-driven system without
it sprawling into mush?**

The short answer this project demonstrates: **you don't carry the whole design in the
prompt — you carry it in standards, patterns, and rules.** Lean on design-first contracts,
machine-checkable conventions, and a persistent memory of decisions, and the prompts stay
small while the system stays coherent.

> **Note on sanitization.** This is a public reference. The original session referenced a
> specific financial institution and real vendor products; all of that has been replaced
> with the fictional **Northwind Credit Union** and generic source systems ("Core Banking",
> "Cards Platform", etc.). Institution-specific strategy lived in a **private memory layer
> and a gitignored notes file** — never in the repo. A `make sanitize` gate enforced this on
> every push. The transcript below is lightly edited for that reason; the structure and
> intent are faithful to what actually happened.

---

## 1. The opening vision (one prompt, lots of signal)

The engineer didn't start with a spec. They started with intent — and explicitly told the
agent **not to assume**:

> *"I don't want you to assume. Here's where my mind is at. As a credit union, I need APIs
> that are secured, auditable, traced. Using an Azure stack, I want to build a comprehensive,
> consistent, self-discoverable API stack that follows conventions. REST. Internal colleague
> auth, external server/org auth, likely OAuth scopes / consent grants. Essentially a kickass
> .NET stack serving a complete set of financial-institution APIs securely.*
>
> *I want to take vendor specs and wrap them in OUR API layer — our logging, our error
> handling, our spelling of `accountId`. Versioning in the path (`/v1`, `/v2`) on breaking
> changes. Consistency and repeatability are KEY — this is the engine our org runs on.
> Eventual internal MCP server, agent connectivity, maybe FDX-type stuff down the road.
> Things like `/accounts`, `/customers`. Basically an open-banking API stack. Make sense?"*

That single message carries the whole north star: **canonical layer over vendors, versioning
discipline, consistency-as-a-feature, and a forward path to agents/FDX.** The agent's job was
to name the patterns (anti-corruption layer, canonical domain model, open-banking projection)
and reflect them back, not to invent a direction.

---

## 2. The decisive constraint: "design-first" + "let an agent rip"

Asked to pick between design-first and code-first, the engineer was unambiguous, and added
the framing that shaped every tooling choice after:

> *"Design-first. And know this — I'm an LLM engineering team leader. My goal is to go as fast
> as we can. I want a world where I dump some specs in and tell an agent to add it to our API
> catalog in accordance with our standards, and let it rip.*
>
> *Use FDX / open-banking as THE model. I'd like the engine to output multiple data-format
> shapes. Full observability for both ops and audit. Defer consent grants, but I want to be
> able to get there. And think about LLMs working in this repo — good, discoverable,
> consistent tooling enables LLM speed. Good REST patterns, mature API ops, mocks, tests."*

This is the load-bearing insight of the whole project:

> **Design-first makes agents fast because it converts conventions into machine-checkable
> gates.** "Is this API consistent with our standards?" stops being a subjective human review
> and becomes a lint exit code (`spectral lint`), a generated contract, and a conformance test.
> An agent self-corrects to green with no human in the loop. That's why the
> [`.spectral.yaml`](../.spectral.yaml) ruleset and [`AGENTS.md`](../AGENTS.md) exist — they
> are the standards the agent obeys.

---

## 3. The clarifying questions the agent asked

Rather than guess on the few decisions that genuinely fork the build, the agent asked **three
high-leverage questions** (and proposed sensible defaults for everything else):

| Question | Options offered | Chosen |
|---|---|---|
| **Repo identity & destiny** | Public sanitized reference · Private/internal · Decide later | **Public sanitized reference** |
| **API-tier compute** | App Service + deployment slots · Container Apps + revisions | App Service → later **pivoted to Container Apps** for cost |
| **CI/CD platform** | GitHub Actions · Azure DevOps | **Azure DevOps** (shift-left templates) |

A few smaller forks were settled in conversation rather than as formal questions:

- **Design-first vs code-first** → design-first (the spec is the source of truth).
- **Canonical model strategy** → *borrow* the vocabulary (FDX data shapes + BIAN domain
  naming) and grow per real consumer demand, rather than inventing a domain model from
  scratch. This is where most of these efforts stall; borrowing avoided the swamp.
- **Dev portal** → "just a dumb static site in blob storage, internal only" → a generated
  Redocly catalog served from a Blob static website.
- **Consent grants** → explicitly deferred (architect toward it, don't build it yet).

The pattern: **the agent asked only about decisions it couldn't responsibly default, and
recommended a first option for each.** Small number of questions, high information value.

---

## 4. The build unfolded as small, incremental prompts

Once the standards were in place, each new capability was a *short* prompt — because the
conventions did the heavy lifting:

1. *"Build it."* → design-first scaffold, the `/v1/accounts` golden-path slice, infra, pipeline.
2. *"Add `/customers`."* → cloned the golden path; consistency came for free.
3. *"Rename it to `dotnet-api-platform`. Build it in Azure. Is there something cheaper than the
   S1 plan? I'm OK with containers for blue/green."* → pivoted to Container Apps (scale-to-zero,
   blue/green via revisions) and deployed live.
4. *"Add another vendor — that'd be cool."* → a second source system behind a routing
   anti-corruption layer, with a deliberately different raw shape and a realistic coverage gap.
   (The agent **declined** to use a real vendor's proprietary specs in the public repo and
   proposed a fictional vendor instead — sanitization in action.)
5. *"Full send. Event Grid, webhooks, maybe queues for fan-out."* → the async face: publish a
   minimal event, fan out to a webhook and two queues.
6. *"Make a PNG architecture diagram with the app exploded into its modules."* → rendered from
   the `diagrams` library.

None of these prompts re-explained the conventions. They didn't have to — the conventions
were encoded in the repo and in the agent's standing rules.

> **By the clock.** From the opening spec prompt to the final commit of the event-driven slice
> was about **1 hour 50 minutes** of focused build (~1 h 39 min from the first commit to that
> commit, per git). In that window the platform went from nothing to a deployed, design-first,
> multi-vendor, event-driven API on scale-to-zero infrastructure — including catching and
> fixing a real container-permissions bug that only surfaced on the live deploy. The speed
> came from the standards, not from heroic prompting.

---

## 5. What was pulled from a persistent memory layer (and why it mattered)

The engineer asked, fairly: *what did you pull from shared memory?* This is central to the
"lean on standards/rules" thesis, so here it is honestly — described by **category**, since
the specifics were institution-private and stayed out of the repo:

- **Coding & commit standards** — formatting, file/naming conventions, commit-message
  attribution, "commit small and often." The agent didn't ask how to write a commit; it knew.
- **A sanitize-before-public rule** — the reason the public repo never contained the real
  institution or vendor names, and why the agent quarantined that context into a gitignored
  notes file and added a `make sanitize` gate. (It caught a real near-miss early.)
- **A subagent-model preference** — heavy code generation was delegated to a cheaper model
  while design and judgment stayed on the main model. The transcript's "let an agent rip"
  goal mapped directly onto this.
- **Prior architectural decisions** — e.g. that a messaging backbone (not the API gateway) is
  the real integration spine, and that the gateway fronts only the API-capable subset. The
  canonical-layer-as-stability-boundary idea was already an established conviction, not a
  fresh debate.
- **Domain reference material** — open-banking / FDX fundamentals (canonical layer as a
  *projection*, polymorphic account types, scope-gated basic-vs-detailed fields, opaque-id
  mapping). These were read as **reference only** and re-expressed in sanitized, generic form;
  no proprietary field names were reproduced.

The payoff: **decisions didn't get re-litigated, standards didn't get re-explained, and
compliance constraints were honored by default.** That is most of where the speed came from.

---

## 6. The takeaway for prompting spec-driven agentic work

If you're struggling to prompt an agent toward something this size, the lessons this repo
embodies:

1. **Lead with intent, not a spec — but say "don't assume."** Let the agent name the patterns;
   correct it where it's wrong.
2. **Make "design-first" a hard rule.** It's what turns "follow our conventions" into a
   pass/fail gate an agent can close by itself.
3. **Encode the conventions in the repo** (`.spectral.yaml`, `AGENTS.md`, a golden-path slice
   to clone). The prompt shouldn't carry the standards; the repo should.
4. **Borrow domain vocabulary** (FDX/BIAN here) instead of inventing it. Don't model the ocean.
5. **Answer only the forking questions; default the rest** — and recommend an option.
6. **Keep standards and prior decisions in a persistent memory** so each new prompt can be
   small. The system stays coherent because the *rules* are coherent.
7. **Sanitize relentlessly if it's going public** — a gate, not good intentions.

The result: a complete, design-first, multi-vendor, event-driven API platform — built through
a handful of short prompts, kept consistent by standards rather than by lengthy instructions.

---

*See [`ARCHITECTURE.md`](../ARCHITECTURE.md) for the system design, and [`AGENTS.md`](../AGENTS.md)
for the conventions an agent follows when adding to this repo.*
