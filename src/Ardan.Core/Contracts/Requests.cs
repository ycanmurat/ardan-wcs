using Ardan.Core.Orders;

namespace Ardan.Core.Contracts;

/// <summary>The answer to an operator command, in a sentence the screen can show as it is.</summary>
public sealed record CommandResult(bool Ok, string Message, string? Ref = null)
{
    public static CommandResult Success(string message, string? reference = null) => new(true, message, reference);

    public static CommandResult Failure(string message) => new(false, message);
}

public sealed record NewOrder(OrderType Type, string SkuCode, int Quantity, int Dock, Priority Priority, int DueInMinutes);

public sealed record SpeedRequest(double Speed);

public sealed record PriorityRequest(Priority Priority);
