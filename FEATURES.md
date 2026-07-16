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
