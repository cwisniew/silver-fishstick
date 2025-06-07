using Godot;
using System.Collections.Generic;

// Assuming TileData and EditorAction are accessible

public class FillAction : EditorAction
{
	private List<PlaceTileAction> elementaryActions;

	public FillAction(List<PlaceTileAction> actions)
	{
		this.elementaryActions = actions ?? new List<PlaceTileAction>();
		// Description = $"Fill area ({elementaryActions.Count} tiles changed)";
	}

	public override void Execute(TileMap tileMap)
	{
		// GD.Print($"FillAction: Executing {elementaryActions.Count} elementary actions.");
		foreach (var action in elementaryActions)
		{
			action.Execute(tileMap);
		}
	}

	public override void Undo(TileMap tileMap)
	{
		// GD.Print($"FillAction: Undoing {elementaryActions.Count} elementary actions.");
		// Undo in reverse order to maintain correctness
		for (int i = elementaryActions.Count - 1; i >= 0; i--)
		{
			elementaryActions[i].Undo(tileMap);
		}
	}
}
