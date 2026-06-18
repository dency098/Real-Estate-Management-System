namespace RealEstateProject.Models
{
    public class RegisterModel
    {
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Password { get; set; } = null!;
        //public string Role { get; set; } = null!; // Buyer, Seller, Admin
        public string? AdminSecret { get; set; } // 🔥 NEW
        public IFormFile? ProfileImageFile { get; set; } // ✅ ADD
        public string? ProfileImage { get; set; } // ✅ used for API
    }
    public class LoginModel
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }
}
