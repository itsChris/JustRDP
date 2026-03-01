using JustRDP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JustRDP.Infrastructure.Persistence.Configurations;

public class TreeEntryConfiguration : IEntityTypeConfiguration<TreeEntry>
{
    public void Configure(EntityTypeBuilder<TreeEntry> builder)
    {
        builder.ToTable("TreeEntries");
        builder.HasKey(e => e.Id);

        builder.HasDiscriminator<string>("EntryType")
            .HasValue<FolderEntry>("Folder")
            .HasValue<ConnectionEntry>("Connection");

        builder.Property(e => e.Name).IsRequired().HasMaxLength(256);
        builder.Property(e => e.SortOrder).HasDefaultValue(0);

        builder.HasOne(e => e.Parent)
            .WithMany(f => f.Children)
            .HasForeignKey(e => e.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ParentId);
        builder.HasIndex(e => new { e.ParentId, e.SortOrder });
    }
}
