# Per Piece Supplies Inventory

Implemented in source on September 30, 2026, following the user's confirmation that soap and Downy should be counted **per piece**. This update adds basic stock monitoring to the Owner and Staff workflows, including Process Walk-in. It extends the earlier [supply source choices](2026-09-30-supply-source-choices.md); it does not claim that inventory existed in the original manuscript or at the earlier documentation cutoff.

Simplified later on September 30 at the user's request: the separate **Issue Supplies for a Booking**, **Return Unused Supplies**, and **Correct Physical Stock Count** forms and server actions were removed as unwanted additions. Following the user's approval to include online bookings, stock usage is now integrated into the existing Staff **Accept & Start** and **Update Status** forms. Basic balances, Owner Stock In, walk-in deductions, and movement history remain. No existing stock or historical records were deleted; no separate issuance page was restored.

## What is counted

One piece is one individually counted sachet/item of Powder Soap or Downy Softener. Stock starts at zero until the owner records a real opening count or delivery. No opening stock, historical usage, or dosage per kilogram is invented.

For walk-ins, staff enters the actual number of shop pieces issued. For online bookings, staff sees the saved piece quantities and confirms that those pieces are being issued. Laundry weight determines per-kilogram laundry charges, not detergent usage. Customer-provided supplies and supplies marked Not needed do not create a shop-product charge or stock deduction. An order can use personal detergent and shop Downy, or the reverse.

This is basic stock control, not supplier purchasing, expense accounting, profit calculation, bulk liquid measurement, or an automatic dosing system. Selling prices continue to come from Owner Service Pricing; the inventory does not determine whether a price increase is commercially necessary.

## Setup on the existing installation

1. Restart the application using the updated project and reload the browser. The already-running application was not stopped during verification.
2. Sign in as Owner and open **Inventory** in the sidebar.
3. If shown, click **Initialize Inventory**. This creates the three additive inventory tables and zero-balance rows for the existing Powder and Downy per-item services. Repeating initialization does not reset stock.
4. Use **Add Stock** separately for each supply. Enter the actual pieces counted and a reason such as Opening stock. Do not use the sample test quantities as real stock.
5. Staff can then open **Supplies Inventory** or **Process Walk-in** from the Staff dashboard.

For database administrators, `migrations/20260930_piece_inventory.sql` is an alternative additive schema setup. Use the intended existing database. Do not reimport the entire main schema into a populated installation just to enable this feature. The complete `laundry_final_3nf.sql` also contains the new tables for fresh installations.

No setup migration, sample stock, booking, or payment was written to the live `laundry_final` database during verification. Until setup is performed, shop-supply processing is blocked with an explanation; own-product/no-product orders can still be processed. The online integration reuses the existing inventory tables and requires no new migration.

## Walk in workflow

The walk-in form retains core laundry services, actual weight, and Rush Fee. Soap and softener each have a required source choice: Shop, customer-provided, or Not needed. For Shop, enter 1 through 20 whole pieces. The form shows the available stock and the current selling price per piece.

When using shop supplies, staff must explicitly confirm that those pieces are being issued. Saving the walk-in then creates the booking, charge lines, customer instructions, status history, and inventory deduction in one database transaction. Insufficient stock rolls back the whole new order; staff can correct the form without leaving a partial booking behind. Source or piece-quantity edits clear the issuance confirmation.

The server calculates the bill. For example, two shop sachets are charged as two times the stored unit price. The client cannot replace the total or inject Powder/Downy through the core-services field. A request token and a unique booking code protect against repeated submission of the same form.

## Online booking workflow

Submitting an online booking or recording its payment does not deduct or reserve stock. In the Staff pending-bookings list, **Accept & Start** shows the saved Powder and Downy piece quantities. If shop supplies are included and not already issued, staff must confirm that those pieces are being physically issued before starting the order. Acceptance moves Pending to Washing and writes the stock deduction, issue marker, and status history together in one database transaction.

Insufficient stock, invalid piece quantities, missing setup, or absent confirmation leaves the order pending and stock unchanged. The staff user cannot bypass confirmation by directly posting the request. Repeated or concurrent acceptance cannot deduct the same order twice. Changing its phase later, recording payment, editing its weight, or completing it does not deduct again. Own-product/no-product bookings require no shop-supply checkbox, and mixed-source orders deduct only their shop-product lines.

For an older active order without an issue marker, **Update Status** offers an optional confirmation for supplies being issued now. Leaving it unchecked permits a phase change without inventing historical usage. Checking it records the saved shop pieces once, including when keeping the same phase. Do not confirm pieces already used before inventory tracking, and do not create a duplicate walk-in for an online booking. Already-recorded usage is visibly labeled on active cards.

Saved Powder/Downy service quantities are the source of the deduction; human-readable notes are not parsed as inventory data. This change does not add supply-quantity editing or automatic retrospective deductions. Staff should confirm only when the displayed quantities match the pieces actually being issued. Completed and cancelled orders cannot be reopened through Accept or Update Status; pending orders must use Accept & Start first.

## Stock movements and permissions

| Action | Owner | Staff | Effect |
| --- | --- | --- | --- |
| View balances and history | Yes | Yes | Read-only view of both products and the latest 100 movements. |
| Initialize inventory | Yes | No | Creates missing inventory structures without resetting balances. |
| Add Stock | Yes | No | Adds received pieces with a reason/reference. |
| Save a walk-in using shop supplies | Through Staff workflow only | Yes | Deducts the saved piece quantities once, in the new order's transaction. |
| Accept and start an online booking | No | Yes | Confirms actual issuance and deducts saved shop pieces once with the status change. |
| Record current issuance on an older active order | No | Yes | Optional confirmation within Update Status; no automatic historical deduction. |

Customer accounts cannot access these inventory actions. Mutating forms use antiforgery validation and server-side role checks. Stock In and walk-in forms also use session request tokens. Online processing serializes changes on the booking row and uses the unique inventory issue marker to prevent duplicate deductions. The acting user comes from the session, not from a submitted user ID.

Cancelling an order does not automatically restore stock: the products might already have been used. The simplified page has no return or physical-count adjustment control. It also does not change bills or payments. These removed controls are not accessible through their old controller actions.

Every movement retains its time, product, quantity change, resulting balance, kind, optional booking reference, recorded-by user, and reason. Used staff accounts cannot be deleted if foreign keys protect their inventory history; the owner receives a message to deactivate the account instead.

## Database additions

| Table | Purpose and key fields |
| --- | --- |
| `inventory_stock` | One balance per service ID; whole-piece quantity and update time. |
| `inventory_booking_issues` | Booking ID as primary key ensures one issue marker per booking; stores issuer and issue time. |
| `inventory_movements` | Append-only application ledger; current actions create Restock and confirmed online/walk-in Issue entries. Historical Count and Return entries remain readable. Stores signed change, balance after, actor, reason, optional booking, and unique request key. |

The stock row is locked during updates. Issuance also locks the booking and locks multiple supply rows in service-ID order. Booking creation/issuance uses READ COMMITTED transactions to avoid unnecessary missing-row gap locks. Future endpoints that edit existing booking service lines must lock the booking first.

Existing bookings, users, payments, service prices, and financial reporting are not rewritten by inventory initialization. The inventory tables use restrictive deletion relationships so audit history cannot silently disappear when a linked record is deleted.

## Initial version verification on September 30 2026

The checks below describe the initial version before scope reduction. Checks involving count corrections, manual issue actions, and returns are historical results, not claims that those removed features remain available. The live setup observation is also from that earlier check, not a claim about the user's current database.

- Application build passed with **0 warnings and 0 errors**, using separate temporary build output because the normal intermediate assembly was locked by a compiler process.
- **41 isolated regression checks passed**: zero initialization, repeat initialization, validation, exact piece deduction, insufficient-stock rollback, customer-owned supplies, rejected inactive booking states, bounded returns, count corrections, stored actors, server-calculated walk-in charges, and repeated walk-in requests.
- **21 isolated concurrency and authorization checks passed**: ten simultaneous issues for different booking pairs, competition for the final piece, duplicate issuance, repeated stock-in, competing returns, duplicate concurrent walk-ins, and forbidden Staff/Customer actions.
- Rendered Owner/Staff inventory and desktop walk-in views were visually checked with synthetic models. **14 browser-executed walk-in JavaScript checks passed** for piece-based totals, customer-provided supplies, quantity and stock bounds, renewed issuance confirmation, Rush Fee, and runtime errors. These fixture checks did not submit real orders or use customer data.
- The Inventory page also fit a 390-pixel emulated viewport without page overflow; its wider ledger scrolls within its own container. The temporary preview server and isolated browser processes were stopped after verification.
- Tests used only `ezk_inventory_test_20260930_06576700`, a separate synthetic database. It was left available for inspection; no live records were changed. Test programs and build outputs are temporary development artifacts, not application routes.
- A read-only live metadata check confirmed compatible InnoDB parent tables and foreign-key types, existing Powder/Downy PerItem catalogue entries, and no inventory tables yet. Setup must still be run on the actual installation.
- These are database/controller tests, not a claim of completed physical stocktaking or authenticated browser acceptance testing on the running installation. Antiforgery attributes were reviewed but are not exercised by direct controller calls.

## Scope reduction verification on September 30 2026

These checks describe the removal pass before the subsequent online integration below.

- The simplified application compiled with **0 warnings and 0 errors**, including the updated Razor view, into separate build output without restarting the user's running app.
- **25 isolated checks passed** against the newly built assembly: remaining MVC actions, removed store APIs, retained walk-in deduction method and antiforgery filter, non-owner restrictions, invalid stock-entry validation, and HTTP routing.
- GET and POST requests to the removed `AdjustStock`, `IssueBooking`, and `ReturnSupply` routes returned 404. Unauthenticated access to Inventory still redirected to Login.
- Source review confirmed that the Inventory view contains only Setup and StockIn forms, and the only remaining caller of `InventoryStore.IssueBooking` is the walk-in save workflow.
- These checks opened no database connections and changed no live stock, booking, payment, or history records. Successful database stock-in and walk-in transactions were not rerun in this removal pass; initial-version results above are historical. The temporary HTTP test host was stopped after the checks.

## Online integration verification on September 30 2026

- The final application build passed with **0 warnings and 0 errors**.
- **43 isolated checks passed**: exact online deductions, required acceptance confirmation, duplicate/concurrent submissions, competition for the last pieces, full rollback on insufficient stock, own/mixed sources, missing inventory setup, older active orders, walk-in compatibility, phase changes, cancellation, completion, payment and weight changes, actor recording, and role restrictions.
- The compiled Staff Razor view rendered through a temporary local HTTP host with synthetic models. Checks confirmed quantity labels, required pending confirmation, optional older-order confirmation, issued/no-duplicate labels, absent own-product checkboxes, and antiforgery fields.
- Tests used only the separate synthetic inventory test database, not live records. The temporary HTTP host was stopped. The user's running app was not restarted.
- Browser automation could not start because its runtime file was unavailable. HTML rendering and controller/database behavior were checked, but visual browser layout and authenticated end-to-end clicks on the user's installation remain unverified.

## Acceptance checks after restart

Use a separate test installation for transactions that should not appear in real business records.

1. Initialize inventory and enter a known test stock count. Confirm both balances and Stock In history.
2. Create a walk-in with two shop Powder pieces and one shop Downy piece. Confirm the price uses those quantities and the stock reduces by exactly those amounts.
3. Create a customer-provided/no-supply order and confirm no stock changes or shop-product charges.
4. Try more pieces than are available. Confirm no booking is created and the form retains the entered details for correction.
5. Resubmit the same walk-in request; confirm no second booking or deduction.
6. Confirm the manual issue, unused return, and physical-count adjustment forms are absent, and their old actions are unavailable.
7. Cancel an issued order and confirm stock does not automatically increase. Confirm existing movement history has been preserved.
8. Confirm Staff cannot add deliveries, and Customer cannot access inventory.
9. Submit an online booking with two shop Powder pieces and one shop Downy piece. Stock must remain unchanged until Staff checks the issuance confirmation and clicks Accept & Start. Verify exact deductions and the booking reference in history.
10. Repeat acceptance and change phase; stock must not reduce a second time. Try acceptance with insufficient stock and confirm the order stays pending with no partial movement.
11. Verify own-product/no-product acceptance has no stock deduction. For older active orders, an unchecked issuance box must not backfill usage when changing phase.

## Remaining limits

The customer booking page does not reserve stock or promise availability. Stock availability is enforced when staff confirms issuance in online processing or saves walk-ins using shop supplies. Older active orders are not backfilled automatically; their stock history may remain incomplete for usage before this feature. There is no current UI for changing saved online supply quantities, correcting a mistaken stock entry, or returning unused supplies. The history screen displays the latest 100 movements; filtering, exports, and a dedicated inventory print report are not part of this change. There are no expiry dates, batches, automatic low-stock thresholds, restock costs, or kilogram-based dosing rules.

The application's existing session-only role checks do not recheck a staff account's current database activation status on every request. This is an inherited authentication limitation, not solved by this inventory update. Schema creation still requires the configured database account to have the necessary permissions.

## Implementation files

- `Data/InventoryStore.cs`
- `Models/InventoryModels.cs`, `Models/InventoryPageVM.cs`, and `Models/WalkInFormModel.cs`
- `Controllers/InventoryController.cs` and `Controllers/StaffController.cs`
- `Controllers/OwnerController.cs` for safe handling of deletion blocked by recorded history
- `Views/Inventory/Index.cshtml`, `Views/Staff/Index.cshtml`, and the Owner sidebar in `Views/Owner/Index.cshtml`
- `migrations/20260930_piece_inventory.sql` and the additive inventory section of `laundry_final_3nf.sql`
