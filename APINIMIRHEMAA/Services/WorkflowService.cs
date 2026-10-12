using APINIMIRHEMAA.Data;

namespace APINIMIRHEMAA.Services
{
    public class WorkflowService
    {
        private readonly LocalDbContext _db;
        public WorkflowService(LocalDbContext db) { _db = db; }

        // Job Order: both receipts = Completed, one = InProgress, none = Pending
        public void RefreshJobOrderStatus(int jobOrderId)
        {
            var job = _db.JobOrder.FirstOrDefault(j => j.JobOrder_ID == jobOrderId);
            if (job == null) return;

            var hasDelivery = _db.DeliveryReceipt.Any(d => d.JobOrderID == jobOrderId);
            var hasCollection = _db.CollectionReceipt.Any(c => c.JobOrderID == jobOrderId);

            job.Status = hasDelivery && hasCollection ? "Completed"
                       : hasDelivery || hasCollection ? "InProgress"
                       : "Pending";
            _db.SaveChanges();
        }

        // Conforme: PO + Job Order = Completed, PO only = In Progress, Job Order only = Incomplete
        public void RefreshConformeStatus(int conformeId)
        {
            var conforme = _db.Conforme.FirstOrDefault(c => c.FileID == conformeId);
            if (conforme == null) return;

            var pos = _db.PurchaseOrder.Where(p => p.Conforme_FileID == conformeId).ToList();
            var hasJob = _db.JobOrder.Any(j => j.Conforme_FileID == conformeId);

            if (pos.Count > 0 && hasJob)
            {
                conforme.Status = "Completed";
                foreach (var po in pos) po.Status = "Completed";
            }
            else if (pos.Count > 0) conforme.Status = "In Progress";
            else if (hasJob) conforme.Status = "Incomplete";

            _db.SaveChanges();
        }

        public void CompleteQuotation(int quotationId)
        {
            var q = _db.Quotation.FirstOrDefault(x => x.Quotation_ID == quotationId);
            if (q != null && q.Status != "Completed") { q.Status = "Completed"; _db.SaveChanges(); }
        }
    }
}