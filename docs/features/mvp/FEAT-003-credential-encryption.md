# FEAT-003: Credential Encryption (DPAPI)

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-003 |
| **Title** | Credential Encryption (DPAPI) |
| **Category** | MVP |
| **Priority** | P0 (Critical) |
| **PRD Sections** | §5.3.4, §7 |
| **Depends On** | FEAT-001 (domain interfaces) |
| **Dependents** | FEAT-009, FEAT-011, FEAT-013 |
| **Estimated Complexity** | S (1-2 days) |

---

## 1. Overview

### 1.1 Purpose
Implements secure credential storage using Windows DPAPI (Data Protection API). Passwords are encrypted with the current user's Windows credentials, ensuring they can only be decrypted by the same user on the same machine. This eliminates key management entirely.

### 1.2 Scope
**In Scope:**
- `DpapiCredentialEncryptor` implementing `ICredentialEncryptor`
- Encrypt plaintext → byte[] using ProtectedData.Protect
- Decrypt byte[] → plaintext using ProtectedData.Unprotect
- DataProtectionScope.CurrentUser binding

**Out of Scope:**
- Key management (DPAPI handles this)
- Password export (intentionally excluded per §7.2)
- Credential inheritance logic (FEAT-013)

### 1.3 Key Decisions
- **DPAPI over AES**: Zero key management, OS-integrated, no master password needed
- **CurrentUser scope**: Per-user isolation; even admins on the same machine can't decrypt

### 1.4 Assumptions
> [!NOTE]
> **Implementation Assumptions:**
> - [ASSUMPTION] Only Windows is supported; DPAPI is Windows-only
> - [ASSUMPTION] No key rotation mechanism; re-encryption requires manual intervention

---

## 2. User Stories & Acceptance Criteria

### US-003-01: Secure Password Storage
**As a** user
**I want to** store passwords securely
**So that** they cannot be read from the database file

**Acceptance Criteria:**
- [ ] AC1: Passwords stored as encrypted byte[] in SQLite
- [ ] AC2: Raw database file does not contain plaintext passwords
- [ ] AC3: Passwords decryptable only by the same Windows user
- [ ] AC4: Encrypt/decrypt roundtrip preserves original value

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-003-01 | Encrypt string → byte[] using DPAPI CurrentUser scope | Must | §7.1 |
| FR-003-02 | Decrypt byte[] → string using DPAPI CurrentUser scope | Must | §7.1 |
| FR-003-03 | Register as singleton in DI container | Must | §3.3 |
| FR-003-04 | Passwords never exported in JSON/RDP exports | Must | §7.2 |

---

## 4. Implementation Notes

### 4.1 Key File
`src/JustRDP.Infrastructure/Security/DpapiCredentialEncryptor.cs`

### 4.2 Dependencies
| Package | Version | Purpose |
|---------|---------|---------|
| System.Security.Cryptography.ProtectedData | 10.0.* | DPAPI wrapper |

---

## 5. Testing

### 5.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-003-01 | Encrypt then decrypt string | Returns original string |
| UT-003-02 | Encrypt same string twice | Different byte arrays (DPAPI adds entropy) |
| UT-003-03 | Decrypt with corrupted data | Throws CryptographicException |
| UT-003-04 | Encrypt empty string | Returns non-empty byte array |

---

## 6. Security

### 6.1 Data Protection
| Data | Classification | Protection |
|------|----------------|------------|
| Passwords | Secret | DPAPI CurrentUser encryption |
| Encrypted blobs | Sensitive | Stored as BLOB in SQLite |

### 6.2 Audit Requirements
| Action | Logged | Details |
|--------|--------|---------|
| Password encryption | No | No logging of credential operations |
| Password decryption | No | No logging of credential operations |

---

## 7. References
- §7: Security — DPAPI specification
- §5.3.4: Credential Inheritance — Consumer of encryption
