# HuahaiClipboard Data Recovery and Lossless Upgrade Design

## Objective

Recover data for users whose history appeared empty after an upgrade, then prevent any future upgrade from committing when previously readable user data is missing, unreadable, or reduced.

The first deliverable is a standalone recovery executable that does not require reinstalling the application. The second deliverable is a lossless upgrade contract shared by the installer and application. Public release, GitHub push, manifest updates, and deployment remain outside this design until separately authorized.

## Confirmed Problem

The `v1.1.13` installer asks an interactive user to choose an install directory before consulting the registered install location. An empty directory is accepted as a new target. `InstallDataPreserver` then copies only the `Data` directory already under that newly selected target, while the application resolves its active data from the directory containing the running executable.

This creates a confirmed failure path in which the old `Data` directory remains on disk but the new application opens a different, empty data directory. It is a path-identity defect, not proof that every affected user's files were physically deleted.

A second failure mode exists when `history.dat` cannot be parsed or decrypted. The current history source moves it to `history.dat.corrupt*` and presents an empty list. A third failure mode is DPAPI scope: encrypted history and protected images can be decrypted only in the original Windows user context unless that profile's DPAPI material is also recoverable.

## Product And Change Classification

- Product scale: medium existing desktop product; this is not a separate consumer product.
- Change scale: large because recovery, storage identity, installer activation, first-start verification, and release gates change together.
- Risk: high because an incorrect implementation can overwrite or make user data harder to recover.
- Runtime topology: desktop-first, with a standalone WinUI recovery executable and shared .NET core services.
- UI lineage: the later in-app recovery center uses the approved executable shell; the emergency standalone tool may use a focused native window because affected users cannot depend on the installed application opening correctly.

## Non-Negotiable Lossless Invariant

An upgrade may commit only when the post-upgrade readable data is a verified superset of the pre-upgrade readable data.

The comparison uses stable record IDs, todo and note IDs, and attachment hashes. Record counts alone are insufficient. Before the invariant is proven, the new application may not create or replace an empty `history.dat`, `todo-workspace.json`, or settings file over an expected existing dataset.

If snapshot creation, decryption, parsing, migration, attachment copying, first-start verification, or manifest comparison fails, the update must stop or roll back. The previous program, previous data, and independent recovery snapshot remain available.

## Standalone Recovery Experience

The deliverable is `HuahaiClipboard-Recovery.exe`. It runs as the current Windows user and does not request elevation.

The initial screen explains that no clipboard content is uploaded and that scanning is read-only. It then performs bounded discovery in this order:

1. Stable product `DataLocation`, if registered.
2. Uninstall registration and current executable locations.
3. Current and stale Start Menu or desktop shortcut targets.
4. Installer logs and known `HuahaiClipboard\Data` directories on fixed non-system drives.
5. Legacy `%LocalAppData%\HuahaiClipboard` data.
6. Sibling `.HuahaiClipboard-backup-*` directories.
7. `history.dat.corrupt*` files under every discovered data directory.

If bounded discovery finds nothing, the user can start a cancellable deep scan of fixed drives. Network, removable, reparse-point, and inaccessible paths are skipped and reported without failing the whole scan.

Each source row shows its path, modification time, byte size, detected schema, history count, todo count, note count, image count, and one of these states: readable, encrypted for another user, malformed, incomplete, duplicate, or not yet inspected. Clipboard body text is hidden by default. Explicit preview reveals only the selected local source and never sends data over the network.

The primary command is `合并恢复`. Destructive replacement is not offered in the normal flow.

## Recovery Architecture

### RecoverySourceDiscovery

Discovers candidate roots from explicit, registered, shortcut, log, legacy, backup, and optional deep-scan sources. It returns normalized paths with provenance and never writes to them.

### RecoverySourceInspector

Creates a read-only inventory, computes SHA-256 hashes, attempts DPAPI decryption in the current user context, parses known schemas, and records precise failures. It does not quarantine or rename malformed files.

### RecoverySnapshotService

Before mutation, copies both the selected source and current destination to an installation-independent snapshot root. The preferred root is a sibling of the stable data root named `HuahaiClipboard-Recovery`; `%LocalAppData%\HuahaiClipboard\RecoverySnapshots` is used only when the preferred location is unavailable and has sufficient free space.

Every snapshot contains a manifest of relative paths, lengths, timestamps, and SHA-256 hashes. The copied manifest must equal the source manifest before recovery continues. Snapshots are retained for 30 days with a maximum of three verified snapshots per Windows SID; unverified or currently referenced snapshots are never pruned.

### RecoveryMergePlanner

Builds an immutable recovery plan without changing the destination. It preserves unknown compatible JSON fields so recovery does not erase fields introduced by newer schemas.

History records are matched first by ID and then by a semantic key made from kind, primary payload, and source identity. Pinned and favorite states use a union. The newest copy timestamp is retained. If one ID has irreconcilably different payloads, the destination record keeps its ID and the recovered payload is cloned under a new ID so neither value is silently discarded.

Todos are matched by ID. Unique recovered todos are appended after current items and sort positions are normalized. Completion state uses a union. Conflicting text under one ID is retained as a new recovered todo.

Notes are matched by ID. A conflicting recovered note is cloned with a new ID and a `（恢复副本）` title suffix. Note images are copied into the destination todo image store and their persisted references are rewritten.

Clipboard images are decrypted from the source store, written through the destination protected image store, and assigned destination-owned paths. This removes stale absolute paths while preserving DPAPI protection. File payload paths are not copied because they refer to user-owned external files; their availability is recalculated.

Current settings and window positions win when they are valid and non-default. Old settings are restored only when the destination is absent or demonstrably default, and every retained alternative remains in the snapshot and report.

### RecoveryTransaction

Writes the planned result to a candidate data directory, reloads it through production readers, validates the lossless invariant and attachment hashes, and then activates it with an atomic directory swap where the filesystem permits. If activation or post-activation validation fails, the original destination is restored. Candidate and rollback paths are explicit children of the destination parent and are never derived from an unresolved environment variable.

### RecoveryReport

Produces a local JSON and human-readable report containing paths, provenance, counts, hashes, decisions, conflicts, and error codes. It excludes clipboard text, note HTML, image bytes, and external file contents.

## Stable Data Identity

Add a current-user product key independent of the uninstall entry:

`HKCU\Software\HuahaiClipboard\DataLocation`

The value identifies the stable data root containing direct Windows SID children. The application resolves data in this order:

1. Valid registered `DataLocation`.
2. Existing data under the registered install location.
3. Existing data under the running executable location.
4. A verified legacy local-app-data source as a migration candidate, never as a `DataLocation` value.
5. Only when no stable root exists, a new data root for a genuinely new installation.

Resolution that finds multiple non-empty stable roots enters recovery-required state instead of choosing by timestamp. Existing users are registered in place during the hotfix; their data is not moved merely to introduce the new key. The legacy local-app-data directory has a different single-user layout, so it is first snapshotted, copied into the selected stable root's SID child, validated through production readers, and retained until the health receipt verifies the migrated set. It is never written directly to `DataLocation` and is not deleted by the hotfix.

The uninstall entry may be repaired, removed, or recreated without changing `DataLocation`. Normal uninstall preserves data and the product key. Permanent data deletion is a separate explicit operation that displays the exact data path and requires a second confirmation.

## Upgrade Target Rules

An interactive setup with an existing registered installation automatically uses its registered program location and does not show the folder picker. New installations retain the folder picker.

Supplying a different `--install-dir` while an existing installation is registered is rejected by normal upgrade mode. Moving the program uses a separate migration mode that keeps `DataLocation` stable unless the user separately requests and confirms a verified data migration.

Before replacing program files, the installer:

1. Resolves and validates `DataLocation`.
2. Opens existing history, todo, notes, settings, and protected images with production readers.
3. Writes a pre-upgrade manifest containing IDs and attachment hashes, not clipboard bodies.
4. Creates and verifies an independent recovery snapshot.
5. Aborts without touching the active installation if any required step fails.

The existing installation backup is not deleted when shortcuts and uninstall registration succeed. The installer launches the candidate with a one-time upgrade verification token. The application validates production reads and the lossless manifest before writing a health receipt. Until that receipt exists, failure causes program rollback and the recovery snapshot remains retained.

## Storage Write Safety

History, todo workspace, settings, and window-position stores use the same durable write pattern:

1. Serialize to a uniquely named temporary file in the destination directory.
2. Flush file contents to stable storage.
3. Read the temporary file through the production parser and validator.
4. Atomically replace the active file while retaining `bak1`.
5. Rotate the previously verified backup to `bak2` only after successful activation.

On read failure, the application does not rename the only copy or silently substitute an empty dataset. It returns a typed recovery-required result and opens the recovery center. Explicit user confirmation is required before creating a fresh empty dataset.

## Failure Handling

- Insufficient snapshot space: stop before installation and show required and available bytes.
- DPAPI failure: classify the source as belonging to another Windows user; never rewrite it.
- Malformed JSON with decryptable plaintext: preserve the file and expose a repair attempt only from a verified snapshot.
- Image failure: retain the history record, mark the attachment conflict, and block the strict upgrade invariant until the user chooses recovery outside the update flow.
- Registry missing or stale: discover existing sources; multiple non-empty candidates require recovery selection.
- Process or file lock: bounded retry followed by a clean abort with all originals retained.
- Power loss: on next start, inspect transaction markers and select the last fully verified active or rollback directory.
- Disk move or unavailable drive: keep `DataLocation`, report the unavailable path, and do not create substitute empty data elsewhere.

## Verification Matrix

Automated tests must cover upgrades from `v1.1.11`, `v1.1.12`, and `v1.1.13` with:

- Same program directory and a different requested directory.
- Valid, missing, stale, and conflicting registry locations.
- Normal history, `history.dat.corrupt*`, wrong-user DPAPI, malformed JSON, and missing attachments.
- Clipboard text, links, images, external files, favorite, pinned, todos, ordered todos, notes, note images, settings, and window positions.
- Failures before snapshot, during copy, during program swap, before health receipt, and after candidate launch.
- Insufficient disk space, locked files, and simulated interrupted transactions.

Every upgrade test captures the pre-upgrade ID sets and attachment hashes and proves that the post-upgrade readable set is a superset. Fault-injection tests prove rollback leaves the original set readable. Recovery tests prove source and destination remain unchanged until the verified activation step.

The release build is blocked unless focused recovery tests, core storage tests, installer policy tests, fault-injection tests, a Visual Studio x64 Release build, and isolated real installed-upgrade evidence all pass. A local candidate and evidence may be produced; pushing, publishing, changing the public manifest, or offering the update to users requires separate authorization.

## Delivery Order

1. Shared read-only discovery, inspection, snapshot, merge planning, transaction, and reporting core.
2. Standalone recovery executable for already affected users.
3. Stable `DataLocation` resolution and typed recovery-required startup state.
4. Installer target reuse, preflight manifest, independent snapshot, first-start health receipt, and rollback retention.
5. Durable rolling backups for every local data store.
6. In-app recovery center reusing the same core services.
7. Full upgrade matrix and local signed candidate evidence.

No prototype, remark, shortcut, or unrelated UI work is part of these checkpoints. Those changes resume only after the lossless upgrade boundary is verified.
