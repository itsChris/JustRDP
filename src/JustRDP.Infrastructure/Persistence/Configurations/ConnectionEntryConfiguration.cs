using JustRDP.Domain.Entities;
using JustRDP.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JustRDP.Infrastructure.Persistence.Configurations;

public class ConnectionEntryConfiguration : IEntityTypeConfiguration<ConnectionEntry>
{
    public void Configure(EntityTypeBuilder<ConnectionEntry> builder)
    {
        builder.Property(e => e.ConnectionType).HasDefaultValue(ConnectionType.RDP);
        builder.Property(e => e.HostName).HasMaxLength(256);
        builder.Property(e => e.Port).HasDefaultValue(3389);
        builder.Property(e => e.ColorDepth).HasDefaultValue(32);
        builder.Property(e => e.AutoReconnect).HasDefaultValue(true);
        builder.Property(e => e.NetworkLevelAuthentication).HasDefaultValue(true);
        builder.Property(e => e.Compression).HasDefaultValue(true);
        builder.Property(e => e.RedirectClipboard).HasDefaultValue(true);
        builder.Property(e => e.Notes).HasMaxLength(4000);
        builder.Property(e => e.GatewayHostName).HasMaxLength(256);
        builder.Property(e => e.SshTerminalFontFamily).HasMaxLength(128);
    }
}
