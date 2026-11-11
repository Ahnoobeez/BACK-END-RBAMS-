using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using static System.Net.WebRequestMethods;

namespace APINIMIRHEMAA.Data
{
    public class LocalDbContext : DbContext
    {
        private readonly IHttpContextAccessor _http;
        private int? _currentDepartmentId;
        private bool _currentDepartmentIdResolved;
        public LocalDbContext(DbContextOptions<LocalDbContext> options, IHttpContextAccessor http) : base(options)
        {
            _http = http;
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
        public DbSet<InstallationScheduleEmployee> Installation_Schedule_Employee { get; set; }
        public DbSet<InstallationReport> InstallationReport { get; set; }
        public DbSet<IssuedBy> IssuedBy { get; set; }
        public DbSet<MaterialRequesition_Slip> MaterialRequisition_Slip { get; set; }
        public DbSet<Materials> Materials { get; set; }
        public DbSet<PurchaseOrder> PurchaseOrder { get; set; }
        public DbSet<StockTransfer_Slip> StockTransfer_Slip { get; set; }
        public DbSet<Transmittal_Slip> Transmittal_Slip { get; set; }
        public DbSet<ServiceInvoice> ServiceInvoice { get; set; }
        public DbSet<ServiceInvoiceItem> ServiceInvoiceItems { get; set; }
        public DbSet<ServiceInvoiceFile> ServiceInvoiceFiles { get; set; }
        public DbSet<Users> Users { get; set; }
        public DbSet<UserSignature> UserSignatures { get; set; }
        public DbSet<UserSecurityAnswer> UserSecurityAnswers { get; set; }
        public DbSet<PasswordRecoveryChallenge> PasswordRecoveryChallenges { get; set; }
        public DbSet<Roles> Roles { get; set; }
        public DbSet<Departments> Departments { get; set; }
        public DbSet<AccessPermission> AccessPermissions { get; set; }
        public DbSet<Inventory> Inventory { get; set; }
        public DbSet<Purchased_Material> Purchased_Material { get; set; }
        public DbSet<Quotation_Items> Quotation_Items { get; set; }
        public DbSet<QuotationItemMaterial> QuotationItemMaterials { get; set; }
        public DbSet<Quotation_Store> Quotation_Store { get; set; }
        public DbSet<JobOrderComment> JobOrderComments { get; set; }
        public DbSet<Employee> Employee { get; set; }
        public DbSet<JobOrderItems> JobOrderItems { get; set; }
        public DbSet<DeliveryReceiptItems> DeliveryReceiptItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
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
            modelBuilder.Entity<JobOrderComment>(entity =>
            {
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
            modelBuilder.Entity<UserSignature>(entity =>
            {
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
            modelBuilder.Entity<UserSecurityAnswer>(entity =>
            {
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
            SetCreatedByRecords();
            var pending = CollectAudits();
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            FlushAudits(pending);
            return result;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
        {
            SetCreatedByRecords();
            var pending = CollectAudits();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            await FlushAuditsAsync(pending, ct);
            return result;
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

            var creator = GetCurrentEmployeeName();
            var creatorUserId = CurrentUserId;
            var creatorDepartmentId = GetCurrentDepartmentId();

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
            get
            {
                var role = _http.HttpContext?.User.FindFirstValue(ClaimTypes.Role);
                return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(role, "Super Admin", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsDepartmentManager =>
            string.Equals(
                _http.HttpContext?.User.FindFirstValue(ClaimTypes.Role),
                "Department Manager",
                StringComparison.OrdinalIgnoreCase);

        public int? CurrentDepartmentId => GetCurrentDepartmentId();

        public int? GetCurrentDepartmentId()
        {
            if (_currentDepartmentIdResolved)
            {
                return _currentDepartmentId;
            }

            var departmentClaim = _http.HttpContext?.User.FindFirstValue("DepartmentId");
            if (int.TryParse(departmentClaim, out var departmentId) && departmentId > 0)
            {
                _currentDepartmentId = departmentId;
                _currentDepartmentIdResolved = true;
                return _currentDepartmentId;
            }

            var userId = CurrentUserId;
            if (!userId.HasValue)
            {
                _currentDepartmentIdResolved = true;
                return null;
            }

            _currentDepartmentId = Users.Local.FirstOrDefault(user => user.User_ID == userId.Value)?.Department_ID
                ?? Users.AsNoTracking()
                    .Where(user => user.User_ID == userId.Value)
                    .Select(user => (int?)user.Department_ID)
                    .FirstOrDefault();
            _currentDepartmentIdResolved = true;
            return _currentDepartmentId;
        }

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
                filterMethod.MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }

        private void ApplyCreatedByFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ICreatedByRecord
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(record =>
                HasGlobalRecordAccess ||
                (CurrentUserId.HasValue && record.CreatedByUserId == CurrentUserId) ||
                (IsDepartmentManager &&
                    CurrentUserId.HasValue &&
                    CurrentDepartmentId.HasValue &&
                    record.CreatedByDepartmentId == CurrentDepartmentId));
        }

        // ---------- Audit logic ----------
        private static readonly object AuditBatchIdContextKey = new();

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

        private List<PendingAudit> CollectAudits()
        {
            ChangeTracker.DetectChanges();
            var list = new List<PendingAudit>();
            var httpContext = _http.HttpContext;
            var requestedBatchId = httpContext?.Request.Headers["X-Audit-Batch-Id"].FirstOrDefault();
            string? batchId = Guid.TryParse(requestedBatchId, out var parsedBatchId)
                ? parsedBatchId.ToString("N")
                : httpContext?.Items[AuditBatchIdContextKey] as string;
            batchId ??= Guid.NewGuid().ToString("N");
            if (httpContext != null)
            {
                httpContext.Items[AuditBatchIdContextKey] = batchId;
            }

            foreach (var entry in ChangeTracker.Entries()
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

                if (entry.State == EntityState.Modified)
                {
                    foreach (var prop in entry.Properties.Where(x => x.IsModified && !x.Metadata.IsPrimaryKey()))
                    {
                        if (Equals(prop.OriginalValue, prop.CurrentValue)) continue;

                        var name = prop.Metadata.Name;

                        if (name.Contains("password", StringComparison.OrdinalIgnoreCase))
                        {
                            p.Changes.Add("password was changed");   // never log the values
                        }
                        else if (name.Equals("Client_Subject", StringComparison.OrdinalIgnoreCase))
                        {
                            p.Changes.Add("client subject was changed");
                        }
                        else if (name.EndsWith("data", StringComparison.OrdinalIgnoreCase) ||
                                 prop.CurrentValue is byte[] ||
                                 (prop.CurrentValue is string s && s.Length > 200))
                        {
                            p.Changes.Add($"{name.Replace("_", " ").ToLowerInvariant()} was changed");
                        }
                        else
                        {
                            var label = name.Replace("_", " ").ToLowerInvariant();
                            p.Changes.Add($"{label} {Show(prop.OriginalValue)} to {Show(prop.CurrentValue)}");
                        }
                    }

                    if (p.Changes.Count == 0) continue;
                }

                list.Add(p);
            }

            foreach (var answerGroup in ChangeTracker.Entries<UserSecurityAnswer>()
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

        private static string? KeyOf(EntityEntry e) =>
            e.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault() is { } k
                ? e.Property(k.Name).CurrentValue?.ToString()
                : null;

        private static string Show(object? v) =>
            v is null || (v is string s && s == "") ? "(empty)" : v.ToString()!;

        private static Dictionary<string, string?> CaptureAuditValues(
            EntityEntry entry,
            bool useOriginalValues)
        {
            var values = new Dictionary<string, string?>();
            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                var normalizedName = name.Replace("_", "", StringComparison.Ordinal)
                    .ToLowerInvariant();
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
                if (text?.Length > 8000)
                {
                    continue;
                }

                values[name] = text;
            }

            return values;
        }

        private string ResolveAuditDocumentLabel(EntityEntry entry)
        {
            var clientName = ReadAuditValue(entry, "Client_Name") as string
                ?? ReadAuditValue(entry, "ClientName") as string;
            var clientSubject = ReadAuditValue(entry, "Client_Subject") as string;
            var projectTitle = ReadAuditValue(entry, "Title") as string;
            var quotationId = ResolveQuotationId(entry);

            if (quotationId is int id && FindQuotation(id) is { } quotation)
            {
                clientSubject ??= quotation.Client_Subject;
                projectTitle ??= quotation.Title;
                clientName ??= FindClientName(quotation.Client_ID);
            }

            var parts = new[] { clientName, clientSubject, projectTitle }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim());
            var label = string.Join(" - ", parts);
            return string.IsNullOrWhiteSpace(label)
                ? HumanizeEntityName(entry.Metadata.ClrType.Name)
                : label;
        }

        private static object? ReadAuditValue(EntityEntry entry, string propertyName)
        {
            if (entry.Metadata.FindProperty(propertyName) == null)
            {
                return null;
            }

            var property = entry.Property(propertyName);
            return entry.State == EntityState.Deleted
                ? property.OriginalValue
                : property.CurrentValue;
        }

        private int? ResolveQuotationId(EntityEntry entry)
        {
            if (entry.Entity is Quotation quotation)
            {
                return quotation.Quotation_ID;
            }

            var directQuotationId = ReadAuditValue(entry, "Quotation_ID");
            if (directQuotationId != null)
            {
                return Convert.ToInt32(directQuotationId, CultureInfo.InvariantCulture);
            }

            if (entry.Entity is Quotation_Store store)
            {
                return store.Quotation_ID;
            }

            if (entry.Entity is Quotation_Items quotationItem)
            {
                return FindQuotationStore(quotationItem.QS_ID)?.Quotation_ID;
            }

            if (entry.Entity is QuotationItemMaterial material)
            {
                var item = FindQuotationItem(material.QI_ID);
                return item == null ? null : FindQuotationStore(item.QS_ID)?.Quotation_ID;
            }

            if (entry.Entity is Conforme conforme)
            {
                return conforme.Quotation_ID;
            }

            if (entry.Entity is JobOrder jobOrder)
            {
                return FindConforme(jobOrder.Conforme_FileID)?.Quotation_ID;
            }

            if (entry.Entity is DeliveryReceipt deliveryReceipt &&
                deliveryReceipt.JobOrderID is int jobOrderId)
            {
                var relatedJobOrder = FindJobOrder(jobOrderId);
                return relatedJobOrder == null
                    ? null
                    : FindConforme(relatedJobOrder.Conforme_FileID)?.Quotation_ID;
            }

            if (entry.Entity is JobOrderItems jobOrderItem)
            {
                return ResolveQuotationIdForJobOrder(jobOrderItem.JobOrder_ID);
            }

            if (entry.Entity is DeliveryReceiptItems receiptItem)
            {
                var receipt = FindDeliveryReceipt(receiptItem.Delivery_ID);
                return receipt?.JobOrderID is int receiptJobOrderId
                    ? ResolveQuotationIdForJobOrder(receiptJobOrderId)
                    : null;
            }

            if (entry.Entity is DeliveryFiles deliveryFile)
            {
                var receipt = FindDeliveryReceipt(deliveryFile.Delivery_ID);
                return receipt?.JobOrderID is int fileJobOrderId
                    ? ResolveQuotationIdForJobOrder(fileJobOrderId)
                    : null;
            }

            if (entry.Entity is ServiceInvoice serviceInvoice)
            {
                return serviceInvoice.JobOrder_ID is int invoiceJobOrderId
                    ? ResolveQuotationIdForJobOrder(invoiceJobOrderId)
                    : null;
            }

            if (entry.Entity is ServiceInvoiceItem serviceInvoiceItem)
            {
                var invoice = FindServiceInvoice(serviceInvoiceItem.ServiceInvoice_ID);
                return invoice?.JobOrder_ID is int itemJobOrderId
                    ? ResolveQuotationIdForJobOrder(itemJobOrderId)
                    : null;
            }

            if (entry.Entity is ServiceInvoiceFile serviceInvoiceFile)
            {
                return ResolveQuotationIdForJobOrder(serviceInvoiceFile.JobOrderID);
            }

            if (entry.Entity is CollectionReceipt collectionReceipt)
            {
                return ResolveQuotationIdForJobOrder(collectionReceipt.JobOrderID);
            }

            if (entry.Entity is InstallationReport installationReport)
            {
                return ResolveQuotationIdForJobOrder(installationReport.JobOrder_ID);
            }

            if (entry.Entity is Installation_Schedule installationSchedule)
            {
                return ResolveQuotationIdForJobOrder(installationSchedule.JobOrder_ID);
            }

            if (entry.Entity is JobOrderComment jobOrderComment)
            {
                return ResolveQuotationIdForJobOrder(jobOrderComment.JobOrder_ID);
            }

            if (entry.Entity is PurchaseOrder purchaseOrder)
            {
                return FindConforme(purchaseOrder.Conforme_FileID)?.Quotation_ID;
            }

            return null;
        }

        private int? ResolveQuotationIdForJobOrder(int jobOrderId)
        {
            var jobOrder = FindJobOrder(jobOrderId);
            return jobOrder == null
                ? null
                : FindConforme(jobOrder.Conforme_FileID)?.Quotation_ID;
        }

        private Quotation? FindQuotation(int id) =>
            ChangeTracker.Entries<Quotation>()
                .FirstOrDefault(entry => entry.Entity.Quotation_ID == id)?.Entity
            ?? Quotation.AsNoTracking().FirstOrDefault(record => record.Quotation_ID == id);

        private Quotation_Store? FindQuotationStore(int id) =>
            ChangeTracker.Entries<Quotation_Store>()
                .FirstOrDefault(entry => entry.Entity.QS_ID == id)?.Entity
            ?? Quotation_Store.AsNoTracking().FirstOrDefault(record => record.QS_ID == id);

        private Quotation_Items? FindQuotationItem(int id) =>
            ChangeTracker.Entries<Quotation_Items>()
                .FirstOrDefault(entry => entry.Entity.QI_ID == id)?.Entity
            ?? Quotation_Items.AsNoTracking().FirstOrDefault(record => record.QI_ID == id);

        private Conforme? FindConforme(int id) =>
            ChangeTracker.Entries<Conforme>()
                .FirstOrDefault(entry => entry.Entity.FileID == id)?.Entity
            ?? Conforme.AsNoTracking().FirstOrDefault(record => record.FileID == id);

        private JobOrder? FindJobOrder(int id) =>
            ChangeTracker.Entries<JobOrder>()
                .FirstOrDefault(entry => entry.Entity.JobOrder_ID == id)?.Entity
            ?? JobOrder.AsNoTracking().FirstOrDefault(record => record.JobOrder_ID == id);

        private DeliveryReceipt? FindDeliveryReceipt(int id) =>
            ChangeTracker.Entries<DeliveryReceipt>()
                .FirstOrDefault(entry => entry.Entity.Delivery_ID == id)?.Entity
            ?? DeliveryReceipt.AsNoTracking().FirstOrDefault(record => record.Delivery_ID == id);

        private ServiceInvoice? FindServiceInvoice(int id) =>
            ChangeTracker.Entries<ServiceInvoice>()
                .FirstOrDefault(entry => entry.Entity.ServiceInvoice_ID == id)?.Entity
            ?? ServiceInvoice.AsNoTracking().FirstOrDefault(record => record.ServiceInvoice_ID == id);

        private string? FindClientName(int id) =>
            ChangeTracker.Entries<ClientEntity>()
                .FirstOrDefault(entry => entry.Entity.Client_ID == id)?.Entity.Client_Name
            ?? Clients.AsNoTracking()
                .Where(client => client.Client_ID == id)
                .Select(client => client.Client_Name)
                .FirstOrDefault();

        private ClientEntity? FindClient(int id) =>
            ChangeTracker.Entries<ClientEntity>()
                .FirstOrDefault(entry => entry.Entity.Client_ID == id)?.Entity
            ?? Clients.AsNoTracking().FirstOrDefault(client => client.Client_ID == id);

        private static string HumanizeEntityName(string entityName) =>
            string.Concat(entityName.Select((character, index) =>
                index > 0 && char.IsUpper(character) && !char.IsUpper(entityName[index - 1])
                    ? $" {character}"
                    : character.ToString()))
                .Replace('_', ' ')
                .Trim();

        private List<AuditTrail> ToRows(List<PendingAudit> pending)
        {
            if (pending.Count == 0) return new();

            var (name, department) = ResolveActor();
            var now = DateTime.Now;

            return pending.Select(p =>
            {
                var id = p.Id ?? KeyOf(p.Entry);
                var actorName = name;
                var actorDepartment = department;
                var associatedUserId = p.Entry.Entity switch
                {
                    UserSecurityAnswer answer => (int?)answer.User_ID,
                    PasswordRecoveryChallenge challenge => challenge.User_ID,
                    _ => null
                };
                if (associatedUserId is int userId)
                {
                    var associatedActor = ResolveAssociatedActor(userId);
                    if (!string.IsNullOrWhiteSpace(associatedActor.name))
                        actorName = associatedActor.name;
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

        private static void AddQuotationClientTerms(
            Dictionary<string, string?>? values,
            Quotation quotation,
            ClientEntity client)
        {
            if (values == null)
            {
                return;
            }

            var termsKey = values.Keys.FirstOrDefault(key =>
                key.Equals("Terms_Condition", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("TermsAndCondition", StringComparison.OrdinalIgnoreCase))
                ?? "Terms_Condition";
            if (string.IsNullOrWhiteSpace(values.GetValueOrDefault(termsKey)))
            {
                values[termsKey] = quotation.Terms_Condition ?? client.TermsAndCondition;
            }
            values["Payment_Terms"] = client.Payment_Terms;
        }

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
            if (actor == null)
            {
                return (null, null);
            }

            var name = $"{actor.FirstName} {actor.LastName}".Trim();
            return (string.IsNullOrWhiteSpace(name) ? actor.Username : name, actor.DepartmentName);
        }

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

        private void FlushAudits(List<PendingAudit> pending)
        {
            try
            {
                var rows = ToRows(pending);
                if (rows.Count == 0) return;
                AuditTrail.AddRange(rows);
                base.SaveChanges(true);
            }
            catch (Exception ex) { Console.Error.WriteLine("Audit trail save failed: " + ex.Message); }
        }

        private async Task FlushAuditsAsync(List<PendingAudit> pending, CancellationToken ct)
        {
            try
            {
                var rows = ToRows(pending);
                if (rows.Count == 0) return;
                AuditTrail.AddRange(rows);
                await base.SaveChangesAsync(true, ct);
            }
            catch (Exception ex) { Console.Error.WriteLine("Audit trail save failed: " + ex.Message); }
        }
    }
}
