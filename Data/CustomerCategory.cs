namespace EZKLaundrySystem.Data;

/// <summary>
/// User Categorization (Scope): customers are automatically "New" or "Regular" based on
/// their completed transactions. Regular customers get priority requests in the staff
/// queue, right after Rush orders.
/// </summary>
public static class CustomerCategory
{
    public const int RegularThreshold = 5;
    public const string New = "New";
    public const string Regular = "Regular";

    public static string Classify(int completedOrders) => completedOrders >= RegularThreshold ? Regular : New;

    public static int OrdersUntilRegular(int completedOrders) => Math.Max(0, RegularThreshold - completedOrders);

    /// <summary>SQL expression (needs alias b for bookings) counting the customer's completed orders.</summary>
    public const string CompletedOrdersSql = """
        (SELECT COUNT(*) FROM bookings completed_b
         JOIN booking_statuses completed_s ON completed_s.booking_status_id = completed_b.current_status_id
         WHERE completed_b.customer_id = b.customer_id AND completed_s.status_name = 'Completed')
        """;
}
