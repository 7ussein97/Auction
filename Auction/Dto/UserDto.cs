using System.ComponentModel.DataAnnotations;

namespace Auction.Dto
{
    
        public class UserDto
        {
            public int Id { get; set; }
            [Required(ErrorMessage = "civil is required.")]
            
            public string? Name { get; set; }
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Invalid email format.")]
            public string? Email { get; set; }
            [Required(ErrorMessage = "Password is required.")]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[\W_]).{8,}$",
               ErrorMessage = "Must contain an uppercase and lowercase letters && a special character")]
            public string? Password { get; set; }
            [Required(ErrorMessage = "Role is required.")]
            public string? Role { get; set; }
        
    }
}
