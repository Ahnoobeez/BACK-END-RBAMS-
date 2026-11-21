using APINIMIRHEMAA.Data;

namespace APINIMIRHEMAA.Services
{
    // Provides reusable application operations for workflow service workflows.
    public class WorkflowService
    {
        // Database context used by this class to query and persist application records.
        private readonly LocalDbContext _db;
        // Initializes a new workflow service instance.
        public WorkflowService(LocalDbContext db) { _db = db; }

        // Recalculates a job order status from its related quotation and delivery records.
        public void RefreshJobOrderStatus(int jobOrderId)
        {
            var job = _db.JobOrder.FirstOrDefault(j => j.JobOrder_ID == jobOrderId);
            // Exit the current operation when `job == null` is true.
            if (job == null) return;

            job.Status = job.Production_Completed
                ? "Completed"
                : job.Technical_Completed
                    ? "In Progress (1/2)"
                    : string.Equals(job.Status, "In Progress (0/2)", StringComparison.OrdinalIgnoreCase)
                        ? "In Progress (0/2)"
                        : "Pending";
            // Persist the pending entity changes to the database at this point.
            _db.SaveChanges();
        }

        // Conforme: PO + Job Order = Completed, PO only = In Progress, Job Order only = Incomplete
        public void RefreshConformeStatus(int conformeId)
        {
            var conforme = _db.Conforme.FirstOrDefault(c => c.FileID == conformeId);
            // Exit the current operation when `conforme == null` is true.
            if (conforme == null) return;

            // Apply this predicate in the database query before materializing results.
            var pos = _db.PurchaseOrder.Where(p => p.Conforme_FileID == conformeId).ToList();
            var hasJob = _db.JobOrder.Any(j => j.Conforme_FileID == conformeId);

            // Enter this branch when `pos.Count > 0 && hasJob` is true.
            if (pos.Count > 0 && hasJob)
            {
                conforme.Status = "Completed";
                // Process each `po` returned by the collection expression in the loop header.
                foreach (var po in pos) po.Status = "Completed";
            }
            // Enter this branch when `pos.Count > 0` is true.
            else if (pos.Count > 0) conforme.Status = "In Progress";
            // Enter this branch when `hasJob` is true.
            else if (hasJob) conforme.Status = "Incomplete";

            // Persist the pending entity changes to the database at this point.
            _db.SaveChanges();
        }

        // Marks a quotation complete when its related workflow conditions are satisfied.
        public void CompleteQuotation(int quotationId)
        {
            var q = _db.Quotation.FirstOrDefault(x => x.Quotation_ID == quotationId);
            // Enter this branch when `q != null && q.Status !=` is true.
            if (q != null && q.Status != "Completed") { q.Status = "Completed"; _db.SaveChanges(); }
        }
    }
}