using MongoDB.Driver;
using Product.API.Domain.Entities;

namespace Product.API.Infrastructure.Data;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(MongoDbSettings settings)
    {
        IMongoDatabase? db = null;
        for (var i = 1; i <= 6; i++)
        {
            try
            {
                var client = new MongoClient(settings.ConnectionString);
                db = client.GetDatabase(settings.DatabaseName);
                // force connection check
                db.ListCollectionNames().FirstOrDefault();
                Console.WriteLine($"[Product.API] MongoDB connected to '{settings.DatabaseName}'");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Product.API] MongoDB attempt {i}/6 failed: {ex.Message}. Retrying in 5s...");
                if (i == 6) throw;
                Thread.Sleep(5000);
            }
        }
        _database = db!;
        TryCreateIndexes();
    }

    public IMongoCollection<Domain.Entities.Product> Products =>
        _database.GetCollection<Domain.Entities.Product>("products");

    public IMongoCollection<Category> Categories =>
        _database.GetCollection<Category>("categories");

    private void TryCreateIndexes()
    {
        try
        {
            Products.Indexes.CreateOne(new CreateIndexModel<Domain.Entities.Product>(
                Builders<Domain.Entities.Product>.IndexKeys
                .Ascending(p => p.Name)
                .Ascending(p => p.Category),
                new CreateIndexOptions { Unique = true }));
            Categories.Indexes.CreateOne(new CreateIndexModel<Category>(
                Builders<Category>.IndexKeys.Ascending(c => c.Name),
                new CreateIndexOptions { Unique = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Product.API] Index warning (non-fatal): {ex.Message}");
        }
    }
}
