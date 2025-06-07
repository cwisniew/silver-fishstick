using Godot;
using System.Collections.Generic;

// Assumes TileData and EditorAction are accessible

public class UpdateAutoTilesAction : EditorAction
{
	private List<PlaceTileAction> elementaryActions;

	public UpdateAutoTilesAction(List<PlaceTileAction> actions)
	{
		this.elementaryActions = actions ?? new List<PlaceTileAction>();
		// Description = $"Update Auto-Tiles ({elementaryActions.Count} changes)";
	}

	public override void Execute(TileMap tileMap)
	{
		// GD.Print($"UpdateAutoTilesAction: Executing {elementaryActions.Count} elementary actions.");
		foreach (var action in elementaryActions)
		{
			action.Execute(tileMap);
		}
	}

	public override void Undo(TileMap tileMap)
	{
		// GD.Print($"UpdateAutoTilesAction: Undoing {elementaryActions.Count} elementary actions.");
		// Undo in reverse order
		for (int i = elementaryActions.Count - 1; i >= 0; i--)
		{
			elementaryActions[i].Undo(tileMap);
		}
	}
}
