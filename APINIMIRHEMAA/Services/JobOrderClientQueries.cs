using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{
    public static class JobOrderClientQueries
    {
        public static async Task PopulateAsync(
            LocalDbContext db,
            List<JobOrder> jobOrders,
            CancellationToken cancellationToken = default)
        {
            if (jobOrders.Count == 0)
            {
                return;
            }

            var jobOrderIds = jobOrders.Select(jobOrder => jobOrder.JobOrder_ID).ToList();
            var clientDetailsByJobOrderId = await (
                from jobOrder in db.JobOrder.IgnoreQueryFilters().AsNoTracking()
                join conforme in db.Conforme.IgnoreQueryFilters().AsNoTracking()
                    on jobOrder.Conforme_FileID equals conforme.FileID
                join quotation in db.Quotation.IgnoreQueryFilters().AsNoTracking()
                    on conforme.Quotation_ID equals quotation.Quotation_ID
                join client in db.Clients.IgnoreQueryFilters().AsNoTracking()
                    on quotation.Client_ID equals client.Client_ID
                where jobOrderIds.Contains(jobOrder.JobOrder_ID)
                select new
                {
                    jobOrder.JobOrder_ID,
                    client.Client_Name,
                    client.Contact_Person
                }
            ).ToDictionaryAsync(record => record.JobOrder_ID, cancellationToken);

            foreach (var jobOrder in jobOrders)
            {
                if (clientDetailsByJobOrderId.TryGetValue(jobOrder.JobOrder_ID, out var clientDetails))
                {
                    jobOrder.Client_Name = clientDetails.Client_Name;
                    jobOrder.Contact_Person = clientDetails.Contact_Person;
                }
            }
        }
    }
}
