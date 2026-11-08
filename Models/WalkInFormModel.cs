namespace EZKLaundrySystem.Models;

public class WalkInFormModel
{
    public string CustomerName { get; set; } = "";
    public string Contact { get; set; } = "";
    public string Services { get; set; } = "";
    public string ServiceMode { get; set; } = ServiceRules.FullService;
    public decimal Weight { get; set; }
    public bool IsRush { get; set; }
    public string DetergentSource { get; set; } = "";
    public string SoftenerSource { get; set; } = "";
    public int PowderQuantity { get; set; } = 1;
    public int DownyQuantity { get; set; } = 1;
    public bool IssueShopSupplies { get; set; }
    public string RequestToken { get; set; } = "";

    public string[] CoreServices => (Services ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
    public bool UsesShopSupplies => DetergentSource == LaundrySupplyChoices.Shop || SoftenerSource == LaundrySupplyChoices.Shop;

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(CustomerName) || CustomerName.Length > 200 ||
            string.IsNullOrWhiteSpace(Contact) || Contact.Trim().Length > 20)
            return "Enter a customer name (up to 200 characters) and contact number (up to 20 characters).";
        if (Weight <= 0m || Weight > 1000m || decimal.Round(Weight, 2) != Weight)
            return "Enter the actual weight between 0.01 and 1,000 kg, using at most two decimal places.";
        string[] allowed = ["Washing", "Drying", "Folding"];
        if (CoreServices.Length == 0 || CoreServices.Any(service => !allowed.Contains(service)))
            return "Choose at least one valid laundry service. Choose the detergent and fabric conditioner source separately below.";
        if (ServiceRules.ServiceModeConflict(ServiceMode, IsRush) is string serviceModeError)
            return serviceModeError;
        if (!LaundrySupplyChoices.IsValidQuantity(DetergentSource, PowderQuantity) ||
            !LaundrySupplyChoices.IsValidQuantity(SoftenerSource, DownyQuantity))
            return "Choose the source of detergent and fabric conditioner. Shop quantities must be whole pieces from 1 to 20.";
        if (UsesShopSupplies && !IssueShopSupplies)
            return "Confirm that the shop supplies have actually been issued before saving this walk-in order.";
        return null;
    }
}
