using Product.API.Application.Interfaces;
using Product.API.Application.Services;
using Product.API.Infrastructure.Data;
using Product.API.Infrastructure.Messaging;
using Product.API.Infrastructure.Repositories;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var mongoSettings = new MongoDbSettings
{
    ConnectionString = builder.Configuration["MongoDbSettings:ConnectionString"]
                       ?? "mongodb://admin:admin123@localhost:27017",
    DatabaseName = builder.Configuration["MongoDbSettings:DatabaseName"] ?? "productdb"
};

var rabbitSettings = new RabbitMqSettings
{
    HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost",
    UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
};

builder.Services.AddSingleton(mongoSettings);
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<IProductService, ProductService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Product Service API",
        Version = "v1",
        Description = "Marketplace Product Microservice"
    });
});

builder.Services.AddHealthChecks();
builder.Services.AddCors(options =>
    options.AddPolicy("AllowAll", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
