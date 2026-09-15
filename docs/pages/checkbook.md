# Checkbook

## What this page is

The **Checkbook** page is the heart of the app.

It works like a modern digital check register:

- You enter transactions (income or expenses)
- The app keeps a running **Balance**
- You can mark transactions as **Cleared** as they hit your bank

It’s fast, practical, and meant to match how you actually think about money day-to-day.

## What you’ll see at the top

- **Balance**: your current running balance
- **Cleared**: your cleared balance (helpful when reconciling)
- **Safe to Spend**: the lowest projected balance over the next six months after uncleared Checkbook transactions are treated as committed today

If you have bills that are due today or past due, you may see a warning that tells you how many are due and nudges you to open the Budgets panel.

## Adding a transaction

1. Select **Add Transaction**.
2. Fill in the form:
   - **Date**
   - **Payee** (who the money went to or came from)
   - **Amount**
   - **Type**: Expense or Income
   - **Category** (helps you group spending)
   - **Notes** (optional)
   - **Attachment** (optional, like a receipt or confirmation)
   - **Cleared** (optional)
3. Select **Save**.

Select **Cancel** if you want to back out without saving.

## Notes and attachments (receipts)

Sometimes you want proof or context, not just numbers.

On a transaction you can add:

- **Notes**: a short description (for example, “this was the annual renewal”)
- **Attachment**: a PDF or image receipt/confirmation

This is great for returns, reimbursements, and keeping a clean paper trail.

Receipt uploads are limited to PDF and common image formats (JPG, PNG, GIF, WebP, BMP, and TIFF). The default maximum file size is 5 MiB unless the administrator changes the `ReceiptAttachments:MaxFileSizeBytes` configuration value.

The app validates the receipt before saving it, stores it with a generated safe filename, and records only an app-relative path on the transaction. A pluggable scan hook runs during upload; the default local/review scanner is no-op, but a production scanner can reject an upload before the transaction references it.

When you replace, remove, or delete a receipt attachment, the app deletes only files inside its managed receipt upload folder. A cleanup worker also removes orphaned receipt files under that folder when no transaction references them.

## Editing or deleting a transaction

In the transactions list:

- Select the **Edit** button (pencil icon) to change a transaction.
- When editing an existing transaction, you’ll also see **Delete this Transaction**.

Tip: Deleting is powerful. If you’re unsure, edit it instead and correct the details.

## Marking a transaction Cleared

Each transaction has a **cleared toggle**:

- If it’s not cleared yet, you can mark it **Cleared**.
- If it’s already cleared, you can switch it back to **Uncleared**.

This is especially handy if you want your register to include pending items while still being able to reconcile against what the bank says has cleared.

## Searching and filtering

At the top of the grid, use **Search** to quickly narrow down the list.

Examples:

- Type a payee name to find all transactions for that place.
- Type a category to review spending patterns.

## The Budgets panel (Upcoming Budget Items)

The Checkbook page can also show a built-in “heads up” list of what’s coming.

1. Select the **Budgets** button.
2. You’ll see **Upcoming Budget Items**.
3. Choose how many days ahead you want to view.

For each upcoming item, you can:

- **Record to Checkbook**: creates a matching transaction in your register.
- **Skip**: marks the item handled without adding a transaction.

In the upcoming-items list, the original small green Radzen **Record to Checkbook** icon button sits beside a narrow three-dot **More actions** button. Tapping the trigger explicitly opens the overflow menu, which shows the app's original icons plus text for Skip, Edit, and Edit Next.

This is one of the app’s best quality-of-life features. It helps you move from “planning” to “recording” without doing the same work twice.


## Statement import formats

Use **Import** to select a CSV, OFX, QFX, or QIF file up to 5 MiB. Review the destination account, statement account (when supplied), signed amounts, and cleared status. The preview shows the first ten records; importing validates and saves the entire file atomically. No transaction is saved while choosing a file or adjusting CSV mappings. Invalid replacement files clear the previous selection.

- **CSV:** existing date/amount/payee/category column mapping; Date and Amount required. Entries start uncleared.
- **OFX/QFX:** one USD bank or credit-card statement/account; XML and legacy SGML with `OFXHEADER:100` supported. Posted entries become cleared. Required transaction identifiers must be unique within the file. Multi-account, investment, unsupported currency, correction, or malformed statement structures are rejected.
- **QIF:** one `!Type:Bank` or `!Type:CCard` section; US `M/d/yyyy` dates or QIF apostrophe years (`7/23'26` means 2026), signed amounts, payee/category and cleared markers. Account definitions, section changes, transfers, splits, investments and incomplete records are rejected instead of being silently flattened.

All rows go to the displayed destination account; statement account numbers are **not** automatically matched. Confirm that the destination is correct. Changing the active/default account or losing permission after preview rejects the import, so reopen Import to select a fresh destination. Files do not create accounts. Re-importing files creates duplicate transactions; preview reference IDs are not stored for cross-upload deduplication. CSV export and Plaid reconciliation behavior are unchanged.

During import, the Import/Cancel/mapping controls are disabled to prevent overlapping submissions. Skip actions in Checkbook and Forecast likewise remain busy until their operation finishes, including data refresh, and become available again after an error.

Ambiguous slash dates with two-digit years are rejected rather than assigned a guessed century. XML namespaces, declarations such as DTDs, comments, pending/correction OFX entries, and unsupported QIF fields or category/classes are rejected. The 5 MiB parser limit is measured as UTF-8 bytes.
