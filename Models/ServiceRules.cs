namespace EZKLaundrySystem.Models;

public static class ServiceRules
{
    public const string FullService = "Full-Service";
    public const string SelfService = "Self Service";

    public static bool IsValidMode(string? serviceMode) => serviceMode is FullService or SelfService;

    public static string? ServiceModeConflict(string? serviceMode, bool isRush)
    {
        if (!IsValidMode(serviceMode)) return "Choose Full-Service or Self-Service (DIY).";
        return serviceMode == SelfService && isRush
            ? "Rush priority is not available for Self-Service (DIY)."
            : null;
    }
}
