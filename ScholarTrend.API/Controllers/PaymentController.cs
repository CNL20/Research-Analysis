using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScholarTrend.Application.DTOs.Payment;
using ScholarTrend.Application.Interfaces.Services;
using System.Security.Claims;

namespace ScholarTrend.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ISubscriptionService _subscriptionService;

    public PaymentController(IPaymentService paymentService, ISubscriptionService subscriptionService)
    {
        _paymentService = paymentService;
        _subscriptionService = subscriptionService;
    }

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _subscriptionService.GetPlansAsync();
        return Ok(plans);
    }

    [Authorize]
    [HttpPost("checkout")]
    public async Task<IActionResult> Checkout([FromBody] CreateCheckoutRequestDto request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _paymentService.CreateCheckoutUrlAsync(userId, request.PlanId, request.CancelUrl, request.ReturnUrl);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] PayOS.Models.Webhooks.Webhook body)
    {
        // PayOS sends the payload to this endpoint
        var success = await _paymentService.HandleWebhookAsync(body);

        if (success)
        {
            return Ok(new { success = true });
        }
        
        // Return 200 OK even on failure to acknowledge receipt (as per PayOS best practice)
        return Ok(new { success = false });
    }

    [Authorize]
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var history = await _paymentService.GetUserTransactionHistoryAsync(userId);
        return Ok(history);
    }
}
