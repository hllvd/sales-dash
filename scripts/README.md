# Autonomous Testing Workflow

## Goal

Create a deterministic, AI-friendly autonomous testing workflow optimized for:

* low token consumption
* predictable execution
* autonomous retry loops
* concise debugging context
* architectural safety

The AI agent should handle repetitive execution and repair loops while humans handle business and architectural decisions.

---

# Core Principles

## Deterministic Execution

All commands must be standardized through scripts.

The AI must NEVER invent commands dynamically.

Always use:

```bash
./test.sh build
./test.sh integration
./test.sh e2e
./test.sh all
```

This guarantees:

* stable execution
* predictable outputs
* reusable workflows
* easier retries
* lower hallucination risk

---

# Log Strategy

## Full Logs

Every execution generates full logs:

```text
/artifacts
  full-build.log
  full-integration.log
  full-e2e.log
```

These logs are preserved for deep debugging.

---

## Concise Error Logs

The workflow extracts ONLY important failures into concise logs:

```text
/artifacts
  build-errors.log
  integration-errors.log
  e2e-errors.log
```

Filtering uses:

```bash
FILTER='fail(ed)?|exception|panic|fatal|error:'
```

This minimizes token usage and reduces noise.

The AI should primarily consume:

* concise error logs
* summaries
* recent failures

Full logs should only be inspected when necessary.

---

# Test Script Responsibilities

The `test.sh` script is responsible for:

* deterministic execution
* standardized outputs
* log generation
* error summarization
* artifact creation

The script MUST NOT:

* contain AI logic
* make architectural decisions
* modify code

---

# Antigravity Workflow

## Planning Mode First

For new features:

1. create implementation plan
2. identify impacted tests/services
3. identify risks
4. request approval for architectural changes

Only after approval:

* implementation begins

---

# Autonomous Retry Loop

For integration/E2E tests:

```text
1. Run tests
2. Read concise error logs
3. Analyze failures
4. Apply minimal fix
5. Re-run impacted tests only
6. Repeat until green
```

---

# Retry TTL

Default retry TTL:

* 3 retries maximum

If retries exceed TTL:

* summarize attempted fixes
* summarize blockers
* request human intervention

The AI must NEVER enter infinite repair loops.

---

# Memory System

Every testing/fixing session MUST update:

```text
memory.txt
```

The memory file should contain concise summaries only.

Example:

```text
[Integration]
- Fixed race condition in ContractService
- Previous issue caused intermittent SQLite locking
- Avoided adding arbitrary delays

[E2E]
- Login selector changed from CSS to getByRole
- Disabled animation causing flaky modal timing
```

Purpose:

* avoid repeating failed fixes
* preserve debugging history
* compact context for future retries
* reduce token usage

The memory must remain concise and actionable.

---

# Forbidden Actions

The AI MUST NOT:

* disable tests
* remove assertions
* weaken validations
* use arbitrary sleeps
* add unnecessary timeout increases
* bypass business logic
* modify architecture silently

If required:

* stop
* explain why
* request approval

---

# Human Responsibilities

Humans handle:

* architecture
* business logic
* schema changes
* API contracts
* security/authentication
* caching strategies
* concurrency decisions

AI handles:

* execution loops
* repetitive debugging
* deterministic retries
* log analysis
* test repair

---

# Final Philosophy

```text
Human controls decisions
AI controls execution
Scripts control determinism
```

This workflow optimizes:

* autonomous development
* token efficiency
* debugging speed
* reproducibility
* maintainability
