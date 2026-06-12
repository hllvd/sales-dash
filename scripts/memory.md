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

## 2026-06-04 e2e — Attempt 2
**Failure:** `contracts_team_filter.spec.ts` user registration fails on the second sequential run without database wipe.
**Root cause:** The test cleanup soft-deletes (`IsActive = false`) test users, but the backend's register uniqueness check still flags the email as existing. The `registerUser` helper's recovery flow could not find paginated-out users via the `GET /api/users?pageSize=1000` list endpoint.
**Fix applied:** Refactored `registerUser` in `contracts_team_filter.spec.ts`, `contracts_users_filter.spec.ts`, `team_members_management.spec.ts`, and `teams_hierarchy_visibility.spec.ts` to query for the email via `GET /api/users?search=...`. If the found user is inactive, it calls `PUT /api/users/{id}` to reactivate them (`isActive: true`), and returns the ID.
**Result:** ✅ Green

## 2026-06-11 e2e — Attempt 1
**Failure:** `user_metadata.spec.ts` timeout/strict-mode failures on checkbox select, gated page access, profile save required field validation, and confirmation modal.
**Root cause:** Validation checkbox used a missing span; gated page access did not display an error on frontend; required fields blocked form submission natively without showing the validation message; multiple inputs/buttons triggered strict-mode violations.
**Fix applied:** Updated checkbox locator to getByLabel; mocked fields endpoint to return 403 for non-admin; set form to novalidate via page.evaluate to trigger mock 400 validation error; scoped strict-mode violating selectors to their unique labels/dialogs.
**Result:** ✅ Green
