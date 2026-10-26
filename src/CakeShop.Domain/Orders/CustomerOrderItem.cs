namespace CakeShop.Domain.Orders;

public sealed class CustomerOrderItem
{
    private CustomerOrderItem() { }

    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ProductId { get; private set; }
    public Guid ProductVariantId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string SizeLabel { get; private set; } = string.Empty;
    public int WeightInGrams { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    public CustomerOrderItem(
        Guid id, Guid clientId, Guid orderId, Guid productId, Guid productVariantId,
        string productName, string sizeLabel, int weightInGrams, int quantity, decimal unitPrice)
    {
        if (id == Guid.Empty || clientId == Guid.Empty || orderId == Guid.Empty ||
            productId == Guid.Empty || productVariantId == Guid.Empty)
            throw new ArgumentException("Cart order item identifiers are required.");
        if (string.IsNullOrWhiteSpace(productName) || productName.Trim().Length > 160)
            throw new ArgumentException("Product name is required.");
        if (string.IsNullOrWhiteSpace(sizeLabel) || sizeLabel.Trim().Length > 30)
            throw new ArgumentException("Size label is required.");
        if (weightInGrams <= 0 || quantity < 1 || quantity > 100)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (unitPrice <= 0 || unitPrice != decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero))
            throw new ArgumentOutOfRangeException(nameof(unitPrice));

        Id = id;
        ClientId = clientId;
        OrderId = orderId;
        ProductId = productId;
        ProductVariantId = productVariantId;
        ProductName = productName.Trim();
        SizeLabel = sizeLabel.Trim();
        WeightInGrams = weightInGrams;
        Quantity = quantity;
        UnitPrice = unitPrice;
        LineTotal = decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
    }
}
