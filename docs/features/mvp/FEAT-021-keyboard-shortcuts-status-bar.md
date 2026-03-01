# FEAT-021: Keyboard Shortcuts & Status Bar

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-021 |
| **Title** | Keyboard Shortcuts & Status Bar |
| **Category** | MVP |
| **Priority** | P2 (Medium) |
| **PRD Sections** | §5.7, §5.8 |
| **Depends On** | FEAT-005 (tree CRUD), FEAT-011 (tab management) |
| **Dependents** | — |
| **Estimated Complexity** | S (1-2 days) |

---

## 1. Overview

### 1.1 Purpose
Adds keyboard shortcuts for common actions and a status bar showing entry count and open connection count.

### 1.2 Scope
**In Scope:**
- Keyboard shortcuts via Window.InputBindings
- Status bar with dynamic text (entry count, connection count)

**Out of Scope:**
- Customizable keyboard shortcuts
- Status bar icons/indicators

---

## 2. User Stories & Acceptance Criteria

### US-021-01: Keyboard Navigation
**As a** power user
**I want to** use keyboard shortcuts
**So that** I can work efficiently without the mouse

**Acceptance Criteria:**
- [ ] AC1: Ctrl+N creates new connection
- [ ] AC2: Ctrl+Shift+N creates new folder
- [ ] AC3: F2 renames selected entry
- [ ] AC4: Delete removes selected entry
- [ ] AC5: Ctrl+W closes active tab

### US-021-02: Status Bar
**As a** user
**I want to** see a status bar
**So that** I know how many entries and connections I have

**Acceptance Criteria:**
- [ ] AC1: Shows "No entries" when tree is empty
- [ ] AC2: Shows "X entries" when tree has items
- [ ] AC3: Shows "X entries | Y connection(s) open" when tabs open
- [ ] AC4: Updates in real-time on add/delete/connect/disconnect

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-021-01 | Ctrl+N → AddConnectionCommand | Must | §5.7 |
| FR-021-02 | Ctrl+Shift+N → AddFolderCommand | Must | §5.7 |
| FR-021-03 | F2 → RenameSelectedCommand | Must | §5.7 |
| FR-021-04 | Delete → DeleteSelectedCommand | Must | §5.7 |
| FR-021-05 | Ctrl+W → CloseActiveTabCommand | Must | §5.7 |
| FR-021-06 | Status bar with entry/connection counts | Must | §5.8 |

---

## 4. Implementation Notes
- Shortcuts defined in MainWindow.xaml as `<KeyBinding>` elements
- Status text computed in `MainWindowViewModel.UpdateStatus()`
- MaterialDesign ColorZone for status bar styling

---

## 5. Enhancements (2026-03-01)

### Toolbar Redesign
The toolbar was redesigned with labeled buttons using `MaterialDesignToolForegroundButton` style (auto-sizes to content). Layout:
- **Left**: New Folder, New Connection (elevated `MaterialDesignFlatMidBgButton`), separator, Import, Export, separator, Quick Connect field + Connect button
- **Right**: Open Log Folder, Theme, About

### About Dialog
Added `AboutDialog` modal window showing app version, "Made with heart in Switzerland", Solvia AG contact details (email, phone, website links).

### Window State Persistence
Window position, size, state (Normal/Maximized), tree panel width, and properties panel width are saved to the AppSettings DB table on close and restored on startup. Includes screen boundary validation to handle disconnected monitors.

### Compact Tab Headers
Tab header height reduced to 28px with smaller font size and close button to maximize RDP viewport space.

## 6. References
- §5.7: Keyboard Shortcuts — Key table
- §5.8: Status Bar — Format specification
