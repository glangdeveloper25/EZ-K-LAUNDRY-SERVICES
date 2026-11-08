// Persistent audio alert for new customer bookings on the staff dashboard.
// Polls the server for pending bookings and keeps beeping until a staff member
// acknowledges the newest one, so orders are noticed even when no one is watching the screen.
(function () {
    'use strict';

    const script = document.currentScript;
    const statusUrl = script.dataset.statusUrl;
    const pollIntervalMs = 10000;
    const beepIntervalMs = 2000;
    const ackKey = 'ezk.staff.acknowledgedBookingId';
    const enabledKey = 'ezk.staff.audioAlertEnabled';

    const toggle = document.getElementById('audioAlertToggle');
    const banner = document.getElementById('newBookingAlert');
    const bannerCount = document.getElementById('newBookingAlertCount');
    const unlockHint = document.getElementById('audioUnlockHint');

    const originalTitle = document.title;
    let latestId = Number(script.dataset.latestId || 0);
    let pendingCount = 0;
    let beepTimer = null;
    let audioContext = null;

    function readStorage(key) {
        try { return window.localStorage.getItem(key); } catch (_) { return null; }
    }

    function writeStorage(key, value) {
        try { window.localStorage.setItem(key, value); } catch (_) { /* storage blocked: keep working in memory */ }
    }

    // First visit: treat bookings already on screen as seen, so the alarm only rings for new ones.
    let acknowledgedId = readStorage(ackKey) === null ? latestId : Number(readStorage(ackKey));
    writeStorage(ackKey, String(acknowledgedId));
    toggle.checked = readStorage(enabledKey) !== 'false';

    function getAudioContext() {
        if (!audioContext) {
            const AudioCtor = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtor) return null;
            audioContext = new AudioCtor();
        }
        return audioContext;
    }

    // Browsers block sound until the user interacts with the page once.
    function unlockAudio() {
        const context = getAudioContext();
        if (context && context.state === 'suspended') context.resume();
        unlockHint.hidden = true;
    }

    // Two-tone "ding-dong" generated in the browser, so no sound file is needed.
    function playChime() {
        const context = getAudioContext();
        if (!context) return;
        if (context.state === 'suspended') {
            unlockHint.hidden = false;
            return;
        }
        // Triangle wave and longer notes carry better on small laptop speakers in a noisy shop.
        [[988, 0], [784, 0.4]].forEach(([frequency, offset]) => {
            const oscillator = context.createOscillator();
            const gain = context.createGain();
            const start = context.currentTime + offset;
            oscillator.type = 'triangle';
            oscillator.frequency.value = frequency;
            gain.gain.setValueAtTime(0.0001, start);
            gain.gain.exponentialRampToValueAtTime(0.9, start + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, start + 0.38);
            oscillator.connect(gain).connect(context.destination);
            oscillator.start(start);
            oscillator.stop(start + 0.4);
        });
    }

    function hasUnacknowledgedBooking() {
        return pendingCount > 0 && latestId > acknowledgedId;
    }

    function render() {
        const alerting = hasUnacknowledgedBooking();
        banner.hidden = !alerting;
        bannerCount.textContent = alerting ? `(${pendingCount} pending)` : '';
        document.title = alerting ? `(!) New Booking - ${originalTitle}` : originalTitle;

        if (alerting && toggle.checked) {
            if (!beepTimer) {
                playChime();
                beepTimer = window.setInterval(playChime, beepIntervalMs);
            }
        } else if (beepTimer) {
            window.clearInterval(beepTimer);
            beepTimer = null;
        }
    }

    function acknowledge() {
        acknowledgedId = latestId;
        writeStorage(ackKey, String(acknowledgedId));
        render();
    }

    async function poll() {
        try {
            const response = await fetch(statusUrl, { headers: { 'Accept': 'application/json' }, cache: 'no-store' });
            if (!response.ok) return;
            const data = await response.json();
            pendingCount = data.pendingCount;
            latestId = data.latestId;
            render();
        } catch (_) {
            // Network hiccup (e.g. brownout); try again on the next poll.
        }
    }

    toggle.addEventListener('change', function () {
        writeStorage(enabledKey, String(toggle.checked));
        if (toggle.checked) unlockAudio();
        render();
    });

    document.getElementById('newBookingAlertAck').addEventListener('click', acknowledge);
    document.getElementById('newBookingAlertView').addEventListener('click', function () {
        acknowledge();
        window.location.href = window.location.pathname;
    });

    ['click', 'keydown', 'touchstart'].forEach(eventName =>
        document.addEventListener(eventName, unlockAudio, { once: true }));

    poll();
    window.setInterval(poll, pollIntervalMs);
})();
