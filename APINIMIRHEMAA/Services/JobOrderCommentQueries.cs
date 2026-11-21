using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{

    public sealed record JobOrderCommentPage(IReadOnlyList<JobOrderComment> Comments, bool HasMore);


    public static class JobOrderCommentQueries
    {

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

                .Where(comment => comment.JobOrder_ID == jobOrderId);


            if (afterCommentId.HasValue)
            {
                var newerComments = await query

                    .Where(comment => comment.JobOrderComment_ID > afterCommentId.Value)

                    .OrderBy(comment => comment.JobOrderComment_ID)
                    .Take(pageSize + 1)
                    .ToListAsync(cancellationToken);
                var hasMore = newerComments.Count > pageSize;
                return new JobOrderCommentPage(newerComments.Take(pageSize).ToList(), hasMore);
            }


            if (beforeCommentId.HasValue)
            {

                query = query.Where(comment => comment.JobOrderComment_ID < beforeCommentId.Value);
            }

            var olderComments = await query

                .OrderByDescending(comment => comment.JobOrderComment_ID)
                .Take(pageSize + 1)
                .ToListAsync(cancellationToken);
            var hasEarlierComments = olderComments.Count > pageSize;
            var page = olderComments
                .Take(pageSize)

                .OrderBy(comment => comment.JobOrderComment_ID)
                .ToList();
            return new JobOrderCommentPage(page, hasEarlierComments);
        }
    }
}
