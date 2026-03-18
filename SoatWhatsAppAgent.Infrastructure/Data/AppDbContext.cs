using SoatWhatsAppAgent.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace SoatWhatsAppAgent.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Conversation>().HasKey(c => c.Id);
        modelBuilder.Entity<Message>().HasKey(m => m.Id);
    }
}