# FEAT-109: Network Scan (IP Range Discovery)

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-109 |
| **Title** | Network Scan (IP Range Discovery) |
| **Category** | Post-MVP |
| **Priority** | P2 (Medium) |
| **PRD Sections** | — (new feature) |
| **Depends On** | FEAT-005 (tree CRUD), FEAT-011 (RDP sessions) |
| **Dependents** | — |
| **Estimated Complexity** | L (1-2 weeks) |

---

## 1. Overview

### 1.1 Purpose
Allows users to scan an IP range (CIDR notation) for hosts with open RDP ports. Discovered hosts can be selectively imported as connection entries into the tree. This enables quick onboarding of servers without manually creating each connection.

### 1.2 Scope
**In Scope:**
- Toolbar "Scan" button opening a non-modal scan window (single instance)
- CIDR input with live range translation (human-readable)
- CIDR range validation with upper bound (/16 max, confirmation for >/20)
- TCP port scanning (default 3389 + user-defined additional ports with validation)
- Configurable timeout per port (default 1500ms)
- Concurrent scanning (16 hosts in parallel)
- Reverse DNS lookup for hostname resolution (silent failure)
- Results list with existing-host detection (green + "Exists" label, blue + "New" label)
- Selective import into user-chosen folder with duplicate prevention
- Progress bar with cancellation support
- Security disclaimer before scanning
- Batched UI updates for performance

**Out of Scope:**
- UDP port scanning
- OS fingerprinting or service detection
- Scheduled/recurring scans
- Scan profiles/templates
- ICMP ping (may be blocked by firewalls; TCP-only approach)

### 1.3 Key Decisions
- **TCP connect only (no ICMP)**: Many servers block ping but have RDP open. TCP connect is more reliable for RDP discovery.
- **Single scan window instance**: Only one `NetworkScanWindow` can be open at a time. Prevents duplicate socket pressure and race conditions on import.
- **DNS failures are expected**: Most IPs won't have PTR records. `Dns.GetHostEntryAsync` failures are handled silently (no error logging), falling back to IP as display name.
- **Imported entries have no credentials**: Credentials are inherited from the parent folder per existing credential inheritance (FEAT-013).

### 1.4 Assumptions
> [!NOTE]
> **Implementation Assumptions:**
> - [ASSUMPTION] Outgoing TCP connections on port 3389 are not blocked by the scanning machine's firewall
> - [ASSUMPTION] Corporate firewalls/IDS may flag port scans — user is responsible for authorization
> - [ASSUMPTION] Reverse DNS is available for at least some hosts on the network

---

## 2. User Stories & Acceptance Criteria

### US-109-01: Scan IP Range
**As a** sysadmin
**I want to** scan a CIDR range for RDP hosts
**So that** I can discover servers on my network without adding them one by one

**Acceptance Criteria:**
- [ ] AC1: Toolbar "Scan" button opens a non-modal window (single instance — clicking again focuses existing window)
- [ ] AC2: User enters CIDR notation (e.g. `192.168.1.0/24`)
- [ ] AC3: Live translation shows range and host count (e.g. "192.168.1.1 - 192.168.1.254 (254 hosts)")
- [ ] AC4: Invalid CIDR shows inline validation error (e.g. "Invalid CIDR notation")
- [ ] AC5: Ranges larger than /16 (65,534 hosts) are rejected with error message
- [ ] AC6: Ranges larger than /20 (4,094 hosts) show confirmation dialog before scanning
- [ ] AC7: Scan checks TCP connectivity on specified ports (no ICMP ping)
- [ ] AC8: Determinate progress bar shows "X / N hosts scanned" with Cancel button
- [ ] AC9: 16 hosts scanned concurrently via `SemaphoreSlim`
- [ ] AC10: Closing the scan window cancels any running scan and disposes all connections
- [ ] AC11: Security disclaimer shown above Start Scan button: "Network scanning may trigger security alerts. Ensure you have authorization."

### US-109-02: View Scan Results
**As a** user
**I want to** see discovered hosts with their details
**So that** I can decide which to import

**Acceptance Criteria:**
- [ ] AC1: Results displayed in a DataGrid below the parameters section
- [ ] AC2: Columns: Checkbox | IP Address | Hostname | Open Ports | Port (dropdown) | Status
- [ ] AC3: Hostname resolved via reverse DNS; falls back to IP if DNS fails (no error shown)
- [ ] AC4: Hosts already in the database show green background + "Exists" text label
- [ ] AC5: New (unknown) hosts show blue background + "New" text label
- [ ] AC6: Status uses both color AND text label (accessibility — not color-only)
- [ ] AC7: Results persist in the window until a new scan is started or window is closed
- [ ] AC8: DataGrid columns are sortable (click header to sort)
- [ ] AC9: "Select All New" button selects all blue/new hosts with one click
- [ ] AC10: Before first scan, results area shows placeholder: "Run a scan to discover hosts"
- [ ] AC11: After scan with 0 results, shows: "No hosts found. The range may be unreachable or ports may be blocked."

### US-109-03: Import Discovered Hosts
**As a** user
**I want to** import selected hosts into my connection tree
**So that** I can connect to them via RDP

**Acceptance Criteria:**
- [ ] AC1: Checkboxes on each result row for selection
- [ ] AC2: "Import Selected (N)" button creates connection entries (count shown on button)
- [ ] AC3: Folder dropdown lets user choose target folder (or root) — positioned near Import button
- [ ] AC4: Each result row with multiple open ports shows a port dropdown (default: 3389 if open, else lowest open port)
- [ ] AC5: Display name = hostname if resolved, otherwise IP address
- [ ] AC6: Imported entries appear immediately in the tree
- [ ] AC7: Hosts already in DB (green rows) have checkboxes disabled — cannot be imported again
- [ ] AC8: Imported entries are created with no credentials (inherit from parent folder)
- [ ] AC9: After import, newly imported hosts are re-detected as "Exists" (green) in the results list

---

## 3. Functional Requirements

| Req ID | Requirement | Priority |
|--------|-------------|----------|
| FR-109-01 | Toolbar "Scan" button opens non-modal `NetworkScanWindow` (singleton) | Must |
| FR-109-02 | CIDR input field with live range translation | Must |
| FR-109-03 | CIDR validation: reject invalid notation, reject ranges > /16, confirm ranges > /20 | Must |
| FR-109-04 | Default port field (3389) + additional ports field (semicolon-separated, validated 1-65535, deduped) | Must |
| FR-109-05 | Configurable timeout per port (default 1500ms, range 200-10000ms) | Must |
| FR-109-06 | TCP connect scan — no ICMP (avoids blocked ping) | Must |
| FR-109-07 | 16-host concurrency via `SemaphoreSlim` | Must |
| FR-109-08 | Reverse DNS lookup (`Dns.GetHostEntryAsync`) — silent failure, fallback to IP | Must |
| FR-109-09 | Determinate progress bar (X/N) with Cancel via `CancellationTokenSource` | Must |
| FR-109-10 | Results DataGrid: checkbox, IP, hostname, open ports, port dropdown, status | Must |
| FR-109-11 | Existing-host detection: case-insensitive match against `ConnectionEntry.HostName` (IP, short hostname, FQDN) | Must |
| FR-109-12 | Green + "Exists" label = in DB, Blue + "New" label = not in DB (color + text for accessibility) | Must |
| FR-109-13 | "Import Selected" creates `ConnectionEntry` per selected host in chosen folder via `TreeService` | Must |
| FR-109-14 | Duplicate prevention: disable checkboxes on green/existing rows | Must |
| FR-109-15 | "Select All New" button to check all blue/new rows | Must |
| FR-109-16 | Sortable DataGrid columns (IP, hostname, ports, status) | Should |
| FR-109-17 | Target folder dropdown near Import button (bottom of window) | Must |
| FR-109-18 | Security disclaimer text in scan parameters section | Must |
| FR-109-19 | Batched UI updates (buffer results, flush every 250ms) | Must |
| FR-109-20 | Window close cancels running scan, disposes all `TcpClient` instances | Must |
| FR-109-21 | Port validation: integers 1-65535, semicolon-separated, duplicates silently removed | Must |
| FR-109-22 | Empty/zero-results states with instructional messages | Should |

---

## 4. UI Layout

### 4.1 Scan Window (Non-Modal, Single Instance)

```
┌─────────────────────────────────────────────────────────────┐
│  Network Scan                                          [X]  │
├─────────────────────────────────────────────────────────────┤
│  Scan Parameters                                            │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ CIDR Range:    [ 192.168.1.0/24                   ]    │ │
│  │                 → 192.168.1.1 - 192.168.1.254          │ │
│  │                   (254 hosts)                          │ │
│  │                                                        │ │
│  │ Port:          [ 3389    ]   Timeout: [ 1500 ] ms      │ │
│  │ Additional:    [ 3390;5985;22                     ]    │ │
│  │                                                        │ │
│  │ ⚠ Network scanning may trigger security alerts.        │ │
│  │   Ensure you have authorization before scanning.       │ │
│  │                                                        │ │
│  │         [ ▶ Start Scan ]        [ ■ Cancel ]           │ │
│  │ ████████████████████░░░░░░░░░░  142 / 254 hosts        │ │
│  └────────────────────────────────────────────────────────┘ │
│                                                             │
│  Results (12 hosts found)           [ Select All New ]      │
│  ┌──┬────────────────┬──────────────┬───────┬──────┬──────┐ │
│  │☑ │ IP Address     │ Hostname     │ Ports │ Port │Status│ │
│  ├──┼────────────────┼──────────────┼───────┼──────┼──────┤ │
│  │░░│ 192.168.1.10   │ web-srv-01   │ 3389  │      │🟢 DB │ │
│  │☑ │ 192.168.1.22   │ db-srv-01    │ 3389  │ 3389 │🔵New │ │
│  │☐ │ 192.168.1.55   │ 192.168.1.55 │ 3389, │[▼3389│🔵New │ │
│  │  │                │              │ 5985  │      │      │ │
│  │░░│ 192.168.1.100  │ jump-host    │ 3389, │      │🟢 DB │ │
│  │  │                │              │ 5985  │      │      │ │
│  └──┴────────────────┴──────────────┴───────┴──────┴──────┘ │
│  ░░ = checkbox disabled (host already exists in DB)         │
│                                                             │
│  Import to: [ ▼ (Root)              ]  [Import Selected (1)]│
└─────────────────────────────────────────────────────────────┘
```

### 4.2 UI States

| State | Display |
|-------|---------|
| Before first scan | Results area: "Run a scan to discover hosts on your network" |
| Scan in progress | Progress bar active, Cancel button enabled, Start Scan disabled |
| Scan complete, results found | DataGrid populated, "X hosts found" header |
| Scan complete, no results | "No hosts found. The range may be unreachable or ports may be blocked." |
| Scan cancelled | Partial results shown, "Scan cancelled. X hosts found (Y / Z scanned)" |

### 4.3 Status Indicators (Color + Text)

| Status | Background | Text Label | Checkbox | Meaning |
|--------|------------|------------|----------|---------|
| Exists in DB | Green (`#4CAF50`) | "Exists" | Disabled | Host already has a connection entry |
| New host | Blue (`#2196F3`) | "New" | Enabled | Host not yet in the database |

> **Accessibility note:** Status is always conveyed by both color AND text label, never color alone.

---

## 5. Technical Design

### 5.1 Architecture

```
NetworkScanWindow (View — Presentation)
    └─ NetworkScanViewModel (ViewModel — Presentation)
            ├─ INetworkScanner (Interface — Application)
            │      └─ NetworkScanner (Implementation — Infrastructure)
            │             ├─ TCP port scanning (TcpClient.ConnectAsync)
            │             └─ Reverse DNS (Dns.GetHostEntryAsync)
            ├─ CidrParser (Pure utility — Application)
            │      ├─ Parse CIDR notation
            │      ├─ Validate range bounds
            │      └─ Enumerate host IPs
            ├─ TreeService (check existing hosts, create entries on import)
            └─ ITreeEntryRepository (query existing hostnames)
```

### 5.2 Key Classes

| Class | Layer | Project | Responsibility |
|-------|-------|---------|----------------|
| `NetworkScanWindow` | Presentation | JustRDP.Presentation | Non-modal WPF window (single instance) |
| `NetworkScanViewModel` | Presentation | JustRDP.Presentation | Scan orchestration, results, import logic |
| `INetworkScanner` | Application | JustRDP.Application | Interface for port scanning + DNS resolution |
| `NetworkScanner` | Infrastructure | JustRDP.Infrastructure | TCP connect scanning + DNS implementation |
| `CidrParser` | Application | JustRDP.Application | Pure CIDR parsing utility (static, no I/O) |
| `ScanResult` | Application | JustRDP.Application/DTOs | DTO: IP, hostname, open ports, exists-in-DB flag |

### 5.3 Scanning Algorithm

```
1. Validate input:
   a. Parse CIDR via CidrParser → list of host IPs
   b. Reject if prefix > /16 (error) or > /20 (confirmation dialog)
   c. Validate ports: integers 1-65535, deduplicate
2. Show security disclaimer (already static in UI)
3. Load existing hostnames from DB (case-insensitive set of HostName values)
4. For each IP (16 concurrent via SemaphoreSlim, CancellationToken):
   a. For each port (3389 + additional):
      - using var tcp = new TcpClient();
      - tcp.ConnectAsync(ip, port, linkedToken) with timeout
      - If connected → add port to open list; dispose immediately
   b. If any port open:
      - try { Dns.GetHostEntryAsync(ip) } catch { /* silent — use IP */ }
      - Determine display name: hostname if resolved, else IP
      - Check existing-host set (match: IP, short hostname, FQDN — case-insensitive)
      - Create ScanResult, add to batch buffer
   c. Increment progress counter
5. Flush buffered results to UI every 250ms via dispatcher timer
6. On completion: update header text, enable Import button
```

### 5.4 Concurrency & Resource Management

- **`SemaphoreSlim(16)`** limits concurrent host scans
- **`CancellationTokenSource`** for user cancellation — linked with per-port timeout tokens via `CancellationTokenSource.CreateLinkedTokenSource`
- **`TcpClient` disposal**: Every `TcpClient` wrapped in `using` statement. Disposed immediately after connect attempt (success or failure). No lingering sockets.
- **Batched UI updates**: Results buffered in a `ConcurrentQueue<ScanResult>`. A `DispatcherTimer` (250ms interval) drains the queue and adds to `ObservableCollection<ScanResult>` on the UI thread. This prevents UI thread congestion on large scans.
- **Window close**: `Window.Closing` event cancels the `CancellationTokenSource`, awaits scan task completion (with short timeout), then disposes all resources.
- **DNS inside semaphore**: DNS resolution happens within the semaphore-protected block, so it does not create unbounded concurrent DNS requests.

### 5.5 Existing Host Detection

Query all `ConnectionEntry.HostName` values from DB into a `HashSet<string>` (case-insensitive via `StringComparer.OrdinalIgnoreCase`).

For each discovered host, check membership against:
1. **Exact IP match** — e.g. "192.168.1.10"
2. **Resolved hostname match** — e.g. "web-srv-01"
3. **FQDN-to-short-name match** — if DNS returns "web-srv-01.domain.local", also check "web-srv-01" (strip after first dot)

A match on any of these marks the result as "Exists."

### 5.6 Port Validation Rules

- Ports must be integers in range 1-65535
- Semicolon-separated in the additional ports field
- Whitespace around values is trimmed
- Duplicate ports (including default port) are silently removed
- Invalid values show inline validation error: "Invalid port: {value}. Ports must be 1-65535."

### 5.7 CIDR Parsing Rules

- Format: `{IP}/{prefix}` (e.g. `192.168.1.0/24`)
- Prefix length: 16-32
- Network address and broadcast address excluded for /31 and smaller (standard convention)
- `/32` returns exactly 1 IP (the address itself)
- `/31` returns exactly 2 IPs (point-to-point link, no broadcast)
- Invalid format, invalid octets (>255), or prefix out of range → validation error

### 5.8 TreeService Extension

`TreeService.CreateConnectionAsync` currently accepts `(string name, string hostName, Guid? parentId)` and always creates connections with default port 3389. Add an optional `port` parameter:

```csharp
public async Task<ConnectionEntry> CreateConnectionAsync(
    string name, string hostName, Guid? parentId = null, int port = 3389)
```

This allows the scan import to create entries with the user-selected port without bypassing the service layer.

### 5.9 DI Registration

Register in `App.xaml.cs`:
```csharp
services.AddTransient<INetworkScanner, NetworkScanner>();
services.AddTransient<NetworkScanViewModel>();
services.AddTransient<NetworkScanWindow>();
```

`MainWindowViewModel.ShowScanCommand` resolves `NetworkScanWindow` from the service provider (same pattern as other windows). Single-instance enforcement: store reference, check `IsLoaded` before creating new.

---

## 6. Testing

### 6.1 Unit Tests (CidrParser)

| Test ID | Case | Expected |
|---------|------|----------|
| UT-109-01 | Parse `10.0.0.0/24` | Returns 254 IPs (10.0.0.1 - 10.0.0.254) |
| UT-109-02 | Parse `10.0.0.5/32` | Returns 1 IP (10.0.0.5) |
| UT-109-03 | Parse `10.0.0.0/31` | Returns 2 IPs (10.0.0.0, 10.0.0.1) |
| UT-109-04 | Parse `999.0.0.0/24` | Returns error (invalid IP) |
| UT-109-05 | Parse `10.0.0.0/8` | Returns error (prefix too large, > /16 limit) |
| UT-109-06 | Parse `10.0.0.0/15` | Returns error (prefix too large, > /16 limit) |
| UT-109-07 | Parse `10.0.0.0/16` | Returns 65,534 IPs (accepted, at limit) |
| UT-109-08 | Parse empty string | Returns error |
| UT-109-09 | Parse `10.0.0.0` (no prefix) | Returns error (missing prefix) |
| UT-109-10 | Range translation for /24 | "10.0.0.1 - 10.0.0.254 (254 hosts)" |
| UT-109-11 | Range translation for /30 | "10.0.0.1 - 10.0.0.2 (2 hosts)" |

### 6.2 Unit Tests (Port Validation)

| Test ID | Case | Expected |
|---------|------|----------|
| UT-109-12 | Parse `3390;5985;22` | Returns [3390, 5985, 22] |
| UT-109-13 | Parse empty string | Returns empty list |
| UT-109-14 | Parse `0;70000;-1` | Returns error for each invalid port |
| UT-109-15 | Parse `3389;3389;3390` | Returns [3389, 3390] (deduped) |
| UT-109-16 | Parse `3390 ; 5985` | Returns [3390, 5985] (whitespace trimmed) |
| UT-109-17 | Parse `abc;3389` | Returns error for "abc" |

### 6.3 Unit Tests (Existing Host Detection)

| Test ID | Case | Expected |
|---------|------|----------|
| UT-109-18 | DB has "192.168.1.10", scan finds 192.168.1.10 | ExistsInDatabase = true |
| UT-109-19 | DB has "web-srv-01", DNS resolves to "web-srv-01" | ExistsInDatabase = true |
| UT-109-20 | DB has "WEB-SRV-01", DNS resolves to "web-srv-01" | ExistsInDatabase = true (case-insensitive) |
| UT-109-21 | DB has "web-srv-01", DNS resolves to "web-srv-01.domain.local" | ExistsInDatabase = true (FQDN→short match) |
| UT-109-22 | DB has "other-host", scan finds 192.168.1.99 → "unknown" | ExistsInDatabase = false |

### 6.4 Unit Tests (ViewModel)

| Test ID | Case | Expected |
|---------|------|----------|
| UT-109-23 | Import skips disabled (green) rows | Only blue/new rows imported |
| UT-109-24 | After import, row status changes to green/Exists | Re-detection works |
| UT-109-25 | Import uses selected port from dropdown | ConnectionEntry.Port matches |

### 6.5 Integration Tests

| Test ID | Case | Expected |
|---------|------|----------|
| IT-109-01 | Scan localhost on a known open port | Returns 1 result with port open |
| IT-109-02 | Scan unreachable range | Returns 0 results, shows empty message |
| IT-109-03 | Import selected results | ConnectionEntry created in DB with correct port |
| IT-109-04 | Cancel mid-scan | Scan stops, partial results shown, all TcpClients disposed |
| IT-109-05 | Close window during active scan | Scan cancelled, resources cleaned up, no exceptions |
| IT-109-06 | Scan range > /20 | Confirmation dialog shown before scan starts |

---

## 7. Implementation Notes

### 7.1 Key Files (Planned)

| File | Project | Purpose |
|------|---------|---------|
| `Application/Services/CidrParser.cs` | JustRDP.Application | Static CIDR parsing + IP enumeration |
| `Application/Interfaces/INetworkScanner.cs` | JustRDP.Application | Scanner interface (testable) |
| `Application/DTOs/ScanResult.cs` | JustRDP.Application | Result DTO |
| `Infrastructure/Services/NetworkScanner.cs` | JustRDP.Infrastructure | TCP scan + DNS implementation |
| `Presentation/Views/NetworkScanWindow.xaml(.cs)` | JustRDP.Presentation | Non-modal scan window |
| `Presentation/ViewModels/NetworkScanViewModel.cs` | JustRDP.Presentation | Scan orchestration and import |

### 7.2 Dependencies
- No additional NuGet packages required
- `System.Net.Sockets.TcpClient` for TCP port scanning
- `System.Net.Dns` for reverse DNS
- `System.Collections.Concurrent.ConcurrentQueue<T>` for result buffering

### 7.3 CIDR Parsing (CidrParser — pure, static, no I/O)
- Split on `/` → IP + prefix length
- Validate IP octets (0-255) and prefix length (16-32)
- Calculate network address and broadcast address via bitmask
- Enumerate host addresses between first+1 and last-1 (exclude network/broadcast)
- Special cases: /32 → single IP, /31 → both IPs (point-to-point)

### 7.4 Known Limitations
- **Firewall false negatives**: Egress rules on the scanning machine may block non-standard ports. A host with RDP on port 3390 may appear "closed" if the scanner's firewall blocks outgoing 3390.
- **DNS latency**: Reverse DNS can take 2-5s per host if the DNS server is slow or PTR records don't exist. DNS is performed within the semaphore block to bound concurrency.
- **FQDN mismatch**: If a user stored "web-srv-01.corp.local" and DNS returns "web-srv-01.domain.local", these won't match. Normalization handles common cases but can't resolve all naming inconsistencies.

### 7.5 Logging
Follow existing project pattern (`Serilog.Log`):
- **Information**: Scan started (CIDR, ports, host count), scan completed (results count, duration), import completed (count, folder)
- **Debug**: Per-host scan result, DNS resolution result, existing-host match details
- **Warning**: Only for unexpected errors (not DNS failures — those are expected and silent)
- **Never log**: Individual DNS lookup failures (expected for 80%+ of IPs)

---

## 8. Security Considerations

- **Authorization**: The UI displays a static warning: "Network scanning may trigger security alerts. Ensure you have authorization before scanning." This is always visible in the scan parameters section, not a dismissable dialog.
- **IDS/EDR detection**: TCP connect scanning 254 hosts will be flagged by most intrusion detection systems. Users should be aware this is visible network activity.
- **Audit trail**: All scan activity is logged with timestamps (scan start, completion, cancellation, import) for audit purposes.
- **No credential scanning**: The feature only checks port availability. It does not attempt authentication or credential testing.

---

## 9. References
- FEAT-005: Tree CRUD — for creating imported connection entries
- FEAT-011: RDP sessions — connections created by import use standard RDP flow
- FEAT-013: Credential inheritance — imported entries inherit credentials from parent folder
