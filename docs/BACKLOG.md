# Backlog

## BUG-001: DuplicateConnectionAsync does not copy ConnectionType or SSH fields

**Severity:** Medium
**Introduced in:** FEAT-111 (SSH Terminal Connections)
**Discovered during:** FEAT-109 SSH protocol support review

### Description

`TreeService.DuplicateConnectionAsync` copies all RDP-specific properties but does not copy:
- `ConnectionType` (defaults to `RDP` instead of preserving the source value)
- `SshPrivateKeyPath`
- `SshPrivateKeyPassphraseEncrypted`
- `SshTerminalFontFamily`
- `SshTerminalFontSize`

Duplicating an SSH connection produces an RDP connection entry with the wrong type and lost SSH settings.

### Location

`src/JustRDP.Application/Services/TreeService.cs` — `DuplicateConnectionAsync` method

### Fix

Add the missing properties to the `new ConnectionEntry { ... }` initializer:
```csharp
ConnectionType = source.ConnectionType,
SshPrivateKeyPath = source.SshPrivateKeyPath,
SshPrivateKeyPassphraseEncrypted = source.SshPrivateKeyPassphraseEncrypted is not null
    ? (byte[])source.SshPrivateKeyPassphraseEncrypted.Clone()
    : null,
SshTerminalFontFamily = source.SshTerminalFontFamily,
SshTerminalFontSize = source.SshTerminalFontSize,
```
