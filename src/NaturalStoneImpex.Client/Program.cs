using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NaturalStoneImpex.Client.Auth;
using NaturalStoneImpex.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Same-origin API calls: the HttpOnly admin cookie travels automatically;
// CsrfHeaderHandler adds the header the server requires on state-changing calls.
builder.Services.AddTransient<CsrfHeaderHandler>();

builder.Services.AddHttpClient("NaturalStoneImpex.Api", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
}).AddHttpMessageHandler<CsrfHeaderHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("NaturalStoneImpex.Api"));

builder.Services.AddScoped<CookieAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CookieAuthStateProvider>());

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IVisualizerService, VisualizerService>();
builder.Services.AddScoped<ICartApi, CartApi>();

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

await builder.Build().RunAsync();
