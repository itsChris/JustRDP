# Backlog

## BUG-012: Dashboard hides open tabs with no way to return (FIXED)

**Severity:** High
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed (2026-03-21)

When connections are open and user clicks the Dashboard tree node, `IsDashboardVisible = true` hides the entire tab panel (including tab headers). Clicking a non-dashboard tree entry did not restore the tabs — there was no way to get back to open connections without opening a new one.

**Fix:** In `OnSelectionChanged`, when a non-dashboard tree entry is selected and `OpenTabs.Count > 0`, set `IsDashboardVisible = false` to show the tabs again.

---

## BUG-011: Tree drag-drop to root level silently fails (FIXED)

**Severity:** High
**Introduced in:** FEAT-007 (Drag & Drop)
**Status:** Fixed (2026-03-21)

Dropping an item onto the empty area of the TreeView (to move it to root level) was silently ignored. `TreeView_Drop` returned early when `FindAncestor<TreeViewItem>` found no target. Additionally, there was no visual feedback during drag operations — no drop indicator lines or highlights.

**Fix:**
- Handle null `targetItem` in both `DragOver` and `Drop` as a root-level drop
- Added `DropAdorner` with three visual states: blue line for before/after, blue highlight for into-folder
- Added position-aware drop zones (top/middle/bottom thirds of item header)
- Replaced `MoveEntryAsync` with `MoveEntryToPositionAsync` that computes insertion index after removal (fixing off-by-one within same parent)
- Added "Move to..." context menu with `MoveToDialog` folder picker as alternative to drag-drop

---

## BUG-002: NullReferenceException in UpdateSortOrdersInMemory when drag-dropping to root (FIXED)

**Severity:** Critical
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

Dashboard node has `Entity = null!`. When drag-dropping an item to root level, `UpdateSortOrdersInMemory` iterates all siblings in `RootEntries` (including Dashboard) and accesses `siblings[i].Entity.SortOrder`, causing NRE.

**Fix:** Skip `IsDashboard` entries in `UpdateSortOrdersInMemory` and exclude Dashboard from sort order persistence.

---

## BUG-003: RecentConnections.Count binding uses int instead of bool for BoolToVisibilityConverter (FIXED)

**Severity:** High
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

`DashboardView.xaml` binds `Visibility="{Binding RecentConnections.Count, Converter={StaticResource BoolToVis}}"` but `BoolToVisibilityConverter` expects `bool`, not `int`. The Recent Connections section may never display.

**Fix:** Add `HasRecentConnections` computed property to `DashboardViewModel` and bind to it.

---

## BUG-004: DashboardViewModel._isLoading defaults to false — loading state never visible (FIXED)

**Severity:** Medium
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

Spec requires `_isLoading = true` default so loading state shows on startup until first `Refresh()`. Implementation defaults to `false` and `Refresh()` doesn't set `IsLoading = false`.

**Fix:** Default `_isLoading = true`, set `IsLoading = false` in `Refresh()`.

---

## BUG-005: Properties panel missing "Last connected" and "Times connected" fields (FIXED)

**Severity:** Medium
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

FR-113-15 / US-113-05 AC5 require Properties panel to show "Last connected" and "Times connected" for connections. `PropertiesViewModel` was not modified.

**Fix:** Add `LastConnected` and `TimesConnected` properties to `PropertiesViewModel` and display in Properties panel.

---

## BUG-006: Recent Connections missing empty state text "No recent connections" (FIXED)

**Severity:** Low
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

US-113-04 AC6 requires "No recent connections" text when the list is empty. Currently the section is just hidden.

**Fix:** Add TextBlock with "No recent connections" bound to inverse of `HasRecentConnections`.

---

## BUG-007: Dashboard stat cards missing icons (FIXED)

**Severity:** Low
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

US-113-02 AC5 requires each stat card to have an icon. Currently only text labels.

**Fix:** Add PackIcon to each card.

---

## BUG-008: All Connections DataGrid missing default column sort configuration (FIXED)

**Severity:** Low
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

US-113-03 AC3 requires sortable columns with default sort by name. DataGrid has default sortability but no explicit `SortDirection` set.

**Fix:** Add `CanUserSortColumns="True"` and `SortDirection="Ascending"` on the Name column.

---

## BUG-009: Double-click on dashboard row doesn't validate connection still exists (FIXED)

**Severity:** Low
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

US-113-03 AC9 requires double-click to validate connection still exists; if deleted since last refresh, dashboard refreshes with log warning.

**Fix:** Add existence check in `ConnectFromDashboard` that refreshes dashboard if connection not found in tree.

---

## BUG-010: Enter key doesn't activate dashboard view when Dashboard node selected (FIXED)

**Severity:** Low
**Introduced in:** FEAT-113 (Dashboard)
**Status:** Fixed

US-113-01 AC7 requires Enter to activate the dashboard view.

**Fix:** Handle Enter in `TreeViewItem_MouseDoubleClick` equivalent or InputBindings.

---

## ~~BUG-001: DuplicateConnectionAsync does not copy ConnectionType or SSH fields~~ (FIXED)

**Severity:** Medium
**Introduced in:** FEAT-111 (SSH Terminal Connections)
**Discovered during:** FEAT-109 SSH protocol support review
**Status:** Fixed

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
