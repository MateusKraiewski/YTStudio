using Microsoft.EntityFrameworkCore;
using YTStudio.Core.Models;

namespace YTStudio.Core.Data;

public class StudioDbContext(DbContextOptions<StudioDbContext> options) : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Script> Scripts => Set<Script>();
    public DbSet<ScriptVersion> ScriptVersions => Set<ScriptVersion>();
    public DbSet<Publication> Publications => Set<Publication>();

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

        b.Entity<Script>(e =>
        {
            e.ToTable("scripts");
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Tags).HasMaxLength(2000);
            e.Property(x => x.ThumbnailIdea).HasMaxLength(1000);
            e.Property(x => x.Status).HasConversion<int>();
            // Excluir um canal não apaga os roteiros: eles ficam "sem canal".
            e.HasOne(x => x.Channel).WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.ScriptId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ChannelId);
            e.HasIndex(x => x.UpdatedAt);
        });

        b.Entity<Publication>(e =>
        {
            e.ToTable("publications");
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.VideoUrl).HasMaxLength(500);
            e.Property(x => x.Status).HasConversion<int>();
            e.Ignore(x => x.ReminderAt);
            // Excluir canal ou roteiro não apaga o item da agenda.
            e.HasOne<Channel>().WithMany().HasForeignKey(x => x.ChannelId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Script>().WithMany().HasForeignKey(x => x.ScriptId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.ScheduledAt);
        });

        b.Entity<ScriptVersion>(e =>
        {
            e.ToTable("script_versions");
            e.HasIndex(x => new { x.ScriptId, x.SavedAt });
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
