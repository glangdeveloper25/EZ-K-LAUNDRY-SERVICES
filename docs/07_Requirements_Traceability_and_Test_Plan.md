# Requirements Traceability and Acceptance Test Plan

**System:** EZ-K Laundry System  
**Source-review date:** 30 September 2026  
**Purpose:** Connect the requested changes to the current implementation and provide repeatable tests for demonstration, manuscript evaluation, and remaining Gantt-chart activities.

## 1. Evidence and result conventions

This chapter is based on a read-only inspection of the application source and SQL schema. The tests below are **planned tests, not executed test results**. No customer bookings, payments, prices, or inventory records were changed to prepare this document.

Use these result labels when testing:

- **Not run:** Steps have not been executed and recorded.
- **Pass:** Actual behavior matches the expected outcome, with supporting evidence.
- **Fail:** Actual behavior differs; record the precise difference and affected reference.
- **Blocked:** A prerequisite prevents execution, such as unavailable test database access.
- **Source finding:** A code-level issue was observed; this is not a claim that a browser test was performed.

An implemented controller action is not equivalent to an accepted feature. A successful build is not proof of correct payment totals, role isolation, browser printing, or end-to-end booking behavior. Screenshots show the displayed state at one moment and do not, by themselves, verify all underlying rules.

The expected results below describe the acceptance criteria. Where the current source has a known gap, the test explicitly says so rather than claiming it already passes. The companion [Known Limitations and Future Work](08_Known_Limitations_and_Future_Work.md) explains the findings.

## 2. Requirements-to-source traceability

| ID | Requested capability or rule | Implementation evidence | Current qualification | Acceptance tests |
|---|---|---|---|---|
| R01 | Separate customer, staff, and owner access | `AccountController.Authenticate`; session role guards in the three dashboard controllers | Session-based role checks exist; production hardening and active-session revocation need review | T01–T04 |
| R02 | Customer registration, validation, and staff approval | `CustomerRegistration`; `PasswordSecurity`; `AccountController.Register`; `StaffController.ApproveCustomer` | Pending customers cannot log in until activated | T01–T03 |
| R03 | Password visible/hidden eye buttons | `Views/Account/Register.cshtml`, `togglePasswordVisibility` | Registration password and confirmation fields have separate toggles | T05 |
| R04 | Remove duplicate Book now button; retain sidebar navigation | `Views/Customer/Index.cshtml` navigation and dashboard heading | Booking remains a customer sidebar section | T06 |
| R05 | Place Add New Staff in Staff Management | `Views/Owner/Index.cshtml` staff section and add-staff modal | Account creation remains owner-only through `AddStaff` | T06, T07 |
| R06 | Customer selects core services and optional add-ons | `CustomerController.ConfirmBooking` and `CreateBookingDraft`; customer booking view | Washing, Drying, Folding, Self Service; Powder, Downy, Rush Fee, Delivery Fee; zero-total handling remains a gap | T08–T10, T45 |
| R07 | Keep Self Service and allow owner pricing | `OwnerController.UpdatePricing`, `UpsertSelfService`; customer service selection | Customer booking requires a positive Self Service price | T10, T11 |
| R08 | Review and edit before creating a booking | Session draft; `ReviewBooking`; `ConfirmReviewedBooking` | Review/edit are draft operations; final persistence differs by payment method | T12–T15 |
| R09 | Do not create GCash booking merely by opening the QR screen | `PaymentQR`; `ProcessPartialPayment`; `CreateBookingFromDraft` | Booking is created on payment declaration, not on real gateway confirmation | T14–T16 |
| R10 | Cash on Pickup booking is separate from cash collection | Cash branch of `ConfirmReviewedBooking`; staff `RecordPaymentCore` | Cash booking does not automatically insert a paid payment | T13, T22 |
| R11 | Receipt shows the selected order, not sample details | `BookingReceipt` reads booking items/services for signed-in customer | Receipt is tied to the session's last booking; not a complete payment receipt ledger | T17 |
| R12 | Display all active customer orders | `CustomerController.Index`, `ActiveBookings`; tracking-card loop | Status snapshot is loaded on page request; all cards use the same displayed stages | T18 |
| R13 | Staff accepts and updates laundry status | `AcceptBooking`; `UpdateStatus`; `ChangeBookingStatus`; status history | Generic status action is less restrictive than dedicated completion/cancellation actions | T19–T21 |
| R14 | Staff can record actual weight and recalculate | `UpdateActualWeight`; staff actual-weight modal | Uses saved unit prices; overwrites first Clothes item weight; logs prior weight in history notes | T26–T28 |
| R15 | Notify customer about weight and payment changes | Notification inserts in `UpdateActualWeight` and `RecordPaymentCore` | Read on refresh; no notification detail-opening action or push delivery | T29–T31 |
| R16 | Record cash and manually verify GCash | `RecordPaymentCore`; normalized booking references | Validates balance and locks booking; external GCash authenticity is manual | T22–T25 |
| R17 | Monthly income is based on recorded payments | `OwnerController.LoadIncomeReport` | Paid-only collection summary, not profit; monthly rows require non-null `paid_at` | T32–T34 |
| R18 | Owner can filter transactions by period/date/method/status | `TransactionReportFilter`; `LoadTransactionReport`; report filter form | Server filters exist; date/month/year fields have a visibility defect in current CSS/JavaScript | T35–T38 |
| R19 | Print current filtered results or all results without filters | `printTransactionReport`; owner print CSS | Prints the server-rendered result set; browser and long-table layout acceptance pending | T39–T40 |
| R20 | Printable history, receipt, operations, monthly report, and customer records | Customer print actions; owner print actions and targeted print CSS | Operations print overrides pagination to show all loaded rows | T17, T40–T42 |
| R21 | Google-style previous/next operation pagination, 22 rows | Owner operations table pagination JavaScript | Client-side pagination; all orders are still loaded by server | T41 |
| R22 | Owner prices flow to bookings without changing old unit prices | `services.current_price`; draft prices; `booking_services.unit_price` | Price snapshot taken at review; final confirmation does not revalidate changed master prices | T11 |
| R23 | Walk-in booking and service total | `ProcessWalkIn`; `AddBookingService`; walk-in JavaScript | Soap/Downy labels do not match seeded service names; fix and retest required | T43 |
| R24 | Accurate dashboard counts and reward balance | Owner/customer/staff views; `RedeemReward`; `reward_transactions` | Several metrics are placeholders or count a broader set than their labels | T44 |
| R25 | Basic supply inventory and usage per order | Discussion only; not implemented in inspected schema/controllers | Paused at user's request; do not mark as delivered | Future-work approval |
| R26 | Actual changes preserved for Gantt chart and manuscript | This documentation package and source references | Exact historical completion dates must come from dated evidence, not assumed from feature order | Documentation review |

## 3. Safe test setup

Use a **separate test database and test accounts**, not a live operational database. Before changing pricing, inserting test data, or trying cancellation/refund scenarios, obtain the project owner's approval and take a restorable backup. Do not submit real GCash transfers to the prototype QR page.

Suggested fictional accounts:

| Test identity | Purpose |
|---|---|
| Test Owner | Pricing, staff management, reports |
| Test Staff A and Test Staff B | Processing, payment verification, concurrency checks |
| Test Customer A | Booking owner and notification recipient |
| Test Customer B | Cross-customer isolation checks |
| Pending Customer C | Approval and login rejection |

Use different browser profiles or private windows for each role so one login does not overwrite another session. Keep passwords out of screenshots, reports, and committed test fixtures.

### 3.1 Calculation fixture

The following rates are **test assumptions**, not a statement of current live prices:

| Service | Basis | Test unit price |
|---|---|---:|
| Washing | PerKg | PHP 25.00 |
| Drying | PerKg | PHP 25.00 |
| Folding | PerKg | PHP 10.00 |
| Self Service | PerKg | PHP 40.00 |
| Powder | PerItem | PHP 10.00 |
| Downy | PerItem | PHP 10.00 |
| Rush Fee | Flat | PHP 75.00 |
| Delivery Fee | Flat | PHP 40.00 |

**Fixture A:** 5.00 kg, Washing + Drying, Powder quantity 2, Downy quantity 1, Rush selected, Delivery not selected.

`(5 × 25) + (5 × 25) + (2 × 10) + (1 × 10) + 75 = PHP 355.00`

After actual weight becomes 6.50 kg:

`(6.5 × 25) + (6.5 × 25) + 20 + 10 + 75 = PHP 430.00`

If PHP 100.00 was already recorded Paid, remaining balance is PHP 330.00. Powder, Downy, and Rush amounts remain unchanged because they are not priced per kilogram.

### 3.2 Report fixture

For repeatable report testing, an authorized tester may prepare the following isolated payment records. Each must link to a valid fictional booking. Pending/failed/refunded rows do not contribute to the application's Paid totals. Some statuses need controlled fixture preparation because the current UI does not provide every lifecycle action.

| Record | Relevant report date | Method | Status | Amount |
|---|---|---|---|---:|
| P1 | 2026-09-29 | Cash | Paid | PHP 100.00 |
| P2 | 2026-09-30 | GCash | Paid | PHP 255.00 |
| P3 | 2026-09-30 | GCash | Pending | PHP 80.00 |
| P4 | 2026-10-01 | Cash | Paid | PHP 50.00 |
| P5 | 2026-09-30 | Cash | Failed | PHP 25.00 |
| P6 | 2026-09-30 | GCash | Refunded | PHP 30.00 |

For Paid records, use `paid_at` as the relevant date. For unpaid records with null `paid_at`, the transaction report uses `created_at`.

- All six transactions: count 6; Paid Cash PHP 150.00; Paid GCash PHP 255.00; Paid Total PHP 405.00.
- September 2026: count 5; Paid Cash PHP 100.00; Paid GCash PHP 255.00; Paid Total PHP 355.00.
- Specific date 30 September 2026: count 4; Paid Total PHP 255.00.
- September + Pending: count 1; displayed declared amount PHP 80.00; Paid Total PHP 0.00.
- September + Cash + Paid: count 1; Paid Total PHP 100.00.

The synthetic Refunded record illustrates filtering only. It does not mean an actual refund workflow is implemented or that subtracting refund rows produces net profit.

## 4. Manual acceptance cases

**Initial execution status of every case: NOT RUN.** Record actual results using the worksheet in Section 5. References to an expected rejection or known defect are source-review predictions, not completed runtime tests.

### T01 — Registration validation

**Preconditions:** Registration is accessible; fictional unique email and 11-digit phone are available.  
**Steps:** Submit missing names, malformed email, a short phone, a weak password, and mismatched confirmation in separate attempts; then submit valid data.  
**Expected:** Invalid requests do not create accounts. A valid account is created once with Customer role, Pending status, and a password hash rather than plaintext. Pending login is refused with an approval message.  
**Evidence:** Form validation screenshots and a sanitized database check of role/status/hash format; do not capture the password itself.

### T02 — Duplicate customer account

**Preconditions:** T01 valid account exists.  
**Steps:** Register a second account using the same email; repeat using the same phone with a different email.  
**Expected:** Duplicate attempts are rejected and the user count remains unchanged. Check the unique email/phone constraints as well as the friendly validation message. Simultaneous duplicate submissions should not create duplicates; unhandled constraint errors must be recorded as defects.

### T03 — Staff approval and account status

**Preconditions:** Pending Customer C and an authorized staff session.  
**Steps:** Approve Customer C from the staff dashboard; log in as that customer. In a separate test, try inactive staff login.  
**Expected:** Customer status becomes Active and login succeeds. Inactive staff cannot establish a new login session. Test and separately document whether an already-open staff session still works after the owner deactivates it; current guards only consult session values.

### T04 — Role boundaries and anti-forgery protection

**Preconditions:** Owner, staff, and two customer test accounts; only the test instance is targeted.  
**Steps:** While logged out, request each protected dashboard. As customer, request owner/staff pages and attempt their POST actions. Submit a state-changing request without its valid anti-forgery token. Compare Customer A and B history/notifications.  
**Expected:** Unauthorized actions do not mutate data; protected pages redirect to login; tokenless POST is rejected; one customer cannot retrieve another's private records. Record actual HTTP status and before/after counts without including session cookies in documentation.

### T05 — Independent password visibility

**Preconditions:** Customer registration form open.  
**Steps:** Type test values into Password and Confirm Password. Toggle the eye on each independently using mouse and keyboard.  
**Expected:** Each control changes only its own field between password and text, preserves the value, updates eye/eye-slash and accessible label, and does not submit registration. Passwords should remain hidden by default after a fresh form load.

### T06 — Navigation and requested button placement

**Preconditions:** Customer and owner dashboards accessible.  
**Steps:** Inspect customer dashboard heading and sidebar. Open Book now. On owner account, open Overview then Staff Management. Resize the viewport and navigate again.  
**Expected:** No duplicate top-right Book now button; sidebar booking still opens correctly. Add New Staff is available in Staff Management, not as an overview action. Labels, icons, and header spacing remain readable without covering content.

### T07 — Owner creates and manages staff

**Preconditions:** Test Owner; a new fictional staff email/phone.  
**Steps:** Try incomplete fields, weak password, mismatch, and duplicate email/phone; then add a valid staff account. Toggle it inactive and active. Only in a disposable fixture, test deletion with historical records.  
**Expected:** Invalid input is rejected; valid staff can log in; status controls affect the intended staff only. Historical bookings/payments are not deleted by staff deletion. Document the loss of recorder identity caused by nullable foreign keys rather than calling deletion a complete audit-preserving archive.

### T08 — Core services and add-on arithmetic

**Preconditions:** Calculation-fixture rates configured in isolated database.  
**Steps:** Enter Fixture A on customer Book now and open Review Order.  
**Expected acceptance target:** Review clearly identifies Washing 5 × 25, Drying 5 × 25, Powder 2 × 10, Downy 1 × 10, Rush 75, and total PHP 355.00. Folding, Self Service, and Delivery must not appear. No booking/payment row is inserted by review alone. **Current source gap:** Review and receipt display quantity multipliers only for PerKg lines. Non-PerKg lines show the unit price and calculated line total but omit their explicit quantity, so two Powder items are not clearly labeled as two. Check persisted arithmetic separately from this presentation acceptance target.

### T09 — Booking input boundaries

**Preconditions:** Customer booking form; server-side requests can be inspected safely.  
**Steps:** Try no core service; weights 0, 0.5, 1, 20, and 21; selected add-on quantities 0, 1, 20, and 21; unsupported payment method; inactive/unknown service.  
**Expected:** Customer estimate is limited to 1–20 kg and selected add-on quantity to 1–20. Invalid/unavailable selections are rejected before persistence. Test forged service lists containing only fee names too: the server currently validates existence, not a strict core-service allowlist, so record any accepted add-on-only request as a validation gap.

### T10 — Self Service preservation and price requirement

**Preconditions:** Owner pricing includes Self Service.  
**Steps:** Set its test price to zero and attempt customer booking; then set PHP 40.00 and review a 5 kg Self Service-only order.  
**Expected:** A zero/unavailable Self Service price prevents booking; valid price produces PHP 200.00 and an explicit Self Service line. Clarify with the shop whether combining Self Service with other core services is allowed; the current multi-select form does not enforce exclusivity.

### T11 — Price snapshot consistency

**Preconditions:** Fixture A reviewed or confirmed before an owner price change.  
**Steps:** Change Washing from PHP 25.00 to PHP 30.00. Reopen an already-created booking/receipt; confirm the existing draft in a separate test; create a fresh review.  
**Expected under current implementation:** Saved booking unit price remains PHP 25.00; an existing draft also retains its review-time price; a fresh review uses PHP 30.00. Confirm this policy with the adviser/shop. A price change should not silently reprice historical bookings. Record whether a warning/re-review is needed for stale drafts.

### T12 — Review, edit, and cancel before confirmation

**Preconditions:** Booking and payment counts captured for Customer A.  
**Steps:** Review Fixture A, return using Edit, change weight/services/quantities, review again, then use Cancel.  
**Expected:** Edit restores the draft fields; revised summary reflects the latest selections. Review and cancel create no booking/payment records and no staff Pending item. Session draft is cleared on explicit cancellation. Confirm data counts, not only the displayed success message.

### T13 — Cash on Pickup final confirmation

**Preconditions:** Valid Cash draft; baseline counts known.  
**Steps:** Review the order and perform final confirmation. Open staff Pending Bookings and owner report.  
**Expected:** One Pending booking with selected service lines/items/history is created. There is no QR requirement and no Paid payment. Staff can accept the order, but it does not increase collected income until money is recorded. A booking receipt is not proof of cash already collected.

### T14 — GCash QR navigation without submission

**Preconditions:** Valid GCash draft.  
**Steps:** Confirm review to open QR page; use Back to order review and browser Back in separate runs; finally use Cancel booking and go back.  
**Expected:** Merely viewing or leaving the QR screen does not create a booking or payment. Explicit cancellation clears the draft. The displayed QR is a prototype text payload, not an accepted live merchant payment integration; do not transfer real money during this test.

### T15 — GCash declaration and session expiry

**Preconditions:** Valid GCash draft and a second expired/no-draft session.  
**Steps:** Click I've Made My Payment once for the valid draft. Attempt the same endpoint without a valid draft in the expired session.  
**Expected:** Valid declaration creates one Pending booking plus one Pending GCash payment at the estimated total, not Paid income. Expired draft produces no booking and shows the session-expired message. Staff verification is still required.

### T16 — Duplicate final booking submissions

**Preconditions:** One fixed draft reference in an isolated test session.  
**Steps:** Double-click final confirmation or replay two concurrent valid confirmation submissions for that same draft. Then refresh the receipt.  
**Expected:** At most one booking with that code and one intended set of service/item/payment records exist. Source includes unique booking-code enforcement and duplicate-key recovery; record runtime behavior and any error response. This protection does not imply two intentionally separate drafts are the same order.

### T17 — Dynamic booking receipt and print

**Preconditions:** Fixture A confirmed by Customer A; another customer logged in separately.  
**Steps:** Open BookingReceipt, compare reference, weight, services, quantities, rates, and PHP 355.00 total with stored lines. Print preview to PDF. Try opening receipt without the appropriate last-booking session.  
**Expected acceptance target:** No sample 2024 reference, unselected add-ons, or unrelated customer's details appear. Print is readable and excludes navigation/actions. **Current source gaps:** Receipt has a `window.print()` button but no receipt-specific print CSS hiding its dashboard/print actions; clean-output acceptance is not established. Per-item quantities are not explicitly displayed, as noted in T08. Current design exposes the session's last booking, not arbitrary historical receipt selection or detailed payment installments.

### T18 — Multiple active tracking cards and refresh behavior

**Preconditions:** Customer A has Pending, Washing, Drying, Ready, Completed, and Cancelled fixtures.  
**Steps:** Load customer dashboard; update one active fixture as staff in a second browser; observe without reload, then reload customer page.  
**Expected:** All nonterminal active bookings are shown; completed/cancelled fixtures are excluded. Reload retrieves changed status. Do not mark instant push updates as passed: current code does not automatically refresh the already-rendered page. Record that stages are displayed uniformly even when services differ.

### T19 — Acceptance and status history

**Preconditions:** One Pending booking with selected services.  
**Steps:** Staff accepts it, then uses the available dropdown to change it to Drying and Ready. Read customer dashboard after refresh and inspect history. In a separate authorized controller-level test on a disposable booking, submit a valid anti-forgery-protected `UpdateStatus` request with Folding.  
**Expected:** Database current status, staff queue, refreshed customer card, and inserted history agree for supported transitions. **Current UI gap:** Staff dropdown offers Washing, Drying, and Ready, but not Folding; therefore a complete Washing-to-Drying-to-Folding-to-Ready UI test is blocked until that option/workflow is provided. Controller support for Folding is not proof of UI availability. Record notification behavior separately: the generic status-change helper currently does not create customer notification rows.

### T20 — Dedicated completion rule and generic-endpoint regression

**Preconditions:** One Washing and one Ready order; disposable test bookings only.  
**Steps:** Attempt dedicated CompleteOrder for Washing, then for Ready. Separately test whether UpdateStatus can set Completed directly or alter a terminal order.  
**Expected intended rule:** Completion is restricted to Ready orders and terminal records cannot be freely reopened. Dedicated completion enforces Ready; generic UpdateStatus currently lacks the same transition restrictions. Record the bypass as a defect if reproduced. Payment completion is a separate state and is not enforced by the current completion action.

### T21 — Cancellation with reason

**Preconditions:** Active and terminal booking fixtures.  
**Steps:** Submit cancellation reasons shorter than 3 or longer than 150 characters; cancel an active order with valid reason; repeat on completed/cancelled order.  
**Expected:** Dedicated CancelOrder rejects invalid reason and nonactive order; valid cancellation removes order from active lists and records reason/history. A previously Paid payment is not automatically refunded; do not assume cancellation reverses income.

### T22 — Payment reference formats and cash balance

**Preconditions:** Fixture A total PHP 355.00; no Paid payment.  
**Steps:** Record PHP 100.00 Cash using a supported reference form, then PHP 255.00 Cash using another. Repeat reference-parsing tests on separate fixtures using numeric ID, `BK-ID`, `#BK-ID`, and full EZK code.  
**Expected:** All supported formats resolve to the intended booking. First payment leaves PHP 255.00 balance; second leaves zero. Both actual receipts are Paid rows; the booking need not be Completed. Staff daily cash and owner paid reports include the recorded amounts on their payment dates.

### T23 — Payment validation and overpayment prevention

**Preconditions:** Booking has PHP 255.00 balance; cancelled and fully-paid fixtures also exist.  
**Steps:** Submit zero, negative, PHP 256.00, unsupported method, missing reference, cancelled booking, and fully-paid booking requests.  
**Expected:** None creates a new paid amount. Correct balance/message is shown. Validity is server-enforced, not merely HTML validation. The code uses a half-cent comparison tolerance; separately test cent rounding and actual stored DECIMAL amounts.

### T24 — Manual GCash verification and cash replacement

**Preconditions:** Pending GCash declaration exists for PHP 355.00.  
**Steps:** After independently validating a fictional transfer, record PHP 355.00 GCash. In a separate fixture with the same declaration, record full remaining balance by Cash instead.  
**Expected:** GCash verification updates the pending row to Paid with staff/date/amount; it should not create a second matching Paid row. Full cash settlement marks pending GCash declaration Failed so it is not later counted as another collection. Pending declaration alone never increases Paid Total.

### T25 — Concurrent/replayed payment requests

**Preconditions:** Two staff sessions and a PHP 100.00 balance.  
**Steps:** Submit concurrent PHP 100.00 payments; inspect resulting paid sum. Separately replay a PHP 20.00 payment while a larger balance remains.  
**Expected:** Paid sum must not exceed order total; booking lock/balance check should serialize the full-settlement case. The small replay may currently create another legitimate-looking payment because there is no request idempotency key. Capture that separately; do not describe overpayment prevention as complete duplicate-payment prevention.

### T26 — Actual weight recalculation and customer notice

**Preconditions:** Fixture A with PHP 100.00 Paid; active status.  
**Steps:** Staff records 6.50 kg through the actual-weight dialog. Reload staff/customer pages and inspect booking service quantities, history, and notifications.  
**Expected:** Total becomes PHP 430.00; Paid remains PHP 100.00; balance becomes PHP 330.00. PerKg quantities change to 6.50; fixed/per-item lines remain PHP 105.00 combined. History notes contain old/new weight and recalculated total. Only the affected customer receives the weight notification.

### T27 — Actual-weight boundaries and terminal records

**Preconditions:** Active and terminal fixtures with a Clothes item.  
**Steps:** Submit actual weights 0, negative, 0.01, 999.99, and 1000; attempt update on Completed/Cancelled booking and on a fixture missing Clothes. Separately test crafted requests for 0.001, 0.005, and 1.234 kg, bypassing only the browser's `step="0.01"` constraint in the isolated test instance. Compare stored weight/quantities, projected totals, notification text, and balance protection after database precision conversion.  
**Expected acceptance target:** Persisted weights follow an agreed positive two-decimal policy consistently, with totals based on that same persisted value. Terminal and missing-item updates are rejected without partial changes. **Current precision gap:** Server checks only `> 0` and `<= 999.99`, not a 0.01 minimum or two-decimal precision. Decimal weight/quantity columns store two decimals; values such as 0.001 may round to zero and violate a constraint, while other values may be rounded after the pre-save projected calculation. Record actual database behavior/error handling; do not assume HTML step validation protects crafted requests. Customer estimate limits (1–20 kg) differ from staff actual-weight limits; verify the policy.

### T28 — Weight reduction below collected amount and pending GCash

**Preconditions:** An active fixture whose proposed reduced total is below already-Paid money; a separate pending-GCash fixture.  
**Steps:** Attempt the reduction, then increase the second fixture's weight before verifying GCash.  
**Expected:** Below-paid reduction is blocked; weight/service/history/notification data remain unchanged. Increasing weight does not magically increase the declared GCash payment; verify only the actual received amount and leave remaining balance due. Test refund/credit needs separately because no automated refund adjustment exists.

### T29 — Notification recipient isolation and actual content

**Preconditions:** Customers A and B; staff weight/payment action on A's booking.  
**Steps:** Refresh both notification pages. Compare code, new weight/amount, message type, relative time, and unread marker.  
**Expected:** A sees the inserted message; B does not. Text reflects the actual saved action. Weight notification carries booking link data in storage, whereas the displayed card has no opening/detail behavior. Record this distinction rather than expecting click-through navigation.

### T30 — Mark all read and notification limit

**Preconditions:** Test customer has read/unread notifications, including a separate fixture exceeding 100 rows.  
**Steps:** Click Mark all as read; refresh. Inspect newest-first order and count.  
**Expected:** Own unread rows become read with timestamp; other users' rows do not change. Only newest 100 messages are currently loaded, and the badge counts unread within that loaded list. If an all-history unread badge is required, log the truncation as a requirement gap.

### T31 — No automatic notification/card opening claim

**Preconditions:** Customer notifications already open in one browser; staff action in another.  
**Steps:** Wait without reloading, switch sidebar sections, click an existing card, then reload.  
**Expected current behavior:** No automatic server fetch from waiting or section switching; no detail page from card click; new message appears after page reload. If instant delivery or clickable details is an acceptance requirement, this test remains a failed/unimplemented requirement until added and retested.

### T32 — No paid data versus completed bookings

**Preconditions:** Fresh isolated dataset contains bookings, including Completed, but zero Paid payments.  
**Steps:** Open owner overview and monthly report.  
**Expected:** Total earnings is zero and monthly report has no paid transactions. Completed laundry alone does not count as money received. Do not insert fake payments merely to fill the report; record a real test payment through the staff payment workflow when testing collection.

### T33 — Monthly Paid totals and date attribution

**Preconditions:** Report fixture P1–P6.  
**Steps:** Open owner monthly report; compare September/October Cash/GCash/Other totals to fixture arithmetic.  
**Expected:** September totals PHP 355.00; October PHP 50.00. Pending, Failed, and Refunded records contribute zero. Grouping uses `paid_at`, not booking date; a September booking paid in October belongs to October collection. Grand earnings PHP 405.00 agrees when all Paid rows have valid dates.

### T34 — Imported null payment dates and Partial status

**Preconditions:** Disposable fixtures include one Paid row with null `paid_at` and one payment-status Partial row.  
**Steps:** Compare Total Earnings, monthly totals, transaction totals, staff balance, and SQL `v_booking_payment_balances`.  
**Expected intended rule:** All definitions should reconcile or disclose differences. Current source does not: earnings includes Paid with null date; monthly excludes it; transaction date falls back to created date; SQL balance view includes Partial while application balance uses Paid only. Capture actual differences and treat policy harmonization as remaining work.

### T35 — All, Today, Yesterday, and specific-date transaction filters

**Preconditions:** Fixture dates known and application/database time zones recorded.  
**Steps:** Use All, Today, Yesterday, then Specific Date for 2026-09-30. Apply each filter. If needed, verify server filtering with the corresponding query parameters independently of the form.  
**Expected:** All returns six fixture rows; specific date returns four with PHP 255.00 Paid Total. Today/Yesterday follow the database's current date. The exact-date input must be visible when chosen; current CSS/JavaScript has a source-confirmed visibility issue, so do not mark UI acceptance passed without a fix/retest.

### T36 — Month, year, and custom-range filters

**Preconditions:** P1–P6 and records at date/time boundaries.  
**Steps:** Select September 2026, year 2026, and custom 29–30 September; include a row late on the end date and one at next-day midnight. Reverse start/end dates and repeat.  
**Expected:** Month/year/date fields are visible and usable. End date includes its full day; next-day midnight is excluded. Reversed dates are normalized by current controller. September produces five fixture rows/PHP 355.00. Field-visibility defect applies here too.

### T37 — Combined method/status filters and clear

**Preconditions:** P1–P6.  
**Steps:** Apply September + Cash + Paid, then September + GCash + Pending; click Clear/Reset.  
**Expected:** First filter has one row and PHP 100.00 Paid Total. Second has one PHP 80.00 Pending row but Paid Total zero. Reset returns All period/method/status and all six rows. Transaction count describes rows, not unique bookings; multiple payments on one booking produce multiple rows.

### T38 — Invalid filter values and empty result

**Preconditions:** Owner session.  
**Steps:** Submit unsupported period/method/status, out-of-range month/year, a date with no payments, and a booking with no payment row.  
**Expected:** Unsupported strings normalize to All; year is clamped 2000–2100 and month 1–12. Empty result displays a clear message and zero totals. Unpaid cash bookings without a payment record are absent from transaction rows; look in Laundry Operations for all bookings. Malformed date strings/model binding behavior must be recorded, not assumed.

### T39 — Print the applied filter and distinguish unapplied inputs

**Preconditions:** September + Cash + Paid result is already applied.  
**Steps:** Open print preview; compare printed scope, rows, count, and PHP 100.00 total. Change the dropdown without clicking Apply and preview again. Finally apply a new filter and print.  
**Expected:** Print matches the last server-rendered applied result, not a dropdown change that has never been submitted. Header includes the applied scope and printed date. Navigation, filter widgets, and buttons are absent. A user-facing clarification may be needed if unapplied controls create confusion.

### T40 — Unfiltered and long transaction report print

**Preconditions:** All filters reset; at least 50 fictional transactions, long customer/service text, multiple payment statuses.  
**Steps:** Save print preview as PDF on A4 landscape; inspect first, middle, and last pages and page counts. Test another supported browser where available.  
**Expected:** Every loaded transaction prints once, all nine columns remain visible, headers repeat appropriately, totals match screen, and text/rows are not clipped. Current responsive-table overflow and nowrap content require runtime print QA. Never infer printable completeness merely because the browser dialog opens.

### T41 — Operation pagination and all-row printing

**Preconditions:** Owner has 45 fictional bookings.  
**Steps:** Navigate operation pages with previous/next arrows, then print while on page 2.  
**Expected:** Screen pages show 22, 22, and 1 rows, with correct range/count and disabled arrows at endpoints. Print includes all 45 loaded operations because print CSS intentionally overrides hidden pagination rows. The screen page size does not limit the report to 22 records.

### T42 — Other printable records and privacy

**Preconditions:** Customer A and B have distinct histories; owner customer list and a customer's modal contain enough rows for multiple pages.  
**Steps:** Print customer History, owner Monthly Income, Customer Records, and the selected customer's Booking History modal.  
**Expected:** Each output contains the intended report only; customer output never includes B's data; owner customer modal includes only selected customer. Hide action buttons/modal backdrop/sidebar, preserve table headers and reference columns, and confirm pagination does not drop rows. Redact personal information before using outputs as thesis evidence.

### T43 — Walk-in add-ons and server-calculated total

**Preconditions:** Test staff and fixture prices.  
**Steps:** Enter 5 kg Washing, Powder, and Downy in Process Walk-in; submit; compare client total with persisted lines and server total. Repeat with an unknown service token in a controlled request.  
**Expected intended result:** PHP 145.00, comprising Washing PHP 125.00 + Powder PHP 10.00 + Downy PHP 10.00. Current JavaScript sends `Soap Powder` and `Downy Softener`, but lookup expects exact service names `Powder` and `Downy`; source predicts skipped add-ons. Unknown service should not silently create an incomplete order. Log and fix before claiming acceptance.

### T44 — Metric truthfulness and reward consistency

**Preconditions:** Known counts of active/completed/cancelled bookings; active/inactive staff; real reward ledger balance unlike 1,250.  
**Steps:** Compare each dashboard number with filtered database counts; compare rewards display with ledger; try a reward redemption.  
**Expected intended result:** Labels match real definitions and displayed points match spendable balance. Current source uses all-history count for Finished Laundry, all-booking count for owner Active Orders, active queue count for staff Today's Orders, hardcoded +5 Rush and 1,250 points, and nonpending staff count for Active Staff. Record each discrepancy; do not describe these cards as validated analytics.

### T45 — Zero-priced GCash order and transactional failure

**Preconditions:** Disposable test database; Owner sets an active non-Self-Service core service, such as Washing, to PHP 0.00. No positive-price add-ons are selected. Capture booking/item/service/history/payment counts.  
**Steps:** Customer reviews 5 kg Washing with GCash, continues to the QR screen, and clicks I've Made My Payment. Inspect response and all related record counts. Restore the agreed test price after the case.  
**Expected acceptance target:** A zero-total order has an explicit approved policy: either reject before final submission with a helpful message or confirm as a free order without a zero-value payment. No partial booking data should remain after failure. **Current source prediction:** Owner permits zero pricing for this service and draft validation only specifically blocks zero-priced Self Service. GCash persistence then attempts amount 0; `payments` has `CHECK (amount > 0)`. On a database enforcing that check, the transaction should roll back, but the generic catch rethrows rather than giving a friendly validation result. Execute to confirm enforcement, rollback, and response; this is not a passed test.

## 5. Execution worksheet

Copy this worksheet for each test execution; replace placeholders only after testing.

| Field | Value to enter |
|---|---|
| Test ID / requirement | e.g., T26 / R14 |
| Tester | Name or initials |
| Actual test date and local time | YYYY-MM-DD HH:mm with timezone |
| Source version | Commit ID when available; otherwise dated source snapshot identifier |
| Environment | OS, browser/version, app runtime, test database identifier |
| Preconditions/fixture references | Fictional booking/payment IDs only |
| Steps actually performed | Include deviations from plan |
| Expected result | Copy the relevant acceptance rule |
| Actual result | Observed values/messages/HTTP response |
| Outcome | Pass / Fail / Blocked |
| Evidence | Sanitized screenshot/PDF/query result/log path |
| Defect or follow-up | ID, owner, next action |
| Retest date/outcome | Only after execution |

Do not fill a September test date merely because September is the documentation period. The execution date must be when the test actually happened.

## 6. Suggested execution order and acceptance gates

1. **Foundation:** Restore isolated test database; verify role/session/token behavior (T01–T07).
2. **Booking correctness:** Selected services, arithmetic, review/edit/cancel, both payment-method branches, duplicate confirmation, receipt (T08–T17).
3. **Operations and money:** Status transitions, payment validations, concurrency, actual weight, notifications (T18–T31).
4. **Reporting:** Reconcile source payments before evaluating date filters, totals, and printing (T32–T42).
5. **Cross-module regression:** Walk-in pricing, metric/reward truthfulness, and zero-total transactional handling (T43–T45).

Suggested minimum demonstration gate: no unexplained booking created before final confirmation; no double-counted Paid amount; no cross-customer record disclosure; accurate selected-service totals; correct weight recalculation; monthly/transaction Paid totals reconcile; requested filter controls are usable; chosen report prints all intended rows.

For the Gantt chart, keep **implementation**, **defect correction**, **manual testing**, **retesting**, and **documentation approval** as separate work items. Do not mark an entire module complete merely because its view exists.
