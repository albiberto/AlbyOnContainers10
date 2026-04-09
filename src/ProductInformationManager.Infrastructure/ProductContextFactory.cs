using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProductInformationManager.Infrastructure;

public class ProductContextFactory : IDesignTimeDbContextFactory<ProductContext>
{
    public ProductContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ProductContext>();
        
        // We use a dummy PostgreSQL connection string here to satisfy the Npgsql provider.
        optionsBuilder.UseNpgsql("Host=localhost;Database=dummy;Username=postgres;Password=dummy");

        // Passiamo un array vuoto di interceptors perché a design-time (generazione migrazioni) non sono necessari
        return new ProductContext(optionsBuilder.Options, []);
    }
}
