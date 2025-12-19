# Matricula Validation Error - Debugging Guide

## Issue
When updating contract #72 with matricula number "1", receiving error:
```
400 Bad Request
"Matricula '1' does not belong to the specified user"
```

## Root Cause Analysis

The error occurs because the matricula validation checks if the matricula belongs to the user assigned to the contract. This can fail in several scenarios:

### Scenario 1: Matricula Belongs to Different User
- Contract is assigned to User A
- Matricula "1" belongs to User B
- ❌ Validation fails: matricula doesn't belong to the contract's user

### Scenario 2: User Changed in Same Request
- Contract was assigned to User A
- Request changes user to User B AND sets matricula "1"
- Matricula "1" belongs to User A (not User B)
- ❌ Validation fails: matricula doesn't belong to the NEW user

### Scenario 3: Contract Has No User
- Contract has no user assigned (`userId = null`)
- Trying to assign matricula "1"
- ❌ Validation fails: cannot assign matricula without a user

## Fix Applied

### Enhanced Error Messages
Updated the error message to include user information for easier debugging:

**Before:**
```
"Matricula '1' does not belong to the specified user"
```

**After:**
```
"Matricula '1' does not belong to the specified user (Contract assigned to: John Doe, User ID: 123e4567-...)"
```

This helps identify:
- Which user the contract is currently assigned to
- Whether the matricula belongs to a different user
- If there's a mismatch between the contract user and matricula owner

### Code Changes

**File:** `SalesApp.Api/Controllers/ContractsController.cs`

```csharp
// Enhanced error message with user context
if (!isValid)
{
    // Get user info for better error message
    var user = await _userRepository.GetByIdAsync(userId);
    var userName = user?.Name ?? "Unknown";
    var enhancedMessage = $"{errorMessage} (Contract assigned to: {userName}, User ID: {userId})";
    
    return BadRequest(new ApiResponse<ContractResponse>
    {
        Success = false,
        Message = enhancedMessage
    });
}
```

## How to Resolve

### Option 1: Verify Matricula Ownership
Check which user owns matricula "1":

```sql
SELECT * FROM UserMatriculas WHERE MatriculaNumber = '1' AND IsActive = true;
```

Then ensure the contract is assigned to that user.

### Option 2: Change User First, Then Matricula
If you need to change both the user and matricula:

1. **First request:** Update the contract's user
   ```json
   PUT /api/contracts/72
   {
     "userId": "correct-user-id"
   }
   ```

2. **Second request:** Update the matricula
   ```json
   PUT /api/contracts/72
   {
     "matriculaNumber": "1"
   }
   ```

### Option 3: Update Both in Single Request
Ensure the user you're assigning owns the matricula:

```json
PUT /api/contracts/72
{
  "userId": "user-who-owns-matricula-1",
  "matriculaNumber": "1"
}
```

## Validation Rules

The backend enforces these rules:

1. ✅ **Matricula must exist** in the database
2. ✅ **Matricula must be active** (`IsActive = true`)
3. ✅ **Matricula must belong to the contract's user** (`matricula.UserId == contract.UserId`)
4. ✅ **Contract must have a user** to assign a matricula

## Debugging Steps

1. **Check the enhanced error message** - It now shows which user the contract is assigned to
2. **Verify matricula ownership:**
   ```sql
   SELECT um.*, u.Name as UserName
   FROM UserMatriculas um
   JOIN Users u ON um.UserId = u.Id
   WHERE um.MatriculaNumber = '1' AND um.IsActive = true;
   ```
3. **Check contract's current user:**
   ```sql
   SELECT c.*, u.Name as UserName
   FROM Contracts c
   LEFT JOIN Users u ON c.UserId = u.Id
   WHERE c.Id = 72;
   ```
4. **Verify the match** - The `UserId` from both queries should match

## Example Error Messages

With the enhanced error messages, you'll now see:

```
"Matricula '1' does not belong to the specified user (Contract assigned to: Maria Silva, User ID: abc123...)"
```

This tells you:
- The contract is assigned to "Maria Silva"
- Matricula "1" does NOT belong to Maria Silva
- You need to either:
  - Assign the contract to the user who owns matricula "1", OR
  - Use a different matricula that belongs to Maria Silva

## API Testing

To test with curl:
```bash
# Get contract details
curl -X GET http://localhost:5017/api/contracts/72 \
  -H "Authorization: Bearer YOUR_TOKEN"

# Update with better error message
curl -X PUT http://localhost:5017/api/contracts/72 \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "matriculaNumber": "1"
  }'
```

The error response will now include the user information to help debug the issue.

## Build Status

✅ **Backend builds successfully**  
✅ **Enhanced error messages active**  
✅ **Ready for debugging**
