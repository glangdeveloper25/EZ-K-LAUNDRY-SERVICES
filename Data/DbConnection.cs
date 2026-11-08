using System.Data;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Data
{
    public class DbConnection
    {
        // Online, the hosting service sets EZK_DB_CONNECTION; on the laptop it falls back to XAMPP MySQL.
        private static readonly string? OnlineConnectionString = Environment.GetEnvironmentVariable("EZK_DB_CONNECTION");
        private readonly string _connectionString = string.IsNullOrWhiteSpace(OnlineConnectionString)
            ? "Server=localhost;Database=laundry_final;Uid=root;Pwd=;"
            : OnlineConnectionString;

        public MySqlConnection GetConnection()
        {
            var connection = new MySqlConnection(_connectionString);
            // Online database servers run on UTC, so keep NOW() and CURDATE() on Philippine time.
            if (!string.IsNullOrWhiteSpace(OnlineConnectionString))
                connection.StateChange += UsePhilippineTime;
            return connection;
        }

        private static void UsePhilippineTime(object sender, StateChangeEventArgs change)
        {
            if (change.CurrentState != ConnectionState.Open) return;
            using var command = new MySqlCommand("SET time_zone = '+08:00'", (MySqlConnection)sender);
            command.ExecuteNonQuery();
        }
    }
}
