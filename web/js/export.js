// UOMapVibe — "Copy for AI" button: copies enriched JSON to clipboard
(function () {
    'use strict';

    document.getElementById('btn-copy').addEventListener('click', async function () {
        const data = UOMapVibe.preparedData;
        if (!data) {
            UOMapVibe.setStatus('No prepared data. Click "Prepare" first.');
            return;
        }

        try {
            const json = JSON.stringify(data, null, 2);

            // Copy to clipboard
            await navigator.clipboard.writeText(json);

            // Also save to a file via download
            const blob = new Blob([json], { type: 'application/json' });
            const url = URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = `uomapvibe_payload_${Date.now()}.json`;
            a.click();
            URL.revokeObjectURL(url);

            UOMapVibe.setStatus('Copied to clipboard + downloaded JSON');
        } catch (err) {
            // Clipboard may fail in non-HTTPS contexts
            try {
                const textarea = document.createElement('textarea');
                textarea.value = JSON.stringify(data, null, 2);
                document.body.appendChild(textarea);
                textarea.select();
                document.execCommand('copy');
                document.body.removeChild(textarea);
                UOMapVibe.setStatus('Copied to clipboard (fallback)');
            } catch {
                UOMapVibe.setStatus('Failed to copy. Download only.');
            }
        }
    });
})();
