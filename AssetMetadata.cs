using Godot;

public class AssetMetadata
{
	public string AssetName { get; set; }
	public string ImageFileName { get; set; } // Just the filename, not the full path
	public string Category { get; set; } // Store as string from OptionButton
	public bool IsSpritesheet { get; set; }
	public int TileWidth { get; set; }
	public int TileHeight { get; set; }
	public int SeparationX { get; set; }
	public int SeparationY { get; set; }
	public int MarginX { get; set; }
	public int MarginY { get; set; }
	public string TileSetResourcePath { get; set; } // Only if IsSpritesheet is true, otherwise null or empty
}
