using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for user session service workflows.
    public sealed class UserSessionService
    {
        // JWT claim name that identifies the session associated with the authenticated user.
        public const string SessionIdClaimType = "session_id";
        // Maximum age of a session heartbeat for it to count as currently active.
        private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(3);
        // Database context used by this class to query and persist application records.
        private readonly LocalDbContext _db;

        // Initializes a new user session service instance.
        public UserSessionService(LocalDbContext db)
        {
            _db = db;
        }

        // Returns the session claim, falling back to a legacy identifier when the claim is absent.
        public static string GetSessionId(ClaimsPrincipal user, int userId) =>
            user.FindFirstValue(SessionIdClaimType) ?? $"legacy-{userId}";

        // Upserts the user session heartbeat, rejects cross-user session IDs, and removes stale sessions.
        public async Task TouchAsync(
            int userId,
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            // Raise an exception when `userId <= 0 || string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 64` is true.
            if (userId <= 0 || string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 64)
                throw new ArgumentException("The user session identity is invalid.");

            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.UtcNow;
            // Compute the retention cutoff used to remove old session heartbeats.
            var staleBefore = now.AddDays(-30);
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE dbo.UserSessions WITH (UPDLOCK, SERIALIZABLE)
                   SET LastSeenUtc = {now}
                   WHERE Session_ID = {sessionId} AND User_ID = {userId};

                   IF @@ROWCOUNT = 0
                   BEGIN
                       IF EXISTS (SELECT 1 FROM dbo.UserSessions WHERE Session_ID = {sessionId})
                           THROW 51000, 'Session identifier is assigned to another user.', 1;

                       INSERT INTO dbo.UserSessions (Session_ID, User_ID, LastSeenUtc)
                       VALUES ({sessionId}, {userId}, {now});
                   END;

                   DELETE FROM dbo.UserSessions
                   WHERE User_ID = {userId} AND LastSeenUtc < {staleBefore};",
                cancellationToken);
        }

        // Deletes the specified session belonging to the user.
        public Task<int> EndAsync(
            int userId,
            string sessionId,
            CancellationToken cancellationToken = default) =>
            _db.UserSessions
                // Apply this predicate in the database query before materializing results.
                .Where(session => session.User_ID == userId && session.Session_ID == sessionId)
                // Delete matching rows in the database without loading them into application memory.
                .ExecuteDeleteAsync(cancellationToken);

        // Checks whether any supplied user has a session seen within the active window.
        public Task<bool> HasActiveSessionsAsync(
            IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default)
        {
            // Exit the current operation when `userIds.Count == 0` is true.
            if (userIds.Count == 0)
                return Task.FromResult(false);

            // Compute the lower time bound used to decide whether a session is still active.
            var activeAfter = DateTime.UtcNow.Subtract(ActiveWindow);
            return _db.UserSessions
                .AsNoTracking()
                .AnyAsync(session =>
                    userIds.Contains(session.User_ID) &&
                    session.LastSeenUtc >= activeAfter,
                    cancellationToken);
        }
    }
}
