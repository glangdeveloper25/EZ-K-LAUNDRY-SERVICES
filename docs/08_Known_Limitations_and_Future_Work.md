# Known Limitations, Source Findings, and Future Work

**Review date:** 30 September 2026  
**Scope:** Current repository implementation; not a live-database audit or executed penetration test.

This chapter prevents the manuscript from claiming functionality beyond the evidence. It records source-observed gaps and separates them from optional extensions. No application code was changed while preparing this documentation.

## 1. Priority meaning

- **P1 — resolve or explicitly disclose before acceptance:** Affects correctness of amounts, requested behavior, state transitions, or report usability.
- **P2 — address before operational deployment or broader demonstration:** Affects reliability, trust in displayed information, scalability, or security hardening.
- **P3 — optional/approval-dependent extension:** Expands the product scope; not automatically authorized by this documentation task.

Priority is an assessment for planning, not a verified production incident or a claim that data was lost. Test IDs refer to the [Acceptance Test Plan](07_Requirements_Traceability_and_Test_Plan.md).

## 2. P1 findings: correctness and promised workflow

### L01 — Conditional report filter fields remain hidden

**Evidence:** `Views/Owner/Index.cshtml` defines `.report-period-field { display: none; }`. `updateReportPeriodFields()` assigns an empty inline `display` value to fields for the selected period.

**Why it matters:** Removing the inline display does not override the class rule. The date/month/year/custom-range input containers therefore remain hidden even when the matching period is chosen. The controller accepts the filter parameters, but that does not establish usable filtering through the form.

**Required follow-up:** Correct the visibility rule; test Specific Date, Monthly, Yearly, and Custom in the browser; confirm submitted values and printed scope. Do not claim these controls passed UI testing until T35–T36 are executed.

### L02 — Walk-in soap/softener names disagree with service lookup

**Evidence:** Walk-in JavaScript in `Views/Staff/Index.cshtml` sends `Soap Powder` and `Downy Softener`. `StaffController.ProcessWalkIn` normalizes only `Rush` to `Rush Fee`; `AddBookingService` looks up an exact active `service_name` and returns zero if not found. Seeded/owner-maintained names are `Powder` and `Downy`.

**Impact:** The screen can add an add-on cost to its estimate while persistence skips that line and reports a lower server total. Unknown tokens are also silently skipped instead of rejecting the incomplete selection.

**Required follow-up:** Use consistent identifiers or a service-ID catalog, reject unknown services, and retest persisted quantities/totals (T43). Keep server-side price calculation; do not trust a hidden client total as the remedy.

### L03 — Status update paths do not enforce a single state-transition policy

**Evidence:** Dedicated `CompleteOrder` accepts only Ready; dedicated `CancelOrder` accepts selected active states and requires a reason. `UpdateStatus` accepts a list including Completed and Cancelled, then calls `ChangeBookingStatus`, which updates a booking by ID without checking its previous status. `AcceptBooking` also calls the generic helper.

**Impact:** A crafted or stale request can bypass the stricter completion/cancellation path, move stages backwards, or alter an already terminal booking. Generic transitions do not verify whether the selected laundry service includes that stage. The UI alone is not a complete state machine.

There is also an interface mismatch: the staff phase dropdown offers Washing, Drying, and Ready but omits Folding, although `UpdateStatus` accepts Folding and the customer tracking view displays it. A controller capability must not be described as an available staff dropdown choice.

**Required follow-up:** Define allowed transitions once and enforce them server-side for every endpoint; decide whether completion requires payment or allows credit; require cancellation reason on every path. Test T19–T21. Until then, document status changes as staff-managed updates, not a fully validated workflow engine.

### L04 — Several dashboard metrics do not match their labels

| Display | Current source behavior | Consequence / desired definition |
|---|---|---|
| Customer Finished Laundry | `Model.History.Count` | Counts all history statuses, not only Completed |
| Customer EZ-K Points / Rewards points | Literal `1,250` in view | Can disagree with `reward_transactions` ledger used for redemption |
| Owner Active Orders | `Model.BookingList.Count` | Includes all loaded bookings, including terminal ones |
| Owner Rush orders | Literal `+5 Rush orders` | Not a database-derived rush count |
| Owner Active Staff | `Model.StaffList.Count / 15` | List includes nonpending staff, including Inactive; 15 is a fixed display value |
| Staff Today's Orders | `Model.ActiveQueue.Count` | Counts ongoing orders, not necessarily orders booked today |
| Staff My Shift | Fixed shift and On Duty text | No roster/attendance database workflow established |

**Required follow-up:** Agree on definitions, query matching data, remove or label placeholders, and test T44. Screenshots of these cards should not be used as evidence of validated operational statistics.

### L05 — GCash page is a prototype declaration, not a payment gateway

**Evidence:** `Views/Customer/PaymentQR.cshtml` obtains a QR image whose data payload is the literal text `EZKLaundryPartialPay`. `ProcessPartialPayment` creates a Pending declaration; it does not contact GCash or verify a merchant transaction.

**Impact:** The page cannot be represented as automatically processing or confirming actual GCash payment. Its title says “Scan to Pay Partial” while the pending record is populated with the whole estimated order total; no explicit deposit amount/percentage or uploaded proof is captured there.

**Required follow-up:** For a prototype, clearly label manual payment instructions and staff verification. Before real use, establish a legitimate merchant payment method and verification/reference workflow, then test settlement and failure cases. Never transfer real funds to a placeholder. Tests T14–T15 and T24 evaluate the internal record workflow, not external payment success.

### L06 — Partial-payment semantics differ between database view and controllers

**Evidence:** SQL `v_booking_payment_balances` counts statuses `Partial` and `Paid` as collected amounts. Staff balance, actual-weight checks, owner earnings, and report summary code count only `Paid`. Staff recording stores any accepted installment amount as a Paid payment row.

**Impact:** A PHP 100 installment can be a Paid transaction while the booking still has an unpaid balance; that is different from a row whose payment status is named Partial. Imported or manually prepared Partial rows can cause the SQL view and the application to disagree.

**Required follow-up:** Define payment-row status versus overall booking settlement. Harmonize the SQL view, controllers, fixtures, and manuscript. Do not merge the words “partially paid order” and “Partial payment status” without explanation. Test T22 and T34.

### L07 — Paid rows without `paid_at` can produce unequal report totals

**Evidence:** `LoadIncomeReport` Total Earnings sums all Paid rows. Its monthly query also requires `paid_at IS NOT NULL`. Transaction reporting uses `COALESCE(p.paid_at, p.created_at)`.

**Impact:** A legacy/imported Paid row lacking a payment date appears in earnings and transaction totals but not monthly income. The normal staff path sets `paid_at`; the risk concerns inconsistent existing/imported data and database constraints that do not require a date for Paid status.

**Required follow-up:** Audit such rows without inventing dates; require meaningful payment dates or disclose unallocated collections. Reconcile all three report totals (T34).

## 3. P2 findings: reliability, information design, and deployment readiness

### L08 — Tracking and notifications are page-load snapshots

`CustomerController.Index` reads the database when requested. The customer view switches already-loaded sections; it contains no polling, server-sent events, or WebSocket update mechanism. Staff changes appear after a reload/navigation that fetches data, not necessarily while the customer's open page sits idle.

The Notifications view renders message cards as non-clickable containers. There is no implemented notification detail/open action or per-message mark-read navigation. “Mark all as read” is implemented, but that is different from opening a notification. Newest 100 messages are loaded, and the unread badge counts only this loaded subset.

Generic status changes do not currently insert notifications. Actual-weight updates and payment recording do. A staff broadcast endpoint inserts a generic notice for active customers; it is not an SMS, email, mobile push, or custom message service.

**Next decision:** Keep refresh-based behavior and label it accurately, or approve automatic refresh/click-through work. Tests T18–T19 and T29–T31.

### L09 — Actual weight overwrites one stored weight rather than preserving two fields

`UpdateActualWeight` updates the first Clothes item and all PerKg service quantities. The prior weight is included in status-history notes, but there are no separate structured `estimated_weight`, `actual_weight`, `weighed_at`, and `weighed_by` columns in the current implementation.

Consequently, the current item weight becomes the actual weight and the original estimate is no longer directly available as a separate field for comparison/reporting. The helper assumes one Clothes item; it is not a general multi-item weighing workflow. Weight changes below collected money are blocked rather than initiating refunds/credit handling.

**Next decision:** If the manuscript requires distinct estimate/actual tracking or dispute resolution, preserve both structured values and actor/time metadata. Do not claim those fields already exist. Tests T26–T28.

### L10 — A payment lock is not complete duplicate-request protection

`RecordPaymentCore` locks the booking and checks its remaining Paid balance. This is useful protection against overpayment and concurrent full settlement. It has no unique request/idempotency key, however. Repeating a smaller payment while enough balance remains may create another Paid row. The optional database transaction reference is not captured in this staff form.

Customer booking confirmation has different protection: the draft carries a code; `bookings.booking_code` is unique; duplicate-key handling retrieves the existing customer booking. This should be tested under concurrent requests rather than described as an all-system guarantee.

**Next decision:** Define which repeated requests are duplicates versus separate installments; capture verified payment references where applicable. Tests T16 and T25.

### L11 — Cancellation, completion, payment, and refund are separate concepts

Completing a laundry order does not create a Paid payment. Cancelling one does not reverse an existing Paid payment. Payment-status rows include Refunded/Failed/Partial, but the application does not provide a complete refund approval/execution ledger or reversal workflow.

Therefore, the Monthly Income Report represents amounts currently recorded Paid, not necessarily net cash after refunds and not business profit. Laundry Operations is a booking list, while Transaction Report is a payment-row list. An unpaid Cash booking without a payment row will not appear in Transaction Report.

**Next decision:** Approve refund and release-with-balance policies before expanding behavior. Do not create fake historical payments to make a report nonempty. Tests T21–T24 and T32–T38.

### L12 — Customer rush and delivery fees do not set fulfillment metadata

Customer draft persistence inserts booking code, customer, and Pending status, then items/services. Selecting Rush Fee or Delivery Fee creates a billable line but does not also populate `bookings.is_rush`, `fulfillment_method`, `delivery_address`, or a pickup schedule. Database defaults therefore remain relevant, including DropOff and false rush.

Staff pickup-method screens can display fulfillment data already present in the database, but this is not proof that customer scheduling/address entry is implemented end-to-end. A fee selection is not equivalent to an arranged delivery.

**Next decision:** Link selections to explicit fulfillment requirements only after confirming the desired booking scope. Review rush counts and logistics reports accordingly.

### L13 — Price updates and draft pricing need an explicit policy

Owner updates write individual service prices without wrapping the full set in one transaction. A mid-operation failure can leave a partially updated catalog. Customer drafts store selected IDs, basis, quantities, and prices at review time; final confirmation does not reread whether catalog prices or availability changed in the meantime.

Saved booking unit prices correctly preserve an order-specific price snapshot. The remaining decision is whether a review-time quote is guaranteed until confirmation or must expire/revalidate. Current session idle timeout is not a formal published quote-validity policy.

**Next decision:** Define quote validity, atomic catalog update behavior, and any service-availability recheck; test T11. Do not silently alter historical booking prices to synchronize them with the current catalog.

### L14 — Service selection and process stages are not fully reconciled

Customer service selection is multi-select, including Self Service. The server confirms names exist and are active but does not enforce a strict core-service list or an incompatibility rule between self-service and staff-operated services. Tracking always displays Washing, Drying, Folding, and Ready, even for a booking that did not purchase all those stages.

This does not prove extra services are charged: totals come from saved service lines. It does mean the process presentation can confuse a customer who selected only Washing or Drying.

**Next decision:** Define whether stages describe physical workflow or purchased services; present them consistently. Validate add-on-only/forged service selections and service combinations (T09–T10, T18).

### L15 — Print controls exist; complete print acceptance remains pending

Owner print CSS has dedicated sections for monthly income, operations, customer records, per-customer booking modal, and transactions. Customer history/receipt also have print controls. Operations printing intentionally reveals all loaded rows rather than the current 22-row screen page.

The receipt's Print Receipt control calls `window.print()`, but `Views/Customer/BookingReceipt.cshtml` has no receipt-specific print CSS to hide its dashboard/print action row. Therefore, the button establishes access to browser printing, not a verified clean receipt-only layout. Hiding actions is an acceptance target, not a current proven behavior.

Transaction output uses a wide table and a responsive overflow container. Unlike the monthly report's explicit print overflow rule, the transaction container has no equivalent override in its dedicated print rules. Long names, long service lists, nowrap columns, browser-specific paging, and modal heights still need visual PDF/print checks.

Filter changes are only applied after form submission; printing immediately after changing an unsent dropdown prints the last rendered result. A Print dialog opening is insufficient proof that all rows/columns are included.

**Next decision:** Execute T39–T42 on supported browsers and paper sizes; save sanitized evidence. Do not mark printing fully accepted based on source inspection alone.

### L16 — Scalability is limited by full-data dashboard loading

Owner Index loads staff, customers, all bookings, per-customer bookings through individual queries, monthly income, and filtered payment transactions for one dashboard request. Operations pagination is client-side; it does not reduce database row loading. Transaction Report has no server-side pagination/streamed export. Customer history and active lists are also loaded for rendering.

**Impact:** Larger datasets can increase query count, memory, response time, and print size. This is a capacity risk, not a measured performance result.

**Next decision:** Establish realistic volume targets and response-time measurements before redesigning queries/pagination. Keep all-row printing/export semantics clear if screen paging becomes server-side.

### L17 — Authentication is prototype session-based and needs hardening review

Positive controls present: role checks, customer-scoped queries, controller anti-forgery validation, parameterized SQL, PBKDF2-SHA256 password storage with random salts, and fixed-time hash comparison. These should be described accurately rather than dismissed.

Remaining source-observed considerations:

- Role helpers primarily trust existing session values; deactivating a user does not recheck account status on every protected request.
- No application login rate limiter, account lockout, MFA, password reset/email verification workflow, or centralized authorization policy is established in the inspected code.
- Logout is a GET action that clears session state.
- HTTPS redirection is commented out in `Program.cs`; local HTTP demonstrations should not be represented as production-secure deployment.
- Database connection configuration is embedded in application source. Actual credentials must not be reproduced in documentation; move deployment secrets into protected configuration and use a least-privilege database account.
- Staff addition and profile updates do not apply exactly the same model validation path as customer registration; unique data failures and malformed contact/email handling need uniform tests.
- Owner initialization checks whether an owner exists before insertion; concurrency and secure first-run access need consideration before public deployment.

**Next decision:** Complete deployment/security review and T01–T04/T07 before public exposure. This documentation does not assert that an exploit was attempted or that production security has been certified.

### L18 — Staff deletion can remove useful actor attribution

`OwnerController.DeleteStaff` deletes the staff user row. Several historical actor relationships use `ON DELETE SET NULL`, retaining bookings/payments/history while losing the staff foreign-key reference. Transaction Report then may show the fallback label Customer/System when recorder identity is absent; that fallback does not establish who actually performed the action.

**Next decision:** Prefer a clearly defined retention/archive policy if accountability is required. Reusing Inactive status may be more appropriate than deletion, but this is a proposed change, not a performed one. Test with disposable data only (T07).

### L19 — Loyalty screens and ledger do not form a complete verified reward lifecycle

The database has `reward_transactions` and a derived balance view. `RedeemReward` sums ledger points and inserts a negative redemption when sufficient. Displayed customer points remain fixed at 1,250, and a complete automatic earn-on-completion/payment path was not established in the inspected controllers.

Redemption accepts a submitted reward name and assigns 800 points for exactly Free Wash, otherwise 200; it does not use a dedicated reward catalog/strict name allowlist. Concurrent redemption balance checks lack a clearly locked user/balance record.

**Next decision:** Either limit demonstration claims to the existing ledger prototype or approve a full earn/redeem/catalog/concurrency integration. Never promise a displayed 1,250-point balance is spendable without checking the actual ledger (T44).

### L20 — Date/time conventions are mixed

Booking codes use `DateTime.UtcNow`. UI relative times and default report labels use local `DateTime.Now/Today`. SQL dates use database `NOW()` and `CURDATE()`. Transaction grouping uses payment timestamps; it does not use the date portion embedded in booking codes.

**Impact:** Near midnight, a code's date, displayed relative time, and Today filter can disagree if application/database time zones differ. The code date is not evidence of the booking's local business day by itself.

**Next decision:** Establish a documented business timezone and storage/display conversion rule; test midnight boundaries and end-of-month reports (T33–T36). Historical project-work dates must be independently evidenced, not inferred from booking references.

### L21 — External assets and prototype text need presentation QA

PaymentQR uses externally hosted logo/QR assets; unavailable network resources can leave broken images. Some source strings contain a literal question mark where a currency sign appears intended. Audio alert and shift displays do not establish working sound notifications or attendance monitoring.

**Next decision:** Check offline/network-failure presentation, currency labels, icon accessibility, responsive navigation, and label accuracy. Do not equate a toggle's appearance with a functioning backend capability.

### L22 — No accepted automated regression evidence accompanies this snapshot

No dedicated test project or application test suite was identified in the inspected source set. Existing binaries, prior chat statements about builds, and file timestamps do not prove that every user scenario passed. This package supplies a manual acceptance plan rather than manufacturing a pass report.

**Next decision:** Record executed test dates/results and sanitized evidence. Later add unit/integration tests for price arithmetic, draft persistence, authorization, status transitions, payment locking, and report filters. Separate source implementation dates from actual test dates in the Gantt chart.

### L23 — Per-item quantities are not explicit on review and receipt

`ReviewBooking.cshtml` and `BookingReceipt.cshtml` render quantity-times-price detail only when charging basis is PerKg. All other lines show unit price and line total without the quantity multiplier. A customer selecting two Powder items can see PHP 10.00 unit price and PHP 20.00 line total without an explicit quantity 2.

The underlying line-total calculation still multiplies quantity by unit price; this is a transparency/presentation gap, not evidence that arithmetic always fails. The receipt also retains estimated-weight/estimated-total wording, and its footer selects a payment message from the payment-method value rather than a full payment verification/balance model. It should not be presented as an authoritative paid receipt after later staff payment or actual-weight changes.

**Next decision:** Display PerItem quantity, unit price, and line total explicitly; distinguish booking acknowledgment from payment receipt, and label current versus estimated weight correctly. Retest T08 and T17.

### L24 — Actual-weight precision is not enforced consistently server-side

The modal requests a 0.01 step, while `UpdateActualWeight` rejects only `<= 0` and `> 999.99`. It does not explicitly reject more than two decimal places or values between 0 and 0.01. Database weight and service-quantity columns store two decimals, whereas the projected-total calculation uses the submitted decimal value before persistence.

Consequently, crafted requests such as 0.001, 0.005, or 1.234 kg can encounter database rounding, a rounded zero violating a positive-quantity constraint, or disagreement between the pre-save projection and persisted totals. These are source-identified risks; actual behavior depends on database precision and constraint enforcement and has not been executed here.

**Next decision:** Define the supported measurement precision and minimum, validate/normalize once on the server before both calculations and writes, and give a friendly failure message. Test stored amount, paid-balance protection, audit notes, and notification text together (T27).

### L25 — Zero-total GCash confirmation lacks a friendly business rule

Owner pricing permits zero for non-Self-Service services; customer draft validation specifically rejects nonpositive Self Service pricing but does not reject a zero grand total. GCash booking persistence attempts to insert a Pending payment at that draft total. The schema requires `payments.amount > 0`.

On a database enforcing that constraint, a zero-total submission should fail and roll back the booking transaction. The surrounding generic catch rolls back and rethrows, so the source does not provide a friendly free-order/invalid-price decision. This is not a claim that a zero-price attempt has been run or that partial data was left behind.

**Next decision:** Either disallow zero-priced bookable services with an explanatory validation response or define a legitimate free-order path that does not insert a zero-value payment. Run T45 against an isolated fixture and verify all related row counts after failure.

## 4. P3: proposed extensions, not delivered features

| Proposed extension | Minimum definition before implementation | Current status |
|---|---|---|
| Basic soap/softener inventory | Unit of measure, opening balance, stock-in, actual usage, adjustment, remaining stock, responsible staff | Discussed; paused; no inventory tables/workflow implemented |
| Usage by laundry weight | Shop-approved dose/product rules and actual measured consumption; no universal grams-per-kg assumption | Not implemented |
| Supply costing and price-increase analysis | Purchase cost history, consumption costing, waste, margin definition, period comparison | Not implemented; report currently measures collections, not profit |
| Automatic live tracking/notifications | Refresh transport, interval/reconnect policy, unread counts, authorization, accessibility | Not implemented as push/polling |
| Clickable notifications | Destination per type, customer ownership validation, per-message read state | Not implemented |
| Complete delivery/pickup workflow | Address, date/time, fulfillment method, fee policy, staff assignment | Schema supports some fields; complete customer input flow not established |
| Refunds, voids, payment-reference verification | Approval policy, immutable reversal records, balance/report reconciliation | No complete UI workflow established |
| Separate estimate versus actual weight | Structured fields, timestamp/actor, customer-visible comparison and audit | Current weight overwrite plus history notes only |
| Reward catalog and automatic earning | Approved earning rate, event trigger, prevention of duplicate awards, redemption controls | Partial ledger implementation only |
| Production deployment/backup operations | HTTPS, secrets, account privileges, backups, restore drill, monitoring, retention | Not demonstrated by local prototype source |

Inventory must remain a separate approved scope item. Selecting Powder/Downy in a booking currently represents a chargeable service/add-on; it does **not** deduct physical stock. Do not retroactively describe booking add-on quantities as a stock ledger.

## 5. Suggested correction and validation sequence

1. Resolve report filter visibility and walk-in add-on mismatch; retest exact user-requested scenarios.
2. Unify state transitions, metric definitions, and payment/Partial semantics.
3. Clarify manual GCash versus real payment integration; reconcile all Paid totals and payment dates.
4. Validate actual-weight recalculation, notifications, duplicate requests, and all print targets.
5. Agree on production readiness and data-retention controls before external use.
6. Only after scope approval, schedule inventory or other extensions separately.

This sequence is proposed planning guidance. It is not a list of completed changes or assigned completion dates. Use the actual execution log and evidence to populate future Gantt-chart start/end dates.

## 6. Safe wording for manuscript and defense

Use:

> The prototype supports customer booking review, staff-managed processing and payment recording, and owner collection reports. Current report summaries are derived from payment records marked Paid. Selected limitations, including report-control visibility, placeholder metrics, and manual GCash verification, are documented for correction and acceptance testing.

Avoid claiming:

- “All features are fully tested and error-free.”
- “GCash is automatically verified by the system.”
- “Completed orders automatically become income.”
- “The customer dashboard updates instantly without refresh.”
- “Soap inventory is automatically deducted per kilogram.”
- “Monthly income is net profit.”
- “All chart dates are verified historical development dates.”

These distinctions make the documentation more defensible: it describes what exists, how it works, what still needs evidence, and which changes remain outside the delivered scope.
