var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("AllowAll");
app.MapReverseProxy();
app.MapHealthChecks("/health");

// Gateway info endpoint
app.MapGet("/", () => new
{
    service = "Marketplace API Gateway",
    version = "1.0",
    routes = new[]
    {
        "GET/POST/PUT/DELETE /api/products",
        "GET/POST/PUT/DELETE /api/orders",
        "GET/POST /api/recommendations",
        "GET/POST /api/categories"
    }
});

app.Run();
