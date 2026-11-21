using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APINIMIRHEMAA.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    // Coordinates the user signature controller API endpoints, including request handling and response generation.
    public class UserSignatureController : ControllerBase
    {
        // Maximum accepted signature upload size; the upload endpoint applies this limit before buffering the file.
        private const long MaxSignatureBytes = 5 * 1024 * 1024;
        // PNG file signature bytes used to reject uploads whose content is not a PNG image.
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        // Database context used by this class to query and persist application records.
        private readonly LocalDbContext _db;

        // Initializes a new user signature controller instance.
        public UserSignatureController(LocalDbContext db)
        {
            _db = db;
        }

        /// <summary>Retrieves the my signature.</summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetMySignature(CancellationToken cancellationToken)
        {
            // Return HTTP 401 Unauthorized when `!TryGetUserId(out var userId)` is true.
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });
            }

            var signature = await _db.UserSignatures
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.User_ID == userId, cancellationToken);
            // Return HTTP 404 Not Found when `signature == null` is true.
            if (signature == null)
            {
                return NotFound();
            }

            Response.Headers["Cache-Control"] = "no-store";
            return File(signature.SignatureData, "image/png");
        }

        /// <summary>Persists the my signature.</summary>
        [HttpPost("me")]
        [RequestSizeLimit(MaxSignatureBytes + 1024)]
        public async Task<IActionResult> SaveMySignature(
            IFormFile? signature,
            CancellationToken cancellationToken)
        {
            // Return HTTP 401 Unauthorized when `!TryGetUserId(out var userId)` is true.
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });
            }
            // Return HTTP 400 Bad Request when `signature == null || signature.Length == 0` is true.
            if (signature == null || signature.Length == 0)
            {
                return BadRequest(new { message = "Choose a signature image to upload." });
            }
            // Return HTTP 400 Bad Request when `signature.Length > MaxSignatureBytes` is true.
            if (signature.Length > MaxSignatureBytes)
            {
                return BadRequest(new { message = "The transparent signature image must be 5 MB or smaller." });
            }

            await using var stream = new MemoryStream((int)signature.Length);
            await signature.CopyToAsync(stream, cancellationToken);
            // Buffer the uploaded bytes so the PNG signature can be checked before the record is saved.
            var imageData = stream.ToArray();
            // Return HTTP 400 Bad Request when `imageData.Length < PngSignature.Length || !imageData.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature)` is true.
            if (imageData.Length < PngSignature.Length ||
                !imageData.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
            {
                return BadRequest(new { message = "Upload a valid transparent PNG signature image." });
            }

            var record = await _db.UserSignatures
                .SingleOrDefaultAsync(item => item.User_ID == userId, cancellationToken);
            // Enter this branch when `record == null` is true.
            if (record == null)
            {
                record = new UserSignature
                {
                    User_ID = userId,
                    SignatureData = imageData,
                    UpdatedAt = DateTime.UtcNow
                };
                _db.UserSignatures.Add(record);
            }
            // Use this fallback path when the preceding condition is false.
            else
            {
                record.SignatureData = imageData;
                record.UpdatedAt = DateTime.UtcNow;
            }

            // Persist the pending entity changes to the database at this point.
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Signature saved." });
        }

        // Reads the authenticated user ID from the claims principal.
        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
