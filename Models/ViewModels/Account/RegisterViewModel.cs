using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models.ViewModels.Account
{
    /// <summary>
    /// Data the Register form collects. Every new registrant becomes a Guest.
    /// </summary>
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Please enter your first name.")]
        [StringLength(50)]
        [Display(Name = "First name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your last name.")]
        [StringLength(50)]
        [Display(Name = "Last name")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone number (optional)")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Please choose a password.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "The password must be at least {2} characters long.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
