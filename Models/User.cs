using System.ComponentModel.DataAnnotations;

namespace EZKLaundrySystem.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Full Name is required")]
        [RegularExpression(@"^[a-zA-Z\s]+$", ErrorMessage = "Letters and spaces only for Name")]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid Email address")]
        public string Email { get; set; } = "";

        [Required(ErrorMessage = "Password is required")]
        [StrongPassword]
        public string Password { get; set; } = "";

        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; } = ""; // Verification purposes only

        [Required(ErrorMessage = "Contact number is required")]
        [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "Contact must be exactly 11 digits")]
        public string ContactNumber { get; set; } = "";

        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime? CreatedAt { get; set; }
    }
}
