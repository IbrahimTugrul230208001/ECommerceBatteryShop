using ECommerceBatteryShop.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.DataAccess
{
    public class BatteryShopCOntext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Username=postgres;Password=ibrahim06;Database=ecommercebatteryshop;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            const string trCollation = "tr_icu_det";   // PostgreSQL deterministic ICU collation

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(b => b.Name)
                      .UseCollation(trCollation);
            });

            base.OnModelCreating(modelBuilder);
        }

    }
}
