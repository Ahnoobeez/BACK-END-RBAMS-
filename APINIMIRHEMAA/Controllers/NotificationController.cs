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
    // Coordinates the notification controller API endpoints, including request handling and response generation.
    public class NotificationController : ControllerBase
    {
        // Injected or cached local db dependency/state used by this class.
        private readonly LocalDbContext _localDb;

        // Initializes a new notification controller instance.
        public NotificationController(LocalDbContext localDb)
        {
            _localDb = localDb;
        }

        /// <summary>Retrieves the notifications.</summary>
        [HttpGet]
        public async Task<ActionResult<List<Notification>>> GetNotifications()
        {
            // Return HTTP 401 Unauthorized when `!TryGetUserId(out var userId)` is true.
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var notifications = await _localDb.Notifications
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(notification => notification.User_ID == userId)
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(notification => notification.CreatedAt)
                // Apply the ordering expression here before returning or materializing the results.
                .ThenByDescending(notification => notification.Notification_ID)
                .ToListAsync();
            return Ok(notifications);
        }

        /// <summary>Retrieves the unread count.</summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            // Return HTTP 401 Unauthorized when `!TryGetUserId(out var userId)` is true.
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var unreadCount = await _localDb.Notifications
                .CountAsync(notification =>
                    notification.User_ID == userId && !notification.IsRead);
            return Ok(new { unreadCount });
        }

        /// <summary>Marks the specified notification as read for the current user.</summary>
        [HttpPatch("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            // Return HTTP 400 Bad Request when `id <= 0` is true.
            if (id <= 0)
            {
                return BadRequest("A valid notification ID is required.");
            }
            // Return HTTP 401 Unauthorized when `!TryGetUserId(out var userId)` is true.
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var notification = await _localDb.Notifications
                .FirstOrDefaultAsync(record =>
                    record.Notification_ID == id && record.User_ID == userId);
            // Return HTTP 404 Not Found when `notification == null` is true.
            if (notification == null)
            {
                return NotFound();
            }

            // Enter this branch when `!notification.IsRead` is true.
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                // Persist the pending entity changes to the database at this point.
                await _localDb.SaveChangesAsync();
            }
            return NoContent();
        }

        /// <summary>Marks all of the current user's notifications as read.</summary>
        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            // Return HTTP 401 Unauthorized when `!TryGetUserId(out var userId)` is true.
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var unreadNotifications = await _localDb.Notifications
                // Apply this predicate in the database query before materializing results.
                .Where(notification =>
                    notification.User_ID == userId && !notification.IsRead)
                .ToListAsync();
            // Process each `notification` returned by the collection expression in the loop header.
            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }

            // Enter this branch when `unreadNotifications.Count > 0` is true.
            if (unreadNotifications.Count > 0)
            {
                // Persist the pending entity changes to the database at this point.
                await _localDb.SaveChangesAsync();
            }

            return Ok(new { updatedCount = unreadNotifications.Count });
        }

        // Reads the authenticated user ID from the claims principal.
        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
                && userId > 0;
        }
    }
}
