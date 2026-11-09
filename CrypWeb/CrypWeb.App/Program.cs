using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using CrypWeb.App;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<Localizer>();

var host = builder.Build();
await host.Services.GetRequiredService<Localizer>().InitAsync();
try
{
    var xi = await host.Services.GetRequiredService<HttpClient>().GetStringAsync("xaml/index.json");
    CrypTool.Web.App.Visualizers.XamlRegistry.SetKnown(System.Text.Json.JsonDocument.Parse(xi).RootElement.EnumerateObject().Select(p => p.Name));
}
catch { }
await host.RunAsync();
