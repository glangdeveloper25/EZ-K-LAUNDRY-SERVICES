(function () {
    'use strict';

    const button = document.getElementById('printInventoryReportButton');
    const modalElement = document.getElementById('inventoryReportDateModal');
    const form = document.getElementById('inventoryReportDateForm');
    if (!button || !modalElement || !form) return;

    const fromDate = document.getElementById('inventoryReportFromDate');
    const toDate = document.getElementById('inventoryReportToDate');
    const submit = document.getElementById('inventoryReportDateSubmit');
    const error = document.getElementById('inventoryReportDateError');
    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    let activeRequest = null;

    function setBusy(busy) {
        form.setAttribute('aria-busy', String(busy));
        fromDate.disabled = busy;
        toDate.disabled = busy;
        submit.disabled = busy;
        submit.textContent = busy ? 'Preparing report...' : 'Print Report';
    }

    function showError(message) {
        error.textContent = message;
        error.hidden = !message;
    }

    function validateDates() {
        toDate.setCustomValidity('');
        if (fromDate.value && toDate.value && toDate.value < fromDate.value) {
            toDate.setCustomValidity('To Date must be on or after From Date.');
        }
    }

    [fromDate, toDate].forEach(input => input.addEventListener('input', function () {
        showError('');
        validateDates();
    }));

    function updatePrintDate() {
        const printDate = document.getElementById('inventoryReportPrintDate');
        if (printDate) printDate.textContent = new Intl.DateTimeFormat('en-PH', {
            dateStyle: 'long',
            timeStyle: 'short'
        }).format(new Date());
    }

    // Also refresh the timestamp when the browser's Print / Ctrl+P is used.
    window.addEventListener('beforeprint', updatePrintDate);

    modalElement.addEventListener('show.bs.modal', function () {
        showError('');
        validateDates();
    });
    modalElement.addEventListener('shown.bs.modal', function () { fromDate.focus(); });
    modalElement.addEventListener('hide.bs.modal', function () {
        // Cancel immediately, not after the close animation, so a late response cannot print.
        if (activeRequest && !activeRequest.printing) {
            activeRequest.controller.abort();
            activeRequest = null;
            setBusy(false);
        }
    });

    form.addEventListener('submit', async function (event) {
        event.preventDefault();
        if (activeRequest) return;
        validateDates();
        if (!form.reportValidity()) return;

        const from = fromDate.value;
        const to = toDate.value;
        const request = { controller: new AbortController(), printing: false };
        activeRequest = request;
        showError('');
        setBusy(true);

        try {
            const url = new URL(form.action, window.location.href);
            url.searchParams.set('fromDate', from);
            url.searchParams.set('toDate', to);
            const response = await fetch(url, {
                credentials: 'same-origin',
                cache: 'no-store',
                headers: { Accept: 'text/html' },
                signal: request.controller.signal
            });
            if (activeRequest !== request) return;
            if (response.status === 401 || response.status === 403 || response.redirected) {
                throw new Error('Your session may have expired. Please sign in as Owner or Staff and try again.');
            }
            if (!response.ok) {
                const message = response.status === 400 || response.status === 409
                    ? await response.text()
                    : 'The inventory report is temporarily unavailable. Please try again.';
                throw new Error(message);
            }
            if (!response.headers.get('content-type')?.includes('text/html')) {
                throw new Error('The report could not be loaded. Please try again.');
            }

            const html = await response.text();
            if (activeRequest !== request) return;
            const documentPreview = new DOMParser().parseFromString(html, 'text/html');
            const report = documentPreview.getElementById('inventoryReportDocument');
            if (!report || report.dataset.fromDate !== from || report.dataset.toDate !== to) {
                throw new Error('The report dates could not be verified. Please try again.');
            }
            document.getElementById('inventoryReportDocument').replaceWith(report);
            // Wait for the local logo to render before opening the print preview.
            await Promise.all(Array.from(report.querySelectorAll('img')).map(img => img.decode().catch(() => {})));
            if (activeRequest !== request) return;
            request.printing = true;
            await new Promise(resolve => {
                modalElement.addEventListener('hidden.bs.modal', resolve, { once: true });
                modal.hide();
            });
            if (activeRequest !== request) return;
            updatePrintDate();
            window.print();
        } catch (failure) {
            if (activeRequest === request && failure.name !== 'AbortError') {
                if (!modalElement.classList.contains('show')) modal.show();
                showError(failure instanceof TypeError
                    ? 'Could not load the report. Check your connection and try again.'
                    : failure.message || 'Could not print the report. Please try again.');
            }
        } finally {
            if (activeRequest === request) {
                activeRequest = null;
                setBusy(false);
            }
        }
    });
})();
