// UOMapVibe — Item catalog search (fallback for manual item lookup)
(function () {
    'use strict';

    let searchTimeout = null;

    document.getElementById('catalog-search').addEventListener('input', function () {
        const query = this.value.trim();
        if (query.length < 2) {
            document.getElementById('catalog-results').innerHTML = '';
            return;
        }

        // Debounce search
        clearTimeout(searchTimeout);
        searchTimeout = setTimeout(() => searchCatalog(query), 300);
    });

    async function searchCatalog(query) {
        try {
            const response = await fetch(`/api/catalog/search?q=${encodeURIComponent(query)}`);
            if (!response.ok) return;

            const results = await response.json();
            const container = document.getElementById('catalog-results');

            container.innerHTML = results.map(item =>
                `<div class="catalog-item"><span class="id">0x${item.itemId.toString(16).toUpperCase().padStart(4, '0')}</span> "${item.name}" H=${item.height} ${item.flags}</div>`
            ).join('');

        } catch (err) {
            console.error('Catalog search error:', err);
        }
    }
})();
