USE laundry_final;

-- EZ-K price list (shop visit, November 2026), one load = up to 7.5 kg:
--   Self-Service: Wash PHP 55, Dry PHP 65 per load.
--   Drop-off (Full-Service): wash-dry-fold PHP 155 per load with the customer's own
--   detergent and fabric conditioner, PHP 165 with the shop's, and PHP 3 more per
--   load for Ariel. Fold has no price of its own on the list; PHP 35 makes
--   Wash + Dry + Fold equal the PHP 155 package. Safe to re-run.
UPDATE services SET current_price = 55.00 WHERE service_name = 'Washing';
UPDATE services SET current_price = 65.00 WHERE service_name = 'Drying';
UPDATE services SET current_price = 35.00 WHERE service_name = 'Folding';
UPDATE app_settings SET setting_value = '7.5' WHERE setting_key = 'kg_per_load';

-- Extra charged per load when a product is used in the Full-Service package (Ariel: PHP 3).
ALTER TABLE services ADD COLUMN IF NOT EXISTS package_extra DECIMAL(10, 2) NOT NULL DEFAULT 0.00 AFTER current_price;
UPDATE services SET package_extra = 3.00 WHERE service_name = 'Ariel 58 g';

INSERT IGNORE INTO services
    (service_name, service_category, charging_basis, current_price, description, is_active)
VALUES
    ('Detergent & Fabcon Package', 'Fee', 'PerLoad', 10.00,
     'Full-Service: the shop''s detergent and fabric conditioner, one each per load', TRUE),
    ('Premium Detergent', 'Fee', 'PerLoad', 0.00,
     'Full-Service: extra per load for a premium detergent such as Ariel; the amount comes from the product', TRUE);
