using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{

    public sealed class NotificationService
    {
        private static readonly string[] JobOrderServiceKeys =
        {
            "marketing.job-order",
            "technical.job-order",
            "production.job-order"
        };


        private readonly LocalDbContext _localDb;

        private readonly AccessPermissionService _accessPermissions;

        private readonly ILogger<NotificationService> _logger;


        public NotificationService(
            LocalDbContext localDb,
            AccessPermissionService accessPermissions,
            ILogger<NotificationService> logger)
        {
            _localDb = localDb;
            _accessPermissions = accessPermissions;
            _logger = logger;
        }


        public async Task NotifyJobOrderReadersAsync(
            int jobOrderId,
            CancellationToken cancellationToken = default)
        {

            if (jobOrderId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(jobOrderId));
            }

            var recipientIds = await GetJobOrderReaderIdsAsync(cancellationToken);


            if (recipientIds.Count == 0)
            {
                _logger.LogWarning(
                    "No active users have access to a Job Order form for Job Order {JobOrderId}.",
                    jobOrderId);
                return;
            }

            await AddNotificationsAsync(
                recipientIds.Select(userId => new NotificationDraft(
                    userId,
                    "New Job Order",
                    $"Job Order #{jobOrderId} has been created.",
                    "JobOrder")),
                cancellationToken);
        }


        public async Task NotifyJobOrderCommentAsync(
            JobOrderComment comment,
            CancellationToken cancellationToken = default)
        {

            if (comment == null)
            {
                throw new ArgumentNullException(nameof(comment));
            }

            if (comment.JobOrderComment_ID <= 0 || comment.JobOrder_ID <= 0)
            {
                throw new ArgumentException("A saved Job Order comment is required.", nameof(comment));
            }

            var recipientIds = await GetJobOrderReaderIdsAsync(cancellationToken);

            if (recipientIds.Count == 0)
            {
                _logger.LogWarning(
                    "No active users have access to Job Order comments for Job Order {JobOrderId}.",
                    comment.JobOrder_ID);
                return;
            }

            var employeeName = string.IsNullOrWhiteSpace(comment.Employee_Name)
                ? "An employee"
                : comment.Employee_Name.Trim();
            var message =
                $"Job Order #{comment.JobOrder_ID} has a new comment from {employeeName}.";
            await AddNotificationsAsync(
                recipientIds.Select(userId => new NotificationDraft(
                    userId,
                    "New Job Order Comment",
                    message,
                    "JobOrderComment")),
                cancellationToken,
                skipPreviouslyCreated: false);
        }


        private async Task<List<int>> GetJobOrderReaderIdsAsync(CancellationToken cancellationToken)
        {
            var activeUsers = await (
                from user in _localDb.Users.AsNoTracking()
                join role in _localDb.Roles.AsNoTracking() on user.Role_ID equals role.Role_ID
                join department in _localDb.Departments.AsNoTracking()
                    on user.Department_ID equals department.Department_ID
                where user.IsActive
                select new
                {
                    user.User_ID,
                    role.Role_Name,
                    department.Department_Name
                }
            ).ToListAsync(cancellationToken);
            var recipientIds = new List<int>();


            foreach (var user in activeUsers)
            {

                if (ModuleAccess.CanAccess(user.Role_Name, user.Department_Name, "admin"))
                {
                    recipientIds.Add(user.User_ID);
                    continue;
                }

                var permissions = await _accessPermissions.GetEffectivePermissionsAsync(
                    user.User_ID,
                    null,
                    null,
                    cancellationToken);

                if (permissions.Any(permission =>
                    permission.ActionKey == "Read" &&
                    JobOrderServiceKeys.Contains(permission.ServiceKey, StringComparer.Ordinal) &&
                    permission.Allowed))
                {
                    recipientIds.Add(user.User_ID);
                }
            }

            return recipientIds;
        }


        public async Task NotifyMaterialRequisitionSubmittedAsync(
            IEnumerable<MaterialRequesition_Slip> requisitions,
            CancellationToken cancellationToken = default)
        {

            var records = requisitions.Where(requisition => requisition.MR_ID > 0).ToList();

            if (records.Count == 0)
            {
                return;
            }

            var recipientIds = await GetWarehouseDepartmentHeadIdsAsync(cancellationToken);

            if (recipientIds.Count == 0)
            {
                _logger.LogWarning(
                    "No active Inventory Department Manager has approval access for Material Requisitions.");
                return;
            }

            var submitterIds = records

                .Where(requisition => requisition.CreatedByUserId.HasValue)
                .Select(requisition => requisition.CreatedByUserId!.Value)
                .ToHashSet();
            recipientIds.RemoveAll(userId => submitterIds.Contains(userId));

            if (recipientIds.Count == 0)
            {
                _logger.LogWarning(
                    "All Inventory Department Managers eligible to review the requisition also submitted it.");
                return;
            }

            var drafts = new List<NotificationDraft>();

            foreach (var group in records.GroupBy(requisition => new
            {
                ControlNumber = string.IsNullOrWhiteSpace(requisition.Control_Number)
                    ? $"MR #{requisition.MR_ID}"
                    : requisition.Control_Number.Trim(),
                Source = string.IsNullOrWhiteSpace(requisition.FromWho)
                    ? "an employee"
                    : requisition.FromWho.Trim()
            }))
            {
                var requisitionIds = string.Join(
                    ", ",

                    group.Select(requisition => requisition.MR_ID).Distinct().OrderBy(id => id));
                var message =
                    $"Material requisition {group.Key.ControlNumber} from {group.Key.Source} " +
                    $"is waiting for review. MR IDs: {requisitionIds}.";
                drafts.AddRange(recipientIds.Select(userId => new NotificationDraft(
                    userId,
                    "Material Requisition Requires Review",
                    message,
                    "MaterialRequisitionPending")));
            }

            await AddNotificationsAsync(drafts, cancellationToken);
        }


        public async Task NotifyMaterialRequisitionDecisionAsync(
            IEnumerable<MaterialRequesition_Slip> requisitions,
            string status,
            CancellationToken cancellationToken = default)
        {

            if (!string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Material requisition status must be Approved or Rejected.",
                    nameof(status));
            }

            var records = requisitions

                .Where(requisition => requisition.CreatedByUserId.HasValue)
                .ToList();

            if (records.Count == 0)
            {
                _logger.LogWarning(
                    "No requester identity was recorded for the {Status} material requisitions.",
                    status);
                return;
            }

            var drafts = records
                .GroupBy(requisition => new
                {
                    UserId = requisition.CreatedByUserId!.Value,
                    ControlNumber = string.IsNullOrWhiteSpace(requisition.Control_Number)
                        ? $"MR #{requisition.MR_ID}"
                        : requisition.Control_Number.Trim()
                })
                .Select(group =>
                {

                    var requisitionIds = string.Join(
                        ", ",

                        group.Select(requisition => requisition.MR_ID).Distinct().OrderBy(id => id));
                    var normalizedStatus = status.Equals("Approved", StringComparison.OrdinalIgnoreCase)
                        ? "approved"
                        : "rejected";
                    return new NotificationDraft(
                        group.Key.UserId,
                        $"Material Requisition {char.ToUpperInvariant(normalizedStatus[0])}{normalizedStatus[1..]}",
                        $"Your material requisition {group.Key.ControlNumber} was {normalizedStatus} " +
                        $"by the Warehouse & Inventory department head. MR IDs: {requisitionIds}.",
                        "MaterialRequisitionDecision");
                })
                .ToList();

            await AddNotificationsAsync(drafts, cancellationToken);
        }


        private async Task<List<int>> GetWarehouseDepartmentHeadIdsAsync(
            CancellationToken cancellationToken)
        {
            var candidates = await (
                from user in _localDb.Users.AsNoTracking()
                join role in _localDb.Roles.AsNoTracking() on user.Role_ID equals role.Role_ID
                join department in _localDb.Departments.AsNoTracking()
                    on user.Department_ID equals department.Department_ID
                where user.IsActive &&
                    role.Role_Name.Trim().ToLower() == "department manager" &&
                    department.Department_Name.Trim().ToLower() == "inventory"
                select user.User_ID
            ).Distinct().ToListAsync(cancellationToken);

            var recipients = new List<int>();

            foreach (var userId in candidates)
            {

                if (await _accessPermissions.IsAllowedAsync(
                    userId,
                    "warehouse.material-requisition",
                    "Approve",
                    cancellationToken))
                {
                    recipients.Add(userId);
                }
            }

            return recipients;
        }


        private async Task AddNotificationsAsync(
            IEnumerable<NotificationDraft> drafts,
            CancellationToken cancellationToken,
            bool skipPreviouslyCreated = true)
        {
            var uniqueDrafts = new Dictionary<(int UserId, string Type, string Message), NotificationDraft>();

            foreach (var draft in drafts)
            {
                uniqueDrafts.TryAdd((draft.UserId, draft.Type, draft.Message), draft);
            }

            if (uniqueDrafts.Count == 0)
            {
                return;
            }

            var userIds = uniqueDrafts.Keys.Select(key => key.UserId).Distinct().ToList();
            var types = uniqueDrafts.Keys.Select(key => key.Type).Distinct().ToList();
            var messages = uniqueDrafts.Keys.Select(key => key.Message).Distinct().ToList();
            var existingKeys = new HashSet<(int User_ID, string Type, string Message)>();

            if (skipPreviouslyCreated)
            {
                var existing = await _localDb.Notifications
                    .AsNoTracking()

                    .Where(notification =>
                        userIds.Contains(notification.User_ID) &&
                        types.Contains(notification.Type) &&
                        messages.Contains(notification.Message))
                    .Select(notification => new
                    {
                        notification.User_ID,
                        notification.Type,
                        notification.Message
                    })
                    .ToListAsync(cancellationToken);
                existingKeys = existing
                    .Select(notification => (
                        notification.User_ID,
                        notification.Type,
                        notification.Message))
                    .ToHashSet();
            }

            var createdAt = DateTime.UtcNow;
            var newNotifications = uniqueDrafts

                .Where(entry => !existingKeys.Contains(entry.Key))
                .Select(entry => new Notification
                {
                    User_ID = entry.Value.UserId,
                    Title = entry.Value.Title,
                    Message = entry.Value.Message,
                    Type = entry.Value.Type,
                    IsRead = false,
                    CreatedAt = createdAt
                })
                .ToList();

            if (newNotifications.Count == 0)
            {
                return;
            }

            _localDb.Notifications.AddRange(newNotifications);

            await _localDb.SaveChangesAsync(cancellationToken);
        }


        private sealed record NotificationDraft(int UserId, string Title, string Message, string Type);
    }
}
