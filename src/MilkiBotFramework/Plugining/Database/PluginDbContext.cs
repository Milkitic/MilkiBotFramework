using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MilkiBotFramework.Plugining.Database
{
    public abstract class PluginDbContext : DbContext
    {
        protected sealed override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionStringBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = TemporaryDbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared
            };

            optionsBuilder.UseSqlite(connectionStringBuilder.ToString());
        }

        internal string? TemporaryDbPath { get; set; }
    }
}
