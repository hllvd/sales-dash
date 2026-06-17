# TODO List: Batch Operations Feature Improvements & Feature Flags

This document outlines proposed future improvements to enhance the quality, security, and scalability of the bulk modification features (`/#/batch` and `/#/tester`).

---

## 1. Feature Flag Implementation (Alternative Toggles)
To dynamically enable or disable developer/admin operations without redeploying the application:
- [ ] **Environment Configuration**: Add config values in `appsettings.json` / environment variables:
  ```json
  "FeatureFlags": {
    "EnableBatchOperations": true,
    "EnableTesterPanel": false
  }
  ```
- [ ] **Backend Enforcement**:
    - Build a `FeatureFlagAttribute` or middleware to intercept requests to `api/batch/*` and `api/tester/*` and return `403 Forbidden` or `404 Not Found` if the flag is disabled.
- [ ] **Frontend Integration**:
    - Fetch active feature flags on user login or application bootstrap.
    - Hide or show the left menu items (**Ferramentas Admin**, **Modificação em Lote**, **Painel de Testes**) based on both permissions and active flags.
    - Prevent direct URL navigation (`/#/batch` / `/#/tester`) using route guards.

---

## 2. Audit Trail & Log History
Bulk modifications can significantly impact database state. Logging these operations is critical:
- [ ] **Database Audit Table**: Create a `BatchOperationsLog` table:
  - `Id` (Guid)
  - `PerformedBy` (UserId/Email)
  - `Timestamp` (DateTime)
  - `OperationType` (e.g., `"UpdateParent"`, `"AssignTeam"`)
  - `Parameters` (JSON representation of the filters/inputs used)
  - `AffectedCount` (int)
  - `ResultSummary` (JSON list of updated and skipped users)
- [ ] **Admin History UI**: Create a read-only list view showing the history of all batch actions performed, allowing superadmins to review who changed what.

---

## 3. Rollback (Undo) Capabilities
- [ ] **State Snapshots**: Before executing any bulk change, capture the current state of affected relationships (e.g. previous parent email, previous active team start dates).
- [ ] **Undo Endpoint**: Expose `POST /api/batch/undo/{operationId}` that restores the snapshot state.
- [ ] **UI Button**: Add an "Desfazer Operação" (Undo) button in the results card immediately after a batch completes.

---

## 4. Scalability & Performance Optimization
- [ ] **Background Processing**: For superior hierarchies with 1000+ members, database updates might cause transaction timeouts or block the main thread. Integrate a background worker (e.g., Hangfire) to process requests asynchronously.
- [ ] **Progress Monitoring**: If processed in the background, provide a WebSocket/SSE connection or polling status bar to show progress (e.g., "Processing: 450/1200 users...").
- [ ] **Concurrency Controls**: Implement database locks or concurrency checks to prevent two superadmins from executing overlapping batch updates simultaneously.

---

## 5. UI/UX Enhancements
- [ ] **Confirmation Dialog**: Add a modal confirming the operation before execution, displaying the number of users that will be affected (e.g., "Esta ação irá atribuir 24 usuários à equipe. Deseja prosseguir?").
- [ ] **Advanced Filter Combos**: Allow combining parent email AND team filters rather than treating them as separate modes.
