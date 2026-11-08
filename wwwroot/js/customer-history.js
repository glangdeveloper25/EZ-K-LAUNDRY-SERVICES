(() => {
    'use strict';

    const table = document.getElementById('bookingHistoryTable');
    if (!table) return;

    const pageSize = 12;
    const tbody = table.tBodies[0];
    const search = document.getElementById('bookingHistorySearch');
    const summary = document.getElementById('bookingHistorySummary');
    const pagination = document.getElementById('bookingHistoryPagination');
    const previous = document.getElementById('bookingHistoryPrevious');
    const next = document.getElementById('bookingHistoryNext');
    const empty = document.getElementById('bookingHistoryEmpty');
    const headings = Array.from(table.querySelectorAll('th[data-history-sort]'));
    const collator = new Intl.Collator('en', { sensitivity: 'base', numeric: true });
    const normalize = value => value.toLocaleLowerCase('en').replace(/\s+/g, ' ').trim();
    const records = Array.from(tbody.querySelectorAll('[data-history-row]')).map((row, index) => ({
        row,
        index,
        reference: row.dataset.reference,
        date: row.dataset.date,
        service: row.dataset.service,
        payment: Number(row.dataset.payment),
        status: row.dataset.status,
        searchText: normalize(Array.from(row.cells).map(cell => cell.textContent).join(' ') + ' ' + row.dataset.date + ' ' + row.dataset.payment)
    }));

    let page = 1;
    let sortKey = 'date';
    let direction = 'descending';
    let matches = [];

    function renderPagination(totalPages) {
        const restoreFocus = pagination.contains(document.activeElement);
        pagination.replaceChildren();
        // Keep the control compact, even for a long history.
        const pages = new Set([1, totalPages]);
        for (let number = Math.max(1, page - 2); number <= Math.min(totalPages, page + 2); number++) pages.add(number);
        let lastPage = 0;
        Array.from(pages).sort((a, b) => a - b).forEach(number => {
            if (lastPage && number - lastPage > 1) {
                const ellipsis = document.createElement('span');
                ellipsis.className = 'booking-history-ellipsis';
                ellipsis.textContent = '…';
                ellipsis.setAttribute('aria-hidden', 'true');
                pagination.append(ellipsis);
            }
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'btn btn-outline-secondary btn-sm';
            button.dataset.historyPage = String(number);
            button.textContent = String(number);
            button.setAttribute('aria-label', `Page ${number}`);
            button.setAttribute('aria-controls', 'bookingHistoryTable');
            if (number === page) button.setAttribute('aria-current', 'page');
            button.addEventListener('click', () => { page = number; render(); });
            pagination.append(button);
            lastPage = number;
        });
        previous.disabled = page === 1;
        next.disabled = page >= totalPages;
        if (restoreFocus) pagination.querySelector('[aria-current="page"]').focus();
    }

    function updatePrintMetadata() {
        document.getElementById('bookingHistoryPrintDate').textContent = new Intl.DateTimeFormat('en-PH', {
            dateStyle: 'long', timeStyle: 'short'
        }).format(new Date());
        const label = headings.find(heading => heading.dataset.historySort === sortKey).querySelector('button').firstChild.textContent.trim();
        const scope = search.value.trim() ? `Search: ${search.value.trim()}` : 'All bookings';
        document.getElementById('bookingHistoryPrintScope').textContent = scope;
        document.getElementById('bookingHistoryPrintCount').textContent = String(matches.length);
        document.getElementById('bookingHistoryPrintSort').textContent = `Sorted by ${label} (${direction}).`;
    }

    function render() {
        const query = normalize(search.value);
        const ordered = records.slice().sort((a, b) => {
            let difference;
            if (sortKey === 'payment') difference = a.payment - b.payment;
            // The server supplies an invariant, fixed-width timestamp; no locale-dependent date parsing.
            else if (sortKey === 'date') difference = a.date < b.date ? -1 : a.date > b.date ? 1 : 0;
            else difference = collator.compare(a[sortKey], b[sortKey]);
            return difference ? difference * (direction === 'ascending' ? 1 : -1) : a.index - b.index;
        });
        matches = ordered.filter(record => record.searchText.includes(query));
        const totalPages = Math.max(1, Math.ceil(matches.length / pageSize));
        page = Math.max(1, Math.min(page, totalPages));
        const start = (page - 1) * pageSize;
        const visible = new Set(matches.slice(start, start + pageSize));
        const matching = new Set(matches);
        const fragment = document.createDocumentFragment();
        ordered.forEach(record => {
            record.row.classList.toggle('history-filtered-out', !matching.has(record));
            record.row.hidden = !visible.has(record);
            fragment.append(record.row);
        });
        fragment.append(empty);
        tbody.append(fragment);
        empty.hidden = matches.length > 0;
        empty.cells[0].textContent = records.length === 0
            ? 'No history yet. Book your first laundry!'
            : 'No bookings match your search. Try another keyword or clear the search.';
        const range = matches.length ? `${start + 1}–${Math.min(start + pageSize, matches.length)}` : '0';
        summary.textContent = `Showing ${range} of ${matches.length} ${query ? 'matching ' : ''}bookings${query ? ` (${records.length} total)` : ''}`;
        headings.forEach(heading => {
            const active = heading.dataset.historySort === sortKey;
            const button = heading.querySelector('button');
            const label = button.firstChild.textContent.trim();
            heading.setAttribute('aria-sort', active ? direction : 'none');
            button.querySelector('span').textContent = active ? (direction === 'ascending' ? '↑' : '↓') : '↕';
            button.setAttribute('aria-label', `${label}: sort ${active && direction === 'ascending' ? 'descending' : 'ascending'}`);
        });
        renderPagination(totalPages);
    }

    search.addEventListener('input', () => { page = 1; render(); });
    headings.forEach(heading => heading.querySelector('button').addEventListener('click', () => {
        const key = heading.dataset.historySort;
        direction = sortKey === key && direction === 'ascending' ? 'descending' : 'ascending';
        sortKey = key;
        page = 1;
        render();
    }));
    previous.addEventListener('click', () => { page--; render(); });
    next.addEventListener('click', () => { page++; render(); });

    // Print CSS reveals all matching rows without modifying the screen's page or search state.
    window.printBookingHistory = () => { updatePrintMetadata(); window.print(); };
    document.getElementById('bookingHistoryPrintButton')?.addEventListener('click', window.printBookingHistory);
    window.addEventListener('beforeprint', updatePrintMetadata);
    render();
})();
