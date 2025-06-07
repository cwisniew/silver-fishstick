using Godot;

public struct TileData
{
	public int SourceId { get; set; }
	public Vector2I AtlasCoords { get; set; }
	// public int AlternativeTile { get; set; } // Godot 4.x TileMap feature

	public static TileData Empty = new TileData { SourceId = -1, AtlasCoords = new Vector2I(-1, -1) };

	public TileData(int sourceId, Vector2I atlasCoords)
	{
		SourceId = sourceId;
		AtlasCoords = atlasCoords;
	}

	public bool IsEmpty() => SourceId == -1 && AtlasCoords == new Vector2I(-1,-1); // More robust check for empty
}
