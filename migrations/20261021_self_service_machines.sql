USE laundry_final;

-- Self-Service (DIY) machine monitoring: staff set each machine's status and
-- customers see how many washers and dryers are free before going to the shop.
-- Additive and safe to re-run. The four stacked washer/dryer units match the
-- shop documented in Appendix C; the owner can rename or add rows later.
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
