# 09 — Manuscript Update and Defense Guide

Prepared September 30, 2026. This chapter helps translate the source-backed technical material into school documentation. It is a writing guide, not an adviser approval or a replacement for your institution's required format.

## 9.1 Which original document was available?

The available reference `../dictionary.docx` contains an earlier data dictionary for Roles, Users, Services, Item_Type, and Bookings. It was read without modification. A full approved proposal, scope-and-limitations chapter, methodology, signed consultation forms, and final school template were not present in the inspected project/reference location.

Therefore, this package can explain **what the current system contains** and compare it with that dictionary, but cannot certify that every later feature is already authorized by the original proposal. When writing “according to the approved documentation,” verify against your actual approved manuscript first.

## 9.2 Chapter-by-chapter update map

| Manuscript section | Material to use | What you must still supply |
| --- | --- | --- |
| Background / problem statement | Chapter 01 and the user-reported concerns in Chapter 06. | Real shop background, approved project title, dated adviser/interview records. |
| General and specific objectives | Chapter 01 objectives and their implementation boundaries. | Your approved wording and any scope-change approval. |
| Scope and limitations | Chapters 01 and 08. | Confirm which proposed items are excluded from your submission. |
| Methodology / development process | Chapter 06 task sequence and requirements-to-code mapping in Chapter 07. | Your actual methodology, meeting dates, and stage approvals; do not invent a formal process. |
| System architecture | Chapter 02. | A diagram if your school requires one; ensure it matches MVC/direct SQL. |
| Database design / ERD / dictionary | Chapter 03 and section 9.3 below. | Verify the deployed database matches the SQL script; update the ERD accordingly. |
| Process / use cases | Chapter 04. | Approved actors, exceptional flows, and numbering required by your template. |
| Reports and sample outputs | Chapter 05. | Anonymized actual screenshots and print-preview examples after testing. |
| Testing / evaluation | Chapter 07. | Executed results, dates, participants, evidence, and actual acceptance decisions. |
| Gantt chart / development history | Chapter 06. | Confirmed actual dates and task durations, particularly the TBC entries. |
| Conclusions / recommendations | Validated outcomes plus Chapter 08 limitations. | Evidence before claiming success, accuracy, usability, or performance. |

## 9.3 Updating the earlier data dictionary

Do not merely rename the old columns while leaving an outdated ERD. The current design introduces separate lookup and transaction tables, and the relationships must be updated together.

| Earlier dictionary description | Current-source design | Documentation correction |
| --- | --- | --- |
| Admin, Customer, Staff role examples | Owner, Staff, Customer lookup rows. | Use Owner consistently; explain that the earlier draft used Admin terminology. |
| Users.Password described as encrypted | `users.password_hash`, generated with PBKDF2-SHA256. | Say password **hashing**, not reversible encryption or plaintext storage. |
| Users.Status as ENUM | `user_status_id` referencing `user_statuses`. | Include lookup relationship and Pending/Active/Inactive/Suspended values. |
| Services described with one per-kg price | `current_price` and `charging_basis` support `PerKg`, `PerItem`, and `Flat`. | Explain service-specific quantity rules and saved booking unit prices. |
| Five listed tables | Fifteen tables and three SQL views in the current script. | Use the complete current dictionary in Chapter 03. |
| Booking status as an inline ENUM | `current_status_id` referencing `booking_statuses`, plus status-history rows. | Separate current state from its change history. |
| Booking date plus separate pickup date/time | `booked_at` and nullable `scheduled_pickup_at`, with other lifecycle timestamps. | Use exact current column names/types rather than the earlier sketch. |
| Booking contains the main transaction description | Booking header, item rows, service-charge rows, and separate payment rows. | Explain one booking can have several services and several payments. |
| Delivery/rush fields described at booking level | Fields exist, but current customer add-on selections do not populate all corresponding metadata. | Document schema support separately from completed UI integration. |

The dictionary is a **schema specification**, not a dump of customer records. Do not include real passwords, password hashes, connection credentials, customer contact lists, or production transaction references in the manuscript.

## 9.4 Reusable system-description paragraph

The EZ-K Laundry System is a role-based web application that supports customer laundry booking, staff processing, payment recording, and owner reporting. Customers prepare service selections and estimated weight, review their order before submission, and view their recorded booking status and history. Staff manage operational tasks, including customer approval, booking acceptance, actual-weight entry, and payment recording or verification. The owner manages staff and service pricing and reviews customer, operational, and payment reports. The application uses ASP.NET Core MVC, Razor views, and a MySQL-compatible relational schema. Its current implementation includes printable records and transaction-report filters, with known interface, workflow, and validation limitations recorded separately.

This paragraph deliberately avoids saying “all features are fully functional,” “payments are automatically verified,” or “tracking updates in real time.” Those statements would overstate the inspected implementation.

## 9.5 Reusable booking-and-payment explanation

The customer booking process uses a review stage before the system creates the final booking record. Service selections, estimated weight, add-on quantities, and payment preference are held in a session draft and presented for checking. The customer can return to edit the draft or cancel before submission. Cash on Pickup bookings can be confirmed without an immediate payment record. For GCash, the current prototype uses a customer acknowledgement followed by staff verification; it does not connect to a live payment gateway. A booking's service total and its received-payment total are stored or calculated separately so that operational status does not automatically become proof of payment.

## 9.6 Reusable weight-and-pricing explanation

Customer-entered weight is an estimate used to calculate a preliminary service amount. Staff can record the measured weight at the shop. The system then adjusts quantities for per-kilogram service lines using the unit prices already saved for that booking, while per-piece and fixed-fee lines remain unchanged. A history note records the weight/amount change, and the customer receives an in-app notification. The current schema uses one item-weight field rather than separate estimated and actual weight columns; this is a limitation relevant to auditability and later enhancement.

When explaining a price example, label it as an example rather than a permanent shop tariff. If Washing is PHP 25/kg and Drying is PHP 25/kg, five kilograms of those two services cost PHP 250 before optional charges. At six kilograms they cost PHP 300. A fixed PHP 75 Rush Fee adds PHP 75 once, not PHP 75 for every kilogram. Always verify the current catalog before demonstrating.

## 9.7 Reusable reporting explanation

The owner has a monthly collection summary and a separate payment-transaction report. The monthly summary groups recorded Paid payments by payment month and method. The transaction report lists individual payment records and provides period, payment-method, and payment-status filters. This lets the owner request a narrower set, such as today's recorded payments, or keep All periods selected. The print action is designed to use the currently rendered report selection. These outputs summarize collections; they do not calculate net profit because operating expenses and stock consumption costs are not currently integrated. Identified filter-visibility and print-layout issues must be validated and corrected before claiming full acceptance.

## 9.8 Explaining the system during a defense

### “Why does a completed booking not immediately appear as income?”

“Magkaiba po ang laundry status at payment record. Ang Completed ay tungkol sa processing ng labahin. Sa income report, ang binibilang ay recorded Paid payments, kaya kailangan munang ma-record o ma-verify ng staff ang natanggap na bayad.”

Do not create a fake payment just to make the report nonempty. Use an authorized test record and demonstrate the proper recording step.

### “Can a customer edit before confirming?”

“May review step po bago ang final submission. Puwedeng bumalik sa form para baguhin ang services o estimate. Hindi pa final booking ang draft.”

Demonstrate that the staff pending list has not increased merely from opening Review Order, then confirm through the intended branch. Test cancellation/back/refresh behavior before the presentation.

### “Why does the customer estimate weight if the staff weighs it again?”

“Para may preliminary estimate po ang customer. Ang staff ang magre-record ng actual na timbang sa shop, kaya puwedeng magbago ang final service amount. Dapat makita rin ng customer ang pagbabago.”

Explain the current storage limitation honestly if asked: the single structured weight field is updated; the old value is in the change note rather than a dedicated estimate column.

### “Is GCash already integrated?”

“Manual declaration at staff verification pa po ang current implementation. Hindi pa ito connected sa payment gateway. Ang QR screen ngayon ay prototype at hindi proof na natanggap ang pera.”

Never present a prototype QR as a functioning merchant payment destination.

### “Can the owner choose today's transactions or print all records?”

“May transaction-report filters po para pumili ng period, method, at status. Kapag All ang pinili, hindi nililimitahan ng date filter ang listahan. Kapag may filter, iyon ang dapat makita at ma-print.”

The current date-control visibility bug must be fixed/tested in a separately authorized task before demonstrating all period selectors as working. This documentation request does not include that fix.

### “Is it real-time tracking?”

“Nakukuha po ang recorded status kapag nilo-load o nire-refresh ang page. Wala pang automatic push update sa kasalukuyang version.”

Describe the display as booking-status tracking, not guaranteed instant real-time tracking.

### “Why is there no inventory?”

“Booking at supporting laundry operations/reporting ang current implementation. Napag-usapan po ang basic monitoring ng soap at softener, pero deferred pa iyon at kailangan munang malinaw ang scope at actual shop usage rules.”

Do not claim that charging for a sachet also deducts it from inventory. There is no stock ledger in the current schema.

## 9.9 Evidence checklist before manuscript submission

Collect an anonymized screenshot or saved print preview for:

1. Customer registration validation and the Pending-approval response.
2. Staff approval and successful role-specific login afterward.
3. Service selection with at least one quantity-based add-on.
4. Review Order and return-to-edit behavior.
5. Cash booking without a fabricated upfront payment.
6. The distinction between a GCash declaration and staff-recorded verification.
7. Two or more customer tracking cards with different recorded statuses.
8. Staff actual-weight change with correct before/after arithmetic.
9. The resulting customer notification and Mark All as Read behavior.
10. Recorded payment, remaining balance, and matching owner income.
11. Unfiltered and date-filtered transaction reports after fixing/testing the controls.
12. Print previews showing complete column headings and uncut rows.

For each item, record test ID, execution date, anonymous test account, input values, actual result, and evidence filename. A screenshot of a button alone is not proof of the associated backend behavior.

## 9.10 Statements to avoid until supported

- “The system eliminates all booking errors.” No measured evaluation supports this.
- “All transactions are automatically paid through GCash.” No live gateway is integrated.
- “The system records net profit.” Current reports summarize payment collections.
- “Stock is automatically deducted according to laundry weight.” Inventory is not implemented.
- “The project was fully completed on every timestamp in the timeline.” File dates are not acceptance dates.
- “The system is fully secure/production-ready.” Important hardening and validation work remains.
- “Every notification opens the related order.” Current notification cards do not implement that detail navigation.
- “All tests passed.” This documentation package supplies test cases, not fabricated execution results.

Keep a clear distinction between your completed demonstration, the current implementation, and future enhancements. That makes the manuscript easier to defend and later update.
