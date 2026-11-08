# Reporting and Printing

Documentation baseline: **30 September 2026**. Reporting behavior below is based on the current controller/view code. This chapter records an important filter-interface defect and identifies print checks still required; it does not claim that every filter or printed page is acceptance-tested.

## 1. Why there are different kinds of reports

A booking records the customer's requested laundry work. Its order amount is the sum of booked service quantities multiplied by their saved unit prices. A payment records a financial event attached to that booking. One booking can have no payment, one payment, or several payments. Processing status and payment status are separate concepts.

Consequently, an order labelled Completed can still have no recorded receipt of money. An amount in Booking History or Laundry Operations is not proof that the amount was paid. Conversely, a received deposit can count in income while the laundry is still being processed. The current system reports **recorded receipts**, not accounting profit: supplier expenses, inventory consumption costs, operating expenses, and a comprehensive refund ledger are not implemented in these reports.

## 2. Report catalog

| Report / record view | Location | Row represents | Financial meaning | Print control |
| --- | --- | --- | --- | --- |
| Monthly Income Report | Owner, Overview | Month containing Paid payments with a paid date | Received Paid totals by Cash, GCash, Other | Print Report |
| Transaction Report | Owner, Transaction Report sidebar section | One payment record | Amount and status of a payment event | Print Report |
| Laundry Operations | Owner, Laundry Operations | One booking | Current sum of saved service lines | Print Operations Report |
| Customer Records | Owner, Customer Records | One customer | Contact listing; no receipt total | Print Customer Records |
| Individual Customer Bookings | Owner, View Bookings modal | One booking for selected customer | Current order amount | Print Booking History |
| Booking History | Customer, History | One booking owned by logged-in customer | Order amount despite Payment column label | Print Booking History |
| Booking Receipt | Customer, after final booking confirmation | Last submitted booking in session | Booking acknowledgment/estimated service total | Print Receipt |

Staff Active Queue is an operational work list. No dedicated print action or print layout for that Staff screen was found. Staff Management, pricing settings, notifications, and reward screens should likewise not be represented as having dedicated printable reports merely because a browser can print a page.

Source: [OwnerController](../Controllers/OwnerController.cs), [Owner view](../Views/Owner/Index.cshtml), [CustomerController](../Controllers/CustomerController.cs), [Customer view](../Views/Customer/Index.cshtml), [receipt view](../Views/Customer/BookingReceipt.cshtml).

## 3. Monthly Income Report

`OwnerController.LoadIncomeReport` calculates Total Earnings from every payment with payment status Paid. Its separate monthly query includes only Paid rows whose `paid_at` is not null, groups them by payment year/month, and sorts the newest month first. Each month shows Cash, GCash, Other, and Total Income. Other means a method whose name is neither Cash nor GCash.

The monthly view does not require that a booking be Completed. It does not sum bookings or quote estimates. Months without qualifying payment rows are omitted instead of appearing automatically as zero-filled calendar months. When no qualifying records exist, the view displays No paid transactions recorded yet.

The monthly summary and the detailed Transaction Report are separate views. Applying a transaction filter does not filter the Overview monthly table or the all-time Total Earnings card.

### 3.1 Why an income report may be empty

Common source-consistent explanations are:

1. Only bookings were created; no payment has been recorded by Staff.
2. Customer GCash declarations are still Pending and have not been verified.
3. Payments have another status, such as Failed, rather than Paid.
4. A Paid row has no `paid_at`, so the monthly query omits it even though the all-time earnings query can include it.

Do not fill the report by automatically declaring completed bookings paid or inserting fabricated historical receipts. Staff should enter/verify actual receipts, and historical corrections should be based on real evidence.

### 3.2 Partial payment versus the Partial status

If a PHP 300 booking receives PHP 100, the Staff entry creates a **Paid payment for PHP 100**, leaving an order balance of PHP 200. The received PHP 100 therefore contributes to income. A separate payment row literally marked **Partial** does not contribute, because the report predicates check for Paid. Current Staff recording does not create Partial-status rows; that lookup status is available in the schema/report filter, but has different semantics from a partly settled booking.

## 4. Transaction Report fields and query

`LoadTransactionReport` starts from `payments`, joins its booking, customer, method, status, and optionally recording user, then joins a grouped service-name subquery. Pre-grouping the service list prevents one payment from being repeated once for every service.

| Displayed field | Source / rule |
| --- | --- |
| Date / Time | `COALESCE(p.paid_at, p.created_at)` |
| Transaction ID | Nonblank `transaction_reference`; otherwise `TXN-` plus zero-padded payment ID |
| Booking | Booking's EZK reference code |
| Customer | Customer first name and last name |
| Services | Comma-separated names of saved booking service lines |
| Method | Payment-method lookup name |
| Amount | That payment row's amount, not the total booking charge |
| Status | Payment status, not laundry processing status |
| Recorded By | Name attached through `recorded_by_user_id`, or Customer/System fallback |

The fallback transaction ID is an internal display identifier, not proof of an external GCash reference. Customer/System also means the recorder reference/name was unavailable; it does not conclusively prove who performed the original event, especially if staff accounts have been deleted.

Results are ordered by effective transaction date descending, then payment ID descending. No transaction-report pagination or row limit is implemented. Multiple payments for the same booking appear as multiple rows. A cash booking with no payment row is absent, even if it appears in Laundry Operations.

## 5. Filters and date semantics

The Owner filter form submits a GET request to `Index` with `openReports=true`. Its supported values are as follows.

| Filter | Options / behavior |
| --- | --- |
| Period | All, Today, Yesterday, Specific Date, Monthly, Yearly, Custom Date Range |
| Date | Selected calendar day for Specific Date |
| Month / year | Matching month and year for Monthly; year alone for Yearly |
| Start / end | Inclusive calendar dates for Custom; implemented as start inclusive and next day after end exclusive |
| Payment Method | All, Cash, GCash, Other |
| Payment Status | All, Paid, Pending, Partial, Failed, Refunded |

Different categories combine with AND. For example, Today plus GCash plus Paid means only today's effective-dated GCash rows marked Paid. Selecting All removes the date restriction, not any method/status restriction the user still has selected.

Invalid enumerated values fall back to All. Controller month values are clamped to 1–12 and year values to 2000–2100. The visible year selector is narrower: 2020 through the current application year plus one. Unspecified dates default to today; custom start defaults to the first day of the current month, and custom end to today. Reversed custom dates are swapped by the controller.

Today and Yesterday use MySQL `CURDATE()`. Scope labels use application-server `DateTime.Today`, and print timestamps use the browser clock. Application, database, and browser timezones should therefore agree before daily totals are accepted. The booking-reference date separately uses UTC and must not be used as the report's payment date.

### 5.1 Known filter-interface defect

The current Owner stylesheet declares `.report-period-field { display: none; }`. `updateReportPeriodFields()` assigns `field.style.display = ''` for a field that should be visible. Removing an inline override does not defeat the class-level `display:none`. As a result, the date, month, year, and custom-range controls can remain hidden when their period is selected.

The controller contains filtering logic, but this UI defect means arbitrary dates/months cannot yet be represented as fully usable through the visible controls. Default hidden input values can still be submitted. Today, Yesterday, and All do not need those additional fields, but still require runtime checks. This issue was recorded during documentation, not repaired as part of the documentation request.

Source: `.report-period-field`, `updateReportPeriodFields`, filter form in [Owner view](../Views/Owner/Index.cshtml).

### 5.2 Demonstration examples after verifying/fixing the controls

- To show money received today: Period Today, Method All, Status Paid, then Apply Filter.
- To inspect unverified GCash submissions: Period All, Method GCash, Status Pending, then Apply Filter.
- To show September 2026 transactions: Period Monthly, Month September, Year 2026, with the desired method/status.
- To print all payment records: Clear filters, confirm the heading All Transactions and All Methods/Statuses, then Print Report.

These are use cases, not evidence that all combinations have passed testing. Printed results are based on the **last applied query**. Changing a dropdown without Apply Filter does not fetch new rows or change the server-generated scope.

## 6. Totals shown above the transaction table

The Transactions card counts all returned payment rows, regardless of status. Paid Cash, Paid GCash, and Paid Other cards sum only returned rows whose status equals Paid. Paid Total is their sum.

For example, a result set with one PHP 200 Paid Cash row and one PHP 100 Pending GCash row has two Transactions and PHP 200 Paid Total. Filtering to Pending can correctly show records while every Paid summary is zero. Amounts should not be blindly summed across all statuses as income.

Refunded rows are not treated as negative cash outflow in the paid-only totals. If a historical Paid row is changed to Refunded, these queries exclude it instead of displaying a refund ledger entry. There is no implemented refund-processing workflow in the examined controllers; do not label the report net revenue or profit.

## 7. Print scope and layout

### 7.1 Detailed Transaction Report

`printTransactionReport()` adds a temporary body class and A4-landscape page rule, sets a print timestamp, and calls `window.print()`. Print CSS selects the report area, shows the business/report title, applied scope, and print date, and hides sidebar/filter controls and unrelated content. Summary cards and the rendered transaction table remain.

Because printing uses the already-rendered table, the intended scope is exactly the last applied results. With defaults/cleared filters, all payment rows are printed; with filters, only matching rows are printed. This is browser printing, not a server-generated PDF export or spreadsheet export. Save as PDF may be selected if supported by the user's browser/operating system.

The nine-column table includes nonwrapping dates and references inside `.table-responsive`. Unlike the monthly table, its print rules do not explicitly make that responsive wrapper's overflow visible. Wide values and long reports therefore need print-preview verification for clipping and page breaks. Browser handling of `window.print()` and cleanup timing also needs testing.

### 7.2 Monthly Income Report

Overview's Print Report prints the monthly table in an A4-portrait layout. Navigation, dashboard metrics, and unrelated sections are hidden. A print-only business/report heading and browser-generated print timestamp are included. Table headers are declared as repeatable header groups and rows are marked to avoid splitting where possible.

The Owner view's default print CSS favors this monthly report. Use each section's dedicated print button rather than assuming Ctrl+P from any visible Owner tab prints that tab.

### 7.3 Laundry Operations and Customer Records

Laundry Operations prints all loaded booking rows, not only the current group of 22. Print CSS explicitly restores `.laundry-operation-row` to table-row and hides the pagination controls. After the print call the screen pagination is restored.

Customer Records prints the customer listing and hides the Action column. The individual customer's View Bookings modal has its own print function; it selects that modal, removes its backdrop/chrome, and prints that customer's booking table. These scopes include personal data, so generated files and physical copies should be shared only with authorized recipients.

### 7.4 Customer history and receipt

Print Booking History uses A4 landscape, a customer/business heading, a print timestamp, and CSS visibility rules that isolate the history table. The customer should open History before using its dedicated button. There is no customer history date-filter interface in the current source.

Print Receipt directly calls `window.print()`. The receipt view does not contain a dedicated print-only stylesheet comparable to the history/report screens, so buttons and general page spacing may still appear. It must be reviewed before being described as a polished receipt-print layout. The receipt remains a booking acknowledgment, not a tax/compliance-certified receipt or confirmation of funds received.

## 8. Minimum reporting acceptance checks

Use controlled test accounts and clearly identified test transactions, not fabricated production payments. Record expected versus actual results and test dates separately from this baseline documentation.

1. A cash booking alone appears in operational/history records but not payment income.
2. A Pending GCash declaration appears in an All/Pending transaction query and does not increase Paid totals.
3. Staff verification changes that declaration to Paid and contributes exactly the verified amount.
4. Two real installments on one booking produce two payment rows without multiplying either by the number of services.
5. Exact-date, month/year, and custom-range controls become visible and retain selections after submission once the UI defect is resolved.
6. Custom start/end days include transactions near midnight correctly; Today/Yesterday labels agree with database date boundaries.
7. Clear restores the unfiltered payment dataset; printing before and after Apply Filter follows the last applied scope.
8. Screen totals match print totals for Paid-only and mixed-status results.
9. Operations printing with more than 22 test bookings includes all rows while the on-screen page stays paginated afterward.
10. Long customer names, service lists, reference codes, empty datasets, and multipage reports remain readable in print preview/PDF.

The current chapters should be used to explain implemented query logic and outstanding checks honestly. Passing a build or loading a login page alone is not equivalent to passing these report and print acceptance tests.
