(function () {
    'use strict';

    const script = document.currentScript;
    const preserveCurrentSection = script && script.dataset.preserveCurrentSection === 'true';
    const sidebar = document.querySelector('.dashboard-sidebar');
    if (!sidebar || typeof window.showSection !== 'function') return;

    const links = Array.from(sidebar.querySelectorAll('a[data-dashboard-section]'));
    const sectionLinks = links.filter(link => document.getElementById(link.dataset.dashboardSection));
    if (sectionLinks.length === 0) return;

    function syncCurrentLink() {
        links.forEach(link => {
            if (link.classList.contains('active')) {
                link.setAttribute('aria-current', 'page');
            } else {
                link.removeAttribute('aria-current');
            }
        });
    }

    function showLink(link) {
        window.showSection(link.dataset.dashboardSection, link);
        syncCurrentLink();
    }

    function showHashSection() {
        let sectionId = '';
        try {
            sectionId = decodeURIComponent(window.location.hash.slice(1));
        } catch (_) {
            // Malformed fragments fall back to Overview rather than breaking navigation.
        }
        const link = sectionLinks.find(item => item.dataset.dashboardSection === sectionId);
        showLink(link || sectionLinks[0]);
    }

    sectionLinks.forEach(link => {
        link.addEventListener('click', function (event) {
            // Keep browser behavior for new tabs/windows and modified clicks.
            if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey ||
                event.shiftKey || event.altKey || link.hasAttribute('download') ||
                (link.target && link.target !== '_self')) return;

            const sectionId = link.dataset.dashboardSection;
            if (!document.getElementById(sectionId)) return;

            event.preventDefault();
            showLink(link);
            const nextHash = '#' + encodeURIComponent(sectionId);
            if (window.location.hash !== nextHash) {
                // Preserve report filters in the query string without reloading the dashboard.
                window.history.pushState(null, '', window.location.pathname + window.location.search + nextHash);
            }
        });
    });

    window.addEventListener('hashchange', showHashSection);
    window.addEventListener('popstate', showHashSection);

    // Server-selected report results or walk-in validation errors take precedence on first load only.
    if (preserveCurrentSection) {
        syncCurrentLink();
    } else {
        showHashSection();
    }
})();
