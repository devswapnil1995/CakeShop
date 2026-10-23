using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace CakeShop.Infrastructure.Payments
{

    public sealed class RazorpayPaymentService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        public bool VerifyCheckoutSignature(
    string providerOrderId,
    string providerPaymentId,
    string suppliedSignature)
        {
            var secret = configuration["Razorpay:KeySecret"];

            if (string.IsNullOrWhiteSpace(secret) ||
                string.IsNullOrWhiteSpace(providerOrderId) ||
                string.IsNullOrWhiteSpace(providerPaymentId) ||
                string.IsNullOrWhiteSpace(suppliedSignature))
            {
                return false;
            }

            var payload = $"{providerOrderId}|{providerPaymentId}";
            var expected = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(secret),
                Encoding.UTF8.GetBytes(payload));

            try
            {
                var supplied = Convert.FromHexString(suppliedSignature);
                return CryptographicOperations.FixedTimeEquals(expected, supplied);
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public async Task<bool> IsPaymentCapturedAsync(
            string providerPaymentId,
            string expectedOrderId,
            long expectedAmountInPaise,
            CancellationToken cancellationToken)
        {
            var keyId = configuration["Razorpay:KeyId"];
            var keySecret = configuration["Razorpay:KeySecret"];

            if (string.IsNullOrWhiteSpace(keyId) ||
                string.IsNullOrWhiteSpace(keySecret) ||
                !keyId.StartsWith("rzp_test_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Razorpay Test Mode credentials are not configured.");
            }

            httpClient.BaseAddress ??= new Uri("https://api.razorpay.com/v1/");

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"payments/{Uri.EscapeDataString(providerPaymentId)}");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));

            var payment = document.RootElement;

            return payment.GetProperty("status").GetString() == "captured"
                && payment.GetProperty("order_id").GetString() == expectedOrderId
                && payment.GetProperty("currency").GetString() == "INR"
                && payment.GetProperty("amount").GetInt64() == expectedAmountInPaise;
        }

        public async Task<RazorpayOrderResult> CreateOrderAsync(
            long amountInPaise,
            string receipt,
            CancellationToken cancellationToken)
        {
            var keyId = configuration["Razorpay:KeyId"];
            var keySecret = configuration["Razorpay:KeySecret"];
            var environment = configuration["Razorpay:Environment"];

            if (!string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(keyId) ||
                !keyId.StartsWith("rzp_test_", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(keySecret))
            {
                throw new InvalidOperationException(
                    "Valid Razorpay Test Mode credentials are not configured.");
            }

            if (amountInPaise < 100)
                throw new ArgumentOutOfRangeException(
                    nameof(amountInPaise),
                    "Payment amount must be at least ₹1.");

            httpClient.BaseAddress ??= new Uri("https://api.razorpay.com/v1/");

            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{keyId}:{keySecret}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, "orders");
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            request.Content = JsonContent.Create(new
            {
                amount = amountInPaise,
                currency = "INR",
                receipt,
                partial_payment = false
            });

            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Razorpay order creation failed with HTTP {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));

            var root = document.RootElement;
            var orderId = root.GetProperty("id").GetString();
            var returnedAmount = root.GetProperty("amount").GetInt64();
            var currency = root.GetProperty("currency").GetString();

            if (string.IsNullOrWhiteSpace(orderId) ||
                returnedAmount != amountInPaise ||
                currency != "INR")
            {
                throw new InvalidOperationException(
                    "Razorpay returned an unexpected order response.");
            }

            return new RazorpayOrderResult(orderId, returnedAmount, currency);
        }
    }

        public sealed record RazorpayOrderResult(
        string OrderId,
        long AmountInPaise,
        string? Currency);
}
