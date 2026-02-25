using Order.API.Application.Interfaces;
using Order.API.Application.Services;
using Order.API.Infrastructure.Data;
using Order.API.Infrastructure.ExternalServices;
using Order.API.Infrastructure.Messaging;
using Order.API.Infrastructure.Repositories;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var mongoSettings = new MongoDbSettings
{
    ConnectionString = builder.Configuration["MongoDbSettings:ConnectionString"]
                       ?? "mongodb://admin:admin123@localhost:27018",
    DatabaseName = builder.Configuration["MongoDbSettings:DatabaseName"] ?? "orderdb"
};

var rabbitSettings = new RabbitMqSettings
{
    HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost",
    UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
};

var productServiceBaseUrl = builder.Configuration["ProductService:BaseUrl"]
                            ?? "http://localhost:8001";

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    client.BaseAddress = new Uri(productServiceBaseUrl);
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Order Service API",
        Version = "v1",
        Description = "Marketplace Order Microservice"
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// Force Singletons to initialize at startup (not lazily on first request)
app.Services.GetRequiredService<MongoDbContext>();
app.Services.GetRequiredService<IEventPublisher>();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();