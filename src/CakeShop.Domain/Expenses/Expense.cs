using System;
using System.Collections.Generic;
using System.Text;

namespace CakeShop.Domain.Expenses
{
    public sealed class Expense
    {
        // Required by EF Core.
        private Expense() { }

        public Guid Id { get; private set; }

        public Guid ClientId { get; private set; }

        public string Category { get; private set; } = string.Empty;

        public decimal Amount { get; private set; }

        public DateOnly ExpenseDate { get; private set; }

        public string? Description { get; private set; }

        public DateTime CreatedUtc { get; private set; }

        public DateTime UpdatedUtc { get; private set; }

        public Expense(
            Guid id,
            Guid clientId,
            string category,
            decimal amount,
            DateOnly expenseDate,
            string? description = null)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Expense ID is required.", nameof(id));

            if (clientId == Guid.Empty)
                throw new ArgumentException("Client ID is required.", nameof(clientId));

            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("Expense category is required.", nameof(category));

            category = category.Trim();

            if (category.Length > 80)
                throw new ArgumentException(
                    "Expense category cannot exceed 80 characters.",
                    nameof(category));

            if (amount <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "Expense amount must be greater than zero.");

            if (description?.Trim().Length > 500)
                throw new ArgumentException(
                    "Description cannot exceed 500 characters.",
                    nameof(description));

            Id = id;
            ClientId = clientId;
            Category = category;
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            ExpenseDate = expenseDate;
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
        }

        public void Update(
            string category,
            decimal amount,
            DateOnly expenseDate,
            string? description = null)
        {
            if (string.IsNullOrWhiteSpace(category))
                throw new ArgumentException("Expense category is required.", nameof(category));

            category = category.Trim();

            if (category.Length > 80)
                throw new ArgumentException(
                    "Expense category cannot exceed 80 characters.",
                    nameof(category));

            if (amount <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "Expense amount must be greater than zero.");

            if (description?.Trim().Length > 500)
                throw new ArgumentException(
                    "Description cannot exceed 500 characters.",
                    nameof(description));

            Category = category;
            Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            ExpenseDate = expenseDate;
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim();

            UpdatedUtc = DateTime.UtcNow;
        }
    }

}
