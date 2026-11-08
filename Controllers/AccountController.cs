using EZKLaundrySystem.Data;
using EZKLaundrySystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace EZKLaundrySystem.Controllers;

[AutoValidateAntiforgeryToken]
public class AccountController : Controller
{
    private readonly DbConnection _db = new();

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    public IActionResult Register(CustomerRegistration user)
    {
        if (!ModelState.IsValid) return View(user);

        using var conn = _db.GetConnection();
        conn.Open();

        const string check = "SELECT COUNT(*) FROM users WHERE email = @email OR phone = @phone";
        using (var checkCommand = new MySqlCommand(check, conn))
        {
            checkCommand.Parameters.AddWithValue("@email", user.Email.Trim());
            checkCommand.Parameters.AddWithValue("@phone", user.ContactNumber.Trim());
            if (Convert.ToInt32(checkCommand.ExecuteScalar()) > 0)
            {
                TempData["Msg"] = "This email or contact number is already registered.";
                TempData["Type"] = "error";
                return View(user);
            }
        }

        const string insert = """
            INSERT INTO users
                (role_id, user_status_id, first_name, last_name, email, phone, password_hash, is_walk_in)
            SELECT r.role_id, us.user_status_id, @firstName, @lastName, @email, @phone, @passwordHash, FALSE
            FROM roles r
            CROSS JOIN user_statuses us
            WHERE r.role_name = 'Customer' AND us.status_name = 'Pending'
            """;

        using (var command = new MySqlCommand(insert, conn))
        {
            command.Parameters.AddWithValue("@firstName", user.FirstName.Trim());
            command.Parameters.AddWithValue("@lastName", user.LastName.Trim());
            command.Parameters.AddWithValue("@email", user.Email.Trim());
            command.Parameters.AddWithValue("@phone", user.ContactNumber.Trim());
            command.Parameters.AddWithValue("@passwordHash", PasswordSecurity.HashPassword(user.Password));
            command.ExecuteNonQuery();
        }

        TempData["Msg"] = "You are registered! Please wait for staff approval.";
        TempData["Type"] = "success";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult Login() => View();

    [HttpPost]
    public IActionResult Login(string email, string password) => Authenticate(email, password, null);

    [HttpGet]
    public IActionResult StaffLogin() => View();

    [HttpPost]
    public IActionResult StaffLogin(string email, string password) => Authenticate(email, password, "Staff");

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    private IActionResult Authenticate(string email, string password, string? expectedRole)
    {
        using var conn = _db.GetConnection();
        conn.Open();

        const string query = """
            SELECT
                u.user_id,
                u.first_name,
                u.last_name,
                u.password_hash,
                r.role_name,
                us.status_name
            FROM users u
            JOIN roles r ON r.role_id = u.role_id
            JOIN user_statuses us ON us.user_status_id = u.user_status_id
            WHERE u.email = @email
            LIMIT 1
            """;

        using var command = new MySqlCommand(query, conn);
        command.Parameters.AddWithValue("@email", email?.Trim() ?? string.Empty);
        using var reader = command.ExecuteReader();

        if (!reader.Read() || !PasswordSecurity.VerifyPassword(
                password ?? string.Empty,
                reader["password_hash"]?.ToString() ?? string.Empty))
        {
            return LoginFailure(expectedRole, "Invalid email or password.");
        }

        string role = reader["role_name"]?.ToString() ?? string.Empty;
        string status = reader["status_name"]?.ToString() ?? string.Empty;

        if (expectedRole is not null && !role.Equals(expectedRole, StringComparison.OrdinalIgnoreCase))
            return LoginFailure(expectedRole, $"This account is not a {expectedRole} account.");

        if (status == "Pending")
            return LoginFailure(expectedRole, "Your account is still pending for staff approval.", "info");

        if (status != "Active")
            return LoginFailure(expectedRole, $"Your account is {status.ToLowerInvariant()}.");

        int userId = Convert.ToInt32(reader["user_id"]);
        string fullName = $"{reader["first_name"]} {reader["last_name"]}".Trim();

        HttpContext.Session.SetInt32("UserId", userId);
        HttpContext.Session.SetString("UserRole", role);
        HttpContext.Session.SetString("UserName", fullName);

        TempData["Msg"] = $"Successfully logged in. Welcome, {fullName}!";
        TempData["Type"] = "success";

        return role switch
        {
            "Owner" => RedirectToAction("Index", "Owner"),
            "Staff" => RedirectToAction("Index", "Staff"),
            _ => RedirectToAction("Index", "Customer")
        };
    }

    private IActionResult LoginFailure(string? expectedRole, string message, string type = "error")
    {
        TempData["Msg"] = message;
        TempData["Type"] = type;
        return expectedRole == "Staff"
            ? RedirectToAction(nameof(StaffLogin))
            : RedirectToAction(nameof(Login));
    }

    internal static (string FirstName, string LastName) SplitFullName(string? fullName)
    {
        string normalized = string.Join(' ', (fullName ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        int separator = normalized.LastIndexOf(' ');

        return separator > 0
            ? (normalized[..separator], normalized[(separator + 1)..])
            : (normalized, "-");
    }
}
