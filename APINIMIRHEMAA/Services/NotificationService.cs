using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for notification service workflows.
    public sealed class NotificationService
    {
        private static readonly string[] JobOrderServiceKeys =
        {
            "marketing.job-order",
            "technical.job-order",
            "production.job-order"
        };

        // Injected or cached local db dependency/state used by this class.
        private readonly LocalDbContext _localDb;
        // Injected or cached access permissions dependency/state used by this class.
        private readonly AccessPermissionService _accessPermissions;
        // Logger used to record this class’s operational events and failures.
        private readonly ILogger<NotificationService> _logger;

        // Initializes a new notification service instance.
        public NotificationService(
            LocalDbContext localDb,
            AccessPermissionService accessPermissions,
            ILogger<NotificationService> logger)
        {
            _localDb = localDb;
            _accessPermissions = accessPermissions;
            _logger = logger;
        }

        // Notifies the relevant recipients about the job order readers.
        public async Task NotifyJobOrderReadersAsync(
            int jobOrderId,
            CancellationToken cancellationToken = default)
        {
            // Raise an exception when `jobOrderId <= 0` is true.
            if (jobOrderId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(jobOrderId));
            }

            var recipientIds = await GetJobOrderReaderIdsAsync(cancellationToken);

            // Enter this branch when `recipientIds.Count == 0` is true.
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

        // Notifies the relevant recipients about the job order comment.
        public async Task NotifyJobOrderCommentAsync(
            JobOrderComment comment,
            CancellationToken cancellationToken = default)
        {
            // Raise an exception when `comment == null` is true.
            if (comment == null)
            {
                throw new ArgumentNullException(nameof(comment));
            }
            // Raise an exception when `comment.JobOrderComment_ID <= 0 || comment.JobOrder_ID <= 0` is true.
            if (comment.JobOrderComment_ID <= 0 || comment.JobOrder_ID <= 0)
            {
                throw new ArgumentException("A saved Job Order comment is required.", nameof(comment));
            }

            var recipientIds = await GetJobOrderReaderIdsAsync(cancellationToken);
            // Enter this branch when `recipientIds.Count == 0` is true.
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

        // Retrieves the job order reader ids.
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

            // Process each `user` returned by the collection expression in the loop header.
            foreach (var user in activeUsers)
            {
                // Enter this branch when `ModuleAccess.CanAccess(user.Role_Name, user.Department_Name, )` is true.
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
                // Enter this branch when `permissions.Any(permission => permission.ActionKey == && JobOrderServiceKeys.Contains(permission.ServiceKey, StringComparer.Ordinal) && permission.Allowed)` is true.
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

        // Notifies the relevant recipients about the material requisition submitted.
        public async Task NotifyMaterialRequisitionSubmittedAsync(
            IEnumerable<MaterialRequesition_Slip> requisitions,
            CancellationToken cancellationToken = default)
        {
            // Apply this predicate in the database query before materializing results.
            var records = requisitions.Where(requisition => requisition.MR_ID > 0).ToList();
            // Exit the current operation when `records.Count == 0` is true.
            if (records.Count == 0)
            {
                return;
            }

            var recipientIds = await GetWarehouseDepartmentHeadIdsAsync(cancellationToken);
            // Enter this branch when `recipientIds.Count == 0` is true.
            if (recipientIds.Count == 0)
            {
                _logger.LogWarning(
                    "No active Inventory Department Manager has approval access for Material Requisitions.");
                return;
            }

            var submitterIds = records
                // Apply this predicate in the database query before materializing results.
                .Where(requisition => requisition.CreatedByUserId.HasValue)
                .Select(requisition => requisition.CreatedByUserId!.Value)
                .ToHashSet();
            recipientIds.RemoveAll(userId => submitterIds.Contains(userId));
            // Enter this branch when `recipientIds.Count == 0` is true.
            if (recipientIds.Count == 0)
            {
                _logger.LogWarning(
                    "All Inventory Department Managers eligible to review the requisition also submitted it.");
                return;
            }

            var drafts = new List<NotificationDraft>();
            // Process each `group` returned by the collection expression in the loop header.
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
                    // Apply the ordering expression here before returning or materializing the results.
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

        // Notifies the relevant recipients about the material requisition decision.
        public async Task NotifyMaterialRequisitionDecisionAsync(
            IEnumerable<MaterialRequesition_Slip> requisitions,
            string status,
            CancellationToken cancellationToken = default)
        {
            // Raise an exception when `!string.Equals(status, , StringComparison.OrdinalIgnoreCase) && !string.Equals(status, , StringComparison.OrdinalIgnoreCase)` is true.
            if (!string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(status, "Rejected", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Material requisition status must be Approved or Rejected.",
                    nameof(status));
            }

            var records = requisitions
                // Apply this predicate in the database query before materializing results.
                .Where(requisition => requisition.CreatedByUserId.HasValue)
                .ToList();
            // Enter this branch when `records.Count == 0` is true.
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
                    // Creates a notification draft for each requisition group and its outcome.
                    var requisitionIds = string.Join(
                        ", ",
                        // Apply the ordering expression here before returning or materializing the results.
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

        // Retrieves the warehouse department head ids.
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
            // Process each `userId` returned by the collection expression in the loop header.
            foreach (var userId in candidates)
            {
                // Enter this branch when `await _accessPermissions.IsAllowedAsync( userId, , , cancellationToken)` is true.
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

        // Adds the notifications.
        private async Task AddNotificationsAsync(
            IEnumerable<NotificationDraft> drafts,
            CancellationToken cancellationToken,
            bool skipPreviouslyCreated = true)
        {
            var uniqueDrafts = new Dictionary<(int UserId, string Type, string Message), NotificationDraft>();
            // Process each `draft` returned by the collection expression in the loop header.
            foreach (var draft in drafts)
            {
                uniqueDrafts.TryAdd((draft.UserId, draft.Type, draft.Message), draft);
            }
            // Exit the current operation when `uniqueDrafts.Count == 0` is true.
            if (uniqueDrafts.Count == 0)
            {
                return;
            }

            var userIds = uniqueDrafts.Keys.Select(key => key.UserId).Distinct().ToList();
            var types = uniqueDrafts.Keys.Select(key => key.Type).Distinct().ToList();
            var messages = uniqueDrafts.Keys.Select(key => key.Message).Distinct().ToList();
            var existingKeys = new HashSet<(int User_ID, string Type, string Message)>();
            // Enter this branch when `skipPreviouslyCreated` is true.
            if (skipPreviouslyCreated)
            {
                var existing = await _localDb.Notifications
                    .AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
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
                // Apply this predicate in the database query before materializing results.
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
            // Exit the current operation when `newNotifications.Count == 0` is true.
            if (newNotifications.Count == 0)
            {
                return;
            }

            _localDb.Notifications.AddRange(newNotifications);
            // Persist the pending entity changes to the database at this point.
            await _localDb.SaveChangesAsync(cancellationToken);
        }

        // Represents a notification to create, including its recipient, message, and type.
        private sealed record NotificationDraft(int UserId, string Title, string Message, string Type);
    }
}
