using MongoDB.Driver;
using Order.API.Domain.Entities;
using Order.API.Domain.Enums;

namespace Order.API.Infrastructure.Data;

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
                db.ListCollectionNames().FirstOrDefault();
                Console.WriteLine($"[Order.API] MongoDB connected to '{settings.DatabaseName}'");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Order.API] MongoDB attempt {i}/6 failed: {ex.Message}. Retrying in 5s...");
                if (i == 6) throw;
                Thread.Sleep(5000);
            }
        }
        _database = db!;
        TryCreateIndexes();
    }

    public IMongoCollection<CustomerOrder> Orders =>
        _database.GetCollection<CustomerOrder>("orders");

    private void TryCreateIndexes()
    {
        try
        {
            Orders.Indexes.CreateOne(new CreateIndexModel<CustomerOrder>(
                Builders<CustomerOrder>.IndexKeys.Ascending(o => o.UserId)));
            Orders.Indexes.CreateOne(new CreateIndexModel<CustomerOrder>(
                Builders<CustomerOrder>.IndexKeys.Ascending(o => o.Status)));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Order.API] Index warning (non-fatal): {ex.Message}");
        }
    }
}
