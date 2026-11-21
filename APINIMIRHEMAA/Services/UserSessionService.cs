using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace APINIMIRHEMAA.Services
{

    public sealed class UserSessionService
    {

        public const string SessionIdClaimType = "session_id";

        private static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(3);

        private readonly LocalDbContext _db;


        public UserSessionService(LocalDbContext db)
        {
            _db = db;
        }


        public static string GetSessionId(ClaimsPrincipal user, int userId) =>
            user.FindFirstValue(SessionIdClaimType) ?? $"legacy-{userId}";


        public async Task TouchAsync(
            int userId,
            string sessionId,
            CancellationToken cancellationToken = default)
        {

            if (userId <= 0 || string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 64)
                throw new ArgumentException("The user session identity is invalid.");


            var now = DateTime.UtcNow;

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


        public Task<int> EndAsync(
            int userId,
            string sessionId,
            CancellationToken cancellationToken = default) =>
            _db.UserSessions

                .Where(session => session.User_ID == userId && session.Session_ID == sessionId)

                .ExecuteDeleteAsync(cancellationToken);


        public Task<bool> HasActiveSessionsAsync(
            IReadOnlyCollection<int> userIds,
            CancellationToken cancellationToken = default)
        {

            if (userIds.Count == 0)
                return Task.FromResult(false);


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
