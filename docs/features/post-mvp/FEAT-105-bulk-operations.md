# FEAT-105: Bulk Operations (Multi-Select)

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-105 |
| **Title** | Bulk Operations (Multi-Select) |
| **Category** | Post-MVP |
| **Status** | **Partially Implemented** |
| **Priority** | P3 (Low) |
| **PRD Sections** | §9.2 |
| **Depends On** | FEAT-005 (tree view) |
| **Dependents** | — |
| **Estimated Complexity** | M (3-5 days) |
| **Last Updated** | 2026-03-01 |

---

## 1. Overview
Enables multi-selection in the tree view (Ctrl+Click, Shift+Click) and bulk operations: delete multiple entries, move multiple entries, connect to multiple servers simultaneously.

## 2. Scope
- [x] Bulk connect (open multiple RDP tabs at once) — via checkbox selection
- [ ] Multi-select in tree view (Ctrl+Click, Shift+Click)
- [ ] Bulk delete
- [ ] Bulk move (drag multiple)
- [ ] Bulk export selected entries

## 3. Implementation Notes (2026-03-01)
Bulk connect implemented using checkboxes on connection entries in the tree view. An `IsChecked` property on `TreeEntryViewModel` tracks selection. When any connections are checked, a "Connect Selected" button appears next to the filter box. The `ConnectSelectedCommand` in `MainWindowViewModel` opens RDP tabs for all checked connections and then clears the checkboxes. Full Ctrl+Click/Shift+Click multi-select is not yet implemented.
