using System.Globalization;
using MEMS.Public.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// The site is in English: format dates as "Saturday, October 17, 2026" whatever the browser language.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-US");

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Same-origin HttpClient: /api/* is forwarded by the MEMS.Public host to MEMS.Api through Aspire
// service discovery (the browser itself cannot resolve "https+http://api").
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<MemsApiClient>();

// Voting guide and comparison: browser sessionStorage only (never sent to the server).
builder.Services.AddScoped<VotingGuideStore>();
builder.Services.AddScoped<CompareStore>();

await builder.Build().RunAsync();
