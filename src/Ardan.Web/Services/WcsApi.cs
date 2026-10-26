using Ardan.Core.Contracts;
using Ardan.Core.Orders;

namespace Ardan.Web.Services;

/// <summary>Typed client for the control server's HTTP API.</summary>
public sealed class WcsApi(HttpClient http)
{
    private const string Unreachable = "Can't reach the control server. Check that the API is running.";

    public Task<LayoutView?> LayoutAsync() => GetAsync<LayoutView>("api/layout");

    public Task<IReadOnlyList<LaneView>> LanesAsync() => ListAsync<LaneView>("api/lanes");

    public Task<LaneDetail?> LaneAsync(int level, int bay) => GetAsync<LaneDetail>($"api/lanes/{level}/{bay}");

    public Task<IReadOnlyList<OrderView>> OrdersAsync(string view) => ListAsync<OrderView>($"api/orders?view={view}");

    public Task<IReadOnlyList<SkuView>> SkusAsync() => ListAsync<SkuView>("api/skus");

    public Task<IReadOnlyList<PalletView>> PalletsOfAsync(string skuCode) =>
        ListAsync<PalletView>($"api/skus/{Uri.EscapeDataString(skuCode)}/pallets");

    public Task<PalletDetail?> PalletAsync(string id) => GetAsync<PalletDetail>($"api/pallets/{Uri.EscapeDataString(id)}");

    public Task<IReadOnlyList<AlarmView>> AlarmHistoryAsync() => ListAsync<AlarmView>("api/alarms?view=history");

    public Task<IReadOnlyList<SearchHit>> SearchAsync(string query) =>
        ListAsync<SearchHit>($"api/search?q={Uri.EscapeDataString(query)}");

    public Task<CommandResult> CreateOrderAsync(NewOrder order) => PostAsync("api/orders", order);

    public Task<CommandResult> CancelOrderAsync(string id) => PostAsync($"api/orders/{id}/cancel");

    public Task<CommandResult> SetPriorityAsync(string id, Priority priority) =>
        PostAsync($"api/orders/{id}/priority", new PriorityRequest(priority));

    public Task<CommandResult> TaxiAsync(string id, string command) => PostAsync($"api/taxis/{id}/{command}");

    public Task<CommandResult> ResetLiftAsync(string id) => PostAsync($"api/lifts/{id}/reset");

    public Task<CommandResult> ResetConveyorAsync(string id) => PostAsync($"api/conveyors/{id}/reset");

    public Task<CommandResult> AcknowledgeAsync(string id) => PostAsync($"api/alarms/{id}/acknowledge");

    public Task<CommandResult> AlarmActionAsync(string id) => PostAsync($"api/alarms/{id}/action");

    public Task<CommandResult> SetSpeedAsync(double speed) => PostAsync("api/simulation/speed", new SpeedRequest(speed));

    private async Task<T?> GetAsync<T>(string path) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path);
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<T>() : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<T>> ListAsync<T>(string path) => await GetAsync<List<T>>(path) ?? [];

    private async Task<CommandResult> PostAsync(string path, object? body = null)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(path, body ?? new { });
            return await response.Content.ReadFromJsonAsync<CommandResult>()
                ?? CommandResult.Failure("The control server sent an empty reply.");
        }
        catch (HttpRequestException)
        {
            return CommandResult.Failure(Unreachable);
        }
    }
}
