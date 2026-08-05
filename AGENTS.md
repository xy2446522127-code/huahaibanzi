<!-- app-product-delivery:start -->
## App Product Delivery Policy

- Persistent delivery policy: enabled.
- Guidance mode: `beginner_detailed_auto`.
- Project kind: `new`.
- Execution mode: autonomous implementation after product decisions; never ask the user to approve code details.
- New-project visual contract: user decides product/UI direction and functional plan; code implementation proceeds autonomously.
- Progress: maintain evidence-weighted progress with separate core and overall percentages.
- Classification: keep product scale and change scale separate; persist classification before UI or architecture work.
- UI carrier: preserve the approved executable UI shell as the source for Web, desktop, and later packaging targets.
- Interaction contract: every approved visible control requires dynamic evidence or a disabled-with-reason state.
- Compatibility review: migrate stale plans before dispatching workers or continuing feature development.
- Tokens: enforce the bounded token budget; freeze optional scope at 70% and stop nonessential work at 100%.
- Stage delivery: use minimum sufficient concurrency; available slots alone never justify another worker.
- Thread orchestration: use cold task packets and isolated worktrees only when parallel critical-path benefit is proven.
- Cross-conversation memory: use the versioned coordination manifest and block stale child tasks before writes or checkpoints.
- Evidence acceptance: route each surface to bounded deterministic evidence; retain before, after, and difference only when material.
- Existing UI: derive from the approved baseline; classify and propagate shared visual changes.
- Checkpoints: one verified functional outcome equals one checkpoint; coordination metadata is not a standalone checkpoint.
- Runtime: use fixed or leased ports, PID records, bounded logs, and cleanup on success, failure, interruption, or timeout.
- Completion boundary: non-blocking findings go to the backlog and do not auto-expand the stage.
- Continue automatically; pause only for an undiscoverable product conflict, missing external authority, irreversible action, or failed required verification.
- Use explicit owned paths for verified Git checkpoints.
- Keep `.codex/artifacts/ui-qa/` evidence out of Git.
- Do not push or deploy without project-specific authorization.
<!-- app-product-delivery:end -->
