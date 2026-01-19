
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Claims;

using Microsoft.EntityFrameworkCore;
using Auction.Models;
using Auction.Dto;
using Auction.Helper;
using Auction;

namespace HallBooking.Controllers
{
    
        public class LoginController : Controller
        {
            private readonly AuctionDbContext _context;
            private readonly IAuthenticationService _authenticationService;
            private readonly JWTSettings _jwtSetting;
            private readonly ILogger _logger;
            public LoginController(AuctionDbContext context, IAuthenticationService authenticationService, ILogger<LoginController> logger, IOptions<JWTSettings> jwtSettings)
            {
                _context = context;
                _authenticationService = authenticationService;
                _jwtSetting = jwtSettings.Value;
                _logger = logger;
            }
            [HttpGet]
            public IActionResult Index()
            {
                return View();
            }

            [HttpPost]
            public async Task<IActionResult> SignIn([FromForm] UserDto user)
            {

                try
                {
                    var IsUser = await _context.Users.Where(x => x.Email == user.Email).FirstOrDefaultAsync();
                    if (IsUser != null)
                    {
                        if (!HashHelper.VerifyPasswordHash(user.Password, IsUser.Password))
                        {
                            TempData["ExceptionMessage"] = "Invalid Password";
                            return RedirectToAction(nameof(Index));
                        }
                        var authenticationProperties = new AuthenticationProperties
                        {
                            IsPersistent = false,
                            ExpiresUtc = DateTime.UtcNow.AddMinutes(50)
                        };
                        await _authenticationService.SignInAsync(
                            HttpContext,
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(new ClaimsIdentity(new[]
                            {
                            new Claim(ClaimTypes.Name, IsUser.Name ?? IsUser.Email ?? ""),
                            new Claim(ClaimTypes.NameIdentifier, IsUser.Id.ToString()),
                            new Claim(ClaimTypes.Role, IsUser.Role)
                            }, CookieAuthenticationDefaults.AuthenticationScheme)),
                            authenticationProperties);

                        return RedirectToAction("Index", "Home");
                    }
                    else
                    {
                        TempData["ExceptionMessage"] = "Account not Found";
                        return RedirectToAction(nameof(Index));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"LoginSignIn: {ex.Message}");
                    TempData["ExceptionMessage"] = "An error occurred while processing your request.";
                    return RedirectToAction(nameof(Index));
                }



            }
            [HttpPost]
            public async Task<IActionResult> SignUp([FromForm] UserDto user)
            {
                if (!ModelState.IsValid)
                {
                    TempData["ExceptionMessage"] = "Some Entries are Invalid";
                    return RedirectToAction(nameof(Index));
                }
                try
                {
                    // Default role for new users is "Bidder"
                    var role = "Bidder";
                    var existingByEmail = await _context.Users.FirstOrDefaultAsync(x => x.Email == user.Email);
                    if (existingByEmail == null)
                    {
                        HashHelper.CreatePasswordHash(user.Password, out var hash);
                        if (!Directory.Exists("wwwroot/Image/profile"))
                        {
                            Directory.CreateDirectory("wwwroot/Image/profile");
                        }
                        var imagePath = " ";

                        var newUser = new User
                        {
                        
                            Name = user.Name,
                            Email = user.Email,
                            Password = hash,
                            Role = role
                        };
                        _context.Users.Add(newUser);
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Account created successfully. Please Login";
                        return RedirectToAction("Index", "Login");
                    }
                    else if (!existingByEmail.IsActive)
                    {
                        // Reactivate inactive account
                        HashHelper.CreatePasswordHash(user.Password, out var hash);
                     
                        existingByEmail.Name = user.Name;
                        existingByEmail.Password = hash;
                        existingByEmail.Role = role;
                        existingByEmail.IsActive = true;
                        existingByEmail.UpdatedAt = DateTime.Now;
                        await _context.SaveChangesAsync();
                        TempData["SuccessMessage"] = "Your account was reactivated. Please login.";
                        return RedirectToAction("Index", "Login");
                    }
                    else
                    {
                        TempData["ExceptionMessage"] = "Email is already registered.";
                        return RedirectToAction(nameof(Index));
                    }


                }
                catch (Exception ex)
                {
                    _logger.LogError($"LoginSignUp: {ex.Message}");
                    TempData["ExceptionMessage"] = "An error occurred while processing your request.";
                    return RedirectToAction(nameof(Index));

                }

            }

            [HttpPost]
            public async Task<IActionResult> Logout()
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["SuccessMessage"] = "You have been logged out successfully.";
                return RedirectToAction("Index", "Home");
            }
        
    }
}
