# UOMapVibe

AI-assisted map editing for Ultima Online. Draw what you want on a web map, and AI builds it using the same materials as nearby structures — automatically.

## What It Does

1. You open a web map of your UO world
2. You draw shapes where you want changes (rectangles, lines, markers)
3. You type what you want ("build a shop here", "extend the road east")
4. The system analyzes nearby buildings and detects their materials automatically
5. You copy the data to an AI (like Claude), which generates the exact edit commands
6. Commands execute directly against your MUL files — no CentrED# needed

The AI never guesses item IDs. It uses the same walls, floors, roofs, and doors as the buildings already around your target area.

---

## Prerequisites

Before you start, make sure you have:

1. **Windows PC** (tested on Windows 11)
2. **.NET 8 SDK** — download from [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
   - After installing, open a terminal and type `dotnet --version` to confirm it works
3. **UO MUL files** — you need these files from your UO installation:
   - `map0.mul` (terrain data)
   - `statics0.mul` (objects/items on the map)
   - `staidx0.mul` (index for statics)
   - `tiledata.mul` (item names and properties)
   - `radarcol.mul` (optional — colors for the map view)

> **Important:** Make a backup copy of your MUL files before editing. This tool writes directly to MUL files.

---

## Setup (One-Time)

### Step 1: Get the Code

Download or clone this repository to your computer:

```
git clone https://github.com/YOUR_USERNAME/UOMapVibe.git
cd UOMapVibe
```

### Step 2: Copy Your MUL Files

Create a folder called `Map Files` inside the UOMapVibe directory and copy your MUL files into it:

```
UOMapVibe/
  Map Files/
    map0.mul
    statics0.mul
    staidx0.mul
    tiledata.mul
    radarcol.mul     (optional)
```

### Step 3: Configure the Data Path

Open `src/UOMapVibe.Api/appsettings.json` and set `MulDirectory` to the full path of your `Map Files` folder:

```json
{
  "UOData": {
    "MulDirectory": "C:\\path\\to\\UOMapVibe\\Map Files",
    "SnapshotDirectory": "snapshots",
    "DefaultMapId": "0"
  }
}
```

Use double backslashes (`\\`) in the path on Windows.

### Step 4: Generate Map Tiles

This creates the visual map images for the web viewer:

```
dotnet run --project src/UOMapVibe.TileExporter
```

This takes a few minutes. When done, you'll see a `web/tiles/` folder with map images and a `web/data/tile_catalog.json` file.

### Step 5: Start the Server

```
dotnet run --project src/UOMapVibe.Api
```

The server starts at `http://localhost:5000`. Open that URL in your web browser.

---

## How to Use

### Drawing Annotations

1. Open `http://localhost:5000` in your browser
2. Navigate the map to find the area you want to edit
3. Use the drawing tools on the left side of the map:
   - **Rectangle** — select an area (best for buildings)
   - **Polyline** — draw a path (best for roads or walls)
   - **Marker** — mark a single point (best for placing one item)
   - **Circle** — select a circular area
4. After drawing, type a label describing what you want (e.g., "build a tavern here")

### Preparing Data for AI

1. Click **Prepare for AI** — this queries the map data around your annotations
   - The system reads all existing items in your target area
   - It analyzes a 40-tile radius to detect the local building style
   - You'll see the detected materials in the Style Preview panel
2. Review the detected materials — do they look right? (stone walls, wooden floors, etc.)
3. Click **Copy for AI** — this copies the enriched data to your clipboard

### Sending to AI

1. Open Claude (or another AI assistant)
2. Paste the copied data
3. The AI will generate a JSON command list using the detected materials
4. Copy the AI's command output

### Executing Commands

1. Paste the AI's command JSON into the **Execute Commands** text box
2. Click **Execute** — the commands are applied to your MUL files
3. Green dots appear on the map showing where items were placed

### Verifying In-Game

1. Stop your UO server (if running)
2. Copy the edited MUL files back to your UO server's data folder
3. Restart the server and game client
4. Walk to the edited area to see the changes

### Rolling Back

Every edit creates an automatic snapshot. If something looks wrong:

1. Click **Load Snapshots** in the Snapshots section
2. Find the batch you want to undo
3. Click **Rollback** next to it

---

## Project Structure

```
UOMapVibe/
├── src/
│   ├── UOMapVibe.Core/          # MUL file reading/writing + style analysis
│   ├── UOMapVibe.Api/           # Web server (API + serves the web app)
│   └── UOMapVibe.TileExporter/  # Generates map tile images
├── web/                         # Web app (HTML/CSS/JS)
├── tests/                       # Automated tests
└── Map Files/                   # Your MUL files go here (not in git)
```

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/region` | GET | Query statics + terrain in a bounding box |
| `/api/style` | GET | Analyze building style in a region |
| `/api/prepare` | GET | Combined region data + style analysis for AI |
| `/api/execute` | POST | Execute place/delete commands |
| `/api/rollback/{batchId}` | POST | Undo a batch of edits |
| `/api/snapshots` | GET | List available snapshots |
| `/api/catalog/search` | GET | Search item catalog by name |

## Building from Source

```bash
# Build everything
dotnet build UOMapVibe.slnx

# Run tests
dotnet test UOMapVibe.slnx

# Run the tile exporter
dotnet run --project src/UOMapVibe.TileExporter

# Run the API server
dotnet run --project src/UOMapVibe.Api
```

## Troubleshooting

**"MulDirectory not configured"**
→ Check that `appsettings.json` has the correct path to your Map Files folder.

**No map tiles showing in browser**
→ Run the TileExporter first (`dotnet run --project src/UOMapVibe.TileExporter`).

**Changes not visible in-game**
→ Make sure you restart both the UO server and game client after editing MUL files. The game reads MUL files on startup.

**"File is locked" errors**
→ Stop your UO server before editing. The server holds MUL files open while running.

---

## License

MIT
