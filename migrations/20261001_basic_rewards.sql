USE laundry_final;

-- Basic rewards: additive tables and a corrected derived totals view only.
-- Safe to re-run. Never inserts opening points, awards historical orders,
-- resets existing ledger rows, or changes booking/payment/service records.
-- Run outside a business transaction because MySQL DDL commits implicitly.
-- reward_accounts is only a customer mutex; reward_transactions is the balance.
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

-- A returned/cancelled reward has no active discount. Preserve the existing
-- view columns so dependent payment-balance views keep using the net charge.
CREATE OR REPLACE VIEW v_booking_totals AS
SELECT b.booking_id, b.booking_code,
    ROUND(GREATEST(0.00, COALESCE(SUM(bs.quantity * bs.unit_price), 0.00)
        - COALESCE(br.discount_amount, 0.00)), 2) AS order_total
FROM bookings b
LEFT JOIN booking_services bs ON bs.booking_id = b.booking_id
LEFT JOIN booking_rewards br ON br.booking_id = b.booking_id AND br.returned_at IS NULL
GROUP BY b.booking_id, b.booking_code, br.discount_amount;
