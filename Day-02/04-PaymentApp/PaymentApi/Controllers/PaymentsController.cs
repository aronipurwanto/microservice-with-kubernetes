using Microsoft.AspNetCore.Mvc;
using PaymentCore;
using PaymentModels;

namespace PaymentApi.Controllers;

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

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> ProcessPayment([FromBody] PaymentRequest paymentRequest)
    {
        _logger.LogInformation("Processing payment for merchant {MerchantId}", paymentRequest.MerchantId);

        var result = await _paymentService.ProcessPaymentAsync(paymentRequest);

        var response = new PaymentResponse
        {
            TransactionId = result.TransactionId,
            Amount = paymentRequest.Amount,
            Currency = paymentRequest.Currency,
            ProcessedAt = DateTime.UtcNow
        };

        if (result.IsSuccess)
        {
            response.Status = "Success";
            response.Message = "Payment processed successfully";
            return Ok(response);
        }

        response.Status = "Failed";
        response.Message = result.ErrorMessage;
        return BadRequest(response);
    }

    [HttpGet("{transactionId}")]
    public async Task<ActionResult<Transaction>> GetTransaction(string transactionId)
    {
        var transaction = await _paymentService.GetTransactionAsync(transactionId);
        
        if (transaction == null)
            return NotFound();

        return Ok(transaction);
    }

    [HttpGet("merchant/{merchantId}")]
    public async Task<ActionResult<IEnumerable<Transaction>>> GetMerchantTransactions(string merchantId)
    {
        var transactions = await _paymentService.GetMerchantTransactionsAsync(merchantId);
        return Ok(transactions);
    }
}