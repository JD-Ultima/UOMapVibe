# UOMapVibe — AI-Assisted UO Map Editing

AI-assisted Ultima Online map editing through visual annotation. Draw on a map to communicate locations and intent, the system automatically analyzes the surrounding architecture for style matching, and AI generates precise edit commands that execute directly against MUL files. Verify changes in-game.

## How It Works (The Short Version)

1. Open the web app, navigate to an area on the UO map
2. Draw annotations: rectangles, polylines, markers with text descriptions
3. Click "Prepare" — the system auto-detects building materials, wall orientations, road styles from surrounding structures
4. Click "Copy for AI" — enriched JSON + screenshot ready for Claude
5. Claude generates and executes map edit commands via the local API
6. Restart game client, walk to the area — see your changes in-game

**The user only describes WHAT they want. The system tells the AI HOW to build it. The AI decides WHERE to place each piece.**

---

## Repository

- **Location**: `C:\Users\USER-002\Desktop\New Test\UOMapVibe\`
- **GitHub**: `JD-Ultima/UOMapVibe` (public, MIT license)

---

## Honest Assessment: What's Hard

### File Locking (manageable)
Server and game client share the same MUL files on disk. ModernUO opens MUL files with `FileShare.Read` (confirmed from ModernUO's TileMatrix.cs lines 98-137).

**Strategy**: Stop the ModernUO server before editing MUL files, edit, restart server. The game client reads MUL files on startup, so restart the client after edits too. This is the standard workflow for any map editing tool.

Our API opens files ONLY during individual read/write operations (open -> read/write -> close immediately). Between edits, no locks held.

### No Existing Write Code
ModernUO has complete MUL READ code but ZERO write code. The write path must be built from scratch. Key challenges:
- Statics are variable-length per block -- can't overwrite in place when adding items
- Index file (staidx0.mul) must stay consistent with data file (statics0.mul)
- Empty blocks need special handling (index entry with lookup = -1 or length = 0)

### Tile Catalog Quality (solved by style inference)
tiledata.mul item names are often generic ("wall", "floor") or empty. Manual catalog browsing is unreliable. **Solution**: The AI doesn't browse a catalog at all. It automatically analyzes nearby buildings/roads in the map data to extract the exact item IDs already in use. The user never needs to know an item ID.

### AI Accuracy -- Iteration Expected
Placing 50 statics to build a house involves orientation decisions, Z-level stacking, wall direction matching. The AI will get close but need iteration. **The system's job is making iteration fast** -- not promising perfection on the first try. Style inference dramatically reduces the most common errors (wrong materials, wrong item IDs).

---

## Core Architecture

```
UOMapVibe/
├── src/
│   ├── UOMapVibe.Core/               # MUL file reader + writer
│   │   ├── MulFiles/
│   │   │   ├── MapReader.cs           # Read terrain blocks from map0.mul
│   │   │   ├── StaticsReader.cs       # Read statics via staidx0.mul + statics0.mul
│   │   │   ├── StaticsWriter.cs       # Write statics (append + reindex)
│   │   │   ├── TileDataReader.cs      # Parse tiledata.mul -> item catalog
│   │   │   └── RadarColorReader.cs    # Parse RadarCol.mul (with fallback if absent)
│   │   ├── Models/
│   │   │   ├── LandTile.cs            # ushort Id, sbyte Z (3 bytes on disk)
│   │   │   ├── StaticTile.cs          # ushort Id, byte X, byte Y, sbyte Z, short Hue (7 bytes)
│   │   │   └── TileInfo.cs            # Name, flags, height from tiledata
│   │   ├── Operations/
│   │   │   ├── RegionQuery.cs         # Read all statics + terrain in a bounding box
│   │   │   ├── StaticPlacer.cs        # Insert statics into a block
│   │   │   ├── StaticRemover.cs       # Remove statics from a block
│   │   │   └── BatchExecutor.cs       # Execute a list of place/delete commands
│   │   ├── Analysis/
│   │   │   ├── StaticClassifier.cs    # Classify statics by TileFlag: wall, floor, roof, etc.
│   │   │   ├── OrientationDetector.cs # Detect wall orientation from placement patterns
│   │   │   ├── BuildingMetrics.cs     # Extract footprint size, wall height, Z offsets
│   │   │   ├── RoadDetector.cs        # Detect road materials from linear surface patterns
│   │   │   └── StyleAnalyzer.cs       # Orchestrates all analysis -> MaterialPalette
│   │   └── Rollback/
│   │       └── SnapshotManager.cs     # Save/restore block state before/after edits
│   │
│   ├── UOMapVibe.Api/                 # HTTP API (ASP.NET Minimal API)
│   │   ├── Program.cs                 # Endpoints + static file serving
│   │   ├── Endpoints/                 # Endpoint groupings
│   │   └── appsettings.json           # Path to UO data directory
│   │
│   └── UOMapVibe.TileExporter/        # CLI: generate map tiles + item catalog
│       └── Program.cs                 # Reads MUL files, outputs Leaflet tiles + JSON catalog
│
├── web/                               # Annotation web app (Leaflet + Leaflet.Draw)
│   ├── index.html
│   ├── css/style.css
│   └── js/
│       ├── app.js                     # Leaflet setup, coordinate system, toolbar
│       ├── annotationTools.js         # Leaflet.Draw: circle, rect, polyline, marker, text
│       ├── tileBrowser.js             # Fallback: manual item search by name/ID
│       ├── stylePreview.js            # Shows auto-detected material palette
│       ├── enrichment.js              # "Prepare" button: calls /api/prepare
│       ├── export.js                  # "Copy for AI": clipboard enriched JSON + screenshot
│       └── executor.js                # "Execute": send command batch to API
│
├── tests/
│   └── UOMapVibe.Core.Tests/
│
├── UOMapVibe.md                       # This file -- project plan and documentation
├── LICENSE                            # MIT
└── .gitignore
```

**What's intentionally NOT here:**
- No direct edit tools (clear, fill, copy) -- use CentrED# for simple edits, UOMapVibe for AI-assisted creative edits
- No MapWriter.cs -- terrain editing adds complexity, statics are 95% of map editing work
- No FillRegion, CopyRegion operations -- scope creep. Start with place + delete statics only

---

## Automatic Style Inference (the core innovation)

### The Problem It Solves
User says "build a house here that matches the city." Without automation, they'd need to:
1. Open CentrED# or UOFiddler to inspect nearby buildings
2. Click on walls to find item IDs (e.g., 0x0001 = stone wall south)
3. Click on floors, roofs, doors, windows -- note every ID
4. Manually provide all these IDs to the AI
5. Hope they got the right orientations

**With style inference**: The system queries the surrounding area, classifies every static by function using tiledata flags, counts frequency, detects orientation from placement patterns, and hands the AI a complete material palette. Zero manual lookup.

### How It Works

#### Step 1: Query a context region
When user draws an annotation at (1440, 1700), the system automatically queries a larger area -- 40-tile radius around the annotation center. This captures nearby buildings, roads, and features.

#### Step 2: Classify every static by function
Using confirmed TileFlag values from tiledata.mul (ModernUO TileData.cs line 216-284):

| Category | Classification Rule |
|----------|-------------------|
| **Walls** | `TileFlag.Wall` set, OR (`TileFlag.Impassable` + height > 0 + NOT Door/Window) |
| **Floors** | `TileFlag.Surface` set + height == 0 |
| **Roofs** | `TileFlag.Roof` set |
| **Doors** | `TileFlag.Door` set |
| **Windows** | `TileFlag.Window` set |
| **Stairs** | `TileFlag.StairBack` or `TileFlag.StairRight` set |
| **Bridges** | `TileFlag.Bridge` set |
| **Vegetation** | `TileFlag.Foliage` set |
| **Light sources** | `TileFlag.LightSource` set (torches, candles, lanterns) |
| **Water features** | `TileFlag.Wet` set |
| **Furniture/Decor** | Surface items with height > 0 that aren't walls/stairs/bridges |

#### TileFlag Reference (from ModernUO TileData.cs)
```
None        = 0x00000000    Background  = 0x00000001    Weapon      = 0x00000002
Transparent = 0x00000004    Translucent = 0x00000008    Wall        = 0x00000010
Damaging    = 0x00000020    Impassable  = 0x00000040    Wet         = 0x00000080
Surface     = 0x00000200    Bridge      = 0x00000400    Window      = 0x00001000
Foliage     = 0x00020000    Container   = 0x00200000    LightSource = 0x00800000
Roof        = 0x10000000    Door        = 0x20000000    StairBack   = 0x40000000
StairRight  = 0x80000000
```

#### Step 3: Count frequency and rank by usage
For each category, count how many times each itemId appears. The most frequent items = the dominant style.
```
walls:  itemId 1 (x120), itemId 2 (x115), itemId 3 (x118), itemId 4 (x112)
floors: itemId 1301 (x80), itemId 1302 (x12)
roofs:  itemId 51 (x45), itemId 52 (x40)
doors:  itemId 1709 (x8)
```

#### Step 4: Detect wall orientation from placement patterns
UO wall items come in sets (South-facing, East-facing, corner pieces, etc.). We detect orientation by analyzing how each itemId is placed:
- If itemId 1 consistently appears in lines along the Y-axis (same X, varying Y) -> South-facing wall (runs E-W)
- If itemId 2 consistently appears in lines along the X-axis (same Y, varying X) -> East-facing wall (runs N-S)
- Corner pieces appear at direction changes

Algorithm:
```
For each wall itemId with count > 3:
  Collect all (x, y) positions where it appears
  Calculate variance in X vs variance in Y
  If X-variance >> Y-variance: runs along X-axis (E-W wall = S or N facing)
  If Y-variance >> X-variance: runs along Y-axis (N-S wall = E or W facing)
  If roughly equal variance: corner piece or scattered placement
```

#### Step 5: Measure building patterns
From the wall placements, extract structural metrics:
- Typical wall height (Z difference between floor and highest wall in same column)
- Typical building footprint (detect rectangular wall clusters)
- Floor Z level relative to terrain
- Whether buildings in the area have second floors (wall statics at Z > ground + wall_height)
- Roof Z offset from wall tops

#### Step 6: Detect road style
Find surface-flagged items at ground level that form linear patterns:
- Group ground-level Surface items by itemId
- For each, check if positions form a path (connected tiles in a roughly linear direction)
- The most frequent path-forming surface item = road material
- Also detect edge/border tiles (less frequent items adjacent to the main road)

### Style Analysis Output
```json
{
  "analyzed_region": { "x1": 1400, "y1": 1660, "x2": 1480, "y2": 1740 },
  "material_palette": {
    "walls": {
      "south_facing": { "itemId": 1, "name": "stone wall", "count": 120 },
      "east_facing": { "itemId": 2, "name": "stone wall", "count": 115 },
      "north_facing": { "itemId": 3, "name": "stone wall", "count": 118 },
      "west_facing": { "itemId": 4, "name": "stone wall", "count": 112 },
      "corner_pieces": [{ "itemId": 5, "name": "stone wall corner", "count": 32 }]
    },
    "floors": [
      { "itemId": 1301, "name": "stone pavers", "count": 80, "primary": true },
      { "itemId": 1302, "name": "stone paver edge", "count": 12 }
    ],
    "roofs": [
      { "itemId": 51, "name": "stone roof S", "count": 45 },
      { "itemId": 52, "name": "stone roof E", "count": 40 }
    ],
    "doors": [
      { "itemId": 1709, "name": "wooden door S", "count": 8 }
    ],
    "windows": [],
    "stairs": [
      { "itemId": 1822, "name": "stone stairs S", "count": 4 }
    ],
    "decorations": [
      { "itemId": 2880, "name": "torch", "count": 14, "flags": ["LightSource"] },
      { "itemId": 2902, "name": "sign", "count": 6 }
    ],
    "road_surface": [
      { "itemId": 1301, "name": "stone pavers", "count": 240, "pattern": "linear" }
    ]
  },
  "building_metrics": {
    "avg_footprint_width": 8,
    "avg_footprint_depth": 6,
    "wall_height": 20,
    "floor_z_relative_to_terrain": 7,
    "roof_z_offset": 20,
    "has_multi_story": false,
    "building_count_in_region": 4
  }
}
```

### Why This Eliminates Style Drift
- AI uses the **exact same item IDs** as surrounding buildings -- impossible to use wrong materials
- Wall orientations are pre-mapped -- AI knows which ID to use for N/S/E/W walls
- Building dimensions come from actual nearby structures -- new buildings match scale
- Floor Z level comes from real data -- no floating or buried floors
- Road extensions use the same tile -- seamless continuation

---

## MUL File Format Reference

### map0.mul -- Terrain Data
- Organized in 8x8 blocks (64 cells per block)
- 4-byte header per block, then 64 cells x 3 bytes = 192 bytes data
- Total per block: 196 bytes
- Per cell: `ushort tileId` (2 bytes) + `sbyte z` (1 byte)
- Block offset: `(blockX * blockHeight + blockY) * 196 + 4`
- World coord to block: `blockX = worldX >> 3`, `blockY = worldY >> 3`
- Cell within block: `cellX = worldX & 0x7`, `cellY = worldY & 0x7`

### staidx0.mul -- Static Index
- 12 bytes per block: `int lookup` (offset into statics0.mul) + `int length` + `int extra`
- Index offset: `(blockX * blockHeight + blockY) * 12`
- lookup = -1 means empty block (no statics)
- Number of statics in block: `length / 7`

### statics0.mul -- Static Item Data
- 7 bytes per static: `ushort itemId` + `byte x_offset` + `byte y_offset` + `sbyte z` + `short hue`
- x_offset and y_offset are 0-7 (position within 8x8 block)
- World coordinates: `worldX = blockX * 8 + x_offset`, `worldY = blockY * 8 + y_offset`

### tiledata.mul -- Tile Metadata
- Two sections: Land tiles (0x4000 entries) then Item tiles (0x10000 entries max)
- 4-byte header every 32 entries (skip it)
- Per item: flags (4 or 8 bytes depending on client version) + weight + quality + animation + ... + height + 20-byte name
- File size determines flag width: >= 3,188,736 bytes = 64-bit flags

### Writing Statics (append strategy)
When adding/removing statics from a block:
1. Read current block data from statics0.mul (using index offset + length)
2. Modify in memory (add/remove StaticTile entries)
3. **Append** new block data to END of statics0.mul (can't overwrite -- new data may be different size)
4. Update staidx0.mul entry to point to new offset + new length
5. Old data at previous offset becomes orphaned (harmless dead space)

Edge cases:
- **Empty block after deletion**: Set index lookup to -1 (0xFFFFFFFF) and length to -1
- **File growth**: Appending causes statics0.mul to grow. Optional compaction later.
- **Multiple blocks per edit**: A region edit may span many 8x8 blocks. Process each independently.

---

## API Endpoints

```
GET  /api/region?mapId=0&x1=..&y1=..&x2=..&y2=..
     Returns: { statics: [...], terrain: [...] }
     Statics: itemId, name, x, y, z, hue, flags
     Terrain: tileId, x, y, z (height at each cell)

GET  /api/style?mapId=0&x1=..&y1=..&x2=..&y2=..
     Returns style analysis: material_palette + building_metrics
     Key endpoint -- auto-extracts the building vocabulary

GET  /api/prepare?mapId=0&targetX1=..&targetY1=..&targetX2=..&targetY2=..&contextRadius=40
     Combined "prepare for AI" endpoint:
       1. Queries statics + terrain in the target area
       2. Queries style analysis in the wider context area (target + radius)
       3. Returns everything AI needs in one call

POST /api/execute
     Body: { commands: [{ op: "place"|"delete", itemId, x, y, z, hue }] }
     Auto-snapshots affected blocks before execution
     Returns: { batchId, placed: N, deleted: N, errors: [...] }

POST /api/rollback/{batchId}
     Restores snapshot from before that batch

GET  /api/catalog/search?q=stone+wall
     Fallback: search tiledata items by name/flags
```

The API also serves the `web/` directory as static files -- everything runs from one `dotnet run`.

---

## Workflow

### Setup (one-time)
1. Point `appsettings.json` at UO data directory containing MUL files
2. Run TileExporter to generate Leaflet tile images + `tile_catalog.json`
3. Start API: `dotnet run --project src/UOMapVibe.Api`
4. Open `http://localhost:5000` in browser

### Edit Cycle
```
1. USER opens web app, navigates Leaflet map to area of interest
2. USER draws annotations:
   - Rectangle around area: "build a shop here"
   - Polyline along a path: "extend the road to the east"
   - Marker on a spot: "add a well"
   (NO item selection needed -- just describe intent)
3. USER clicks "Prepare for AI"
   -> Web app calls GET /api/prepare (one call does everything):
     a) Queries all statics in the target area (what's there now)
     b) Queries terrain heights (ground level)
     c) Runs style analysis on 40-tile radius (nearby building materials)
     d) Detects wall orientations, building dimensions, road materials
   -> Returns fully enriched JSON with zero user effort
4. USER clicks "Copy for AI" -> enriched JSON + annotated screenshot saved
5. USER pastes into Claude Code:
   "Edit this map area: [JSON path] [screenshot path]"
6. CLAUDE reads everything, generates commands using auto-detected materials,
   and executes directly:
   curl -X POST http://localhost:5000/api/execute -d '{"commands": [...]}'
7. USER checks results:
   - Web app shows placed statics as markers on map (quick layout check)
   - Restart game client -> walk to area -> see changes in-game
8. If wrong -> rollback + draw corrections -> repeat from 2
```

### Enriched AI Payload (fully automated)
```json
{
  "map_id": 0,
  "annotations": [
    {
      "id": 1,
      "type": "rectangle",
      "bounds": { "x1": 1440, "y1": 1700, "x2": 1450, "y2": 1710 },
      "label": "add a shop building here with a counter inside"
    }
  ],
  "style_analysis": {
    "source_region": "40-tile radius around annotation center",
    "material_palette": {
      "walls": {
        "south": { "itemId": 1, "name": "stone wall", "count": 120 },
        "east":  { "itemId": 2, "name": "stone wall", "count": 115 },
        "north": { "itemId": 3, "name": "stone wall", "count": 118 },
        "west":  { "itemId": 4, "name": "stone wall", "count": 112 }
      },
      "floors": [{ "itemId": 1301, "name": "stone pavers", "count": 80 }],
      "roofs":  [{ "itemId": 51, "name": "stone roof S", "count": 45 }],
      "doors":  [{ "itemId": 1709, "name": "wooden door S", "count": 8 }],
      "stairs": [{ "itemId": 1822, "name": "stone stairs S", "count": 4 }],
      "decorations": [
        { "itemId": 2880, "name": "torch", "count": 14 },
        { "itemId": 7845, "name": "shop counter", "count": 3 }
      ]
    },
    "building_metrics": {
      "avg_footprint": { "width": 8, "depth": 6 },
      "wall_height": 20,
      "floor_z_offset": 7,
      "roof_z_offset": 20
    }
  },
  "existing_statics": [
    { "itemId": 3274, "name": "tree", "x": 1445, "y": 1705, "z": 12 }
  ],
  "terrain": [
    { "x": 1440, "y": 1700, "z": 12 },
    { "x": 1441, "y": 1700, "z": 12 }
  ]
}
```

The AI knows:
- WHAT materials to use (from style analysis -- same as surrounding buildings)
- WHICH orientation for each wall direction (from placement pattern detection)
- HOW big and tall to build (from building metrics)
- WHAT Z level for floors/roofs (from measured offsets)
- WHAT's already there (from region query)
- WHERE the ground is (from terrain data)

The user only describes the intent. Everything else is automatic.

---

## Leaflet Tile Generation

### Color source
- **If RadarCol.mul exists**: 2 bytes per tile ID -> RGB565 color. Standard UO client file.
- **Fallback if absent**: Generate colors from tiledata.mul flags:
  - Water/wet -> blue
  - Impassable + no surface -> dark gray (rock/mountain)
  - Surface -> brown/tan (ground)
  - Default -> green (grass/vegetation)
  - Statics rendered as darker dots overlaid on terrain

### Tile pyramid
- Zoom level 0: entire map in one tile (tiny overview)
- Zoom level 5: ~1 pixel per UO tile (full detail)
- Standard Leaflet z/x/y.png naming convention
- For a 7168x4096 map, zoom 5 generates ~900 tiles (256px each)

### Coordinate system
- UO: origin top-left, X = east, Y = south
- Leaflet: use `L.CRS.Simple` with custom bounds matching UO dimensions
- Coordinate mapping must be pixel-perfect -- off-by-one = edit in wrong spot

---

## Implementation Phases

### Phase 0: Proof of Concept (MUST DO FIRST)
Before building anything else, prove the fundamentals with REAL MUL files:
1. Minimal C# console app that:
   - Opens staidx0.mul + statics0.mul
   - Reads statics at a known location (e.g., Britain bank)
   - Prints them (itemId, x, y, z, hue)
   - Adds one new static (e.g., a torch)
   - Writes back (append + reindex)
   - Reads again to verify
2. Verify in-game:
   - Stop ModernUO server
   - Run the edit
   - Restart server + client
   - Walk to the coordinate -- confirm the torch is there
3. If Phase 0 fails, debug before investing more time.

### Phase 1: Core Library
- MapReader, StaticsReader, StaticsWriter, TileDataReader
- RegionQuery, StaticPlacer, StaticRemover, BatchExecutor
- SnapshotManager
- Tests with real MUL files

### Phase 1.5: Style Analysis Engine
- StaticClassifier: categorize by TileFlag
- OrientationDetector: X/Y variance of wall placements -> orientation mapping
- BuildingMetrics: footprints, wall heights, Z offsets
- RoadDetector: linear patterns of ground-level surface tiles
- StyleAnalyzer: orchestrate all -> MaterialPalette JSON
- Tests: run against real map data (Britain), verify detected materials match known buildings

### Phase 2: Tile Exporter
- RadarCol reader (with flag-based fallback)
- Leaflet tile pyramid generator
- Tile catalog JSON from tiledata.mul
- Verify: tiles load correctly in browser

### Phase 3: HTTP API
- Region query, style analysis, prepare, execute, rollback endpoints
- Serve web/ as static files
- Test via curl

### Phase 4: Web App
- Leaflet map with L.CRS.Simple coordinate system
- Leaflet.Draw annotation tools
- "Prepare" button -> /api/prepare
- Style preview panel
- "Copy for AI" + "Execute" buttons
- Placed statics shown as markers after execution

### Phase 5: Polish
- Grid snapping for annotations
- Snapshot history panel
- Claude API integration directly in web app (skip clipboard copy)

---

## What Success Looks Like
User opens web app, navigates to an area near existing buildings, draws a rectangle, types "build a smithy here with an anvil and forge inside," clicks Prepare. The system automatically detects that nearby buildings use stone walls (IDs 1-4), stone floors (ID 1301), and wooden doors (ID 1709). User sees the detected palette in the style preview -- looks right, clicks Copy. Pastes into Claude Code. Claude calls the API to place ~30 statics using the exact same materials as surrounding buildings. User restarts the game client, logs in, walks to the spot -- the new smithy matches the city's architectural style. Maybe the anvil is one tile off -- user draws a correction annotation, iterates. Total: minutes instead of hours, with style consistency guaranteed.

---

## Prerequisites
1. UO data files: map0.mul, staidx0.mul, statics0.mul, tiledata.mul (+ optionally RadarCol.mul)
   - Single shared copy used by both ModernUO server and game client
2. .NET 8+ SDK
3. UO game client (for in-game visual verification)
4. A backup of MUL files before starting (we're writing to game data files)

## Verification Checklist
1. Phase 0: Read statics at known coords -> output matches expected items
2. Phase 0: Write one static -> re-read -> it's there
3. Phase 0: Restart ModernUO server + game client -> new static visible in-game
4. Phase 0: File access confirmed clean (no lingering locks after API closes files)
5. Phase 1: Place 10 statics in a row -> all appear correctly
6. Phase 1: Delete 5 of them -> they're gone, others remain
7. Phase 1: Rollback -> all 10 restored
8. Phase 2: Leaflet tiles load in browser, coordinates match known UO locations
9. Phase 3: curl GET /api/region returns correct data
10. Phase 3: curl POST /api/execute places statics, POST /api/rollback restores
11. Phase 4: Full annotation -> enrichment -> Claude -> execute -> verify in-game
