using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NaturalStoneImpex.Client;
using NaturalStoneImpex.Client.Auth;
using NaturalStoneImpex.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<CustomAuthStateProvider>();
builder.Services.AddSingleton<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthStateProvider>());

builder.Services.AddTransient<AuthTokenHandler>();

builder.Services.AddHttpClient("NaturalStoneImpex.Api", client =>
{
    client.BaseAddress = new Uri("https://localhost:5001/");
}).AddHttpMessageHandler<AuthTokenHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("NaturalStoneImpex.Api"));

builder.Services.AddSingleton<CartService>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
