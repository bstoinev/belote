using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Belote.Client;
using Belote.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddLocalization();
builder.Services.AddScoped<CultureService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<TableHubClient>();

var host = builder.Build();
await host.Services.GetRequiredService<CultureService>().InitializeAsync();
await host.RunAsync();
