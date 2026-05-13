# UOMapVibe

**AI-assisted map editing for Ultima Online.** Draw what you want on a web map, and an AI builds it using the same materials as the buildings already around your target area — no item-ID hunting, no CentrED# round-trips.

The AI never guesses item IDs. The system reads your MUL files, classifies every nearby static by `TileFlag`, detects wall orientations from placement patterns, measures building footprints, and hands the AI an exact material palette to work with.

---

## What It Does

1. You open a web map of your UO world in a browser
2. You draw shapes where you want changes — rectangles, polylines, markers, circles
3. You type plain-English labels ("build a shop here", "extend the road east")
4. **Prepare**: the server analyzes a 40-tile radius around your annotation and auto-detects local materials, wall directions, footprint sizes, road styles
5. **Copy for AI**: enriched JSON (annotations + style palette + terrain Z + existing statics + AI instructions) is copied to your clipboard *and* saved as a download
6. You paste into Claude (or any AI); it returns a JSON command list using the detected materials
7. **Execute**: commands are written directly to your `statics0.mul` / `staidx0.mul` files with automatic snapshots for one-click rollback

---

## Features

### Map viewer
- Leaflet-based web app with full UO tile pyramid (zoom 0–5)
- Live mouse coordinates in UO world space (X/Y) shown in the status bar
- Default view drops you at Britain Bank (1438, 1690)
- Supports all six UO facets: Felucca, Trammel, Ilshenar, Malas, Tokuno, Ter Mur

### Annotation tools (Leaflet.Draw)
- **Rectangle** — area selection (best for buildings)
- **Polyline** — paths (best for roads or walls)
- **Marker** — single point (best for placing one item)
- **Circle** — circular area selection
- Each shape gets an inline text-label popup describing intent
- Edit or delete annotations via the Leaflet.Draw toolbar

### Automatic style inference
- **Wall classification**: items with `TileFlag.Wall` (or impassable + height > 0)
- **Wall orientation detection**: X-variance vs Y-variance of placements maps walls to N/S/E/W facings + corner pieces
- **Floor / Roof / Door / Window / Stairs / Light source / Foliage / Bridge / Decoration / Road** detection — driven by `TileFlag` values from `tiledata.mul`
- **Building metrics**: average footprint width/depth, wall height, floor Z offset, roof Z offset, multi-story detection
- **Road detection**: groups ground-level Surface items, identifies linear patterns
- AI instructions are embedded in the prepare payload — Claude gets a self-explanatory brief

### Map editing
- POST `/api/execute` with a list of `place` / `delete` commands
- Automatic snapshot of every affected 8×8 block before each batch
- Placed statics appear as green dot markers on the web map with hex-ID + Z tooltips
- One-click rollback to any prior snapshot

### Item catalog search
- Sidebar search box queries `tiledata.mul` by name
- Returns item ID (hex), name, height, flags — useful as a fallback when style inference misses something

### Tile exporter
- Standalone CLI that reads `map*.mul`, `statics*.mul`, `staidx*.mul`, `tiledata.mul`, `radarcol.mul`
- Builds a top-down radar image (highest-Z static or terrain wins per cell)
- Writes a Leaflet tile pyramid as 24-bit BMP files (`tiles/{z}/{x}/{y}.bmp`) — zero image-library dependencies
- Generates `web/data/tile_catalog.json` with every named item from `tiledata.mul`
- Falls back to flag-derived colors if `radarcol.mul` is absent

---

## Prerequisites

1. **Windows PC** (tested on Windows 11)
2. **.NET 8 SDK** — [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download). Verify with `dotnet --version`.
3. **UO MUL files** from your installation:
   - `map0.mul` (terrain)
   - `statics0.mul` (objects)
   - `staidx0.mul` (statics index)
   - `tiledata.mul` (item metadata)
   - `radarcol.mul` (optional — color palette; falls back to flag-based colors)
4. **A backup of your MUL files.** This tool writes directly to them.

---

## Setup (one-time)

### 1. Clone the repo

```
git clone https://github.com/JD-Ultima/UOMapVibe.git
cd UOMapVibe
```

### 2. Copy your MUL files into the project

```
UOMapVibe/
  Map Files/
    map0.mul
    statics0.mul
    staidx0.mul
    tiledata.mul
    radarcol.mul     (optional)
```

### 3. Configure the data path

Edit `src/UOMapVibe.Api/appsettings.json` and set absolute paths to your MUL directory and a snapshot directory:

```json
{
  "UOData": {
    "MulDirectory": "C:\\path\\to\\UOMapVibe\\Map Files",
    "SnapshotDirectory": "C:\\path\\to\\UOMapVibe\\snapshots",
    "DefaultMapId": 0
  }
}
```

Use double-backslashes (`\\`) in JSON paths on Windows. `DefaultMapId` selects the facet (0 = Felucca, 1 = Trammel, 2 = Ilshenar, 3 = Malas, 4 = Tokuno, 5 = Ter Mur).

### 4. Generate map tiles + catalog

The TileExporter is a CLI tool with required arguments:

```
dotnet run --project src/UOMapVibe.TileExporter -- --data "C:\path\to\Map Files" --out web --map 0 --maxzoom 5
```

Arguments:
- `--data` — path to your MUL directory (required)
- `--out`  — output directory; `web` writes tiles into `web/tiles/` and the catalog into `web/data/` (required)
- `--map`  — facet ID, default `0` (Felucca)
- `--maxzoom` — pyramid depth, default `5` (1 pixel per UO tile at max zoom)

Generates `web/tiles/{z}/{x}/{y}.bmp` and `web/data/tile_catalog.json`. Takes a few minutes for a full Felucca export.

### 5. Start the API server

```
dotnet run --project src/UOMapVibe.Api
```

Listens on `http://localhost:5000` and serves the web app from `web/` at the same origin. Open `http://localhost:5000` in your browser.

---

## How to Use

### Drawing annotations

1. Navigate the map to your target area (mouse coordinates display in the bottom-right)
2. Pick a shape from the Leaflet.Draw toolbar (top-left of the map)
3. Draw your shape
4. Type a plain-English label in the popup — e.g. "build a tavern here"
5. Repeat for multiple intents in one batch; everything is included in the next Prepare call

### Preparing data for the AI

1. Click **Prepare for AI** in the sidebar
2. The server computes the combined bounding box of all your annotations, then calls `/api/prepare` with a 40-tile context radius
3. The **Style Preview** panel updates with:
   - Walls grouped by detected orientation (S/N facing, E/W facing, corners, other)
   - Floors, roofs, doors, windows, stairs, lights, decorations, road materials — top items by frequency
   - Building metrics: avg footprint, wall height, floor/roof Z offsets, multi-story flag, building count
4. Review the palette. If it looks wrong, draw your annotation closer to the style you actually want to match.

### Sending to AI

1. Click **Copy for AI** — JSON is copied to your clipboard *and* downloaded as `uomapvibe_payload_<timestamp>.json`
2. Paste into Claude (or your AI of choice)
3. Ask the AI to generate a `place` / `delete` command list using the supplied material palette
4. Copy the AI's response

### Executing commands

1. Paste the AI's command JSON into the **Execute Commands** textarea
2. Click **Run Commands**
3. Green dots appear on the map at every placed location (hover for `0xITEMID Z=N` tooltip)
4. Snapshot list refreshes automatically with the new batch ID

Command format:
```json
[
  { "op": "place",  "itemId": 1, "x": 1440, "y": 1700, "z": 0, "hue": 0 },
  { "op": "delete", "itemId": 3274, "x": 1445, "y": 1705, "z": 12 }
]
```

### Verifying in-game

1. Stop your ModernUO (or other UO) server — it holds MUL files open while running
2. Copy the edited MUL files back to your server's data folder (or have the server read directly from `Map Files/` if it's the same directory)
3. Restart the server and game client (the client reads MUL files on startup)
4. Walk to the edited area

### Rolling back

Every `execute` call snapshots affected 8×8 blocks first.

1. Find the batch ID in the **Snapshots** section (most recent on top, up to 20 shown)
2. Click **Rollback**
3. The snapshot is restored and the snapshot list refreshes

---

## Project Structure

```
UOMapVibe/
├── src/
│   ├── UOMapVibe.Core/                  # MUL read/write + style analysis
│   │   ├── MulFiles/                    # MapReader, StaticsReader/Writer, TileDataReader, RadarColorReader
│   │   ├── Models/                      # LandTile, StaticTile, TileInfo, MapDimensions (all 6 facets)
│   │   ├── Operations/                  # RegionQuery, BatchExecutor
│   │   ├── Analysis/                    # StaticClassifier, OrientationDetector, BuildingMetrics, RoadDetector, StyleAnalyzer
│   │   └── Rollback/                    # SnapshotManager
│   ├── UOMapVibe.Api/                   # ASP.NET Minimal API + static-file server for web/
│   └── UOMapVibe.TileExporter/          # CLI: tile pyramid + item catalog
├── web/                                 # Leaflet app (HTML/CSS/JS)
│   ├── index.html
│   ├── css/style.css
│   └── js/
│       ├── app.js                       # Leaflet setup, UO ↔ latLng coordinate conversion
│       ├── annotationTools.js           # Leaflet.Draw shapes + label popups
│       ├── stylePreview.js              # Renders detected material palette
│       ├── enrichment.js                # "Prepare" — calls /api/prepare
│       ├── export.js                    # "Copy for AI" — clipboard + JSON download
│       ├── executor.js                  # "Run Commands" — POST /api/execute, snapshot list, rollback
│       └── tileBrowser.js               # Item catalog search (debounced)
├── tests/UOMapVibe.Core.Tests/          # xUnit tests
└── Map Files/                           # Your MUL files (not in git)
```

## API Reference

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/region?mapId=&x1=&y1=&x2=&y2=` | GET | Statics + terrain in a bounding box |
| `/api/style?mapId=&x1=&y1=&x2=&y2=` | GET | Style analysis (material palette + building metrics) |
| `/api/prepare?mapId=&targetX1=&targetY1=&targetX2=&targetY2=&contextRadius=40` | GET | Combined: target region data + style analysis from wider context + terrain Z grid + Z-level summary + AI instructions |
| `/api/execute` | POST | Body: `{ mapId?, commands: [{op, itemId, x, y, z, hue}] }` — auto-snapshots, returns `{ batchId, placed, deleted, errors }` |
| `/api/rollback/{batchId}` | POST | Restore the snapshot from before that batch |
| `/api/snapshots` | GET | List available snapshot batch IDs |
| `/api/catalog/search?q=` | GET | Search `tiledata.mul` items by name (max 50) |

`mapId` defaults to `appsettings.json` → `UOData.DefaultMapId`.

## Building from Source

```
dotnet build UOMapVibe.slnx          # build everything
dotnet test UOMapVibe.slnx           # run tests
dotnet run --project src/UOMapVibe.Api                # API server
dotnet run --project src/UOMapVibe.TileExporter -- --data "..." --out web   # tiles
```

## Multiple Facets

Each facet has its own MUL set. To use Trammel (map 1), for example:

1. Place `map1.mul`, `statics1.mul`, `staidx1.mul` alongside the Felucca files in `Map Files/`
2. Run TileExporter with `--map 1 --out web-trammel` (or use a different `--out` per facet)
3. Set `DefaultMapId: 1` in `appsettings.json`, or pass `?mapId=1` on API calls

Facet dimensions are hard-coded in [MapDimensions.cs](src/UOMapVibe.Core/Models/MapDimensions.cs).

## Troubleshooting

**`MulDirectory not configured`**
→ `appsettings.json` is missing the `UOData.MulDirectory` setting or pointing at the wrong folder.

**No map tiles in the browser (blank gray)**
→ Run TileExporter first. Confirm `web/tiles/0/0/0.bmp` exists.

**TileExporter says "Usage: ..." and exits**
→ Pass both `--data` and `--out`. Example: `dotnet run --project src/UOMapVibe.TileExporter -- --data "C:\path\Map Files" --out web`

**Coordinates in the status bar but no green dots after Execute**
→ Open browser DevTools → Console. The API logs errors there. Common causes: server can't write to `Map Files/` (permission/lock), or `op` value is something other than `"place"` / `"delete"`.

**`File is locked` errors when executing**
→ Your UO server is running and holding `statics0.mul` open. Stop it before editing.

**Changes not visible in-game**
→ Restart both the UO server *and* the game client. The client caches MUL data on startup.

**Style preview is empty or sparse**
→ Your annotation is in a region with no nearby static structures (open wilderness, water). Draw closer to existing buildings, or increase `contextRadius` in [enrichment.js](web/js/enrichment.js).

---

## License

MIT
