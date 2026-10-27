namespace CakeShop.Domain.Content;

/// <summary>A tenant-scoped product included in a hamper or seasonal collection.</summary>
public sealed class StoreCollectionItem
{
    private StoreCollectionItem() { }
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid CollectionId { get; private set; }
    public Guid ProductId { get; private set; }
    public int SortOrder { get; private set; }

    public StoreCollectionItem(Guid clientId, Guid collectionId, Guid productId, int sortOrder)
    {
        if (clientId == Guid.Empty || collectionId == Guid.Empty || productId == Guid.Empty)
            throw new ArgumentException("Tenant, collection and product IDs are required.");
        if (sortOrder < 0 || sortOrder > 10000) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        Id = Guid.NewGuid(); ClientId = clientId; CollectionId = collectionId; ProductId = productId; SortOrder = sortOrder;
    }
}
