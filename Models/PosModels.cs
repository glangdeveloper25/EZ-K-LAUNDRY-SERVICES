namespace EZKLaundrySystem.Models;

public sealed class PosPageVM
{
    public const int LowStockLevel = InventorySnapshot.LowStockLevel;

    public List<PosCatalogItem> Services { get; set; } = [];
    public List<PosCatalogItem> Products { get; set; } = [];
    public decimal RushFee { get; set; }
    public decimal PackageFee { get; set; }
    public decimal KgPerLoad { get; set; }
    public bool InventoryReady { get; set; }
    public string RequestToken { get; set; } = "";
}

public sealed class PosCatalogItem
{
    public int ServiceId { get; set; }
    public string Name { get; set; } = "";
    public string ProductType { get; set; } = "";
    public decimal Price { get; set; }
    /// <summary>Extra per load when this product is used in the Full-Service package (e.g. Ariel).</summary>
    public decimal PackageExtra { get; set; }
    public bool IsActive { get; set; } = true;
    public int? Stock { get; set; }
}

public sealed class PosProductLine
{
    public int ServiceId { get; set; }
    public int Quantity { get; set; }
}

/// <summary>A walk-in order entered through the POS and paid in full before saving.</summary>
public sealed class PosOrderForm
{
    public const int MaxLoads = 50;
    public const int MaxPieces = 50;

    public int? CustomerId { get; set; }
    public string NewFirstName { get; set; } = "";
    public string NewLastName { get; set; } = "";
    public string NewPhone { get; set; } = "";
    public string ServiceMode { get; set; } = ServiceRules.FullService;
    public decimal Weight { get; set; }
    public bool IsRush { get; set; }
    public int WashLoads { get; set; }
    public int DryLoads { get; set; }
    public int FoldLoads { get; set; }
    public List<PosProductLine> Products { get; set; } = [];
    public bool OwnDetergent { get; set; }
    public bool OwnSoftener { get; set; }
    /// <summary>Full-Service only: the shop's detergent and fabric conditioner are used (package fee per load).</summary>
    public bool ShopSupplies { get; set; }
    public int PackageDetergentId { get; set; }
    public int PackageFabconId { get; set; }
    public string PaymentMethod { get; set; } = "";

    public bool IsFullService => ServiceMode == ServiceRules.FullService;
    /// <summary>Full-Service is a wash-dry-fold package, so every step uses the same number of loads.</summary>
    public int PackageLoads => WashLoads;
    public decimal CashReceived { get; set; }
    public string GCashReference { get; set; } = "";
    public string RequestToken { get; set; } = "";

    public bool IsNewCustomer => CustomerId is null or <= 0;

    public string? Validate()
    {
        if (IsNewCustomer)
        {
            if (string.IsNullOrWhiteSpace(NewFirstName) || NewFirstName.Trim().Length > 100 ||
                string.IsNullOrWhiteSpace(NewLastName) || NewLastName.Trim().Length > 100)
                return "Search and select a customer, or enter the new customer's first and last name.";
            string digits = new(NewPhone.Where(char.IsDigit).ToArray());
            if (digits.Length is < 7 or > 15 || NewPhone.Trim().Length > 20)
                return "Enter the new customer's contact number (7 to 15 digits).";
        }
        if (Weight <= 0m || Weight > 1000m || decimal.Round(Weight, 2) != Weight)
            return "Enter the actual weight between 0.01 and 1,000 kg, using at most two decimal places.";
        if (new[] { WashLoads, DryLoads, FoldLoads }.Any(loads => loads is < 0 or > MaxLoads))
            return $"Each service can have 0 to {MaxLoads} loads.";
        if (ServiceRules.ServiceModeConflict(ServiceMode, IsRush) is string modeError)
            return modeError;
        if (IsFullService)
        {
            if (PackageLoads < 1)
                return "Enter the number of loads for the Full-Service wash-dry-fold package.";
            if (Products.Count > 0)
                return "Full-Service uses the package supplies. Sachets are sold separately only for Self-Service.";
            if (ShopSupplies && (PackageDetergentId <= 0 || PackageFabconId <= 0))
                return "Choose the detergent and fabric conditioner brands used for the package.";
        }
        else
        {
            if (FoldLoads != 0)
                return "Folding is included only in Full-Service.";
            if (WashLoads + DryLoads == 0)
                return "Add at least one Self-Service wash or dry.";
        }
        if (Products.Any(line => line.ServiceId <= 0 || line.Quantity is < 1 or > MaxPieces) ||
            Products.Select(line => line.ServiceId).Distinct().Count() != Products.Count)
            return $"Each product needs a quantity from 1 to {MaxPieces}.";
        if (PaymentMethod is not ("Cash" or "GCash"))
            return "Choose Cash or GCash as the payment method.";
        if (PaymentMethod == "GCash" && GCashReference.Trim().Length > 100)
            return "The GCash reference number is too long.";
        return null;
    }
}

public sealed class PosReceiptVM
{
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = "";
    public DateTime BookedAt { get; set; }
    public string CustomerName { get; set; } = "";
    public string CustomerPhone { get; set; } = "";
    public string StaffName { get; set; } = "";
    public string ServiceMode { get; set; } = ServiceRules.FullService;
    public decimal Weight { get; set; }
    public bool IsRush { get; set; }
    public string Notes { get; set; } = "";
    public List<PosReceiptLine> Lines { get; set; } = [];
    public List<PosReceiptPayment> Payments { get; set; } = [];
    public decimal Total => Lines.Sum(line => line.Amount);
    public decimal Paid => Payments.Sum(payment => payment.Amount);
    public decimal Balance => Math.Max(0m, Total - Paid);
}

public sealed class PosReceiptLine
{
    public string Name { get; set; } = "";
    public string ChargingBasis { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount => Quantity * UnitPrice;
}

public sealed class PosReceiptPayment
{
    public string Method { get; set; } = "";
    public decimal Amount { get; set; }
    public string Reference { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime? PaidAt { get; set; }
}
