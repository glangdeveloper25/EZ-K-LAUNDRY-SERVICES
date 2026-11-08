USE laundry_final;

-- Per-load pricing (panel suggestion): Washing, Drying and Folding are charged per
-- load instead of per kilogram. The actual weight is still recorded and decides the
-- number of loads. Safe to re-run: prices are only seeded while a service is still
-- per kilogram, so the owner's later price changes are never overwritten.
ALTER TABLE services DROP CONSTRAINT IF EXISTS ck_services_charging_basis;
ALTER TABLE services ADD CONSTRAINT ck_services_charging_basis
    CHECK (charging_basis IN ('PerKg', 'PerLoad', 'PerItem', 'Flat'));

CREATE TABLE IF NOT EXISTS app_settings (
    setting_key VARCHAR(50) NOT NULL,
    setting_value VARCHAR(100) NOT NULL,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (setting_key)
) ENGINE = InnoDB;

-- Sample load size until EZ-K confirms it; the owner can change it on the pricing page.
INSERT IGNORE INTO app_settings (setting_key, setting_value) VALUES ('kg_per_load', '7');

-- Sample per-load prices until EZ-K confirms them; the owner can change them.
UPDATE services SET charging_basis = 'PerLoad', current_price = 55.00
WHERE service_name IN ('Washing', 'Drying') AND charging_basis = 'PerKg';
UPDATE services SET charging_basis = 'PerLoad', current_price = 30.00
WHERE service_name = 'Folding' AND charging_basis = 'PerKg';
