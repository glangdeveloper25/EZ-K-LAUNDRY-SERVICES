using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Data;

/// <summary>
/// Per-load pricing: Washing, Drying and Folding are charged per load. The actual
/// weight is still recorded, and the shop's load size turns it into a load count.
/// </summary>
public static class LoadPricing
{
    public const string PerLoad = "PerLoad";
    public const decimal DefaultKgPerLoad = 7m;
    private const string KgPerLoadKey = "kg_per_load";

    public static decimal ReadKgPerLoad(MySqlConnection connection, MySqlTransaction? transaction = null)
    {
        try
        {
            using var command = new MySqlCommand(
                "SELECT setting_value FROM app_settings WHERE setting_key = @key", connection, transaction);
            command.Parameters.AddWithValue("@key", KgPerLoadKey);
            return decimal.TryParse(command.ExecuteScalar()?.ToString(), System.Globalization.NumberStyles.Number,
                       System.Globalization.CultureInfo.InvariantCulture, out decimal value) && value > 0m
                ? value
                : DefaultKgPerLoad;
        }
        catch (MySqlException error) when (error.Number == 1146)
        {
            // The per-load migration has not been run yet.
            return DefaultKgPerLoad;
        }
    }

    /// <returns>False when the value is outside 1 to 50 kg or the settings table is missing.</returns>
    public static bool SaveKgPerLoad(MySqlConnection connection, decimal kgPerLoad)
    {
        if (kgPerLoad is < 1m or > 50m || decimal.Round(kgPerLoad, 2) != kgPerLoad) return false;
        try
        {
            using var command = new MySqlCommand("""
                INSERT INTO app_settings (setting_key, setting_value) VALUES (@key, @value)
                ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value)
                """, connection);
            command.Parameters.AddWithValue("@key", KgPerLoadKey);
            command.Parameters.AddWithValue("@value", kgPerLoad.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
            return true;
        }
        catch (MySqlException error) when (error.Number == 1146)
        {
            return false;
        }
    }

    public static int LoadsFor(decimal weightKg, decimal kgPerLoad) =>
        weightKg <= 0m ? 1 : (int)Math.Ceiling(weightKg / (kgPerLoad > 0m ? kgPerLoad : DefaultKgPerLoad));

    /// <summary>The booking-line quantity for a service's charging basis.</summary>
    public static decimal QuantityFor(string chargingBasis, decimal weightKg, decimal pieces, decimal kgPerLoad) =>
        chargingBasis switch
        {
            "PerKg" => weightKg,
            PerLoad => LoadsFor(weightKg, kgPerLoad),
            "PerItem" => pieces,
            _ => 1m
        };
}
