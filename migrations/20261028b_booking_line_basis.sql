USE laundry_final;

-- Keep each booking line's charging basis, like its unit price, so bookings made while
-- laundry was priced per kilogram are still shown and recalculated per kilogram after
-- the switch to per-load pricing. NULL means "use the service's current basis".
-- Safe to re-run.
ALTER TABLE booking_services ADD COLUMN IF NOT EXISTS charging_basis VARCHAR(20) NULL AFTER unit_price;

-- Lines saved before the per-load switch were charged per kilogram.
UPDATE booking_services bs
JOIN services s ON s.service_id = bs.service_id
JOIN bookings b ON b.booking_id = bs.booking_id
SET bs.charging_basis = 'PerKg'
WHERE bs.charging_basis IS NULL
  AND s.service_name IN ('Washing', 'Drying', 'Folding')
  AND b.booked_at < '2026-10-28 16:51:52'; -- when per-load pricing was switched on
