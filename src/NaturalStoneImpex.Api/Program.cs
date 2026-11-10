using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NaturalStoneImpex.Api.Auth;
using NaturalStoneImpex.Api.Components;
using NaturalStoneImpex.Api.Data;
using NaturalStoneImpex.Api.Data.Seed;
using NaturalStoneImpex.Api.Endpoints;
using NaturalStoneImpex.Api.Middleware;
using NaturalStoneImpex.Api.Models.Entities;
using NaturalStoneImpex.Api.Services;
using NaturalStoneImpex.Api.Services.Segmentation;

var builder = WebApplication.CreateBuilder(args);

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Authentication: admin cookie for the browser, JWT Bearer for API clients and Swagger.
// The policy scheme picks Bearer when an "Authorization: Bearer ..." header is present.
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey is not configured.");

builder.Services.AddAuthentication(AuthSchemes.Policy)
.AddPolicyScheme(AuthSchemes.Policy, null, options =>
    options.ForwardDefaultSelector = AuthSchemes.Select)
.AddCookie(AuthSchemes.Cookie, options =>
{
    options.Cookie.Name = "nsi_admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.LoginPath = "/admin/login";
    options.AccessDeniedPath = "/admin/login";

    // API calls get a status code, not a redirect to the login page.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
})
.AddJwtBearer(AuthSchemes.Bearer, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<AdminUser>, PasswordHasher<AdminUser>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();

// Visualizer (see docs/visualizer-specification.md)
builder.Services.Configure<VisualizerOptions>(builder.Configuration.GetSection("Visualizer"));
builder.Services.AddMemoryCache(options => options.SizeLimit = 16); // embeddings are ~4 MB each
builder.Services.AddSingleton<EncodeGate>();
builder.Services.AddSingleton<ISamModel>(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VisualizerOptions>>().Value;
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new SamOnnxModel(
        Path.Combine(env.ContentRootPath, options.EncoderPath),
        Path.Combine(env.ContentRootPath, options.DecoderPath));
});
builder.Services.AddScoped<ISegmentationService, SegmentationService>();
builder.Services.AddHostedService<VisualizationRequestCleanupService>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<NaturalStoneImpex.Api.Storefront.ICartStore, NaturalStoneImpex.Api.Storefront.CartStore>();
builder.Services.AddScoped<NaturalStoneImpex.Api.Storefront.IStorefrontQueryService, NaturalStoneImpex.Api.Storefront.StorefrontQueryService>();
builder.Services.AddSingleton<NaturalStoneImpex.Api.Storefront.CheckoutGuard>();
builder.Services.AddScoped<NaturalStoneImpex.Api.Storefront.CheckoutHandler>();

builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply pending migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, app.Environment.WebRootPath);
}

// Errors: /api keeps its JSON middleware; everything else gets the designed error and not-found pages.
app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/api"),
    b => b.UseMiddleware<ExceptionHandlingMiddleware>());
app.UseWhen(ctx => !ctx.Request.Path.StartsWithSegments("/api"), b =>
{
    // Development keeps the developer exception page; verify the designed /error with ASPNETCORE_ENVIRONMENT=Production.
    if (!app.Environment.IsDevelopment())
        b.UseExceptionHandler("/error", createScopeForErrors: true);
    b.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    b.Use((ctx, next) =>
    {
        NaturalStoneImpex.Api.Storefront.ErrorPages.Prepare(ctx);
        return next(ctx);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // runtime uploads (/uploads)
// Explicit, and after the error branch: re-executing /error and /not-found must route the new path.
// (The implicit UseRouting runs first in the pipeline, before the branch, so re-execution would find no endpoint.)
app.UseRouting();
app.UseAuthentication();
app.UseMiddleware<AdminCsrfHeaderMiddleware>();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(NaturalStoneImpex.Client._Imports).Assembly);

app.MapControllers();
app.MapCartEndpoints();

// Health check endpoint
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    timestamp = DateTime.UtcNow
}));

app.Run();
