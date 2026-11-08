namespace EZKLaundrySystem.Models;

public sealed class InventorySnapshot
{
    /// <summary>Panel suggestion: warn when a product has this many pieces or fewer.</summary>
    public const int LowStockLevel = 5;

    public bool IsReady { get; set; }
    public List<InventoryItem> Items { get; set; } = new();
    public List<InventoryItem> NeedsRestock => Items.Where(item => item.Quantity <= LowStockLevel).ToList();
    public List<InventoryMovement> Movements { get; set; } = new();
}

public sealed class InventoryItem
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = "";
    public string ProductType { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public bool IsLow => Quantity > 0 && Quantity <= InventorySnapshot.LowStockLevel;
}

public sealed class InventoryMovement
{
    public string Name { get; set; } = "";
    public int Change { get; set; }
    public int BalanceAfter { get; set; }
    public string Kind { get; set; } = "";
    public string BookingReference { get; set; } = "";
    public string Reason { get; set; } = "";
    public string RecordedBy { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class InventoryValidationException : Exception
{
    public InventoryValidationException(string message) : base(message) { }
}
