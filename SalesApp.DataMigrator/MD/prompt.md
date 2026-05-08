## step 1

You are a senior .NET developer. I have a .NET application using SQLite and EF Core.

I need you to create an EF Core migration for a schema EXPANSION ONLY.
Do not remove or modify any existing columns. Only add new structures.

My current schema:
- Users table: has a `Guid` column (used as the primary key and for external system relations). No integer PK exists.
- Contracts table: has a `UserGuid` column (FK to Users.Guid) and a `Status` column (string).

What I need added:
1. Users table: add a new `Id` column (INTEGER PRIMARY KEY AUTOINCREMENT). Keep `Guid` intact.
2. Create a new `ContractStatus` table with: `Id` (INTEGER PRIMARY KEY AUTOINCREMENT), `Name` (TEXT NOT NULL UNIQUE).
3. Contracts table: add a new `ContractStatusId` column (int, nullable for now). Add a new `UserId` column (int, nullable for now). Keep old columns.

Deliverables:
- The EF Core Migration class (Up and Down methods)
- Any changes needed to the DbContext or model classes
- A note if SQLite requires table recreation instead of ALTER TABLE

Do not migrate any data. Schema changes only.

## step 2

You are a senior .NET developer. I have a .NET solution using SQLite.
I need you to create a standalone C# Console project that migrates data after a schema expansion.

Context:
- Users table has both `Guid` (old PK) and `Id` (new INTEGER AUTOINCREMENT, already added by EF migration)
- Contracts table has: old `UserGuid` (string FK), old `Status` (string), new `UserId` (int, nullable), new `ContractStatusId` (int, nullable)
- ContractStatus table exists and is empty: columns are `Id` and `Name`

The utility must run in 3 steps inside a single transaction:
1. Read DISTINCT Status values from Contracts, insert them into ContractStatus, keep a Dictionary<string, int> mapping in memory
2. Update Contracts.ContractStatusId based on the old Status string using that dictionary
3. Update Contracts.UserId by joining Contracts.UserGuid to Users.Guid and mapping to Users.Id

Additional requirements:
- Support a --dry-run flag: executes all logic, logs what would change, but rolls back the transaction
- Log row counts before and after each step
- Use Dapper for queries (no EF Core)
- Accept the SQLite connection string as a command line parameter
- At the end, print a validation summary: count of Contracts where UserId IS NULL, count where ContractStatusId IS NULL (both should be 0)
- If validation fails, rollback and exit with a non-zero code

Deliverables:
- The full console project code (Program.cs and any helper classes)
- The .csproj with correct Dapper and SQLite dependencies

```bash
docker run --rm \
    -v "$(pwd)/SalesApp.DataMigrator:/src" \
    -v "$(pwd)/SalesApp.Api:/db" \
    -w /src \
    mcr.microsoft.com/dotnet/sdk:9.0 \
    dotnet run -- "Data Source=/db/SalesApp.db" --dry-run
```

Checks if data were migrated successfully:
```bash
docker run --rm \
  -v "$(pwd)/SalesApp.Api:/db" \
  alpine sh -c "apk add --no-cache sqlite && sqlite3 /db/SalesApp.db 'SELECT COUNT(*) as Total, COUNT(ContractStatusId) as Populated, COUNT(*) - COUNT(ContractStatusId) as Missing FROM Contracts;'"
```

## step 3

You are a senior .NET developer. I have a .NET application using SQLite and EF Core.
The data migration has been completed and validated. I now need a cleanup migration AND a full
refactor of the EF Core layer to reflect the new schema.

Current state of the schema:
- Contracts table: has old `Status` (string, still present), new `ContractStatusId` (int, currently nullable but fully populated)
- ContractStatuses table: fully populated with all distinct status values

─────────────────────────────────────────
PART 1 — Schema Cleanup Migration
─────────────────────────────────────────
What I need:
1. Contracts table: drop the old `Status` string column, make `ContractStatusId` NOT NULL,
   add a proper FK constraint to ContractStatuses.Id

Important rules:
- Add a guard at the top of the migration Up() method: query COUNT(*) FROM Contracts
  WHERE ContractStatusId IS NULL — if result > 0, throw an exception and abort the migration
- SQLite does not support DROP COLUMN or ADD CONSTRAINT directly — handle table recreation where needed
- Provide the Down() method to reverse the cleanup if needed

Deliverables for Part 1:
- The EF Core Migration class (Up and Down)
- A comment explaining what SQLite limitations were worked around and how

─────────────────────────────────────────
PART 2 — EF Core Model and DbContext Refactor
─────────────────────────────────────────
Update the model classes and DbContext to reflect the final schema:
1. Create a `ContractStatus` entity class with `Id` and `Name` properties
2. Update the `Contract` entity class:
   - Remove the old `Status` string property
   - Add `ContractStatusId` (int) property
   - Add a `ContractStatus` navigation property
3. Update DbContext:
   - Add `DbSet<ContractStatus> ContractStatuses`
   - Configure the FK relationship between Contract and ContractStatus using Fluent API
   - Ensure `ContractStatusId` is required (NOT NULL)

─────────────────────────────────────────
PART 3 — Query Refactor
─────────────────────────────────────────
Find and update ALL queries and usages across the application that previously referenced
the old `Status` string column on Contracts. Replace them with the new relationship.

Examples of what needs to change:

// OLD
var contracts = await _context.Contracts
    .Where(c => c.Status == "Active")
    .ToListAsync();

// NEW
var contracts = await _context.Contracts
    .Where(c => c.ContractStatus.Name == "Active")
    .ToListAsync();

// OLD
var grouped = await _context.Contracts
    .GroupBy(c => c.Status)
    .ToListAsync();

// NEW
var grouped = await _context.Contracts
    .Include(c => c.ContractStatus)
    .GroupBy(c => c.ContractStatus.Name)
    .ToListAsync();

Rules for the query refactor:
- Always use the navigation property `ContractStatus.Name` instead of the old `Status` string
- Add `.Include(c => c.ContractStatus)` wherever the status is read or displayed
- If the codebase filters by status value, keep the filter logic but point it to `ContractStatus.Name`
- If the codebase sets the status, it must now look up or receive a `ContractStatusId` integer
  instead of assigning a raw string — show an example of how to resolve a status name to its Id
- Flag any place where a raw status string is being assigned (e.g. contract.Status = "Active")
  and show the corrected version using ContractStatusId

Deliverables for Part 3:
- Refactored versions of all queries found in the codebase that touched the old Status field
- An example helper method (e.g. GetStatusIdByNameAsync) to resolve a status name to its Id
  when creating or updating a Contract
- A note flagging any edge cases found (e.g. raw SQL queries, string interpolation, reports)