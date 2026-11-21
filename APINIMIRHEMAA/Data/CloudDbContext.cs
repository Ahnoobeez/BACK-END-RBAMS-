using Microsoft.EntityFrameworkCore;
using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;

namespace APINIMIRHEMAA.Data
{
    // Configures database access and entity mapping for cloud db context data.
    public class CloudDbContext : DbContext
    {
        // Injected or cached local db dependency/state used by this class.
        private readonly LocalDbContext _localDb;
        // Encryption service used to protect sensitive persisted values.
        private readonly EncryptionService _encryptionService;

        // Initializes a new cloud db context instance.
        public CloudDbContext(
            DbContextOptions<CloudDbContext> options,
            LocalDbContext localDb,
            EncryptionService encryptionService) : base(options)
        {
            _localDb = localDb;
            _encryptionService = encryptionService;
        }
        // Exposes the client entity entity set for queries and persistence through this context.
        public DbSet<ClientEntity> Clients { get; set; }
        // Exposes the client access grant entity set for queries and persistence through this context.
        public DbSet<ClientAccessGrant> ClientAccessGrants { get; set; }
        // Exposes the quotation entity set for queries and persistence through this context.
        public DbSet<Quotation> Quotation { get; set; }
        // Exposes the client quotation sequence entity set for queries and persistence through this context.
        public DbSet<ClientQuotationSequence> ClientQuotationSequences { get; set; }
        // Exposes the client quotation reservation entity set for queries and persistence through this context.
        public DbSet<ClientQuotationReservation> ClientQuotationReservations { get; set; }
        // Exposes the conforme entity set for queries and persistence through this context.
        public DbSet<Conforme> Conforme { get; set; }
        // Exposes the job order entity set for queries and persistence through this context.
        public DbSet<JobOrder> JobOrder { get; set; }
        // Exposes the audit trail entity set for queries and persistence through this context.
        public DbSet<AuditTrail> AuditTrail { get; set; }
        // Exposes the collection receipt entity set for queries and persistence through this context.
        public DbSet<CollectionReceipt> CollectionReceipt { get; set; }
        // Exposes the delivery files entity set for queries and persistence through this context.
        public DbSet<DeliveryFiles> DeliveryFiles { get; set; }
        // Exposes the delivery receipt entity set for queries and persistence through this context.
        public DbSet<DeliveryReceipt> DeliveryReceipt { get; set; }
        // Exposes the graphics entity set for queries and persistence through this context.
        public DbSet<Graphics> Graphics { get; set; }
        // Exposes the installation schedule entity set for queries and persistence through this context.
        public DbSet<Installation_Schedule> Installation_Schedule { get; set; }
        // Exposes the issued by entity set for queries and persistence through this context.
        public DbSet<IssuedBy> IssuedBy { get; set; }
        // Exposes the material requesition slip entity set for queries and persistence through this context.
        public DbSet<MaterialRequesition_Slip> MaterailsRequisition_Slip { get; set; }
        // Exposes the materials entity set for queries and persistence through this context.
        public DbSet<Materials> Materials { get; set; }
        // Exposes the purchase order entity set for queries and persistence through this context.
        public DbSet<PurchaseOrder> PurchaseOrder { get; set; }
        // Exposes the stock transfer slip entity set for queries and persistence through this context.
        public DbSet<StockTransfer_Slip> StockTransfer_Slip { get; set; }
        // Exposes the transmittal slip entity set for queries and persistence through this context.
        public DbSet<Transmittal_Slip> Transmittal_Slip { get; set; }
        // Exposes the service invoice entity set for queries and persistence through this context.
        public DbSet<ServiceInvoice> ServiceInvoice { get; set; }
        // Exposes the service invoice item entity set for queries and persistence through this context.
        public DbSet<ServiceInvoiceItem> ServiceInvoiceItems { get; set; }
        // Exposes the service invoice file entity set for queries and persistence through this context.
        public DbSet<ServiceInvoiceFile> ServiceInvoiceFiles { get; set; }
        // Exposes the users entity set for queries and persistence through this context.
        public DbSet<Users> Users { get; set; }
        // Exposes the roles entity set for queries and persistence through this context.
        public DbSet<Roles> Roles { get; set; }
        // Exposes the departments entity set for queries and persistence through this context.
        public DbSet<Departments> Departments { get; set; }
        // Exposes the inventory entity set for queries and persistence through this context.
        public DbSet<Inventory> Inventory { get; set; }
        // Exposes the purchased material entity set for queries and persistence through this context.
        public DbSet<Purchased_Material> Purchased_Material { get; set; }
        // Exposes the job order items entity set for queries and persistence through this context.
        public DbSet<JobOrderItems> JobOrderItems { get; set; }

        // Configures entity mappings, relationships, and database constraints for the data model.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EncryptedDataMapping.ConfigureClients(modelBuilder, _encryptionService);

            modelBuilder.Entity<ClientAccessGrant>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("ClientAccessGrants");
                entity.HasKey(grant => new { grant.Client_ID, grant.User_ID });
                entity.Property(grant => grant.GrantedAtUtc)
                    .HasDefaultValueSql("SYSUTCDATETIME()");
            });
            modelBuilder.Entity<ClientQuotationSequence>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("ClientQuotationSequence");
                entity.HasKey(sequence => new { sequence.Client_ID, sequence.SequenceYear, sequence.SequenceMonth });
            });
            modelBuilder.Entity<ClientQuotationReservation>()
                .ToTable("ClientQuotationReservation");
            modelBuilder.Entity<ServiceInvoice>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.Property(invoice => invoice.ServiceInvoice_ID)
                    .HasDefaultValueSql("NEXT VALUE FOR dbo.ServiceInvoiceIdSequence");
                entity.Property(invoice => invoice.Quantity)
                    .ValueGeneratedOnAdd();
            });
            modelBuilder.Entity<ServiceInvoiceFile>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
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
                // Configures the entity mapping, keys, and database-generated values.
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
                // Configures the entity mapping, keys, and database-generated values.
                entity.HasOne<Quotation_Items>()
                    .WithMany()
                    .HasForeignKey(item => item.QI_ID)
                    .OnDelete(DeleteBehavior.SetNull);
            });
            modelBuilder.Entity<JobOrder>()
                .Ignore(jobOrder => jobOrder.Comments);

            ApplyCreatedByFilters(modelBuilder);
        }

        // Persists the changes.
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SetCreatedByRecords();
            // Persist the pending entity changes to the database at this point.
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        // Persists the changes.
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            SetCreatedByRecords();
            // Persist the pending entity changes to the database at this point.
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // Sets the created by records.
        private void SetCreatedByRecords()
        {
            var records = ChangeTracker.Entries()
                // Apply this predicate in the database query before materializing results.
                .Where(entry => entry.State == EntityState.Added && entry.Entity is ICreatedByRecord)
                .ToList();
            // Exit the current operation when `records.Count == 0` is true.
            if (records.Count == 0)
            {
                return;
            }

            var creator = _localDb.GetCurrentEmployeeName();
            var creatorUserId = _localDb.CurrentUserId;
            var creatorDepartmentId = _localDb.CurrentDepartmentId;

            // Process each `entry` returned by the collection expression in the loop header.
            foreach (var entry in records)
            {
                // Enter this branch when `entry.Entity is ICreatedByRecord record` is true.
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

        // Applies the created by filters.
        private void ApplyCreatedByFilters(ModelBuilder modelBuilder)
        {
            var filterMethod = GetType().GetMethod(
                nameof(ApplyCreatedByFilter),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var entityTypes = modelBuilder.Model.GetEntityTypes()
                // Apply this predicate in the database query before materializing results.
                .Where(entityType => typeof(ICreatedByRecord).IsAssignableFrom(entityType.ClrType))
                .ToList();

            // Process each `entityType` returned by the collection expression in the loop header.
            foreach (var entityType in entityTypes)
            {
                // Job Orders are shared across departments; controller policies enforce access.
                if (entityType.ClrType == typeof(JobOrder) ||
                    entityType.ClrType == typeof(Materials) ||
                    entityType.ClrType == typeof(Inventory) ||
                    entityType.ClrType == typeof(Purchased_Material))
                {
                    continue;
                }

                // Enter this branch when `entityType.ClrType == typeof(ClientEntity)` is true.
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

        // Applies created by filter. Inputs: modelBuilder (model builder).
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
