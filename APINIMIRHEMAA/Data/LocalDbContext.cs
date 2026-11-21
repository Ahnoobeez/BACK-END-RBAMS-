using APINIMIRHEMAA.Models;
using APINIMIRHEMAA.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace APINIMIRHEMAA.Data
{
    // Configures database access and entity mapping for local db context data.
    public class LocalDbContext : DbContext
    {
        // Provides access to the current HTTP request and its authenticated user context.
        private readonly IHttpContextAccessor _http;
        // Shared store used to retain undo-operation state across requests.
        private readonly UndoOperationStore _undoOperationStore;
        // Encryption service used to protect sensitive persisted values.
        private readonly EncryptionService _encryptionService;
        private readonly ILogger<LocalDbContext> _logger;
        // Injected or cached current department id dependency/state used by this class.
        private int? _currentDepartmentId;
        // Injected or cached current department id resolved dependency/state used by this class.
        private bool _currentDepartmentIdResolved;
        // Initializes a new local db context instance.
        public LocalDbContext(
            DbContextOptions<LocalDbContext> options,
            IHttpContextAccessor http,
            UndoOperationStore undoOperationStore,
            EncryptionService encryptionService,
            ILogger<LocalDbContext> logger) : base(options)
        {
            _http = http;
            _undoOperationStore = undoOperationStore;
            _encryptionService = encryptionService;
            _logger = logger;
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
        // Exposes the payment transaction entity set for queries and persistence through this context.
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        // Exposes the delivery files entity set for queries and persistence through this context.
        public DbSet<DeliveryFiles> DeliveryFiles { get; set; }
        // Exposes the delivery receipt entity set for queries and persistence through this context.
        public DbSet<DeliveryReceipt> DeliveryReceipt { get; set; }
        // Exposes the graphics entity set for queries and persistence through this context.
        public DbSet<Graphics> Graphics { get; set; }
        // Exposes the installation schedule entity set for queries and persistence through this context.
        public DbSet<Installation_Schedule> Installation_Schedule { get; set; }
        // Exposes the installation schedule employee entity set for queries and persistence through this context.
        public DbSet<InstallationScheduleEmployee> Installation_Schedule_Employee { get; set; }
        // Exposes the installation report entity set for queries and persistence through this context.
        public DbSet<InstallationReport> InstallationReport { get; set; }
        // Exposes the installation report file entity set for queries and persistence through this context.
        public DbSet<InstallationReportFile> InstallationReportFiles { get; set; }
        // Exposes the issued by entity set for queries and persistence through this context.
        public DbSet<IssuedBy> IssuedBy { get; set; }
        // Exposes the material requesition slip entity set for queries and persistence through this context.
        public DbSet<MaterialRequesition_Slip> MaterialRequisition_Slip { get; set; }
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
        // Exposes the user signature entity set for queries and persistence through this context.
        public DbSet<UserSignature> UserSignatures { get; set; }
        // Exposes the user session entity set for queries and persistence through this context.
        public DbSet<UserSession> UserSessions { get; set; }
        // Exposes the user profile photo entity set for queries and persistence through this context.
        public DbSet<UserProfilePhoto> UserProfilePhotos { get; set; }
        // Exposes the employee profile photo entity set for queries and persistence through this context.
        public DbSet<EmployeeProfilePhoto> EmployeeProfilePhotos { get; set; }
        // Exposes the notification entity set for queries and persistence through this context.
        public DbSet<Notification> Notifications { get; set; }
        // Exposes the user security answer entity set for queries and persistence through this context.
        public DbSet<UserSecurityAnswer> UserSecurityAnswers { get; set; }
        // Exposes the password recovery challenge entity set for queries and persistence through this context.
        public DbSet<PasswordRecoveryChallenge> PasswordRecoveryChallenges { get; set; }
        // Exposes the roles entity set for queries and persistence through this context.
        public DbSet<Roles> Roles { get; set; }
        // Exposes the departments entity set for queries and persistence through this context.
        public DbSet<Departments> Departments { get; set; }
        // Exposes the access permission entity set for queries and persistence through this context.
        public DbSet<AccessPermission> AccessPermissions { get; set; }
        // Exposes the inventory entity set for queries and persistence through this context.
        public DbSet<Inventory> Inventory { get; set; }
        // Exposes the purchased material entity set for queries and persistence through this context.
        public DbSet<Purchased_Material> Purchased_Material { get; set; }
        // Exposes the quotation items entity set for queries and persistence through this context.
        public DbSet<Quotation_Items> Quotation_Items { get; set; }
        // Exposes the quotation item material entity set for queries and persistence through this context.
        public DbSet<QuotationItemMaterial> QuotationItemMaterials { get; set; }
        // Exposes the quotation item store entity set for queries and persistence through this context.
        public DbSet<QuotationItemStore> QuotationItemStores { get; set; }
        // Exposes the quotation store entity set for queries and persistence through this context.
        public DbSet<Quotation_Store> Quotation_Store { get; set; }
        // Exposes the job order comment entity set for queries and persistence through this context.
        public DbSet<JobOrderComment> JobOrderComments { get; set; }
        // Exposes the employee entity set for queries and persistence through this context.
        public DbSet<Employee> Employee { get; set; }
        // Exposes the job order items entity set for queries and persistence through this context.
        public DbSet<JobOrderItems> JobOrderItems { get; set; }
        // Exposes the delivery receipt items entity set for queries and persistence through this context.
        public DbSet<DeliveryReceiptItems> DeliveryReceiptItems { get; set; }

        // Configures entity mappings, relationships, and database constraints for the data model.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            EncryptedDataMapping.ConfigureClients(modelBuilder, _encryptionService);
            EncryptedDataMapping.ConfigureLocalRecords(modelBuilder, _encryptionService);

            modelBuilder.Entity<UserSession>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("UserSessions");
                entity.HasKey(session => session.Session_ID);
                entity.Property(session => session.Session_ID)
                    .HasMaxLength(64)
                    .IsRequired();
                entity.Property(session => session.LastSeenUtc)
                    .HasColumnType("datetime2")
                    .IsRequired();
                entity.HasIndex(session => new { session.User_ID, session.LastSeenUtc });
                entity.HasOne<Users>()
                    .WithMany()
                    .HasForeignKey(session => session.User_ID)
                    .OnDelete(DeleteBehavior.Cascade);
            });

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
            modelBuilder.Entity<EmployeeProfilePhoto>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("EmployeeProfilePhoto");
                entity.HasKey(photo => photo.Employee_ID);
                entity.Property(photo => photo.PhotoData)
                    .HasColumnType("varbinary(max)")
                    .IsRequired();
                entity.Property(photo => photo.ContentType)
                    .HasMaxLength(32)
                    .IsRequired();
                entity.HasOne<Employee>()
                    .WithMany()
                    .HasForeignKey(photo => photo.Employee_ID)
                    .OnDelete(DeleteBehavior.Cascade);
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
            modelBuilder.Entity<PaymentTransaction>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("PaymentTransaction");
                entity.HasKey(payment => payment.PaymentTransaction_ID);
                entity.Property(payment => payment.Amount).HasColumnType("decimal(18,2)");
                entity.Property(payment => payment.PaidAtUtc)
                    .HasColumnType("datetime2(7)")
                    .HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasOne<CollectionReceipt>(payment => payment.CollectionReceipt)
                    .WithMany()
                    .HasForeignKey(payment => payment.CollectionReceipt_ID)
                    .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne<JobOrder>()
                    .WithMany()
                    .HasForeignKey(payment => payment.JobOrderID)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne<Quotation>()
                    .WithMany()
                    .HasForeignKey(payment => payment.Quotation_ID)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(payment => payment.JobOrderID)
                    .IsUnique()
                    .HasDatabaseName("UX_PaymentTransaction_JobOrderID");
                entity.HasIndex(payment => payment.CollectionReceipt_ID)
                    .IsUnique()
                    .HasFilter("[CollectionReceipt_ID] IS NOT NULL")
                    .HasDatabaseName("UX_PaymentTransaction_CollectionReceipt_ID");
                entity.HasIndex(payment => payment.PaidAtUtc)
                    .HasDatabaseName("IX_PaymentTransaction_PaidAtUtc");
            });
            modelBuilder.Entity<InstallationReportFile>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("InstallationReportFiles");
                entity.HasKey(file => file.InstallationReportFile_ID);
                entity.Property(file => file.File_Data)
                    .HasColumnType("varbinary(max)")
                    .IsRequired();
                entity.HasOne<InstallationReport>()
                    .WithMany()
                    .HasForeignKey(file => file.InstallationReport_ID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(file => file.InstallationReport_ID);
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
            modelBuilder.Entity<JobOrderComment>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("JobOrderComment");
                entity.HasKey(comment => comment.JobOrderComment_ID);
                entity.Property(comment => comment.Employee_Name)
                    .HasMaxLength(200)
                    .IsRequired();
                entity.Property(comment => comment.Comment).IsRequired();
                entity.Property(comment => comment.CreatedAt)
                    .HasColumnType("datetime2")
                    .HasDefaultValueSql("SYSUTCDATETIME()");
                entity.HasIndex(comment => new { comment.JobOrder_ID, comment.JobOrderComment_ID });
                entity.HasOne<JobOrder>()
                    .WithMany(jobOrder => jobOrder.Comments)
                    .HasForeignKey(comment => comment.JobOrder_ID)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<AccessPermission>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("AccessPermissions");
                entity.HasKey(permission => permission.AccessPermission_ID);
                entity.Property(permission => permission.ServiceKey)
                    .HasMaxLength(80)
                    .IsRequired();
                entity.Property(permission => permission.ActionKey)
                    .HasMaxLength(20)
                    .IsRequired();
                entity.HasOne<Roles>()
                    .WithMany()
                    .HasForeignKey(permission => permission.Role_ID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<Departments>()
                    .WithMany()
                    .HasForeignKey(permission => permission.Department_ID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<Users>()
                    .WithMany()
                    .HasForeignKey(permission => permission.User_ID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(permission => new
                    { permission.Role_ID, permission.Department_ID, permission.ServiceKey, permission.ActionKey })
                    .IsUnique()
                    .HasFilter("[Role_ID] IS NOT NULL AND [Department_ID] IS NOT NULL AND [User_ID] IS NULL");
                entity.HasIndex(permission => new
                    { permission.Role_ID, permission.ServiceKey, permission.ActionKey })
                    .IsUnique()
                    .HasFilter("[Role_ID] IS NOT NULL AND [Department_ID] IS NULL AND [User_ID] IS NULL");
                entity.HasIndex(permission => new
                    { permission.Department_ID, permission.ServiceKey, permission.ActionKey })
                    .IsUnique()
                    .HasFilter("[Role_ID] IS NULL AND [Department_ID] IS NOT NULL AND [User_ID] IS NULL");
                entity.HasIndex(permission => new
                    { permission.User_ID, permission.ServiceKey, permission.ActionKey })
                    .IsUnique()
                    .HasFilter("[Role_ID] IS NULL AND [Department_ID] IS NULL AND [User_ID] IS NOT NULL");
            });
            modelBuilder.Entity<QuotationItemMaterial>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("QuotationItemMaterial");
                entity.HasKey(material => material.QuotationItemMaterial_ID);
                entity.Property(material => material.Code_Name).IsRequired();
                entity.Property(material => material.Item_Name).IsRequired();
                entity.Property(material => material.Unit).IsRequired();
                entity.HasIndex(material => new { material.QI_ID, material.Material_ID })
                    .IsUnique();
                entity.HasOne<Quotation_Items>()
                    .WithMany(item => item.Materials)
                    .HasForeignKey(material => material.QI_ID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<Materials>()
                    .WithMany()
                    .HasForeignKey(material => material.Material_ID)
                    .OnDelete(DeleteBehavior.Restrict);
            });
            modelBuilder.Entity<QuotationItemStore>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("QuotationItemStores");
                entity.HasKey(link => new { link.QI_ID, link.QS_ID });
                entity.HasOne<Quotation_Items>()
                    .WithMany()
                    .HasForeignKey(link => link.QI_ID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne<Quotation_Store>()
                    .WithMany()
                    .HasForeignKey(link => link.QS_ID)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(link => link.QS_ID);
            });
            modelBuilder.Entity<UserSignature>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("UserSignature");
                entity.HasKey(signature => signature.User_ID);
                entity.Property(signature => signature.SignatureData)
                    .HasColumnType("varbinary(max)")
                    .IsRequired();
                entity.HasOne<Users>()
                    .WithOne()
                    .HasForeignKey<UserSignature>(signature => signature.User_ID)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<UserProfilePhoto>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("UserProfilePhoto");
                entity.HasKey(photo => photo.User_ID);
                entity.Property(photo => photo.PhotoData)
                    .HasColumnType("varbinary(max)")
                    .IsRequired();
                entity.Property(photo => photo.ContentType)
                    .HasMaxLength(32)
                    .IsRequired();
                entity.HasOne<Users>()
                    .WithOne()
                    .HasForeignKey<UserProfilePhoto>(photo => photo.User_ID)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<UserSecurityAnswer>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("UserSecurityAnswer");
                entity.HasKey(answer => new { answer.User_ID, answer.Question_ID });
                entity.Property(answer => answer.AnswerHash)
                    .HasMaxLength(100)
                    .IsRequired();
                entity.HasOne<Users>()
                    .WithMany()
                    .HasForeignKey(answer => answer.User_ID)
                    .OnDelete(DeleteBehavior.Cascade);
            });
            modelBuilder.Entity<PasswordRecoveryChallenge>(entity =>
            {
                // Configures the entity mapping, keys, and database-generated values.
                entity.ToTable("PasswordRecoveryChallenge");
                entity.HasKey(challenge => challenge.Challenge_ID);
                entity.HasOne<Users>()
                    .WithMany()
                    .HasForeignKey(challenge => challenge.User_ID)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            ApplyCreatedByFilters(modelBuilder);
        }

        // ---------- SaveChanges hooks ----------
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            // Enter this branch when `!IsUndoRequest` is true.
            if (!IsUndoRequest)
            {
                SetCreatedByRecords();
            }

            var batchId = GetAuditBatchId();
            var pendingUndo = IsUndoRequest ? new List<PendingUndo>() : CollectUndoOperations();
            var pending = CollectAudits(batchId);
            AddLowStockNotifications();
            // Persist the pending entity changes to the database at this point.
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            RecordUndoOperations(batchId, pendingUndo);
            FlushAudits(pending);
            return result;
        }

        // Persists the changes.
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
        {
            // Enter this branch when `!IsUndoRequest` is true.
            if (!IsUndoRequest)
            {
                SetCreatedByRecords();
            }

            var batchId = GetAuditBatchId();
            var pendingUndo = IsUndoRequest ? new List<PendingUndo>() : CollectUndoOperations();
            var pending = CollectAudits(batchId);
            await AddLowStockNotificationsAsync(ct);
            // Persist the pending entity changes to the database at this point.
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            RecordUndoOperations(batchId, pendingUndo);
            await FlushAuditsAsync(pending, ct);
            return result;
        }

        private sealed record LowStockInventoryEvent(int MaterialId, int Balance);

        private List<LowStockInventoryEvent> GetLowStockInventoryEvents()
        {
            ChangeTracker.DetectChanges();

            return ChangeTracker.Entries<Inventory>()
                .Where(entry =>
                {
                    var balance = entry.Property(inventory => inventory.Balance);
                    if (!balance.CurrentValue.HasValue ||
                        balance.CurrentValue.Value > InventoryRules.LowStockThreshold)
                    {
                        return false;
                    }

                    if (entry.State == EntityState.Added)
                    {
                        return true;
                    }

                    return entry.State == EntityState.Modified &&
                        balance.IsModified &&
                        (!balance.OriginalValue.HasValue ||
                         balance.OriginalValue.Value > InventoryRules.LowStockThreshold);
                })
                .Select(entry => new LowStockInventoryEvent(
                    entry.Entity.Material_ID,
                    entry.Entity.Balance!.Value))
                .GroupBy(inventoryEvent => inventoryEvent.MaterialId)
                .Select(group => group.Last())
                .ToList();
        }

        private void AddLowStockNotifications()
        {
            var events = GetLowStockInventoryEvents();
            if (events.Count == 0)
            {
                return;
            }

            var recipientIds = GetLowStockRecipientIds();
            var materialNames = GetLowStockMaterialNames(events);
            AddLowStockNotifications(events, recipientIds, materialNames);
        }

        private async Task AddLowStockNotificationsAsync(CancellationToken cancellationToken)
        {
            var events = GetLowStockInventoryEvents();
            if (events.Count == 0)
            {
                return;
            }

            var recipientIds = await GetLowStockRecipientIdsAsync(cancellationToken);
            var materialNames = await GetLowStockMaterialNamesAsync(events, cancellationToken);
            AddLowStockNotifications(events, recipientIds, materialNames);
        }

        private List<int> GetLowStockRecipientIds()
        {
            var candidates = (
                from user in Users.IgnoreQueryFilters().AsNoTracking()
                join role in Roles.IgnoreQueryFilters().AsNoTracking()
                    on user.Role_ID equals role.Role_ID into roleMatches
                from role in roleMatches.DefaultIfEmpty()
                join department in Departments.IgnoreQueryFilters().AsNoTracking()
                    on user.Department_ID equals department.Department_ID into departmentMatches
                from department in departmentMatches.DefaultIfEmpty()
                where user.IsActive
                select new
                {
                    user.User_ID,
                    RoleName = role == null ? null : role.Role_Name,
                    DepartmentName = department == null ? null : department.Department_Name
                }).ToList();

            return candidates
                .Where(user =>
                    ModuleAccess.IsAdministratorRole(user.RoleName) ||
                    ModuleAccess.GetModuleForDepartment(user.DepartmentName) == "warehouse")
                .Select(user => user.User_ID)
                .Distinct()
                .ToList();
        }

        private async Task<List<int>> GetLowStockRecipientIdsAsync(
            CancellationToken cancellationToken)
        {
            var candidates = await (
                from user in Users.IgnoreQueryFilters().AsNoTracking()
                join role in Roles.IgnoreQueryFilters().AsNoTracking()
                    on user.Role_ID equals role.Role_ID into roleMatches
                from role in roleMatches.DefaultIfEmpty()
                join department in Departments.IgnoreQueryFilters().AsNoTracking()
                    on user.Department_ID equals department.Department_ID into departmentMatches
                from department in departmentMatches.DefaultIfEmpty()
                where user.IsActive
                select new
                {
                    user.User_ID,
                    RoleName = role == null ? null : role.Role_Name,
                    DepartmentName = department == null ? null : department.Department_Name
                }).ToListAsync(cancellationToken);

            return candidates
                .Where(user =>
                    ModuleAccess.IsAdministratorRole(user.RoleName) ||
                    ModuleAccess.GetModuleForDepartment(user.DepartmentName) == "warehouse")
                .Select(user => user.User_ID)
                .Distinct()
                .ToList();
        }

        private Dictionary<int, string> GetLowStockMaterialNames(
            IReadOnlyCollection<LowStockInventoryEvent> events)
        {
            var materialIds = events.Select(inventoryEvent => inventoryEvent.MaterialId)
                .Distinct()
                .ToList();

            return Materials.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(material => materialIds.Contains(material.Material_ID))
                .Select(material => new { material.Material_ID, material.Item_Name })
                .ToDictionary(material => material.Material_ID, material => material.Item_Name);
        }

        private async Task<Dictionary<int, string>> GetLowStockMaterialNamesAsync(
            IReadOnlyCollection<LowStockInventoryEvent> events,
            CancellationToken cancellationToken)
        {
            var materialIds = events.Select(inventoryEvent => inventoryEvent.MaterialId)
                .Distinct()
                .ToList();

            return await Materials.IgnoreQueryFilters()
                .AsNoTracking()
                .Where(material => materialIds.Contains(material.Material_ID))
                .Select(material => new { material.Material_ID, material.Item_Name })
                .ToDictionaryAsync(
                    material => material.Material_ID,
                    material => material.Item_Name,
                    cancellationToken);
        }

        private void AddLowStockNotifications(
            IReadOnlyCollection<LowStockInventoryEvent> events,
            IReadOnlyCollection<int> recipientIds,
            IReadOnlyDictionary<int, string> materialNames)
        {
            if (recipientIds.Count == 0)
            {
                _logger.LogWarning(
                    "Low-stock inventory changes were saved, but no active Inventory or Admin users were found to notify.");
                return;
            }

            var createdAt = DateTime.UtcNow;
            var notifications = events.SelectMany(inventoryEvent =>
            {
                var materialName = materialNames.TryGetValue(inventoryEvent.MaterialId, out var name)
                    ? name
                    : $"Material #{inventoryEvent.MaterialId}";
                var message =
                    $"{materialName} has a low inventory balance of {inventoryEvent.Balance}. " +
                    $"The low-stock threshold is {InventoryRules.LowStockThreshold}.";

                return recipientIds.Select(userId => new Notification
                {
                    User_ID = userId,
                    Title = "Low Material Balance",
                    Message = message,
                    Type = "InventoryLowStock",
                    IsRead = false,
                    CreatedAt = createdAt
                });
            }).ToList();

            Notifications.AddRange(notifications);
        }

        // Shared store used to retain undo-operation state across requests.
        private bool IsUndoRequest =>
            _http.HttpContext?.Request.Path.StartsWithSegments(
                "/api/Undo",
                StringComparison.OrdinalIgnoreCase) == true;

        // Retrieves the audit batch id.
        private string GetAuditBatchId()
        {
            var httpContext = _http.HttpContext;
            var requestedBatchId = httpContext?.Request.Headers["X-Audit-Batch-Id"].FirstOrDefault();
            var batchId = Guid.TryParse(requestedBatchId, out var parsedBatchId)
                ? parsedBatchId.ToString("N")
                : httpContext?.Items[AuditBatchIdContextKey] as string;
            batchId ??= Guid.NewGuid().ToString("N");
            // Enter this branch when `httpContext != null` is true.
            if (httpContext != null)
            {
                httpContext.Items[AuditBatchIdContextKey] = batchId;
            }

            return batchId;
        }

        // Creates undo snapshots for tracked entity changes that can be reversed.
        private List<PendingUndo> CollectUndoOperations()
        {
            ChangeTracker.DetectChanges();
            return ChangeTracker.Entries()
                // Apply this predicate in the database query before materializing results.
                .Where(entry =>
                    entry.Entity is not APINIMIRHEMAA.Models.AuditTrail &&
                    entry.Entity is not APINIMIRHEMAA.Models.ClientQuotationSequence &&
                    entry.Entity is not APINIMIRHEMAA.Models.ClientQuotationReservation &&
                    (entry.State == EntityState.Added ||
                     entry.State == EntityState.Modified ||
                     entry.State == EntityState.Deleted))
                .Select(entry =>
                {
                    // Captures the key and original property values for each tracked change.
                    var state = entry.State;
                    var keyValues = state == EntityState.Added
                        ? new Dictionary<string, object?>()
                        : CaptureUndoKeyValues(entry);
                    var previousValues = state == EntityState.Added
                        ? new Dictionary<string, object?>()
                        : CaptureUndoValues(
                            entry,
                            state == EntityState.Deleted
                                ? entry.Properties
                                // Filter the query to rows satisfying `: entry.Properties.Where(property => property.IsModified && !property.Metadata.IsPrimaryKey()), useOriginalValues: true` before materializing the results.
                                : entry.Properties.Where(property =>
                                    property.IsModified && !property.Metadata.IsPrimaryKey()),
                            useOriginalValues: true);

                    return new PendingUndo(entry, state, keyValues, previousValues);
                })
                .ToList();
        }

        // Stores snapshots of the pending changes for the current user.
        private void RecordUndoOperations(string batchId, List<PendingUndo> pending)
        {
            // Exit the current operation when `pending.Count == 0 || CurrentUserId is not int userId || IsUndoRequest` is true.
            if (pending.Count == 0 || CurrentUserId is not int userId || IsUndoRequest)
            {
                return;
            }

            var changes = pending.Select(item =>
            {
                // Configures database access and entity mapping for for data.
                // Captures expected post-change values and creates an undo record for each snapshot.
                var keyValues = item.State == EntityState.Added
                    ? CaptureUndoKeyValues(item.Entry)
                    : item.KeyValues;
                var expectedValues = item.State switch
                {
                    EntityState.Added => new Dictionary<string, object?>(),
                    EntityState.Modified => CaptureUndoValues(
                        item.Entry,
                        item.PreviousValues.Keys.Select(name => item.Entry.Property(name)),
                        useOriginalValues: false),
                    _ => new Dictionary<string, object?>()
                };

                return new UndoEntityChange(
                    item.Entry.Metadata.ClrType,
                    item.State,
                    keyValues,
                    item.PreviousValues,
                    expectedValues);
            }).ToList();

            _undoOperationStore.Record(batchId, userId, changes);
        }

        // Copies primary-key values used to identify an entity during undo.
        private static Dictionary<string, object?> CaptureUndoKeyValues(EntityEntry entry)
        {
            var key = entry.Metadata.FindPrimaryKey();
            // Raise an exception when `key == null` is true.
            if (key == null)
            {
                throw new InvalidOperationException(
                    $"Cannot create an undo snapshot for keyless entity {entry.Metadata.ClrType.Name}.");
            }

            return key.Properties.ToDictionary(
                property => property.Name,
                property => CloneUndoValue(entry.Property(property.Name).CurrentValue));
        }

        // Copies selected original or current property values for an undo snapshot.
        private static Dictionary<string, object?> CaptureUndoValues(
            EntityEntry entry,
            IEnumerable<PropertyEntry> properties,
            bool useOriginalValues)
        {
            return properties.ToDictionary(
                property => property.Metadata.Name,
                property => CloneUndoValue(
                    useOriginalValues ? property.OriginalValue : property.CurrentValue));
        }

        // Copies byte-array values so undo snapshots are isolated from later mutations.
        private static object? CloneUndoValue(object? value) =>
            value is byte[] bytes ? bytes.ToArray() : value;

        // Holds a tracked entity change and the values needed for its undo snapshot.
        private sealed record PendingUndo(
            EntityEntry Entry,
            EntityState State,
            Dictionary<string, object?> KeyValues,
            Dictionary<string, object?> PreviousValues);

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

            var creator = GetCurrentEmployeeName();
            var creatorUserId = CurrentUserId;
            var creatorDepartmentId = GetCurrentDepartmentId();

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

        // Resolves the current request actor from claims, falling back to the linked employee record and route-derived department when needed. Returns the actor name and department.
        public string GetCurrentEmployeeName() => ResolveActor().name;

        public int? CurrentUserId
        {
            get
            {
                var userId = _http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
                return int.TryParse(userId, out var parsedUserId) ? parsedUserId : null;
            }
        }

        public bool HasGlobalRecordAccess
        {
            get => _http.HttpContext?.Items[RecordAccessScope.SystemAdministrator] is true;
        }

        public bool HasModuleRecordAccess =>
            _http.HttpContext?.Items[RecordAccessScope.Module] is true;

        public bool IsDepartmentManager =>
            _http.HttpContext?.Items[RecordAccessScope.DepartmentManager] is true;

        public string CurrentRoleName =>
            _http.HttpContext?.Items[RecordAccessScope.RoleName] as string ?? "";

        public string CurrentDepartmentName =>
            _http.HttpContext?.Items[RecordAccessScope.DepartmentName] as string ?? "";

        public int? CurrentDepartmentId => GetCurrentDepartmentId();

        // Retrieves the current department id.
        public int? GetCurrentDepartmentId()
        {
            // Exit the current operation when `_currentDepartmentIdResolved` is true.
            if (_currentDepartmentIdResolved)
            {
                return _currentDepartmentId;
            }

            var departmentValue = _http.HttpContext?.Items[RecordAccessScope.DepartmentId];
            // Enter this branch when `departmentValue is int authorizedDepartmentId && authorizedDepartmentId > 0` is true.
            if (departmentValue is int authorizedDepartmentId && authorizedDepartmentId > 0)
            {
                _currentDepartmentId = authorizedDepartmentId;
                _currentDepartmentIdResolved = true;
                return _currentDepartmentId;
            }

            var userId = CurrentUserId;
            // Enter this branch when `!userId.HasValue` is true.
            if (!userId.HasValue)
            {
                _currentDepartmentIdResolved = true;
                return null;
            }

            _currentDepartmentId = Users.Local.FirstOrDefault(user => user.User_ID == userId.Value)?.Department_ID
                ?? Users.AsNoTracking()
                    // Apply this predicate in the database query before materializing results.
                    .Where(user => user.User_ID == userId.Value)
                    .Select(user => (int?)user.Department_ID)
                    .FirstOrDefault();
            _currentDepartmentIdResolved = true;
            return _currentDepartmentId;
        }

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

        // ---------- Audit logic ----------
        private static readonly object AuditBatchIdContextKey = new();

        // Configures database access and entity mapping for pending audit data.
        private sealed class PendingAudit
        {
            public EntityEntry Entry = default!;
            public EntityState State;
            public string Entity = "";
            public string? Id;
            public string BatchId = "";
            public string DocumentLabel = "";
            public Dictionary<string, string?>? PreviousValues;
            public Dictionary<string, string?>? CurrentValues;
            public List<string> Changes = new();
        }

        // Builds audit entries from tracked entity changes before they are saved.
        private List<PendingAudit> CollectAudits(string batchId)
        {
            ChangeTracker.DetectChanges();
            var list = new List<PendingAudit>();

            // Process each `entry` returned by the collection expression in the loop header.
            foreach (var entry in ChangeTracker.Entries()
                         // Apply this predicate in the database query before materializing results.
                         .Where(e => e.Entity is not APINIMIRHEMAA.Models.AuditTrail &&
                         e.Entity is not APINIMIRHEMAA.Models.ClientQuotationSequence &&
                         e.Entity is not APINIMIRHEMAA.Models.ClientQuotationReservation &&
                         e.Entity is not UserSecurityAnswer &&
                         (e.State == EntityState.Added ||
                          e.State == EntityState.Modified ||
                          e.State == EntityState.Deleted)))
            {
                var p = new PendingAudit
                {
                    Entry = entry,
                    State = entry.State,
                    Entity = entry.Metadata.ClrType.Name,
                    BatchId = batchId,
                    PreviousValues = entry.State == EntityState.Added
                        ? null
                        : CaptureAuditValues(entry, useOriginalValues: true),
                    CurrentValues = entry.State == EntityState.Deleted
                        ? null
                        : CaptureAuditValues(entry, useOriginalValues: false),
                    DocumentLabel = ResolveAuditDocumentLabel(entry)
                };

                // Added rows get their real ID only after saving
                if (entry.State != EntityState.Added) p.Id = KeyOf(entry);

                // Enter this branch when `entry.State == EntityState.Modified` is true.
                if (entry.State == EntityState.Modified)
                {
                    // Process each `prop` from `entry.Properties.Where(x => x.IsModified && !x.Metadata.IsPrimaryKey(` using the operation in the loop body.
                    // Filter the query to rows satisfying `ry.Properties.Where(x => x.IsModified && !x.Metadata.IsPrimaryKey())` before materializing the results.
                    foreach (var prop in entry.Properties.Where(x => x.IsModified && !x.Metadata.IsPrimaryKey()))
                    {
                        // Skip this item and continue when `Equals(prop.OriginalValue, prop.CurrentValue)` is true.
                        if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;

                        var name = prop.Metadata.Name;

                        // Enter this branch when `name.Contains(, StringComparison.OrdinalIgnoreCase)` is true.
                        if (name.Contains("password", StringComparison.OrdinalIgnoreCase))
                        {
                            p.Changes.Add("password was changed");   // never log the values
                        }
                        // Enter this branch when `name.Equals(, StringComparison.OrdinalIgnoreCase)` is true.
                        else if (name.Equals("Client_Subject", StringComparison.OrdinalIgnoreCase))
                        {
                            p.Changes.Add("client subject was changed");
                        }
                        // Enter this branch when `name.EndsWith(, StringComparison.OrdinalIgnoreCase) || prop.CurrentValue is byte[] || (prop.CurrentValue is string s && s.Length > 200)` is true.
                        else if (name.EndsWith("data", StringComparison.OrdinalIgnoreCase) ||
                                 prop.CurrentValue is byte[] ||
                                 (prop.CurrentValue is string s && s.Length > 200))
                        {
                            p.Changes.Add($"{name.Replace("_", " ").ToLowerInvariant()} was changed");
                        }
                        // Use this fallback path when the preceding condition is false.
                        else
                        {
                            var label = name.Replace("_", " ").ToLowerInvariant();
                            p.Changes.Add($"{label} {Show(prop.OriginalValue)} to {Show(prop.CurrentValue)}");
                        }
                    }

                    // Skip this item and continue when `p.Changes.Count == 0` is true.
                    if (p.Changes.Count == 0) continue;
                }

                list.Add(p);
            }

            // Process each `answerGroup` returned by the collection expression in the loop header.
            foreach (var answerGroup in ChangeTracker.Entries<UserSecurityAnswer>()
                         // Filter the query to rows satisfying `.Where(entry => entry.State == EntityState.Added) .GroupBy(entry => entry.Entity.User_ID)` before materializing the results.
                         .Where(entry => entry.State == EntityState.Added)
                         .GroupBy(entry => entry.Entity.User_ID))
            {
                list.Add(new PendingAudit
                {
                    Entry = answerGroup.First(),
                    State = EntityState.Added,
                    Entity = "UserSecurity",
                    Id = answerGroup.Key.ToString(),
                    BatchId = batchId
                });
            }
            return list;
        }

        // Returns the primary-key value used to identify an audited entity.
        private static string? KeyOf(EntityEntry e) =>
            e.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault() is { } k
                ? e.Property(k.Name).CurrentValue?.ToString()
                : null;

        // Formats null and empty values for display in an audit entry.
        private static string Show(object? v) =>
            v is null || (v is string s && s == "") ? "(empty)" : v.ToString()!;

        // Copies non-sensitive entity values for an audit record.
        private static Dictionary<string, string?> CaptureAuditValues(
            EntityEntry entry,
            bool useOriginalValues)
        {
            var values = new Dictionary<string, string?>();
            // Process each `property` returned by the collection expression in the loop header.
            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                var normalizedName = name.Replace("_", "", StringComparison.Ordinal)
                    .ToLowerInvariant();
                // Skip this item and continue when `property.Metadata.IsPrimaryKey() || normalizedName.Contains(, StringComparison.Ordinal) || normalizedName.Contains(, StringComparison.Ordinal) || normalizedName.Contains(, StringComparison.Ordinal) || normalizedName.Contains(, StringComparison.Ordinal) || normalizedName.Contains(, StringComparison.Ordinal) || normalizedName is or or || normalizedName.EndsWith(, StringComparison.Ordinal)` is true.
                if (property.Metadata.IsPrimaryKey() ||
                    normalizedName.Contains("password", StringComparison.Ordinal) ||
                    normalizedName.Contains("secret", StringComparison.Ordinal) ||
                    normalizedName.Contains("token", StringComparison.Ordinal) ||
                    normalizedName.Contains("tin", StringComparison.Ordinal) ||
                    normalizedName.Contains("socialsecurity", StringComparison.Ordinal) ||
                    normalizedName is "sss" or "philhealth" or "pagibig" ||
                    normalizedName.EndsWith("data", StringComparison.Ordinal))
                {
                    continue;
                }

                var value = useOriginalValues ? property.OriginalValue : property.CurrentValue;
                // Skip this item and continue when `value is byte[]` is true.
                if (value is byte[])
                {
                    continue;
                }

                var text = value switch
                {
                    null => null,
                    DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                    DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
                    DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
                    _ => Convert.ToString(value, CultureInfo.InvariantCulture)
                };
                // Skip this item and continue when `text?.Length > 8000` is true.
                if (text?.Length > 8000)
                {
                    continue;
                }

                values[name] = text;
            }

            return values;
        }

        // Resolves the audit document label.
        private string ResolveAuditDocumentLabel(EntityEntry entry)
        {
            var clientName = ReadAuditValue(entry, "Client_Name") as string
                ?? ReadAuditValue(entry, "ClientName") as string;
            var clientSubject = ReadAuditValue(entry, "Client_Subject") as string;
            var projectTitle = ReadAuditValue(entry, "Title") as string;
            var quotationId = ResolveQuotationId(entry);

            // Enter this branch when `quotationId is int id && FindQuotation(id) is { } quotation` is true.
            if (quotationId is int id && FindQuotation(id) is { } quotation)
            {
                clientSubject ??= quotation.Client_Subject;
                projectTitle ??= quotation.Title;
                clientName ??= FindClientName(quotation.Client_ID);
            }

            var parts = new[] { clientName, clientSubject, projectTitle }
                // Apply this predicate in the database query before materializing results.
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim());
            var label = string.Join(" - ", parts);
            return string.IsNullOrWhiteSpace(label)
                ? HumanizeEntityName(entry.Metadata.ClrType.Name)
                : label;
        }

        // Reads the audit value.
        private static object? ReadAuditValue(EntityEntry entry, string propertyName)
        {
            // Exit the current operation when `entry.Metadata.FindProperty(propertyName) == null` is true.
            if (entry.Metadata.FindProperty(propertyName) == null)
            {
                return null;
            }

            var property = entry.Property(propertyName);
            return entry.State == EntityState.Deleted
                ? property.OriginalValue
                : property.CurrentValue;
        }

        // Resolves the quotation id.
        private int? ResolveQuotationId(EntityEntry entry)
        {
            // Exit the current operation when `entry.Entity is Quotation quotation` is true.
            if (entry.Entity is Quotation quotation)
            {
                return quotation.Quotation_ID;
            }

            var directQuotationId = ReadAuditValue(entry, "Quotation_ID");
            // Exit the current operation when `directQuotationId != null` is true.
            if (directQuotationId != null)
            {
                return Convert.ToInt32(directQuotationId, CultureInfo.InvariantCulture);
            }

            // Exit the current operation when `entry.Entity is Quotation_Store store` is true.
            if (entry.Entity is Quotation_Store store)
            {
                return store.Quotation_ID;
            }

            // Exit the current operation when `entry.Entity is Quotation_Items quotationItem` is true.
            if (entry.Entity is Quotation_Items quotationItem)
            {
                return FindQuotationStore(quotationItem.QS_ID)?.Quotation_ID;
            }

            // Enter this branch when `entry.Entity is QuotationItemMaterial material` is true.
            if (entry.Entity is QuotationItemMaterial material)
            {
                var item = FindQuotationItem(material.QI_ID);
                return item == null ? null : FindQuotationStore(item.QS_ID)?.Quotation_ID;
            }

            // Enter this branch when `entry.Entity is QuotationItemStore quotationItemStore` is true.
            if (entry.Entity is QuotationItemStore quotationItemStore)
            {
                var item = FindQuotationItem(quotationItemStore.QI_ID);
                return item == null ? null : FindQuotationStore(item.QS_ID)?.Quotation_ID;
            }

            // Exit the current operation when `entry.Entity is Conforme conforme` is true.
            if (entry.Entity is Conforme conforme)
            {
                return conforme.Quotation_ID;
            }

            // Exit the current operation when `entry.Entity is JobOrder jobOrder` is true.
            if (entry.Entity is JobOrder jobOrder)
            {
                return FindConforme(jobOrder.Conforme_FileID)?.Quotation_ID;
            }

            // Enter this branch when `entry.Entity is DeliveryReceipt deliveryReceipt && deliveryReceipt.JobOrderID is int jobOrderId` is true.
            if (entry.Entity is DeliveryReceipt deliveryReceipt &&
                deliveryReceipt.JobOrderID is int jobOrderId)
            {
                var relatedJobOrder = FindJobOrder(jobOrderId);
                return relatedJobOrder == null
                    ? null
                    : FindConforme(relatedJobOrder.Conforme_FileID)?.Quotation_ID;
            }

            // Exit the current operation when `entry.Entity is JobOrderItems jobOrderItem` is true.
            if (entry.Entity is JobOrderItems jobOrderItem)
            {
                return ResolveQuotationIdForJobOrder(jobOrderItem.JobOrder_ID);
            }

            // Enter this branch when `entry.Entity is DeliveryReceiptItems receiptItem` is true.
            if (entry.Entity is DeliveryReceiptItems receiptItem)
            {
                var receipt = FindDeliveryReceipt(receiptItem.Delivery_ID);
                return receipt?.JobOrderID is int receiptJobOrderId
                    ? ResolveQuotationIdForJobOrder(receiptJobOrderId)
                    : null;
            }

            // Enter this branch when `entry.Entity is DeliveryFiles deliveryFile` is true.
            if (entry.Entity is DeliveryFiles deliveryFile)
            {
                var receipt = FindDeliveryReceipt(deliveryFile.Delivery_ID);
                return receipt?.JobOrderID is int fileJobOrderId
                    ? ResolveQuotationIdForJobOrder(fileJobOrderId)
                    : null;
            }

            // Exit the current operation when `entry.Entity is ServiceInvoice serviceInvoice` is true.
            if (entry.Entity is ServiceInvoice serviceInvoice)
            {
                return serviceInvoice.JobOrder_ID is int invoiceJobOrderId
                    ? ResolveQuotationIdForJobOrder(invoiceJobOrderId)
                    : null;
            }

            // Enter this branch when `entry.Entity is ServiceInvoiceItem serviceInvoiceItem` is true.
            if (entry.Entity is ServiceInvoiceItem serviceInvoiceItem)
            {
                var invoice = FindServiceInvoice(serviceInvoiceItem.ServiceInvoice_ID);
                return invoice?.JobOrder_ID is int itemJobOrderId
                    ? ResolveQuotationIdForJobOrder(itemJobOrderId)
                    : null;
            }

            // Exit the current operation when `entry.Entity is ServiceInvoiceFile serviceInvoiceFile` is true.
            if (entry.Entity is ServiceInvoiceFile serviceInvoiceFile)
            {
                return ResolveQuotationIdForJobOrder(serviceInvoiceFile.JobOrderID);
            }

            // Exit the current operation when `entry.Entity is CollectionReceipt collectionReceipt` is true.
            if (entry.Entity is CollectionReceipt collectionReceipt)
            {
                return ResolveQuotationIdForJobOrder(collectionReceipt.JobOrderID);
            }

            // Exit the current operation when `entry.Entity is InstallationReport installationReport` is true.
            if (entry.Entity is InstallationReport installationReport)
            {
                return ResolveQuotationIdForJobOrder(installationReport.JobOrder_ID);
            }

            // Enter this branch when `entry.Entity is InstallationReportFile installationReportFile` is true.
            if (entry.Entity is InstallationReportFile installationReportFile)
            {
                var report = InstallationReport.Local.FirstOrDefault(record =>
                    record.InstallationReport_ID == installationReportFile.InstallationReport_ID)
                    ?? InstallationReport.AsNoTracking().FirstOrDefault(record =>
                        record.InstallationReport_ID == installationReportFile.InstallationReport_ID);
                return report == null ? null : ResolveQuotationIdForJobOrder(report.JobOrder_ID);
            }

            // Exit the current operation when `entry.Entity is Installation_Schedule installationSchedule` is true.
            if (entry.Entity is Installation_Schedule installationSchedule)
            {
                return ResolveQuotationIdForJobOrder(installationSchedule.JobOrder_ID);
            }

            // Exit the current operation when `entry.Entity is JobOrderComment jobOrderComment` is true.
            if (entry.Entity is JobOrderComment jobOrderComment)
            {
                return ResolveQuotationIdForJobOrder(jobOrderComment.JobOrder_ID);
            }

            // Exit the current operation when `entry.Entity is PurchaseOrder purchaseOrder` is true.
            if (entry.Entity is PurchaseOrder purchaseOrder)
            {
                return FindConforme(purchaseOrder.Conforme_FileID)?.Quotation_ID;
            }

            return null;
        }

        // Resolves the quotation id for job order.
        private int? ResolveQuotationIdForJobOrder(int jobOrderId)
        {
            var jobOrder = FindJobOrder(jobOrderId);
            return jobOrder == null
                ? null
                : FindConforme(jobOrder.Conforme_FileID)?.Quotation_ID;
        }

        // Finds the quotation that matches the supplied criteria.
        private Quotation? FindQuotation(int id) =>
            ChangeTracker.Entries<Quotation>()
                .FirstOrDefault(entry => entry.Entity.Quotation_ID == id)?.Entity
            ?? Quotation.AsNoTracking().FirstOrDefault(record => record.Quotation_ID == id);

        // Finds the quotation store that matches the supplied criteria.
        private Quotation_Store? FindQuotationStore(int id) =>
            ChangeTracker.Entries<Quotation_Store>()
                .FirstOrDefault(entry => entry.Entity.QS_ID == id)?.Entity
            ?? Quotation_Store.AsNoTracking().FirstOrDefault(record => record.QS_ID == id);

        // Finds the quotation item that matches the supplied criteria.
        private Quotation_Items? FindQuotationItem(int id) =>
            ChangeTracker.Entries<Quotation_Items>()
                .FirstOrDefault(entry => entry.Entity.QI_ID == id)?.Entity
            ?? Quotation_Items.AsNoTracking().FirstOrDefault(record => record.QI_ID == id);

        // Finds the conforme that matches the supplied criteria.
        private Conforme? FindConforme(int id) =>
            ChangeTracker.Entries<Conforme>()
                .FirstOrDefault(entry => entry.Entity.FileID == id)?.Entity
            ?? Conforme.AsNoTracking().FirstOrDefault(record => record.FileID == id);

        // Finds the job order that matches the supplied criteria.
        private JobOrder? FindJobOrder(int id) =>
            ChangeTracker.Entries<JobOrder>()
                .FirstOrDefault(entry => entry.Entity.JobOrder_ID == id)?.Entity
            ?? JobOrder.AsNoTracking().FirstOrDefault(record => record.JobOrder_ID == id);

        // Finds the delivery receipt that matches the supplied criteria.
        private DeliveryReceipt? FindDeliveryReceipt(int id) =>
            ChangeTracker.Entries<DeliveryReceipt>()
                .FirstOrDefault(entry => entry.Entity.Delivery_ID == id)?.Entity
            ?? DeliveryReceipt.AsNoTracking().FirstOrDefault(record => record.Delivery_ID == id);

        // Finds the service invoice that matches the supplied criteria.
        private ServiceInvoice? FindServiceInvoice(int id) =>
            ChangeTracker.Entries<ServiceInvoice>()
                .FirstOrDefault(entry => entry.Entity.ServiceInvoice_ID == id)?.Entity
            ?? ServiceInvoice.AsNoTracking().FirstOrDefault(record => record.ServiceInvoice_ID == id);

        // Finds the client name that matches the supplied criteria.
        private string? FindClientName(int id) =>
            ChangeTracker.Entries<ClientEntity>()
                .FirstOrDefault(entry => entry.Entity.Client_ID == id)?.Entity.Client_Name
            ?? Clients.AsNoTracking()
                // Apply this predicate in the database query before materializing results.
                .Where(client => client.Client_ID == id)
                .Select(client => client.Client_Name)
                .FirstOrDefault();

        // Finds the client that matches the supplied criteria.
        private ClientEntity? FindClient(int id) =>
            ChangeTracker.Entries<ClientEntity>()
                .FirstOrDefault(entry => entry.Entity.Client_ID == id)?.Entity
            ?? Clients.AsNoTracking().FirstOrDefault(client => client.Client_ID == id);

        // Converts an entity type name into a readable audit label.
        private static string HumanizeEntityName(string entityName) =>
            string.Concat(entityName.Select((character, index) =>
                index > 0 && char.IsUpper(character) && !char.IsUpper(entityName[index - 1])
                    ? $" {character}"
                    : character.ToString()))
                .Replace('_', ' ')
                .Trim();

        // Converts pending audit changes into rows ready to persist.
        private List<AuditTrail> ToRows(List<PendingAudit> pending)
        {
            // Exit the current operation when `pending.Count == 0` is true.
            if (pending.Count == 0) return new();

            var (name, department) = ResolveActor();
            // Capture one UTC instant so the timestamps written by this operation remain consistent.
            var now = DateTime.Now;

            return pending.Select(p =>
            {
                // Builds audit records from pending changes and resolved actor details.
                var id = p.Id ?? KeyOf(p.Entry);
                var actorName = name;
                var actorDepartment = department;
                var associatedUserId = p.Entry.Entity switch
                {
                    UserSecurityAnswer answer => (int?)answer.User_ID,
                    PasswordRecoveryChallenge challenge => challenge.User_ID,
                    _ => null
                };
                // Enter this branch when `associatedUserId is int userId` is true.
                if (associatedUserId is int userId)
                {
                    var associatedActor = ResolveAssociatedActor(userId);
                    // Enter this branch when `!string.IsNullOrWhiteSpace(associatedActor.name)` is true.
                    if (!string.IsNullOrWhiteSpace(associatedActor.name))
                        actorName = associatedActor.name;
                    // Enter this branch when `!string.IsNullOrWhiteSpace(associatedActor.department)` is true.
                    if (!string.IsNullOrWhiteSpace(associatedActor.department))
                        actorDepartment = associatedActor.department;
                }

                var operation = p.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Deleted => "Deleted",
                    _ => "Edited"
                };
                var details = new APINIMIRHEMAA.DTO.AuditTrail.AuditTrailDetailsDTO
                {
                    Entity = p.Entity,
                    RecordId = id,
                    Operation = operation,
                    BatchId = p.BatchId,
                    DocumentLabel = p.DocumentLabel,
                    ActionDescription = p.State switch
                    {
                        EntityState.Added => "has been created",
                        EntityState.Deleted => "has been deleted",
                        _ => $"changed {string.Join("; ", p.Changes)}"
                    },
                    PreviousValues = p.PreviousValues,
                    CurrentValues = p.CurrentValues
                };
                // Enter this branch when `p.Entry.Entity is Quotation quotation && FindClient(quotation.Client_ID) is { } client` is true.
                if (p.Entry.Entity is Quotation quotation &&
                    FindClient(quotation.Client_ID) is { } client)
                {
                    AddQuotationClientTerms(details.PreviousValues, quotation, client);
                    AddQuotationClientTerms(details.CurrentValues, quotation, client);
                }
                var action = "AUDIT_DETAILS_V1:" + JsonSerializer.Serialize(details);

                return new AuditTrail
                {
                    Name = actorName,
                    Date = DateOnly.FromDateTime(now),
                    Time = TimeOnly.FromDateTime(now),
                    Action = action,
                    Department = actorDepartment
                };
            }).ToList();
        }

        // Adds the quotation client terms.
        private static void AddQuotationClientTerms(
            Dictionary<string, string?>? values,
            Quotation quotation,
            ClientEntity client)
        {
            // Exit the current operation when `values == null` is true.
            if (values == null)
            {
                return;
            }

            var termsKey = values.Keys.FirstOrDefault(key =>
                key.Equals("Terms_Condition", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("TermsAndCondition", StringComparison.OrdinalIgnoreCase))
                ?? "Terms_Condition";
            // Enter this branch when `string.IsNullOrWhiteSpace(values.GetValueOrDefault(termsKey))` is true.
            if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(termsKey)))
            {
                values[termsKey] = quotation.Terms_Condition ?? client.TermsAndCondition;
            }
            values["Payment_Terms"] = client.Payment_Terms;
        }

        // Resolves the employee name and department for the related user; returns null values when that user record is not found. Inputs: userId identifies the account to look up. Returns the resolved name and department.
        private (string? name, string? department) ResolveAssociatedActor(int userId)
        {
            var actor = (from account in Users.AsNoTracking()
                         join employeeRecord in Employee.AsNoTracking()
                             on account.Employee_ID equals employeeRecord.Employee_Id into employees
                         from employee in employees.DefaultIfEmpty()
                         join departmentRecord in Departments.AsNoTracking()
                             on account.Department_ID equals departmentRecord.Department_ID into departments
                         from department in departments.DefaultIfEmpty()
                         where account.User_ID == userId
                         select new
                         {
                             account.Username,
                             FirstName = employee == null ? null : employee.FirstName,
                             LastName = employee == null ? null : employee.LastName,
                             DepartmentName = department == null ? null : department.Department_Name
                         }).FirstOrDefault();
            // Exit the current operation when `actor == null` is true.
            if (actor == null)
            {
                return (null, null);
            }

            var name = $"{actor.FirstName} {actor.LastName}".Trim();
            return (string.IsNullOrWhiteSpace(name) ? actor.Username : name, actor.DepartmentName);
        }

        // Resolves the current request actor from claims, falling back to the linked employee record and route-derived department when needed. Returns the actor name and department.
        private (string name, string department) ResolveActor()
        {
            var user = _http.HttpContext?.User;
            var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = user?.FindFirstValue("EmployeeName")
                ?? user?.FindFirstValue(ClaimTypes.Name)
                ?? "Unknown user";

            // Employee full name, same lookup auth.js did (User -> Employee)
            if (string.IsNullOrWhiteSpace(user?.FindFirstValue("EmployeeName")) &&
                int.TryParse(userId, out var uid))
            {
                var emp = (from u in Users.AsNoTracking()
                           join e in Employee.AsNoTracking() on u.Employee_ID equals e.Employee_Id
                           where u.User_ID == uid
                           select new { e.FirstName, e.LastName }).FirstOrDefault();
                // Enter this branch when `emp != null` is true.
                if (emp != null) name = $"{emp.FirstName} {emp.LastName}".Trim();
            }

            // Same route-to-department mapping as getAuditDepartment()
            var path = _http.HttpContext?.Request.Path.Value?.ToLower() ?? "";
            var department =
                path.Contains("/marketing/") ? "Marketing" :
                path.Contains("/production/") ? "JobOrder" :
                path.Contains("/technical/") ? "Technical" :
                path.Contains("/inventory/") ? "Warehouse" :
                path.Contains("/admin/") || path.Contains("/users/") ? "Admin" :
                user?.FindFirstValue("Department") ?? "Admin";

            return (name, department);
        }

        // Saves pending audit records after the entity changes are persisted.
        private void FlushAudits(List<PendingAudit> pending)
        {
            try
            {
                var rows = ToRows(pending);
                // Exit the current operation when `rows.Count == 0` is true.
                if (rows.Count == 0) return;
                AuditTrail.AddRange(rows);
                // Persist the pending entity changes to the database at this point.
                base.SaveChanges(true);
            }
            // Handle `Exception ex` raised by the preceding operation.
            catch (Exception ex) { Console.Error.WriteLine("Audit trail save failed: " + ex.Message); }
        }

        // Saves pending audit records after the entity changes are persisted.
        private async Task FlushAuditsAsync(List<PendingAudit> pending, CancellationToken ct)
        {
            try
            {
                var rows = ToRows(pending);
                // Exit the current operation when `rows.Count == 0` is true.
                if (rows.Count == 0) return;
                AuditTrail.AddRange(rows);
                // Persist the pending entity changes to the database at this point.
                await base.SaveChangesAsync(true, ct);
            }
            // Handle `Exception ex` raised by the preceding operation.
            catch (Exception ex) { Console.Error.WriteLine("Audit trail save failed: " + ex.Message); }
        }
    }
}
