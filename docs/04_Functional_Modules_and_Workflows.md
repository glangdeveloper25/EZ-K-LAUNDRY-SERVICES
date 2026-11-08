# Functional Modules and Workflows

Documentation baseline: **30 September 2026**. This chapter describes the source code inspected on that date, not an assertion that every screen has passed end-to-end testing. Feature completion dates belong in the development timeline; this chapter does not invent dates for earlier work.

## 1. Users and navigation

EZ-K Laundry System separates customer self-service, staff operations, and owner administration. Each role has a server-rendered dashboard. Most sidebar items switch visible sections within the same dashboard page through JavaScript; they are not independent pages with their own refresh cycle.

| Role | Main entry points | Responsibilities represented in source |
| --- | --- | --- |
| Customer | `/Account/Register`, `/Account/Login`, `/Customer` | Register, prepare and review bookings, declare GCash payment or select Cash on Pickup, view active orders/history, read notifications, maintain profile/password, request reward redemption |
| Staff | `/Account/StaffLogin`, `/Staff` | Approve customer accounts, accept bookings, update processing status, record actual weight, record/verify payments, process walk-ins, inspect pickup information |
| Owner | `/Owner/Login`, `/Owner` | Manage staff, inspect laundry operations/customer records, set service prices, view monthly income and transaction reports, print reports |

The Owner setup screen `/Owner/RegisterShop` is available only while no Owner exists according to the controller check. It creates an Owner account, not a complete multi-branch/shop configuration record.

Customer navigation retains Book now in the left sidebar. The former duplicate top-right Book now button is not present in the current customer dashboard. The Owner's Add New Staff control is in Staff Management, not in the overview. Current dashboards use the shared local laundry logo asset with role-specific layout styling.

Source: [AccountController](../Controllers/AccountController.cs), [OwnerController](../Controllers/OwnerController.cs), [Customer view](../Views/Customer/Index.cshtml), [Staff view](../Views/Staff/Index.cshtml), [Owner view](../Views/Owner/Index.cshtml).

## 2. Account registration, approval, and access

### 2.1 Customer registration

`AccountController.Register` validates first and last name, email, an exactly 11-digit contact number, a strong password, and matching confirmation. Names accept letters, spaces, apostrophes, and hyphens. Email or phone already used by another user is rejected. Passwords are passed to `PasswordSecurity.HashPassword`; plaintext passwords are not the intended stored format.

Successful registration creates a Customer with account status **Pending**. This is account approval status, not booking status. Registration therefore does not immediately grant an active customer session. Staff sees pending customer accounts and uses `ApproveCustomer` to change the account to Active. The approval action currently checks the target role, but is not restricted in its SQL to a previously Pending account.

The registration view contains password visibility controls. They change the input between masked and readable display; they do not change how the password is stored. Password policy details and deployment/security qualifications are covered in the architecture/security chapter.

Source: `Register`, `Authenticate` in [AccountController](../Controllers/AccountController.cs); [CustomerRegistration](../Models/CustomerRegistration.cs); [registration view](../Views/Account/Register.cshtml); `ApproveCustomer` in [StaffController](../Controllers/StaffController.cs).

### 2.2 Login and session

Login reads the user's password hash, role, and account status. Pending and non-Active accounts cannot establish a new login session. Successful login sets `UserId`, `UserRole`, and `UserName`, then redirects to the appropriate role dashboard. The Staff login additionally requires the Staff role. The Owner login separately checks Owner role and Active status.

Customer and Staff actions check both role and a session user ID. Owner actions use a role check. Logout clears the session. These are application-specific session guards; do not describe them as an implemented ASP.NET Identity or external single-sign-on system.

### 2.3 Customer account maintenance

`UpdateProfile` updates the logged-in customer's name and contact number and refreshes the session display name. This endpoint has basic nonblank checks, but does not apply all registration validations again; duplicate phone conflicts and full profile validation require testing.

`ChangePassword` checks confirmation, strength, the current password, and whether the new password differs from the existing one before saving a new hash. Neither screen constitutes an email-based forgotten-password/reset workflow.

## 3. Customer booking: draft before submission

The central distinction is between a **draft order in the session** and a **persisted booking in MySQL**. Opening Book now, reviewing an order, or viewing the payment QR screen must not be documented as creating a booking; the code has later persistence points.

### 3.1 Available choices and server validation

The customer booking form presents Washing, Drying, Folding, and Self Service. Multiple services may be selected. Estimated weight is initially 5 kg, with server acceptance from 1 through 20 kg. Optional extras are Powder Soap, Downy Softener, Rush Fee, and Delivery. Powder and Downy quantity must be from 1 through 20 when selected. Payment selection is GCash or Cash on Pickup, submitted as `GCash` or `Cash`.

Self Service is retained. It is treated as a per-kilogram service in the owner price setup and is disabled for customer selection when its loaded price is not positive. This is a bookable service option, not a machine reservation or machine-control subsystem.

Prices come from active rows in `services`. `CreateBookingDraft` looks up each selected name and obtains its ID, charging basis, and current price from the database. It does not trust a browser-supplied total. PerKg quantity becomes the estimated weight, selected PerItem extras use their entered quantities, and flat services use quantity 1. An unavailable service prevents creation of the draft.

Source: `ConfirmBooking`, `CreateBookingDraft` in [CustomerController](../Controllers/CustomerController.cs).

### 3.2 Review, edit, and cancel

Despite its name, `ConfirmBooking` is the first POST from **Review Order** and stores a JSON draft in the session under `CustomerBookingDraft`. It then redirects to `ReviewBooking`. The review page lists only the draft's selected service lines, weight, payment method, and estimated total, together with a warning that measured weight can affect the final amount.

**Edit Order** returns to the booking form with draft selections restored. The customer can revise services, weight, extras, quantities, and method and review again. **Cancel Booking** clears the draft without inserting a booking. The QR screen also provides a return-to-review link and a draft-cancel action. Merely using browser Back does not itself call cancellation; it navigates browser history. Nevertheless, no saved booking exists until the corresponding final submission below.

The draft is session-bound. A missing/expired draft on the review or QR GET route silently redirects to the booking form; the final-submission POST routes also set an expiry/error message. It is not a permanent saved cart. Multiple open browser tabs share the same session draft, so simultaneous booking edits require specific testing.

Source: [ReviewBooking view](../Views/Customer/ReviewBooking.cshtml); `ReviewBooking`, `CancelBookingDraft`, `GetBookingDraft`, and `Index(editBooking)` in [CustomerController](../Controllers/CustomerController.cs).

### 3.3 Payment method determines the final submission point

| Step | Cash on Pickup | GCash |
| --- | --- | --- |
| Review Order | Session draft only | Session draft only |
| Confirm from review | Creates the booking | Opens QR/payment instruction page; still draft only |
| Final payment-page action | Not applicable | I've Made My Payment creates booking and Pending payment declaration |
| Initial booking status | Pending staff acceptance | Pending staff acceptance |
| Payment recorded as received automatically? | No | No |
| Next financial action | Staff records money actually received | Staff independently verifies payment, then records it as Paid |

`CreateBookingFromDraft` uses a database transaction to insert the booking, one Clothes item with weight, selected service lines with saved unit prices, and initial booking-status history. GCash additionally inserts a payment row whose status is Pending and whose amount is the draft total. Cash does not create a payment row at this point.

The booking code contains an `EZK-` prefix, UTC date component, and random suffix. A unique code and duplicate-key recovery attempt reduce repeated insertion of the same draft. This is not evidence that all double-click, concurrent-tab, or network-retry cases have been acceptance-tested.

### 3.4 Important GCash qualification

The current QR is generated from the literal sample text `EZKLaundryPartialPay` by an external QR-image endpoint. It is not verified merchant payment integration. The screen says **Scan to Pay Partial**, and the controller is named `ProcessPartialPayment`, but the screen has no entered partial amount and the inserted Pending amount is the entire estimated total. No GCash transaction reference entry, proof upload, payment-provider callback, or automatic payment verification is implemented in this flow.

Consequently, the safe description is **customer payment declaration followed by manual staff verification**. Do not claim that clicking I've Made My Payment transfers funds or proves that funds were received. A real merchant QR and an agreed partial-payment policy are outstanding integration/design work.

Source: [PaymentQR view](../Views/Customer/PaymentQR.cshtml); `ProcessPartialPayment` and `CreateBookingFromDraft` in [CustomerController](../Controllers/CustomerController.cs).

### 3.5 Booking receipt

After successful persistence, the controller records the last booking ID/code/method in session and clears the draft. The receipt queries the saved booking, item weight, and selected service lines, scoped to the logged-in customer. It shows line totals and the estimated order total and offers Print Receipt.

This is a booking acknowledgment, not proof of a verified payment. Its payment-method text comes from the last-booking session value rather than a permanent booking preference field. Its text still says estimated weight/total and pending staff acceptance even if the underlying booking later progresses. It is not a general receipt-history lookup endpoint; it depends on `LastBookingId` in the session.

Per-item quantities are included in calculations, but the review/receipt's non-PerKg detail currently shows unit price without explicitly displaying the quantity multiplier. For more than one soap/softener item, inspect whether the customer can understand the line total clearly.

## 4. Staff booking and laundry operations

### 4.1 Queues and lifecycle

The Staff dashboard separately loads Pending bookings and an active queue of Washing, Drying, Folding, and Ready bookings. Accepting a customer booking sets it to Washing and adds status history. Processing-status updates write the new current status and a corresponding history row in a transaction. Completed and Cancelled records remain in historical records but disappear from these active queues.

The normal intended sequence is Pending, Washing, Drying, Folding, Ready, and Completed, with cancellation as an alternative terminal result. Current enforcement is less strict than that sequence: the general `UpdateStatus` action accepts a whitelist including Completed and Cancelled and calls a helper that does not validate the prior state. Separate `CompleteOrder` and `CancelOrder` actions do impose stricter rules: completion from Ready only, and cancellation from a listed nonterminal state with a 3–150 character reason. Those stricter rules are not enforced uniformly by every status-changing route.

The active-card dropdown presently offers Washing, Drying, and Ready, not Folding, even though Folding exists in the schema, controller whitelist, and customer progress display. Acceptance always starts at Washing even for a booking that did not select Washing. These are workflow limitations, not service charges added automatically by the tracking graphic.

Source: `AcceptBooking`, `UpdateStatus`, `ChangeBookingStatus`, `CompleteOrder`, `CancelOrder`, `ChangeTerminalStatus` in [StaffController](../Controllers/StaffController.cs); [Staff view](../Views/Staff/Index.cshtml).

### 4.2 Recording actual measured weight

Staff has Record / Edit Actual Weight controls on relevant booking views. The modal shows current weight, current total, paid amount, and a projected total. On submission `UpdateActualWeight`:

1. Requires a Staff session and positive weight no greater than 999.99 kg.
2. Locks the booking and rejects missing or terminal bookings.
3. Selects and locks the first Clothes item belonging to that booking.
4. Calculates the new amount using actual weight for each PerKg line and existing quantities for other lines.
5. Rejects a projected total lower than the already-Paid amount, within the small comparison tolerance used by the code.
6. Updates the item weight and the quantities of PerKg service lines while preserving their saved unit prices.
7. Adds an audit-style status-history note with old/new weight and recalculated total, creates a customer notification, and commits the transaction.

Example only: Washing at PHP 25/kg plus Drying at PHP 25/kg and one PHP 10 softener has a PHP 260 estimate at 5 kg. At measured 6 kg the new total is PHP 310. The softener remains PHP 10. These are illustrative rates, not a statement of the current shop price list.

The schema currently uses the same item weight column for the estimate and later actual weight. There is no separate immutable estimated-weight field, measured-weight field, or explicit weight-verification flag. Previous values survive in the text history note, not as separately queryable weight columns. Multiple laundry-item weighing is not implemented by this action. A pending GCash declaration is not automatically rewritten by a weight change; staff must verify the amount genuinely received.

### 4.3 Payment recording and balance

`RecordPaymentCore` accepts numeric booking ID, `BK-<id>`, `#BK-<id>`, or full EZK reference. It verifies positive amount and Cash/GCash method, locks the matching booking, rejects Cancelled bookings, totals saved service lines, and subtracts only payments with status Paid. It refuses already-settled bookings and amounts above the remaining balance.

For GCash, the earliest Pending GCash row is updated to Paid if one exists; otherwise a new Paid row is inserted. The verified amount, recording Staff user, and paid timestamp are saved. Cash inserts a Paid row. If cash settles the remaining balance, outstanding Pending GCash declarations are marked Failed with a superseded-by-cash note. A payment notification is inserted for the customer in the same transaction.

Actual installments are supported as multiple received payment records. Each received installment is stored as **Paid**, even when the order still has a balance. The booking's payment balance must therefore be distinguished from an individual payment row's status. The controller does not use the Partial status for these installments.

Payment entry is a staff attestation, not payment-provider confirmation. A balance guard does not prevent every duplicate partial entry: a repeated smaller payment can still fit within the remaining balance. Independent transaction-reference validation and complete retry/idempotency coverage remain unverified. Completing laundry does not automatically record payment, and the completion action does not enforce zero remaining balance.

### 4.4 Walk-in processing and fulfillment

`ProcessWalkIn` finds a user by phone or creates an Active walk-in Customer, creates a Washing booking attributed to Staff, records Clothes weight and selected service lines, and adds initial history. It recalculates prices server-side instead of trusting the submitted total. It does not create a received payment automatically.

A known source mismatch exists: walk-in JavaScript sends `Soap Powder` and `Downy Softener`, while the pricing/database lookup names are `Powder` and `Downy`. The helper returns zero for an unavailable service name rather than rejecting the entire order. This can omit the add-on charge and requires correction/testing before declaring walk-in extras complete. Customer add-ons use the correct lookup names.

View Pickup Method reads nonterminal bookings and displays method, schedule, contact, and location. However, selecting Delivery Fee in the current customer form only adds a charge; booking creation does not also populate delivery address, pickup schedule, or fulfillment method. A displayed fee is not evidence of a complete delivery-request workflow.

## 5. Customer tracking, history, notifications, and rewards

### 5.1 Active tracking and history

`CustomerController.Index` retrieves all the logged-in customer's nonterminal bookings and renders one tracking card per booking. Pending bookings show Waiting for Staff acceptance. Washing, Drying, Folding, and Ready map to fixed progress indicators. The indicators are not dynamically generated from the customer's selected services.

Data is loaded when the dashboard request runs. Sidebar switching only reveals already-rendered sections; it does not query the server again. There is no polling or push channel in this view. Accordingly, document this as **database-backed status tracking refreshed on page load**, not guaranteed instantaneous/live synchronization. A customer with an already-open page must reload to see staff changes.

History lists the customer's bookings, including completed and cancelled records, newest first. The column labelled Payment is the computed service-line order total, not the amount received or balance due. That naming matters when explaining why history can contain an amount while the income report remains empty.

### 5.2 Notifications

Notifications now come from the customer's own `notifications` rows, newest first, limited to 100. Cards show title, message, relative time (or an exact date for older entries), type styling, and read/new appearance. Mark all as read updates the current customer's unread rows and returns to the Notifications section.

Actual-weight updates and payment recordings explicitly create notifications. A generic Staff `SendNotification` action can insert a standard message for all Active customers; its existence should not be described as a polished custom-message editor. Ordinary processing-status changes and customer account approval do not insert notification rows in their current controller paths.

Notification cards are display blocks, not openable detail links. There is no per-card read action, detail page, or automatic push delivery. The unread badge counts unread entries within the most recent 100 loaded records; Mark all as read updates all matching unread database rows, not only those 100.

### 5.3 Rewards: partial implementation

The redemption endpoint checks the sum of `reward_transactions.points`, rejects an insufficient balance, and inserts a negative Redeem transaction. Free Wash costs 800 points; other submitted reward names currently follow a 200-point branch.

However, the displayed 1,250 balance, membership/progress, earning examples, and points-history entries are static view content. No automatic earning rule is called from current booking completion/payment recording, and redemption is not connected to a discount applied to a booking. Describe rewards as a partially implemented module, not a completed loyalty program.

## 6. Owner staff, records, and pricing administration

Staff Management lists staff accounts and provides Add New Staff, activation/inactivation, and deletion actions. New staff requires all account fields, matching strong passwords, and a unique email/contact. Deletion physically deletes the Staff user; it is not merely deactivation. Preserve audit needs when choosing between those actions. Existing-session revocation after a status change is not enforced by a fresh account-status query on each request.

Laundry Operations shows all saved bookings with customer, services, amount, and processing status. Browser-side pagination shows 22 rows per page; all rows are still loaded by the controller. Customer Records lists customer contact records and opens each customer's booking history modal. Both are operational records, distinct from the payments-based Transaction Report.

Service Pricing updates Washing, Drying, Folding, Self Service, Powder, Downy, Rush Fee, and Delivery Fee. Negative values are rejected. Self Service is inserted or updated as an active PerKg service. Other prices update existing rows. Price updates are separate commands without one surrounding transaction. Existing booking lines retain saved unit prices, while new drafts read current service prices. Editing/reviewing a draft again refreshes its service lookups; an already-stored draft otherwise retains its saved price values.

## 7. Dashboard numbers that require careful documentation

| Display | Current source behavior | Qualification |
| --- | --- | --- |
| Customer Active Order | Nonterminal bookings owned by customer | Database-backed |
| Customer Finished Laundry | `History.Count` | Counts all history, not only Completed |
| Customer EZ-K Points | Literal 1,250 | Placeholder; not ledger balance |
| Staff Today's Orders | `ActiveQueue.Count` | Not filtered to today's creation date |
| Staff Pending Bookings | Pending booking count | Database-backed |
| Staff Cash Collection Today | Paid Cash sums for database current date | Received cash only, not all-method revenue |
| Owner Total Earnings | All Paid payment amounts | Gross recorded receipts, not profit |
| Owner Active Orders | Entire `BookingList.Count` | Includes terminal bookings |
| Owner Rush Orders | Literal +5 | Placeholder |
| Owner Active Staff | Non-Pending Staff list count, displayed against 15 | Includes inactive staff; denominator is static |

The staff shift panel and audio-alert switch also need functional verification; visible controls/text alone do not establish attendance recording or automated audible notifications. Inventory stock receiving, usage deduction, dosage by weight, low-stock alerts, and stock-cost reports are not implemented in the examined workflow. They remain a separate proposed enhancement.

## 8. Documentation and demonstration boundary

This chapter establishes what the code is designed to do and where its current limitations are. It does not certify successful live GCash collection, completed security testing, all printer/browser combinations, correct historical production data, or a finished inventory module. Use the test plan for reproducible acceptance checks, and use the reporting chapter for the distinction between orders, payments, and printed output.
