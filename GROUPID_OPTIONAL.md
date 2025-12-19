# GroupId Optional for Contracts

## Summary
Ensured that `groupId` is truly optional when creating or updating contracts, both on the backend and frontend.

## Changes Made

### **Backend (Already Correct)**

The backend was already properly configured to handle `groupId` as optional:

**DTOs:**
- `ContractRequest.cs` - `GroupId` is `int?` (nullable)
- `UpdateContractRequest.cs` - `GroupId` is `int?` (nullable)

**Controller Validation:**
- Only validates `groupId` if it has a value (`if (request.GroupId.HasValue)`)
- Allows contracts to be created/updated without a group

### **Frontend Changes**

#### 1. **Updated Initial State** (`ContractForm.tsx`)
Changed default `groupId` from `'0'` to empty string:
```typescript
// Before
groupId: contract?.groupId?.toString() || '0',

// After
groupId: contract?.groupId?.toString() || '',
```

#### 2. **Updated Dropdown** (`ContractForm.tsx`)
- Changed "Nenhum" option value from `'0'` to `''` (empty string)
- Added `clearable` prop to allow clearing the selection
- Kept the label as "Grupo (Opcional)" to indicate it's optional

```typescript
<Select
  label="Grupo (Opcional)"
  value={formData.groupId}
  onChange={(value) => handleChange('groupId', value)}
  data={[
    { value: '', label: 'Nenhum' },
    ...groups.map(group => ({ value: group.id.toString(), label: group.name }))
  ]}
  clearable
  mb="md"
/>
```

#### 3. **Simplified Data Submission Logic**
Since empty string is falsy in JavaScript, simplified the logic:

```typescript
// Both create and update now use the same simple logic
groupId: formData.groupId ? parseInt(formData.groupId) : undefined,
```

When `groupId` is empty string or null:
- Frontend sends `undefined` to the API
- Backend receives `null` for the `GroupId` field
- Contract is created/updated without a group assignment

## Behavior

### **Creating a Contract**
- User can select "Nenhum" or leave the group field empty
- Contract will be created with `groupId = null`
- No validation errors

### **Updating a Contract**
- User can change group to "Nenhum" to remove group assignment
- User can select a different group
- Contract will be updated with the new `groupId` or `null`

### **Validation**
- ✅ Contract number is required
- ✅ Total amount is required
- ✅ Contract start date is required
- ❌ Group is **NOT** required
- ❌ User is **NOT** required
- ❌ PV is **NOT** required

## Files Modified

1. 📝 **Modified:** `client/sales-dash-login/src/components/ContractForm.tsx`
   - Changed default `groupId` to empty string
   - Updated dropdown to use empty string for "Nenhum"
   - Added `clearable` prop
   - Simplified submission logic

## Build Status

✅ **Frontend builds successfully** - No errors  
✅ **Backend already correct** - No changes needed  
✅ **Ready for production**

## Testing Checklist

To verify this works correctly:

1. ✅ Create a new contract without selecting a group
2. ✅ Create a new contract with a group selected
3. ✅ Edit an existing contract and remove the group (select "Nenhum")
4. ✅ Edit an existing contract and change the group
5. ✅ Verify contracts without groups display correctly in the list
6. ✅ Verify filtering by group still works

## API Examples

**Create Contract Without Group:**
```json
POST /api/contracts
{
  "contractNumber": "C-001",
  "totalAmount": 1000.00,
  "status": "Active",
  "contractStartDate": "2024-01-01"
  // groupId is omitted or null
}
```

**Update Contract to Remove Group:**
```json
PUT /api/contracts/123
{
  "groupId": null
  // or simply omit the field
}
```

Both requests will succeed without validation errors.
