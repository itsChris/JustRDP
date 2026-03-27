# FEAT-113: Dashboard

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-113 |
| **Title** | Dashboard |
| **Category** | Post-MVP |
| **Priority** | P2 (Medium) |
| **PRD Sections** | §9.2 |
| **Depends On** | FEAT-001 (data model), FEAT-005 (tree view), FEAT-011 (RDP sessions), FEAT-111 (SSH sessions) |
| **Dependents** | — |
| **Subsumes** | FEAT-107 (Connection History) — `LastConnectedAt` and `ConnectCount` tracking |
| **Status** | **Done** |
| **Estimated Complexity** | L (1-2 weeks) |

---

## 1. Overview

### 1.1 Purpose
Adds a Dashboard as the default home view of JustRDP, replacing the empty tab area with a combined monitoring and quick-access panel. A permanent "Dashboard" root node in the tree provides one-click navigation back to this view. Connection usage history (last connected, connect count) is tracked to power a "Recent Connections" list.

### 1.2 Scope
**In Scope:**
- Permanent "Dashboard" node at the top of the tree (not persisted in DB — synthetic/virtual)
- Dashboard view rendered in the tab content area when the Dashboard node is selected
- Stats summary bar: total connections, online/offline counts, open sessions, RDP/SSH breakdown
- All Connections table: list of all connections with status, name, host, protocol, port, last connected
- Recent connections: top 10 most recently connected, with double-click to reconnect
- New fields on `ConnectionEntry`: `LastConnectedAt` (DateTime?), `ConnectCount` (int)
- Dedicated `UpdateUsageAsync` method on `TreeService` for atomic usage tracking
- EF Core migration for new columns
- Dashboard auto-refreshes when availability monitor cycles, tabs open/close, or tree changes

**Out of Scope:**
- Connection uptime history / historical charting
- Customizable dashboard layout or widgets
- Drag-and-drop reordering of dashboard sections
- Persistent "favorites" system (future enhancement)
- Dashboard in a detached/floating window

### 1.3 Key Decisions
- **Synthetic tree node via second constructor**: The Dashboard node is a `TreeEntryViewModel` created with a dedicated constructor that does not require a `TreeEntry` entity. It sets `IsDashboard = true` and hardcodes `Id = Guid.Empty`, `Name = "Dashboard"`, `EntryType = TreeEntryType.Folder`. This avoids creating a dummy entity, keeps the existing constructor unchanged, and is injected at position 0 of `FilteredRootEntries` at load time.
- **Not a tab — replaces the empty state**: The Dashboard is rendered in the tab content area but is NOT an `IConnectionTab`. It replaces the existing "Double-click a connection to open a session" empty-state `TextBlock`. The `MainWindow.xaml` content area has three visibility states: (1) Dashboard visible, (2) connection tab visible, (3) neither applies — but in practice, the dashboard is always shown when no tab is selected, so the old empty-state `TextBlock` is removed entirely.
- **Stats from existing services**: All statistics are computed from `TreeViewModel` (entry counts, connection types), `AvailabilityMonitorService` (online/offline), and `MainWindowViewModel` (open tabs). No new services needed.
- **History on ConnectionEntry** (not a separate table): `LastConnectedAt` and `ConnectCount` are simple fields on `ConnectionEntry`, updated via a dedicated `UpdateUsageAsync` method that only touches those two columns. This avoids race conditions with full-entity saves from the Properties dialog. FEAT-107 can later add the full session history table if detailed logging is desired.
- **Double-click to connect**: Consistent with tree behavior — double-click a row in the All Connections table or Recent Connections list to open the connection.
- **Dedicated usage tracking method**: A new `TreeService.UpdateUsageAsync(Guid connectionId)` method updates only `LastConnectedAt` and `ConnectCount` via a targeted SQL update, avoiding conflicts with concurrent full-entity saves from the Properties dialog or other operations.

### 1.4 Assumptions
> [!NOTE]
> **Implementation Assumptions:**
> - [ASSUMPTION] The availability monitor data is available synchronously via `AvailabilityMonitorService` properties
> - [ASSUMPTION] `TreeViewModel.RootEntries` can be walked to collect all connections for the overview
> - [ASSUMPTION] Quick connect (temporary) connections are excluded from history tracking (no DB entity)

---

## 2. User Stories & Acceptance Criteria

### US-113-01: Dashboard Tree Node
**As a** user
**I want to** see a "Dashboard" entry at the top of the tree
**So that** I can always navigate back to the home overview

**Acceptance Criteria:**
- [ ] AC1: A "Dashboard" node with a `Home` PackIcon appears as the first entry in the tree, above all folders and connections
- [ ] AC2: The Dashboard node is always visible (not affected by tree filtering)
- [ ] AC3: The Dashboard node cannot be renamed, deleted, dragged, or have children
- [ ] AC4: The Dashboard node has no checkbox, no context menu, no availability dot
- [ ] AC5: Clicking the Dashboard node shows the dashboard view in the tab content area
- [ ] AC6: The Dashboard node is selected by default when the app starts
- [ ] AC7: Keyboard shortcuts (F2 rename, Delete) are no-ops when the Dashboard node is selected; Enter activates the dashboard view

### US-113-02: Stats Summary
**As a** user
**I want to** see a quick summary of my connections at the top of the dashboard
**So that** I get an instant overview of my environment

**Acceptance Criteria:**
- [ ] AC1: Stats bar shows: Total Connections, Online, Offline, Open Sessions
- [ ] AC2: Stats bar shows RDP count and SSH count separately
- [ ] AC3: Stats update in real-time when connections are added/removed, tabs open/close, or monitor cycles
- [ ] AC4: When monitoring is disabled, Online/Offline counts show "—" or are hidden
- [ ] AC5: Each stat is displayed as a card/chip with an icon and value

### US-113-03: All Connections Table
**As a** user
**I want to** see all my connections with their availability status in one list
**So that** I can monitor my infrastructure at a glance

**Acceptance Criteria:**
- [ ] AC1: A table labeled "All Connections" shows: Status dot | Name | Host | Protocol | Port | Last Connected
- [ ] AC2: Status dot is green (online), red (offline), or gray (unknown/monitoring disabled)
- [ ] AC3: Rows are sortable by clicking column headers; click toggles ascending/descending; default sort: status (Available > Unavailable > Unknown) then name ascending; single-column sort is sufficient
- [ ] AC4: Double-clicking a row opens the connection (same as double-clicking in tree)
- [ ] AC5: "Last Connected" shows relative time (see Section 5.7 for full format table: "Just now", "X min ago", "X hours ago", "X days ago", "dd MMM yyyy", or "Never")
- [ ] AC6: The list updates when the availability monitor completes a cycle
- [ ] AC7: Protocol column shows "RDP" or "SSH"
- [ ] AC8: Empty state: "No connections yet. Create a connection from the toolbar or tree."
- [ ] AC9: Double-click validates the connection still exists; if deleted since last refresh, the dashboard refreshes and shows a brief log warning

### US-113-04: Recent Connections
**As a** user
**I want to** see my 10 most recently connected servers
**So that** I can quickly reconnect to servers I use often

**Acceptance Criteria:**
- [ ] AC1: A "Recent Connections" section shows the 10 most recently connected entries
- [ ] AC2: Each row shows: Status dot | Name | Host | Protocol | Last Connected
- [ ] AC3: Double-clicking a row opens the connection
- [ ] AC4: Only connections with `LastConnectedAt != null` appear
- [ ] AC5: The list updates immediately when a new connection is opened
- [ ] AC6: Empty state: "No recent connections"

### US-113-05: Connection Usage Tracking
**As a** user
**I want** my connection usage to be tracked automatically
**So that** the dashboard can show recent and frequently used connections

**Acceptance Criteria:**
- [ ] AC1: `LastConnectedAt` is set to `DateTime.UtcNow` when a connection tab is opened
- [ ] AC2: `ConnectCount` is incremented by 1 when a connection tab is opened
- [ ] AC3: Quick connect (temporary) connections are not tracked (no DB entity to update)
- [ ] AC4: Usage tracking does not block or delay the connection opening
- [ ] AC5: Properties panel shows "Last connected" and "Times connected" for connection entries (see Section 4.5)
- [ ] AC6: If usage tracking fails (e.g., DB locked), the error is logged as a warning but the connection still opens normally
- [ ] AC7: Duplicating a connection resets `LastConnectedAt` to null and `ConnectCount` to 0

---

## 3. Functional Requirements

| Req ID | Requirement | Priority |
|--------|-------------|----------|
| FR-113-01 | Add `LastConnectedAt` (DateTime?) to `ConnectionEntry` | Must |
| FR-113-02 | Add `ConnectCount` (int, default 0) to `ConnectionEntry` | Must |
| FR-113-03 | EF Core migration adding `LastConnectedAt` and `ConnectCount` columns | Must |
| FR-113-04 | Add `TreeService.UpdateUsageAsync(Guid connectionId)` — targeted update of only `LastConnectedAt` and `ConnectCount` | Must |
| FR-113-05 | Call `UpdateUsageAsync` in `OpenConnectionAsync` (fire-and-forget with error logging) | Must |
| FR-113-06 | Synthetic "Dashboard" `TreeEntryViewModel` via second constructor, injected at tree load | Must |
| FR-113-07 | Dashboard view: stats bar with Total, Online, Offline, Open Sessions, RDP/SSH counts | Must |
| FR-113-08 | Dashboard view: "All Connections" table with all connections | Must |
| FR-113-09 | Dashboard view: "Recent Connections" list (top 10 by `LastConnectedAt` desc) | Must |
| FR-113-10 | Double-click on dashboard rows opens the connection (with existence validation) | Must |
| FR-113-11 | Dashboard visibility: shown when Dashboard node selected or no tabs open; hidden when a connection tab is selected. Replaces existing empty-state `TextBlock`. | Must |
| FR-113-12 | Relative time display for `LastConnectedAt` (see Section 5.7 format table) | Should |
| FR-113-13 | Dashboard node excluded from: filtering, drag-drop, rename, delete, context menu, checkbox, export, keyboard shortcuts (F2, Delete) | Must |
| FR-113-14 | Dashboard data refreshes on: monitor cycle complete, tab open/close, tree reload. Relative times are recalculated on each refresh. | Must |
| FR-113-15 | Properties panel shows "Last connected" and "Times connected" for connections | Should |
| FR-113-16 | Import/Export: include `LastConnectedAt` and `ConnectCount` in JSON (usage metadata export is optional — see Section 8) | Should |
| FR-113-17 | `DuplicateConnectionAsync` resets `LastConnectedAt` to null and `ConnectCount` to 0 | Must |

---

## 4. UI Layout

### 4.1 Tree View — Dashboard Node

```
🏠 Dashboard                    ← Always first, Home (PackIconKind) icon
📁 Production Servers
  🖥️ web-srv-01
  🖥️ web-srv-02
  💻 db-srv-01
📁 Development
  🖥️ dev-machine
  💻 build-agent
```

### 4.2 Dashboard View — Full Layout

The stats bar is pinned at the top (does not scroll). The "Recent Connections" and "All Connections" sections scroll together below it in a single `ScrollViewer`.

```
┌──────────────────────────────────────────────────────────────────────┐
│  [PINNED — Stats Bar]                                                │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐│
│  │ 📊 Total │ │ 🟢 Online│ │ 🔴 Offln │ │ 🔌 Open  │ │ 🖥️5  💻3  ││
│  │    8     │ │    5     │ │    3     │ │    2     │ │  RDP  SSH  ││
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └────────────┘│
│┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄│
│  [SCROLLABLE AREA]                                                   │
│                                                                      │
│  ── Recent Connections ──────────────────────────────────────────── │
│                                                                      │
│  Status │ Name           │ Host          │ Protocol │ Last Connected│
│  ───────┼────────────────┼───────────────┼──────────┼───────────────│
│   🟢    │ web-srv-01     │ 10.0.1.10     │ RDP      │ 2 min ago    │
│   🟢    │ db-srv-01      │ 10.0.1.50     │ SSH      │ 1 hour ago   │
│   🔴    │ jump-box       │ 10.0.2.5      │ SSH      │ 3 days ago   │
│                                                                      │
│  ── All Connections ─────────────────────────────────────────────── │
│                                                                      │
│  Status │ Name           │ Host          │ Protocol │ Port │ Last   │
│  ───────┼────────────────┼───────────────┼──────────┼──────┼────────│
│   🟢    │ web-srv-01     │ 10.0.1.10     │ RDP      │ 3389 │ 2m ago │
│   🟢    │ web-srv-02     │ 10.0.1.11     │ RDP      │ 3389 │ 5d ago │
│   🟢    │ db-srv-01      │ 10.0.1.50     │ SSH      │ 22   │ 1h ago │
│   🔴    │ db-srv-02      │ 10.0.1.51     │ SSH      │ 22   │ Never  │
│   🟢    │ jump-box       │ 10.0.2.5      │ SSH      │ 22   │ 3d ago │
│   🔴    │ monitoring-01  │ 10.0.3.10     │ SSH      │ 22   │ Never  │
│   🔴    │ dev-machine    │ 192.168.1.10  │ RDP      │ 3389 │ 7d ago │
│   🔴    │ build-agent    │ 192.168.1.20  │ SSH      │ 2222 │ Never  │
│                                                                      │
│                          Double-click to connect                     │
└──────────────────────────────────────────────────────────────────────┘
```

### 4.3 Dashboard View — Loading State

Shown while `TreeVM.LoadTreeAsync()` is in progress on app startup:

```
┌──────────────────────────────────────────────────────────────────────┐
│                                                                      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐│
│  │ 📊 Total │ │ 🟢 Online│ │ 🔴 Offln │ │ 🔌 Open  │ │ 🖥️—  💻—  ││
│  │    —     │ │    —     │ │    —     │ │    0     │ │  RDP  SSH  ││
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └────────────┘│
│                                                                      │
│                         Loading connections...                       │
│                     [ProgressBar — Indeterminate]                    │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

### 4.4 Dashboard View — Empty State (No Connections)

```
┌──────────────────────────────────────────────────────────────────────┐
│                                                                      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌────────────┐│
│  │ 📊 Total │ │ 🟢 Online│ │ 🔴 Offln │ │ 🔌 Open  │ │ 🖥️0  💻0  ││
│  │    0     │ │    0     │ │    0     │ │    0     │ │  RDP  SSH  ││
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘ └────────────┘│
│                                                                      │
│                                                                      │
│                                                                      │
│         No connections yet. Create a connection from the             │
│         toolbar or right-click the tree to get started.              │
│                                                                      │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

### 4.5 Properties Panel — Usage Fields

When a `ConnectionEntry` is selected in the tree, the properties panel shows the new fields after the existing Port field:

```
  Name:           web-srv-01
  Type:           RDP
  Host:           10.0.1.10
  Port:           3389
  Last connected: 2 min ago              ← NEW (RelativeTimeConverter)
  Times connected: 42                    ← NEW
  Credentials:    admin (inherited)
  Notes:          Production web server
```

### 4.6 Dashboard Visibility Rules

The existing "Double-click a connection to open a session" empty-state `TextBlock` in `MainWindow.xaml` is removed. The dashboard replaces it.

```
App starts          → Dashboard visible (Dashboard node selected)
Click Dashboard     → Dashboard visible
Double-click conn   → Connection tab opens, dashboard hidden
Close all tabs      → Dashboard visible again
Click conn tab      → Dashboard hidden
Click Dashboard     → Dashboard visible (tab still open in background)
```

Visibility logic:
- `IsDashboardVisible = true` → DashboardView `Visible`, tab ItemsControl `Collapsed`
- `IsDashboardVisible = false` → DashboardView `Collapsed`, tab ItemsControl `Visible`
- The old `HasNoTabs` empty-state `TextBlock` is removed entirely

---

## 5. Technical Design

### 5.1 Architecture

```
Domain Layer:
  src/JustRDP.Domain/Entities/ConnectionEntry.cs          ← MODIFIED (add LastConnectedAt, ConnectCount)

Infrastructure Layer:
  src/JustRDP.Infrastructure/Migrations/                   ← MODIFIED (add migration for new columns)
  src/JustRDP.Infrastructure/Repositories/TreeRepository.cs ← MODIFIED (UpdateUsageAsync)

Application Layer:
  src/JustRDP.Application/Services/TreeService.cs          ← MODIFIED (add UpdateUsageAsync)

Presentation Layer:
  src/JustRDP.Presentation/ViewModels/DashboardViewModel.cs            ← NEW
  src/JustRDP.Presentation/Views/DashboardView.xaml(.cs)               ← NEW
  src/JustRDP.Presentation/ViewModels/TreeViewModel.cs                 ← MODIFIED (inject Dashboard node)
  src/JustRDP.Presentation/ViewModels/TreeEntryViewModel.cs            ← MODIFIED (IsDashboard, second constructor)
  src/JustRDP.Presentation/ViewModels/MainWindowViewModel.cs           ← MODIFIED (dashboard visibility, usage tracking)
  src/JustRDP.Presentation/Views/MainWindow.xaml                       ← MODIFIED (dashboard content area, remove empty state)
  src/JustRDP.Presentation/Converters/RelativeTimeConverter.cs         ← NEW
```

### 5.2 Entity Changes

#### ConnectionEntry (modified)
```csharp
// New properties
public DateTime? LastConnectedAt { get; set; }
public int ConnectCount { get; set; }
```

#### DuplicateConnectionAsync (modified)
In `TreeService.DuplicateConnectionAsync`, the new fields must be reset on the duplicate:
```csharp
// In the duplicate creation:
LastConnectedAt = null,   // new connection, no history
ConnectCount = 0,         // new connection, no history
```

### 5.3 Dashboard Node (Synthetic)

The current `TreeEntryViewModel` constructor requires a `TreeEntry entity` parameter and accesses `entity.Id`, `entity.Name`, `entity.ParentId`, etc. The Dashboard node has no entity. To resolve this, add a **second constructor** for synthetic nodes:

```csharp
// In TreeEntryViewModel — new fields
public bool IsDashboard { get; init; }

// Existing constructor — unchanged
public TreeEntryViewModel(TreeEntry entity) { /* ... existing code ... */ }

// NEW: constructor for synthetic nodes (Dashboard)
public TreeEntryViewModel(string name, bool isDashboard)
{
    IsDashboard = isDashboard;
    Entity = null!;             // no backing entity — guard with IsDashboard checks
    Id = Guid.Empty;
    Name = name;
    ParentId = null;
    SortOrder = -1;             // sorts before all real entries
    EntryType = TreeEntryType.Dashboard;  // dedicated enum value — prevents folder-like behavior (BUG-013 fix)
    ConnectionType = null;
}
```

All code that accesses `Entity` must guard with `IsDashboard` checks. The following locations need guards:
- `TreeViewModel`: `GetCheckedConnections()`, `EntryCount`, `ApplyFilter`, `DeleteEntry`, `RenameEntry`
- `TreeViewDragDropBehavior`: reject Dashboard as drag source or drop target
- `MainWindow.xaml`: context menu binding (suppress for Dashboard), checkbox visibility, availability dot
- `TreeEntryTypeToIconConverter`: return `Home` PackIconKind when `IsDashboard` is true

Created in `TreeViewModel.LoadTreeAsync()` and inserted at index 0 of `RootEntries` / `FilteredRootEntries`. It:
- Has `Name = "Dashboard"`, icon = `Home` (PackIconKind)
- Is excluded from filtering (`ApplyFilter` always keeps it visible)
- Is excluded from drag-drop (both as source and target)
- Has no context menu, no checkbox, no availability dot
- Cannot be renamed or deleted (F2 and Delete are no-ops when selected)
- Is not included in `GetCheckedConnections()` or `EntryCount`
- Is selected by default on app start

### 5.4 DashboardViewModel

```csharp
public partial class DashboardViewModel : ObservableObject
{
    // Stats
    [ObservableProperty] private int _totalConnections;
    [ObservableProperty] private int _onlineCount;
    [ObservableProperty] private int _offlineCount;
    [ObservableProperty] private int _openSessions;
    [ObservableProperty] private int _rdpCount;
    [ObservableProperty] private int _sshCount;
    [ObservableProperty] private bool _isMonitoringEnabled;
    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private bool _hasConnections;

    // Data
    public ObservableCollection<DashboardConnectionItem> AllConnections { get; } = [];
    public ObservableCollection<DashboardConnectionItem> RecentConnections { get; } = [];

    // Connect callback — set by MainWindowViewModel to avoid coupling
    private readonly Func<ConnectionEntry, Task>? _connectCallback;

    public DashboardViewModel(Func<ConnectionEntry, Task>? connectCallback = null)
    {
        _connectCallback = connectCallback;
    }

    [RelayCommand]
    private async Task ConnectFromDashboard(DashboardConnectionItem? item)
    {
        if (item?.Connection is null) return;
        if (_connectCallback is not null)
            await _connectCallback(item.Connection);
    }

    /// <summary>
    /// Refreshes all dashboard data from the provided state snapshot.
    /// </summary>
    public void Refresh(DashboardData data)
    {
        IsLoading = false;
        TotalConnections = data.TotalConnections;
        OnlineCount = data.OnlineCount;
        OfflineCount = data.OfflineCount;
        OpenSessions = data.OpenSessions;
        RdpCount = data.RdpCount;
        SshCount = data.SshCount;
        IsMonitoringEnabled = data.IsMonitoringEnabled;
        HasConnections = data.TotalConnections > 0;

        AllConnections.Clear();
        foreach (var item in data.AllConnections)
            AllConnections.Add(item);

        RecentConnections.Clear();
        foreach (var item in data.RecentConnections)
            RecentConnections.Add(item);
    }
}
```

#### DashboardData (DTO — decouples DashboardViewModel from concrete services)

```csharp
public record DashboardData
{
    public int TotalConnections { get; init; }
    public int OnlineCount { get; init; }
    public int OfflineCount { get; init; }
    public int OpenSessions { get; init; }
    public int RdpCount { get; init; }
    public int SshCount { get; init; }
    public bool IsMonitoringEnabled { get; init; }
    public IReadOnlyList<DashboardConnectionItem> AllConnections { get; init; } = [];
    public IReadOnlyList<DashboardConnectionItem> RecentConnections { get; init; } = [];
}
```

#### DashboardConnectionItem

All properties are `init`-only — items are recreated on each refresh cycle, so relative time strings are always recalculated.

```csharp
public class DashboardConnectionItem
{
    public ConnectionEntry Connection { get; init; }
    public string Name { get; init; }
    public string Host { get; init; }
    public string Protocol { get; init; }   // "RDP" or "SSH"
    public int Port { get; init; }
    public AvailabilityStatus Status { get; init; }
    public DateTime? LastConnectedAt { get; init; }
    public string LastConnectedDisplay { get; init; }  // "2 min ago", "Never"
}
```

### 5.5 Dashboard Visibility

The dashboard is NOT a tab. It's a separate content area in `MainWindow.xaml` that replaces the existing empty-state `TextBlock`:

```csharp
// In MainWindowViewModel
[ObservableProperty]
private bool _isDashboardVisible = true;

// Visibility logic (updated in OnSelectedTabChanged, CloseTab, OnSelectionChanged):
// IsDashboardVisible = true  when: Dashboard tree node is selected, OR no connection tabs open
// IsDashboardVisible = false when: a connection tab is selected (SelectedTab != null)
```

The `MainWindow.xaml` content area changes:
- **Remove**: the "Double-click a connection to open a session" `TextBlock` (was bound to `HasNoTabs`)
- **Add**: `DashboardView` with `Visibility="{Binding IsDashboardVisible, Converter={StaticResource BoolToVis}}"`
- **Existing** `ItemsControl` (tabs): `Visibility="{Binding IsDashboardVisible, Converter={StaticResource InverseBoolToVis}}"`

### 5.6 Usage Tracking

#### New method: `TreeService.UpdateUsageAsync`

A dedicated method that only updates the two usage columns, avoiding conflicts with full-entity saves:

```csharp
// In ITreeEntryRepository (Domain)
Task UpdateUsageAsync(Guid connectionId, DateTime lastConnectedAt, int connectCount);

// In TreeRepository (Infrastructure)
public async Task UpdateUsageAsync(Guid connectionId, DateTime lastConnectedAt, int connectCount)
{
    await _context.Database.ExecuteSqlInterpolatedAsync(
        $"UPDATE TreeEntries SET LastConnectedAt = {lastConnectedAt}, ConnectCount = {connectCount}, ModifiedAt = {DateTime.UtcNow} WHERE Id = {connectionId}");
}

// In TreeService (Application)
public Task UpdateUsageAsync(Guid connectionId, DateTime lastConnectedAt, int connectCount)
    => _repository.UpdateUsageAsync(connectionId, lastConnectedAt, connectCount);
```

#### Calling from `MainWindowViewModel.OpenConnectionAsync`:

```csharp
// After tab is opened and added to OpenTabs:
if (connection.Id != Guid.Empty)  // skip quick connect (temporary entries)
{
    connection.LastConnectedAt = DateTime.UtcNow;
    connection.ConnectCount++;
    _ = Task.Run(async () =>
    {
        try
        {
            await _treeService.UpdateUsageAsync(connection.Id, connection.LastConnectedAt.Value, connection.ConnectCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update usage tracking for {Name}", connection.Name);
        }
    });
}
```

### 5.7 Relative Time Display

A `RelativeTimeConverter` (IValueConverter) converts `DateTime?` to human-readable strings. Values are recalculated on each dashboard refresh cycle.

| Input | Output |
|-------|--------|
| null | "Never" |
| < 1 minute ago | "Just now" |
| < 60 minutes ago | "X min ago" |
| < 24 hours ago | "X hours ago" |
| < 7 days ago | "X days ago" |
| >= 7 days ago | "dd MMM yyyy" |

### 5.8 Dashboard Refresh Triggers

`MainWindowViewModel` builds a `DashboardData` DTO and calls `DashboardVM.Refresh(data)` on:
1. **App startup** — after `TreeVM.LoadTreeAsync()` completes (sets `IsLoading = false`)
2. **Monitor cycle complete** — `AvailabilityMonitorService.SummaryChanged` event
3. **Tab opened/closed** — after `OpenConnectionAsync` or `CloseTab`
4. **Tree changed** — after add/delete/import operations

The `DashboardData` is built by `MainWindowViewModel` by walking `TreeVM.RootEntries`, reading `AvailabilityMonitorService` properties, and computing `OpenTabCount`. This keeps `DashboardViewModel` decoupled from concrete services.

### 5.9 EF Core Migration

New columns on `TreeEntries` table (TPH — `ConnectCount` applies semantically only to Connection rows, but the column exists on all rows with DEFAULT 0 for Folder rows, which is harmless):
- `LastConnectedAt` DATETIME NULL
- `ConnectCount` INT NOT NULL DEFAULT 0

No index needed on `LastConnectedAt` — the "Recent Connections" query (`ORDER BY LastConnectedAt DESC LIMIT 10`) operates on a small dataset (typically < 1000 rows). Revisit if scaling to thousands.

### 5.10 Import/Export

JSON format extended with optional fields:
```json
{
  "Name": "web-srv-01",
  "LastConnectedAt": "2026-03-01T14:30:00Z",
  "ConnectCount": 42
}
```

- Export: includes `LastConnectedAt` and `ConnectCount`
- Import: if absent, defaults to null / 0

---

## 6. Testing

### 6.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-113-01 | RelativeTimeConverter: null input | Returns "Never" |
| UT-113-02 | RelativeTimeConverter: 30 seconds ago | Returns "Just now" |
| UT-113-03 | RelativeTimeConverter: 45 minutes ago | Returns "45 min ago" |
| UT-113-04 | RelativeTimeConverter: 5 hours ago | Returns "5 hours ago" |
| UT-113-05 | RelativeTimeConverter: 3 days ago | Returns "3 days ago" |
| UT-113-06 | RelativeTimeConverter: 30 days ago | Returns "dd MMM yyyy" format |
| UT-113-07 | DashboardViewModel.Refresh: no connections | All counts = 0, empty lists, HasConnections = false |
| UT-113-08 | DashboardViewModel.Refresh: 5 RDP + 3 SSH | TotalConnections=8, RdpCount=5, SshCount=3 |
| UT-113-09 | RecentConnections: sorted by LastConnectedAt desc | Most recent first |
| UT-113-10 | RecentConnections: max 10 items | Only top 10 shown |
| UT-113-11 | RecentConnections: excludes entries with null LastConnectedAt | Only connected entries shown |
| UT-113-12 | Dashboard visibility: app start | IsDashboardVisible = true |
| UT-113-13 | Dashboard visibility: select connection tab | IsDashboardVisible = false |
| UT-113-14 | Dashboard visibility: close all tabs | IsDashboardVisible = true |
| UT-113-15 | Dashboard visibility: click Dashboard node while tab open | IsDashboardVisible = true |
| UT-113-16 | Dashboard visibility: click tab after dashboard | IsDashboardVisible = false |
| UT-113-17 | Dashboard visibility: open tab then close it | IsDashboardVisible transitions false → true |
| UT-113-18 | DuplicateConnection: usage fields reset | LastConnectedAt = null, ConnectCount = 0 |

### 6.2 Integration Tests

| Test ID | Case | Expected |
|---------|------|----------|
| IT-113-01 | App start | Dashboard node visible in tree, dashboard view shown, loading state then data |
| IT-113-02 | Click Dashboard node | Dashboard view shown in content area |
| IT-113-03 | Double-click connection in tree | Tab opens, dashboard hides |
| IT-113-04 | Close all tabs | Dashboard reappears |
| IT-113-05 | Click Dashboard node while tab is open | Dashboard shown, tab still in background |
| IT-113-06 | Click tab after viewing dashboard | Dashboard hides, tab content shown |
| IT-113-07 | Double-click row in All Connections table | Connection opens |
| IT-113-08 | Double-click row in recent connections | Connection opens |
| IT-113-09 | Open connection | LastConnectedAt and ConnectCount updated in DB via UpdateUsageAsync |
| IT-113-10 | Monitor cycle completes | Availability dots update in dashboard |
| IT-113-11 | Add new connection in tree | Dashboard refreshes, new entry in overview |
| IT-113-12 | Delete connection | Dashboard refreshes, entry removed |
| IT-113-13 | Import connections | Dashboard refreshes with imported entries |
| IT-113-14 | Bulk connect 5 connections simultaneously | All LastConnectedAt and ConnectCount values correctly updated |
| IT-113-15 | Double-click dashboard row for deleted connection | Dashboard refreshes, no error |

### 6.3 Manual Tests

| Test ID | Case | Expected |
|---------|------|----------|
| MT-113-01 | Dashboard with 50+ connections | Scrollable, responsive, no lag |
| MT-113-02 | Dashboard with monitoring disabled | Online/Offline show "—", status dots gray |
| MT-113-03 | Dashboard after fresh install (no connections) | Empty state message, stats all 0 |
| MT-113-04 | Tree filter active | Dashboard node still visible |
| MT-113-05 | Drag-drop over Dashboard node | Not accepted as drop target |
| MT-113-06 | Right-click Dashboard node | No context menu appears |
| MT-113-07 | Dark/light theme toggle | Dashboard respects theme |
| MT-113-08 | Press F2 with Dashboard selected | No rename initiated |
| MT-113-09 | Press Delete with Dashboard selected | No delete operation |

---

## 7. Implementation Notes

### 7.1 Key Files

| File | Project | Purpose | Status |
|------|---------|---------|--------|
| `src/JustRDP.Domain/Entities/ConnectionEntry.cs` | Domain | Add `LastConnectedAt`, `ConnectCount` | MODIFIED |
| `src/JustRDP.Domain/Interfaces/ITreeEntryRepository.cs` | Domain | Add `UpdateUsageAsync` | MODIFIED |
| `src/JustRDP.Infrastructure/Migrations/` | Infrastructure | Migration for new columns | MODIFIED |
| `src/JustRDP.Infrastructure/Repositories/TreeRepository.cs` | Infrastructure | Implement `UpdateUsageAsync` | MODIFIED |
| `src/JustRDP.Application/Services/TreeService.cs` | Application | Add `UpdateUsageAsync`, reset in `DuplicateConnectionAsync` | MODIFIED |
| `src/JustRDP.Presentation/ViewModels/DashboardViewModel.cs` | Presentation | Dashboard data + stats + DashboardData DTO | NEW |
| `src/JustRDP.Presentation/Views/DashboardView.xaml(.cs)` | Presentation | Dashboard UI | NEW |
| `src/JustRDP.Presentation/Converters/RelativeTimeConverter.cs` | Presentation | DateTime → "X ago" | NEW |
| `src/JustRDP.Presentation/ViewModels/TreeViewModel.cs` | Presentation | Inject Dashboard node, guard IsDashboard | MODIFIED |
| `src/JustRDP.Presentation/ViewModels/TreeEntryViewModel.cs` | Presentation | `IsDashboard` flag, second constructor | MODIFIED |
| `src/JustRDP.Presentation/ViewModels/MainWindowViewModel.cs` | Presentation | Dashboard visibility, usage tracking, DashboardData builder | MODIFIED |
| `src/JustRDP.Presentation/Views/MainWindow.xaml` | Presentation | Dashboard content area, remove empty state TextBlock | MODIFIED |
| `src/JustRDP.Presentation/ViewModels/PropertiesViewModel.cs` | Presentation | Show last connected + count | MODIFIED |
| `src/JustRDP.Presentation/Behaviors/TreeViewDragDropBehavior.cs` | Presentation | Reject Dashboard as drag/drop target | MODIFIED |
| `src/JustRDP.Infrastructure/Import/JsonTreeImporter.cs` | Infrastructure | Import new fields | MODIFIED |
| `src/JustRDP.Infrastructure/Export/JsonTreeExporter.cs` | Infrastructure | Export new fields | MODIFIED |

### 7.2 Dependencies
- No new NuGet packages required
- Uses existing `MaterialDesignThemes` for card styling
- Uses existing `AvailabilityMonitorService` for online/offline data

### 7.3 Threading Model
- Dashboard refresh runs on UI thread (reading in-memory data, building DTO)
- Usage tracking (`UpdateUsageAsync`) runs fire-and-forget via `Task.Run` with `try/catch` and warning-level logging on failure
- No new background threads or timers — piggybacks on existing monitor timer

### 7.4 Performance Considerations
- Dashboard `Refresh()` walks all connections — O(n) where n = total connections
- For large trees (hundreds of connections), consider caching the connection list
- `RecentConnections` is a LINQ query with `OrderByDescending` + `Take(10)` — negligible cost
- All Connections DataGrid supports virtualization for large lists
- `DashboardConnectionItem` is fully immutable (`init` properties) — recreated on each refresh

### 7.5 Logging
- **Information**: Dashboard refresh (connection count, online count), connection opened from dashboard
- **Warning**: Usage tracking DB update failures (caught, not thrown)
- **Debug**: Dashboard visibility changes, usage tracking updates

---

## 8. Security Considerations

- `LastConnectedAt` and `ConnectCount` are non-sensitive metadata individually, but exported timestamps reveal operational patterns (when connections are used, how frequently, which servers are accessed most). For security-conscious organizations, this metadata could be sensitive.
- Export includes timestamps but no credentials (existing security model)
- Consider making usage metadata export optional in a future enhancement (e.g., "Include usage statistics" checkbox in the export dialog)

---

## 9. Migration Path

### 9.1 Database Migration
- New columns are nullable/defaulted — existing connections unaffected
- `LastConnectedAt` defaults to NULL (shown as "Never" on dashboard)
- `ConnectCount` defaults to 0
- Both columns exist on all TPH rows (including Folder rows) due to table-per-hierarchy; Folder rows have harmless defaults (NULL / 0)

### 9.2 Backward Compatibility
- Dashboard node is synthetic — no DB changes for the tree structure
- JSON import without `LastConnectedAt`/`ConnectCount` defaults to null/0
- Existing workflow unchanged — dashboard is additive, connections still open the same way

### 9.3 Rollback
- Rollback simply involves removing the code; the `LastConnectedAt` and `ConnectCount` columns can remain in the DB harmlessly since they are nullable/defaulted and unused without the dashboard code.

---

## 10. Implementation Order

| Step | Task | Depends On |
|------|------|------------|
| 1 | Add `LastConnectedAt` and `ConnectCount` to `ConnectionEntry` (Domain) | — |
| 2 | Add `UpdateUsageAsync` to `ITreeEntryRepository` and `TreeRepository` (Domain + Infrastructure) | Step 1 |
| 3 | Add `UpdateUsageAsync` to `TreeService`, reset fields in `DuplicateConnectionAsync` (Application) | Step 2 |
| 4 | EF Core migration for new columns (Infrastructure) | Step 1 |
| 5 | Add `IsDashboard` flag + second constructor to `TreeEntryViewModel` | — |
| 6 | Inject synthetic Dashboard node in `TreeViewModel.LoadTreeAsync`, add IsDashboard guards | Step 5 |
| 7 | Exclude Dashboard node from filtering, drag-drop, CRUD, context menu, keyboard shortcuts, export | Step 6 |
| 8 | Create `RelativeTimeConverter` | — |
| 9 | Create `DashboardConnectionItem` model + `DashboardData` DTO | — |
| 10 | Create `DashboardViewModel` with stats + collections + Refresh logic | Steps 8, 9 |
| 11 | Create `DashboardView.xaml` (stats bar + recent list + all connections table + loading/empty states) | Step 10 |
| 12 | Add `DashboardView` to `MainWindow.xaml` content area, remove empty-state TextBlock | Step 11 |
| 13 | Wire `IsDashboardVisible` logic in `MainWindowViewModel` (tree selection, tab changes) | Step 12 |
| 14 | Update `OpenConnectionAsync` to call `UpdateUsageAsync` (fire-and-forget with error logging) | Steps 3, 4 |
| 15 | Wire dashboard refresh triggers (monitor, tabs, tree changes) + build DashboardData DTO | Steps 10, 13, 14 |
| 16 | Update `PropertiesViewModel` to show last connected + count | Step 1 |
| 17 | Update Import/Export for new fields | Step 1 |
| 18 | Build & verify | All |

---

## 11. References
- FEAT-001: Data Model & Persistence
- FEAT-005: Tree View — CRUD & Organization
- FEAT-011: RDP Session Management — tab lifecycle
- FEAT-107: Connection History & Recents — partially subsumed (simple tracking vs full history table)
- FEAT-111: SSH Terminal Connections — SSH connections shown on dashboard
