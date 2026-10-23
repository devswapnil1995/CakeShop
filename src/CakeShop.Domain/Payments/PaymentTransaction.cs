using System;
using System.Collections.Generic;
using System.Text;

namespace CakeShop.Domain.Payments
{
    public enum PaymentStatus
    {
        Created = 0,
        Pending = 1,
        Authorized = 2,
        Captured = 3,
        Failed = 4,
        Cancelled = 5,
        Refunded = 6
    }

    public sealed class PaymentTransaction
    {
        private PaymentTransaction() { } // EF Core

        public Guid Id { get; private set; }
        public Guid ClientId { get; private set; }
        public Guid OrderId { get; private set; }

        public string Provider { get; private set; } = "Razorpay";
        public string Currency { get; private set; } = "INR";

        // Amount is stored in INR, not paise.
        public decimal Amount { get; private set; }

        public PaymentStatus Status { get; private set; }

        // Razorpay identifiers; never store secret keys here.
        public string? ProviderOrderId { get; private set; }
        public string? ProviderPaymentId { get; private set; }

        public DateTime CreatedUtc { get; private set; }
        public DateTime UpdatedUtc { get; private set; }

        public PaymentTransaction(
            Guid id,
            Guid clientId,
            Guid orderId,
            decimal amount)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Payment ID is required.");

            if (clientId == Guid.Empty)
                throw new ArgumentException("Client ID is required.");

            if (orderId == Guid.Empty)
                throw new ArgumentException("Order ID is required.");

            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Id = id;
            ClientId = clientId;
            OrderId = orderId;
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            Status = PaymentStatus.Created;
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
        }

        public void SetProviderOrderId(string providerOrderId)
        {
            if (string.IsNullOrWhiteSpace(providerOrderId))
                throw new ArgumentException("Provider order ID is required.");

            if (Status != PaymentStatus.Created)
                throw new InvalidOperationException(
                    "Provider order ID can only be set when payment is created.");

            ProviderOrderId = providerOrderId.Trim();
            Status = PaymentStatus.Pending;
            UpdatedUtc = DateTime.UtcNow;
        }

        public void MarkCaptured(string providerPaymentId)
        {
            if (string.IsNullOrWhiteSpace(providerPaymentId))
                throw new ArgumentException("Provider payment ID is required.");

            if (Status == PaymentStatus.Captured)
            {
                if (ProviderPaymentId != providerPaymentId)
                    throw new InvalidOperationException(
                        "Payment was already captured with a different payment ID.");

                return; // Idempotent retry
            }

            if (Status is PaymentStatus.Failed
                or PaymentStatus.Cancelled
                or PaymentStatus.Refunded)
            {
                throw new InvalidOperationException(
                    $"Cannot capture a payment in {Status} status.");
            }

            ProviderPaymentId = providerPaymentId.Trim();
            Status = PaymentStatus.Captured;
            UpdatedUtc = DateTime.UtcNow;
        }

        public void MarkFailed()
        {
            if (Status == PaymentStatus.Failed)
                return;

            if (Status == PaymentStatus.Captured
                || Status == PaymentStatus.Refunded)
            {
                throw new InvalidOperationException(
                    $"Cannot fail a payment in {Status} status.");
            }

            Status = PaymentStatus.Failed;
            UpdatedUtc = DateTime.UtcNow;
        }
    }
}
