namespace EZKLaundrySystem.Models;

public sealed class RewardSnapshot
{
    public bool IsReady { get; set; }
    public long Balance { get; set; }
    public long EarnedPoints { get; set; }
    public List<RewardHistoryItem> History { get; set; } = new();
    public bool CanRedeem => IsReady && Balance >= 100;
}

public sealed class RewardHistoryItem
{
    public long Id { get; set; }
    public int Points { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string BookingReference { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class RewardValidationException : Exception
{
    public RewardValidationException(string message) : base(message) { }
}
