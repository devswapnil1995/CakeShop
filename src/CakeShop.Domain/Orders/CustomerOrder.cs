using System;
using System.Collections.Generic;
using System.Text;

namespace CakeShop.Domain.Orders
{

    public enum CustomerOrderStatus
    {
        Pending = 0,
        Confirmed = 1,
        Preparing = 2,
        Ready = 3,
        Completed = 4,
        Cancelled = 5
    }

    public sealed class CustomerOrder
    {
        private CustomerOrder() { } // EF Core

        public Guid Id { get; private set; }
        public Guid ClientId { get; private set; }
        public string OrderNumber { get; private set; } = string.Empty;
        public string CustomerName { get; private set; } = string.Empty;
        public string CustomerPhone { get; private set; } = string.Empty;
        public Guid? ProductId { get; private set; }
        public string CakeName { get; private set; } = string.Empty;
        public int Quantity { get; private set; }
        public string CakeSize { get; private set; } = string.Empty;
        public DateOnly? PreferredDate { get; private set; }
        public string? Customization { get; private set; }
        public CustomerOrderStatus Status { get; private set; }
        public DateTime CreatedUtc { get; private set; }
        public DateTime UpdatedUtc { get; private set; }

        public CustomerOrder(
            Guid id,
            Guid clientId,
            string orderNumber,
            string customerName,
            string customerPhone,
            Guid? productId,
            string cakeName,
            int quantity,
            string cakeSize,
            DateOnly? preferredDate,
            string? customization)
        {
            if (id == Guid.Empty) throw new ArgumentException("Order ID is required.");
            if (clientId == Guid.Empty) throw new ArgumentException("Client ID is required.");
            if (string.IsNullOrWhiteSpace(customerName)) throw new ArgumentException("Customer name is required.");
            if (string.IsNullOrWhiteSpace(customerPhone)) throw new ArgumentException("Customer phone is required.");
            if (string.IsNullOrWhiteSpace(cakeName)) throw new ArgumentException("Cake name is required.");
            if (quantity < 1 || quantity > 100) throw new ArgumentOutOfRangeException(nameof(quantity));

            Id = id;
            ClientId = clientId;
            OrderNumber = orderNumber;
            CustomerName = customerName.Trim();
            CustomerPhone = customerPhone.Trim();
            ProductId = productId;
            CakeName = cakeName.Trim();
            Quantity = quantity;
            CakeSize = cakeSize.Trim();
            PreferredDate = preferredDate;
            Customization = string.IsNullOrWhiteSpace(customization)
                ? null
                : customization.Trim();
            Status = CustomerOrderStatus.Pending;
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
        }


        public bool CanTransitionTo(CustomerOrderStatus nextStatus)
        {
            return Status switch
            {
                CustomerOrderStatus.Pending =>
                    nextStatus is CustomerOrderStatus.Confirmed
                        or CustomerOrderStatus.Cancelled,

                CustomerOrderStatus.Confirmed =>
                    nextStatus is CustomerOrderStatus.Preparing
                        or CustomerOrderStatus.Cancelled,

                CustomerOrderStatus.Preparing =>
                    nextStatus is CustomerOrderStatus.Ready
                        or CustomerOrderStatus.Cancelled,

                CustomerOrderStatus.Ready =>
                    nextStatus is CustomerOrderStatus.Completed
                        or CustomerOrderStatus.Cancelled,

                CustomerOrderStatus.Completed => false,
                CustomerOrderStatus.Cancelled => false,

                _ => false
            };
        }

        public void UpdateStatus(CustomerOrderStatus status)
        {
            if (!CanTransitionTo(status))
            {
                throw new InvalidOperationException(
                    $"Cannot change order status from {Status} to {status}.");
            }

            Status = status;
            UpdatedUtc = DateTime.UtcNow;
        }

    }

}