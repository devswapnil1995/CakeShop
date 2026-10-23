using System;
using System.Collections.Generic;
using System.Text;

namespace CakeShop.Domain.Payments
{
    public enum PaymentEnvironment
    {
        Test = 0,
        Live = 1
    }

    public sealed class TenantPaymentSettings
    {
        private TenantPaymentSettings() { } // EF Core

        public Guid Id { get; private set; }
        public Guid ClientId { get; private set; }

        public string Provider { get; private set; } = "Razorpay";
        public PaymentEnvironment Environment { get; private set; }
        public bool IsEnabled { get; private set; }

        // Set when using a provider-supported connected merchant account.
        public string? MerchantAccountReference { get; private set; }

        // Public key identifier and secret-store reference only.
        // Never persist the actual secret in this table.
        public string? KeyId { get; private set; }
        public string? CredentialSecretReference { get; private set; }

        public DateTime CreatedUtc { get; private set; }
        public DateTime UpdatedUtc { get; private set; }

        public TenantPaymentSettings(
            Guid id,
            Guid clientId,
            PaymentEnvironment environment)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Settings ID is required.");

            if (clientId == Guid.Empty)
                throw new ArgumentException("Client ID is required.");

            Id = id;
            ClientId = clientId;
            Environment = environment;
            IsEnabled = false;
            CreatedUtc = DateTime.UtcNow;
            UpdatedUtc = CreatedUtc;
        }

        public void Configure(
            string? merchantAccountReference,
            string? keyId,
            string? credentialSecretReference,
            bool enable)
        {
            if (enable &&
                string.IsNullOrWhiteSpace(credentialSecretReference))
            {
                throw new ArgumentException(
                    "A secure credential reference is required to enable payments.");
            }

            MerchantAccountReference =
                string.IsNullOrWhiteSpace(merchantAccountReference)
                    ? null
                    : merchantAccountReference.Trim();

            KeyId = string.IsNullOrWhiteSpace(keyId)
                ? null
                : keyId.Trim();

            CredentialSecretReference =
                string.IsNullOrWhiteSpace(credentialSecretReference)
                    ? null
                    : credentialSecretReference.Trim();

            IsEnabled = enable;
            UpdatedUtc = DateTime.UtcNow;
        }

        public void Disable()
        {
            IsEnabled = false;
            UpdatedUtc = DateTime.UtcNow;
        }
    }
}
