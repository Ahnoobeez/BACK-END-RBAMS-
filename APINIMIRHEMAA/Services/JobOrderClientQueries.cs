using APINIMIRHEMAA.Data;
using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for job order client queries workflows.
    public static class JobOrderClientQueries
    {
        // Adds client details to each job-order query result.
        public static async Task PopulateAsync(
            LocalDbContext db,
            List<JobOrder> jobOrders,
            CancellationToken cancellationToken = default)
        {
            // Exit the current operation when `jobOrders.Count == 0` is true.
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

            // Process each `jobOrder` returned by the collection expression in the loop header.
            foreach (var jobOrder in jobOrders)
            {
                // Enter this branch when `clientDetailsByJobOrderId.TryGetValue(jobOrder.JobOrder_ID, out var clientDetails)` is true.
                if (clientDetailsByJobOrderId.TryGetValue(jobOrder.JobOrder_ID, out var clientDetails))
                {
                    jobOrder.Client_Name = clientDetails.Client_Name;
                    jobOrder.Contact_Person = clientDetails.Contact_Person;
                }
            }
        }
    }
}
