# FEAT-019: Theme Management (Dark/Light)

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-019 |
| **Title** | Theme Management (Dark/Light) |
| **Category** | MVP |
| **Priority** | P2 (Medium) |
| **PRD Sections** | §5.6 |
| **Depends On** | FEAT-001 (AppSettings persistence) |
| **Dependents** | — |
| **Estimated Complexity** | S (1-2 days) |

---

## 1. Overview

### 1.1 Purpose
Provides a dark/light theme toggle with persistence. Users can switch themes at runtime via a toolbar button, and their preference is saved to the database for next launch.

### 1.2 Scope
**In Scope:**
- ThemeManager service (load, set, apply)
- MaterialDesign PaletteHelper integration
- AppSettings persistence ("Theme" → "Dark"/"Light")
- Toolbar toggle button
- Default: Dark theme (Blue primary, LightBlue secondary)

**Out of Scope:**
- Custom color schemes (Post-MVP)
- Per-window themes

---

## 2. User Stories & Acceptance Criteria

### US-019-01: Toggle Theme
**As a** user
**I want to** switch between dark and light themes
**So that** I can use the app comfortably in different lighting conditions

**Acceptance Criteria:**
- [ ] AC1: Toolbar button toggles between dark and light
- [ ] AC2: Theme change is immediate (no restart needed)
- [ ] AC3: Preference persisted to database
- [ ] AC4: Theme restored on next launch
- [ ] AC5: Default is dark theme on first launch

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-019-01 | PaletteHelper.SetTheme for runtime switching | Must | §5.6.1 |
| FR-019-02 | Persist to AppSettings ("Theme" key) | Must | §5.6.2 |
| FR-019-03 | Load on startup before showing window | Must | §5.6.2 |
| FR-019-04 | Default to Dark if no setting exists | Must | §5.6.1 |

---

## 4. Implementation Notes

### 4.1 Key File
`Themes/ThemeManager.cs`

---

## 5. References
- §5.6: Theme Management — Full specification
