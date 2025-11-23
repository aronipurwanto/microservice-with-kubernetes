using Microsoft.AspNetCore.Mvc;
using PaymentService.Services;
using SharedModels.Models;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(IPaymentService paymentService, ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<PaymentResponseDto>>> GetPayments(
        [FromQuery] PaymentStatus? status = null)
    {
        try
        {
            List<PaymentResponseDto> payments;
            
            if (status.HasValue)
            {
                payments = await _paymentService.GetPaymentsByStatusAsync(status.Value);
            }
            else
            {
                // For simplicity, return empty list when no status filter
                // In real scenario, you might want to implement pagination for all payments
                payments = new List<PaymentResponseDto>();
            }
            
            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payments");
            return StatusCode(500, "An error occurred while retrieving payments");
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PaymentResponseDto>> GetPayment(Guid id)
    {
        try
        {
            var payment = await _paymentService.GetPaymentAsync(id);
            if (payment == null)
            {
                return NotFound($"Payment with ID {id} not found");
            }
            return Ok(payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment with ID: {PaymentId}", id);
            return StatusCode(500, "An error occurred while retrieving the payment");
        }
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponseDto>> ProcessPayment(PaymentCreateDto paymentDto)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var payment = await _paymentService.ProcessPaymentAsync(paymentDto);
            return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment");
            return StatusCode(500, "An error occurred while processing the payment");
        }
    }

    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<PaymentResponseDto>> RefundPayment(
        Guid id, 
        [FromBody] RefundRequestDto refundRequest)
    {
        try
        {
            var payment = await _paymentService.RefundPaymentAsync(id, refundRequest);
            return Ok(payment);
        }
        catch (ArgumentException ex)
        {
            return NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding payment with ID: {PaymentId}", id);
            return StatusCode(500, "An error occurred while refunding the payment");
        }
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<ActionResult<List<PaymentResponseDto>>> GetPaymentsByOrder(Guid orderId)
    {
        try
        {
            var payments = await _paymentService.GetPaymentsByOrderAsync(orderId);
            return Ok(payments);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payments for order ID: {OrderId}", orderId);
            return StatusCode(500, "An error occurred while retrieving payments");
        }
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdatePaymentStatus(
        Guid id, 
        [FromBody] UpdatePaymentStatusDto statusDto)
    {
        try
        {
            var success = await _paymentService.UpdatePaymentStatusAsync(id, statusDto);
            if (!success)
            {
                return NotFound($"Payment with ID {id} not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment status with ID: {PaymentId}", id);
            return StatusCode(500, "An error occurred while updating the payment status");
        }
    }
}