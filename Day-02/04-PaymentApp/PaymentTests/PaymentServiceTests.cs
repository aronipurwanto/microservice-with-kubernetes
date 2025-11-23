using Microsoft.Extensions.Logging;
using Moq;
using PaymentCore;
using PaymentData;
using PaymentModels;

namespace PaymentTests;

public class PaymentServiceTests
{
    private readonly Mock<ITransactionRepository> _mockRepository;
    private readonly Mock<ILogger<PaymentService>> _mockLogger;
    private readonly PaymentService _paymentService;

    public PaymentServiceTests()
    {
        _mockRepository = new Mock<ITransactionRepository>();
        _mockLogger = new Mock<ILogger<PaymentService>>();
        _paymentService = new PaymentService(_mockRepository.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ProcessPayment_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new PaymentRequest
        {
            CardNumber = "4111111111111111",
            CardHolderName = "John Doe",
            ExpiryMonth = 12,
            ExpiryYear = 2025,
            CVV = "123",
            Amount = 100.00m,
            Currency = "USD",
            MerchantId = "MERCHANT123"
        };

        _mockRepository.Setup(repo => repo.AddAsync(It.IsAny<Transaction>()))
                      .ReturnsAsync((Transaction t) => t);
        _mockRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Transaction>()));

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.TransactionId);
        _mockRepository.Verify(repo => repo.AddAsync(It.IsAny<Transaction>()), Times.Once);
        _mockRepository.Verify(repo => repo.UpdateAsync(It.IsAny<Transaction>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPayment_InvalidCard_ReturnsFailure()
    {
        // Arrange
        var request = new PaymentRequest
        {
            CardNumber = "1234",
            CardHolderName = "John Doe",
            ExpiryMonth = 12,
            ExpiryYear = 2025,
            CVV = "123",
            Amount = 100.00m,
            Currency = "USD",
            MerchantId = "MERCHANT123"
        };

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid card number", result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPayment_ExpiredCard_ReturnsFailure()
    {
        // Arrange
        var request = new PaymentRequest
        {
            CardNumber = "4111111111111111",
            CardHolderName = "John Doe",
            ExpiryMonth = 1,
            ExpiryYear = 2020,
            CVV = "123",
            Amount = 100.00m,
            Currency = "USD",
            MerchantId = "MERCHANT123"
        };

        // Act
        var result = await _paymentService.ProcessPaymentAsync(request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Card has expired", result.ErrorMessage);
    }
}