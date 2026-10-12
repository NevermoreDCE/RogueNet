using System.Text;
using RogueNet.Logic.MapGeneration;

Console.OutputEncoding = Encoding.UTF8;

var seed = ReadSeed(args) ?? Random.Shared.Next();
var generator = new DungeonMapGenerator();
var dungeon = generator.Generate(random: new Random(seed));

Console.WriteLine($"RogueNet dungeon generator demo — seed: {seed}");
Console.WriteLine("Press any key to exit.");
Console.WriteLine();
Render(dungeon);

if (!Console.IsInputRedirected)
	Console.ReadKey(intercept: true);

static int? ReadSeed(string[] arguments)
{
	for (var index = 0; index < arguments.Length; index++)
	{
		if (arguments[index] == "--seed" && index + 1 < arguments.Length &&
			int.TryParse(arguments[index + 1], out var seed))
			return seed;
	}

	return null;
}

static void Render(DungeonMap map)
{
	for (var y = 0; y < map.Height; y++)
	{
		var activeColor = string.Empty;
		for (var x = 0; x < map.Width; x++)
		{
			var tile = map.Tiles[y, x];
			var color = GetColor(tile);
			if (color != activeColor)
			{
				Console.Write(color);
				activeColor = color;
			}

			Console.Write(GetGlyph(tile));
		}

		Console.WriteLine("\u001b[0m");
	}
}

static char GetGlyph(DungeonTile tile) => tile.Feature switch
{
	TileFeature.ClosedDoor or TileFeature.LockedDoor => '+',
	TileFeature.OpenDoor => '/',
	TileFeature.EntranceStairs => '<',
	TileFeature.ExitStairs => '>',
	TileFeature.Pillar => 'O',
	TileFeature.Water => '~',
	TileFeature.Trap => '^',
	TileFeature.Treasure => '$',
	_ => tile.Terrain switch
	{
		TerrainType.Rock => ' ',
		TerrainType.Floor => '.',
		TerrainType.Wall => '#',
		_ => ' '
	}
};

static string GetColor(DungeonTile tile) => tile.Feature switch
{
	TileFeature.ClosedDoor or TileFeature.LockedDoor => "\u001b[33m",
	TileFeature.OpenDoor => "\u001b[93m",
	TileFeature.EntranceStairs or TileFeature.ExitStairs => "\u001b[32m",
	TileFeature.Pillar => "\u001b[37m",
	TileFeature.Water => "\u001b[36m",
	TileFeature.Trap => "\u001b[31m",
	TileFeature.Treasure => "\u001b[93m",
	_ => tile.Terrain switch
	{
		TerrainType.Wall => "\u001b[90m",
		TerrainType.Floor => "\u001b[37m",
		_ => "\u001b[0m"
	}
};
