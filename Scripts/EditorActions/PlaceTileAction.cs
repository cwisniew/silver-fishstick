using Godot;

public class PlaceTileAction : EditorAction
{
	private Vector2I position;
	private TileData previousTile;
	private TileData newTile;
	private int layer;

	public PlaceTileAction(Vector2I position, TileData newTile, TileData previousTile, int layer = 0)
	{
		this.position = position;
		this.newTile = newTile;
		this.previousTile = previousTile;
		this.layer = layer;
		// Description = $"Place tile at ({position.X},{position.Y})"; // Example description
	}

	public override void Execute(TileMap tileMap)
	{
		if (newTile.IsEmpty()) // If new tile is "Empty", effectively erase.
		{
			tileMap.EraseCell(layer, position);
		}
		else
		{
			tileMap.SetCell(layer, position, newTile.SourceId, newTile.AtlasCoords);
		}
	}

	public override void Undo(TileMap tileMap)
	{
		if (previousTile.IsEmpty()) // If previous tile was "Empty", erase to revert.
		{
			tileMap.EraseCell(layer, position);
		}
		else
		{
			tileMap.SetCell(layer, position, previousTile.SourceId, previousTile.AtlasCoords);
		}
	}
}
