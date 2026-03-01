# FEAT-001: Data Model & Persistence

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-001 |
| **Title** | Data Model & Persistence |
| **Category** | MVP |
| **Priority** | P0 (Critical) |
| **PRD Sections** | §3, §4, §8 |
| **Depends On** | — (Foundation) |
| **Dependents** | FEAT-003, FEAT-005, FEAT-007, FEAT-009, FEAT-011, FEAT-013, FEAT-015, FEAT-017, FEAT-019, FEAT-021 |
| **Estimated Complexity** | M (3-5 days) |

---

## 1. Overview

### 1.1 Purpose
Establishes the domain model, database schema, and persistence layer for JustRDP. This is the foundational feature that all other features depend on. It defines the TreeEntry hierarchy (FolderEntry, ConnectionEntry), the AppSettings table, and the EF Core DbContext with SQLite storage.

### 1.2 Scope
**In Scope:**
- Domain entities: TreeEntry (abstract), FolderEntry, ConnectionEntry
- Value object: Credential
- Enum: TreeEntryType
- Repository interfaces: ITreeEntryRepository, ISettingsRepository, ICredentialEncryptor
- EF Core DbContext with TPH mapping
- SQLite database at %LOCALAPPDATA%\JustRDP\justrdp.db
- AppSettings table for key-value storage
- DI container setup with Microsoft.Extensions.Hosting
- Application services: TreeService, ImportExportService

**Out of Scope:**
- Credential encryption implementation (FEAT-003)
- UI components (FEAT-005+)
- RDP connection logic (FEAT-011)

### 1.3 Key Decisions
- **TPH over TPT**: Single `TreeEntries` table with discriminator column simplifies queries and avoids joins for tree operations
- **SQLite over SQL Server**: Zero-config local database, no installation needed, single-file backup
- **EnsureCreatedAsync over Migrations**: Simpler for v1; migrations can be added later if schema evolves
- **Guid PKs**: Enables offline creation without DB roundtrip, important for import scenarios

### 1.4 Assumptions
> [!NOTE]
> **Implementation Assumptions:**
> - [ASSUMPTION] No migration strategy for v1; schema changes require DB recreation or manual ALTER
> - [ASSUMPTION] WAL mode not explicitly configured; SQLite default journaling used

---

## 2. User Stories & Acceptance Criteria

### US-001-01: Application Data Persistence
**As a** user
**I want to** have my connections and folders saved automatically
**So that** they persist across application restarts

**Acceptance Criteria:**
- [ ] AC1: SQLite database created at %LOCALAPPDATA%\JustRDP\justrdp.db on first launch
- [ ] AC2: Directory created automatically if it doesn't exist
- [ ] AC3: Folders and connections saved to database on creation
- [ ] AC4: Data survives application restart

### US-001-02: Hierarchical Data Organization
**As a** user
**I want to** organize connections in nested folders
**So that** I can group related servers logically

**Acceptance Criteria:**
- [ ] AC1: Folders can contain other folders and connections
- [ ] AC2: Deleting a folder deletes all its children (cascade)
- [ ] AC3: Sort order preserved within each parent

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-001-01 | TreeEntry abstract base with Id, Name, ParentId, SortOrder, timestamps | Must | §4.1 |
| FR-001-02 | FolderEntry with IsExpanded, credential fields, Children collection | Must | §4.2 |
| FR-001-03 | ConnectionEntry with all RDP properties (host, port, display, redirection, etc.) | Must | §4.3 |
| FR-001-04 | TPH discriminator "Folder"/"Connection" on single TreeEntries table | Must | §8.2 |
| FR-001-05 | Self-referencing ParentId FK with cascade delete | Must | §8.2 |
| FR-001-06 | AppSettings table with Key (PK) and Value columns | Must | §8.2 |
| FR-001-07 | ITreeEntryRepository with CRUD, GetAncestors, GetChildren, UpdateSortOrder | Must | §3 |
| FR-001-08 | ISettingsRepository with Get, Set, GetAll | Must | §3 |
| FR-001-09 | TreeService wrapping repository with business logic | Must | §3 |
| FR-001-10 | DI host with all services registered | Must | §3.3 |
| FR-001-11 | Database auto-created on startup | Must | §8.1 |

---

## 4. Data Model

### 4.1 Entities

See PRD §4 for full entity definitions. Key files:
- `src/JustRDP.Domain/Entities/TreeEntry.cs`
- `src/JustRDP.Domain/Entities/FolderEntry.cs`
- `src/JustRDP.Domain/Entities/ConnectionEntry.cs`
- `src/JustRDP.Domain/ValueObjects/Credential.cs`
- `src/JustRDP.Domain/Enums/TreeEntryType.cs`

### 4.2 Database Configuration
- `TreeEntryConfiguration` — TPH discriminator, FK, indexes
- `ConnectionEntryConfiguration` — Column defaults (port 3389, color depth 32, etc.)
- `AppSettingConfiguration` — Key as PK, max lengths

---

## 5. Testing

### 5.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-001-01 | Create FolderEntry with default values | Id generated, Name empty, SortOrder 0 |
| UT-001-02 | Create ConnectionEntry with defaults | Port=3389, ColorDepth=32, AutoReconnect=true |
| UT-001-03 | Credential.IsEmpty when no username/password | Returns true |
| UT-001-04 | Credential.IsInherited when InheritedFromName set | Returns true |

### 5.2 Integration Tests

| Test ID | Case | Expected |
|---------|------|----------|
| IT-001-01 | Save and retrieve FolderEntry | Data roundtrips correctly |
| IT-001-02 | Save ConnectionEntry with all fields | All properties persisted |
| IT-001-03 | Delete folder cascades to children | Children removed |
| IT-001-04 | GetAncestors walks up tree | Returns parent chain in order |
| IT-001-05 | GetNextSortOrder returns max+1 | Correct next value |

---

## 6. Implementation Notes

### 6.1 Code Structure
```
src/JustRDP.Domain/
├── Entities/          TreeEntry, FolderEntry, ConnectionEntry
├── ValueObjects/      Credential
├── Enums/             TreeEntryType
└── Interfaces/        ITreeEntryRepository, ICredentialEncryptor, ISettingsRepository

src/JustRDP.Application/
├── Services/          TreeService, ImportExportService
├── DTOs/              TreeEntryDto, ConnectionDto, FolderDto
└── Mapping/           TreeEntryMappingExtensions

src/JustRDP.Infrastructure/
├── Persistence/       JustRdpDbContext, Configurations/, Repositories/
└── Security/          DpapiCredentialEncryptor (interface here, impl in FEAT-003)
```

### 6.2 Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.EntityFrameworkCore.Sqlite | 10.0.* | SQLite ORM |
| Microsoft.Extensions.Hosting | 10.0.* | DI container |

---

## 7. References

### PRD Sections
- §3: Architecture — Clean Architecture structure
- §4: Domain Model — All entity definitions
- §8: Data Storage — SQLite location, table schemas

### Related Features
- FEAT-003: Implements ICredentialEncryptor
- FEAT-005: Consumes TreeService for CRUD
- FEAT-017: Consumes ImportExportService
