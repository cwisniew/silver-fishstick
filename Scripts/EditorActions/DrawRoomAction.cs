using Godot;
using System.Collections.Generic;

// Assuming TileData and EditorAction are accessible from the same base namespace
// or appropriate using directives are in place.

public class DrawRoomAction : EditorAction
{
	private List<PlaceTileAction> elementaryActions;

	public DrawRoomAction(List<PlaceTileAction> actions)
	{
		this.elementaryActions = actions ?? new List<PlaceTileAction>(); // Ensure list is not null
		// Description = $"Draw Room (composed of {elementaryActions.Count} tile placements)";
	}

	public override void Execute(TileMap tileMap)
	{
		// GD.Print($"DrawRoomAction: Executing {elementaryActions.Count} elementary actions.");
		foreach (var action in elementaryActions)
		{
			action.Execute(tileMap);
		}
	}

	public override void Undo(TileMap tileMap)
	{
		// GD.Print($"DrawRoomAction: Undoing {elementaryActions.Count} elementary actions.");
		// Undo in reverse order to maintain correctness, especially if actions could overlap
		// or have dependencies (though for simple wall placements, order might not be super critical).
		for (int i = elementaryActions.Count - 1; i >= 0; i--)
		{
			elementaryActions[i].Undo(tileMap);
		}
	}
}
