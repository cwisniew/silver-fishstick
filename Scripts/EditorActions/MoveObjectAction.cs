using Godot;

public class MoveObjectAction : EditorAction
{
	private PlacedObject targetObject;
	private NodePath objectNodePath; // For potential re-acquisition if targetObject becomes invalid
	private Vector2 oldGlobalPosition;
	private Vector2 newGlobalPosition;

	public MoveObjectAction(PlacedObject obj, Vector2 oldPos, Vector2 newPos)
	{
		this.targetObject = obj;
		this.objectNodePath = obj.GetPath(); // Store path for potential robustness
		this.oldGlobalPosition = oldPos;
		this.newGlobalPosition = newPos;
		// Description = $"Move Object '{obj?.AssetNameRef ?? "Unknown"}' from {oldPos} to {newPos}";
	}

	public override void Execute(TileMap tileMap) // tileMap context not directly used by this action
	{
		// Attempt to use direct reference first
		if (targetObject != null && GodotObject.IsInstanceValid(targetObject))
		{
			targetObject.GlobalPosition = newGlobalPosition;
			// GD.Print($"MoveObjectAction Execute: Moved '{targetObject.AssetNameRef}' to {newGlobalPosition}");
		}
		else // Fallback: try to re-acquire node via path (e.g., if undo/redo of delete invalidated direct ref)
		{
			Node node = tileMap.GetTree().Root.GetNode(objectNodePath); // tileMap is used to GetTree()
			if (node is PlacedObject foundObject) {
				targetObject = foundObject; // Re-link
				targetObject.GlobalPosition = newGlobalPosition;
				// GD.Print($"MoveObjectAction Execute (Re-acquired): Moved '{targetObject.AssetNameRef}' to {newGlobalPosition}");
			} else {
				GD.PrintErr($"MoveObjectAction Execute: Target object at path '{objectNodePath}' not found or not a PlacedObject. Current direct ref is also invalid.");
			}
		}
	}

	public override void Undo(TileMap tileMap)
	{
		if (targetObject != null && GodotObject.IsInstanceValid(targetObject))
		{
			targetObject.GlobalPosition = oldGlobalPosition;
			// GD.Print($"MoveObjectAction Undo: Moved '{targetObject.AssetNameRef}' back to {oldGlobalPosition}");
		}
		else
		{
			Node node = tileMap.GetTree().Root.GetNode(objectNodePath);
			if (node is PlacedObject foundObject) {
				targetObject = foundObject; // Re-link
				targetObject.GlobalPosition = oldGlobalPosition;
				// GD.Print($"MoveObjectAction Undo (Re-acquired): Moved '{targetObject.AssetNameRef}' back to {oldGlobalPosition}");
			} else {
				GD.PrintErr($"MoveObjectAction Undo: Target object at path '{objectNodePath}' not found or not PlacedObject. Current direct ref is also invalid.");
			}
		}
	}
}
