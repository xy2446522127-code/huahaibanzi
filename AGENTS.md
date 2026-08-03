<!-- app-product-delivery:start -->
## App Product Delivery Policy

- Persistent delivery policy: enabled.
- Guidance mode: `beginner_detailed_auto`.
- Project kind: `new`.
- New-project visual contract: two approvals required: visual master, then shared UI shell preview before real features.
- Stage delivery: maximize safe parallel functional slices; keep shared resources and integration single-writer.
- Thread orchestration: automatically create isolated child tasks; coordinator owns integration and shared contracts.
- Cross-conversation memory: use the versioned coordination manifest and block stale child tasks before writes or checkpoints.
- Evidence acceptance: every coherent step records baseline, expected-vs-actual difference, outcome, and checkpoint using the appropriate adapter.
- Existing UI: derive from the approved baseline; classify and propagate shared visual changes.
- Continue automatically except at documented approval, risk, authority, destructive-action, or failed-verification boundaries.
- Use explicit owned paths for verified Git checkpoints.
- Keep `.codex/artifacts/ui-qa/` evidence out of Git.
- Do not push or deploy without project-specific authorization.
<!-- app-product-delivery:end -->
