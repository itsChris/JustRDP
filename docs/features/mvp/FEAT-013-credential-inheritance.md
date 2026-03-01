# FEAT-013: Credential Inheritance

## Metadata

| Attribute | Value |
|-----------|-------|
| **Feature ID** | FEAT-013 |
| **Title** | Credential Inheritance |
| **Category** | MVP |
| **Priority** | P1 (High) |
| **PRD Sections** | §5.3.4 |
| **Depends On** | FEAT-003 (encryption), FEAT-005 (tree structure) |
| **Dependents** | — |
| **Estimated Complexity** | M (3-5 days) |

---

## 1. Overview

### 1.1 Purpose
Implements the credential inheritance algorithm that allows connections to inherit credentials from their parent folder hierarchy. When a connection has no credentials of its own, the system walks up the folder tree to find the nearest ancestor with credentials set.

### 1.2 Scope
**In Scope:**
- CredentialInheritanceService with ResolveCredentialAsync method
- Walk ancestor chain (parent → grandparent → root)
- First ancestor with non-null CredentialUsername wins
- Return Credential value object with InheritedFromName
- Password decryption via ICredentialEncryptor
- Display inheritance source in Properties panel

**Out of Scope:**
- Setting credentials on folders (FEAT-009)
- Using resolved credentials for RDP connection (FEAT-011 consumes)

---

## 2. User Stories & Acceptance Criteria

### US-013-01: Inherit Folder Credentials
**As a** user
**I want to** set credentials on a folder and have child connections inherit them
**So that** I don't need to enter credentials for every connection

**Acceptance Criteria:**
- [ ] AC1: Connection with own credentials uses its own
- [ ] AC2: Connection without credentials inherits from parent folder
- [ ] AC3: Connection without credentials and parent without credentials inherits from grandparent
- [ ] AC4: Properties panel shows "inherited from: <folder name>"
- [ ] AC5: Inherited credentials used when opening RDP connection

---

## 3. Functional Requirements

| Req ID | Requirement | Priority | PRD Ref |
|--------|-------------|----------|---------|
| FR-013-01 | Check connection's own credentials first | Must | §5.3.4 |
| FR-013-02 | Walk ancestor folders (parent → root) | Must | §5.3.4 |
| FR-013-03 | First ancestor with CredentialUsername wins | Must | §5.3.4 |
| FR-013-04 | Set InheritedFromName on returned Credential | Must | §5.3.4 |
| FR-013-05 | Decrypt ancestor password via ICredentialEncryptor | Must | §5.3.4 |
| FR-013-06 | Return empty Credential if no credentials found | Must | §5.3.4 |

---

## 4. Algorithm

```
ResolveCredentialAsync(connection):
  1. IF connection.CredentialUsername is not empty:
       - Decrypt connection.CredentialPasswordEncrypted
       - RETURN Credential(username, domain, password, inheritedFrom=null)

  2. ancestors = GetAncestorsAsync(connection.Id)  // parent, grandparent, ...
     FOR EACH ancestor IN ancestors:
       IF ancestor.CredentialUsername is not empty:
         - Decrypt ancestor.CredentialPasswordEncrypted
         - RETURN Credential(username, domain, password, inheritedFrom=ancestor.Name)

  3. RETURN Credential(null, null, null)  // No credentials found
```

---

## 5. Testing

### 5.1 Unit Tests

| Test ID | Case | Expected |
|---------|------|----------|
| UT-013-01 | Connection has own credentials | Returns own, IsInherited=false |
| UT-013-02 | Connection inherits from parent | Returns parent's, InheritedFromName=parent |
| UT-013-03 | Connection inherits from grandparent | Skips parent, returns grandparent's |
| UT-013-04 | No credentials anywhere | Returns empty Credential |
| UT-013-05 | Multiple ancestors with credentials | Returns nearest (parent wins over grandparent) |

---

## 6. Implementation Notes

### 6.1 Key File
`src/JustRDP.Application/Services/CredentialInheritanceService.cs`

---

## 7. References
- §5.3.4: Credential Inheritance — Algorithm specification
- FEAT-003: ICredentialEncryptor used for decryption
- FEAT-005: Tree structure for ancestor traversal
- FEAT-009: Properties panel displays inheritance info
- FEAT-011: Consumes resolved credentials for RDP connection
