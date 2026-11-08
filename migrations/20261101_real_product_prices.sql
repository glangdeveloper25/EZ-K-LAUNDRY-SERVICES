USE laundry_final;

-- Actual EZ-K sachet prices (shop visit, November 1, 2026): Ariel is PHP 21.00 and every
-- other detergent and fabric conditioner is PHP 10.00. Safe to re-run.
UPDATE services SET current_price = 21.00
WHERE service_name = 'Ariel 58 g' AND service_category = 'Consumable';

UPDATE services SET current_price = 10.00
WHERE service_category = 'Consumable' AND is_active = TRUE AND service_name <> 'Ariel 58 g';
