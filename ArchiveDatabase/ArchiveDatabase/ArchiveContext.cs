using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;

namespace ArchiveDatabase
{
    public class ArchiveContext : DbContext
    {
        public DbSet<DatabasePOCOs.Category> Categories { get; set; }
        public DbSet<DatabasePOCOs.Document> Documents { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Properties<DateTime>().Configure(x => x.HasColumnType("datetime2"));
        }
    }
}
