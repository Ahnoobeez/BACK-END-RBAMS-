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
    public class UserSignatureController : ControllerBase
    {
        private const long MaxSignatureBytes = 5 * 1024 * 1024;
        private static readonly byte[] PngSignature = { 137, 80, 78, 71, 13, 10, 26, 10 };
        private readonly LocalDbContext _db;

        public UserSignatureController(LocalDbContext db)
        {
            _db = db;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMySignature(CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });
            }

            var signature = await _db.UserSignatures
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.User_ID == userId, cancellationToken);
            if (signature == null)
            {
                return NotFound();
            }

            Response.Headers["Cache-Control"] = "no-store";
            return File(signature.SignatureData, "image/png");
        }

        [HttpPost("me")]
        [RequestSizeLimit(MaxSignatureBytes + 1024)]
        public async Task<IActionResult> SaveMySignature(
            IFormFile? signature,
            CancellationToken cancellationToken)
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized(new { message = "Your session is invalid. Please sign in again." });
            }
            if (signature == null || signature.Length == 0)
            {
                return BadRequest(new { message = "Choose a signature image to upload." });
            }
            if (signature.Length > MaxSignatureBytes)
            {
                return BadRequest(new { message = "The transparent signature image must be 5 MB or smaller." });
            }

            await using var stream = new MemoryStream((int)signature.Length);
            await signature.CopyToAsync(stream, cancellationToken);
            var imageData = stream.ToArray();
            if (imageData.Length < PngSignature.Length ||
                !imageData.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature))
            {
                return BadRequest(new { message = "Upload a valid transparent PNG signature image." });
            }

            var record = await _db.UserSignatures
                .SingleOrDefaultAsync(item => item.User_ID == userId, cancellationToken);
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
            else
            {
                record.SignatureData = imageData;
                record.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Signature saved." });
        }

        private bool TryGetUserId(out int userId) =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
    }
}
