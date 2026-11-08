USE laundry_final;

-- My Shift: staff record Time In / Time Out so the owner can see who is on duty and
-- track staff performance (Significance of the Study: Staff Management). Safe to re-run.
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
