# Features

## Reconciliação de Contratos: Filtro por Equipe e Novas Categorias de Divergência

Esta funcionalidade expande a ferramenta de Reconciliação de Contratos (`/#/contract-reconciliation`), adicionando o filtro opcional por **Equipe** que preenche/restringe a listagem de usuários apenas aos membros ativos da equipe selecionada, além de introduzir duas novas categorias de divergência de auditoria: **Divergência de Data** e **Divergência de Vendedor**.

### Core Objectives
- **Filtro de Equipe (`Team`)**:
  - Novo campo de seleção de equipes no formulário de reconciliação.
  - Ao selecionar uma equipe, o dropdown de seleção de usuários passa a listar automaticamente apenas os membros **ativos** pertencentes àquela equipe (`isActive = true`).
  - Ao alterar ou limpar a equipe selecionada, a seleção de usuário é redefinida com segurança.
  - No backend, ao submeter com uma equipe selecionada (sem usuário individual), os contratos do sistema e o cruzamento com o XLSX são filtrados estritamente pelos membros ativos daquela equipe.
- **Rótulo Dinâmico para Contratos Ausentes na Planilha**:
  - Quando uma equipe está selecionada, o card de KPI e a aba correspondente passam a indicar expressamente os contratos da equipe: *"Contratos da Equipe [Nome da Equipe] ausentes no XLSX"*.
- **Divergência de Data (`Date Mismatches`)**:
  - Identifica contratos presentes tanto na planilha XLSX quanto no sistema cuja data de venda (`SaleStartDate` vs data extraída da planilha) seja divergente (considerando apenas a parte da data).
  - Exibe card de KPI dedicado com contagem e valor total, além de aba interativa detalhando número do contrato, data no sistema, data no XLSX, valor e usuário.
- **Divergência de Vendedor (`Seller Mismatches`)**:
  - Identifica contratos presentes em ambas as fontes, porém atribuídos a vendedores/usuários diferentes entre o sistema e a planilha importada.
  - Exibe card de KPI dedicado com contagem e valor total, além de aba interativa exibindo o vendedor no sistema, vendedor identificado no XLSX, valor e data de venda.
- **Exportação CSV Completa**:
  - As novas abas de Divergência de Data e Divergência de Vendedor contam com suporte à exportação de relatórios em formato CSV compatível com Excel.

---

Esta funcionalidade simplifica e automatiza o processo de criação de novos usuários por administradores (`Admin`), preenchendo automaticamente o gestor com o administrador logado, selecionando por padrão a matrícula própria do gestor e vinculando o novo usuário à equipe do gestor.

### Core Objectives
- **Preenchimento Automático do Gestor**: Ao abrir o modal de criação de usuário como Administrador, o campo `"Usuário Pai"` é preenchido automaticamente com o e-mail do administrador autenticado, mantendo a possibilidade de alteração para outros usuários subordinados na sua hierarquia. Para Superadministradores, o campo inicia vazio.
- **Usar Matrícula do Gestor**:
  - Opção `"Usar matrícula do gestor"` marcada por padrão (`checked`).
  - Caso o gestor possua exatamente 1 matrícula como proprietário (`isOwner = true`), esta matrícula é selecionada e exibida automaticamente.
  - Caso o gestor seja proprietário de 2 ou mais matrículas, um menu de seleção (`Select`) é exibido contendo apenas as matrículas onde o gestor é o proprietário.
  - Caso o gestor não possua matrículas próprias, uma mensagem informativa é exibida e o campo manual de matrícula fica disponível.
  - Caso a opção seja desmarcada, o campo de entrada manual de matrícula é habilitado.
  - Usuários criados através desse fluxo são vinculados à matrícula como membros (`isOwner = false`).
- **Participação na Equipe do Gestor**:
  - Caso o gestor pertença a uma equipe ativa (`currentTeamName`), a opção `"Participar da equipe [Nome da Equipe]"` é exibida e marcada por padrão (`checked`).
  - Caso o gestor não possua equipe vinculada, o campo de equipe permanece oculto.
  - Ao salvar o cadastro, o novo usuário é inserido automaticamente como membro ativo da equipe informada (`UserTeam`).
- **Atualização Reativa**: Ao trocar o gestor selecionado no formulário, as matrículas próprias e a equipe são consultadas e atualizadas dinamicamente.

---

## Coluna "Usuário Ativo" nos Relatórios (Reports)

Esta funcionalidade adiciona a coluna de saída **"Usuário Ativo"** na seleção de campos e projeção de resultados dos relatórios (`Reports` / `ReportFilters`). O campo avalia se o vendedor/usuário responsável é considerado ativo de acordo com critérios temporais de acesso e criação de conta combinados com o status cadastral ativo.

### Core Objectives
- Disponibilizar a coluna **"Usuário Ativo"** sob a fonte de dados `Users_Contract` (e `Users_Matricula`) no modal de seleção de colunas do relatório.
- Projetar o valor booleano formatado em texto (`"Sim"` / `"Não"` / `"—"`) em tabelas de visualização, visualizações compartilhadas (`Views`) e exportações.

### Critérios de Avaliação do Usuário Ativo
O status ativo (`"Sim"`) é determinado quando todas as condições a seguir são atendidas simultaneamente (**AND**):
1. **Cadastro Ativo**: `User.IsActive == true`.
2. **Criação da Conta**: Conta criada há pelo menos 15 dias (`User.CreatedAt <= now - 15 dias`).
3. **Último Acesso**: Usuário acessou o sistema nos últimos 30 dias (`User.LastAccessedAt != null` e `User.LastAccessedAt >= now - 30 dias`).
4. **Sem Usuário Atribuído**: Caso o contrato não tenha vendedor/usuário associado (`User == null`), o valor retornado é `"—"`.
5. **Critérios Não Atendidos**: Caso o usuário exista mas qualquer um dos critérios acima não seja satisfeito (ex: sem login registrado, login há mais de 30 dias, conta com menos de 15 dias de criação ou desativada), retorna `"Não"`.

### Key Capabilities
- **Disponibilidade em Colunas**: Exposto em `GetAvailableColumns` na API e selecionável na interface de criação/edição de relatórios (`ReportFormPage.tsx`).
- **Resolução Determinística**: Função pura `ResolveUserActive(User? user, DateTime? referenceTime = null)` em `ReportFilterService` para projeção ágil durante a execução do relatório.

---

## High-Volume File Import Performance & Timeout Optimization (1.7MB+ / 15k+ Rows)

This feature optimizes the bulk import pipeline to reliably process large files (such as 1.7MB+ `contractDashboard` exports containing 15,000+ rows) without hitting proxy timeouts, memory bloat, or EF Core change tracking degradation.

### Core Objectives
- Prevent `"Failed to confirm import"` and `504 Gateway Timeout` errors when confirming high-volume dashboard and contract imports.
- Eliminate EF Core `ChangeTracker` graph accumulation across batch iterations by using `AsNoTracking()` on bulk lookups and explicitly clearing the tracker between 500-row chunks.
- Explicitly persist modified and restored entities via `_context.Contracts.UpdateRange()` and `_context.PendingContractClaims.Update()`.
- Replace global full-table pending claims scanning with targeted SQL-filtered queries matching the current batch's contract numbers.
- Increase Nginx reverse-proxy read/send timeouts to 300 seconds for `/api` endpoints across production, local, and E2E configurations.
- Provide interactive button loading spinner and informational progress banner during import confirmation so users know large files are actively processing.
- Provide descriptive, user-friendly frontend timeout notifications on gateway timeout responses (504/502).

---

## Atualizar Data do Contrato (`SaleStartDate`) no Import Upload (contractDashboard)

This feature introduces an **"Atualizar data do contrato"** option when importing sales via `contractDashboard` upload. Turned off by default (`false`), when enabled it allows existing contracts in the system to have their `SaleStartDate` updated to the value specified in the uploaded file.

### Core Objectives
- Provide an option `"Atualizar data do contrato"` (checkbox, default `false`) on the `contractDashboard` import modal.
- Allow updating `SaleStartDate` on existing contracts when turned on and a valid date is present in the row.
- Keep existing contract start dates untouched when the toggle is turned off (default behavior).
- Thread the option through DTOs (`ColumnMappingRequest`, `ConfirmImportRequest`), controller endpoints, service layer (`IImportExecutionService`, `ImportExecutionService`), and frontend UI (`BulkImportModal.tsx`, `apiService.ts`).

---

## Idempotent User Re-Import (`users.xlsx`) in Import Wizard

This feature ensures that re-importing `users.xlsx` via Step 2 of the Import Wizard (`/api/wizard/step2-import`) is fully idempotent and succeeds cleanly when executed multiple times.

### Core Objectives
- Prevent `UNIQUE constraint failed: Users.Email` database errors when re-importing existing users in `users.xlsx`.
- Safely update `ImportSession` records without re-marking attached EF Core navigation entity graphs (such as `UploadedBy`) as `Added`.
- Enable `UserRepository.GetByEmailAsync` to look up existing user records regardless of their `IsActive` state, preventing duplicate insert attempts.
- Automatically reactivate (`IsActive = true`) and update details when an inactive user is re-imported.

---

## Scrape Diagnostics & Auth Step Logging (PowerBI Extrações)

This feature provides step-by-step diagnostic logging and immediate authentication failure detection for PowerBI extractions, capturing detailed status reports and remaining attempt warnings.

### Core Objectives
- Detect authentication errors (`"Usuário ou senha inválida"`) and 403 Forbidden attempts warnings (`"Você ainda possui mais X tentativas..."`) immediately upon login form submission.
- Prevent waiting for full navigation timeouts when credentials are wrong.
- Record step-by-step diagnostic logs (`AuthSteps`), authentication status (`AuthStatus`), error messages (`AuthMessage`), and PowerBI report loading indicators (`PowerBiLoaded`) in DynamoDB per scrape job.
- Display diagnostic badges, PowerBI report indicators, and interactive step log modals in both the **Histórico de Extrações** detail view (`/#/scrapes/runs/:runId`) and the **Testar Autenticação** modal.

---

## User Last Access Tracking (`LastAccessedAt`)

This feature tracks when users last accessed the system (`LastAccessedAt`), throttled to once per 24 hours per user to prevent unnecessary database writes on every API request. The last access timestamp is exposed in API endpoints and displayed in the Users table.

### Core Objectives
- Track the exact date and time when a user accesses the application or API.
- Eliminate database write overhead on standard API requests by implementing a 24-hour in-memory sliding cache throttle (`ConcurrentDictionary<Guid, DateTime>`).
- Update `LastAccessedAt` immediately upon explicit login (`/api/auth/token`).
- Display user last access timestamps on the Users table (`/#/users`), with `"Never"` as the fallback for users who have not yet accessed the system.

### Key Capabilities
- **24-Hour Database Throttle**: Middleware checks in-memory timestamp cache on authenticated requests. If the user's last DB update was < 24 hours ago, DB writes are skipped entirely.
- **Background Asynchronous Updates via Root Scope Factory**: When the 24-hour threshold is exceeded, the update runs asynchronously in a non-blocking background task using the root-level `IServiceScopeFactory` to safely create a new `IServiceScope`, completely decoupled from the short-lived HTTP request lifecycle to eliminate `ObjectDisposedException`.
- **Login Instant Update**: Explicit user login via `/api/auth/token` updates `LastAccessedAt` instantly and updates the in-memory cache.
- **Users Table Display**: Adds an **"Último Acesso"** column to the Users page table, displaying formatted dates (`DD/MM/YYYY`) or `"Never"` if null.

## Reconciliação de Contratos (Ferramentas Admin)

This feature introduces a contract reconciliation audit tool under **"Ferramentas Admin"** (`/#/contract-reconciliation`), allowing administrators to upload an XLSX file of expected customer contracts and cross-reference them against system contracts for a selected date range and user.

### Core Objectives
- Resolve contract count and total amount discrepancies reported by users/customers.
- Allow admins to select a `startDate`, `endDate`, and optional target `userId`.
- Parse uploaded `.xlsx` or `.csv` files on-the-fly in memory without writing temp records to the database.
- Categorize contract mismatches into 4 distinct breakdown metrics with detailed expandable tables and CSV export.

### Key Capabilities
- **Cross-Reference Breakdown Categories**:
  1. **Ausentes no Sistema**: Contracts present in XLSX (for user) that do not exist in the database within the date range.
  2. **Ausentes no XLSX**: System contracts for user within the date range that are missing in the XLSX file.
  3. **Divergência de Valor**: Contracts present in both sources for user where `|SystemAmount - XlsxAmount| > 0.01`.
  4. **Sem Usuário Atribuído**: Contracts in XLSX where user identifier (email, matricula, CPF, or user ID) is missing or cannot be matched to any system user.
- **KPI Summary Cards**: 4 visual status cards with totals and amounts.
- **Interactive Data Table**: Search bar filter by contract number or user name + instant CSV export for each category.
- **Role-Based Access**: Restricted to superadmin access under **"Ferramentas Admin" -> "Reconciliação de Contratos"**.

## Teams Page State & Store Filters (Superadmin Only)

This feature adds **Estado** (State) and **Loja** (Store) `MultiSelect` filters on the `/#/teams` page, exclusively visible to **superadmins**.

### Core Objectives
- Allow superadmins to filter the Teams list by state and/or store.
- Apply **AND** logical combination when both State and Store filters are selected.
- Hide teams that have no store assigned whenever any State or Store filter is active.
- Restrict filter controls so they are visible strictly to superadmins (`currentUserRole === 'superadmin'`).

### Key Capabilities
- **State Filter**: Multi-select dropdown listing states associated with active stores.
- **Store Filter**: Multi-select dropdown listing all active stores.
- **AND Filter Combination**: Teams must match all active filter criteria.
- **Role-Based Visibility**: Only rendered for superadmins; standard admins and regular users see the normal search input.
- **Session Memory**: In-memory filter state (not persisted across page navigation).

## Team Creation & Admin Promotion via Requests (Solicitações)

This feature enables users to request team creation directly through the unified **Solicitações** system. Upon approval by a superadmin or parent admin (superior in hierarchy), the system automatically creates the new team, sets the requester as its owner (admin), and promotes the requester's role to `admin`.

### Core Objectives
- Allow users of any role to submit a team creation request using the prompt: `"Eu sou Guimel agora, quero criar minha equipe"`.
- Enforce team name uniqueness validation in Portuguese (`"Nome da equipe já existe"`) both at request submission and at approval execution.
- Enable superadmins and superior admins (parentAdmin in hierarchy) to approve or reject the request.
- Automatically execute team creation, owner assignment, team membership creation, and user role promotion to `Admin` (roleId = 2) upon approval.

### Key Capabilities
- **Request Type**: `CreateTeam` in `ApprovalRequestType`.
- **UI Option**: Select dropdown option labelled `"Eu sou Guimel agora, quero criar minha equipe"` visible across user roles.
- **Immediate Validation**: Checks for existing team names in Portuguese during submission to prevent duplicate requests.
- **One-Step Execution**: On approval by superadmin or parent admin, creates `Team`, links `UserTeam`, and updates user's `RoleId` to `Admin`.

## Exported Fields in Reports (Interactive Viewer Filters)

This feature allows report administrators to "export" filter fields (`Teams` and `Emails`) on saved reports, making them interactive for any viewer on the report results page without requiring multiple separate reports.

### Core Objectives
- Allow admins to mark `Teams` and/or `Emails` as exported fields when configuring a report filter, with optional custom labels (e.g. "Selecione a Equipe").
- Enable any user with view access to dynamically select different teams or sellers directly at the top of the report results view.
- Support local storage persistence of viewer filter choices so user selections remain intact across page refreshes.

### Key Capabilities
- **Admin Configuration**: Toggle exported status for `Teams` and `Emails` inside `ReportFormPage`, defining custom display header labels.
- **On-The-Fly Overrides**: Interactively override filter values via inline `MultiSelect` components on `ReportResultsPage`.
- **In-Memory Query Execution**: Server-side filter overrides replace the base filter at query time without altering the saved report definition in DynamoDB.
- **State Persistence**: Viewer selection choices are stored in `localStorage` under `report-exports-{filterId}`.
- **Loading & Refetch Experience**: Skeleton loading states while option lists fetch, and overlay spinners over report tables/charts when applying updated filters.

## Inactive User Management & Status Filter

This feature ensures inactive users are strictly excluded from display and counting across all application modules (Teams, Classifications, Contracts, Matriculas), manages user active state during contract lifecycle changes, and adds a status dropdown filter on the Users management page.

### Core Objectives
- Filter out inactive users (`IsActive == false`) from team members, team counts, classification levels, matriculas, and contract seller selection dropdowns.
- Provide a status dropdown filter on the Users page (**Ativos** [Default], **Inativos**, **Todos**) with dynamic header counter updates.
- Automatically end team and classification memberships when a user is disabled.
- Prevent user disabling if the user is the owner of an active matricula with the notice: `"Por favor, defina a matrícula para outro proprietário."`

### Key Capabilities
- **Users Page Dropdown**: Filter users table by Active, Inactive, or All, updating the pagination and dynamic header count.
- **Contract Lifecycle Synchronization**: Automatically manage `IsActive` state when contracts are assigned or unassigned/migrated.
- **Matricula Ownership Protection**: Require reassigning matricula ownership before disabling a user.
- **Automatic Membership Cleanup**: Set `EndDate = DateTime.UtcNow` on active `UserTeams` and `UserClassifications` upon user inactivation.

## Contract Dashboard Cota Field Extraction & Upsert

This feature ensures that compound semicolon-separated `Cota` strings (e.g. `G1;300;X;Customer;1100826650`) imported via the `contractDashboard` template are accurately parsed and preserved across both initial contract creation and subsequent contract updates (upserts).

### Core Objectives
- Automatically decompose compound `Cota` strings into virtual fields (`cota.group`, `cota.cota`, `cota.customer`, `cota.contract`).
- Populate the `Quota` (`Cota`) integer field on new and existing contracts when re-imported via `contractDashboard`.

### Key Capabilities
- **Decomposition**: Parses `parts[1]` from the compound `Cota` column as the numerical quota value.
- **Upsert Persistence**: Ensures `contract.Quota` is updated when existing contracts receive recurring dashboard updates.

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
- **Grouped Extraction History by Run**: Extractions are grouped by unique `RunId` execution GUIDs. The history UI presents a summarized list of runs ordered newest-first, showing executor email, matricula, final status summary, and total records. Clicking a run opens a dedicated detail page with filtering capabilities (user, matricula, store, final status). Legacy un-grouped records without a `RunId` are filtered out of the view.
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

This feature enforces user scoping, seller unassignment, and active matricula checks during contract creation and editing.

### Key Capabilities
- **Hierarchical Vendor Scoping**: When an Admin accesses the contract form, the "Vendedor" (Seller) dropdown is restricted to only their descendant users.
- **Seller Unassignment / Clearing**: When editing a contract in `/#/contracts` and selecting "Sem vendedor atribuído" (or clearing the Vendedor field), `PUT /api/contracts/{id}` receives `userId: null` and `matriculaNumber: null`. The backend explicitly clears the seller (`UserInternalId`, `User`) and its associated matricula (`MatriculaId`, `TempMatricula`, `Matricula`), successfully leaving the contract unassigned.
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
2. **User Request Matricula (`RequestMatricula`)**: Standard users or managers request using their manager's matricula number (`"Solicitar o uso da matrícula do gestor"`).
3. **Admin Request Matricula (`AdminRequestMatricula`)**: Users or Admins request creation/assignment of a matricula that they will own (`IsOwner = true`). SuperAdmins, parent Admins in the user's hierarchy, and existing matricula owners can approve this request. Approving automatically transfers ownership by setting `IsOwner = false` on any previous owner.
4. **Request Admin Role (`RequestAdminRole`)**: Non-admin users request promotion to the Admin role (`"Solicitação de Perfil Administrador (Role Admin)"`). SuperAdmins and parent Admins can approve this request.
5. **Request Classification Level (`RequestClassificationLevel`)**: `user` and `admin` roles request assignment to a classification level (`"Solicitação de Nível de Classificação"`). Requires a mandatory start date and auto-closes any previous active level on approval.

### Approver Actions
- **Sim / Aprovar (Yes)**: One-step immediate approval. Executes the underlying model update on the server (e.g. re-parenting user, creating/linking matricula, updating RoleId to Admin, assigning classification level, transferring matricula ownership) and marks status as `Approved`.
- **Não / Rejeitar (No)**: Rejects the request, recording an optional rejection reason comment for the requester to view.
- **Depois (Later)**: Postpones decision, leaving the request in `Pending` status to be revisited later.

### Access Control and Scoping
- **SuperAdmin**: Sees all system pending requests and can approve/reject any request type.
- **ChangeParentEmail**: If the target `parentEmail` is an Admin, **only** that designated `parentEmail` user and SuperAdmins can see, approve, or reject the request.
- **RequestMatricula & AdminRequestMatricula**: SuperAdmins, the user's parent Admin(s) in the hierarchy, and current matricula owners can see, approve, or reject the request.
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

## Consolidar Usuários Duplicados (Batch User Merge) (2026-07-31)

Adds a new "Consolidar Usuários" tab to the `/#/batch` page for SuperAdmin users to consolidate duplicated user accounts into a single main account.

### Key Capabilities
- **Bulk Pair Format Support**: Accepts multiple email pairs per line separated by comma (`email1,email2`), space (`email1 email2`), or tab (`email1\temail2`), where `email1` is the main/survivor user and `email2` is the duplicate user.
- **Complete Relationship Migration**: Re-links all contracts (`UserInternalId`), matricula relationships (`UserMatriculas`), child hierarchy users (`ParentUserId`), and team memberships (`UserTeams`) from `email2` to `email1`.
- **Matricula Ownership Preservation**: Retains contract-to-matricula references. If `email1` does not have a link to a matricula owned/used by `email2`, the link is transferred; if `email1` already links to the matricula, ownership (`IsOwner`) is transferred if applicable and duplicate links are cleaned up.
- **Parametrized Duplicate Deactivation**: Provides a `Desativar usuário duplicado (email2) ao concluir?` toggle (disabled by default) to optionally set `IsActive = false` on `email2`.
- **Dry-Run Preview**: Offers a "Pré-visualizar Consolidação" step before committing changes to inspect item counts and validate emails.

### Key Files Created / Modified
- `SalesApp.Api/DTOs/BatchDTOs.cs` — Added `MergeUserPair`, `MergeUsersRequest`, `MergeUserPairResult`, `MergeUsersResult`.
- `SalesApp.Api/Controllers/BatchController.cs` — Added `POST /api/batch/users/merge` endpoint.
- `client/sales-dash/src/services/apiService.ts` — Added `batchMergeUsers` API client method & interfaces.
- `client/sales-dash/src/components/BatchPage.tsx` — Added third tab "Consolidar Usuários" with pair parser, toggle, preview table, and confirmation logic.
- `client/e2e-test/e2e/batch_merge_users.spec.ts` — Added Playwright E2E test spec for batch user merge.

## Consolidar Matrículas Duplicadas (Batch Matricula Merge) (2026-07-31)

Adds a new 4th tab "Consolidar Matrículas" to the `/#/batch` page for SuperAdmin users to consolidate duplicated matricula numbers (e.g. `02123` and `2123` or `MAT-001` and `MAT-002`) into a single main matricula.

### Key Capabilities
- **Bulk Pair Format Support**: Accepts multiple matricula pairs per line separated by comma (`mat1,mat2`), space (`mat1 mat2`), or tab (`mat1\tmat2`), where `mat1` is the main/survivor matricula and `mat2` is the duplicate matricula.
- **Link & Contract Migration**: Re-links all `UserMatricula` records and `Contract.MatriculaId` foreign key references from `mat2` to `mat1`.
- **Ownership Preservation**: Preserves `IsOwner = true` status on `UserMatriculas` links. If either `mat1` or `mat2` has ownership for a user, the merged link for `mat1` retains `IsOwner = true`.
- **Parametrized Duplicate Row Deletion**: Provides an `Excluir matrícula duplicada (mat2) ao concluir?` toggle (disabled by default). When enabled, deletes the duplicate `Matricula` record from the database after verifying zero remaining references.
- **Dry-Run Preview**: Offers a "Pré-visualizar Consolidação" simulation step to inspect user link and contract counts before executing changes.

### Key Files Created / Modified
- `SalesApp.Api/DTOs/BatchDTOs.cs` — Added `MergeMatriculaPair`, `MergeMatriculasRequest`, `MergeMatriculaPairResult`, `MergeMatriculasResult`.
- `SalesApp.Api/Controllers/BatchController.cs` — Added `POST /api/batch/matriculas/merge` endpoint.
- `client/sales-dash/src/services/apiService.ts` — Added `batchMergeMatriculas` API client method & interfaces.
- `client/sales-dash/src/components/BatchPage.tsx` — Added 4th tab "Consolidar Matrículas" with pair parser, deletion toggle, preview table, and execution confirmation logic.
- `SalesApp.IntegrationTests/Users/BatchControllerIntegrationTests.cs` — Added integration tests for matricula merge authorization, dry-run, execution, ownership preservation, and deletion.
- `client/e2e-test/e2e/batch_merge_matriculas.spec.ts` — Added Playwright E2E test spec.

## Coluna de Última Atualização na Página de Matrículas (2026-08-03)

Exibe a informação da última atualização da matrícula (data/hora e tempo relativo) em uma nova coluna na tabela da página de Matrículas (`/#/matriculas`), com tooltip explicativo visível para todos os usuários.

### Key Capabilities
- **Última Atualização Embutida no Backend**: O DTO `UserMatriculaResponse` inclui o campo `LastUpdate`, calculado via consulta agregada `MAX(Contract.UpdatedAt)` sobre contratos ativos (excluindo o status "desistente"), reutilizando a mesma lógica da tela de Monitoramento.
- **Visualização Completa para Todos os Usuários**: A coluna é exibida para todos os papéis de usuário (`User`, `Admin`, `SuperAdmin`).
- **Tooltip Informativo no Cabeçalho**: Apresenta o texto de auxílio ao passar o cursor sobre o cabeçalho da coluna: *"Esta é a última vez que esta matrícula foi atualizada"*.
- **Formatação Dupla e Fallback**: Mostra a data/hora exata (`DD/MM/YYYY HH:mm`) na primeira linha e a idade relativa (ex.: *"há 2 horas"*) na segunda linha em português (`pt-br`). Exibe `"Nunca"` para matrículas sem histórico de contratos.

### Key Files Created / Modified
- `SalesApp.Api/DTOs/UserMatriculaResponse.cs` — Adicionado o campo `public DateTime? LastUpdate`.
- `SalesApp.Api/Repositories/IUserMatriculaRepository.cs` — Declarado o método `GetLastUpdateByMatriculaNumberAsync()`.
- `SalesApp.Api/Repositories/UserMatriculaRepository.cs` — Implementada a busca agregada de `MAX(UpdatedAt)` por número de matrícula em `Contracts`.
- `SalesApp.Api/Controllers/UserMatriculasController.cs` — Atualizados os endpoints GET (`GetAll`, `GetById`, `GetByUserId`) para preencher `LastUpdate` na resposta.
- `client/sales-dash/src/services/apiService.ts` — Adicionada a propriedade `lastUpdate?: string` na interface `UserMatricula`.
- `client/sales-dash/src/components/MatriculasPage.tsx` — Adicionada a coluna "Última Atualização" com Tooltip Mantine, formatação `dayjs` (`pt-br`) e tratamento do estado vazio `"Nunca"`.

## Importação de Contratos DESISTENTE no Template ContractDashboard (2026-08-03)

Permite a importação de contratos com status "DESISTENTE" quando utilizado o template de importação `contractDashboard`, mantendo esses contratos ocultos por padrão em todas as listas/consultas do sistema, permitindo que SuperAdmins os visualizem através do filtro de status de contratos.

### Key Capabilities
- **Enum Status `Desistente`**: Adicionado o valor `Desistente` ao enum `ContractStatus` e atualizado o mapeamento canônico no `appsettings.json`.
- **Importação Direta sem Warning**: Removida a trava/descarte de linhas com status "DESISTENTE" no loop de importação do `contractDashboard` (`ImportExecutionService.cs` e `WizardService.cs`). O alerta de contratos descartados é ocultado ao utilizar o template.
- **Ocultação Padrão e Filtro de SuperAdmin**: Contratos desistentes continuam ocultos em todas as consultas, estatísticas e listagens gerais do sistema. SuperAdmins têm permissão para selecionar o status "Desistente" no filtro de contratos da página `/#/contracts` para visualizá-los.
- **Badge de Status**: Componente `ContractStatusBadge.tsx` atualizado com o rótulo e estilo visual para o status `Desistente`.

### Key Files Created / Modified
- `SalesApp.Api/Models/ContractStatus.cs` — Adicionado o valor `Desistente` ao enum `ContractStatus` e ao parser `FromApiString`.
- `SalesApp.Api/Attributes/ValidContractStatusAttribute.cs` — Incluído `Desistente` nos status válidos da API.
- `SalesApp.Api/appsettings.json` — Criado mapeamento canônico `"Desistente": ["Desistente", "DESISTENTE"]`.
- `SalesApp.Api/Services/ImportExecutionService.cs` — Removido o skip block de DESISTENTE no loop do `contractDashboard`.
- `SalesApp.Api/Services/WizardService.cs` — Mapeado "DESISTENTE" para "Desistente" e removida sinalização de warning no pré-validador.
- `SalesApp.Api/Repositories/IContractRepository.cs` e `ContractRepository.cs` — Adicionados parâmetros `statuses` e `isSuperAdmin` permitindo a SuperAdmin consultar desistentes ao filtrar explicitamente.
- `SalesApp.Api/Controllers/ContractsController.cs` — Adicionado suporte ao parâmetro de consulta `statuses` e verificação de permissão de SuperAdmin.
- `client/sales-dash/src/shared/ContractStatusBadge.tsx` — Adicionada cor, rótulo e opção de status para `Desistente`.
- `client/sales-dash/src/services/contractService.ts` — Adicionado o parâmetro `statuses` na função `getContracts`.
- `client/sales-dash/src/components/ContractsPage.tsx` — Adicionado filtro de status na barra de filtros da página de contratos, restringindo a opção "Desistente" para SuperAdmins.

## Playwright Shared Authentication (storageState) (2026-08-05)

Acelera a execução dos testes E2E Playwright via reutilização de sessões autenticadas (`storageState`), eliminando o login repetitivo via formulário UI em cada spec.

### Key Capabilities
- **Global Setup (`global-setup.ts`)**: Executa uma única vez antes de toda a suíte E2E, realizando o login dos usuários padrão (`superadmin@salesapp.com` e `admin@salesapp.com`) e armazenando os tokens/cookies em arquivos de estado JSON (`.auth/superadmin.json`, `.auth/admin.json`).
- **Playwright Config (`playwright.config.ts`)**: Configura `globalSetup` e aplica `storageState: '.auth/superadmin.json'` padrão para os projetos (`tear-1`, `tear-2`, `tear-3`).
- **Helper Reutilizável (`auth.ts`)**: Centraliza rotinas de login e credenciais padrão para testes com logins dinâmicos.
- **Spec Direct Navigation**: Permite que as specs naveguem diretamente para suas telas alvo (`await page.goto('/#/import-wizard')`) sem preencher o formulário de login repetidamente.
- **Exceção para `login.spec.ts`**: Mantém o contexto limpo sem `storageState` para validar explicitamente a interface e o fluxo do formulário de login.

## Playwright Sub-Tear Parallel Architecture (2026-08-06)

Otimização de paralelismo horizontal para a suíte E2E do Playwright, dividindo os gargalos `tear-2` (20 specs) e `tear-3` (31 specs) em sub-tears paralelos (`tear-2a-import`, `tear-2b-roles`, `tear-3a-hierarchy`, `tear-3b-admin`) executadas simultaneamente.

### Key Capabilities
- **Sub-Tear Splitting (`playwright.config.ts`)**:
  - `tear-2a-import` & `tear-2b-roles` dependem unicamente de `tear-1-setup-and-import` e executam em paralelo.
  - `tear-3a-hierarchy` & `tear-3b-admin` dependem dos dois sub-tears do tear-2 e executam em paralelo após a conclusão do tear-2.
- **Worker Allocation**: Aumentado o número de workers simultâneos de 2 para 4.
- **Data Isolation**: Specs agrupadas por afinidade de dados para prevenir colisões de chaves de banco ou usuários concorrentes.

## Integration Tests Parallel Collections (2026-08-06)

Otimização de paralelismo horizontal para a suíte de testes de integração .NET, dividindo a collection única sequencial `Integration Tests` em 4 collections isoladas por domínio com bancos SQLite dedicados.

### Key Capabilities
- **Collection Splitting (`IntegrationTestsCollection.cs`)**:
  - `Contracts Tests`: Testes da pasta `Contracts/` e `DatabaseSeedingTests.cs` (banco `SalesApp.Contracts.Tests.db`).
  - `Imports Tests`: Testes da pasta `Imports/` (banco `SalesApp.Imports.Tests.db`).
  - `Users Tests`: Testes da pasta `Users/` (banco `SalesApp.Users.Tests.db`).
  - `Misc Tests`: Testes de `Classifications`, `UserMatriculas`, `Roles`, `PointOfSale`, `ReportFilters`, etc. (banco `SalesApp.Misc.Tests.db`).
- **Parallel Assembly Execution (`AssemblyInfo.cs`)**:
  - Removido `DisableTestParallelization = true` e ativado `[assembly: CollectionBehavior(MaxParallelThreads = 4)]`.
- **Isolation Guarantee**: Cada collection possui uma instância isolada de `TestWebApplicationFactory` com seu próprio arquivo de banco SQLite, prevenindo conflitos de escrita e travamento de DB.

## Contract Dashboard Import - Unmapped Columns Warning Suppression (2026-08-07)

Remoção do aviso `"Colunas não mapeadas detectadas na origem: ..."` especificamente durante a importação em lote do Dashboard de Contratos (`contractDashboard`).

### Key Capabilities
- **Selective Suppression (`ImportExecutionService.cs`)**: O aviso de colunas não mapeadas é omitido de `result.Warnings` apenas no fluxo do `ExecuteContractDashboardImportAsync`.
- **Warning Retention**: Outros avisos durante a importação do dashboard (ex: mapeamento de status, matrículas e valor total ausente) permanecem ativos, assim como os avisos de colunas não mapeadas em importações padrão de contratos (`ExecuteContractImportAsync`) e usuários (`ExecuteUserImportAsync`).

## Contracts Page - Data da Última Atualização Column (2026-08-06)

Adicionada a coluna "Data da última atualização" na página de gerenciamento de contratos (`ContractsPage`), permitindo aos usuários visualizar quando um contrato foi atualizado.

### Key Capabilities
- **Togglable Column**: Oculta por padrão (`lastUpdated: false`), podendo ser ativada/desativada no modal "Colunas".
- **Data Source**: Utiliza o campo `updatedAt` retornado pela API na resposta de contratos.

## Store Entity & Team-Store Relationship (2026-08-07)

Criação da entidade **Store** (Loja) com CRUD completo, tela de gerenciamento (`/#/stores`) nos moldes da página de Matrículas, e associação de cada Equipe a uma Loja com restrição de permissão de edição por perfil.

### Key Capabilities
- **Entidade Store**: Atributos `Name` (único, max 200), `State` (sigla de 2 caracteres dos estados do Brasil, ex.: `PR`, `SC`, `SP`), `IsActive` (padrão true), `CreatedAt` e `UpdatedAt`.
- **Relacionamento Team-Store**: Cada equipe possui o campo opcional `StoreId`. Exclusão de uma loja desvincula (limpa `StoreId = null`) as equipes associadas sem excluí-las (`DeleteBehavior.SetNull`).
- **Controle de Acesso à Tela de Lojas**: A navegação e o gerenciamento da tela de Lojas (`/#/stores`) são restritos ao perfil `SuperAdmin` (permissão `system:superadmin`).
- **Endpoint Público de Seleção de Lojas**: Endpoint `GET /api/stores/all` acessível para todos os usuários autenticados para popular dropdowns de seleção de loja.
- **Permissão de Edição da Loja na Equipe**: A alteração do campo de Loja de uma Equipe na API (`PUT /api/teams/{id}`) e na interface é permitida **exclusivamente** para o perfil `SuperAdmin` ou para o **Admin proprietário (Owner)** daquela equipe. Para os demais usuários, o campo é exibido em modo leitura sem permissão de alteração.
- **Interface Visual**:
  - Tela `/#/stores` com busca em tempo real por nome/estado, filtro de status ativo/inativo, tabela informativa de criação/atualização e modal para criação/edição com seletor dos 27 estados do Brasil.
  - Coluna "Loja" na página de Equipes (`/#/teams`) e seletor no modal de gerenciamento de membros da equipe.

### Key Files Created / Modified
- `SalesApp.Api/Models/Store.cs` — Classe do modelo de entidade de banco de dados `Store`.
- `SalesApp.Api/Models/StoreConstants.cs` — Renomeada a classe estática legada para `StoreConstants`.
- `SalesApp.Api/Models/Team.cs` — Adicionado `StoreId` e a propriedade de navegação `Store`.
- `SalesApp.Api/Data/AppDbContext.cs` — Configurado o `DbSet<Store>`, a chave única de nome e o relacionamento `Team -> Store`.
- `SalesApp.Api/Migrations/20260807170000_AddStoresTable.cs` — Migração para criação da tabela `Stores` e chave estrangeira em `Teams`.
- `SalesApp.Api/DTOs/StoreDTOs.cs` — DTOs de criação, atualização e resposta de loja.
- `SalesApp.Api/Repositories/IStoreRepository.cs` e `StoreRepository.cs` — Repositório de dados para a entidade `Store`.
- `SalesApp.Api/Controllers/StoresController.cs` — Endpoints REST CRUD de lojas e endpoint público `/api/stores/all`.
- `SalesApp.Api/Controllers/TeamsController.cs` — Atualizado `UpdateTeam` e `MapToTeamResponse` para gerenciar permissões de edição da loja por perfil.
- `client/sales-dash/src/services/apiService.ts` — Métodos cliente e interfaces de API para `Store` e `Team`.
- `client/sales-dash/src/components/StoresPage.tsx` — Interface React completa para gerenciamento de lojas.
- `client/sales-dash/src/components/TeamsPage.tsx` — Exibição da coluna "Loja" na tabela de equipes.
- `client/sales-dash/src/components/TeamMembersModal.tsx` — Dropdown de seleção de loja na equipe com validação de perfil.
- `client/sales-dash/src/App.tsx` e `Menu.tsx` — Rota `/#/stores` e item no menu lateral para SuperAdmin.
- `SalesApp.IntegrationTests/Stores/StoresControllerIntegrationTests.cs` — Suíte de testes de integração backend.
- `client/e2e-test/e2e/stores_crud.spec.ts` — Especificação de testes E2E com Playwright.

## PowerBI Scraper - In-Memory Token Caching, Auto Re-Auth & Multi-Month Date Range Scraping (2026-08-17)

Provides in-memory token caching per matrícula, automatic re-authentication upon encountering HTTP 401/402/403 token expiration (with up to 3 retries), and support for multi-month date range extractions (`SCRAPE_DATES="2026-02,2026-03,2026-04"`).

### Key Capabilities
- **In-Memory Token Cache (`tokenManager.js`)**: Caches Avapro Bearer JWT (`avaJwt`) and PowerBI query tokens (`MWCToken`) in memory per matrícula. Subsequent extractions for the same matrícula reuse the cached token, eliminating Puppeteer browser launch overhead (~0s auth time, ~4s query execution).
- **Automatic Re-Authentication on Expiration (`extractor.js`)**: If PowerBI returns HTTP 401, 402, or 403 (token expired/invalid) during query execution, the system automatically invalidates the cached token, launches Puppeteer once to obtain fresh tokens, updates the cache, and seamlessly retries the query (up to 3 max retries).
- **Immediate Failure on Bad Credentials**: Clear authentication failures (`wrong-password` or invalid credentials) bypass retry loops and fail immediately to prevent account lockouts.
- **Multi-Month Date Range Scraping**: Supports array or comma-separated date range inputs (`SCRAPE_DATES="2026-02,2026-03,2026-04"`). Month 1 authenticates and caches tokens; Month 2 and Month 3 reuse the cached token in sequence, returning combined results and CSVs.

### Key Files Created / Modified
- `pbi-scraper/tokenManager.js` — In-memory token cache store (`Map<matricula, { avaJwt, pbiToken }>` with get/set/invalidate/clear helpers).
- `pbi-scraper/auth.js` — Integrated `tokenManager` and added `getOrFetchTokens` wrapper to return cached tokens or perform Puppeteer login on cache miss/force refresh.
- `pbi-scraper/extractor.js` — Implemented `isAuthErrorStatus` detection and `scrapeWithReauth` retry loop (up to 3 retries on 401/402/403).
- `pbi-scraper/server.js` — Added multi-month date range parser (`normalizeScrapeDates`), updating `/jobs` to process batch ranges reusing cached tokens.
- `pbi-scraper/scratch/test-range.js` — CLI test script for verifying multi-month extraction speed and auto-reauth behavior (`SCRAPE_DATES="2026-02,2026-03,2026-04"`).

## PowerBI Scraper - Auto-Detect Store (Loja) from AVA PRO (2026-08-17)

Automates store (Unidade / Loja) detection directly from the AVA PRO portal header after login, eliminating store filter mismatch errors in PowerBI DAX queries.

### Key Capabilities
- **Configurable Max Months Ago (`appsettings.json` & `ScrapeController.cs`)**: Added `PbiScraper:MaxMonthsAgo` setting (default `15`). Clamps historical scrape start dates to a maximum of 15 months ago (e.g., 20 months ago clamps to 15 months ago, while 4 months ago stays 4 months ago).
- **Automated Store DOM Extraction (`auth.js`)**: Upon login, Puppeteer inspects `[data-testid="select_loja"] [data-slot="value"]` (or header fallbacks) to capture the exact store name string (e.g. `BALNEARIO CAMBORIU - SC`).
- **DAX Query Filter Override (`extractor.js`)**: Automatically overrides DAX query filters (`nm_unidade_bi_original`) with the captured store name to guarantee 100% query execution accuracy.
- **Contract Status Column Fix (`extractor.js` & `appsettings.json`)**: Replaced non-existent `nm_situacao_cobranca` property with the dataset property `status_cota` in DAX payload generation (`buildPayload2`), mapping `Status Cota` / `status_cota` / `Situação cobrança` aliases to `Status` in `ScrapeImportMappings`.
- **Conditional SQLite Auto-Update (`ScrapeController.cs` & `ScrapeOrchestrator.cs`)**: Auto-populates `ScrapeConfig.Store` in SQLite only when the database field is currently null or empty.
- **Optional Store Selection UI (`ScrapeDashboard.tsx`)**: Unidade (Store) field in account setup is optional and defaults to `"Tentar selecionar automaticamente"`.

### Key Files Modified
- `pbi-scraper/auth.js` — Added DOM extraction for store from `[data-testid="select_loja"] [data-slot="value"]` after login.
- `pbi-scraper/tokenManager.js` — Cached `detectedStore` along with JWT & PBI tokens per matricula.
- `pbi-scraper/extractor.js` & `pbi-scraper/server.js` — Overrode DAX queries with `detectedStore` and returned it in callbacks and test-auth APIs.
- `SalesApp.Api/Models/ScrapeConfig.cs` — Made `Store` property nullable.
- `SalesApp.Api/Controllers/ScrapeController.cs` & `ScrapeOrchestrator.cs` — Handled optional store requests and persisted `detectedStore` to DB when current `Store` is null.
- `client/sales-dash/src/components/Scrape/ScrapeDashboard.tsx` — Added `"Tentar selecionar automaticamente"` as default store option.

## Dashboard Import - Upsert Robustness & Contract Number Normalization (2026-08-24)

Fixes `SQLite Error 19: UNIQUE constraint failed: Contracts.ContractNumber` during contract dashboard imports (`contractDashboard`) by unifying contract number extraction, normalizing leading zeros at write and lookup times, making in-memory lookups case/format insensitive, and clearing EF Core change trackers on failure.

### Key Capabilities
- **Contract Number Normalization at Storage Time (`ContractRepository.cs`)**: `CreateAsync` and `CreateBatchAsync` strip leading zeros and whitespace using `NormalizationUtils.NormalizeNumber` before inserting new contracts into SQLite.
- **Unified Contract Resolution (`ImportExecutionService.cs`)**: Extracted canonical `ResolveContractNumber` helper applied equally across pre-fetch bulk query, duplicate checking, and row processing.
- **Case-Insensitive Normalized In-Memory Map (`ImportExecutionService.cs`)**: Pre-fetched existing contracts dictionary (`existingMap`) uses `NormalizeNumber` keys and `StringComparer.OrdinalIgnoreCase` to prevent false misses during upsert checks.
- **In-Batch Duplicate De-duplication (`ImportExecutionService.cs`)**: Multiple rows referencing the same contract number in the same batch or across chunks are merged and updated in memory rather than causing duplicate batch insert attempts.
- **Pre-Insert Database Existence Guard (`ImportExecutionService.cs`)**: Re-checks SQLite database before calling `CreateBatchAsync` to filter out any contracts committed concurrently or in prior chunks.
- **Soft-Deleted Contract Full Restoration (`ImportExecutionService.cs`)**: When importing a contract that was soft-deleted (`IsActive == false`), reactivates the contract (`IsActive = true`) and updates all fields (`TotalAmount`, `SaleStartDate`, `CustomerName`, `GroupId`, `PvId`, `Quota`, `Version`, `MatriculaId`, `TempMatricula`, `Metadata`, etc.) with incoming data, identical to an insert update.
- **ChangeTracker Cleanup on Failure (`ImportsController.cs`)**: Clears EF Core `ChangeTracker` before executing the session status update in `ConfirmImportInternal` exception handler to avoid re-triggering constraint violations.

### Key Files Modified
- `SalesApp.Api/Repositories/ContractRepository.cs` — Normalized `ContractNumber` in `CreateAsync` and `CreateBatchAsync`.
- `SalesApp.Api/Services/ImportExecutionService.cs` — Added `ResolveContractNumber`, updated `existingMap` dictionary keys with case-insensitivity, added duplicate guards, and implemented full field updates for restored soft-deleted contracts.
- `SalesApp.Api/Controllers/ImportsController.cs` — Added `_context.ChangeTracker.Clear()` before updating session status on failure.
- `client/e2e-test/e2e/import_dashboard_upsert_robustness.spec.ts` — Comprehensive E2E test covering re-import updates, leading-zero normalization duplicates, and compound cota upserts.
- `client/e2e-test/playwright.config.ts` — Registered new E2E test under `tear-2b-roles`.







