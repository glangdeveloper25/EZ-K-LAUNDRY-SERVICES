namespace EZKLaundrySystem.Models;

/// <summary>
/// A customer's supply preference, not a stock movement or proof of consumption.
/// </summary>
public static class LaundrySupplyChoices
{
    public const string Shop = "Shop";
    public const string Customer = "Customer";
    public const string None = "None";

    public static bool IsValid(string? source) => source is Shop or Customer or None;

    public static bool IsValidQuantity(string? source, int quantity) =>
        IsValid(source) && (source != Shop || quantity is >= 1 and <= 20);

    public static string Describe(string? source, decimal quantity, string productName = "") => source switch
    {
        Shop => $"Buy from shop{(string.IsNullOrWhiteSpace(productName) ? "" : $": {productName}")} ({quantity:0.##} piece(s))",
        Customer => "Customer will bring their own (no shop-product charge)",
        None => "Not needed / no shop supply (no shop-product charge)",
        _ => "Not specified"
    };

    public static string BuildInstructions(
        string detergentSource, decimal detergentQuantity,
        string softenerSource, decimal softenerQuantity,
        string detergentName = "", string softenerName = "") =>
        $"Detergent: {Describe(detergentSource, detergentQuantity, detergentName)}\n" +
        $"Fabric conditioner: {Describe(softenerSource, softenerQuantity, softenerName)}";
}
