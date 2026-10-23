using System;
using System.Collections.Generic;
using System.Text;

namespace CakeShop.Domain.Payments
{
    public sealed class ProcessedPaymentWebhook
    {
        private ProcessedPaymentWebhook() { }

        public Guid Id { get; private set; }

        public string Provider { get; private set; } = "Razorpay";

        public string EventId { get; private set; } = string.Empty;

        public DateTime ProcessedUtc { get; private set; }

        public ProcessedPaymentWebhook(string eventId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
                throw new ArgumentException("Webhook event ID is required.", nameof(eventId));

            if (eventId.Length > 200)
                throw new ArgumentException("Webhook event ID is too long.", nameof(eventId));

            Id = Guid.NewGuid();
            EventId = eventId.Trim();
            ProcessedUtc = DateTime.UtcNow;
        }
    }
}
