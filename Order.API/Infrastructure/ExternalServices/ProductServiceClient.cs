using System.Text;
using System.Text.Json;
using Order.API.Application.Interfaces;

namespace Order.API.Infrastructure.ExternalServices;

public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductServiceClient> _logger;

    public ProductServiceClient(HttpClient httpClient, ILogger<ProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> CheckStockAsync(string productId, int quantity)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/products/{productId}");
            if (!response.IsSuccessStatusCode) return false;

            var content = await response.Content.ReadAsStringAsync();
            var product = JsonSerializer.Deserialize<ProductResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return product?.Stock >= quantity;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking stock for product {ProductId}", productId);
            return false;
        }
    }

    public async Task<bool> DecrementStockAsync(string productId, int quantity)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new { Quantity = quantity });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"/api/products/{productId}/decrement-stock", content);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrementing stock for product {ProductId}", productId);
            return false;
        }
    }

    private class ProductResponse
    {
        public string Id { get; set; } = string.Empty;
        public int Stock { get; set; }
    }
}
