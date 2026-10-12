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
        public DbSet<ClientsProject> ClientsProject { get; set; }
        public DbSet<Quotation> Quotation { get; set; }
        public DbSet<Conforme> Conforme { get; set; }
        public DbSet<JobOrder> JobOrder { get; set; }
        public DbSet<AuditTrail> AuditTrail { get; set; }
        public DbSet<CollectionReceipt> CollectionReceipt { get; set; }
        public DbSet<DeliveryFiles> DeliveryFiles { get; set; }
        public DbSet<DeliveryReceipt> DeliveryReceipt { get; set; }
        public DbSet<Graphics> Graphics { get; set; }
        public DbSet<Installation_Schedule> Installation_Schedule { get; set; }
        public DbSet<IssuedBy> IssuedBy { get; set; }
        public DbSet<MaterialRequesition_Slip> MaterialRequisition_Slip { get; set; }
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
        public DbSet<Quotation_Items> Quotation_Items { get; set; }
        public DbSet<Quotation_Store> Quotation_Store { get; set; }
        public DbSet<Employee> Employee { get; set; }
        public DbSet<JobOrderItems> JobOrderItems { get; set; }
        public DbSet<DeliveryReceiptItems> DeliveryReceiptItems { get; set; }


        // ---------- SaveChanges hooks ----------
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            var pending = CollectAudits();
            var result = base.SaveChanges(acceptAllChangesOnSuccess);
            FlushAudits(pending);
            return result;
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
        {
            var pending = CollectAudits();
            var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
            await FlushAuditsAsync(pending, ct);
            return result;
        }

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
                var action = p.State switch
                {
                    EntityState.Added => $"{p.Entity}# {id} has been created",
                    EntityState.Deleted => $"{p.Entity}# {id} has been deleted",
                    _ => $"{p.Entity}#{id} changed {string.Join("; ", p.Changes)}"
                };

                return new AuditTrail
                {
                    Name = name,
                    Date = DateOnly.FromDateTime(now),
                    Time = TimeOnly.FromDateTime(now),
                    Action = action,
                    Department = department
                };
            }).ToList();
        }

        private (string name, string department) ResolveActor()
        {
            var user = _http.HttpContext?.User;
            var userId = user?.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = user?.FindFirstValue(ClaimTypes.Name) ?? "Unknown user";

            // Employee full name, same lookup auth.js did (User -> Employee)
            if (int.TryParse(userId, out var uid))
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
