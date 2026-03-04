using Belote.Server.Hubs;
using Belote.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddSingleton<TableManager>();
builder.Services.AddHostedService<TableRunnerService>();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapHub<TableHub>("/hubs/table");
    endpoints.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
    endpoints.MapFallbackToFile("index.html");
});

app.Run();
