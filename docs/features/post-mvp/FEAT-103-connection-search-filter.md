# FEAT-103: Connection Search & Filter

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-103 |
| **Title** | Connection Search & Filter |
| **Category** | Post-MVP |
| **Status** | **Partially Implemented** |
| **Priority** | P2 (Medium) |
| **PRD Sections** | §9.2 |
| **Depends On** | FEAT-005 (tree view) |
| **Dependents** | — |
| **Estimated Complexity** | M (3-5 days) |
| **Last Updated** | 2026-03-01 |

---

## 1. Overview
Adds a search box above the tree view that filters entries by name, hostname, or other properties in real-time. Matching entries highlighted, non-matching entries hidden or dimmed.

## 2. Scope
- [x] Search TextBox above tree view
- [x] Real-time filtering as user types
- [x] Search by Name (case-insensitive)
- [x] Clear search button (MaterialDesign HasClearButton)
- [x] Auto-expand folders with matching children
- [ ] Search by HostName, Notes
- [ ] Keyboard shortcut (Ctrl+F) to focus search

## 3. Implementation Notes (2026-03-01)
Basic name-based filtering implemented via `TreeViewModel.ApplyFilter()` with `FilteredRootEntries` and `FilteredChildren` observable collections. The filter box is positioned above the tree panel using a `DockPanel` layout. Parent folders of matching entries are automatically expanded during filtering.
