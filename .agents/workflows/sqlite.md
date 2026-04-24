---
description: sqlite migration
---

# SQLite Migration & Troubleshooting Guide

This guide documents known issues and best practices for managing Entity Framework Core migrations with SQLite in this project.

## 1. Limited `ALTER TABLE` Support
SQLite does not support many standard `ALTER TABLE` operations that other databases (like PostgreSQL or SQL Server) do. 

### Problematic Operations:
- Dropping a column
- Renaming a column
- Adding a Foreign Key to an existing table
- Adding a Unique Constraint to an existing table
- Changing a column's type or nullability

### Solution:
When making significant schema changes (like normalization or renaming), the most stable approach is to:
1. **Drop** the dependent tables.
2. **Recreate** them with the new schema.
3. This avoids the "rebuild table" logic that EF Core tries to perform, which often fails or causes deadlocks in SQLite.

---

## 2. Migration Locking & 502 Errors
If the application crashes during a migration (e.g., due to a Dependency Injection error or a SQL syntax error), it may leave a **Lock** on the SQLite database.

### Symptoms:
- Logs show a loop of `INSERT OR IGNORE INTO "__EFMigrationsLock"`.
- Backend fails to start, resulting in a `502 Bad Gateway`.
- Database file size doesn't change, but the app is unresponsive.

### Fix:
To clear a stuck migration lock, you must delete the database file and its temporary journal files:
```bash
rm SalesApp.db SalesApp.db-shm SalesApp.db-wal
```
Then restart the application. The seeder will recreate the initial data.

---

## 3. Filtered Indices
SQLite supports filtered indices (indices with a `WHERE` clause), but the syntax in EF Core must be compatible.

**Correct Syntax for SQLite:**
```csharp
modelBuilder.Entity<UserMatricula>()
    .HasIndex(e => e.MatriculaId)
    .IsUnique()
    .HasFilter("IsOwner = 1"); // Use SQLite compatible syntax (no square brackets)
```

---

## 4. WAL Mode (Write-Ahead Logging)
This project uses SQLite in WAL mode for better concurrency (`PRAGMA journal_mode=WAL`).

### Benefits:
- Allows multiple readers and one writer simultaneously.
- Significantly faster for most operations.

### Gotchas:
- It creates `-shm` and `-wal` files next to your `.db` file. **Never delete the `.db` file without also deleting these two**, or you may corrupt the database state.
- If you are manually copying the database, make sure to copy all three files.

---

## 5. Deployment Best Practices
- **Always backup** the `SalesApp.db` before applying migrations in production.
- If a migration fails in production, the safest path is often to roll back the code, fix the migration logic to be "SQLite-safe" (Drop/Create), and try again.
- For development, simply deleting the `.db` file is the fastest way to resolve schema inconsistencies.
