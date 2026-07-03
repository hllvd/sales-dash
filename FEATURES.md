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
- **Dynamic Visible Columns Selection**: Users can choose which columns are visible in the contracts table using a Mantine checkboxed modal. Visibility settings are saved to `localStorage`. Includes a single-click option to restore columns to their default layout.
- **Improved Empty State Messages**: Enhanced the empty state display on `MyContractsPage`. If no contracts are returned while filters (date/matricula) are active, it prompts the user with an improved Portuguese instruction: *"Nenhum contrato correspondente aos filtros aplicados foi encontrado. Você pode limpar os filtros para tentar novamente."* and a clear button to reset filters.


