using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.DTO;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserProfileController : ControllerBase
    {
        private const long MaxPhotoBytes = 2 * 1024 * 1024;
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private readonly LocalDbContext _db;

        public UserProfileController(LocalDbContext db)
        {
            _db = db;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });
            }

            var user = await _db.Users.AsNoTracking()
                .SingleOrDefaultAsync(item => item.User_ID == userId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { message = "Your account could not be found." });
            }

            var employee = await _db.Employee.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Employee_Id == user.Employee_ID, cancellationToken);
            if (employee == null)
            {
                return NotFound(new { message = "Your account is not linked to an employee record." });
            }

            return Ok(new
            {
                user.User_ID,
                user.Username,
                user.Email,
                user.Employee_ID,
                employee.FirstName,
                employee.LastName,
                MobileNumber = employee.ContactNumber
            });
        }

        [HttpGet("me/photo")]
        public async Task<IActionResult> GetMyPhoto(CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });
            }

            var photo = await _db.UserProfilePhotos.AsNoTracking()
                .SingleOrDefaultAsync(item => item.User_ID == userId, cancellationToken);
            if (photo == null)
            {
                return NotFound();
            }

            Response.Headers["Cache-Control"] = "no-store";
            return File(photo.PhotoData, photo.ContentType);
        }

        [HttpPut("me")]
        [RequestSizeLimit(MaxPhotoBytes + 128 * 1024)]
        public async Task<IActionResult> SaveMyProfile(
            [FromForm] ProfileUpdateForm form,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });
            }

            var firstName = form.FirstName?.Trim() ?? "";
            var lastName = form.LastName?.Trim() ?? "";
            var email = form.Email?.Trim() ?? "";
            var mobileNumber = form.MobileNumber?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) ||
                string.IsNullOrWhiteSpace(mobileNumber))
            {
                return BadRequest(new { message = "First name, last name, and mobile number are required." });
            }
            if (firstName.Length > 100 || lastName.Length > 100 || mobileNumber.Length > 50)
            {
                return BadRequest(new { message = "One or more profile fields are too long." });
            }
            if (!IsValidProfileName(firstName) || !IsValidProfileName(lastName))
            {
                return BadRequest(new
                {
                    message = "First and last names may contain letters, spaces, hyphens, apostrophes, or periods."
                });
            }
            if (email.Length > 254)
            {
                return BadRequest(new { message = "Email addresses must be 254 characters or fewer." });
            }
            if (!string.IsNullOrWhiteSpace(email) && !MailAddress.TryCreate(email, out _))
            {
                return BadRequest(new { message = "Enter a valid email address." });
            }
            if (!IsValidPhilippineMobileNumber(mobileNumber))
            {
                return BadRequest(new
                {
                    message = "Enter an 11-digit Philippine mobile number starting with 09."
                });
            }
            if (form.Photo != null && form.RemovePhoto)
            {
                return BadRequest(new { message = "Choose a photo or remove the current photo, not both." });
            }
            if (form.Photo != null && (form.Photo.Length == 0 || form.Photo.Length > MaxPhotoBytes))
            {
                return BadRequest(new { message = "Choose a JPG or PNG profile photo no larger than 2 MB." });
            }

            byte[]? photoData = null;
            string? photoContentType = null;
            if (form.Photo != null)
            {
                await using var stream = new MemoryStream((int)form.Photo.Length);
                await form.Photo.CopyToAsync(stream, cancellationToken);
                photoData = stream.ToArray();
                var validationError = UploadValidator.ValidateImage(photoData);
                if (validationError != null)
                {
                    return BadRequest(new { message = "Upload a valid JPG or PNG profile photo." });
                }

                photoContentType = photoData.Length >= PngSignature.Length &&
                    photoData.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature)
                    ? "image/png"
                    : "image/jpeg";
            }

            var user = await _db.Users
                .SingleOrDefaultAsync(item => item.User_ID == userId, cancellationToken);
            if (user == null)
            {
                return NotFound(new { message = "Your account could not be found." });
            }

            var employee = await _db.Employee
                .SingleOrDefaultAsync(item => item.Employee_Id == user.Employee_ID, cancellationToken);
            if (employee == null)
            {
                return NotFound(new { message = "Your account is not linked to an employee record." });
            }

            user.Email = email;
            employee.FirstName = firstName;
            employee.LastName = lastName;
            employee.ContactNumber = mobileNumber;

            var savedPhoto = await _db.UserProfilePhotos
                .SingleOrDefaultAsync(item => item.User_ID == userId, cancellationToken);
            if (photoData != null && photoContentType != null)
            {
                if (savedPhoto == null)
                {
                    _db.UserProfilePhotos.Add(new UserProfilePhoto
                    {
                        User_ID = userId,
                        PhotoData = photoData,
                        ContentType = photoContentType,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    savedPhoto.PhotoData = photoData;
                    savedPhoto.ContentType = photoContentType;
                    savedPhoto.UpdatedAt = DateTime.UtcNow;
                }
            }
            else if (form.RemovePhoto && savedPhoto != null)
            {
                _db.UserProfilePhotos.Remove(savedPhoto);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Profile changes saved successfully." });
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

        private static bool IsValidPhilippineMobileNumber(string value) =>
            Regex.IsMatch(value, @"^09[0-9]{9}$");

        private static bool IsValidProfileName(string value) =>
            Regex.IsMatch(value, @"^[\p{L}\p{M} .'-]+$");
    }
}
