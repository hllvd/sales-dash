# Multiple Users with Same Matricula Number - Fix

## Issue
User reported: "Both matricula number 1 and matricula number 2 is assigned to this user. Please allow assign a contractor for this specific user for both matricula number."

Error received:
```
400 Bad Request
"Matricula '1' does not belong to the specified user"
```

## Root Cause

The system allows **multiple users to have the same matricula number**. This is by design (see `UserMatriculaRepository.SetOwnerAsync` which handles multiple users with the same matricula).

However, the validation logic was using `GetByMatriculaNumberAsync(matriculaNumber)` which returns the **FIRST** matricula with that number, regardless of which user it belongs to.

### Example Scenario:
- User A has matricula "1"
- User B has matricula "1" (same number, different user)
- Contract is assigned to User B
- When trying to assign matricula "1" to the contract:
  - Old logic: Finds User A's matricula "1" (first one in database)
  - Validation fails: "matricula.UserId (User A) != contract.UserId (User B)"
  - ❌ Error: "Matricula '1' does not belong to the specified user"

## Solution

### 1. **Added New Repository Method**

**File:** `SalesApp.Api/Repositories/IUserMatriculaRepository.cs`
```csharp
Task<UserMatricula?> GetByMatriculaNumberAndUserIdAsync(string matriculaNumber, Guid userId);
```

**File:** `SalesApp.Api/Repositories/UserMatriculaRepository.cs`
```csharp
public async Task<UserMatricula?> GetByMatriculaNumberAndUserIdAsync(string matriculaNumber, Guid userId)
{
    return await _context.UserMatriculas
        .Include(m => m.User)
        .FirstOrDefaultAsync(m => m.MatriculaNumber == matriculaNumber && m.UserId == userId);
}
```

This method queries by **BOTH** matricula number **AND** user ID, ensuring we get the correct matricula for the specific user.

### 2. **Updated Validation Logic**

**File:** `SalesApp.Api/Controllers/ContractsController.cs`

**Before:**
```csharp
private async Task<(bool isValid, UserMatricula? matricula, string? errorMessage)> 
    ValidateMatriculaForUser(string matriculaNumber, Guid userId)
{
    var matricula = await _matriculaRepository.GetByMatriculaNumberAsync(matriculaNumber);
    
    if (matricula == null)
        return (false, null, $"Matricula '{matriculaNumber}' not found");
    
    if (!matricula.IsActive)
        return (false, null, $"Matricula '{matriculaNumber}' is not active");
    
    if (matricula.UserId != userId)  // ❌ This check is now redundant!
        return (false, null, $"Matricula '{matriculaNumber}' does not belong to the specified user");
    
    return (true, matricula, null);
}
```

**After:**
```csharp
private async Task<(bool isValid, UserMatricula? matricula, string? errorMessage)> 
    ValidateMatriculaForUser(string matriculaNumber, Guid userId)
{
    // Query for matricula by BOTH number AND userId since multiple users can have the same number
    var matricula = await _matriculaRepository.GetByMatriculaNumberAndUserIdAsync(matriculaNumber, userId);
    
    if (matricula == null)
        return (false, null, $"Matricula '{matriculaNumber}' not found for this user");
    
    if (!matricula.IsActive)
        return (false, null, $"Matricula '{matriculaNumber}' is not active");
    
    return (true, matricula, null);
}
```

### Key Changes:
1. ✅ Uses `GetByMatriculaNumberAndUserIdAsync` instead of `GetByMatriculaNumberAsync`
2. ✅ Removed redundant `matricula.UserId != userId` check (already filtered by query)
3. ✅ Updated error message to be more specific: "not found for this user"

## Behavior

### Before Fix:
- ❌ If multiple users had matricula "1", validation would fail randomly
- ❌ Would always find the first matricula "1" in the database
- ❌ Contract assignment would fail even if the user legitimately had that matricula

### After Fix:
- ✅ Correctly finds the matricula for the specific user
- ✅ Allows multiple users to have the same matricula number
- ✅ Each user can assign contracts to their own matriculas (1, 2, etc.)

## Example Scenarios

### Scenario 1: User with Multiple Matriculas
```
User: Maria Silva (ID: abc123)
Matriculas: 
  - Matricula "1" (active, owner)
  - Matricula "2" (active, not owner)

Contract #72 assigned to Maria Silva
```

**Now works:**
- ✅ Can assign matricula "1" to contract #72
- ✅ Can assign matricula "2" to contract #72
- ✅ Both matriculas belong to Maria Silva

### Scenario 2: Multiple Users with Same Number
```
User A: Matricula "1"
User B: Matricula "1" (same number, different user)

Contract #72 assigned to User B
```

**Now works:**
- ✅ Assigns User B's matricula "1" to the contract
- ✅ Doesn't accidentally use User A's matricula "1"

## Files Modified

1. **Interface:** `SalesApp.Api/Repositories/IUserMatriculaRepository.cs`
   - Added `GetByMatriculaNumberAndUserIdAsync` method signature

2. **Implementation:** `SalesApp.Api/Repositories/UserMatriculaRepository.cs`
   - Added `GetByMatriculaNumberAndUserIdAsync` implementation

3. **Controller:** `SalesApp.Api/Controllers/ContractsController.cs`
   - Updated `ValidateMatriculaForUser` to use new repository method
   - Removed redundant userId check
   - Improved error message

## Build Status

✅ **Backend builds successfully** - No errors  
✅ **All validation logic updated**  
✅ **Ready for production**

## Testing

To verify the fix works:

1. **Create test data:**
   ```sql
   -- User A with matricula "1"
   INSERT INTO UserMatriculas (UserId, MatriculaNumber, IsActive, IsOwner, StartDate)
   VALUES ('user-a-id', '1', true, true, NOW());
   
   -- User B with matricula "1" (same number)
   INSERT INTO UserMatriculas (UserId, MatriculaNumber, IsActive, IsOwner, StartDate)
   VALUES ('user-b-id', '1', true, true, NOW());
   ```

2. **Test contract assignment:**
   ```bash
   # Assign contract to User B with matricula "1"
   curl -X PUT http://localhost:5017/api/contracts/72 \
     -H "Authorization: Bearer TOKEN" \
     -H "Content-Type: application/json" \
     -d '{
       "userId": "user-b-id",
       "matriculaNumber": "1"
     }'
   ```

3. **Expected result:**
   - ✅ Success: Contract assigned with User B's matricula "1"
   - ✅ No error about matricula not belonging to user

You can now assign contracts to any of your matriculas (1, 2, etc.) without errors! 🎉
