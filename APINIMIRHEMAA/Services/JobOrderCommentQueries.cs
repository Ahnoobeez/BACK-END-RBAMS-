using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{
    // Represents a page of job-order comments and whether more comments are available.
    public sealed record JobOrderCommentPage(IReadOnlyList<JobOrderComment> Comments, bool HasMore);

    // Provides reusable application operations for job order comment queries workflows.
    public static class JobOrderCommentQueries
    {
        // Retrieves a page of comments for the job order, using the supplied comment cursor when present.
        public static async Task<JobOrderCommentPage> GetPageAsync(
            LocalDbContext db,
            int jobOrderId,
            int? beforeCommentId,
            int? afterCommentId,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var query = db.JobOrderComments
                .IgnoreQueryFilters()
                .AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(comment => comment.JobOrder_ID == jobOrderId);

            // Enter this branch when `afterCommentId.HasValue` is true.
            if (afterCommentId.HasValue)
            {
                var newerComments = await query
                    // Apply this predicate in the database query before materializing results.
                    .Where(comment => comment.JobOrderComment_ID > afterCommentId.Value)
                    // Apply the ordering expression here before returning or materializing the results.
                    .OrderBy(comment => comment.JobOrderComment_ID)
                    .Take(pageSize + 1)
                    .ToListAsync(cancellationToken);
                var hasMore = newerComments.Count > pageSize;
                return new JobOrderCommentPage(newerComments.Take(pageSize).ToList(), hasMore);
            }

            // Enter this branch when `beforeCommentId.HasValue` is true.
            if (beforeCommentId.HasValue)
            {
                // Apply this predicate in the database query before materializing results.
                query = query.Where(comment => comment.JobOrderComment_ID < beforeCommentId.Value);
            }

            var olderComments = await query
                // Apply the ordering expression here before returning or materializing the results.
                .OrderByDescending(comment => comment.JobOrderComment_ID)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);
            var hasEarlierComments = olderComments.Count > pageSize;
            var page = olderComments
                .Take(pageSize)
                // Apply the ordering expression here before returning or materializing the results.
                .OrderBy(comment => comment.JobOrderComment_ID)
                .ToList();
            return new JobOrderCommentPage(page, hasEarlierComments);
        }
    }
}
