The Best Overall Approach
The most reliable, flexible method for Rogue‑like dungeon generation is:

Room‑First Generation + Corridor Graph Connectivity
This is the same approach used by:

Rogue

NetHack

Brogue

Many modern roguelikes

It’s simple, deterministic, and easy to tune.

# Map Data Models
Represent the dungeon as a rectangular grid of tiles. The map owns the dimensions and tiles, while each tile stores its terrain and any fixed feature. Room metadata is kept separately so generation can use room bounds and centers when building the connection graph.

```csharp
public enum TerrainType
{
	Rock,
	Floor,
	Wall
}

public enum TileFeature
{
	None,
	ClosedDoor,
    OpenDoor,
    LockedDoor,
	EntranceStairs,
	ExitStairs,
	Pillar,
	Water,
	Trap
}

public sealed class DungeonTile
{
	public TerrainType Terrain { get; set; } = TerrainType.Rock;
	public TileFeature Feature { get; set; } = TileFeature.None;
	public int? RoomId { get; set; }
}

public sealed class DungeonMap
{
	public int Width { get; }
	public int Height { get; }
	public DungeonTile[,] Tiles { get; }
	public List<Room> Rooms { get; } = new();

	public DungeonMap(int width, int height)
	{
		Width = width;
		Height = height;
		Tiles = new DungeonTile[height, width];

		for (var y = 0; y < height; y++)
		for (var x = 0; x < width; x++)
			Tiles[y, x] = new DungeonTile();
	}
}

public sealed record MapPoint(int X, int Y);

public sealed record Room(int Id, int X, int Y, int Width, int Height)
{
	public MapPoint Center => new(X + Width / 2, Y + Height / 2);
}
```

`Tiles[y, x]` uses row-first indexing. Keep moving actors and player-specific visibility/exploration outside `DungeonTile`; those change during play and can differ between players. The renderer can map terrain and features to their ANSI characters.

# Step 1 — Define Room Sizes
You want three categories:

Large rooms
Width: 12–18

Height: 8–12

Count: 2–3

Medium rooms
Width: 8–12

Height: 6–8

Count: 4–6

Small rooms
Width: 4–7

Height: 3–5

Count: 6–10

These ranges give you enough variety without breaking the ASCII feel.

# Step 2 — Place Rooms Randomly (with collision avoidance)
Algorithm:
Start with an empty grid (e.g., 80×25 or whatever your map size is).

For each room:

Randomly choose a width/height within its category

Randomly choose a top‑left coordinate

Check if it overlaps any existing room (with a 1‑tile buffer)

If it overlaps, retry

If it fits, stamp it onto the map

Why this works
You get organic layouts

Rooms feel scattered, not grid‑aligned

You avoid weird overlaps or merged rooms

Tip
Use a simple rectangle intersection check. It’s fast and trivial.

# Step 3 — Build a Connectivity Graph
Once rooms are placed, treat them as nodes in a graph.

For each room:
Store its center point (cx, cy)

Add it to a list of nodes

Now you need to connect these nodes so the dungeon is traversable.

Best method:
Minimum Spanning Tree (MST) + Random Extra Edges
This gives you:

Guaranteed connectivity

Natural corridor layout

Occasional loops (which feel great in Rogue)

## Steps:
Compute all pairwise distances between room centers

Run Kruskal’s algorithm to build an MST

Add 1–3 random extra edges for loops

This is a classic roguelike trick.

# Step 4 — Carve Corridors
For each edge in your graph:

Use L‑shaped corridors:
From room A center → horizontal to target x → vertical to target y
or

Vertical first, then horizontal

Randomize which direction you carve first.

Why L‑shapes?
Classic Rogue feel

Simple to implement

Works well with ASCII

Avoids diagonal carving complexity

Optional:
Add “jaggedness” by inserting a random bend

Add corridor width (1–2 tiles)

# Step 5 — Direct Room Connections (optional)
To mimic Rogue’s “rooms touching rooms” behavior:

Occasionally skip corridor carving:
If two rooms are adjacent or nearly adjacent and not already connected

Replace a single section of the wall between them with a door, or 15-25% of the time merge them into a larger composite room by removing all shared walls.

This creates:

Multi‑chamber rooms

Organic shapes

Interesting tactical spaces

# Step 6 — Add Doors
Doors make navigation more readable.

Rules:
Place doors at corridor/room boundaries

Only place doors where a corridor meets a room wall

Avoid placing doors inside rooms or in the middle of corridors

This is purely aesthetic but adds a lot of charm.

# Step 7 — Final Pass: Cleanup & Decoration
Add:

Walls around rooms

Walls around corridors

Floor tiles

An Entrance staircase

An Exit staircase at least a certain distance away from the Entrance

Optional features (pillars, traps, water, treasure)

This is where you can add flavor without changing the core algorithm.

# Summary of the Algorithm (high-level)
Generate rooms of varying sizes

Place rooms randomly with collision avoidance

Build a graph of room centers

Generate an MST + extra edges

Carve corridors between connected rooms

Add doors

Add walls and decorations


This is the exact approach used by many classic roguelikes because it’s:

Deterministic

Easy to debug

Easy to tune

Fast

Produces great layouts