# FEAT-015: Detachable Tabs (Dragablz)

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-015 |
| **Title** | Detachable Tabs (Dragablz) |
| **Category** | MVP |
| **Priority** | P1 (High) |
| **PRD Sections** | §5.4 |
| **Depends On** | FEAT-011 (RDP session management) |
| **Dependents** | — |
| **Estimated Complexity** | M (3-5 days) |

---

## 1. Overview

### 1.1 Purpose
Replaces the standard WPF TabControl with Dragablz TabablzControl, enabling Chrome-style tab tearing (drag a tab out to create a new window) and merging (drag back to combine). This supports multi-monitor workflows where users want RDP sessions in separate windows.

### 1.2 Scope
**In Scope:**
- TabablzControl with MaterialDesign styling
- JustRdpInterTabClient (IInterTabClient) implementation
- TabHostWindow for torn-out tabs
- Tab close via built-in close button and Ctrl+W
- ClosingItemCallback for clean RDP disconnect on close
- Secondary window closes when last tab removed
- Main window never closes from tab emptying

**Out of Scope:**
- Tab persistence across restarts (tabs are ephemeral)
- Custom tab ordering persistence

### 1.3 Key Decisions
- **Dragablz over custom solution**: Proven library with MaterialDesign integration
- **Shared InterTabClient**: Static instance shared between all windows
- **Partition "RdpTabs"**: Groups all RDP tabs for inter-window drag

---

## 2. User Stories & Acceptance Criteria

### US-015-01: Tear Out Tab
**As a** user
**I want to** drag a tab out of the window
**So that** I can view RDP sessions on separate monitors

**Acceptance Criteria:**
- [ ] AC1: Dragging a tab out creates a new window
- [ ] AC2: New window has same theme and styling
- [ ] AC3: RDP session continues without disconnection

### US-015-02: Merge Tabs
**As a** user
**I want to** drag a tab from one window back to another
**So that** I can consolidate sessions

**Acceptance Criteria:**
- [ ] AC1: Tab can be dragged between windows
- [ ] AC2: Source window closes if last tab removed (secondary windows only)
- [ ] AC3: Main window stays open even if all tabs removed

### US-015-03: Close Tab
**As a** user
**I want to** close individual tabs
**So that** I can end specific RDP sessions

**Acceptance Criteria:**
- [ ] AC1: Close button on each tab
- [ ] AC2: Ctrl+W closes active tab
- [ ] AC3: RDP session properly disconnected on close

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-015-01 | TabablzControl replaces standard TabControl | Must | §5.4.1 |
| FR-015-02 | MaterialDesignDragableTabControl style | Must | §5.4.1 |
| FR-015-03 | IInterTabClient creates TabHostWindow on tear-out | Must | §5.4.2 |
| FR-015-04 | TabEmptiedHandler closes secondary windows | Must | §5.4.2 |
| FR-015-05 | Main window never closed by tab emptying | Must | §5.4.2 |
| FR-015-06 | ShowDefaultCloseButton on tabs | Must | §5.4.3 |
| FR-015-07 | ClosingItemCallback disconnects RDP | Must | §5.4.3 |

---

## 4. Implementation Notes

### 4.1 Key Files
- `Views/JustRdpInterTabClient.cs` — IInterTabClient implementation
- `Views/TabHostWindow.xaml(.cs)` — Secondary window
- `Views/MainWindow.xaml` — TabablzControl in main window

### 4.2 Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| Dragablz | 0.0.3.234 | Detachable tab control |

---

## 5. Testing

### 5.1 Manual Tests

| Test ID | Case | Expected |
|---------|------|----------|
| MT-015-01 | Drag tab out of main window | New TabHostWindow created |
| MT-015-02 | Drag last tab out of secondary window | Secondary window closes |
| MT-015-03 | Drag tab back to main window | Tab merges, source window closes if empty |
| MT-015-04 | Close tab via X button | RDP disconnected, tab removed |
| MT-015-05 | Close all tabs in main window | Main window stays open |

---

## 6. References
- §5.4: Detachable Tabs — Full specification
- FEAT-011: RDP sessions that live inside tabs
