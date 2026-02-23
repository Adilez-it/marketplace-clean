using Neo4j.Driver;

namespace Recommendation.API.Infrastructure.Data;

public class Neo4jSettings
{
    public string Uri { get; set; } = "bolt://localhost:7687";
    public string Username { get; set; } = "neo4j";
    public string Password { get; set; } = "password123";
}

public class Neo4jContext : IDisposable
{
    private readonly IDriver _driver;

    public Neo4jContext(Neo4jSettings settings)
    {
        IDriver? driver = null;
        for (var i = 1; i <= 8; i++)
        {
            try
            {
                driver = GraphDatabase.Driver(
                    settings.Uri,
                    AuthTokens.Basic(settings.Username, settings.Password));
                // Verify connectivity
                driver.VerifyConnectivityAsync().GetAwaiter().GetResult();
                Console.WriteLine($"[Recommendation.API] Neo4j connected to {settings.Uri}");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Recommendation.API] Neo4j attempt {i}/8 failed: {ex.Message}. Retrying in 7s...");
                driver?.Dispose();
                driver = null;
                if (i == 8) throw;
                Thread.Sleep(7000);
            }
        }
        _driver = driver!;
        TryInitConstraints();
    }

    private void TryInitConstraints()
    {
        try
        {
            using var session = _driver.AsyncSession();
            session.ExecuteWriteAsync(async tx =>
            {
                await tx.RunAsync("CREATE CONSTRAINT user_id IF NOT EXISTS FOR (u:User) REQUIRE u.userId IS UNIQUE");
                await tx.RunAsync("CREATE CONSTRAINT product_id IF NOT EXISTS FOR (p:Product) REQUIRE p.productId IS UNIQUE");
            }).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Recommendation.API] Constraint warning (non-fatal): {ex.Message}");
        }
    }

    public IAsyncSession GetSession() => _driver.AsyncSession();

    public async Task<List<T>> ExecuteReadAsync<T>(
        string query,
        object parameters,
        Func<IRecord, T> mapper)
    {
        await using var session = GetSession();
        return await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(query, parameters);
            return await cursor.ToListAsync(mapper);
        });
    }

    public async Task ExecuteWriteAsync(string query, object parameters)
    {
        await using var session = GetSession();
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(query, parameters);
        });
    }

    public void Dispose() => _driver?.Dispose();
}
