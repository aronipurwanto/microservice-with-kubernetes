using Microsoft.AspNetCore.Mvc;
using PaymentService.Services;
using SharedModels.Models;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponseDto>> ProcessPayment(PaymentCreateDto paymentCreateDto)
    {
        var payment = await _paymentService.ProcessPaymentAsync(paymentCreateDto);
        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PaymentResponseDto>> GetPayment(Guid id)
    {
        var payment = await _paymentService.GetPaymentAsync(id);
        if (payment == null)
        {
            return NotFound();
        }
        return payment;
    }

    [HttpGet("order/{orderId}")]
    public async Task<ActionResult<PaymentResponseDto>> GetPaymentByOrder(Guid orderId)
    {
        var payment = await _paymentService.GetPaymentByOrderAsync(orderId);
        if (payment == null)
        {
            return NotFound();
        }
        return payment;
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<PaymentResponseDto>> UpdatePaymentStatus(Guid id, UpdatePaymentStatusDto updateDto)
    {
        var payment = await _paymentService.UpdatePaymentStatusAsync(id, updateDto);
        if (payment == null)
        {
            return NotFound();
        }
        return payment;
    }

    [HttpPost("{paymentId}/refund")]
    public async Task<ActionResult<PaymentResponseDto>> ProcessRefund(Guid paymentId, RefundRequestDto refundRequest)
    {
        var payment = await _paymentService.ProcessRefundAsync(paymentId, refundRequest);
        if (payment == null)
        {
            return NotFound();
        }
        return payment;
    }
}