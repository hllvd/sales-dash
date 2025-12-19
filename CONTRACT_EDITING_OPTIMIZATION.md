# Contract Editing API Optimization

## Problem
When editing a contract from the Contracts page (`http://localhost:3000/#/contracts`), the application was making multiple redundant API calls to `/api/users?page=1&pageSize=1000` and `/api/groups`. This happened because:

1. **ContractsPage** loads users and groups on mount for the filter dropdowns
2. **ContractForm** (the edit modal) also loads users and groups on mount for its dropdowns
3. This resulted in at least 2 duplicate API calls every time a user clicked "Edit" on a contract

## Solution
Implemented a React Context-based caching solution that:

1. **Created ContractsContext** (`src/contexts/ContractsContext.tsx`)
   - Provides a centralized store for contracts, users, and groups data
   - Allows components to share cached data without re-fetching

2. **Updated ContractsPage** (`src/components/ContractsPage.tsx`)
   - Now caches users, groups, and contracts data in the context after fetching
   - This data becomes available to all child components

3. **Updated ContractForm** (`src/components/ContractForm.tsx`)
   - Now checks if users and groups are available in the context cache
   - Only fetches from API if cache is empty
   - Still fetches PVs (smaller dataset) on each open

4. **Updated App.tsx**
   - Wrapped the application with `ContractsProvider` to make the context available globally

## Benefits
- **Reduced API Calls**: Eliminates duplicate calls to `/api/users` and `/api/groups` when editing contracts
- **Better Performance**: Faster form loading when editing contracts
- **Improved UX**: No unnecessary loading states or network requests
- **Scalable**: The context can be extended to cache other data as needed

## Technical Details
- Uses React Context API (no additional dependencies required)
- Cache is populated when ContractsPage loads
- Cache is updated whenever contracts are loaded/refreshed
- ContractForm falls back to API calls if cache is empty (handles edge cases)

## Files Modified
1. `src/contexts/ContractsContext.tsx` (new file)
2. `src/components/ContractsPage.tsx`
3. `src/components/ContractForm.tsx`
4. `src/App.tsx`

## Testing
The build completed successfully with no errors. The application should now:
- Load users and groups only once when visiting the Contracts page
- Reuse cached data when opening the edit form
- Still work correctly if the form is opened before the cache is populated
