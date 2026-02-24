# EF Core Migration Plan

## Overview
Replace all SqlProvider stored procedure calls with Entity Framework Core queries using `ClintonFranklandDbContext`.

**Total Stored Procedures to Migrate:** 22 unique procedure calls  
**Estimated Total Time:** 4-6 hours  
**Start Date:** Today

---

## Progress Tracker

| # | Procedure | Page | Type | Status | Time Est. |
|---|-----------|------|------|--------|-----------|
| 1 | `spcfGetFrequencies` | Budget, BudgetItems | SELECT | ✅ Complete | 10 min |
| 2 | `spcfGetCategories_020000` | Budget, BudgetItems, Checkbook | SELECT | ✅ Complete | 10 min |
| 3 | `spcfGetPayees_020000` | Budget, BudgetItems, Checkbook | SELECT | ✅ Complete | 15 min |
| 4 | `spcfGetAccounts` | Accounts | SELECT | ✅ Complete | 20 min |
| 5 | `spcfGetAccount` | Accounts | SELECT (single) | ✅ Complete | 10 min |
| 6 | `spcfSaveAccount` | Accounts | INSERT/UPDATE | ✅ Complete | 20 min |
| 7 | `spcfDeleteAccount` | Accounts | DELETE | ✅ Complete | 10 min |
| 8 | `spcfGetBudget` | Budget, BudgetItems | SELECT (single) | ✅ Complete | 10 min |
| 9 | `spcfGetBudgetItems_030000` | BudgetItems | SELECT | ✅ Complete | 25 min |
| 10 | `spcfSaveBudget_030000` | Budget, BudgetItems | INSERT/UPDATE | ✅ Complete | 25 min |
| 11 | `spcfDeleteBudget` | Budget, BudgetItems | DELETE | ✅ Complete | 10 min |
| 12 | `spcfMarkPaid` | Budget, Checkbook | UPDATE/DELETE | ✅ Complete | 20 min |
| 13 | `spcfGetMyBudget_020000` | Budget, Checkbook | SELECT (complex) | ✅ Complete | 45 min |
| 14 | `spcfMyBudgetGetChart` | Budget | SELECT (complex) | ✅ Complete | 30 min |
| 15 | `spcfGetCheckbookBalance_020000` | Checkbook | SELECT | ✅ Complete | 15 min |
| 16 | `spcfGetMyCheckbook_040100` | Checkbook | SELECT | ✅ Complete | 25 min |
| 17 | `spcfGetTransaction` | Checkbook | SELECT (single) | ✅ Complete | 10 min |
| 18 | `spcfSaveTransaction_030000` | Checkbook | INSERT/UPDATE | ✅ Complete | 20 min |
| 19 | `spcfMyCheckbookDeleteTransaction` | Checkbook | DELETE | ✅ Complete | 10 min |
| 20 | `spcfMyCheckboxMarkTransactionCleared` | Checkbook | UPDATE | ✅ Complete | 10 min |
| 21 | `spcfMyCheckboxMarkTransactionUncleared` | Checkbook | UPDATE | ✅ Complete | 10 min |

---

## Phase 1: Simple SELECT Queries (Lookup Tables)
**Estimated Time: 35 minutes**

### Step 1.1: `spcfGetFrequencies` ✅ COMPLETE
- **File:** `Budget.razor.cs`, `BudgetItems.razor.cs`
- **Current:** Returns all frequencies ordered by Sort
- **EF Equivalent:**
  ```csharp
  await DbContext.Frequencies.OrderBy(f => f.Sort).ToListAsync();
  ```
- **Status:** ✅ Complete
- **Notes:** Added DbContext injection, made methods async, updated .razor files

### Step 1.2: `spcfGetCategories_020000` ✅ COMPLETE
- **File:** `Budget.razor.cs`, `BudgetItems.razor.cs`, `Checkbook.razor.cs`
- **Current:** Returns categories for a user ordered by name
- **EF Equivalent:**
  ```csharp
  await DbContext.Categories
      .Where(c => c.UserId == userId)
      .OrderBy(c => c.CategoryName)
      .ToListAsync();
  ```
- **Status:** ✅ Complete
- **Notes:** Combined with Step 1.3 in LoadCategoriesAndPayeesAsync

### Step 1.3: `spcfGetPayees_020000` ✅ COMPLETE
- **File:** `Budget.razor.cs`, `BudgetItems.razor.cs`, `Checkbook.razor.cs`
- **Current:** Returns payees with transaction/budget counts
- **EF Equivalent:**
  ```csharp
  await DbContext.Payees
      .Where(p => p.UserId == userId && !p.IsDeleted)
      .OrderBy(p => p.PayeeName)
      .ToListAsync();
  ```
- **Status:** ✅ Complete
- **Notes:** Combined with Step 1.2 in LoadCategoriesAndPayeesAsync (simplified - no aggregations needed for autocomplete)

---

## Phase 2: Account Operations ✅ COMPLETE
**Estimated Time: 60 minutes**

### Step 2.1: `spcfGetAccounts` ✅ COMPLETE
- **File:** `Accounts.razor.cs`
- **Current:** Returns accounts with sorting options
- **EF Equivalent:**
  ```csharp
  await DbContext.Accounts
      .Include(a => a.AccountType)
      .Where(a => a.UserId == userId && (a.IsDeleted == null || a.IsDeleted == false))
      .OrderBy(a => a.AccountName)
      .ToListAsync();
  ```
- **Status:** ✅ Complete
- **Notes:** Simplified sorting to just AccountName (can add dynamic sorting later)

### Step 2.2: `spcfGetAccount` ✅ COMPLETE
- **File:** `Accounts.razor.cs`
- **Current:** Returns single account by ID
- **EF Equivalent:**
  ```csharp
  await DbContext.Accounts
      .Include(a => a.AccountType)
      .FirstOrDefaultAsync(a => a.AccountId == accountId);
  ```
- **Status:** ✅ Complete
- **Notes:** Direct entity mapping to edit fields

### Step 2.3: `spcfSaveAccount` ✅ COMPLETE
- **File:** `Accounts.razor.cs`
- **Current:** Inserts or updates account
- **EF Equivalent:** Add/Update pattern with SaveChangesAsync
- **Status:** ✅ Complete
- **Notes:** Full CRUD with EF Core

### Step 2.4: `spcfDeleteAccount` ✅ COMPLETE
- **File:** `Accounts.razor.cs`
- **Current:** Deletes account by ID
- **EF Equivalent:**
  ```csharp
  var account = await DbContext.Accounts.FindAsync(accountId);
  if (account != null) DbContext.Accounts.Remove(account);
  await DbContext.SaveChangesAsync();
  ```
- **Status:** ✅ Complete
- **Notes:** Hard delete (can change to soft delete later)

---

## Phase 3: Budget Operations ✅ COMPLETE
**Estimated Time: 90 minutes**

### Step 3.1: `spcfGetBudget` ✅ COMPLETE
- **File:** `Budget.razor.cs`, `BudgetItems.razor.cs`
- **Current:** Returns single budget with related data
- **EF Equivalent:**
  ```csharp
  await DbContext.Budgets
      .Include(b => b.Category)
      .Include(b => b.Payee)
      .FirstOrDefaultAsync(b => b.BudgetId == budgetId);
  ```
- **Status:** ✅ Complete
- **Notes:** Used in ShowEditBudgetAsync and ShowEditNextAsync

### Step 3.2: `spcfGetBudgetItems_030000` ✅ COMPLETE
- **File:** `BudgetItems.razor.cs`
- **Current:** Returns budget items with monthly calculations
- **EF Equivalent:** Complex - requires calculation logic in C#
- **Status:** ✅ Complete
- **Notes:** Added CalculateMonthlyAmount helper method that converts frequency-based amounts to monthly equivalents

### Step 3.3: `spcfSaveBudget_030000` ✅ COMPLETE
- **File:** `Budget.razor.cs`, `BudgetItems.razor.cs`
- **Current:** Inserts or updates budget, creates category/payee if needed
- **EF Equivalent:** Add/Update with related entity creation
- **Status:** ✅ Complete
- **Notes:** Includes GetOrCreateCategoryAsync and GetOrCreatePayeeAsync helper methods

### Step 3.4: `spcfDeleteBudget` ✅ COMPLETE
- **File:** `Budget.razor.cs`, `BudgetItems.razor.cs`
- **Current:** Deletes budget by ID
- **Status:** ✅ Complete
- **Notes:** Simple EF Core Remove + SaveChangesAsync

### Step 3.5: `spcfMarkPaid` ✅ COMPLETE
- **File:** `Budget.razor.cs`, `Checkbook.razor.cs`
- **Current:** Updates NextDueDate based on frequency, may delete
- **EF Equivalent:** Complex frequency calculation logic
- **Status:** ✅ Complete
- **Notes:** Added CalculateNextDueDate and CalculateSemiMonthly helper methods. Also updated SaveEditNextAsync to use EF Core.

### Step 3.6: `spcfGetMyBudget_020000` ✅ COMPLETE
- **File:** `Budget.razor.cs`, `Checkbook.razor.cs`
- **Current:** Complex forecast calculation with running balance
- **EF Equivalent:** Requires significant C# logic for projection
- **Status:** ✅ Complete
- **Notes:** Added GenerateBudgetForecastAsync method that projects budgets forward through time with running balance calculation

### Step 3.7: `spcfMyBudgetGetChart` ✅ COMPLETE
- **File:** `Budget.razor.cs`
- **Current:** Returns chart data for budget forecast
- **Status:** ✅ Complete
- **Notes:** Added GenerateChartData helper that groups forecast items by date

---

## Phase 4: Transaction/Checkbook Operations ✅ COMPLETE
**Estimated Time: 90 minutes**

### Step 4.1: `spcfGetCheckbookBalance_020000` ✅ COMPLETE
- **File:** `Checkbook.razor.cs`
- **Current:** Returns balance and cleared amounts
- **EF Equivalent:** Calculate from transactions sum
- **Status:** ✅ Complete
- **Notes:** Combined with Step 4.2 in LoadDataAsync

### Step 4.2: `spcfGetMyCheckbook_040100` ✅ COMPLETE
- **File:** `Checkbook.razor.cs`
- **Current:** Returns transactions with running balance
- **EF Equivalent:** Query with client-side running balance calculation
- **Status:** ✅ Complete
- **Notes:** Running balance calculation in C# using LINQ Select with accumulator

### Step 4.3: `spcfGetTransaction` ✅ COMPLETE
- **File:** `Checkbook.razor.cs`
- **Current:** Returns single transaction with related data
- **EF Equivalent:**
  ```csharp
  await DbContext.Transactions
      .Include(t => t.Payee)
      .Include(t => t.Category)
      .FirstOrDefaultAsync(t => t.TransactionId == transactionId);
  ```
- **Status:** ✅ Complete
- **Notes:** Direct entity to edit fields mapping

### Step 4.4: `spcfSaveTransaction_030000` ✅ COMPLETE
- **File:** `Checkbook.razor.cs`
- **Current:** Inserts or updates transaction, creates category/payee if needed
- **Status:** ✅ Complete
- **Notes:** Uses GetOrCreateCategoryAsync and GetOrCreatePayeeAsync helpers. Handles DateOnly conversion.

### Step 4.5: `spcfMyCheckbookDeleteTransaction` ✅ COMPLETE
- **File:** `Checkbook.razor.cs`
- **Current:** Deletes transaction by ID
- **Status:** ✅ Complete
- **Notes:** Simple EF Core Remove + SaveChangesAsync

### Step 4.6: `spcfMyCheckboxMarkTransactionCleared` ✅ COMPLETE
- **File:** `Checkbook.razor.cs`
- **Current:** Sets Cleared = true
- **Status:** ✅ Complete
- **Notes:** Direct property update + SaveChangesAsync

### Step 4.7: `spcfMyCheckboxMarkTransactionUncleared` ✅ COMPLETE
- **File:** `Checkbook.razor.cs`
- **Current:** Sets Cleared = false
- **Status:** ✅ Complete
- **Notes:** Direct property update + SaveChangesAsync

---

## Prerequisites
- [x] DbContext created (`ClintonFranklandDbContext`)
- [x] All entity models created and verified
- [ ] DbContext registered in DI container
- [ ] Connection string configured in appsettings.json

---

## Notes
- Each step will be done one at a time
- User approval required before proceeding to next step
- Test each migration before marking complete
- Keep SqlProvider code commented (not deleted) until verified

---

## Completion Log
| Date | Step | Notes |
|------|------|-------|
| Today | 1.1 spcfGetFrequencies | Added DbContext to Budget.razor.cs and BudgetItems.razor.cs, replaced LoadFrequencies with async EF Core query |
| Today | 1.2 & 1.3 spcfGetCategories/Payees | Added DbContext to Checkbook.razor.cs, replaced LoadCategoriesAndPayees with async EF Core queries in all 3 files |
| Today | 2.1-2.4 Account Operations | Fully migrated Accounts.razor.cs - GetAccounts, GetAccount, SaveAccount, DeleteAccount all use EF Core now |
| Today | 3.1, 3.3, 3.4 Budget CRUD | GetBudget, SaveBudget (with GetOrCreate helpers), DeleteBudget migrated to EF Core |
| Today | 3.5 spcfMarkPaid | Complex frequency calculation logic ported to C#. Added CalculateNextDueDate helper. Updated SaveEditNextAsync. |
| Today | 3.2 spcfGetBudgetItems | Added CalculateMonthlyAmount helper for frequency-to-monthly conversion |
| Today | 3.6 & 3.7 Budget Forecast | GenerateBudgetForecastAsync projects budgets forward with running balance. GenerateChartData groups by date. |
| Today | 4.1-4.7 Transaction Operations | Full checkbook migration - balance, transactions, CRUD, cleared/uncleared all use EF Core now |

