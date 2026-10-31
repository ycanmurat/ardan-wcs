# Ardán

A warehouse control system (WCS) concept for an automated pallet store, built end to end in .NET:
a live simulation of the store, an HTTP and SignalR API, and an operator console in Blazor
whose live view plays like a management game: a 3D store, a shift score, goals and one-click fixes.

Live Demo: https://yorusun.com/ardan-wcs/

Source Code: https://github.com/ycanmurat/ardan-wcs

The demo site is fictional: Glenmore Foods, a chilled distribution centre in Dundalk. Machine figures
(speeds, loads, temperatures) follow public data sheets for pallet-shuttle systems. Not affiliated with any vendor.

![The live view in 3D, dark theme: Taxi T-06 has stopped in the level 0 aisle with a red marker and its reset button in the alert strip, green arrows lead T-05 into store, with the shift score at the top and open jobs on the left](docs/screenshots/live.png)

## What the console does

| Screen | What you can do |
| --- | --- |
| Overview | The shift score with level, stars and goals, system status, KPIs with the last hour's trend, throughput, storage per level, every machine with its battery and level, alarms you can fix in place, suggested work and live activity. |
| Live system | The store in 3D, run like a game. Taxis drive the aisles, VTUs carry them between levels, pallets ride the conveyors and leave for the docks. Click any machine or lane to see it and act on it, fix stops from the alert strip, accept suggested work, and watch the shift score, level, star rating and goals. A flat 2D drawing of the rack is one click away. |
| Fleet | Every Taxi, VTU and conveyor as a card: battery, level, where it is going and what it carries. Send to charge, take out of service, reset after a stop. Each command asks for confirmation and says what will happen. |
| Orders | ERP orders plus your own, with what is due soon, late or short at the top. Filter, change priority, cancel, and create inbound or outbound orders with a live free-stock check. |
| Inventory | Products, their pallets in best-before order, and each pallet's history from the infeed onwards. |
| Alarms | What happened, the likely cause and the steps to fix it, with the right action button. Closed alarms show who fixed them and how long they were open. |
| Insights | The plan for AI-assisted features in the next phase. |

Every number and state has an explanation on hover. `?` opens help for the page and `⌘K` searches pallets, orders, products and machines.
Light and dark themes follow the system setting and can be switched in the sidebar.

## Screenshots

| Overview | Live system, light theme |
| --- | --- |
| ![Overview with the shift score, level, stars and goals, the system status, KPI cards with a trend line, throughput and storage by level](docs/screenshots/overview.png) | ![The 3D live view in the light theme with the stopped Taxi T-06 selected and its reset button in the side panel](docs/screenshots/live-light.png) |
| **Fleet** | **Alarms** |
| ![Fleet with a card per Taxi showing its battery, level and route, and the panel of a stopped Taxi with its fault, the remote support fallback and a reset button](docs/screenshots/fleet.png) | ![Alarms with counts at the top and a critical alarm with what happened, the likely cause, numbered steps and action buttons](docs/screenshots/alarms.png) |
| **New order** | **Inventory** |
| ![New order form with free stock for the chosen product and a summary of what will happen](docs/screenshots/new-order.png) | ![Pallet panel with its location, batch, weight and history from the profile check to its lane](docs/screenshots/inventory.png) |

## The live view as a game

The game layer only watches the store and offers shortcuts. Every action is an ordinary operator command
with the same checks and confirmations as the rest of the console.

| Part | How it works |
| --- | --- |
| Shift score | 10 points for each pallet stored, 15 for each pallet sent to a dock and 50 for each order finished on time. Fixing a stop yourself earns 25, and 25 more within a minute. A late order costs 25, a stop left for remote support 40 and a reject returned to the supplier 30. |
| Level | Goes up with the points earned. Each level needs 250 more points than the last. Penalties lower the score, never the level. |
| Stars | Half stars from 0 to 5, from how many of the last 20 orders finished on time. |
| Goals | Store pallets, send pallets, finish orders on time, fix stops yourself, run without a stop. Each one pays its reward and comes back a tier harder. |
| Offers | Work worth doing now: rush an order close to its due time, receive stock for a short order, ship stock near its best-before date, restock a fast mover. Nothing changes until you accept. |
| Alerts | A red ! marks a machine that needs you; hover it to see what happened. The alert strip has the fix button; the side panel has the steps. |

Drag to turn the camera, right-drag (or Shift + drag) to move it, scroll to zoom towards the cursor. On a touch screen one finger turns,
two fingers move, pinch and twist. Zoom and movement stop at the edges of the store. `Q` and `E` turn, `0` shows the whole store
and `F` is focus mode. Green arrows lead a Taxi into store, blue arrows out of store, and a frame marks the place it is going to. The 3D view uses [three.js](https://threejs.org) r170 from jsDelivr; without WebGL the page shows the 2D drawing.
The shift starts when the control server starts and is shared by everyone who opens the demo.

## Run it

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
./run.sh
```

Open http://localhost:5090. The API listens on http://localhost:5080 and serves its OpenAPI document at `/openapi/v1.json`.

To run the parts yourself:

```bash
dotnet run --project src/Ardan.Api
dotnet run --project src/Ardan.Web
```

With Docker, the way the live demo runs:

```bash
docker build -t ardan .
docker run --rm -p 8080:8080 ardan
```

The container runs the tests while it builds, then starts the API on `127.0.0.1:5080` inside the container
and serves the console on port 8080.

The production console is mounted at `/ardan-wcs` on the existing portfolio domain.
In Coolify, route `https://yorusun.com:8080/ardan-wcs` to this application and set
**Advanced → Proxy → Path prefixes** to **Keep paths as-is**. No new DNS record is needed
for this path. Root hosting remains supported for local runs and generated application domains.

Tests:

```bash
dotnet test
```

The store keeps its state in `src/Ardan.Api/App_Data/ardan.db`. Delete that file to start again from the demo stock.

## Rules the simulation follows

- Deep lanes are last in, first out. A lane holds one batch, so it never mixes best-before dates.
- Retrieval takes the earliest best-before first (FEFO), then the nearest lane.
- Velocity slotting: fast movers (A) are stored near the VTUs, slow movers (C) at the back.
- One Taxi per aisle per level. Taxis change level only on a VTU.
- The infeed checks every pallet: height up to 1,800 mm, weight up to 1,500 kg, a readable label. Failures go to the reject lane.
- Taxis take jobs above 30% battery and charge at the dock whenever they stop there.
- A stopped machine that nobody resets within 4 minutes is reset by remote support.
  A rejected pallet that is not fixed within 10 minutes goes back to the supplier.

## Project layout

| Folder | Contents |
| --- | --- |
| `src/Ardan.Core` | Domain model and simulation. No web or database dependencies. |
| `src/Ardan.Api` | ASP.NET Core minimal API, SignalR hub, EF Core journal on SQLite. |
| `src/Ardan.Web` | Blazor Web App (interactive server): the operator console and the 3D live view (`wwwroot/js/world.js`). |
| `tests/Ardan.Core.Tests` | xUnit tests for storage rules, order flow, long simulation runs, the shift score and the advisor. |
| `docs` | [Architecture](docs/architecture.md) and the [AI feature plan](docs/ai-features.md). |
