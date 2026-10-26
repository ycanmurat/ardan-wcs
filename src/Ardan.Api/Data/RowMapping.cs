using Ardan.Core.Alarms;
using Ardan.Core.Contracts;
using Ardan.Core.Engine;
using Ardan.Core.Layout;
using Ardan.Core.Orders;
using Ardan.Core.Stock;

namespace Ardan.Api.Data;

/// <summary>Converts between database rows and the live model.</summary>
public static class RowMapping
{
    private const char StepSeparator = '\n';

    public static SkuRow ToRow(this Sku sku) => new()
    {
        Code = sku.Code,
        Name = sku.Name,
        Category = sku.Category,
        Velocity = sku.Velocity,
        CasesPerPallet = sku.CasesPerPallet,
        ShelfLifeDays = sku.ShelfLifeDays,
    };

    public static Sku ToSku(this SkuRow row) =>
        new(row.Code, row.Name, row.Category, row.Velocity, row.CasesPerPallet, row.ShelfLifeDays);

    public static PalletRow ToRow(this Pallet pallet) => new()
    {
        Id = pallet.Id,
        SkuCode = pallet.SkuCode,
        Batch = pallet.Batch,
        Expiry = pallet.Expiry,
        WeightKg = pallet.WeightKg,
        HeightMm = pallet.HeightMm,
        Status = pallet.Status,
        Level = pallet.Status == PalletStatus.Stored ? pallet.Lane?.Level : null,
        Bay = pallet.Status == PalletStatus.Stored ? pallet.Lane?.Bay : null,
        Slot = pallet.Slot,
        OrderId = pallet.OrderId,
        ReceivedAt = pallet.ReceivedAt,
    };

    public static Pallet ToPallet(this PalletRow row) => new()
    {
        Id = row.Id,
        SkuCode = row.SkuCode,
        Batch = row.Batch,
        Expiry = row.Expiry,
        WeightKg = row.WeightKg,
        HeightMm = row.HeightMm,
        Status = row.Status,
        Lane = row is { Level: { } level, Bay: { } bay } ? new LaneId(level, bay) : null,
        Slot = row.Slot,
        OrderId = row.OrderId,
        ReceivedAt = row.ReceivedAt,
    };

    public static PalletView ToView(this PalletRow row, string skuName) => new(
        row.Id,
        row.SkuCode,
        skuName,
        row.Batch,
        row.Expiry,
        row.WeightKg,
        row.HeightMm,
        row.Status,
        row.Status switch
        {
            PalletStatus.Dispatched => "Dispatched",
            PalletStatus.Returned => "Returned to the supplier",
            PalletStatus.Rejected => "Reject lane",
            _ => "In transit when the system last stopped",
        },
        row.OrderId,
        row.ReceivedAt);

    public static OrderRow ToRow(this Order order) => new()
    {
        Id = order.Id,
        Type = order.Type,
        Reference = order.Reference,
        SkuCode = order.SkuCode,
        Quantity = order.Quantity,
        Dock = order.Dock,
        Priority = order.Priority,
        Status = order.Status,
        Source = order.Source,
        CreatedAt = order.CreatedAt,
        DueAt = order.DueAt,
        CompletedAt = order.CompletedAt,
        Batch = order.Batch,
        Expiry = order.Expiry,
        Done = order.Done,
        Returned = order.Returned,
        Note = order.Note,
    };

    /// <summary>Restores an open order. Pallets that were moving when the system stopped are planned again.</summary>
    public static Order ToOpenOrder(this OrderRow row) => new()
    {
        Id = row.Id,
        Type = row.Type,
        Reference = row.Reference,
        SkuCode = row.SkuCode,
        Quantity = row.Quantity,
        Dock = row.Dock,
        Priority = row.Priority,
        Status = row.Status == OrderStatus.Exception ? OrderStatus.InProgress : row.Status,
        Source = row.Source,
        CreatedAt = row.CreatedAt,
        DueAt = row.DueAt,
        Batch = row.Batch,
        Expiry = row.Expiry,
        Done = row.Done,
        Returned = row.Returned,
        Arrived = row.Type == OrderType.Inbound ? row.Done + row.Returned : 0,
        Note = row.Status == OrderStatus.Exception ? null : row.Note,
    };

    public static MovementRow ToRow(this Movement movement) => new()
    {
        PalletId = movement.PalletId,
        Time = movement.Time,
        Kind = movement.Kind,
        From = movement.From,
        To = movement.To,
        By = movement.By,
    };

    public static MovementView ToView(this MovementRow row) => new(row.Time, row.Kind, row.From, row.To, row.By);

    public static AlarmRow ToRow(this Alarm alarm) => new()
    {
        Id = alarm.Id,
        Code = alarm.Code,
        Severity = alarm.Severity,
        Source = alarm.Source,
        Title = alarm.Title,
        What = alarm.What,
        Cause = alarm.Cause,
        Steps = string.Join(StepSeparator, alarm.Steps),
        Action = alarm.Action,
        ActionLabel = alarm.ActionLabel,
        Subject = alarm.Subject,
        RaisedAt = alarm.RaisedAt,
        State = alarm.State,
        AcknowledgedAt = alarm.AcknowledgedAt,
        ClearedAt = alarm.ClearedAt,
        ClearedBy = alarm.ClearedBy,
    };

    public static AlarmView ToView(this AlarmRow row) => new(
        row.Id,
        row.Code,
        row.Severity,
        row.State,
        row.Source,
        row.Title,
        row.What,
        row.Cause,
        row.Steps.Split(StepSeparator, StringSplitOptions.RemoveEmptyEntries),
        row.Action,
        row.ActionLabel,
        row.RaisedAt,
        row.AcknowledgedAt,
        row.ClearedAt,
        row.ClearedBy);
}
