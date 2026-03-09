// UOMapVibe — Leaflet.Draw annotation tools
(function () {
    'use strict';

    const map = UOMapVibe.map;
    const annotations = [];
    let nextId = 1;

    // Drawing layer
    const drawnItems = new L.FeatureGroup();
    map.addLayer(drawnItems);

    // Draw controls
    const drawControl = new L.Control.Draw({
        position: 'topleft',
        draw: {
            polygon: false,
            circlemarker: false,
            circle: { shapeOptions: { color: '#e94560', weight: 2 } },
            rectangle: { shapeOptions: { color: '#53a8b6', weight: 2 } },
            polyline: { shapeOptions: { color: '#f0a500', weight: 3 } },
            marker: true
        },
        edit: {
            featureGroup: drawnItems,
            remove: true
        }
    });
    map.addControl(drawControl);

    // Handle new drawings
    map.on(L.Draw.Event.CREATED, function (e) {
        const layer = e.layer;
        drawnItems.addLayer(layer);

        const annotation = {
            id: nextId++,
            type: e.layerType,
            layer: layer,
            label: '',
            bounds: null
        };

        // Extract bounds/position
        if (e.layerType === 'rectangle') {
            annotation.bounds = UOMapVibe.boundsToUO(layer.getBounds());
        } else if (e.layerType === 'circle') {
            const center = UOMapVibe.latLngToUO(layer.getLatLng());
            const radius = Math.floor(layer.getRadius());
            annotation.bounds = {
                x1: center.x - radius, y1: center.y - radius,
                x2: center.x + radius, y2: center.y + radius
            };
            annotation.center = center;
            annotation.radius = radius;
        } else if (e.layerType === 'polyline') {
            const points = layer.getLatLngs().map(ll => UOMapVibe.latLngToUO(ll));
            annotation.points = points;
            const xs = points.map(p => p.x);
            const ys = points.map(p => p.y);
            annotation.bounds = {
                x1: Math.min(...xs), y1: Math.min(...ys),
                x2: Math.max(...xs), y2: Math.max(...ys)
            };
        } else if (e.layerType === 'marker') {
            const pos = UOMapVibe.latLngToUO(layer.getLatLng());
            annotation.position = pos;
            annotation.bounds = { x1: pos.x - 1, y1: pos.y - 1, x2: pos.x + 1, y2: pos.y + 1 };
        }

        annotations.push(annotation);
        renderAnnotationList();
        updateButtons();

        // Bind popup for label editing
        layer.bindPopup(`<input type="text" class="popup-label" data-id="${annotation.id}" placeholder="Describe intent..." />`);
        layer.openPopup();
    });

    // Handle popup label input
    document.addEventListener('input', function (e) {
        if (e.target.classList.contains('popup-label')) {
            const id = parseInt(e.target.dataset.id);
            const ann = annotations.find(a => a.id === id);
            if (ann) ann.label = e.target.value;
            renderAnnotationList();
        }
    });

    // Handle drawn item deletion
    map.on(L.Draw.Event.DELETED, function (e) {
        e.layers.eachLayer(function (layer) {
            const idx = annotations.findIndex(a => a.layer === layer);
            if (idx >= 0) annotations.splice(idx, 1);
        });
        renderAnnotationList();
        updateButtons();
    });

    function renderAnnotationList() {
        const list = document.getElementById('annotation-list');
        list.innerHTML = annotations.map(a => `
            <div class="annotation-item">
                <span class="type">${a.type}</span>
                ${a.bounds ? ` (${a.bounds.x1},${a.bounds.y1})-(${a.bounds.x2},${a.bounds.y2})` : ''}
                <input type="text" value="${a.label}" placeholder="Describe intent..."
                    oninput="UOMapVibe.updateLabel(${a.id}, this.value)" />
            </div>
        `).join('');
    }

    function updateButtons() {
        const hasAnnotations = annotations.length > 0;
        document.getElementById('btn-prepare').disabled = !hasAnnotations;
        document.getElementById('btn-copy').disabled = !UOMapVibe.preparedData;
    }

    // Clear all annotations
    document.getElementById('btn-clear-annotations').addEventListener('click', function () {
        drawnItems.clearLayers();
        annotations.length = 0;
        renderAnnotationList();
        updateButtons();
        UOMapVibe.preparedData = null;
        document.getElementById('style-preview').innerHTML = '<p class="hint">Click "Prepare" to analyze nearby building styles.</p>';
    });

    // Public API
    UOMapVibe.annotations = annotations;
    UOMapVibe.drawnItems = drawnItems;
    UOMapVibe.updateLabel = function (id, value) {
        const ann = annotations.find(a => a.id === id);
        if (ann) ann.label = value;
    };
    UOMapVibe.updateButtons = updateButtons;
})();
