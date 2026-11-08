-- EZK Laundry System
-- MySQL 8.0 / MariaDB-compatible 3NF database schema
--
-- This script is intentionally non-destructive:
--   * It does not DROP an existing database or table.
--   * Reference data is inserted idempotently.
--   * No default user is created because passwords must be securely hashed
--     by the application before they are stored.

SET NAMES utf8mb4;
SET time_zone = '+08:00';

CREATE DATABASE IF NOT EXISTS laundry_final
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE laundry_final;

-- 1. Roles: one row per system role.
CREATE TABLE IF NOT EXISTS roles (
    role_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    role_name VARCHAR(50) NOT NULL,
    description VARCHAR(255) NULL,
    PRIMARY KEY (role_id),
    CONSTRAINT uq_roles_role_name UNIQUE (role_name)
) ENGINE = InnoDB;

-- 2. User statuses: account lifecycle values are stored once.
CREATE TABLE IF NOT EXISTS user_statuses (
    user_status_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    status_name VARCHAR(50) NOT NULL,
    description VARCHAR(255) NULL,
    PRIMARY KEY (user_status_id),
    CONSTRAINT uq_user_statuses_status_name UNIQUE (status_name)
) ENGINE = InnoDB;

-- 3. Users: Owner, Staff, registered Customer, or walk-in Customer.
CREATE TABLE IF NOT EXISTS users (
    user_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    role_id TINYINT UNSIGNED NOT NULL,
    user_status_id TINYINT UNSIGNED NOT NULL,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(191) NULL,
    phone VARCHAR(20) NULL,
    password_hash VARCHAR(255) NULL,
    address VARCHAR(255) NULL,
    is_walk_in BOOLEAN NOT NULL DEFAULT FALSE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (user_id),
    CONSTRAINT uq_users_email UNIQUE (email),
    CONSTRAINT uq_users_phone UNIQUE (phone),
    KEY ix_users_role_id (role_id),
    KEY ix_users_status_id (user_status_id),
    KEY ix_users_name (last_name, first_name),
    CONSTRAINT fk_users_role
        FOREIGN KEY (role_id) REFERENCES roles (role_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_users_status
        FOREIGN KEY (user_status_id) REFERENCES user_statuses (user_status_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT ck_users_first_name CHECK (CHAR_LENGTH(TRIM(first_name)) > 0),
    CONSTRAINT ck_users_last_name CHECK (CHAR_LENGTH(TRIM(last_name)) > 0),
    CONSTRAINT ck_users_account_credentials CHECK (
        is_walk_in = TRUE OR (email IS NOT NULL AND password_hash IS NOT NULL)
    )
) ENGINE = InnoDB;

-- 4. Item types: physical laundry classifications.
CREATE TABLE IF NOT EXISTS item_types (
    item_type_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
    item_name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (item_type_id),
    CONSTRAINT uq_item_types_item_name UNIQUE (item_name)
) ENGINE = InnoDB;

-- 5. Services: services, consumables, and fees share one price catalogue.
CREATE TABLE IF NOT EXISTS services (
    service_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
    service_name VARCHAR(100) NOT NULL,
    service_category VARCHAR(20) NOT NULL DEFAULT 'Laundry',
    product_type VARCHAR(30) NULL,
    charging_basis VARCHAR(20) NOT NULL,
    current_price DECIMAL(10, 2) NOT NULL,
    estimated_hours SMALLINT UNSIGNED NULL,
    description VARCHAR(500) NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (service_id),
    CONSTRAINT uq_services_service_name UNIQUE (service_name),
    CONSTRAINT ck_services_category CHECK (
        service_category IN ('Laundry', 'Consumable', 'Fee')
    ),
    CONSTRAINT ck_services_charging_basis CHECK (
        charging_basis IN ('PerKg', 'PerLoad', 'PerItem', 'Flat')
    ),
    CONSTRAINT ck_services_current_price CHECK (current_price >= 0)
) ENGINE = InnoDB;

-- 6. Booking statuses: operational workflow values.
CREATE TABLE IF NOT EXISTS booking_statuses (
    booking_status_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    status_name VARCHAR(50) NOT NULL,
    display_order TINYINT UNSIGNED NOT NULL,
    is_terminal BOOLEAN NOT NULL DEFAULT FALSE,
    description VARCHAR(255) NULL,
    PRIMARY KEY (booking_status_id),
    CONSTRAINT uq_booking_statuses_status_name UNIQUE (status_name),
    CONSTRAINT uq_booking_statuses_display_order UNIQUE (display_order)
) ENGINE = InnoDB;

-- 7. Bookings: one row per customer order.
CREATE TABLE IF NOT EXISTS bookings (
    booking_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    booking_code VARCHAR(30) NOT NULL,
    customer_id BIGINT UNSIGNED NOT NULL,
    current_status_id TINYINT UNSIGNED NOT NULL,
    created_by_user_id BIGINT UNSIGNED NULL,
    booked_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    scheduled_pickup_at DATETIME NULL,
    fulfillment_method VARCHAR(20) NOT NULL DEFAULT 'DropOff',
    delivery_address VARCHAR(255) NULL,
    is_rush BOOLEAN NOT NULL DEFAULT FALSE,
    customer_notes VARCHAR(1000) NULL,
    completed_at DATETIME NULL,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (booking_id),
    CONSTRAINT uq_bookings_booking_code UNIQUE (booking_code),
    KEY ix_bookings_customer_id (customer_id),
    KEY ix_bookings_current_status_id (current_status_id),
    KEY ix_bookings_booked_at (booked_at),
    KEY ix_bookings_created_by_user_id (created_by_user_id),
    CONSTRAINT fk_bookings_customer
        FOREIGN KEY (customer_id) REFERENCES users (user_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_bookings_current_status
        FOREIGN KEY (current_status_id) REFERENCES booking_statuses (booking_status_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_bookings_created_by
        FOREIGN KEY (created_by_user_id) REFERENCES users (user_id)
        ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT ck_bookings_fulfillment_method CHECK (
        fulfillment_method IN ('DropOff', 'Pickup', 'Delivery')
    ),
    CONSTRAINT ck_bookings_delivery_address CHECK (
        fulfillment_method <> 'Delivery' OR delivery_address IS NOT NULL
    )
) ENGINE = InnoDB;

-- 8. Booking items: the physical item types and measured quantities.
CREATE TABLE IF NOT EXISTS booking_items (
    booking_item_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    booking_id BIGINT UNSIGNED NOT NULL,
    item_type_id SMALLINT UNSIGNED NOT NULL,
    quantity INT UNSIGNED NULL,
    weight_kg DECIMAL(7, 2) NULL,
    item_notes VARCHAR(500) NULL,
    PRIMARY KEY (booking_item_id),
    KEY ix_booking_items_booking_id (booking_id),
    KEY ix_booking_items_item_type_id (item_type_id),
    CONSTRAINT fk_booking_items_booking
        FOREIGN KEY (booking_id) REFERENCES bookings (booking_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_booking_items_item_type
        FOREIGN KEY (item_type_id) REFERENCES item_types (item_type_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT ck_booking_items_measurement CHECK (
        (quantity IS NOT NULL AND quantity > 0)
        OR (weight_kg IS NOT NULL AND weight_kg > 0)
    )
) ENGINE = InnoDB;

-- 9. Booking services: selected catalogue entries and immutable sale prices.
CREATE TABLE IF NOT EXISTS booking_services (
    booking_service_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    booking_id BIGINT UNSIGNED NOT NULL,
    service_id SMALLINT UNSIGNED NOT NULL,
    quantity DECIMAL(9, 2) NOT NULL DEFAULT 1.00,
    unit_price DECIMAL(10, 2) NOT NULL,
    charging_basis VARCHAR(20) NULL,
    PRIMARY KEY (booking_service_id),
    CONSTRAINT uq_booking_services_booking_service UNIQUE (booking_id, service_id),
    KEY ix_booking_services_service_id (service_id),
    CONSTRAINT fk_booking_services_booking
        FOREIGN KEY (booking_id) REFERENCES bookings (booking_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_booking_services_service
        FOREIGN KEY (service_id) REFERENCES services (service_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT ck_booking_services_quantity CHECK (quantity > 0),
    CONSTRAINT ck_booking_services_unit_price CHECK (unit_price >= 0)
) ENGINE = InnoDB;

-- 10. Payment methods.
CREATE TABLE IF NOT EXISTS payment_methods (
    payment_method_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    method_name VARCHAR(50) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    PRIMARY KEY (payment_method_id),
    CONSTRAINT uq_payment_methods_method_name UNIQUE (method_name)
) ENGINE = InnoDB;

-- 11. Payment statuses.
CREATE TABLE IF NOT EXISTS payment_statuses (
    payment_status_id TINYINT UNSIGNED NOT NULL AUTO_INCREMENT,
    status_name VARCHAR(50) NOT NULL,
    description VARCHAR(255) NULL,
    PRIMARY KEY (payment_status_id),
    CONSTRAINT uq_payment_statuses_status_name UNIQUE (status_name)
) ENGINE = InnoDB;

-- 12. Payments: supports partial and multiple payments for one booking.
CREATE TABLE IF NOT EXISTS payments (
    payment_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    booking_id BIGINT UNSIGNED NOT NULL,
    payment_method_id TINYINT UNSIGNED NOT NULL,
    payment_status_id TINYINT UNSIGNED NOT NULL,
    recorded_by_user_id BIGINT UNSIGNED NULL,
    amount DECIMAL(12, 2) NOT NULL,
    transaction_reference VARCHAR(100) NULL,
    paid_at DATETIME NULL,
    notes VARCHAR(500) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (payment_id),
    CONSTRAINT uq_payments_transaction_reference UNIQUE (transaction_reference),
    KEY ix_payments_booking_id (booking_id),
    KEY ix_payments_method_id (payment_method_id),
    KEY ix_payments_status_id (payment_status_id),
    KEY ix_payments_recorded_by_user_id (recorded_by_user_id),
    CONSTRAINT fk_payments_booking
        FOREIGN KEY (booking_id) REFERENCES bookings (booking_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_payments_method
        FOREIGN KEY (payment_method_id) REFERENCES payment_methods (payment_method_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_payments_status
        FOREIGN KEY (payment_status_id) REFERENCES payment_statuses (payment_status_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_payments_recorded_by
        FOREIGN KEY (recorded_by_user_id) REFERENCES users (user_id)
        ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT ck_payments_amount CHECK (amount > 0)
) ENGINE = InnoDB;

-- 13. Booking status history: complete audit trail for laundry tracking.
CREATE TABLE IF NOT EXISTS booking_status_history (
    booking_status_history_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    booking_id BIGINT UNSIGNED NOT NULL,
    booking_status_id TINYINT UNSIGNED NOT NULL,
    changed_by_user_id BIGINT UNSIGNED NULL,
    changed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    notes VARCHAR(500) NULL,
    PRIMARY KEY (booking_status_history_id),
    KEY ix_booking_status_history_booking_id (booking_id, changed_at),
    KEY ix_booking_status_history_status_id (booking_status_id),
    KEY ix_booking_status_history_changed_by_user_id (changed_by_user_id),
    CONSTRAINT fk_booking_status_history_booking
        FOREIGN KEY (booking_id) REFERENCES bookings (booking_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_booking_status_history_status
        FOREIGN KEY (booking_status_id) REFERENCES booking_statuses (booking_status_id)
        ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_booking_status_history_changed_by
        FOREIGN KEY (changed_by_user_id) REFERENCES users (user_id)
        ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE = InnoDB;

-- 14. Notifications: customer/account/booking messages.
CREATE TABLE IF NOT EXISTS notifications (
    notification_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id BIGINT UNSIGNED NOT NULL,
    booking_id BIGINT UNSIGNED NULL,
    notification_type VARCHAR(30) NOT NULL DEFAULT 'System',
    title VARCHAR(150) NOT NULL,
    message VARCHAR(1000) NOT NULL,
    is_read BOOLEAN NOT NULL DEFAULT FALSE,
    sent_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    read_at DATETIME NULL,
    PRIMARY KEY (notification_id),
    KEY ix_notifications_user_read (user_id, is_read, sent_at),
    KEY ix_notifications_booking_id (booking_id),
    CONSTRAINT fk_notifications_user
        FOREIGN KEY (user_id) REFERENCES users (user_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_notifications_booking
        FOREIGN KEY (booking_id) REFERENCES bookings (booking_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT ck_notifications_type CHECK (
        notification_type IN ('System', 'Account', 'Booking', 'Payment', 'Promotion')
    ),
    CONSTRAINT ck_notifications_read_at CHECK (
        is_read = FALSE OR read_at IS NOT NULL
    )
) ENGINE = InnoDB;

-- 15. Reward transactions: balance is derived by SUM(points), not duplicated.
CREATE TABLE IF NOT EXISTS reward_transactions (
    reward_transaction_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    user_id BIGINT UNSIGNED NOT NULL,
    booking_id BIGINT UNSIGNED NULL,
    transaction_type VARCHAR(20) NOT NULL,
    points INT NOT NULL,
    description VARCHAR(255) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (reward_transaction_id),
    KEY ix_reward_transactions_user_id (user_id, created_at),
    KEY ix_reward_transactions_booking_id (booking_id),
    CONSTRAINT fk_reward_transactions_user
        FOREIGN KEY (user_id) REFERENCES users (user_id)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT fk_reward_transactions_booking
        FOREIGN KEY (booking_id) REFERENCES bookings (booking_id)
        ON UPDATE CASCADE ON DELETE SET NULL,
    CONSTRAINT ck_reward_transactions_type CHECK (
        transaction_type IN ('Earn', 'Redeem', 'Adjustment', 'Expire')
    ),
    CONSTRAINT ck_reward_transactions_points CHECK (points <> 0),
    CONSTRAINT ck_reward_transactions_sign CHECK (
        (transaction_type = 'Earn' AND points > 0)
        OR (transaction_type IN ('Redeem', 'Expire') AND points < 0)
        OR transaction_type = 'Adjustment'
    )
) ENGINE = InnoDB;

-- Stable reference data. Explicit identifiers simplify application mapping.
INSERT INTO roles (role_id, role_name, description) VALUES
    (1, 'Owner', 'Laundry shop owner and system administrator'),
    (2, 'Staff', 'Laundry operations staff member'),
    (3, 'Customer', 'Registered or walk-in laundry customer')
ON DUPLICATE KEY UPDATE
    role_name = VALUES(role_name),
    description = VALUES(description);

INSERT INTO user_statuses (user_status_id, status_name, description) VALUES
    (1, 'Pending', 'Account is waiting for approval'),
    (2, 'Active', 'Account may access the system'),
    (3, 'Inactive', 'Account has been disabled'),
    (4, 'Suspended', 'Account access is temporarily blocked')
ON DUPLICATE KEY UPDATE
    status_name = VALUES(status_name),
    description = VALUES(description);

INSERT INTO item_types (item_type_id, item_name, description, is_active) VALUES
    (1, 'Clothes', 'Regular garments and everyday clothing', TRUE),
    (2, 'Blanket', 'Blankets and similar bedding', TRUE),
    (3, 'Comforter', 'Thick comforters and duvets', TRUE),
    (4, 'Curtain', 'Curtains and drapes', TRUE),
    (5, 'Shoes', 'Washable footwear', TRUE)
ON DUPLICATE KEY UPDATE
    item_name = VALUES(item_name),
    description = VALUES(description),
    is_active = VALUES(is_active);

INSERT INTO services (
    service_id,
    service_name,
    service_category,
    charging_basis,
    current_price,
    estimated_hours,
    description,
    is_active
) VALUES
    (1, 'Washing', 'Laundry', 'PerKg', 25.00, 2, 'Machine washing service', TRUE),
    (2, 'Drying', 'Laundry', 'PerKg', 25.00, 2, 'Machine drying service', TRUE),
    (3, 'Folding', 'Laundry', 'PerKg', 10.00, 1, 'Folding service', TRUE),
    (4, 'Powder', 'Consumable', 'PerItem', 15.00, NULL, 'Laundry powder soap', TRUE),
    (5, 'Downy', 'Consumable', 'PerItem', 15.00, NULL, 'Fabric conditioner', TRUE),
    (6, 'Rush Fee', 'Fee', 'Flat', 75.00, NULL, 'Priority processing fee', TRUE),
    -- Retired: the shop does not offer delivery. Kept inactive so older bookings that used it still load.
    (7, 'Delivery Fee', 'Fee', 'Flat', 50.00, NULL, 'Retired: the shop does not offer delivery', FALSE),
    (8, 'Self Service', 'Laundry', 'PerKg', 0.00, NULL, 'Customer-operated laundry service', TRUE)
ON DUPLICATE KEY UPDATE
    service_name = VALUES(service_name),
    service_category = VALUES(service_category),
    charging_basis = VALUES(charging_basis),
    current_price = VALUES(current_price),
    estimated_hours = VALUES(estimated_hours),
    description = VALUES(description),
    is_active = VALUES(is_active);

INSERT INTO booking_statuses (
    booking_status_id,
    status_name,
    display_order,
    is_terminal,
    description
) VALUES
    (1, 'Pending', 1, FALSE, 'Booking is awaiting staff acceptance'),
    (2, 'Washing', 2, FALSE, 'Laundry is being washed'),
    (3, 'Drying', 3, FALSE, 'Laundry is being dried'),
    (4, 'Folding', 4, FALSE, 'Laundry is being folded'),
    (5, 'Ready', 5, FALSE, 'Laundry is ready for pickup or delivery'),
    (6, 'Completed', 6, TRUE, 'Booking has been completed'),
    (7, 'Cancelled', 7, TRUE, 'Booking has been cancelled')
ON DUPLICATE KEY UPDATE
    status_name = VALUES(status_name),
    display_order = VALUES(display_order),
    is_terminal = VALUES(is_terminal),
    description = VALUES(description);

INSERT INTO payment_methods (payment_method_id, method_name, is_active) VALUES
    (1, 'Cash', TRUE),
    (2, 'GCash', TRUE),
    (3, 'Maya', TRUE),
    (4, 'Bank Transfer', TRUE)
ON DUPLICATE KEY UPDATE
    method_name = VALUES(method_name),
    is_active = VALUES(is_active);

INSERT INTO payment_statuses (payment_status_id, status_name, description) VALUES
    (1, 'Pending', 'Payment was initiated but has not been confirmed'),
    (2, 'Partial', 'Booking has an outstanding balance'),
    (3, 'Paid', 'Payment has been completed'),
    (4, 'Failed', 'Payment attempt failed'),
    (5, 'Refunded', 'Payment was returned to the customer')
ON DUPLICATE KEY UPDATE
    status_name = VALUES(status_name),
    description = VALUES(description);

-- Reporting views contain derived values without duplicating them in tables.
CREATE OR REPLACE VIEW v_booking_totals AS
SELECT
    b.booking_id,
    b.booking_code,
    COALESCE(SUM(bs.quantity * bs.unit_price), 0.00) AS order_total
FROM bookings AS b
LEFT JOIN booking_services AS bs ON bs.booking_id = b.booking_id
GROUP BY b.booking_id, b.booking_code;

CREATE OR REPLACE VIEW v_booking_payment_balances AS
SELECT
    bt.booking_id,
    bt.booking_code,
    bt.order_total,
    COALESCE(SUM(
        CASE WHEN ps.status_name IN ('Partial', 'Paid') THEN p.amount ELSE 0.00 END
    ), 0.00) AS amount_paid,
    bt.order_total - COALESCE(SUM(
        CASE WHEN ps.status_name IN ('Partial', 'Paid') THEN p.amount ELSE 0.00 END
    ), 0.00) AS balance_due
FROM v_booking_totals AS bt
LEFT JOIN payments AS p ON p.booking_id = bt.booking_id
LEFT JOIN payment_statuses AS ps ON ps.payment_status_id = p.payment_status_id
GROUP BY bt.booking_id, bt.booking_code, bt.order_total;

CREATE OR REPLACE VIEW v_user_reward_balances AS
SELECT
    u.user_id,
    COALESCE(SUM(rt.points), 0) AS reward_points
FROM users AS u
LEFT JOIN reward_transactions AS rt ON rt.user_id = u.user_id
GROUP BY u.user_id;

-- Piece-based inventory: Powder and Downy only.
-- Run once (or safely re-run) against the existing laundry_final database.
-- Additive only: no existing tables, prices, bookings, payments, or stock are reset.
-- Starting quantities are zero until staff/owner records a physical count or restock.
-- Historical orders are not inferred as stock movements.
CREATE TABLE IF NOT EXISTS inventory_stock (
    service_id SMALLINT UNSIGNED NOT NULL,
    quantity INT UNSIGNED NOT NULL DEFAULT 0,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (service_id),
    CONSTRAINT fk_inventory_stock_service FOREIGN KEY (service_id)
        REFERENCES services (service_id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT ck_inventory_stock_quantity CHECK (quantity <= 2147483647)
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS inventory_booking_issues (
    booking_id BIGINT UNSIGNED NOT NULL,
    issued_by_user_id BIGINT UNSIGNED NOT NULL,
    issued_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (booking_id),
    CONSTRAINT fk_inventory_issue_booking FOREIGN KEY (booking_id)
        REFERENCES bookings (booking_id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_issue_user FOREIGN KEY (issued_by_user_id)
        REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS inventory_movements (
    movement_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    service_id SMALLINT UNSIGNED NOT NULL,
    quantity_change INT NOT NULL,
    balance_after INT UNSIGNED NOT NULL,
    movement_kind VARCHAR(12) NOT NULL,
    booking_id BIGINT UNSIGNED NULL,
    recorded_by_user_id BIGINT UNSIGNED NOT NULL,
    reason VARCHAR(255) NOT NULL,
    request_key VARCHAR(100) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    requested_quantity INT UNSIGNED NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (movement_id),
    CONSTRAINT uq_inventory_movement_request UNIQUE (request_key),
    KEY ix_inventory_movement_service (service_id, movement_id),
    KEY ix_inventory_movement_booking (booking_id, service_id),
    CONSTRAINT fk_inventory_movement_service FOREIGN KEY (service_id)
        REFERENCES services (service_id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_movement_booking FOREIGN KEY (booking_id)
        REFERENCES bookings (booking_id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT fk_inventory_movement_user FOREIGN KEY (recorded_by_user_id)
        REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT ck_inventory_movement_kind CHECK (movement_kind IN ('Restock', 'Count', 'Issue', 'Return')),
    CONSTRAINT ck_inventory_movement_sign CHECK (
        (movement_kind IN ('Restock', 'Return') AND quantity_change > 0)
        OR (movement_kind = 'Issue' AND quantity_change < 0)
        OR movement_kind = 'Count'),
    CONSTRAINT ck_inventory_movement_booking CHECK (
        (movement_kind IN ('Issue', 'Return') AND booking_id IS NOT NULL)
        OR (movement_kind IN ('Restock', 'Count') AND booking_id IS NULL)),
    CONSTRAINT ck_inventory_movement_balance CHECK (balance_after <= 2147483647)
) ENGINE = InnoDB;

INSERT INTO inventory_stock (service_id, quantity)
SELECT service_id, 0 FROM services
WHERE service_name IN ('Powder', 'Downy') AND charging_basis = 'PerItem'
ON DUPLICATE KEY UPDATE service_id = VALUES(service_id);

-- Basic rewards: additive tables and a corrected derived totals view only.
-- No opening points or historical earnings are inserted. Existing ledger rows
-- are preserved. reward_accounts is only a mutex, never a cached balance.
CREATE TABLE IF NOT EXISTS reward_accounts (
    user_id BIGINT UNSIGNED NOT NULL,
    PRIMARY KEY (user_id),
    CONSTRAINT fk_reward_accounts_user FOREIGN KEY (user_id)
        REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT
) ENGINE = InnoDB;

CREATE TABLE IF NOT EXISTS booking_rewards (
    booking_id BIGINT UNSIGNED NOT NULL,
    redeemed_points INT UNSIGNED NOT NULL DEFAULT 0,
    discount_amount DECIMAL(10, 2) NOT NULL DEFAULT 0.00,
    redeemed_at DATETIME NULL,
    points_awarded INT UNSIGNED NOT NULL DEFAULT 0,
    earned_at DATETIME NULL,
    paid_amount_at_earning DECIMAL(14, 2) NULL,
    returned_at DATETIME NULL,
    PRIMARY KEY (booking_id),
    CONSTRAINT fk_booking_rewards_booking FOREIGN KEY (booking_id)
        REFERENCES bookings (booking_id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT ck_booking_rewards_discount CHECK (
        (redeemed_points = 0 AND discount_amount = 0 AND redeemed_at IS NULL)
        OR (redeemed_points = 100 AND discount_amount = 50 AND redeemed_at IS NOT NULL)),
    CONSTRAINT ck_booking_rewards_award CHECK (
        points_awarded <= 2147483647 AND
        ((earned_at IS NULL AND points_awarded = 0 AND paid_amount_at_earning IS NULL)
        OR (earned_at IS NOT NULL AND paid_amount_at_earning IS NOT NULL
            AND paid_amount_at_earning >= 0))),
    CONSTRAINT ck_booking_rewards_return CHECK (
        returned_at IS NULL OR redeemed_points = 100)
) ENGINE = InnoDB;

CREATE OR REPLACE VIEW v_booking_totals AS
SELECT b.booking_id, b.booking_code,
    ROUND(GREATEST(0.00, COALESCE(SUM(bs.quantity * bs.unit_price), 0.00)
        - COALESCE(br.discount_amount, 0.00)), 2) AS order_total
FROM bookings b
LEFT JOIN booking_services bs ON bs.booking_id = b.booking_id
LEFT JOIN booking_rewards br ON br.booking_id = b.booking_id AND br.returned_at IS NULL
GROUP BY b.booking_id, b.booking_code, br.discount_amount;

-- Self-Service (DIY) machine monitoring: staff set each machine's status and
-- customers see how many washers and dryers are free before going to the shop.
CREATE TABLE IF NOT EXISTS laundry_machines (
    machine_id SMALLINT UNSIGNED NOT NULL AUTO_INCREMENT,
    machine_name VARCHAR(30) NOT NULL,
    machine_type VARCHAR(10) NOT NULL,
    machine_status VARCHAR(15) NOT NULL DEFAULT 'Available',
    updated_by_user_id BIGINT UNSIGNED NULL,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (machine_id),
    UNIQUE KEY uq_laundry_machines_name (machine_name),
    CONSTRAINT ck_laundry_machines_type CHECK (machine_type IN ('Washer', 'Dryer')),
    CONSTRAINT ck_laundry_machines_status CHECK (machine_status IN ('Available', 'In Use', 'Maintenance')),
    CONSTRAINT fk_laundry_machines_updated_by FOREIGN KEY (updated_by_user_id)
        REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE SET NULL
) ENGINE = InnoDB;

INSERT IGNORE INTO laundry_machines (machine_name, machine_type) VALUES
    ('Washer 1', 'Washer'), ('Washer 2', 'Washer'), ('Washer 3', 'Washer'), ('Washer 4', 'Washer'),
    ('Dryer 1', 'Dryer'), ('Dryer 2', 'Dryer'), ('Dryer 3', 'Dryer'), ('Dryer 4', 'Dryer');

-- My Shift: staff record Time In / Time Out so the owner can see who is on duty and
-- track staff performance (Significance of the Study: Staff Management).
CREATE TABLE IF NOT EXISTS staff_shifts (
    shift_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
    staff_id BIGINT UNSIGNED NOT NULL,
    time_in DATETIME NOT NULL,
    time_out DATETIME NULL,
    PRIMARY KEY (shift_id),
    KEY ix_staff_shifts_staff_time_in (staff_id, time_in),
    CONSTRAINT fk_staff_shifts_staff FOREIGN KEY (staff_id)
        REFERENCES users (user_id) ON UPDATE CASCADE ON DELETE RESTRICT,
    CONSTRAINT ck_staff_shifts_order CHECK (time_out IS NULL OR time_out >= time_in)
) ENGINE = InnoDB;
