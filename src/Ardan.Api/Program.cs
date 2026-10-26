using Ardan.Api.Data;
using Ardan.Api.Endpoints;
using Ardan.Api.Live;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var dataFolder = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataFolder);

builder.Services.AddDbContextFactory<ArdanDb>(options =>
    options.UseSqlite($"Data Source={Path.Combine(dataFolder, "ardan.db")}"));
builder.Services.AddSingleton<DbJournal>();
builder.Services.AddSingleton<StoreHost>();
builder.Services.AddHostedService<JournalWriter>();
builder.Services.AddHostedService<SimulationLoop>();
builder.Services.AddHostedService<LiveBroadcaster>();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

var app = builder.Build();

await app.Services.GetRequiredService<StoreHost>().StartAsync();

app.MapOpenApi();
app.MapHub<LiveHub>("/hubs/live");
app.MapStoreEndpoints();
app.MapGet("/", () => Results.Redirect("/openapi/v1.json")).ExcludeFromDescription();

app.Run();
