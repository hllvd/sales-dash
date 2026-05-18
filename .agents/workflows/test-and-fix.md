---
description: Autonomous test-fix loop using test.sh. Run tests, read summaries, fix, repeat until green. TTL 5.
---

# Autonomous Test & Fix Workflow

## Context

- **Test runner:** `./test.sh` (symlink to `scripts/test.sh`) — always run from project root.
- **Artifacts:** `artifacts/` — generated automatically, gitignored.
- **Memory:** `scripts/memory.md` — append a concise entry after every fix attempt.
- **Exclusions:** `scripts/exclude.txt` — add patterns for known false-positive log lines.

---

## Commands

| Command | When to use |
|---|---|
| `./test.sh build` | Docker compose build only |
| `./test.sh integration` | .NET integration tests only |
| `./test.sh e2e` | Playwright E2E only |
| `./test.sh all` | Full suite |
| `./test.sh logs` | Dump all raw logs |
| `./test.sh clean` | Delete artifacts |

---

## Retry Loop

TTL: **5 attempts**. Reset TTL per command (build / integration / e2e).

```
ATTEMPT = 1

while ATTEMPT <= 5:
  1. Run ./test.sh <target>
  2. Read artifacts/<target>-errors.log  ← primary signal
  3. If 0 errors → ✅ STOP. Write success summary to memory.md.
  4. Analyze failures.
  5. Apply minimal fix.
  6. Append fix entry to scripts/memory.md.
  7. ATTEMPT++

If ATTEMPT > 5 → STOP. Do not continue.
  Write blocker summary to memory.md.
  Request human intervention.
```

---

## Reading Errors — Priority Order

1. **`artifacts/<target>-errors.log`** — always read first. Filtered, concise.
2. **`artifacts/full-<target>.log`** — inspect only when:
   - error log is empty but tests failed (exit code non-zero), OR
   - all items in `memory.md` have already been tried and failed.
3. **Never** read the full log as the default first step — it defeats the token strategy.

---

## memory.md Format

File: `scripts/memory.md`
Append only. Never delete past entries. Keep each entry concise.

```markdown
## [YYYY-MM-DD] <target> — Attempt <N>

**Failure:** <one-line summary of the error>
**Root cause:** <what caused it>
**Fix applied:** <what was changed>
**Result:** ✅ Green / ❌ Still failing
```

Before applying any fix, scan `memory.md` for the same failure.
If a fix was already tried and failed, skip it — do not repeat failed strategies.

---

## Fix Rules

**Allowed without approval:**
- Fix failing assertions
- Fix test data / selectors / timing
- Fix API response handling in tests
- Fix test setup/teardown
- Add entries to `scripts/exclude.txt` for confirmed false positives

**Requires human approval before touching:**
- Database schema changes
- API contract changes (new endpoints, changed DTOs)
- Authentication / authorization logic
- Business logic in production code
- Architectural changes

If a required fix falls into the approval category:
1. Stop the loop.
2. Explain exactly what change is needed and why.
3. Wait for approval.

---

## False Positives

If a log line is confirmed noise (not a real error):
- Add its pattern to `scripts/exclude.txt`
- Add a comment explaining why it is safe to suppress
- Do **not** widen existing patterns — be specific

---

## End of Run — Required Summary

After the loop ends (green or TTL exceeded), output:

```
## Test Run Summary

Target: <build | integration | e2e | all>
Attempts: <N> / 5
Result: ✅ Green | ❌ Blocked

### Fixes Applied
- <fix 1>
- <fix 2>

### Remaining Failures (if blocked)
- <description>

### Recommended Next Step (if blocked)
- <what the human needs to decide or change>
```
