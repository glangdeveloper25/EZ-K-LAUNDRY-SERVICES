USE laundry_final;

-- Product catalog (panel suggestion): the shop sells real detergent and fabric
-- conditioner brands instead of generic "Powder" and "Downy". Products stay in the
-- services catalogue (category Consumable, charged per piece) and get a product type.
-- Prices are samples until EZ-K confirms them; the owner can change them. Stock starts
-- at zero: the owner records the actual opening stock with Stock In. Safe to re-run.
ALTER TABLE services ADD COLUMN IF NOT EXISTS product_type VARCHAR(30) NULL AFTER service_category;

UPDATE services SET product_type = 'Detergent' WHERE service_name = 'Powder' AND product_type IS NULL;
UPDATE services SET product_type = 'Fabric Conditioner' WHERE service_name = 'Downy' AND product_type IS NULL;

INSERT IGNORE INTO services
    (service_name, service_category, product_type, charging_basis, current_price, description, is_active)
VALUES
    ('Ariel 58 g', 'Consumable', 'Detergent', 'PerItem', 15.00, 'Liquid detergent sachet', TRUE),
    ('Surf Ultra Power Sun Fresh 64 ml', 'Consumable', 'Detergent', 'PerItem', 15.00, 'Liquid detergent sachet', TRUE),
    ('Del Fragrance Pearls Lavender 33 ml', 'Consumable', 'Fabric Conditioner', 'PerItem', 12.00, 'Fabric conditioner sachet', TRUE),
    ('Del Fragrance Pearls Blossom Fresh 33 ml', 'Consumable', 'Fabric Conditioner', 'PerItem', 12.00, 'Fabric conditioner sachet', TRUE),
    ('Downy Premium Parfum Passion 26 ml', 'Consumable', 'Fabric Conditioner', 'PerItem', 12.00, 'Fabric conditioner sachet', TRUE),
    ('Downy Premium Parfum Mystique 26 ml', 'Consumable', 'Fabric Conditioner', 'PerItem', 12.00, 'Fabric conditioner sachet', TRUE),
    ('Downy Sunrise Fresh 26 ml', 'Consumable', 'Fabric Conditioner', 'PerItem', 12.00, 'Fabric conditioner sachet', TRUE),
    ('Surf Fabric Conditioner Blossom Fresh 28 ml', 'Consumable', 'Fabric Conditioner', 'PerItem', 10.00, 'Fabric conditioner sachet', TRUE);

-- The generic items are replaced by the real brands but kept for past bookings and stock history.
UPDATE services SET is_active = FALSE WHERE service_name IN ('Powder', 'Downy');

INSERT IGNORE INTO inventory_stock (service_id, quantity)
SELECT service_id, 0 FROM services WHERE service_category = 'Consumable' AND charging_basis = 'PerItem';
