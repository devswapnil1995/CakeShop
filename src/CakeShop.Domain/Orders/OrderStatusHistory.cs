using System;
using System.Collections.Generic;
using System.Text;

namespace CakeShop.Domain.Orders
{
    public sealed class OrderStatusHistory
    {
        // Required by EF Core
        private OrderStatusHistory() { }

        public Guid Id { get; private set; }

        public Guid ClientId { get; private set; }

        public Guid OrderId { get; private set; }

        public CustomerOrderStatus PreviousStatus { get; private set; }

        public CustomerOrderStatus NewStatus { get; private set; }

        public DateTime ChangedUtc { get; private set; }

        public string? ChangedBy { get; private set; }

        public OrderStatusHistory(
            Guid id,
            Guid clientId,
            Guid orderId,
            CustomerOrderStatus previousStatus,
            CustomerOrderStatus newStatus,
            string? changedBy)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("History ID is required.");

            if (clientId == Guid.Empty)
                throw new ArgumentException("Client ID is required.");

            if (orderId == Guid.Empty)
                throw new ArgumentException("Order ID is required.");

            if (previousStatus == newStatus)
                throw new ArgumentException(
                    "Previous and new statuses must be different.");

            Id = id;
            ClientId = clientId;
            OrderId = orderId;
            PreviousStatus = previousStatus;
            NewStatus = newStatus;
            ChangedBy = string.IsNullOrWhiteSpace(changedBy)
                ? null
                : changedBy.Trim();
            ChangedUtc = DateTime.UtcNow;
        }
    }
}
