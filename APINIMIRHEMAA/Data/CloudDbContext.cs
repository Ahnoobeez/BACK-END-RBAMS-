using Microsoft.EntityFrameworkCore;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;

namespace APINIMIRHEMAA.Data
{

    public class CloudDbContext : DbContext
    {

        private readonly LocalDbContext _localDb;

        private readonly EncryptionService _encryptionService;


        public CloudDbContext(
            DbContextOptions<CloudDbContext> options,
            LocalDbContext localDb,
            EncryptionService encryptionService) : base(options)
        {
            _localDb = localDb;
            _encryptionService = encryptionService;
        }

        public DbSet<ClientEntity> Clients { get; set; }

        public DbSet<ClientAccessGrant> ClientAccessGrants { get; set; }

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

        public DbSet<ServiceInvoiceItem> ServiceInvoiceItems { get; set; }

        public DbSet<ServiceInvoiceFile> ServiceInvoiceFiles { get; set; }

        public DbSet<Users> Users { get; set; }

        public DbSet<Roles> Roles { get; set; }

        public DbSet<Departments> Departments { get; set; }

        public DbSet<Inventory> Inventory { get; set; }

        public DbSet<Purchased_Material> Purchased_Material { get; set; }

        public DbSet<JobOrderItems> JobOrderItems { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EncryptedDataMapping.ConfigureClients(modelBuilder, _encryptionService);

            modelBuilder.Entity<AuditTrail>(entity =>
            {
                entity.HasIndex(audit => audit.User_ID)
                    .HasDatabaseName("IX_AuditTrail_User_ID");
                entity.HasOne<Users>()
                    .WithMany()
                    .HasForeignKey(audit => audit.User_ID)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_AuditTrail_Users_User_ID");
            });

            modelBuilder.Entity<ClientAccessGrant>(entity =>
            {

                entity.ToTable("ClientAccessGrants");
                entity.HasKey(grant => new { grant.Client_ID, grant.User_ID });
                entity.Property(grant => grant.GrantedAtUtc)
                    .HasDefaultValueSql("SYSUTCDATETIME()");
            });
            modelBuilder.Entity<ClientQuotationSequence>(entity =>
            {

                entity.ToTable("ClientQuotationSequence");
                entity.HasKey(sequence => new { sequence.Client_ID, sequence.SequenceYear, sequence.SequenceMonth });
            });
            modelBuilder.Entity<ClientQuotationReservation>()
                .ToTable("ClientQuotationReservation");
            modelBuilder.Entity<ServiceInvoice>(entity =>
            {

                entity.Property(invoice => invoice.ServiceInvoice_ID)
                    .HasDefaultValueSql("NEXT VALUE FOR dbo.ServiceInvoiceIdSequence");
                entity.Property(invoice => invoice.Quantity)
                    .ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<ServiceInvoiceFile>(entity =>
            {

                entity.ToTable("ServiceInvoiceFiles");
                entity.HasKey(file => file.ServiceInvoiceFile_ID);
                entity.Property(file => file.FileData)
                    .HasColumnType("varbinary(max)")
                    .IsRequired();
                entity.HasOne<JobOrder>()
                    .WithMany()
                    .HasForeignKey(file => file.JobOrderID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(file => file.JobOrderID);
            });
            modelBuilder.Entity<ServiceInvoiceItem>(entity =>
            {

                entity.ToTable("ServiceInvoiceItems");
                entity.HasKey(item => item.ServiceInvoiceItem_ID);
                entity.Property(item => item.Unit_Price).HasColumnType("decimal(18,2)");
                entity.Property(item => item.Amount).HasColumnType("decimal(18,2)");
                entity.HasOne<ServiceInvoice>()
                    .WithMany()
                    .HasForeignKey(item => item.ServiceInvoice_ID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(item => item.ServiceInvoice_ID);
            });
            modelBuilder.Entity<JobOrderItems>(entity =>
            {

                entity.HasOne<Quotation_Items>()
                    .WithMany()
                    .HasForeignKey(item => item.QI_ID)
                    .OnDelete(DeleteBehavior.SetNull);
            });
            modelBuilder.Entity<JobOrder>()
                .Ignore(jobOrder => jobOrder.Comments);

            ApplyCreatedByFilters(modelBuilder);
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
            var records = ChangeTracker.Entries()

                .Where(entry => entry.State == EntityState.Added && entry.Entity is ICreatedByRecord)
                .ToList();

            if (records.Count == 0)
            {
                return;
            }

            var creator = _localDb.GetCurrentEmployeeName();
            var creatorUserId = _localDb.CurrentUserId;
            var creatorDepartmentId = _localDb.CurrentDepartmentId;


            foreach (var entry in records)
            {

                if (entry.Entity is ICreatedByRecord record)
                {
                    record.CreatedBy = creator;
                    record.CreatedByUserId = creatorUserId;
                    record.CreatedByDepartmentId = creatorDepartmentId;
                }
            }
        }

        private int? CurrentUserId => _localDb.CurrentUserId;
        private int? CurrentDepartmentId => _localDb.CurrentDepartmentId;
        private bool HasGlobalRecordAccess => _localDb.HasGlobalRecordAccess;
        private bool HasModuleRecordAccess => _localDb.HasModuleRecordAccess;
        private bool IsDepartmentManager => _localDb.IsDepartmentManager;


        private void ApplyCreatedByFilters(ModelBuilder modelBuilder)
        {
            var filterMethod = GetType().GetMethod(
                nameof(ApplyCreatedByFilter),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var entityTypes = modelBuilder.Model.GetEntityTypes()

                .Where(entityType => typeof(ICreatedByRecord).IsAssignableFrom(entityType.ClrType))
                .ToList();


            foreach (var entityType in entityTypes)
            {

                if (entityType.ClrType == typeof(JobOrder) ||
                    entityType.ClrType == typeof(Materials) ||
                    entityType.ClrType == typeof(Inventory) ||
                    entityType.ClrType == typeof(Purchased_Material))
                {
                    continue;
                }


                if (entityType.ClrType == typeof(ClientEntity))
                {
                    modelBuilder.Entity<ClientEntity>().HasQueryFilter(client =>
                        HasGlobalRecordAccess ||
                        HasModuleRecordAccess ||
                        (CurrentUserId.HasValue && client.CreatedByUserId == CurrentUserId) ||
                        (IsDepartmentManager &&
                            CurrentUserId.HasValue &&
                            CurrentDepartmentId.HasValue &&
                            client.CreatedByDepartmentId == CurrentDepartmentId) ||
                        (CurrentUserId.HasValue &&
                            ClientAccessGrants.Any(grant =>
                                grant.Client_ID == client.Client_ID &&
                                grant.User_ID == CurrentUserId)));
                    continue;
                }

                filterMethod.MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }


        private void ApplyCreatedByFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ICreatedByRecord
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(record =>
                HasGlobalRecordAccess ||
                HasModuleRecordAccess ||
                (CurrentUserId.HasValue && record.CreatedByUserId == CurrentUserId) ||
                (IsDepartmentManager &&
                    CurrentUserId.HasValue &&
                    CurrentDepartmentId.HasValue &&
                    record.CreatedByDepartmentId == CurrentDepartmentId));
        }
    }
}
