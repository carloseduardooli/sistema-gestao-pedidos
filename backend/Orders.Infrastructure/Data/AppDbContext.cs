namespace Orders.Infrastructure.Data;

using Microsoft.EntityFrameworkCore;
using Orders.Core.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Cliente).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Produto).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Valor).HasColumnType("decimal(18,2)");
            
            // Index no Status ajuda muito o Worker que vai fazer polling para achar pedidos pendentes
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired().HasMaxLength(100);
            
            // Transformamos o payload em um campo JSONB nativo do Postgres
            entity.Property(e => e.Payload).HasColumnType("jsonb");
            
            // Index no ProcessedAt é vital para a performance da query do Outbox
            entity.HasIndex(e => e.ProcessedAt);
        });
    }
}
