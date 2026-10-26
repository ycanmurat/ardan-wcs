# AI-assisted features: plan for phase 2

Status: planned. Nothing on this page is built yet. The console shows the same plan on the Insights page.

## Why

| Today on shift | With the feature |
| --- | --- |
| Questions need a report or someone who knows where to look. | Ask in plain English and get an answer with links to the records. |
| Alarm steps are generic for each alarm code. | See what fixed the same alarm on the same machine last time, and how long it took. |
| Lanes slowly fragment: part-full lanes, fast movers drifting to the back. | Housekeeping moves are proposed for quiet hours and approved by a supervisor. |
| Wear shows up as a stop. | Machines that drift from the fleet's normal are flagged weeks earlier. |

## Ground rules for every feature

1. **A person decides.** Anything that would move a pallet or change an order needs a named person to approve it.
2. **Read-only by default.** The assistant's tools can only read. There is no tool that sends a command.
3. **Answers show their sources.** Every answer links to the orders, pallets, lanes or alarms it is based on.
4. **Site data stays private.** The model is reached through one gateway that can point at a hosted model or an
   on-site one. Site data is not used to train models.
5. **Everything is logged.** Questions, tool calls, answers and approvals go into the same journal as store events.
6. **Fails safe.** Calls have timeouts. If the AI service is slow or down, the control system carries on unchanged.

## Architecture

A new service, `Ardan.Insights`, sits beside the control system. It never talks to the `Warehouse` model directly.

```
Ardán screens ──► Ardan.Insights ──► model gateway ──► hosted or on-site language model
                       │
                       ├──► read-only tools ──► existing WCS API (/api/search, /api/orders, /api/lanes, ...)
                       ├──► knowledge index ──► alarm history, maintenance notes, machine manuals
                       └──► proposal queue  ──► supervisor approves ──► WCS command (low priority job)
```

| Part | Plan |
| --- | --- |
| Model gateway | One `IChatModel` interface with an adapter per provider. Configuration picks the model per site. Streaming answers to the screen over the existing SignalR connection. |
| Tools | Thin wrappers over today's API, called with a read-only token: `search`, `get_order`, `list_orders`, `get_lane`, `list_pallets`, `get_pallet`, `list_alarms`, `get_snapshot`. Each returns compact JSON plus screen links such as `/orders?select=OUT-88231`. |
| Knowledge index | Alarm history from the journal, maintenance notes and manual pages split into chunks with embeddings. Rebuilt nightly. |
| Proposal queue | A `Proposals` table: kind, payload, reason, status, approved by and when. An approved proposal becomes normal low-priority jobs through a new WCS command, so the traffic rules still apply. |
| Screens | Insights page: ask box, proposal list with Approve and Reject, fleet health table. Each alarm card gets an "Alarm guide" panel. |

### How a question is answered

1. The operator asks: "Why is OUT-88231 running late?"
2. The service sends the question, the tool list and a short brief (site, storage rules, answer format) to the model.
3. The model calls tools, for example `get_order("OUT-88231")`, then `get_lane(...)` for the lanes it waits on.
4. The service runs each call against the WCS API and returns the results as data, never as instructions.
5. The model writes the answer. Before it is shown, the service checks that every id it cites came back from a tool.
6. The answer streams to the screen with links to the order, the lanes and any alarm involved.

## The four features

### 1. Ask the store

- **What:** answers questions about stock, orders, machines and alarms in plain English.
- **Examples:** "Which cheddar batches are best before this week, and where are they?",
  "How many pallets went to dock 2 since 6 am?", "Which Taxi stopped most often this week?"
- **Data:** live snapshot, orders, pallets and their movement history, alarms.
- **Safety:** read-only tools; ids in the answer are checked against the tool results.
- **Done when:** shift leads answer most day-to-day questions without a report, and a fixed set of 50 questions
  scores at least 95% correct.

### 2. Alarm guide

- **What:** when an alarm is raised, shows similar past alarms (same code, machine and level), what fixed them,
  how long that took, and the right manual page.
- **How:** search over the knowledge index, ranked by code, machine and level, then a three-line summary.
- **Safety:** advice only. The alarm's own steps and reset buttons stay as they are.
- **Done when:** median time from alarm to reset drops, measured from the journal.

### 3. Slotting advisor

- **What:** proposes housekeeping moves for quiet hours: merge part-full lanes of one batch, bring fast movers
  closer to the VTUs, free lanes before a large inbound delivery.
- **How:** an optimiser scores candidate moves by Taxi time saved against Taxi time used. The model only explains
  each proposal in one sentence; it does not choose the moves.
- **Safety:** a supervisor approves every batch. Moves run as low-priority jobs and give way to orders.
- **Done when:** average cycle time and the share of part-full lanes both improve over four weeks.

### 4. Fleet health forecast

- **What:** flags Taxis and VTUs that drift from the fleet's normal: longer charge times, hotter motors,
  more obstacle stops on one level, repeated drive faults.
- **How:** trend and anomaly checks on telemetry (no language model needed for the numbers), plus a weekly
  plain-English note for the maintenance team.
- **Safety:** advice only. It never takes a machine out of service.
- **Done when:** at least three months of telemetry are collected and flagged machines match later faults.

## Testing

- **Fixed scenarios.** The simulation takes its random source as a parameter, so a busy day can be replayed exactly.
  Questions and expected answers are written against those replays.
- **No-write test.** The tool layer rejects anything but reads; a test suite asks the model to "cancel", "reset" or
  "move" and checks that nothing reaches the WCS.
- **Prompt injection.** Text inside data (a pallet label, an order note, a manual page) must never change behaviour.
  Test cases hide instructions in those fields.
- **Cost and speed.** Track tokens, tool calls and time to first word per question; set budgets per site.

## Rollout

| Step | Scope | Who |
| --- | --- | --- |
| 2a | Ask the store, read-only | Pilot with two shift leads for four weeks |
| 2b | Alarm guide | All shifts |
| 2c | Slotting advisor with the approval queue | Supervisors |
| 2d | Fleet health forecast | Maintenance team, after three months of telemetry |

## Open questions

- Hosted or on-site model, decided per customer site and its data policy.
- Who owns the manuals and maintenance notes, and how they stay current.
- Which roles may approve housekeeping moves, and whether approvals expire.
