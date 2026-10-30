using ChannelSurfer.Components;
using ChannelSurfer.Proxy;
using ChannelSurfer.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Config volume (container sets CONFIG_DIR=/config; dev falls back to ./config).
var configDir = Environment.GetEnvironmentVariable("CONFIG_DIR")
    ?? Path.Combine(builder.Environment.ContentRootPath, "config");
// Persist data-protection keys on the volume so Blazor circuits survive container restarts.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(configDir, "keys")));
builder.Services.AddSingleton(new SettingsService(configDir));
builder.Services.AddSingleton<HttpClient>();
builder.Services.AddSingleton<SourceFetcher>();
builder.Services.AddSingleton<ContentService>();
builder.Services.AddHostedService<ContentRefresher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapProxyEndpoints();

app.Run();

public partial class Program { }
