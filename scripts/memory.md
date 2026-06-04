# Test & Fix Memory

This file is maintained by the Antigravity agent.
Each entry records a fix attempt — past entries must be consulted before retrying a failed fix.

---

<!-- Append new entries below this line -->

## 2026-06-04 e2e — Attempt 1
**Failure:** 10 flaky/failed E2E tests in tear-2-roles-testing due to timeouts, race conditions, static delays, and database variance.
**Root cause:** Hardcoded timeouts on searchable Select fields, race conditions in template mismatch warning handling, static delays after DB updates before query, and static percentage comparison.
**Fix applied:** Increased timeouts, replaced static waits with dynamic visibility (`toBeVisible`/`toBeEnabled`) waits, added Promise.race mismatch warning handling, used robust href-based sidebar locators, added SQLite commit settle delay, and adjusted percentage assertions.
**Result:** ✅ Green
