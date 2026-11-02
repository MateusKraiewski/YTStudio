using Microsoft.EntityFrameworkCore;
using YTStudio.Core.Models;

namespace YTStudio.Core.Data;

public class StudioDbContext(DbContextOptions<StudioDbContext> options) : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Channel>(e =>
        {
            e.ToTable("channels");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Url).HasMaxLength(500);
            e.Property(x => x.Niche).HasMaxLength(100);
            e.Property(x => x.Language).HasMaxLength(50);
            e.Property(x => x.Color).HasMaxLength(9);
            e.HasIndex(x => x.Name);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder c)
    {
        // SQLite não ordena DateTimeOffset nativamente; guardamos em ticks UTC.
        c.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToUtcTicksConverter>();
    }
}

internal sealed class DateTimeOffsetToUtcTicksConverter()
    : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(
        v => v.UtcTicks,
        v => new DateTimeOffset(v, TimeSpan.Zero));
