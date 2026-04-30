using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace OneGood.Infrastructure.Data
{
    public class OneGoodDbContextFactory : IDesignTimeDbContextFactory<OneGoodDbContext>
    {
        public OneGoodDbContext CreateDbContext(string[] args)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.Development.json"), optional: true)
                .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), optional: true)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<OneGoodDbContext>();
            var connectionString = config.GetConnectionString("DefaultConnection");
            optionsBuilder.UseNpgsql(connectionString);

            return new OneGoodDbContext(optionsBuilder.Options);
        }
    }
}
