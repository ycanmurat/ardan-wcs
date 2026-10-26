using System.Globalization;
using Ardan.Web.Components;
using Ardan.Web.Services;

var culture = CultureInfo.GetCultureInfo("en-IE");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

var apiBase = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5080/");
builder.Services.AddHttpClient<WcsApi>(client => client.BaseAddress = apiBase);
builder.Services.AddSingleton<LiveFeed>();
builder.Services.AddHostedService(services => services.GetRequiredService<LiveFeed>());
builder.Services.AddScoped<UiState>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

// Support the portfolio subpath while retaining root hosting for local runs.
app.UsePathBase("/ardan-wcs");
app.UseRouting();

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/error", createScopeForErrors: true);
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
