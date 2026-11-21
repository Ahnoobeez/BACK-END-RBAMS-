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

    public class NotificationController : ControllerBase
    {

        private readonly LocalDbContext _localDb;


        public NotificationController(LocalDbContext localDb)
        {
            _localDb = localDb;
        }


        [HttpGet]
        public async Task<ActionResult<List<Notification>>> GetNotifications()
        {

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var notifications = await _localDb.Notifications
                .AsNoTracking()

                .Where(notification => notification.User_ID == userId)

                .OrderByDescending(notification => notification.CreatedAt)

                .ThenByDescending(notification => notification.Notification_ID)
                .ToListAsync();
            return Ok(notifications);
        }


        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var unreadCount = await _localDb.Notifications
                .CountAsync(notification =>
                    notification.User_ID == userId && !notification.IsRead);
            return Ok(new { unreadCount });
        }


        [HttpPatch("{id:int}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {

            if (id <= 0)
            {
                return BadRequest("A valid notification ID is required.");
            }

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var notification = await _localDb.Notifications
                .FirstOrDefaultAsync(record =>
                    record.Notification_ID == id && record.User_ID == userId);

            if (notification == null)
            {
                return NotFound();
            }


            if (!notification.IsRead)
            {
                notification.IsRead = true;

                await _localDb.SaveChangesAsync();
            }
            return NoContent();
        }


        [HttpPatch("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {

            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            var unreadNotifications = await _localDb.Notifications

                .Where(notification =>
                    notification.User_ID == userId && !notification.IsRead)
                .ToListAsync();

            foreach (var notification in unreadNotifications)
            {
                notification.IsRead = true;
            }


            if (unreadNotifications.Count > 0)
            {

                await _localDb.SaveChangesAsync();
            }

            return Ok(new { updatedCount = unreadNotifications.Count });
        }


        private bool TryGetUserId(out int userId)
        {
            return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId)
                && userId > 0;
        }
    }
}
