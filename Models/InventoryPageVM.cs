namespace EZKLaundrySystem.Models;

public class InventoryPageVM
{
    public InventorySnapshot Snapshot { get; set; } = new();
    public bool IsOwner { get; set; }
    public string RequestToken { get; set; } = "";
    public DateOnly? ReportFromDate { get; set; }
    public DateOnly? ReportToDate { get; set; }
}
