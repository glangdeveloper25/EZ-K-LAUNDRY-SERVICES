using EZKLaundrySystem.Models;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Data;

/// <summary>
/// The shop's detergent and fabric conditioner brands. Each product is a per-piece
/// entry in the services catalogue (category Consumable) with its own stock.
/// </summary>
public static class ProductCatalog
{
    public const string Detergent = "Detergent";
    public const string FabricConditioner = "Fabric Conditioner";
    public static readonly string[] Types = [Detergent, FabricConditioner];

    // Full-Service (drop-off) with the shop's supplies: one detergent and one fabric
    // conditioner per load, charged as a flat package fee plus any premium extra.
    public const string PackageFee = "Detergent & Fabcon Package";
    public const string PremiumFee = "Premium Detergent";

    /// <summary>An active product of the given type with its package extra; null when it is not sold.</summary>
    public static (string Name, decimal PackageExtra)? FindActive(MySqlConnection connection,
        MySqlTransaction? transaction, int serviceId, string productType)
    {
        using var command = new MySqlCommand("""
            SELECT service_name, package_extra FROM services
            WHERE service_id = @id AND service_category = 'Consumable' AND charging_basis = 'PerItem'
              AND is_active = TRUE AND product_type = @type
            """, connection, transaction);
        command.Parameters.AddWithValue("@id", serviceId);
        command.Parameters.AddWithValue("@type", productType);
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetString("service_name"), reader.GetDecimal("package_extra")) : null;
    }

    /// <param name="activeOnly">False also returns products the owner has hidden.</param>
    public static List<PosCatalogItem> Read(MySqlConnection connection, bool activeOnly = true)
    {
        bool stockReady = InventoryStore.IsReady(connection);
        using var command = new MySqlCommand($"""
            SELECT s.service_id, s.service_name, COALESCE(s.product_type, '') AS product_type, s.current_price,
                   s.package_extra, s.is_active, {(stockReady ? "stock.quantity" : "NULL")} AS stock
            FROM services s
            {(stockReady ? "LEFT JOIN inventory_stock stock ON stock.service_id = s.service_id" : "")}
            WHERE s.service_category = 'Consumable' AND s.charging_basis = 'PerItem'
              {(activeOnly ? "AND s.is_active = TRUE" : "")}
            ORDER BY s.product_type, s.service_name
            """, connection);
        using var reader = command.ExecuteReader();
        var products = new List<PosCatalogItem>();
        while (reader.Read())
            products.Add(new PosCatalogItem
            {
                ServiceId = Convert.ToInt32(reader["service_id"]),
                Name = reader.GetString("service_name"),
                ProductType = reader.GetString("product_type"),
                Price = Convert.ToDecimal(reader["current_price"]),
                PackageExtra = Convert.ToDecimal(reader["package_extra"]),
                IsActive = Convert.ToBoolean(reader["is_active"]),
                Stock = reader["stock"] == DBNull.Value ? null : Convert.ToInt32(reader["stock"])
            });
        return products;
    }

    /// <summary>Looks up an active product of the given type; null when it is not sold.</summary>
    public static string? FindActiveName(MySqlConnection connection, int serviceId, string productType)
    {
        using var command = new MySqlCommand("""
            SELECT service_name FROM services
            WHERE service_id = @id AND service_category = 'Consumable' AND charging_basis = 'PerItem'
              AND is_active = TRUE AND product_type = @type
            """, connection);
        command.Parameters.AddWithValue("@id", serviceId);
        command.Parameters.AddWithValue("@type", productType);
        return command.ExecuteScalar() as string;
    }
}
