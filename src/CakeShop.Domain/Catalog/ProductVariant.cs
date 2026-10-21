using System;
using System.Collections.Generic;
using System.Text;

namespace CakeShop.Domain.Catalog
{
    public sealed class ProductVariant
    {
        private ProductVariant() { } // EF Core

        public Guid Id { get; private set; }
        public Guid ClientId { get; private set; }
        public Guid ProductId { get; private set; }

        public string SizeLabel { get; private set; } = string.Empty;
        public int WeightInGrams { get; private set; }
        public decimal Price { get; private set; }
        public bool IsAvailable { get; private set; }

        public ProductVariant(
            Guid id,
            Guid clientId,
            Guid productId,
            string sizeLabel,
            int weightInGrams,
            decimal price,
            bool isAvailable = true)
        {
            if (id == Guid.Empty) throw new ArgumentException("Variant ID is required.");
            if (clientId == Guid.Empty) throw new ArgumentException("Client ID is required.");
            if (productId == Guid.Empty) throw new ArgumentException("Product ID is required.");
            if (string.IsNullOrWhiteSpace(sizeLabel) || sizeLabel.Trim().Length > 30)
                throw new ArgumentException("A size label of at most 30 characters is required.");
            if (weightInGrams <= 0 || weightInGrams > 100000)
                throw new ArgumentOutOfRangeException(nameof(weightInGrams));
            if (price <= 0 || price > 9999999999999999m)
                throw new ArgumentOutOfRangeException(nameof(price));
            if (price != decimal.Round(price, 2, MidpointRounding.AwayFromZero))
                throw new ArgumentException("Price cannot have more than two decimal places.");

            Id = id;
            ClientId = clientId;
            ProductId = productId;
            SizeLabel = sizeLabel.Trim();
            WeightInGrams = weightInGrams;
            Price = price;
            IsAvailable = isAvailable;
        }

        public void Update(
            string sizeLabel,
            int weightInGrams,
            decimal price,
            bool isAvailable)
        {
            if (string.IsNullOrWhiteSpace(sizeLabel) || sizeLabel.Trim().Length > 30)
                throw new ArgumentException("A size label of at most 30 characters is required.");
            if (weightInGrams <= 0 || weightInGrams > 100000)
                throw new ArgumentOutOfRangeException(nameof(weightInGrams));
            if (price <= 0 || price > 9999999999999999m)
                throw new ArgumentOutOfRangeException(nameof(price));
            if (price != decimal.Round(price, 2, MidpointRounding.AwayFromZero))
                throw new ArgumentException("Price cannot have more than two decimal places.");

            SizeLabel = sizeLabel.Trim();
            WeightInGrams = weightInGrams;
            Price = price;
            IsAvailable = isAvailable;
        }
    }
}
