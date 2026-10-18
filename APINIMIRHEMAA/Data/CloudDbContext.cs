using Microsoft.EntityFrameworkCore;
using APINIMIRHEMAA.Models;

namespace APINIMIRHEMAA.Data
{
    public class CloudDbContext : DbContext
    {
        private readonly LocalDbContext _localDb;

        public CloudDbContext(
            DbContextOptions<CloudDbContext> options,
            LocalDbContext localDb) : base(options)
        {
            _localDb = localDb;
        }
        public DbSet<ClientEntity> Clients { get; set; }
        public DbSet<Quotation> Quotation { get; set; }
        public DbSet<ClientQuotationSequence> ClientQuotationSequences { get; set; }
        public DbSet<ClientQuotationReservation> ClientQuotationReservations { get; set; }
        public DbSet<Conforme> Conforme { get; set; }
        public DbSet<JobOrder> JobOrder { get; set; }
        public DbSet<AuditTrail> AuditTrail { get; set; }
        public DbSet<CollectionReceipt> CollectionReceipt { get; set; }
        public DbSet<DeliveryFiles> DeliveryFiles { get; set; }
        public DbSet<DeliveryReceipt> DeliveryReceipt { get; set; }
        public DbSet<Graphics> Graphics { get; set; }
        public DbSet<Installation_Schedule> Installation_Schedule { get; set; }
        public DbSet<IssuedBy> IssuedBy { get; set; }
        public DbSet<MaterialRequesition_Slip> MaterailsRequisition_Slip { get; set; }
        public DbSet<Materials> Materials { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrder { get; set; }
        public DbSet<StockTransfer_Slip> StockTransfer_Slip { get; set; }
        public DbSet<Transmittal_Slip> Transmittal_Slip { get; set; }
        public DbSet<ServiceInvoice> ServiceInvoice { get; set; }
        public DbSet<Users> Users { get; set; }
        public DbSet<Roles> Roles { get; set; }
        public DbSet<Departments> Departments { get; set; }
        public DbSet<Inventory> Inventory { get; set; }
        public DbSet<Purchased_Material> Purchased_Material { get; set; }
        public DbSet<JobOrderItems> JobOrderItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ClientQuotationSequence>(entity =>
            {
                entity.ToTable("ClientQuotationSequence");
                entity.HasKey(sequence => new { sequence.Client_ID, sequence.SequenceYear, sequence.SequenceMonth });
            });
            modelBuilder.Entity<ClientQuotationReservation>()
                .ToTable("ClientQuotationReservation");
            modelBuilder.Entity<JobOrder>()
                .Ignore(jobOrder => jobOrder.Comments);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SetCreatedByRecords();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SetCreatedByRecords();
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void SetCreatedByRecords()
        {
            var creator = _localDb.GetCurrentEmployeeName();

            foreach (var entry in ChangeTracker.Entries()
                         .Where(entry => entry.State == EntityState.Added))
            {
                if (entry.Entity is ICreatedByRecord record)
                {
                    record.CreatedBy = creator;
                }
            }
        }
    }
}
