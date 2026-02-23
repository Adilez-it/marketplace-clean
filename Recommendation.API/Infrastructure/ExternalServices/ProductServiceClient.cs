using System.Text;
using System.Text.Json;
using Recommendation.API.Application.DTOs;
using Recommendation.API.Application.Interfaces;

namespace Recommendation.API.Infrastructure.ExternalServices;

public class ProductServiceClient : IProductServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductServiceClient> _logger;

    public ProductServiceClient(HttpClient httpClient, ILogger<ProductServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<ProductDetailsDto>> GetProductsByIdsAsync(IEnumerable<string> ids)
    {
        var idList = ids.ToList();
        if (!idList.Any()) return new List<ProductDetailsDto>();

        try
        {
            var payload = JsonSerializer.Serialize(idList);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("/api/products/batch", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get products by IDs: {Status}", response.StatusCode);
                return new List<ProductDetailsDto>();
            }

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ProductDetailsDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products by IDs");
            return new List<ProductDetailsDto>();
        }
    }

    public async Task<ProductDetailsDto?> GetProductByIdAsync(string id)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/products/{id}");
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<ProductDetailsDto>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product {ProductId}", id);
            return null;
        }
    }
}
