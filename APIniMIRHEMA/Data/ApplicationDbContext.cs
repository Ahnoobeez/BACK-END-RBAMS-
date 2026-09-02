using APIniMIRHEMA.Model;
using Microsoft.EntityFrameworkCore;

namespace APIniMIRHEMA.Data
{
    public class ApplicationDbContext: DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<Mirhema> mirhemas { get; set; }
    }
}
