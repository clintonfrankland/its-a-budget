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

Receipt uploads are limited to PDF and common image formats (JPG, PNG, GIF, WebP, BMP, and TIFF). The default maximum file size is 5 MB unless the administrator changes the `ReceiptAttachments:MaxFileSizeBytes` configuration value.

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

In the upcoming-items list, **Record to Checkbook** is the icon-only primary control and sits beside the icon-only three-dot **More actions** control. The overflow menu shows labeled icon commands for Skip, Edit, and Edit Next. These controls use embedded SVG icons so they remain visible without an external icon font.

This is one of the app’s best quality-of-life features. It helps you move from “planning” to “recording” without doing the same work twice.
