# 01 — System Overview and Scope

Documentation cutoff: September 30, 2026. September 2026 is the main development period being documented; earlier history is included where evidence exists.

## 1.1 System identity

The **EZ-K Laundry System** is a role-based, browser-accessible laundry booking and operations application. Customers prepare laundry bookings, staff process orders and record payments, and the owner manages services, staff, customer records, and reports. The current project runs as an ASP.NET Core MVC application connected directly to a MySQL-compatible relational database.

The application should be described as a booking system with supporting operational and reporting functions. It is not currently a complete accounting package, automated GCash gateway, warehouse inventory system, or multi-branch enterprise platform.

## 1.2 Business problem and purpose

The supplied discussions identify practical problems encountered while building and demonstrating the application:

- A booking could appear submitted before the customer finished reviewing or acknowledging the payment step.
- A customer needed to correct selections before final confirmation rather than create unwanted pending orders.
- Static or mismatched receipt information could show services the customer had not selected.
- Showing only one tracked booking was insufficient when the same customer had multiple active orders.
- Customer-estimated weight could differ from the weight measured at the laundry shop.
- Completed laundry and received payment were being confused when interpreting monthly income.
- The owner needed transaction details for a selected date or period, not only a monthly summary.
- The adviser requested printable records and reports.
- Redundant actions and excessive header spacing made the dashboards less clear.

These are documented as project concerns, not as results of a formal external survey. No survey sample, interview transcript, statistical finding, or adviser approval is supplied by this package.

## 1.3 General objective

To organize customer laundry bookings, staff processing activities, service charges, payment recording, and owner reporting within one role-based application, while providing reviewable booking details and printable records.

## 1.4 Specific objectives and current implementation boundaries

1. **Maintain role-separated access.** Provide Customer, Staff, and Owner screens using session-based role checks. Existing checks and remaining security work are described in Chapter 02.
2. **Support deliberate customer booking submission.** Collect services, estimated weight, optional add-ons, and payment preference; show a review step with editing/cancellation before persistence.
3. **Record service selections and prices.** Use service catalog prices to build booking line items. Saved unit prices preserve the rate applied to that booking rather than automatically repricing old orders when the owner changes the catalog.
4. **Monitor multiple laundry orders.** Display active bookings and their current recorded status. Current tracking is loaded with the page; automatic real-time push updates are not implemented.
5. **Allow staff to record actual weight.** Recalculate weight-based line quantities while retaining saved unit prices and fixed/per-piece charges. The current design overwrites the item's weight; it does not preserve estimate and actual weight in separate columns.
6. **Distinguish payment recording from laundry processing.** Staff-recorded/verified payments support balances and income reporting. A GCash declaration is not bank-verified payment.
7. **Provide owner summaries and transaction reporting.** Show paid-payment income grouped by month and support transaction filters. Source-level filter UI defects and pending print validation are explicitly recorded.
8. **Produce printable outputs.** Offer print actions for relevant customer and owner records. Individual print scopes differ; printing is not the same as exporting a database backup.

## 1.5 Actors and responsibility boundaries

| Actor | Primary responsibilities | Boundary |
| --- | --- | --- |
| Customer | Register, sign in after approval, prepare/review bookings, view own tracking/history/notifications, maintain account information. | Should only retrieve their own private records; does not set catalog prices or verify received payments. |
| Staff | Approve customers, accept/process bookings, handle walk-ins, update status and actual weight, record/verify payments. | Day-to-day operations; no owner pricing/staff-management authority. |
| Owner | Manage staff, set prices, inspect operations and customer records, view income and transaction reports, print records. | Business oversight; not an external payment processor. |
| Walk-in customer | A customer recorded by staff during shop intake. | May exist without a registered email/password; not an additional login role. |

The application uses **Owner**, not **Admin**, as its current management role name. The earlier `dictionary.docx` uses Admin in its role description; manuscript terminology should be reconciled.

## 1.6 Main functional modules

### Public access and accounts

Public pages include the home page, customer/general login, staff login, customer registration, and initial owner setup. Registration uses validation and password hashing. Customer registration creates a Pending account which staff can approve. Login only admits Active accounts. Role checks are custom session checks, not an ASP.NET Identity user-management implementation.

### Customer booking and tracking

The booking form offers Washing, Drying, Folding, and Self Service. Customers enter an estimated weight and can select Powder Soap, Downy Softener, Rush Fee, and Delivery Fee where available in the catalog/form. Review and edit take place before submission. Cash on Pickup and GCash follow different confirmation branches.

The customer dashboard includes active-order tracking, booking history with printing, notifications, rewards-related UI, and account settings. Some displayed metrics/reward content remain placeholders or use unsuitable counts. Therefore, the presence of a card does not establish that the entire module is complete.

### Staff operations

The staff area groups pending bookings, current orders, the active queue, customer approvals, walk-in processing, payment recording, and actual-weight editing. Status updates are recorded in history. A standard intended process is Pending, Washing, Drying, Folding, Ready, then Completed; the UI and backend do not consistently enforce every stage or restrict the generic status endpoint to this exact order.

### Owner administration

The owner area includes dashboard summaries, staff management, operations listing, customer records and booking details, service pricing, and transaction reporting. The operations list has client-side pagination with 22 rows per page. Add New Staff belongs to the Staff Management section rather than the dashboard overview.

### Reports and printing

The Monthly Income Report aggregates recorded Paid payments by month and payment method. Transaction Reports are payment-level records with date/period, method, and status filters. Printable customer history, receipts, operations, customer records, and per-customer booking records support review and submission. Chapter 05 explains exact inclusion rules and print scope.

## 1.7 End-to-end business sequence

1. The owner establishes the service catalog and staff accounts.
2. A customer registers; staff approves the account before login is permitted.
3. The customer selects services, estimates weight, and chooses optional fees and a payment method.
4. The system constructs a draft and displays its calculated order summary.
5. The customer edits or cancels the draft, or proceeds through final confirmation/payment acknowledgement.
6. The confirmed booking is stored and becomes visible to staff as a pending order.
7. Staff receives the laundry, measures its actual weight, and records any allowed recalculation.
8. Staff processes the order and changes its recorded status.
9. Staff records actual cash received or verifies a declared GCash payment; the booking's paid amount and outstanding balance can then change.
10. The owner reviews recorded transactions and paid income, optionally filtering and printing the relevant records.

This sequence describes the intended business progression supported by the modules. It does not imply every transition is fully enforced, that all status changes generate notifications, or that a payment gateway is connected.

## 1.8 Important business distinctions

### Estimated amount versus final service amount

A customer estimate is a starting value, not a certified shop measurement. Weight-based service lines use the entered kilograms. When staff records actual weight, the amount may change. A fixed Rush Fee is not multiplied by kilograms; per-piece soap/softener lines use their selected quantity.

### Booking state versus payment state

Pending, Washing, Drying, Folding, Ready, Completed, and Cancelled describe laundry processing. Pending, Partial, Paid, Failed, and Refunded are separate payment-status labels in the database. A Completed laundry booking is not automatically proof that money was received. Similarly, a received payment does not by itself prove that laundry is complete.

### Booking total versus collected income

The booking total is the sum of service line charges. Collected income in the owner implementation is based on qualifying payment rows. An unpaid cash booking has an order value but no payment transaction until staff records one. The reports are collections summaries, not net-profit calculations: operating expenses and inventory costs are not deducted.

### Booking confirmation versus payment declaration

For the current GCash branch, the customer's “I've Made My Payment” action submits a declaration and the booking, not independently verified funds. Staff verification is still required. The current QR is a prototype placeholder, so it must not be presented as a production merchant payment integration.

## 1.9 Scope exclusions and deferred work

The inspected project does not establish implemented stock-in/stock-out inventory, detergent consumption rules per kilogram, supplier purchasing, verified gateway callbacks, automated bank reconciliation, SMS/email delivery, automatic live push tracking, expense accounting, or multi-shop tenant isolation.

Basic inventory was discussed with the adviser, but implementation is paused at the user's request. Do not include it as a completed September deliverable. Soap and softener being selectable billable services does not mean their physical stock is being monitored.

## 1.10 Evidence sources

- `Program.cs` and `EZKLaundrySystem.csproj`: application type, runtime target, middleware, dependencies.
- `Controllers/AccountController.cs`, `CustomerController.cs`, `StaffController.cs`, `OwnerController.cs`: actual role-specific behavior.
- `Views/Account`, `Views/Customer`, `Views/Staff`, `Views/Owner`: current UI and print actions.
- `laundry_final_3nf.sql`: schema design, lookup values, constraints, and database views.
- `../dictionary.docx`: earlier, smaller data dictionary, not the complete current specification.
- Supplied conversation and screenshots: development concerns, requested refinements, and visible historical UI states.
