// UOMapVibe — "Prepare" button: calls /api/prepare for region + style analysis
(function () {
    'use strict';

    UOMapVibe.preparedData = null;

    document.getElementById('btn-prepare').addEventListener('click', async function () {
        const annotations = UOMapVibe.annotations;
        if (annotations.length === 0) return;

        UOMapVibe.setStatus('Preparing...');
        this.disabled = true;

        try {
            // Compute combined bounding box of all annotations
            let x1 = Infinity, y1 = Infinity, x2 = -Infinity, y2 = -Infinity;
            for (const ann of annotations) {
                if (ann.bounds) {
                    x1 = Math.min(x1, ann.bounds.x1);
                    y1 = Math.min(y1, ann.bounds.y1);
                    x2 = Math.max(x2, ann.bounds.x2);
                    y2 = Math.max(y2, ann.bounds.y2);
                }
            }

            // Call prepare endpoint (target + 40-tile context radius)
            const params = new URLSearchParams({
                targetX1: x1, targetY1: y1,
                targetX2: x2, targetY2: y2,
                contextRadius: 40
            });

            const response = await fetch(`/api/prepare?${params}`);
            if (!response.ok) throw new Error(`API error: ${response.status}`);

            const data = await response.json();

            // Store prepared data
            UOMapVibe.preparedData = {
                map_id: 0,
                annotations: annotations.map(a => ({
                    id: a.id,
                    type: a.type,
                    bounds: a.bounds,
                    label: a.label,
                    points: a.points || undefined,
                    position: a.position || undefined
                })),
                ...data
            };

            // Update style preview
            UOMapVibe.renderStylePreview(data.style_analysis);

            // Enable copy button
            document.getElementById('btn-copy').disabled = false;
            UOMapVibe.setStatus(`Prepared: ${data.target_region.statics.length} statics, ${data.target_region.terrain.length} terrain tiles`);

        } catch (err) {
            UOMapVibe.setStatus(`Error: ${err.message}`);
            console.error('Prepare error:', err);
        } finally {
            this.disabled = false;
        }
    });
})();
