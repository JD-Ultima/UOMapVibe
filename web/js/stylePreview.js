// UOMapVibe — Style preview panel: shows auto-detected materials
(function () {
    'use strict';

    UOMapVibe.renderStylePreview = function (style) {
        const box = document.getElementById('style-preview');
        if (!style || !style.palette) {
            box.innerHTML = '<p class="hint">No style data available.</p>';
            return;
        }

        const p = style.palette;
        const m = style.metrics;
        let html = '';

        // Walls — now lists per orientation
        if (p.walls) {
            html += '<div class="category"><span class="category-title">Walls</span>';

            const wallGroups = [
                ['S/N walls (E-W)', p.walls.southFacing],
                ['E/W walls (N-S)', p.walls.eastFacing],
                ['Corners', p.walls.cornerPieces],
                ['Other', p.walls.other]
            ];

            for (const [label, items] of wallGroups) {
                if (items && items.length > 0) {
                    for (const item of items.slice(0, 3)) {
                        html += renderOriented(label, item);
                    }
                }
            }
            html += '</div>';
        }

        // Simple categories
        const categories = [
            ['Floors', p.floors],
            ['Roofs', p.roofs],
            ['Doors', p.doors],
            ['Windows', p.windows],
            ['Stairs', p.stairs],
            ['Lights', p.lightSources],
            ['Decorations', p.decorations],
            ['Roads', p.roadSurface]
        ];

        for (const [name, items] of categories) {
            if (items && items.length > 0) {
                html += `<div class="category"><span class="category-title">${name}</span>`;
                for (const item of items.slice(0, 5)) {
                    const cls = item.isPrimary || item.primary ? 'material primary' : 'material';
                    const id = item.itemId ?? item.itemID ?? '';
                    html += `<div class="${cls}">0x${id.toString(16).toUpperCase().padStart(4, '0')} "${item.name}" ×${item.count}</div>`;
                }
                html += '</div>';
            }
        }

        // Building metrics
        if (m) {
            html += '<div class="category"><span class="category-title">Building Metrics</span>';
            html += `<div class="material">Avg footprint: ${m.avgFootprintWidth?.toFixed(0) ?? '?'}×${m.avgFootprintDepth?.toFixed(0) ?? '?'}</div>`;
            html += `<div class="material">Wall height: ${m.wallHeight ?? '?'}</div>`;
            html += `<div class="material">Floor Z offset: ${m.floorZRelativeToTerrain ?? '?'}</div>`;
            html += `<div class="material">Roof Z offset: ${m.roofZOffset ?? '?'}</div>`;
            html += `<div class="material">Buildings found: ${m.buildingCount ?? '?'}</div>`;
            html += `<div class="material">Multi-story: ${m.hasMultiStory ? 'Yes' : 'No'}</div>`;
            html += '</div>';
        }

        box.innerHTML = html || '<p class="hint">No materials detected in area.</p>';
    };

    function renderOriented(direction, item) {
        const id = item.itemId ?? item.itemID ?? 0;
        return `<div class="material">  ${direction}: 0x${id.toString(16).toUpperCase().padStart(4, '0')} "${item.name}" ×${item.count}</div>`;
    }
})();
