using Ardan.Api.Data;
using Ardan.Api.Live;
using Ardan.Core.Alarms;
using Ardan.Core.Contracts;
using Ardan.Core.Layout;
using Microsoft.EntityFrameworkCore;

namespace Ardan.Api.Endpoints;

/// <summary>The HTTP API. Reads return views; commands return a sentence the operator can read.</summary>
public static class StoreEndpoints
{
    private const int AlarmHistorySize = 100;

    public static void MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/layout", (StoreHost store) => store.Read(ViewBuilder.Layout));
        api.MapGet("/snapshot", (StoreHost store) => store.Read(store.Snapshots.Current));
        api.MapGet("/lanes", (StoreHost store) => store.Read(ViewBuilder.Lanes));
        api.MapGet("/lanes/{level:int}/{bay:int}", (int level, int bay, StoreHost store) =>
            store.Read(w => ViewBuilder.LaneDetail(w, new LaneId(level, bay))) is { } lane
                ? Results.Ok(lane)
                : Results.NotFound());

        api.MapGet("/orders", (string? view, StoreHost store) => store.Read(w => ViewBuilder.Orders(w, view)));
        api.MapPost("/orders", (NewOrder order, StoreHost store) => Reply(store.Run(w => w.CreateOrder(order))));
        api.MapPost("/orders/{id}/cancel", (string id, StoreHost store) => Reply(store.Run(w => w.CancelOrder(id))));
        api.MapPost("/orders/{id}/priority", (string id, PriorityRequest request, StoreHost store) =>
            Reply(store.Run(w => w.SetPriority(id, request.Priority))));

        api.MapGet("/skus", (StoreHost store) => store.Read(ViewBuilder.Skus));
        api.MapGet("/skus/{code}/pallets", (string code, StoreHost store) => store.Read(w => ViewBuilder.PalletsOf(w, code)));
        api.MapGet("/pallets/{id}", PalletAsync);

        api.MapGet("/alarms", AlarmsAsync);
        api.MapPost("/alarms/{id}/acknowledge", (string id, StoreHost store) => Reply(store.Run(w => w.Acknowledge(id))));
        api.MapPost("/alarms/{id}/action", (string id, StoreHost store) => Reply(store.Run(w => w.RunAlarmAction(id))));

        api.MapPost("/taxis/{id}/{command}", (string id, string command, StoreHost store) => Reply(store.Run(w => command switch
        {
            "charge" => w.SendToCharge(id),
            "park" => w.SetInService(id, false),
            "resume" => w.SetInService(id, true),
            "reset" => w.ResetTaxi(id),
            _ => CommandResult.Failure($"Unknown command '{command}'."),
        })));
        api.MapPost("/lifts/{id}/reset", (string id, StoreHost store) => Reply(store.Run(w => w.ResetLift(id))));
        api.MapPost("/conveyors/{id}/reset", (string id, StoreHost store) => Reply(store.Run(w => w.ResetConveyor(id))));
        api.MapPost("/simulation/speed", (SpeedRequest request, StoreHost store) => Reply(store.Run(w => w.SetSpeed(request.Speed))));

        api.MapGet("/search", (string? q, StoreHost store) => store.Read(w => ViewBuilder.Search(w, q)));
    }

    private static async Task<IResult> PalletAsync(string id, StoreHost store, IDbContextFactory<ArdanDb> dbFactory)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var history = (await db.Movements.AsNoTracking()
                .Where(row => row.PalletId == id)
                .OrderByDescending(row => row.Time)
                .ToListAsync())
            .Select(row => row.ToView())
            .ToList();

        var pallet = store.Read(w => ViewBuilder.FindPallet(w, id));
        if (pallet is null)
        {
            var row = await db.Pallets.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (row is null) return Results.NotFound();
            pallet = row.ToView(store.Read(w => w.NameOf(row.SkuCode)));
        }

        return Results.Ok(new PalletDetail(pallet, history));
    }

    private static async Task<IResult> AlarmsAsync(string? view, StoreHost store, IDbContextFactory<ArdanDb> dbFactory)
    {
        if (view != "history")
        {
            return Results.Ok(store.Read(w => w.Alarms.Active
                .OrderByDescending(alarm => alarm.Severity)
                .ThenByDescending(alarm => alarm.RaisedAt)
                .Select(ViewBuilder.ToView)
                .ToList()));
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var rows = await db.Alarms.AsNoTracking()
            .Where(row => row.State == AlarmState.Cleared)
            .OrderByDescending(row => row.RaisedAt)
            .Take(AlarmHistorySize)
            .ToListAsync();
        return Results.Ok(rows.Select(row => row.ToView()).ToList());
    }

    private static IResult Reply(CommandResult result) =>
        Results.Json(result, statusCode: result.Ok ? StatusCodes.Status200OK : StatusCodes.Status409Conflict);
}
