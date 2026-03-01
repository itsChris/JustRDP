# FEAT-017: Import/Export (JSON & .rdp)

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-017 |
| **Title** | Import/Export (JSON & .rdp) |
| **Category** | MVP |
| **Priority** | P1 (High) |
| **PRD Sections** | §5.5 |
| **Depends On** | FEAT-001 (data model) |
| **Dependents** | — |
| **Estimated Complexity** | M (3-5 days) |

---

## 1. Overview

### 1.1 Purpose
Enables data portability by allowing users to import connections from standard .rdp files and JSON tree structures, and export their entire connection tree as JSON. This supports migration from other RDP managers and backup/restore workflows.

### 1.2 Scope
**In Scope:**
- Import from .rdp files (key:type:value format)
- Import from JSON files (hierarchical tree structure)
- Export entire tree as pretty-printed JSON
- File open/save dialogs with filters
- Multi-file import (batch)
- Tree reload after import

**Out of Scope:**
- Password import/export (intentionally excluded for security)
- Import from Royal TS .rtsz files (Post-MVP)
- Import from mRemoteNG .xml files (Post-MVP)

### 1.3 Key Decisions
- **No password export**: Passwords never leave DPAPI protection boundary
- **JSON format**: Custom tree structure with Type discriminator and recursive Children arrays
- **.rdp format**: Standard Microsoft key:type:value format for maximum compatibility

### 1.4 Assumptions
> [!NOTE]
> **Implementation Assumptions:**
> - [ASSUMPTION] .rdp files are UTF-8 or ASCII encoded
> - [ASSUMPTION] Import creates new entries (no merge/update of existing)

---

## 2. User Stories & Acceptance Criteria

### US-017-01: Import .rdp Files
**As a** user
**I want to** import standard .rdp files
**So that** I can migrate connections from other tools

**Acceptance Criteria:**
- [ ] AC1: File dialog filters for .rdp files
- [ ] AC2: Multiple files can be selected
- [ ] AC3: Each file creates one ConnectionEntry
- [ ] AC4: Connection name from filename
- [ ] AC5: Host, port, and settings parsed from key:type:value format
- [ ] AC6: Tree reloads showing imported entries

### US-017-02: Import JSON Tree
**As a** user
**I want to** import a JSON tree export
**So that** I can restore from backup

**Acceptance Criteria:**
- [ ] AC1: File dialog filters for .json files
- [ ] AC2: Hierarchical structure (folders with children) recreated
- [ ] AC3: All connection settings imported except passwords
- [ ] AC4: Tree reloads showing imported structure

### US-017-03: Export as JSON
**As a** user
**I want to** export my entire tree as JSON
**So that** I can back up my configuration

**Acceptance Criteria:**
- [ ] AC1: Save dialog with .json filter
- [ ] AC2: Pretty-printed JSON output
- [ ] AC3: Recursive folder/children structure
- [ ] AC4: Passwords NOT included in export
- [ ] AC5: Usernames and domains included

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-017-01 | Parse .rdp key:type:value format | Must | §5.5.3 |
| FR-017-02 | Parse JSON tree structure | Must | §5.5.1 |
| FR-017-03 | Export tree as JSON with Children nesting | Must | §5.5.2 |
| FR-017-04 | Passwords never exported | Must | §5.5.2 |
| FR-017-05 | Multi-file import support | Should | §5.5.1 |
| FR-017-06 | Status bar shows import/export result | Should | §5.5 |

---

## 4. .rdp File Format

### Supported Keys

| Key | Type | Maps To |
|-----|------|---------|
| full address | s | HostName (and Port if :port suffix) |
| server port | i | Port |
| username | s | CredentialUsername |
| domain | s | CredentialDomain |
| desktopwidth | i | DesktopWidth |
| desktopheight | i | DesktopHeight |
| session bpp | i | ColorDepth |
| smart sizing | i | ResizeBehavior (1=SmartSizing) |
| enablecredsspsupport | i | NetworkLevelAuthentication |
| compression | i | Compression |
| redirectclipboard | i | RedirectClipboard |
| redirectprinters | i | RedirectPrinters |
| redirectdrives | i | RedirectDrives |
| redirectsmartcards | i | RedirectSmartCards |
| redirectcomports | i | RedirectPorts |
| audiomode | i | AudioRedirectionMode |
| autoreconnection enabled | i | AutoReconnect |
| gatewayhostname | s | GatewayHostName |
| gatewayusagemethod | i | GatewayUsageMethod |

---

## 5. JSON Format

### Export Structure
```json
[
  {
    "Type": "Folder",
    "Name": "Production",
    "CredentialUsername": "admin",
    "CredentialDomain": "CORP",
    "Children": [
      {
        "Type": "Connection",
        "Name": "Web Server 01",
        "HostName": "10.0.0.1",
        "Port": 3389,
        "RedirectClipboard": true,
        "Notes": "Primary web server"
      }
    ]
  }
]
```

---

## 6. Testing

### 6.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-017-01 | Parse .rdp with full address:s:host:port | HostName and Port extracted |
| UT-017-02 | Parse .rdp with all supported keys | All properties mapped |
| UT-017-03 | Import JSON with nested folders | Hierarchy recreated |
| UT-017-04 | Export tree excludes passwords | No password fields in output |

---

## 7. Implementation Notes

### 7.1 Key Files
- `Infrastructure/Import/RdpFileParser.cs` — .rdp parser
- `Infrastructure/Import/JsonTreeImporter.cs` — JSON importer
- `Infrastructure/Export/RdpFileExporter.cs` — .rdp exporter
- `Infrastructure/Export/JsonTreeExporter.cs` — JSON exporter
- `Application/Services/ImportExportService.cs` — Orchestration

---

## 8. References
- §5.5: Import/Export — Full specification
- §5.5.3: .rdp File Format Support — Key listing
