# Database Design and Data Dictionary

**System:** EZ-K Laundry System  
**Documentation snapshot:** September 30, 2026  
**Evidence:** current SQL schema and controller source; not a certification of the running database.

## 1. Purpose, scope, and evidence

The database supports account approval, customer and walk-in bookings, selected laundry services and add-ons, laundry processing, payment recording, notifications, rewards transactions, and owner reporting. This chapter describes what is actually defined in the repository, distinguishing database capability from functionality exposed by the application.

Primary sources:

- [laundry_final_3nf.sql](../laundry_final_3nf.sql): table definitions, constraints, indexes, reference records, and reporting views.
- [CustomerController.cs](../Controllers/CustomerController.cs): customer draft finalization, receipts, notifications, and reward redemption.
- [StaffController.cs](../Controllers/StaffController.cs): walk-in creation, status updates, actual-weight recording, and payments.
- [OwnerController.cs](../Controllers/OwnerController.cs): account administration, pricing, and report queries.
- [PasswordSecurity.cs](../Data/PasswordSecurity.cs): application-generated password hashes.

The SQL file contains **15 tables and 3 views**. Its header describes a MySQL 8.0 / MariaDB-compatible design. The installed database engine/version and enforcement of constraints have not been independently verified for this documentation. No database rows were changed or exported during preparation of this chapter. Historical creation and deployment dates cannot be established from the schema text alone; September 30 is the documentation date, not an invented database implementation date.

## 2. Physical design and normalization

The script targets database `laundry_final`, uses `utf8mb4` with `utf8mb4_unicode_ci`, defines InnoDB tables, and sets the importing connection's time zone to `+08:00`. That `SET time_zone` statement does not, by itself, configure every later application connection.

Reference tables store role, account-status, booking-status, payment-method, payment-status, and item-type labels once. Transaction tables reference their identifiers instead of copying these labels into every row. Bookings are separated from physical laundry items, purchased service lines, payments, and history. This avoids repeated customer details and supports several service lines and payment records for one booking.

The design is intended to support third normal form: non-key attributes describe the entity represented by their table; reusable descriptions are separated; many-to-many booking/service selections use a linking table. `booking_services.unit_price` is an intentional historical sale-price snapshot, not a duplicate of the current catalogue price with the same meaning. A later owner price change should affect new selections, not overwrite the unit price of existing booked lines.

The design does **not** include inventory balances, stock movements, suppliers, product purchase costs, dosing rules, or a stock-usage ledger. A consumable service line is a charge to a customer, not proof of remaining stock.

### Relationship summary

| Parent | Child | Relationship and business meaning |
|---|---|---|
| `roles`, `user_statuses` | `users` | Each account has one role and account status. |
| `users` | `bookings.customer_id` | One customer may own many bookings. |
| `users` | `bookings.created_by_user_id` | Optional user who entered a booking. |
| `booking_statuses` | `bookings` | One current operational status per booking. |
| `bookings` | `booking_items` | One booking can contain several physical item records. |
| `item_types` | `booking_items` | Each item record has one classification. |
| `bookings`, `services` | `booking_services` | Many-to-many relationship, with quantity and sale price. |
| `bookings` | `payments` | Zero or more payment attempts/receipts per booking. |
| `payment_methods`, `payment_statuses` | `payments` | One method and status per payment record. |
| `bookings`, `booking_statuses` | `booking_status_history` | Recorded status/audit events for a booking. |
| `users`, optionally `bookings` | `notifications` | A recipient's account, booking, or other message. |
| `users`, optionally `bookings` | `reward_transactions` | Signed point movements for a customer. |

An FK to `users` confirms that the user exists, but does not itself prove that `customer_id` references a Customer or that a recording user is Staff. Role checks are application responsibilities.

## 3. Dictionary conventions

`PK` means primary key; `FK` means foreign key; `AI` means auto-increment. `Yes` in the nullable column means SQL `NULL` is allowed. Unless stated otherwise, a non-nullable column has no explicit default and must be supplied. All primary keys are non-nullable auto-increment identifiers. `BOOLEAN` is the declared SQL type; its underlying representation/enforcement depends on the installed compatible engine.

### 3.1 `roles`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `role_id` | `TINYINT UNSIGNED` | No | PK, AI. |
| `role_name` | `VARCHAR(50)` | No | Unique role label. |
| `description` | `VARCHAR(255)` | Yes | Role explanation. |

Unique constraint: `uq_roles_role_name` on `role_name`.

### 3.2 `user_statuses`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `user_status_id` | `TINYINT UNSIGNED` | No | PK, AI. |
| `status_name` | `VARCHAR(50)` | No | Unique account-state label. |
| `description` | `VARCHAR(255)` | Yes | Meaning of that state. |

Unique constraint: `uq_user_statuses_status_name` on `status_name`.

### 3.3 `users`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `user_id` | `BIGINT UNSIGNED` | No | PK, AI. |
| `role_id` | `TINYINT UNSIGNED` | No | FK to `roles.role_id`. |
| `user_status_id` | `TINYINT UNSIGNED` | No | FK to `user_statuses.user_status_id`. |
| `first_name` | `VARCHAR(100)` | No | First/given name; trimmed value must not be empty. |
| `last_name` | `VARCHAR(100)` | No | Last/family name; trimmed value must not be empty. |
| `email` | `VARCHAR(191)` | Yes | Unique when supplied. |
| `phone` | `VARCHAR(20)` | Yes | Unique when supplied. |
| `password_hash` | `VARCHAR(255)` | Yes | Application-generated hash, never a plaintext password. |
| `address` | `VARCHAR(255)` | Yes | Account-level address. |
| `is_walk_in` | `BOOLEAN` | No | Default `FALSE`; permits non-login walk-in records. |
| `created_at` | `DATETIME` | No | Default `CURRENT_TIMESTAMP`. |
| `updated_at` | `DATETIME` | No | Default current timestamp; automatically updates on row modification. |

Unique constraints: `uq_users_email`, `uq_users_phone`. Named indexes: `ix_users_role_id`, `ix_users_status_id`, and `ix_users_name(last_name, first_name)`. The two FKs use `ON DELETE RESTRICT`.

Checks: `ck_users_first_name`, `ck_users_last_name`, and `ck_users_account_credentials`. The credentials check requires a non-null email and hash for a non-walk-in account, but it does not validate email format, hash strength, or non-empty credentials. Application validation supplies additional checks. Nullable unique email/phone fields permit multiple missing values; empty strings are different from `NULL`.

### 3.4 `item_types`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `item_type_id` | `SMALLINT UNSIGNED` | No | PK, AI. |
| `item_name` | `VARCHAR(100)` | No | Unique laundry classification. |
| `description` | `VARCHAR(255)` | Yes | Classification explanation. |
| `is_active` | `BOOLEAN` | No | Default `TRUE`. |

Unique constraint: `uq_item_types_item_name`. Although several types are seeded, current customer and walk-in creation select the `Clothes` type.

### 3.5 `services`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `service_id` | `SMALLINT UNSIGNED` | No | PK, AI. |
| `service_name` | `VARCHAR(100)` | No | Unique catalogue name. |
| `service_category` | `VARCHAR(20)` | No | Default `Laundry`; allowed `Laundry`, `Consumable`, `Fee`. |
| `charging_basis` | `VARCHAR(20)` | No | Allowed `PerKg`, `PerItem`, `Flat`. |
| `current_price` | `DECIMAL(10,2)` | No | Current selling price; must be nonnegative. |
| `estimated_hours` | `SMALLINT UNSIGNED` | Yes | Optional estimated duration. |
| `description` | `VARCHAR(500)` | Yes | Service explanation. |
| `is_active` | `BOOLEAN` | No | Default `TRUE`. |
| `created_at` | `DATETIME` | No | Default current timestamp. |
| `updated_at` | `DATETIME` | No | Default current timestamp; auto-updated. |

Unique constraint: `uq_services_service_name`. Checks: `ck_services_category`, `ck_services_charging_basis`, `ck_services_current_price`. No quantity-on-hand column exists. Current application queries depend on exact catalogue names, so renaming entries requires a code-impact review.

### 3.6 `booking_statuses`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `booking_status_id` | `TINYINT UNSIGNED` | No | PK, AI. |
| `status_name` | `VARCHAR(50)` | No | Unique operational-state label. |
| `display_order` | `TINYINT UNSIGNED` | No | Unique presentation order. |
| `is_terminal` | `BOOLEAN` | No | Default `FALSE`; terminal-state flag. |
| `description` | `VARCHAR(255)` | Yes | Status explanation. |

Unique constraints: `uq_booking_statuses_status_name`, `uq_booking_statuses_display_order`. Display order is not a database-enforced transition rule.

### 3.7 `bookings`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `booking_id` | `BIGINT UNSIGNED` | No | PK, AI; staff short references use this identifier. |
| `booking_code` | `VARCHAR(30)` | No | Unique customer-facing reference. |
| `customer_id` | `BIGINT UNSIGNED` | No | FK to the owning user. |
| `current_status_id` | `TINYINT UNSIGNED` | No | FK to `booking_statuses`. |
| `created_by_user_id` | `BIGINT UNSIGNED` | Yes | Optional creator FK to `users`. |
| `booked_at` | `DATETIME` | No | Default current timestamp. |
| `scheduled_pickup_at` | `DATETIME` | Yes | Optional pickup schedule. |
| `fulfillment_method` | `VARCHAR(20)` | No | Default `DropOff`; allowed `DropOff`, `Pickup`, `Delivery`. |
| `delivery_address` | `VARCHAR(255)` | Yes | Required to be non-null when method is `Delivery`. |
| `is_rush` | `BOOLEAN` | No | Default `FALSE`. |
| `customer_notes` | `VARCHAR(1000)` | Yes | Optional instructions. |
| `completed_at` | `DATETIME` | Yes | Completion timestamp when recorded. |
| `updated_at` | `DATETIME` | No | Default current timestamp; auto-updated. |

Unique constraint: `uq_bookings_booking_code`. Indexes: `ix_bookings_customer_id`, `ix_bookings_current_status_id`, `ix_bookings_booked_at`, `ix_bookings_created_by_user_id`. Customer/status deletion is restricted; deleting a creator sets the optional creator FK to null. Checks: `ck_bookings_fulfillment_method`, `ck_bookings_delivery_address`. The delivery check permits an empty but non-null address; it is not full address validation.

### 3.8 `booking_items`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `booking_item_id` | `BIGINT UNSIGNED` | No | PK, AI. |
| `booking_id` | `BIGINT UNSIGNED` | No | FK to `bookings`. |
| `item_type_id` | `SMALLINT UNSIGNED` | No | FK to `item_types`. |
| `quantity` | `INT UNSIGNED` | Yes | Physical item count, if applicable. |
| `weight_kg` | `DECIMAL(7,2)` | Yes | Weight stored to two decimal places. |
| `item_notes` | `VARCHAR(500)` | Yes | Optional item-specific notes. |

Indexes: `ix_booking_items_booking_id`, `ix_booking_items_item_type_id`. Booking deletion cascades; item-type deletion is restricted. `ck_booking_items_measurement` requires a positive quantity **or** a positive weight. It does not independently require every supplied measurement to be positive when the other side of the OR already passes. There is no unique `(booking_id,item_type_id)` constraint and no separate estimated/actual-weight field.

### 3.9 `booking_services`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `booking_service_id` | `BIGINT UNSIGNED` | No | PK, AI. |
| `booking_id` | `BIGINT UNSIGNED` | No | FK to `bookings`. |
| `service_id` | `SMALLINT UNSIGNED` | No | FK to catalogue entry. |
| `quantity` | `DECIMAL(9,2)` | No | Default `1.00`; positive billing quantity. |
| `unit_price` | `DECIMAL(10,2)` | No | Nonnegative sale-price snapshot. |

Unique constraint `uq_booking_services_booking_service(booking_id,service_id)` allows each catalogue entry once per booking. Extra pieces are represented by quantity, not duplicate rows. Index: `ix_booking_services_service_id`. Booking deletion cascades; service deletion is restricted. Checks: `ck_booking_services_quantity`, `ck_booking_services_unit_price`.

For `PerKg`, quantity represents kilograms; for `PerItem`, pieces; for `Flat`, current creation logic uses one charge. The database permits fractional quantities regardless of basis. Price immutability is an application convention: no trigger blocks a direct SQL change to `unit_price`.

### 3.10 `payment_methods`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `payment_method_id` | `TINYINT UNSIGNED` | No | PK, AI. |
| `method_name` | `VARCHAR(50)` | No | Unique payment-method name. |
| `is_active` | `BOOLEAN` | No | Default `TRUE`. |

Unique constraint: `uq_payment_methods_method_name`. Seeded methods exceed the two exposed by the current payment-recording UI.

### 3.11 `payment_statuses`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `payment_status_id` | `TINYINT UNSIGNED` | No | PK, AI. |
| `status_name` | `VARCHAR(50)` | No | Unique payment-state label. |
| `description` | `VARCHAR(255)` | Yes | Meaning of that state. |

Unique constraint: `uq_payment_statuses_status_name`.

### 3.12 `payments`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `payment_id` | `BIGINT UNSIGNED` | No | PK, AI. |
| `booking_id` | `BIGINT UNSIGNED` | No | FK to the associated booking. |
| `payment_method_id` | `TINYINT UNSIGNED` | No | FK to `payment_methods`. |
| `payment_status_id` | `TINYINT UNSIGNED` | No | FK to `payment_statuses`. |
| `recorded_by_user_id` | `BIGINT UNSIGNED` | Yes | Optional recording/verification user FK. |
| `amount` | `DECIMAL(12,2)` | No | Positive amount for this payment row. |
| `transaction_reference` | `VARCHAR(100)` | Yes | Unique external/reference value when supplied. |
| `paid_at` | `DATETIME` | Yes | Receipt/verification time, when known. |
| `notes` | `VARCHAR(500)` | Yes | Declaration, verification, or other explanation. |
| `created_at` | `DATETIME` | No | Default current timestamp. |

Unique constraint: `uq_payments_transaction_reference`. Indexes: `ix_payments_booking_id`, `ix_payments_method_id`, `ix_payments_status_id`, `ix_payments_recorded_by_user_id`. Booking, method, and status deletion is restricted; deleting a recorder sets that FK to null. Check: `ck_payments_amount` (`amount > 0`).

There is no unique booking/payment constraint: installments and multiple attempts are supported structurally. There is no database check that the total received is within the booking total, that Paid rows have `paid_at`, or that cancelled bookings cannot receive money. Current staff payment code supplies those business validations in its workflow. Generated report IDs such as `TXN-00000001` are display fallbacks, not evidence of a GCash gateway reference.

### 3.13 `booking_status_history`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `booking_status_history_id` | `BIGINT UNSIGNED` | No | PK, AI. |
| `booking_id` | `BIGINT UNSIGNED` | No | FK to `bookings`. |
| `booking_status_id` | `TINYINT UNSIGNED` | No | Status associated with the event. |
| `changed_by_user_id` | `BIGINT UNSIGNED` | Yes | Optional actor FK to `users`. |
| `changed_at` | `DATETIME` | No | Default current timestamp. |
| `notes` | `VARCHAR(500)` | Yes | Audit explanation. |

Indexes: `ix_booking_status_history_booking_id(booking_id,changed_at)`, `ix_booking_status_history_status_id`, `ix_booking_status_history_changed_by_user_id`. Booking deletion cascades, status deletion is restricted, actor deletion sets null. No trigger automatically adds an event when `bookings` is updated. The controller must write history; actual-weight recording also adds an event retaining the current status, so not every history row represents a status transition.

### 3.14 `notifications`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `notification_id` | `BIGINT UNSIGNED` | No | PK, AI. |
| `user_id` | `BIGINT UNSIGNED` | No | Recipient FK to `users`. |
| `booking_id` | `BIGINT UNSIGNED` | Yes | Optional related booking FK. |
| `notification_type` | `VARCHAR(30)` | No | Default `System`; constrained category. |
| `title` | `VARCHAR(150)` | No | Short message heading. |
| `message` | `VARCHAR(1000)` | No | Message body. |
| `is_read` | `BOOLEAN` | No | Default `FALSE`. |
| `sent_at` | `DATETIME` | No | Default current timestamp. |
| `read_at` | `DATETIME` | Yes | Read acknowledgement timestamp. |

Indexes: `ix_notifications_user_read(user_id,is_read,sent_at)` and `ix_notifications_booking_id`. User/booking deletions cascade. `ck_notifications_type` permits `System`, `Account`, `Booking`, `Payment`, `Promotion`. `ck_notifications_read_at` requires a non-null read timestamp when marked read; it does not require unread rows to have a null timestamp.

### 3.15 `reward_transactions`

| Column | Declared type | Nullable | Definition/default |
|---|---|---|---|
| `reward_transaction_id` | `BIGINT UNSIGNED` | No | PK, AI. |
| `user_id` | `BIGINT UNSIGNED` | No | Account FK to `users`. |
| `booking_id` | `BIGINT UNSIGNED` | Yes | Optional earning/redeeming booking link. |
| `transaction_type` | `VARCHAR(20)` | No | Allowed `Earn`, `Redeem`, `Adjustment`, `Expire`. |
| `points` | `INT` | No | Nonzero signed point change. |
| `description` | `VARCHAR(255)` | Yes | Reason for the movement. |
| `created_at` | `DATETIME` | No | Default current timestamp. |

Indexes: `ix_reward_transactions_user_id(user_id,created_at)`, `ix_reward_transactions_booking_id`. User deletion cascades; booking deletion sets null. Checks: `ck_reward_transactions_type`, `ck_reward_transactions_points`, `ck_reward_transactions_sign`. Earn must be positive, Redeem/Expire negative, and Adjustment may have either nonzero sign. There is no database constraint that the accumulated balance cannot become negative.

## 4. Referential integrity and deletion effects

All declared FKs use `ON UPDATE CASCADE`. Delete behavior deliberately varies:

- **RESTRICT** protects reference records in use, customers owning bookings, and bookings with payment records.
- **CASCADE** removes booking item/service/history rows and booking-linked notifications if a booking is actually deleted. User-linked notifications and reward movements also cascade with their user.
- **SET NULL** preserves a booking/payment/history record when its optional actor is deleted, and preserves reward movements when their optional booking is deleted.

Cancellation in the application is a status change, not physical deletion of a booking. It preserves customer history. However, FK cascades do not constitute a permanent immutable audit archive. Backup, retention, and any administrative deletion policy need separate decisions.

## 5. Seed catalogue and stable reference values

These are **SQL seed values**, not a claim that the live shop still uses these prices. Owner pricing changes and earlier screenshots may differ.

| Reference table | Explicit seeded IDs and labels |
|---|---|
| `roles` | 1 Owner; 2 Staff; 3 Customer |
| `user_statuses` | 1 Pending; 2 Active; 3 Inactive; 4 Suspended |
| `item_types` | 1 Clothes; 2 Blanket; 3 Comforter; 4 Curtain; 5 Shoes; all active |
| `booking_statuses` | 1 Pending; 2 Washing; 3 Drying; 4 Folding; 5 Ready; 6 Completed; 7 Cancelled |
| `payment_methods` | 1 Cash; 2 GCash; 3 Maya; 4 Bank Transfer; all active |
| `payment_statuses` | 1 Pending; 2 Partial; 3 Paid; 4 Failed; 5 Refunded |

Booking display orders are 1–7 in the same sequence. Only Completed and Cancelled are seeded terminal states.

| Service ID/name | Category | Basis | Seed price (PHP) | Estimated hours |
|---|---|---|---:|---:|
| 1 Washing | Laundry | PerKg | 25.00 | 2 |
| 2 Drying | Laundry | PerKg | 25.00 | 2 |
| 3 Folding | Laundry | PerKg | 10.00 | 1 |
| 4 Powder | Consumable | PerItem | 15.00 | Not supplied |
| 5 Downy | Consumable | PerItem | 15.00 | Not supplied |
| 6 Rush Fee | Fee | Flat | 75.00 | Not supplied |
| 7 Delivery Fee | Fee | Flat | 50.00 | Not supplied |
| 8 Self Service | Laundry | PerKg | 0.00 | Not supplied |

All eight services are seeded active. Customer draft creation refuses Self Service when its price is zero or negative, requiring the Owner to set a usable price first. No default account or password is inserted by the SQL file.

**Re-import warning:** `CREATE TABLE IF NOT EXISTS` avoids dropping tables but does not migrate existing column definitions. Reference inserts use `ON DUPLICATE KEY UPDATE`, including `services.current_price`; rerunning the entire script can overwrite owner-maintained prices with seeds. `CREATE OR REPLACE VIEW` replaces the three view definitions. Therefore, “non-destructive” here means no explicit table/database DROP, not “no existing configuration changes.” Back up and compare before a deliberate import; this documentation task performed no import.

## 6. Views and calculation rules

### `v_booking_totals`

One row per booking: `booking_id`, `booking_code`, and `order_total`. Its formula is `SUM(booking_services.quantity * unit_price)`, defaulting to zero when no line exists. The booking table does not store a separate total that can drift from its lines.

### `v_booking_payment_balances`

One row per booking: booking identifiers, `order_total`, `amount_paid`, and `balance_due`. The view counts payment rows whose status is **Partial or Paid**, then subtracts that sum from the order total. It does not clamp a negative balance to zero.

**Current inconsistency:** Staff balance calculations, the actual-weight guard, and Owner earnings count **Paid only**. The schema view therefore disagrees with those controllers if a Partial-status row is introduced. Current staff installments are individually recorded as Paid even when the booking still has a balance. Decide and standardize the meaning of Partial before relying on externally entered Partial records.

### `v_user_reward_balances`

One row per user: `user_id` and `reward_points = SUM(points)`, defaulting to zero. This supports a ledger-derived balance without a duplicated balance column. The view's presence does not prove that all reward-earning rules or displayed balances are connected to it.

The inspected controllers compute their own aggregates; they do not call these three views. A schema view and a page with a similar label are not automatically the same data path.

### Reporting date distinctions

| Value | Meaning in the current source |
|---|---|
| `bookings.booked_at` | When the order was created. |
| `payments.created_at` | When a payment/declaration record was inserted. |
| `payments.paid_at` | When staff recorded or verified received payment. |
| Monthly income | Paid rows with non-null `paid_at`, grouped by that month/year. |
| Transaction report date | `COALESCE(paid_at, created_at)`; allows unpaid declarations to appear. |

Total Earnings counts every Paid row, while the monthly income query additionally requires `paid_at`. A malformed Paid row without a payment date could therefore increase the headline without appearing in a monthly row. Staff recording sets `paid_at`, but the schema itself does not enforce that pairing.

## 7. Database-to-application write workflows

### Customer booking

The customer selects services and reviews a server-created draft kept in session. The draft is not yet a `bookings` row. Finalization writes, within one database transaction: Pending booking, one Clothes item, selected booking-service lines, optional Pending GCash declaration, and initial status-history event. Customer Cash-on-Pickup booking creates no payment record until staff records received money. Duplicate-key handling looks up an existing booking using the same draft code/customer after rollback; it is not a general guarantee against every duplicate submission.

The GCash declaration amount is currently the draft's whole service total. Despite method names such as `ProcessPartialPayment`, this is not an integrated gateway verification or a documented percentage-deposit calculation. Staff verification is separate.

### Staff actual-weight adjustment

Within a transaction the code locks the booking and first Clothes item, rejects terminal bookings, calculates the revised service total using saved sale prices, and rejects a total below already Paid amounts. It changes `weight_kg` and the quantity of all PerKg service lines, leaving PerItem/Flat quantities unchanged. It appends a same-status history event and a booking-linked customer notification, then commits.

The original estimate is overwritten in the structured item field; the audit note retains the previous/new values as text. The schema does not record a separate immutable estimate, explicit actual-weight timestamp, or structured weighed-by field. Existing Pending GCash declaration amounts are not automatically recalculated by this action.

### Staff payment recording

The code locks the booking, accepts Cash/GCash only, rejects cancelled bookings and invalid or excessive amounts, and totals existing Paid rows. It either verifies the oldest Pending GCash row or inserts a new Paid row, with recorder and `paid_at`. Paying the remaining balance in Cash marks existing Pending GCash declarations Failed with an explanatory note. A customer payment notification is inserted before commit.

That payment notification currently contains the booking code in its text but does not populate its optional `booking_id`. A bank/GCash reference is not required or captured by this payment action. Do not describe the feature as automatic bank reconciliation.

### Laundry status changes

Controller operations update the current status and append history in a transaction. Some terminal-action paths check allowed source states, while the general update path does not impose the full ordered lifecycle. There are no database triggers enforcing state progression, generating notifications for every transition, or requiring full payment before Completed. “Completed laundry” and “Paid payment” remain separate concepts.

## 8. Current schema-to-feature gaps

| Area | Evidence-backed limitation |
|---|---|
| Rush and delivery metadata | Customer creation inserts only code/customer/status into `bookings`. A selected Rush Fee does not set `is_rush`; a Delivery Fee does not set fulfillment/address. The booking keeps defaults unless another path changes them. |
| Payment preference | There is no preferred-payment-method FK on `bookings`. Cash-on-Pickup choice is not independently retained after finalization until an actual payment record exists. |
| Multiple item types | Schema supports multiple items; current forms normally create one Clothes item. Weight adjustment changes only the first Clothes item. |
| Price snapshots | Unit prices are saved, but historical service names and charging bases are still read from the current catalogue. Renaming/reclassifying a catalogue entry can change the interpretation/display of old bookings. |
| Quantity precision | Stored weights have two decimals; Staff validation accepts positive decimals up to 999.99. Precision/rounding at input boundaries deserves a test, particularly below 0.01 or above two decimal places. |
| Identifier sizes | SQL uses unsigned BIGINT in many tables, while several controllers/session identifiers use 32-bit integers. Large-scale ID capacity is not aligned. |
| Reporting indexes | Named payment indexes cover relationships/status, not `paid_at` or `created_at`. Date-filter performance has not been benchmarked. |
| Refunds and other methods | Refunded, Maya, and Bank Transfer exist as reference values; their presence does not prove dedicated refund or payment workflows are implemented. |
| Stock monitoring | Powder/Downy sales are stored, but no stock receipt, balance, consumption standard, or purchase-price history exists. Inventory remains proposed, not completed. |

## 9. Comparison with the earlier dictionary document

The separately located [dictionary.docx](../../dictionary.docx) was inspected read-only during this documentation task. It is an earlier five-table dictionary, not evidence of an approved complete manuscript. Its filesystem timestamps place it on August 6, 2026, but timestamps alone do not establish when each design decision was authored or implemented.

| Earlier dictionary | Current repository schema |
|---|---|
| Five tables: Roles, Users, Services, Item_Type, Bookings | Fifteen tables plus three derived reporting views. |
| Admin role terminology | Owner role terminology in seed data and application. |
| Users.Password described as encrypted | `users.password_hash`; application uses salted PBKDF2-SHA256 hashing, not reversible password encryption. |
| Account status ENUM | `user_statuses` lookup referenced by `users.user_status_id`. |
| Per-kilogram service price, with a malformed combined field label | `services.current_price` plus explicit category and charging basis; historical prices in `booking_services`. |
| Separate booking date, pickup date/time, delivery option, and booking-status ENUM | `booked_at`, optional `scheduled_pickup_at`, checked `fulfillment_method`, and `current_status_id` FK. |
| Basic booking-centred structure | Separate item lines, service charges, payments, audit history, notifications, and reward movements. |

Update the manuscript's database diagrams and dictionary to match the current schema, while retaining the earlier file as design-history evidence. Do not claim that every field added to the schema has a finished interface; the gaps above remain relevant.

## 10. Verification and maintenance checklist

Before citing this chapter as the deployed database specification:

1. Record the actual database product/version and capture `SHOW CREATE TABLE` and `SHOW CREATE VIEW` in an authorized environment; compare them with the repository script.
2. Confirm FK and CHECK enforcement using an isolated test database, never by inserting invalid data into real customer records.
3. Test unique references, null walk-in credentials, multi-service totals, partial installments, cancelled-order payment rejection, and actual-weight rollback when the new total would be below received money.
4. Reconcile a known Paid transaction against staff balance, daily cash, monthly income, filtered report, and printed output. Check time zones at a day/month boundary.
5. Resolve the Partial/Paid interpretation before using the balance view interchangeably with controller totals.
6. Keep database backups separate from this documentation folder. Exclude connection passwords, account hashes, and real customer exports from documentation handed to advisers.
7. Introduce deliberate versioned migrations for future structural changes rather than treating a repeated initial-schema import as a migration process.

These are verification tasks and maintenance recommendations, not a record of tests already executed. This documentation snapshot changes no application logic, database structure, prices, accounts, or transaction records.
