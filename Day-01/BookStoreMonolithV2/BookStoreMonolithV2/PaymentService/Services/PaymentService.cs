using SharedModels.Models;
using System.Collections.Concurrent;

namespace PaymentService.Services;

public interface IPaymentService
{
    Task<PaymentResponseDto> ProcessPaymentAsync(PaymentCreateDto paymentCreateDto);
    Task<PaymentResponseDto> GetPaymentAsync(Guid id);
    Task<PaymentResponseDto> GetPaymentByOrderAsync(Guid orderId);
    Task<PaymentResponseDto> UpdatePaymentStatusAsync(Guid id, UpdatePaymentStatusDto updateDto);
    Task<PaymentResponseDto> ProcessRefundAsync(Guid paymentId, RefundRequestDto refundRequest);
}

public class PaymentService : IPaymentService
{
    private static readonly ConcurrentDictionary<Guid, Payment> _payments = new();
    private static readonly ConcurrentDictionary<Guid, Transaction> _transactions = new();

    public Task<PaymentResponseDto> ProcessPaymentAsync(PaymentCreateDto paymentCreateDto)
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = paymentCreateDto.OrderId,
            Amount = paymentCreateDto.Amount,
            Currency = paymentCreateDto.Currency,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Completed, // Simulate successful payment
            PaymentMethod = paymentCreateDto.PaymentMethod,
            GatewayTransactionId = $"GTX_{DateTime.UtcNow.Ticks}",
            Description = paymentCreateDto.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            PaymentId = payment.Id,
            Amount = payment.Amount,
            TransactionType = "Capture",
            GatewayTransactionId = payment.GatewayTransactionId,
            CreatedAt = DateTime.UtcNow,
            Status = "Completed",
            GatewayResponse = "Payment processed successfully"
        };

        payment.Transactions = new List<Transaction> { transaction };
        
        _payments[payment.Id] = payment;
        _transactions[transaction.Id] = transaction;

        return Task.FromResult(MapToPaymentResponseDto(payment));
    }

    public Task<PaymentResponseDto> GetPaymentAsync(Guid id)
    {
        if (_payments.TryGetValue(id, out var payment))
        {
            return Task.FromResult(MapToPaymentResponseDto(payment));
        }
        return Task.FromResult<PaymentResponseDto>(null);
    }

    public Task<PaymentResponseDto> GetPaymentByOrderAsync(Guid orderId)
    {
        var payment = _payments.Values.FirstOrDefault(p => p.OrderId == orderId);
        return Task.FromResult(payment != null ? MapToPaymentResponseDto(payment) : null);
    }

    public Task<PaymentResponseDto> UpdatePaymentStatusAsync(Guid id, UpdatePaymentStatusDto updateDto)
    {
        if (_payments.TryGetValue(id, out var payment))
        {
            payment.Status = updateDto.Status;
            if (!string.IsNullOrEmpty(updateDto.GatewayTransactionId))
            {
                payment.GatewayTransactionId = updateDto.GatewayTransactionId;
            }
            payment.UpdatedAt = DateTime.UtcNow;
            _payments[id] = payment;

            return Task.FromResult(MapToPaymentResponseDto(payment));
        }
        return Task.FromResult<PaymentResponseDto>(null);
    }

    public Task<PaymentResponseDto> ProcessRefundAsync(Guid paymentId, RefundRequestDto refundRequest)
    {
        if (_payments.TryGetValue(paymentId, out var payment))
        {
            var refundTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                PaymentId = payment.Id,
                Amount = refundRequest.Amount,
                TransactionType = "Refund",
                GatewayTransactionId = $"REF_{DateTime.UtcNow.Ticks}",
                CreatedAt = DateTime.UtcNow,
                Status = "Completed",
                GatewayResponse = $"Refund processed: {refundRequest.Reason}"
            };

            payment.Transactions.Add(refundTransaction);
            payment.Status = refundRequest.Amount == payment.Amount ? 
                PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
            payment.UpdatedAt = DateTime.UtcNow;

            _transactions[refundTransaction.Id] = refundTransaction;
            _payments[paymentId] = payment;

            return Task.FromResult(MapToPaymentResponseDto(payment));
        }
        return Task.FromResult<PaymentResponseDto>(null);
    }

    private static PaymentResponseDto MapToPaymentResponseDto(Payment payment)
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