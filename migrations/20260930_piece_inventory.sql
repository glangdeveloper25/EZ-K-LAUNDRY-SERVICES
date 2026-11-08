USE laundry_final;

-- Piece-based inventory: Powder and Downy only.
-- Run once (or safely re-run) against the existing laundry_final database.
-- Additive only: no existing tables, prices, bookings, payments, or stock are reset.
-- Starting quantities are zero until the owner records opening stock or a restock.
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
