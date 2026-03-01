# FEAT-101: RD Gateway Full Support

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-101 |
| **Title** | RD Gateway Full Support |
| **Category** | Post-MVP |
| **Priority** | P2 (Medium) |
| **PRD Sections** | §9.2 |
| **Depends On** | FEAT-011 (RDP session management) |
| **Dependents** | — |
| **Estimated Complexity** | M (3-5 days) |

---

## 1. Overview
Complete RD Gateway integration including gateway credentials, authentication methods, and credential source selection. Schema is already in place (GatewayHostName, GatewayUsername, GatewayDomain, GatewayPasswordEncrypted); this feature adds the UI and full configuration flow.

## 2. Scope
- Gateway credentials editing in Properties Dialog
- Gateway usage method selection (Never, Always, Detect)
- Gateway credential source configuration
- Gateway credentials encryption/inheritance
