using Microsoft.AspNetCore.Mvc;
using Recommendation.API.Application.DTOs;
using Recommendation.API.Application.Interfaces;
using Recommendation.API.Application.Services;

namespace Recommendation.API.API.Controllers;

[ApiController]
[Route("api/recommendations")]
public class RecommendationController : ControllerBase
{
    private readonly IRecommendationService _service;
    private readonly ILogger<RecommendationController> _logger;

    public RecommendationController(IRecommendationService service, ILogger<RecommendationController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetPersonalized(string userId, [FromQuery] int limit = 10)
    {
        var recommendations = await _service.GetPersonalizedAsync(userId, limit);
        return Ok(recommendations);
    }

    [HttpGet("similar/{productId}")]
    public async Task<IActionResult> GetSimilar(string productId, [FromQuery] int limit = 5)
    {
        var similar = await _service.GetSimilarProductsAsync(productId, limit);
        return Ok(similar);
    }

    [HttpGet("trending")]
    public async Task<IActionResult> GetTrending([FromQuery] int days = 7, [FromQuery] int limit = 10)
    {
        var trending = await _service.GetTrendingAsync(days, limit);
        return Ok(trending);
    }

    [HttpGet("history/{userId}")]
    public async Task<IActionResult> GetHistory(string userId)
    {
        var history = await _service.GetUserHistoryAsync(userId);
        return Ok(new { UserId = userId, ProductIds = history });
    }

    [HttpPost("view")]
    public async Task<IActionResult> RecordView([FromBody] RecordViewDto dto)
    {
        await _service.RecordViewAsync(dto.UserId, dto.ProductId);
        return Ok(new { message = "View recorded" });
    }

    [HttpPost("purchase")]
    public async Task<IActionResult> RecordPurchase([FromBody] RecordPurchaseDto dto)
    {
        await _service.RecordPurchaseAsync(dto.UserId, dto.OrderId, dto.Items);
        return Ok(new { message = "Purchase recorded" });
    }
}
