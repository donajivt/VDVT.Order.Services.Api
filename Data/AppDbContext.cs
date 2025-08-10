using Microsoft.EntityFrameworkCore;
using VDVT.Order.Services.Api.Models;

namespace VDVT.Order.Services.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<OrderHeader> OrderHeaders { get; set; }

        public DbSet<OrderDetails> OrderDetails { get; set; }
    }
}
