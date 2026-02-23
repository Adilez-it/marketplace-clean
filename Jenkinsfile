// Program.cs ou Startup.cs
builder.Services.AddHealthChecks()
    .AddMongoDb(
        mongodbConnectionString: builder.Configuration.GetConnectionString("MongoDb"),
        name: "mongodb",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "db", "mongodb" })
    .AddRabbitMQ(
        rabbitConnectionString: builder.Configuration.GetConnectionString("RabbitMQ"),
        name: "rabbitmq",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "mq", "rabbitmq" });

// Pour Recommendation API uniquement
if (builder.Environment.EnvironmentName == "Recommendation")
{
    builder.Services.AddHealthChecks()
        .AddNeo4j(
            neo4jConnectionString: builder.Configuration.GetConnectionString("Neo4j"),
            name: "neo4j",
            timeout: TimeSpan.FromSeconds(3),
            tags: new[] { "db", "neo4j" });
}

// Endpoint health check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            Status = report.Status.ToString(),
            Checks = report.Entries.Select(x => new
            {
                Component = x.Key,
                Status = x.Value.Status.ToString(),
                Description = x.Value.Description,
                Duration = x.Value.Duration
            }),
            TotalDuration = report.TotalDuration
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});