// UOMapVibe — Execute commands and manage snapshots
(function () {
    'use strict';

    const map = UOMapVibe.map;
    const placedMarkers = new L.LayerGroup();
    map.addLayer(placedMarkers);

    // Run commands from textarea
    document.getElementById('btn-run-commands').addEventListener('click', async function () {
        const input = document.getElementById('command-input').value.trim();
        if (!input) return;

        try {
            const commands = JSON.parse(input);
            await executeCommands(commands);
        } catch (err) {
            UOMapVibe.setStatus(`Parse error: ${err.message}`);
        }
    });

    // Execute button (from prepared data — future use)
    document.getElementById('btn-execute').addEventListener('click', async function () {
        const input = document.getElementById('command-input').value.trim();
        if (!input) {
            UOMapVibe.setStatus('Paste commands in the text area first.');
            return;
        }
        try {
            const commands = JSON.parse(input);
            await executeCommands(commands);
        } catch (err) {
            UOMapVibe.setStatus(`Error: ${err.message}`);
        }
    });

    async function executeCommands(commands) {
        UOMapVibe.setStatus('Executing...');

        try {
            const response = await fetch('/api/execute', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ commands: commands })
            });

            if (!response.ok) throw new Error(`API error: ${response.status}`);
            const result = await response.json();

            UOMapVibe.setStatus(
                `Batch ${result.batchId}: placed=${result.placed}, deleted=${result.deleted}` +
                (result.errors?.length ? `, errors=${result.errors.length}` : '')
            );

            // Show placed items as markers on map
            placedMarkers.clearLayers();
            for (const cmd of commands) {
                if (cmd.op === 'place') {
                    const marker = L.circleMarker(
                        UOMapVibe.uoToLatLng(cmd.x, cmd.y),
                        { radius: 3, color: '#00ff88', fillOpacity: 0.8 }
                    );
                    marker.bindTooltip(`0x${cmd.itemId.toString(16).toUpperCase()} Z=${cmd.z}`);
                    placedMarkers.addLayer(marker);
                }
            }

            // Refresh snapshots
            refreshSnapshots();

            if (result.errors?.length) {
                console.warn('Execute errors:', result.errors);
            }

        } catch (err) {
            UOMapVibe.setStatus(`Execute error: ${err.message}`);
            console.error('Execute error:', err);
        }
    }

    // Snapshot management
    async function refreshSnapshots() {
        try {
            const response = await fetch('/api/snapshots');
            const snapshots = await response.json();

            const list = document.getElementById('snapshot-list');
            list.innerHTML = snapshots.slice(0, 20).map(s => `
                <div class="snapshot-item">
                    <span>${s}</span>
                    <button class="btn btn-secondary" onclick="UOMapVibe.rollback('${s}')">Rollback</button>
                </div>
            `).join('');
        } catch (err) {
            console.error('Snapshot refresh error:', err);
        }
    }

    UOMapVibe.rollback = async function (batchId) {
        if (!confirm(`Rollback to snapshot ${batchId}?`)) return;

        try {
            const response = await fetch(`/api/rollback/${batchId}`, { method: 'POST' });
            if (!response.ok) throw new Error(`API error: ${response.status}`);
            UOMapVibe.setStatus(`Rolled back: ${batchId}`);
            refreshSnapshots();
        } catch (err) {
            UOMapVibe.setStatus(`Rollback error: ${err.message}`);
        }
    };

    document.getElementById('btn-refresh-snapshots').addEventListener('click', refreshSnapshots);

    // Enable execute button
    document.getElementById('btn-execute').disabled = false;

    // Initial snapshot load
    refreshSnapshots();
})();
