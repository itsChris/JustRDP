# FEAT-007: Tree View — Drag & Drop

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-007 |
| **Title** | Tree View — Drag & Drop |
| **Category** | MVP |
| **Priority** | P1 (High) |
| **PRD Sections** | §5.1.3 |
| **Depends On** | FEAT-005 (tree view) |
| **Dependents** | — |
| **Estimated Complexity** | M (3-5 days) |

---

## 1. Overview

### 1.1 Purpose
Adds drag-and-drop support to the tree view, allowing users to reorder entries and move them between folders by dragging.

### 1.2 Scope
**In Scope:**
- Attached behavior `TreeViewDragDropBehavior`
- Drag initiation with minimum distance threshold
- Drop on folder → item becomes child
- Drop on connection → item becomes sibling
- Prevention of dropping on self or descendants
- Sort order persistence after drop
- No drag during inline edit mode

**Out of Scope:**
- Multi-select drag (Post-MVP FEAT-105)
- Drag to external applications

### 1.3 Key Decisions
- **Attached behavior over code-behind**: Keeps XAML clean, behavior reusable
- **WPF DragDrop API**: Standard approach, works with TreeViewItem

---

## 2. User Stories & Acceptance Criteria

### US-007-01: Reorder Entries
**As a** user
**I want to** drag entries to reorder them
**So that** I can organize my tree view

**Acceptance Criteria:**
- [ ] AC1: Drag entry and drop on folder → entry becomes child
- [ ] AC2: Drag entry and drop on connection → entry becomes sibling
- [ ] AC3: Cannot drop on self
- [ ] AC4: Cannot drop on own descendants (prevents circular reference)
- [ ] AC5: Sort order updated in database after drop
- [ ] AC6: Drag not initiated during inline rename

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-007-01 | Drag initiation with SystemParameters minimum distance | Must | §5.1.3 |
| FR-007-02 | Drop on folder creates parent-child relationship | Must | §5.1.3 |
| FR-007-03 | Drop on connection creates sibling relationship | Must | §5.1.3 |
| FR-007-04 | Block self-drop and descendant-drop | Must | §5.1.3 |
| FR-007-05 | Persist sort order changes to database | Must | §5.1.3 |
| FR-007-06 | Skip drag when IsEditing is true | Should | §5.1.3 |

---

## 4. Implementation Notes

### 4.1 Key File
`Behaviors/TreeViewDragDropBehavior.cs` — Static attached behavior with:
- `IsEnabled` attached property
- `TreeViewModel` attached property (for calling MoveEntryAsync)
- Mouse tracking for drag initiation
- DragOver/Drop event handlers with ancestor validation

### 4.2 WPF/WinForms Ambiguity
Must alias: `Point`, `DragEventArgs`, `DragDropEffects`, `DataObject`, `DragDrop`, `TreeView`, `TreeViewItem`

---

## 5. Testing

### 5.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-007-01 | IsDescendant with child | Returns true |
| UT-007-02 | IsDescendant with non-descendant | Returns false |
| UT-007-03 | MoveEntryAsync updates ParentId | Entry re-parented |
| UT-007-04 | MoveEntryAsync updates sort orders | Siblings reindexed |

---

## 6. References
- §5.1.3: Drag and Drop — Full specification
- FEAT-005: Base tree view that this builds on
