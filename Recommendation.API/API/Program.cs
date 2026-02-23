using Recommendation.API.Application.Algorithms;
using Recommendation.API.Application.Interfaces;
using Recommendation.API.Application.Services;
using Recommendation.API.Infrastructure.Data;
using Recommendation.API.Infrastructure.ExternalServices;
using Recommendation.API.Infrastructure.Messaging.Consumers;
using Recommendation.API.Infrastructure.Repositories;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

var neo4jSettings = new Neo4jSettings
{
    Uri      = builder.Configuration["Neo4jSettings:Uri"]      ?? "bolt://localhost:7687",
    Username = builder.Configuration["Neo4jSettings:Username"] ?? "neo4j",
    Password = builder.Configuration["Neo4jSettings:Password"] ?? "password123"
};

var rabbitSettings = new RabbitMqConsumerSettings
{
    HostName = builder.Configuration["RabbitMQ:HostName"] ?? "localhost",
    UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
    Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
};

var productServiceBaseUrl = builder.Configuration["ProductService:BaseUrl"]
                            ?? "http://localhost:8001";

builder.Services.AddSingleton(neo4jSettings);
builder.Services.AddSingleton<Neo4jContext>();

builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();

builder.Services.AddHttpClient<IProductServiceClient, ProductServiceClient>(client =>
{
    client.BaseAddress = new Uri(productServiceBaseUrl);
});

builder.Services.AddScoped<CollaborativeFilteringAlgorithm>();
builder.Services.AddScoped<ContentBasedFilteringAlgorithm>();
builder.Services.AddScoped<ScoreCalculator>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddHostedService<EventConsumerService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Recommendation Service API",
        Version = "v1",
        Description = "Marketplace Recommendation Microservice - Powered by Neo4j"
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
