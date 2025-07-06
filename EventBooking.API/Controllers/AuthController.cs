using EventBooking.API.Data;
using EventBooking.API.DTOs;
using EventBooking.API.Models;
using EventBooking.API.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace EventBooking.API.Controllers
{
    [AllowAnonymous]
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _context;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            AppDbContext context)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [HttpPost("create-admin")]
        public async Task<IActionResult> CreateAdmin([FromBody] RegisterDTO dto)
        {
            // Check if an admin user already exists
            var existingAdmin = await _userManager.GetUsersInRoleAsync("Admin");
            if (existingAdmin.Any())
            {
                return BadRequest("Admin user already exists");
            }

            var user = new ApplicationUser
            {
                FullName = dto.FullName,
                Email = dto.Email,
                UserName = dto.Email,
                Role = "Admin"
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(user, "Admin");

            return Ok("Admin user created successfully");
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Check if user already exists
            var existingUser = await _userManager.FindByEmailAsync(dto.Email);
            if (existingUser != null)
                return BadRequest("User with this email already exists");

            var user = new ApplicationUser
            {
                FullName = dto.FullName,
                Email = dto.Email,
                UserName = dto.Email,
                Role = dto.Role
            };

            var result = await _userManager.CreateAsync(user, dto.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            if (dto.Role == "Organizer")
            {
                var organizer = new Organizer
                {
                    Name = user.FullName,
                    ContactEmail = user.Email,
                    PhoneNumber = dto.PhoneNumber ?? "",
                    OrganizationName = dto.OrganizationName,
                    Website = dto.Website,
                    UserId = user.Id
                };

                _context.Organizers.Add(organizer);
                await _context.SaveChangesAsync();
            }

            await _userManager.AddToRoleAsync(user, dto.Role);

            return Ok(new { 
                message = "User registered successfully",
                userId = user.Id,
                role = dto.Role
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
                return Unauthorized("Invalid email or password");

            if (!await _userManager.CheckPasswordAsync(user, model.Password))
                return Unauthorized("Invalid email or password");

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Add user roles to claims
            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var role in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            var token = CreateToken(authClaims);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo,
                user = new
                {
                    id = user.Id,
                    email = user.Email,
                    fullName = user.FullName,
                    roles = userRoles
                }
            });
        }

        private JwtSecurityToken CreateToken(List<Claim> authClaims)
        {
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
                throw new InvalidOperationException("JWT Key is not configured");

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            if (authSigningKey.KeySize < 256)
                throw new InvalidOperationException("JWT Key must be at least 32 bytes long");

            var tokenExpirationMinutes = _configuration["Jwt:ExpiryInMinutes"];
            if (!double.TryParse(tokenExpirationMinutes, out double expiryMinutes))
                expiryMinutes = 60; // Default to 60 minutes if not configured

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                expires: DateTime.Now.AddMinutes(expiryMinutes),
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            return token;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            var userRoles = await _userManager.GetRolesAsync(user);

            var response = new
            {
                id = user.Id,
                email = user.Email,
                fullName = user.FullName,
                role = user.Role,
                roles = userRoles
            };

            // If user is an organizer, include organizer details
            if (user.Role == "Organizer")
            {
                var organizer = await _context.Organizers
                    .FirstOrDefaultAsync(o => o.UserId == user.Id);

                if (organizer != null)
                {
                    return Ok(new
                    {
                        user = response,
                        organizer = new
                        {
                            id = organizer.Id,
                            name = organizer.Name,
                            organizationName = organizer.OrganizationName,
                            phoneNumber = organizer.PhoneNumber,
                            website = organizer.Website,
                            isVerified = organizer.IsVerified,
                            createdAt = organizer.CreatedAt
                        }
                    });
                }
            }

            return Ok(new { user = response });
        }

        [HttpPut("organizer/profile")]
        [Authorize(Roles = "Organizer")]
        public async Task<IActionResult> UpdateOrganizerProfile([FromBody] UpdateOrganizerProfileDTO dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
                return Unauthorized();

            var organizer = await _context.Organizers
                .FirstOrDefaultAsync(o => o.UserId == userId);

            if (organizer == null)
                return NotFound("Organizer profile not found");

            organizer.Name = dto.Name ?? organizer.Name;
            organizer.OrganizationName = dto.OrganizationName ?? organizer.OrganizationName;
            organizer.PhoneNumber = dto.PhoneNumber ?? organizer.PhoneNumber;
            organizer.Website = dto.Website ?? organizer.Website;
            organizer.FacebookUrl = dto.FacebookUrl ?? organizer.FacebookUrl;
            organizer.YoutubeUrl = dto.YoutubeUrl ?? organizer.YoutubeUrl;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Profile updated successfully" });
        }
    }
}
