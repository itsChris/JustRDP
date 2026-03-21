# FEAT-107: Connection History & Recents

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-107 |
| **Title** | Connection History & Recents |
| **Category** | Post-MVP |
| **Priority** | P3 (Low) |
| **PRD Sections** | §9.2 |
| **Depends On** | FEAT-011 (RDP sessions) |
| **Dependents** | — |
| **Estimated Complexity** | M (3-5 days) |
| **Note** | Core tracking (LastConnectedAt, ConnectCount) subsumed by FEAT-113 (Dashboard). This feature remains for full session history logging (ConnectionHistory table with connect/disconnect times, durations). |

---

## 1. Overview
Tracks connection history (when a connection was last opened, how many times) and provides a "Recent Connections" section for quick access. New database table for connection events.

## 2. Scope
- ConnectionHistory table (ConnectionId, ConnectedAt, DisconnectedAt, Duration)
- "Recent Connections" panel or toolbar dropdown
- Last connected timestamp on connection properties
- Connection count badge
- History view with sortable columns
