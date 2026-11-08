using System.ComponentModel.DataAnnotations;

namespace EZKLaundrySystem.Models;

public class CustomerRegistration
{
    [Required(ErrorMessage = "First Name is required")]
    [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "Please enter a valid first name")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Last Name is required")]
    [RegularExpression(@"^[a-zA-Z\s'-]+$", ErrorMessage = "Please enter a valid last name")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid Email address")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Contact number is required")]
    [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "Contact must be exactly 11 digits")]
    public string ContactNumber { get; set; } = "";

    [Required(ErrorMessage = "Password is required")]
    [StrongPassword]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Please confirm your password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = "";
}
