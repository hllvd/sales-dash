# Features

## PowerBI Scraping Pipeline

This feature automates the extraction of data from PowerBI for users who do not have access to the ClientSecret/API directly. It provides a robust, professional-grade solution with historical tracking and manual controls.

### Core Objectives
The primary goal is to stabilize the data scraping pipeline by dynamically handling store-specific filters and user-based access control.

### Architecture Overview
- **Scraper Service (`pbi-scraper`)**: A lightweight Node.js microservice that executes the scraping logic using the existing PowerBI extractor engine.
- **Backend API (C#)**: Orchestrates the scraping process, handles manual triggers, and manages configurations.
- **Data Storage**:
  - **SQLite**: Stores scraping configurations and user matricula relationships.
  - **DynamoDB**: Durably logs all scrape history (jobs, status, row counts) using a single-table design.
  - **Local Storage**: Scraped CSV data is temporarily stored locally for auto-import into the main database.

### Key Capabilities
- **Manual Scrapes**: Users (Admin/SuperAdmin) can trigger a scrape on demand for a specific store and matricula.
- **Historical Tracking**: Detailed logs of every scrape execution, accessible via the dashboard.
- **Retry Mechanism**: Ability to manually retry failed scraping jobs.
- **Auto-Import**: Scraped data is automatically imported into the central contracts database after each successful run.
- **Role-Based Access**:
  - **SuperAdmin**: Can view all history, manage configs for any user, and retry any job.
  - **Admin**: Can view their own history and trigger scrapes for their assigned units.

### Future Roadmap
- **Scheduling**: Automated periodic scrapes (e.g., once every 2 days).
- **Auto-Retry**: Systematic retries for intermittent failures with exponential backoff.
- **Credential Management**: Transition from hardcoded tokens to using stored user credentials for dynamic authentication.

## Feature Testing UI (Tester)

We have a UI for testing features, such as email sending.

- **URL Path**: `#/tester` (hash-based navigation)
- **Menu Entry**: None (accessible only via direct URL)
- **Key Capabilities**:
  - **Email Service Test**: Input a user email to trigger the `forgot-password` recovery flow, validating that SMTP/SES connectivity and email templates are working correctly without requiring backend code changes or new test endpoints.

## Saúde das Matrículas Tabs (Monitoring)

This feature structures the matricula health monitoring interface into tabs for easier navigation, grouping, and administrative insight.

### Core Objectives
Improve visibility over matricula data freshness by allowing grouping by team and tracking administrative update activities.

### Key Capabilities
- **Matrículas Tab**: The baseline view displaying all matriculas, their total active contract volume, last update, and status based on age.
- **Equipes Tab**: Displays distinct matriculas grouped by active team membership. Employs lazy Mantine Accordion panels to render high counts efficiently. Teams with no active matriculas are hidden. Group headers show the worst health status of any matricula in that team.
- **Admins Tab**: Tracks administrative actions. Displays all admin users, their total successful imports count, and their last upload timestamp derived from completed `ImportSessions` for the `contractDashboard` template.

## Contracts Interface Settings and Advanced Filters

This collection of features allows administrators and users to customize their contract view and filters, improving both navigation and usability.

### Key Capabilities
- **End Date Filter & Local Validation**: Added end date filter to `ContractsPage`, defaulting to the current date and persisted in `localStorage`. Includes local validation that checks if the `End Date` is earlier than the `Start Date`, displaying an inline error message and preventing redundant API calls.
- **Dynamic Visible Columns Selection**: Users can choose which columns are visible in the contracts table (including the "Cota" column which is off by default) using a Mantine checkboxed modal. Visibility settings are saved to localStorage. Includes a single-click option to restore columns to their default layout.
- **Improved Empty State Messages**: Enhanced the empty state display on `MyContractsPage`. If no contracts are returned while filters (date/matricula) are active, it prompts the user with an improved Portuguese instruction: *"Nenhum contrato correspondente aos filtros aplicados foi encontrado. Você pode limpar os filtros para tentar novamente."* and a clear button to reset filters.

## Import Wizard Contract Number Pre-Validation

This feature detects malformed, blank, or short contract numbers during Step 1 of the Import Wizard before any records are committed to the database.

### Key Capabilities
- **Blank Contract Number Detection**: Automatically scans the `Contrato` column (case-insensitive) in uploaded spreadsheets. If any row is missing a contract number, a red block alert is displayed and advancement to Step 2 is blocked.
- **Short Contract Number Validation**: Identifies contract numbers with 3 or fewer characters (length ≤ 3). Displays an orange alert listing these contracts, blocking progress until the user checks a confirmation checkbox ("Agree").
- **E2E Test Data Generation Helpers**: Includes utility scripts (`create-xlsx-fixtures.js`) to programmatically compile Excel mock spreadsheets with specific validation criteria.

## Wizard Step 2: Duplicate Email Validation

This feature acts as a hard stop to prevent database pollution by rejecting users import when different user names share the same email address.

### Core Objectives
Detect and block imports where multiple user names are associated with the same email in the uploaded `users.xlsx` during Step 2 of the Import Wizard.

### Key Capabilities
- **Backend Validation Check**: Performs a case-insensitive, whitespace-trimmed check grouping user names by email address in the parsed rows.
- **Identical Duplicates Allowed**: Allows multiple rows with the same name and same email, as it is expected that some users might have multiple matriculas.
- **Frontend Red Alert Banner**: Displays a detailed list of duplicate email violations on Step 2 in a red alert block, blocking execution.

## Equipe (Teams) Permission & Scoping

This feature allows Admin users to access and manage their own team (Equipe), while enforcing security boundaries via the RBAC (Access Control) system and scoping API/UI access.

### Key Capabilities
- **RBAC Matrix Control**: Admin access to the Equipes feature is managed via the `teams:manage` permission on the Access Control (`Controle de Acesso`) page. Toggling it ON/OFF controls both backend API access and frontend menu visibility.
- **Hierarchical Team Scoping**: Admins who own a team can see their own team (along with descendant-owned teams) in the list, but teams owned by unrelated Admins or other hierarchies remain completely hidden.
- **REST CRUD Restrictions**: Admin-role users are prohibited from creating new teams (`POST /api/teams` returns 403 Forbidden) and deleting teams (`DELETE /api/teams/{id}` returns 403 Forbidden), regardless of UI settings.
- **Member Management Scoping**: Admins can add or remove members in their team. They are restricted to adding only descendants (children users) or orphaned users (without parent and without active team). Attempting to add users from other hierarchies is blocked at both UI and API levels (returning 400 Bad Request).
- **UI Gating**: The "Nova Equipe" creation button and "Excluir" trash icons are hidden on the Teams page for users with the Admin role.

## User Deactivation and Mandatory Contract Migration

This feature controls deactivation and ensures that user contracts are never left orphaned when a user is deactivated (soft-deleted) or disabled.

### Key Capabilities
- **Admin Deactivation Control**: Admins are granted the `users:delete` permission to deactivate (soft-delete) users. They are restricted in the backend to only deactivating their **direct child/subordinate users** (checked at `DELETE /api/users/{id}`).
- **Mandatory Contract Migration**: If a user being deactivated has active contracts assigned to them, migration to their direct superior is strictly **mandatory**. The user cannot skip migration (the optional check box has been removed from the confirmation modal).
- **Superior Mandatory Guard**: Deactivating a user who has active contracts but **no superior** is blocked. The confirmation modal will render a red blocking Alert preventing the action.
- **Deactivation Form Guard**: The "Usuário Ativo" checkbox in the profile edit form is disabled and documented for users with active contracts, forcing them to proceed via the Excluir/Delete flow where migration is enforced. If bypassed at the API level, the backend `PUT /api/users/{id}` returns a 400 Bad Request error.

## Contract Assignment Scoping and Matricula Guards

This feature enforces user scoping and active matricula checks during contract assignment in the contract creation and editing form.

### Key Capabilities
- **Hierarchical Vendor Scoping**: When an Admin accesses the contract form, the "Vendedor" (Seller) dropdown is restricted to only their descendant users.
- **Active Matricula Verification**: Displays a prominent Portuguese warning message if the selected seller has no active matriculas: `"Este usuário não possui matrícula, por favor vá em matrícula e atribua uma a ele antes de atribuir este contrato"`. Disables the submit button to block the assignment.
- **Auto-Selection Rules**:
  - If the selected seller has exactly one owner matricula (`isOwner === true`), it is automatically selected in the "Número da Matrícula" dropdown by default.
  - If the seller has more than one owner matricula, no default selection is made to prevent incorrect assignments, requiring the administrator to select one manually.
- **Form Validation Enforcement**: Prevents submitting the form if a seller is selected but no matricula has been selected, displaying an error toast/validation block.

## Active Users Licensing Dashboard

This feature provides a dedicated dashboard for SuperAdmin users to track the number of active users per month and automatically calculate the corresponding licensing cost based on active days and volume pricing tiers.

### Key Capabilities
- **Audit-Log-Based Active Days Calculation**: Counts the exact number of days a user was active (`IsActive == true`) during the selected calendar month by parsing `AuditLogs` for user state changes.
- **Dynamic Tiered Pricing**: Automatically maps the licensed user count (those with active days $\ge$ the threshold) to flat-rate pricing tiers (1–300 users: R$ 28; 301–800 users: R$ 26; >801 users: R$ 20).
- **Flexible Threshold Control**: Allows adjusting the minimum active days threshold (defaulting to 15, configurable in `appsettings.json`).
- **User Exclusion Rule**: Excludes specified accounts (e.g. `superadmin@salesapp.com`) from active count and pricing calculations via `appsettings.json` configuration.
- **Interactive Reports View**: Provides monthly filtering, total licensing costs KPI, active tier highlighting, detailed list of users with active days status, and a CSV export helper.

## Contract "Não Definido" Status

This feature handles unknown contract statuses gracefully during file imports using the `contractDashboard` template, defining them under a new visual-only **"Não Definido"** (Undefined) status.

### Core Objectives
Avoid crashing the entire contract dashboard import pipeline when an unknown status (such as `COBRANCA ADMINISTRATIVA`) is detected, by safely importing it without affecting metrics, sums, or deactivation validations.

### Key Capabilities
- **Fallback Status Mapping**: Unknown status values during import automatically map to the `"NaoDefinido"` status.
- **Traceability of Original Status**: The raw original status string is preserved and saved in the contract's `RawStatus` column.
- **Visual Display & Tooltip**: Shown in `/#/contract` with a neutral gray badge. Hovering over the badge displays a tooltip showing the original status name from the file (e.g., `Status original no arquivo: COBRANCA ADMINISTRATIVA`).
- **Exclusion from Business Logic and Totals**:
  - Excluded from production totals (`totalProduction`).
  - Excluded from active retention calculations (`activeAmount` and `strictActiveAmount`).
  - Excluded from user deactivation checks (contracts with `"NaoDefinido"` status do not block deactivating a user).
- **Import Warning System**: Shows a warning in the import wizard / bulk import results screen specifying which unknown statuses were detected and mapped (e.g., `We detected the status "COBRANCA ADMINISTRATIVA" and we will define it as "Nao definido"`).

## Gamification Fields on ClassificationLevel

This feature enriches the `ClassificationLevel` entity with gamification metadata, enabling administrators to define progression chains, retention targets, and hierarchical minimum-direct-report requirements for each classification tier.

### Key Capabilities
- **Retention Target**: Each level can define an optional retention percentage (0–100%), displayed as an orange badge on the classification card.
- **Next Level (Progression Chain)**: A self-referencing FK (`NextLevelId`) allows building an ordered chain of levels (e.g., Bronze → Silver → Gold). The next-level name is shown as a violet chip on the card.
- **Minimum Direct Rules (#1 and #2)**: Two optional composed rules per level, each specifying:
  - A target classification level that must be directly below (another self-referencing FK)
  - A minimum number of people in that tier (`0` = unlimited, shown as ∞)
  - Displayed as cyan badges on the card when set.
- **Clear/Nullify Logic**: Update requests use explicit `ClearNextLevel`, `ClearMinimumDirect1`, and `ClearMinimumDirect2` boolean flags to explicitly null-out previously set FKs, avoiding ambiguity between "not sending the field" and "clearing it".
- **Database**: All FKs use `SET NULL` on delete — deleting a referenced level does not cascade-delete or block levels that reference it.

## UI Performance Optimization & Caching

This feature optimizes the React client-side application performance, eliminating redundant API requests, parallelizing serial waterfalls, and implementing an in-memory session-scoped Reference Data Cache.

### Core Objectives
Improve UI response times, eliminate redundant load on the backend, and prevent redundant network calls during page navigation or common user actions (such as opening forms).

### Key Capabilities
- **Reference Data Context (`ReferenceDataContext`)**: Serves as a centralized, in-memory cache for long-lived reference data sets (Teams, PVs, Classifications, Matriculas, and all users). Data is fetched once and reused across route navigation.
- **Cache Invalidation on Mutation**: The cache is automatically invalidated when a user performs a write action (Create, Update, or Delete) on Matriculas, Teams, Classification Levels, or PVs. This ensures subsequent fetches retrieve fresh, consistent data.
- **Client-side Search and Filtering (Matriculas)**: Decouples the search debouncer from API requests. The complete list of matriculas is fetched once on mount. 
  - **Comma-Separated Search**: Search input accepts multiple terms separated by commas, checking matches for matricula number or username.
  - **Status Selection Filter**: Introduces active/inactive filtering via a Mantine select dropdown, evaluating validity dates client-side.
  - Done entirely in memory using `useMemo` with no additional API queries.
- **Manual Refresh Actions**: Introduces a manual "Atualizar" (Refresh) button on the Matriculas, Teams, Classifications, and PV management pages, allowing users to explicitly bust the session cache and retrieve fresh data.
- **Contract Form Optimization**: Overhauls `ContractForm` to read from the cache for users, groups, and PVs, completely eliminating up to 3 redundant API calls every time the "Criar/Editar Contrato" modal is opened.
- **Admin Import Model Restriction**: Restricts `BulkImportModal` to hide the template selection dropdown for users with `admin` role, automatically enforcing the `"contractDashboard"` template model as default, while allowing `superadmin` users to still choose other models.
- **Contract Dashboard Parameterized Import Updates**: Enables configurable update behavior when importing existing contracts via the `contractDashboard` template:
  - **Atualizar matrícula em contratos existentes**: Checkbox (default: unchecked/off). Controls whether `MatriculaId` is updated on existing contracts.
  - **Atualizar valor total em contratos existentes**: Checkbox (default: checked/on). Controls whether `TotalAmount` is updated on existing contracts.
- **Waterfall Fetch Elimination**: 
  - **MyContractsPage**: Parallelizes pending claims retrieval using `Promise.all` instead of a serial `for...of` loop (one query per owned matricula). Also separates date-based filter changes from claims loading.
  - **TeamsPage**: Parallelizes member removal logic using `Promise.all` to execute all removal calls simultaneously.

## Admin Matrícula Filtering and Left Menu Integration

This feature restricts Admin users on the Matrículas page to see only their own owned matrículas, while integrating left menu visibility with the Access Control (`Controle de acesso`) permission system.

### Key Capabilities
- **Access Control Left Menu Visibility**: The "Matrículas" option on the left menu is shown based on the dynamic `matriculas:read` permission (managed via `Controle de Acesso`), rather than being hardcoded to `system:superadmin`.
- **Admin Matrícula Scoping**: Users with the Admin role (role ID 2 / role name "admin") are restricted in both frontend and backend to viewing only their own matrículas (where they are defined as the owner, i.e., `IsOwner == true` and `UserId == currentUserId`).
- **Read-Only Screen for Admin**: Write actions on the Matrículas screen (including the "Nova Matrícula" button, "Importar CSV" button, and table "Ações" column containing edit and delete buttons) are hidden for the Admin role, rendering the page read-only for their own matrículas.
- **SuperAdmin Unchanged**: The SuperAdmin retains full system access to all matrículas, and has full permission to create, import, edit, and delete any matrícula.

## Authentication and Password Overwrite Security Fixes

This feature resolves intermittent and silent login failures for advisors and managers, strengthening password security and stability.

### Key Capabilities
- **Thread-safe Password Generation**: Replaces the non-thread-safe `System.Random` class with `RandomNumberGenerator` from `System.Security.Cryptography` in `PasswordGenerator` to prevent random state corruption and password mismatches under concurrent requests.
- **Accidental Password Overwrite Protection**: Restricts Excel spreadsheet imports so that an existing user's password is only overwritten if they still use the default `"ChangeMe123!"` password, have no password, or have never logged in. Active users with custom passwords will never have their passwords silently overwritten.
- **Active RefreshToken Check**: Identifies users who have never logged into the system by querying the database for active RefreshTokens, allowing safe conditional password overwrites without requiring a new database schema migration.
- **Separate Inactive User Check**: Distinguishes between incorrect passwords and deactivated users during authentication, preventing active session validation from returning misleading "invalid credentials" messages for disabled accounts.

## Equipe — Usuários Disponíveis Bug Fix

Fixes a production bug where some users (including admin's direct children and all users visible to superadmin) were silently missing from the "Usuários Disponíveis" left-column in the Equipe (Team) management modal.

### Root Causes Fixed
- **Pagination Truncation (Bug 1):** `fetchAllUsers` in `ReferenceDataContext` called `getUsers(1, 1000)` — a single fixed page. Any users beyond position 1000 (sorted by role then active status) were silently dropped from `allUsers`, making them invisible even for superadmin.
- **Missing Server-Side `IsActive` Filter (Bug 2):** `UserRepository.GetAllAsync` returned both active and inactive users. Inactive users wasted pagination slots, reducing the effective capacity of the 1000-user page and crowding out valid active users.
- **Admin BFS Broken by Truncation (Bug 3):** The client-side BFS in `TeamMembersModal.tsx` traverses `allUsers` to find children by `parentUserId`. When a first-level child was beyond position 1000 they were absent from `allUsers`, so the BFS never visited them.

### Changes
- **`GET /api/users`:** New `activeOnly=true` query param. When set, `UserRepository.GetAllAsync` prepends a `.Where(u => u.IsActive)` filter server-side before pagination, eliminating wasted slots.
- **`fetchAllUsers` (ReferenceDataContext):** Now loops pages (`pageSize=1000`) until `accumulated.length >= totalCount`, guaranteeing all users are fetched regardless of total count. Passes `activeOnly=true` to reduce payload size.
- **`apiService.getUsers`:** New optional `activeOnly` parameter appended to query string when `true`.

### Integration Tests Added
Four new tests in `TeamsControllerIntegrationTests.cs`:
1. `GetUsers_AsSuperadmin_ShouldReturnAllActiveUsers` — verifies active users appear and inactive are excluded with `activeOnly=true`.
2. `GetUsers_WithoutActiveOnlyFilter_ShouldIncludeInactiveUsers` — documents the pre-fix behaviour (inactive users returned without `activeOnly`).
3. `GetUsers_PaginationTruncation_TotalCountExceedsReturnedItems` — proves truncation occurs when `pageSize < totalCount`.
4. `GetUsers_AsAdmin_DirectChildrenAppearWithCorrectParentUserId` — verifies admin's direct children appear with correct `parentUserId` so client BFS resolves them.

## UI Copy Enhancements

Provides clearest contextual language in UI screens to eliminate user confusion and support requests.

### Key Changes
- **Import Wizard Inconsistency Alert:** Renamed the alarming "Inconsistências no Cadastro de Vendedores Detectadas" alert title to "Resumo de Atribuições por Matrícula" and updated description copy to clearly explain existing assignments.
- **My Contracts Matrícula Helper:** Added dynamic explanation text in the contract assignment modal's Matrícula field (e.g. *"A matrícula identifica você como vendedor responsável pelo contrato 123123123."*), helping users understand the concept of a Matrícula when claiming or registering contracts.

## Outlier Amount Detection — Contract Template Import

Detects ambiguously formatted values in the **Total** column during contract template import (Step 1 scan), preventing silent data corruption where `80.000.00` would be misread as R$ 8,000,000.

### Problem
Users occasionally type values with two dots and no comma (e.g. `80.000.00`), intending R$ 80,000.00. The Brazilian currency parser strips all dots and produces R$ 8,000,000 — silently, with no error.

### Detection Logic
- During Step 1 file scan, every row's Total column value is inspected for the ambiguous pattern: **2+ dots and 0 commas**.
- All unambiguous Total values (clear Brazilian `1.000,00` or US `1,000.00` formats) are collected to compute the **file median**.
- For each ambiguous value, two interpretations are generated:
  - **A** — last dot treated as decimal separator (e.g. `80.000.00` → R$ 80,000.00)
  - **B** — all dots treated as thousand separators (e.g. `80.000.00` → R$ 8,000,000.00)
- The interpretation **closest to the file median** is selected as "most likely correct".

### User Experience
- A yellow **"Valores com Formato Ambíguo no Campo Total"** alert appears in Step 1 with:
  - Formatting guidance (correct vs. incorrect examples using `Code` blocks)
  - A table listing: row number, raw value, likely interpretation (highlighted), alternative interpretation
  - The file median displayed as context
- The warning is **non-blocking** — users can still proceed with the import.
- Up to 50 ambiguous values are shown.

### Key Files
- `ImportPreviewResponse.cs` — added `OutlierAmounts` list with `OutlierAmountEntry` record
- `WizardService.cs` — outlier detection + median computation + `TryParseUnambiguousCurrency` helper
- `ImportWizardPage.tsx` — new alert UI with table and formatting guidance

## Agnostic Request and Approval Pipeline

This feature provides a comprehensive, domain-agnostic request and approval pipeline (`#/requests`), enabling users across roles to submit data modification requests and allowing authorized approvers (Admins and SuperAdmins) to review and act on them with one-step immediate execution.

### Core Objectives
Allow users to request operational changes (such as parent email changes or new matriculas) without giving them direct edit permissions, while consolidating all pending requests into a centralized management dashboard for approvers with "Sim" (Approved), "Não" (Rejected with reason), and "Depois" (Later / Pending retention) options.

### Initial Request Types Supported
1. **Change Parent Email (`ChangeParentEmail`)**: Standard users or managers request updating their superior (`ParentUserId`) by specifying the target email.
2. **User Request Matricula (`RequestMatricula`)**: Standard users request assignment of a new matricula number.
3. **Admin Request Matricula (`AdminRequestMatricula`)**: Admins request creation of a new matricula that they will own (`IsOwner = true`). Only SuperAdmins can approve this request type.
4. **Request Admin Role (`RequestAdminRole`)**: Non-admin users request promotion to the Admin role (`"Solicitação de Perfil Administrador (Role Admin)"`). SuperAdmins and parent Admins can approve this request.
5. **Request Classification Level (`RequestClassificationLevel`)**: `user` and `admin` roles request assignment to a classification level (`"Solicitação de Nível de Classificação"`). Requires a mandatory start date and auto-closes any previous active level on approval.

### Approver Actions
- **Sim / Aprovar (Yes)**: One-step immediate approval. Executes the underlying model update on the server (e.g. re-parenting user, creating/linking matricula, updating RoleId to Admin, assigning classification level) and marks status as `Approved`.
- **Não / Rejeitar (No)**: Rejects the request, recording an optional rejection reason comment for the requester to view.
- **Depois (Later)**: Postpones decision, leaving the request in `Pending` status to be revisited later.

### Access Control and Scoping
- **SuperAdmin**: Sees all system pending requests and can approve/reject any request type.
- **ChangeParentEmail**: If the target `parentEmail` is an Admin, **only** that designated `parentEmail` user and SuperAdmins can see, approve, or reject the request.
- **RequestMatricula (Nova Matrícula)**: If the target matricula is already owned by a user (`IsOwner = true`), **only** that matricula owner user and SuperAdmins can see, approve, or reject the request.
- **AdminRequestMatricula**: Only SuperAdmins can see, approve, or reject.
- **RequestAdminRole**: SuperAdmins and the user's parent Admin(s) in the hierarchy can see, approve, or reject.
- **RequestClassificationLevel**: SuperAdmins and the user's parent Admin(s) in the hierarchy can see, approve, or reject.
- **Left Menu Integration**: Shows a "Solicitações" item with a dynamic badge displaying the current count of pending requests for approvers.

### Key Files Created / Modified
- `ApprovalRequest.cs` — Entity model with `RequestType`, `RequesterId`, `ApproverId`, `Status`, `PayloadJson`, `ApproverComment`.
- `ApprovalDTOs.cs` — Request/response DTOs and typed payload structures.
- `IApprovalService.cs` & `ApprovalService.cs` — Business logic, permission checks, and entity mutators.
- `ApprovalRequestsController.cs` — REST endpoints (`POST /api/approval-requests`, `GET /pending`, `GET /mine`, `POST /{id}/resolve`).
- `RequestsPage.tsx` & `RequestsPage.css` — Tabbed UI for pending approval management and user request tracking.
- `MyProfilePage.tsx` & `MatriculasPage.tsx` — Contextual shortcut buttons to open request modals.

## Environment-Gated Google Analytics

Provides production-only Google Analytics tracking by leveraging a build-time environment variable to prevent local development environments, local Docker instances, and E2E test suites from polluting production tracking analytics.

### Key Capabilities
- **Build-Time Variable Injection**: Utilizes `REACT_APP_GA_TRACKING_ID` to supply the Google Analytics Measurement ID.
- **Auto-Disable on Empty Variable**: The initialization service (`analyticsService.ts`) automatically exits if the environment variable is not defined or is left empty, preventing any tracking code from running or script files from loading in local/E2E environments.
- **Docker and CI/CD Pipeline Integration**: Configured in `docker-compose.yml`, `Dockerfile.client`, and `.github/workflows/deploy.yml` to support building the production image in Docker containers locally or through GitHub Actions.

### Key Files Created / Modified
- `analyticsService.ts` — Conditional analytics loader service.
- `index.html` — Removed static analytics scripts.
- `docker-compose.yml` — Added client service build-arg parameter.
- `Dockerfile.client` — Added client container build parameter mapping.
- `deploy.yml` — Configured GitHub Actions build pipelines.

## User Role Access to Solicitações & RBAC Integration

Grants users with the `User` role access to the `Solicitações` page to track their submitted requests ("Minhas Solicitações") and integrates `requests:read` into the Access Control (RBAC) matrix.

### Key Capabilities
- **RBAC Matrix Integration**: Registered `requests:read` permission in `DbSeeder.cs` and assigned it to `SuperAdmin`, `Admin`, and `User` roles by default. Can be enabled/disabled per role on the Access Control (`#/access-control`) page.
- **Menu NavLink Permission Guard**: Wrapped the `Solicitações` link in `Menu.tsx` with `hasPermission('requests:read')` so visibility dynamically reflects the user's role permissions.
- **Non-Approver Default View**: In `RequestsPage.tsx`, users with non-approver roles (`user`) land directly on the "Minhas Solicitações" tab.

## Frontend Search Input Whitespace Trimming

Trims leading and trailing whitespace from search inputs across all frontend pages and modals (Users, Teams, Classifications, Matrículas, Contracts, Access Control, Requests, Monitoring, Reports, etc.), ensuring searches with accidental spaces match correctly while preserving internal spaces between words.

### Key Capabilities
- **Automatic Edge Trimming**: All search filter conditions and API query parameters evaluate `searchQuery.trim()`, stripping accidental leading or trailing spaces.
- **Internal Whitespace Preservation**: Preserves spaces within multi-word search phrases (such as first and last names, e.g. `"João Silva"`).
- **Universal Application**: Standardizes search query handling across list pages, modals, selection dropdowns, and monitoring dashboards.

## User Classification Modal UX Parity & Admin Scoping (2026-07-28)

Aligns the user classification level members modal with the Teams modal UX: wider modal (`size="85%"`), column order swap (Atribuir Novos Membros on the left, Membros Ativos on the right), search filter in Membros Ativos, click-to-add user assignment, and admin hierarchy scoping (BFS subordinate user pool).

### Key Capabilities
- **Column Swap & Layout**: Placed "Atribuir Novos Membros" on the left column and "Membros Ativos" on the right column.
- **Wider Modal Window**: Increased modal dialog size to 85% width matching the Teams modal styling.
- **Search in Active Members**: Added search input (`IconSearch`) in "Membros Ativos" to quickly filter assigned level members by name or email.
- **Click-to-Add Flow**: Single-click user card assignment (removing checkboxes and bulk submit button) applying selected start/end dates immediately.
- **Admin Hierarchy Scoping**: Reused BFS subordinate tree resolution from `TeamMembersModal` so `Admin` users only see their direct and indirect subordinates in the user assignment list, while `SuperAdmin` users see all active users.

### Key Files Created / Modified
- `client/sales-dash/src/components/Menu.tsx` — Authorized `Admin` and `SuperAdmin` roles to see `Níveis de Classificação` link in the left menu.
- `client/sales-dash/src/components/ClassificationsPage.tsx` — Updated modal layout, user pool BFS scoping, search on active members, and click-to-add handler.
- `client/sales-dash/src/components/ClassificationsPage.css` — Modal grid adjustments.
- `client/e2e-test/e2e/classification_management.spec.ts` — Updated E2E tests to match click-to-add interaction.
- `client/e2e-test/e2e/classification_members_modal.spec.ts` — Added dedicated E2E test spec for classification members modal UX and Admin menu access.



