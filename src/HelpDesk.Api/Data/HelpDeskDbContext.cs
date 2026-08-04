using HelpDesk.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HelpDesk.Api.Data;

public class HelpDeskDbContext : DbContext
{
    public HelpDeskDbContext(DbContextOptions<HelpDeskDbContext> options) : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<TicketComment> Comments => Set<TicketComment>();

    /// <summary>
    /// Every timestamp in this system is UTC. Neither SQLite nor SQL Server records the
    /// <see cref="DateTimeKind"/>, so a value read back would come out as Unspecified and then
    /// serialise without the trailing "Z" — leaving clients to guess the offset. These converters
    /// normalise on write and re-stamp Kind=Utc on read.
    /// </summary>
    private static readonly ValueConverter<DateTime, DateTime> UtcConverter = new(
        toDatabase => toDatabase.Kind == DateTimeKind.Utc ? toDatabase : toDatabase.ToUniversalTime(),
        fromDatabase => DateTime.SpecifyKind(fromDatabase, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcConverter = new(
        toDatabase => toDatabase == null
            ? null
            : (toDatabase.Value.Kind == DateTimeKind.Utc ? toDatabase.Value : toDatabase.Value.ToUniversalTime()),
        fromDatabase => fromDatabase == null
            ? null
            : DateTime.SpecifyKind(fromDatabase.Value, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Title).IsRequired().HasMaxLength(120);
            entity.Property(t => t.Description).IsRequired().HasMaxLength(2000);
            entity.Property(t => t.RaisedBy).IsRequired().HasMaxLength(80);
            entity.Property(t => t.RaisedByEmail).HasMaxLength(120);
            entity.Property(t => t.AssignedTo).HasMaxLength(80);

            // Store enums as text so the database stays readable and adding a new
            // member later cannot silently re-map existing rows.
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
            entity.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);

            entity.Ignore(t => t.IsOverdue);

            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Priority);
            entity.HasIndex(t => t.CreatedAt);

            entity.HasMany(t => t.Comments)
                  .WithOne(c => c.Ticket!)
                  .HasForeignKey(c => c.TicketId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketComment>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Author).IsRequired().HasMaxLength(80);
            entity.Property(c => c.Body).IsRequired().HasMaxLength(1000);
            entity.HasIndex(c => c.TicketId);
        });

        // Applied last so it covers every DateTime property on every entity mapped above.
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(DateTime))
            {
                property.SetValueConverter(UtcConverter);
            }
            else if (property.ClrType == typeof(DateTime?))
            {
                property.SetValueConverter(NullableUtcConverter);
            }
        }
    }
}
