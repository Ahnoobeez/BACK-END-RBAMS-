using APINIMIRHEMAA.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Security.Claims;
using static System.Net.WebRequestMethods;

namespace APINIMIRHEMAA.Data
{
    public class LocalDbContext : DbContext
    {
        private readonly IHttpContextAccessor _http;
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
        public DbSet<Users> Users { get; set; }
        public DbSet<UserSignature> UserSignatures { get; set; }
        public DbSet<UserSecurityAnswer> UserSecurityAnswers { get; set; }
        public DbSet<PasswordRecoveryChallenge> PasswordRecoveryChallenges { get; set; }
        public DbSet<Roles> Roles { get; set; }
        public DbSet<Departments> Departments { get; set; }
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
            var creator = GetCurrentEmployeeName();

            foreach (var entry in ChangeTracker.Entries()
                         .Where(entry => entry.State == EntityState.Added))
            {
                if (entry.Entity is ICreatedByRecord record)
                {
                    record.CreatedBy = creator;
                }
            }

        }

        public string GetCurrentEmployeeName() => ResolveActor().name;

        // ---------- Audit logic ----------
        private sealed class PendingAudit
        {
            public EntityEntry Entry = default!;
            public EntityState State;
            public string Entity = "";
            public string? Id;
            public List<string> Changes = new();
        }

        private List<PendingAudit> CollectAudits()
        {
            ChangeTracker.DetectChanges();
            var list = new List<PendingAudit>();

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
                    Entity = entry.Metadata.ClrType.Name
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
                            p.Changes.Add("client subject was protected");
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
                    Id = answerGroup.Key.ToString()
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

                var action = p.Entity == "UserSecurity"
                    ? $"UserSecurity# {id} has been created"
                    : p.State switch
                {
                    EntityState.Added => $"{p.Entity}# {id} has been created",
                    EntityState.Deleted => $"{p.Entity}# {id} has been deleted",
                    _ => $"{p.Entity}#{id} changed {string.Join("; ", p.Changes)}"
                };

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
