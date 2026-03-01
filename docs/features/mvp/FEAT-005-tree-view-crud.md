# FEAT-005: Tree View — CRUD & Organization

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-005 |
| **Title** | Tree View — CRUD & Organization |
| **Category** | MVP |
| **Priority** | P0 (Critical) |
| **PRD Sections** | §5.1.1, §5.1.2, §5.1.4 |
| **Depends On** | FEAT-001 (data model, TreeService) |
| **Dependents** | FEAT-007, FEAT-009, FEAT-013, FEAT-021 |
| **Estimated Complexity** | L (1-2 weeks) |

---

## 1. Overview

### 1.1 Purpose
Implements the left-panel tree view that displays folders and connections in a hierarchical structure. Provides full CRUD operations (create, rename, delete) with inline editing, context menus, and icon differentiation between folders and connections.

### 1.2 Scope
**In Scope:**
- TreeView with HierarchicalDataTemplate
- TreeViewModel managing the tree structure
- TreeEntryViewModel wrapping individual entries
- Create folder/connection (toolbar + context menu)
- Inline rename (F2 → TextBox → Enter/Escape)
- Delete with cascade
- Context menu (New Folder, New Connection, Rename, Delete)
- Folder expand/collapse state persistence
- MaterialDesign icons (Folder, MonitorDashboard)
- Empty state message

**Out of Scope:**
- Drag and drop reordering (FEAT-007)
- Properties editing (FEAT-009)
- Connection double-click to open RDP (FEAT-011)

### 1.3 Key Decisions
- **TreeEntryViewModel wraps entity**: Decouples UI state (IsEditing, IsSelected) from domain entity
- **ObservableCollection for Children**: Enables automatic UI updates on add/remove

### 1.4 Assumptions
> [!NOTE]
> **Implementation Assumptions:**
> - [ASSUMPTION] No confirmation dialog on delete; items are deleted immediately
> - [ASSUMPTION] New items created with default names ("New Folder", "New Connection") and immediately enter edit mode

---

## 2. User Stories & Acceptance Criteria

### US-005-01: View Connection Tree
**As a** user
**I want to** see all my folders and connections in a tree
**So that** I can navigate my organized server inventory

**Acceptance Criteria:**
- [ ] AC1: Tree loads from database on startup
- [ ] AC2: Folders show folder icon, connections show monitor icon
- [ ] AC3: Folders can be expanded/collapsed
- [ ] AC4: Entries sorted by SortOrder within parent
- [ ] AC5: Empty state shows helpful message

### US-005-02: Create Entries
**As a** user
**I want to** create new folders and connections
**So that** I can build my server inventory

**Acceptance Criteria:**
- [ ] AC1: Toolbar buttons create at root or inside selected folder
- [ ] AC2: Context menu creates as child of right-clicked entry
- [ ] AC3: New entry immediately enters inline rename mode
- [ ] AC4: New entry persisted to database

### US-005-03: Rename Entries
**As a** user
**I want to** rename folders and connections inline
**So that** I can organize without opening dialogs

**Acceptance Criteria:**
- [ ] AC1: F2 or context menu starts inline editing
- [ ] AC2: Enter commits the rename
- [ ] AC3: Escape cancels, restoring original name
- [ ] AC4: Empty names rejected (original name restored)

### US-005-04: Delete Entries
**As a** user
**I want to** delete folders and connections
**So that** I can clean up my inventory

**Acceptance Criteria:**
- [ ] AC1: Delete key or context menu removes entry
- [ ] AC2: Deleting folder removes all children
- [ ] AC3: Entry removed from tree and database

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-005-01 | HierarchicalDataTemplate with folder/connection icons | Must | §5.1.1 |
| FR-005-02 | TreeViewModel loads all entries and builds hierarchy | Must | §5.1.1 |
| FR-005-03 | Create folder via toolbar and context menu | Must | §5.1.2 |
| FR-005-04 | Create connection via toolbar and context menu | Must | §5.1.2 |
| FR-005-05 | Inline rename with TextBox (F2, Enter, Escape) | Must | §5.1.2 |
| FR-005-06 | Delete with cascade via Delete key and context menu | Must | §5.1.2 |
| FR-005-07 | Context menu with all CRUD actions | Must | §5.1.4 |
| FR-005-08 | Persist folder expansion state | Should | §5.1.1 |
| FR-005-09 | Entry count for status bar | Should | §5.8 |

---

## 4. UI Components

### 4.1 TreeView

| Aspect | Detail |
|--------|--------|
| **Location** | Left panel (Grid Column 0, 250px default) |
| **Style** | MaterialDesignTreeViewItem |
| **Virtualization** | VirtualizingStackPanel.IsVirtualizing=True |

**Template:**
```
┌──────────────────────┐
│ > 📁 Production      │  ← Folder (expanded)
│   💻 Web-Server-01   │  ← Connection
│   💻 DB-Server-01    │
│ > 📁 Development     │  ← Folder (collapsed)
│ 💻 Jump-Host         │  ← Root-level connection
└──────────────────────┘
```

### 4.2 Inline Edit
When IsEditing=true, TextBlock swaps to TextBox:
- TextBox gets focus and selects all text
- Enter commits, Escape cancels
- LostFocus commits

---

## 5. Testing

### 5.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-005-01 | TreeEntryViewModel wraps entity correctly | All properties mapped |
| UT-005-02 | CommitEdit with valid name | Returns new name, IsEditing=false |
| UT-005-03 | CommitEdit with empty name | Returns null, IsEditing=false |
| UT-005-04 | BeginEdit sets IsEditing=true | EditName = current Name |

### 5.2 Integration Tests

| Test ID | Case | Expected |
|---------|------|----------|
| IT-005-01 | LoadTreeAsync builds hierarchy | Root entries at top, children nested |
| IT-005-02 | AddFolder persists to DB | Entry exists after reload |
| IT-005-03 | DeleteEntry cascades | Children also removed |
| IT-005-04 | RenameEntry updates DB | New name persisted |

---

## 6. Implementation Notes

### 6.1 Key Files
- `ViewModels/TreeViewModel.cs` — Tree state management
- `ViewModels/TreeEntryViewModel.cs` — Single node wrapper
- `Views/MainWindow.xaml` — TreeView with HierarchicalDataTemplate
- `Views/MainWindow.xaml.cs` — Inline edit event handlers
- `Converters/TreeEntryTypeToIconConverter.cs` — Icon resolution

### 6.2 WPF/WinForms Ambiguity
The project has UseWPF=true AND UseWindowsForms=true. All code-behind files must use type aliases for ambiguous types:
```csharp
using TextBox = System.Windows.Controls.TextBox;
using TreeView = System.Windows.Controls.TreeView;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
```

---

## 7. Enhancements (2026-03-01)

### Sort Alphabetically
Added "Sort Alphabetically" context menu item on folders (`SortChildrenCommand` in `TreeViewModel`). Sorts all direct children by name (case-insensitive) and persists the new sort order to the database.

### Duplicate Connection
Context menu on connections includes "Duplicate Connection" which clones a connection with all its properties.

### Connect All
Context menu on folders includes "Connect All" which opens RDP sessions for all child connections recursively.

## 8. References
- §5.1: Tree View — Full specification
- FEAT-007: Adds drag-drop on top of this tree
- FEAT-009: Properties panel uses selected entry from this tree
