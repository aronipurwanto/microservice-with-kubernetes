using Microsoft.EntityFrameworkCore;
using PaymentService.Models;
using SharedModels.Models;

namespace PaymentService.Services;

public interface IPaymentService
{
    Task<PaymentResponseDto?> GetPaymentAsync(Guid id);
    Task<PaymentResponseDto> ProcessPaymentAsync(PaymentCreateDto paymentDto);
    Task<PaymentResponseDto> RefundPaymentAsync(Guid paymentId, RefundRequestDto refundRequest);
    Task<bool> UpdatePaymentStatusAsync(Guid id, UpdatePaymentStatusDto statusDto);
    Task<List<PaymentResponseDto>> GetPaymentsByOrderAsync(Guid orderId);
    Task<List<PaymentResponseDto>> GetPaymentsByStatusAsync(PaymentStatus status);
}

public class PaymentService : IPaymentService
{
    private readonly PaymentContext _context;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(PaymentContext context, ILogger<PaymentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaymentResponseDto?> GetPaymentAsync(Guid id)
    {
        var payment = await _context.Payments
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == id);

        return payment == null ? null : MapToDto(payment);
    }

    public async Task<PaymentResponseDto> ProcessPaymentAsync(PaymentCreateDto paymentDto)
    {
        // Simulate payment processing with a payment gateway
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = paymentDto.OrderId,
            Amount = paymentDto.Amount,
            Currency = paymentDto.Currency,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Processing,
            PaymentMethod = paymentDto.PaymentMethod,
            Description = paymentDto.Description,
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Started payment processing for order ID: {OrderId}", paymentDto.OrderId);

        // Simulate payment gateway processing (in real scenario, this would call actual payment gateway)
        await Task.Delay(1000); // Simulate processing time

        // Add authorization transaction
        var authTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Amount = paymentDto.Amount,
            TransactionType = "Authorization",
            GatewayTransactionId = $"AUTH_{DateTime.UtcNow.Ticks}",
            CreatedAt = DateTime.UtcNow,
            Status = "Completed",
            GatewayResponse = "Approved"
        };

        _context.Transactions.Add(authTransaction);

        // Update payment status based on simulated result
        payment.Status = PaymentStatus.Completed;
        payment.GatewayTransactionId = authTransaction.GatewayTransactionId;
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Completed payment processing with ID: {PaymentId} for order ID: {OrderId}", 
            payment.Id, payment.OrderId);
        
        return MapToDto(payment);
    }

    public async Task<PaymentResponseDto> RefundPaymentAsync(Guid paymentId, RefundRequestDto refundRequest)
    {
        var payment = await _context.Payments
            .Include(p => p.Transactions)
            .FirstOrDefaultAsync(p => p.Id == paymentId);

        if (payment == null)
            throw new ArgumentException($"Payment with ID {paymentId} not found");

        if (payment.Status != PaymentStatus.Completed)
            throw new InvalidOperationException("Can only refund completed payments");

        if (refundRequest.Amount <= 0 || refundRequest.Amount > payment.Amount)
            throw new ArgumentException("Invalid refund amount");

        // Add refund transaction
        var refundTransaction = new Transaction
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Amount = refundRequest.Amount,
            TransactionType = "Refund",
            GatewayTransactionId = $"REFUND_{DateTime.UtcNow.Ticks}",
            CreatedAt = DateTime.UtcNow,
            Status = "Completed",
            GatewayResponse = $"Refund processed: {refundRequest.Reason}"
        };

        _context.Transactions.Add(refundTransaction);

        // Update payment status
        payment.Status = refundRequest.Amount == payment.Amount 
            ? PaymentStatus.Refunded 
            : PaymentStatus.PartiallyRefunded;
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Processed refund of {Amount} for payment ID: {PaymentId}", 
            refundRequest.Amount, paymentId);
        
        return MapToDto(payment);
    }

    public async Task<bool> UpdatePaymentStatusAsync(Guid id, UpdatePaymentStatusDto statusDto)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null) return false;

        payment.Status = statusDto.Status;
        payment.GatewayTransactionId = statusDto.GatewayTransactionId;
        payment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated payment {PaymentId} status to {Status}", id, statusDto.Status);
        return true;
    }

    public async Task<List<PaymentResponseDto>> GetPaymentsByOrderAsync(Guid orderId)
    {
        var payments = await _context.Payments
            .Include(p => p.Transactions)
            .Where(p => p.OrderId == orderId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(MapToDto).ToList();
    }

    public async Task<List<PaymentResponseDto>> GetPaymentsByStatusAsync(PaymentStatus status)
    {
        var payments = await _context.Payments
            .Include(p => p.Transactions)
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return payments.Select(MapToDto).ToList();
    }

    private static PaymentResponseDto MapToDto(Payment payment)
    {
        return new PaymentResponseDto
        {
            Id = payment.Id,
            OrderId = payment.OrderId,
            Amount = payment.Amount,
            Currency = payment.Currency,
            PaymentDate = payment.PaymentDate,
            Status = payment.Status,
            PaymentMethod = payment.PaymentMethod,
            GatewayTransactionId = payment.GatewayTransactionId,
            Description = payment.Description,
            CreatedAt = payment.CreatedAt,
            Transactions = payment.Transactions.Select(t => new TransactionDto
            {
                Id = t.Id,
                Amount = t.Amount,
                TransactionType = t.TransactionType,
                Status = t.Status,
                GatewayResponse = t.GatewayResponse,
                CreatedAt = t.CreatedAt
            }).ToList()
        };
    }
}