# Architecture

```
┌────────────────────┐   HTTP: lists and commands   ┌───────────────────────────────────┐
│ Ardan.Web          │ ───────────────────────────► │ Ardan.Api                         │
│ Blazor Server      │ ◄─────────────────────────── │ minimal API · SignalR hub         │
│ localhost:5090     │   SignalR: snapshot 4× / s   │ localhost:5080                    │
└────────────────────┘                              │                                   │
                                                    │  StoreHost ── one lock ──┐        │
                                                    │                          ▼        │
                                                    │                Warehouse model    │
                                                    │                (Ardan.Core)       │
                                                    │                                   │
                                                    │  SimulationLoop   10× / s         │
                                                    │  LiveBroadcaster   4× / s         │
                                                    │  JournalWriter → SQLite (500 ms)  │
                                                    └───────────────────────────────────┘
```

## Ardan.Core: the model

`Warehouse` owns everything in the store: the rack (`StorageGrid` of `Lane`s), Taxis, VTUs (`Lift`),
the infeed and outfeed (`Conveyor`), orders, jobs, alarms, the event log, throughput counters and the store climate.
It is split by concern into `Warehouse.Fleet`, `.Flow`, `.Orders`, `.Faults` and `.Commands`.

`Advance(elapsed)` runs fixed 0.1 s steps (multiplied by the demo speed). Each step:

1. takes new orders from the ERP feed,
2. moves the infeed (profile check, reject or hand over) and the outfeed (dispatch at the dock),
3. creates putaway and retrieval jobs, then gives waiting jobs to Taxis,
4. moves the VTUs, then the Taxis,
5. updates the climate, injects random faults and watches battery, Wi-Fi and temperature.

Operator commands (`CreateOrder`, `SendToCharge`, `ResetTaxi`, `Reinduct`…) return a `CommandResult`
with a sentence the screen shows as it is. Read models in `Contracts` are immutable records built by `ViewBuilder`.

### The shift as a game

`ShiftScore` watches the store and never changes it. The model tells it when a pallet is stored or dispatched
and when an order completes; `AlarmBoard.Closed` tells it who closed an alarm (the operator, remote support or the supervisor).
It keeps the points, the level, a star rating from the last 20 completed orders, five goals with tiers and the latest score events.
Orders that were already late when the shift started (open orders reloaded after a restart) are not rated.
Each event names its spot (a lane such as `L2·B05`, `Dock 2`, a machine id or `Infeed`), so the 3D view can show the points where they were earned.

`Advisor` turns the current state into up to three suggestions: rush an outbound order due within 10 minutes,
receive stock for a short order, ship stock that is best before within 3 days, restock a fast or steady mover with 2 or fewer free pallets.
A suggestion carries the `NewOrder` (or the order to make urgent), so accepting it is an ordinary command.
Both reach the screens in the live snapshot as `Shift` and `Suggestions`.

### Jobs and routes

- Waiting jobs are served by priority, then age. Each goes to the able Taxi with the lowest estimated time to the pickup:
  driving time, VTU travel, boarding and the current VTU queue.
- A route is a queue of legs: `Drive(to bay)`, `Ride(from level, to level)` and `Transfer(pick up or set down)`.
  To change level a Taxi drives to the dock (bay 0), rides a VTU and drives out again.
- Putaway reserves a place in its lane; retrieval locks its lane. Busy lanes are skipped by slotting and by retrieval.
- Handling time grows with lane depth, because the Taxi travels into the lane to reach the pallet.

### State machines

- Taxi: `Idle`, `Charging`, `Driving`, `WaitingForAisle`, `WaitingForLift`, `OnLift`, `Loading`, `Unloading`,
  `Fault`, `OutOfService`. A fault remembers the previous state, so a reset carries on with the same job.
- VTU: `Idle` → `Moving` (to the Taxi) → `Boarding` → `Carrying` → `Leaving` → `Idle`, with `Fault` from the moving states.
  A Taxi joins the queue of a working VTU with the fewest rides waiting, then the nearest one.

## Ardan.Api: host, live feed and journal

- `StoreHost` keeps the single `Warehouse` behind a `System.Threading.Lock`: `Read` for views, `Run` for commands,
  `Advance` for the loop.
- `SimulationLoop` advances the model every 100 ms using real elapsed time (capped at 1 s).
- `LiveBroadcaster` pushes a `LiveSnapshot` to every screen every 250 ms. It only includes lanes whose version
  changed since the previous snapshot, so a typical push carries a handful of lanes instead of all 108.
- Persistence is write-behind. The model reports changes through `IJournal`; `DbJournal` queues them on a channel and
  `JournalWriter` upserts them into SQLite with EF Core every 500 ms. The simulation never waits for the database.
- On start, `StoreLoader` seeds the demo site if the database is empty, closes alarms left open by the last run,
  loads stored pallets and open orders, and continues every id sequence so new ids never clash with old ones.

| Method | Path | Purpose |
| --- | --- | --- |
| GET | `/api/layout` | Rack size, VTUs, site name |
| GET | `/api/snapshot` | Current live snapshot |
| GET | `/api/lanes`, `/api/lanes/{level}/{bay}` | All lanes, or one lane with its pallets |
| GET | `/api/orders?view=open\|closed\|all` | Orders |
| POST | `/api/orders` | Create an order |
| POST | `/api/orders/{id}/cancel`, `/api/orders/{id}/priority` | Cancel, change priority |
| GET | `/api/skus`, `/api/skus/{code}/pallets` | Products with stock, pallets of a product |
| GET | `/api/pallets/{id}` | A pallet and its movement history |
| GET | `/api/alarms?view=history` | Active or closed alarms |
| POST | `/api/alarms/{id}/acknowledge`, `/api/alarms/{id}/action` | Acknowledge, run the alarm's action |
| POST | `/api/taxis/{id}/charge\|park\|resume\|reset` | Taxi commands |
| POST | `/api/lifts/{id}/reset`, `/api/conveyors/{id}/reset` | Resets after a stop |
| POST | `/api/simulation/speed` | Demo speed: 1×, 2× or 5× |
| GET | `/api/search?q=` | Pallets, orders, products and machines |

Commands answer `200` with the result sentence, or `409` with the reason they were refused.

## Ardan.Web: the console

- `LiveFeed` is one SignalR connection shared by every open screen. It merges lane changes by version,
  reloads the layout after a reconnect and reports `Connecting`, `Live`, `Reconnecting` or `Offline`.
- `LiveComponent` is the base for live screens. It redraws at most once per `RefreshEvery`
  (rack: every snapshot, Fleet: 0.5 s, Overview: 1 s, Orders: 2 s, Inventory: 4 s) and can reload extra data first.
- `WcsApi` is a typed `HttpClient` for lists and commands. Command results become toasts.
- The live view is a 3D scene in `wwwroot/js/world.js`, an ES module on three.js r170 loaded from jsDelivr, with its parts in
  `js/world/`: `site.js` (sizes, colours, themes), `scene.js` (the building, rack, conveyors and docks), `camera.js`
  (the camera rig and mouse, touch and keyboard controls, kept inside fixed zoom and position limits) and `flow.js`
  (the moving arrows that show where each Taxi is heading, green into store and blue out of store).
  `WorldView` imports it once, sends the fixed site shape, then pushes a `WorldFrame` on every snapshot: Taxis with their
  job's from and to places, VTUs, conveyors, the lanes that changed, alarm markers and new score events. Texts and tones are
  worked out in C#, so the module only places things. Machines glide between frames over the measured snapshot interval;
  a Taxi visibly reaches into the lane while it picks up or sets down. Clicks come back through `[JSInvokable] Pick`.
  Without WebGL, or if the module cannot load, the page falls back to the 2D rack view.
- The HUD around the scene is ordinary Blazor: `HudTop` (score, clock, rating), `HudBoard` (jobs, offers, goals),
  `HudRail` (resources and view tools), `HudAlerts` and `Inspector`. Machine and alarm commands come from `OperatorCommands`,
  which the Fleet, Orders and Alarms pages share, so a command asks the same question wherever it starts.
- Overview and Fleet are built from shared parts: `TaxiCard`, `LiftCard` and `BeltCard` in `Components/Machines`,
  `Gauge`, `LevelMeter`, `Sparkline` and `StatTile` in `Components/Ui`, and `OfferCard` and `GoalRow`, which the live view's
  board uses too.
- Every tooltip is drawn by one layer above the page (`tip` in `app.js`), so panels, scroll areas and the 3D view never cut it off.
  Hover texts in the 3D view use the same layer.
- The 2D rack view draws the static frame once in SVG and places lanes, Taxis, carriages and pallets as HTML markers
  in percent. Markers glide between snapshots with 0.26 s linear transitions. `LaneCell` only redraws when its lane
  version, colour or selection changes.
- Usability: help for every page (`?`), global search (`⌘K`), a confirmation for every command that says what will happen,
  tooltips on every number and state, light and dark themes, a phone layout.
