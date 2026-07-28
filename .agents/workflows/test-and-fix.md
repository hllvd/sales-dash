---
description: Autonomous test-fix loop using test.sh. Run tests, read summaries, fix, repeat until green. TTL 5.
---

// turbo-all

# Test & Fix Workflow

## When to Use this Workflow

Just use the test-and-fix workflow if:
- We find a bug and need to test it.
- We need to write down how to test a new feature on the implementation plan.

**CRITICAL:** Don't run `./test.sh all` just to create or prepare the implementation plan. This is useless and delays programming tasks.

---

## Step 1 — Run the full suite

```bash
./test.sh all
```

Read `artifacts/build-errors.log`, `artifacts/integration-errors.log`, `artifacts/e2e-errors.log`.

**If all three show `Error lines found: 0`:**
```
✅ ALL TESTS PASSED

Target: all
Attempts: 0 / 5
Result: ✅ Green — no fixes needed

Fixes applied: None
Remaining failures: None
```
Stop here. Do not continue.

---

## Step 2 — Triage each failure

For each error log that has failures, classify before doing anything:

**Infrastructure failure → STOP IMMEDIATELY. Do not retry. Do not consume TTL.**

Examples: Docker daemon not running, socket permission denied, Compose not installed, port in use, missing `.env`.

Write blocker entry to `scripts/memory.md` and output:
```
🚫 BLOCKED — infrastructure issue, cannot proceed

Failure: <exact error>
Required action: <what the human must do>
```

**Test/code failure → proceed to Step 3.**

Examples: failing assertions, wrong selectors, bad test data, API mismatch in tests.

---

## Step 3 — Retry loop (TTL = 5 per target)

> [!IMPORTANT]
> **E2E Target Execution:** Whenever running or retrying the `e2e` target, **ALWAYS** run `./test.sh rm-db && ./test.sh e2e` instead of `./test.sh e2e` alone. Resetting the database clears stale data and prevents false test failures.

```
ATTEMPT = 1

while ATTEMPT <= 5:
  1. Check scripts/memory.md — if this exact failure was already tried, skip that fix.
  2. Apply minimal fix (see Fix Scope below).
  3. Run ./test.sh <failing-target>   ← only failing target (for 'e2e', ALWAYS run `./test.sh rm-db && ./test.sh e2e`)
  4. Read artifacts/<target>-errors.log
  5. Write entry to scripts/memory.md (see format below).
  6. If 0 errors → ✅ target green. Move to next failing target.
  7. ATTEMPT++

If ATTEMPT > 5 → STOP. Write blocker entry. Request human intervention.
```

---

## Reading logs — priority order

1. `artifacts/<target>-errors.log` — always first. Concise filtered signal.
2. `artifacts/full-<target>.log` — only if error log is empty but exit code was non-zero, OR every fix in `memory.md` has already been tried and failed.

---

## Fix scope

**The agent may ONLY edit these files without approval:**
- `client/e2e-test/e2e/*.spec.ts` — Playwright test specs
- `SalesApp.IntegrationTests/**/*.cs` — integration test files
- `scripts/exclude.txt` — false-positive exclusions
- `scripts/memory.md` — session log (append only)

**Everything else requires explicit human approval before any edit.** This includes:
- `scripts/test.sh` / `test.sh`
- Any production source code
- Database schema / migrations
- API contracts, DTOs, endpoints
- Docker or environment config
- `playwright.config.ts`

If a fix requires a file outside the whitelist:
1. Stop immediately.
2. State: file, line, exact change, reason.
3. Wait for approval.

---

## memory.md entry format

File: `scripts/memory.md` — append only.

```
## [YYYY-MM-DD] <target> — Attempt <N>
**Failure:** <one line>
**Root cause:** <one line>
**Fix applied:** <what changed, or "None — blocker">
**Result:** ✅ Green | ❌ Still failing | 🚫 Blocked
```

---

## End-of-run summary (always required)

```
Target: all
Attempts: <N> / 5
Result: ✅ Green | ❌ Blocked

Fixes applied:
- <fix 1>

Remaining failures / next step:
- <description or "None">
```
