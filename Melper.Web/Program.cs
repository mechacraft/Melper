using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using Melper.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var host = builder.Build();

// Applied before the first render: pages read UnitsCollection.Units in their field
// initializers, so anything later would let a component start on the shipped roster.
await UnitsStorage.LoadAsync(host.Services.GetRequiredService<IJSRuntime>());

await host.RunAsync();
