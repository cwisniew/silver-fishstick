using Godot;

// Assuming TileData and EditorAction are accessible

public class PlaceDoorWindowAction : EditorAction
{
	private Vector2I position;
	private TileData previousTile; // Should be the wall tile
	private TileData newTile;      // Should be the door/window tile
	private int layer;

	public PlaceDoorWindowAction(Vector2I position, TileData newTile, TileData previousTile, int layer = 0)
	{
		this.position = position;
		this.newTile = newTile;
		this.previousTile = previousTile; // The wall that was replaced
		this.layer = layer;
		// Description = $"Place {newTile.CategoryName} at ({position.X},{position.Y}) replacing {previousTile.CategoryName}";
		// (Requires CategoryName or similar in TileData or resolving it)
	}

	public override void Execute(TileMap tileMap)
	{
		// A door or window should always be a valid tile, not an "Empty" tile.
		// If newTile could represent an erase operation for a door/window, that logic would be here.
		// For now, assume newTile is always a valid door/window.
		if (newTile.IsEmpty())
		{
			// This case should ideally not be reached if we are "placing" a door/window.
			// If it means "erase whatever is at this position if it was a door/window",
			// then the previousTile would be the door/window, and newTile would be TileData.Empty.
			// The current logic implies newTile is the door/window itself.
			GD.PrintErr("PlaceDoorWindowAction: newTile is Empty, which is unexpected for placing a door/window.");
			tileMap.EraseCell(layer, position);
		}
		else
		{
			tileMap.SetCell(layer, position, newTile.SourceId, newTile.AtlasCoords);
		}
	}

	public override void Undo(TileMap tileMap)
	{
		// Restore the previous tile (which should have been a wall).
		if (previousTile.IsEmpty())
		{
			// This would happen if a door/window was placed on an empty tile (current logic prevents this)
			// or if the 'previousTile' was incorrectly recorded as empty.
			tileMap.EraseCell(layer, position);
		}
		else
		{
			tileMap.SetCell(layer, position, previousTile.SourceId, previousTile.AtlasCoords);
		}
	}
}
