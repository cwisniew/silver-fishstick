using Godot;

public class ScaleObjectAction : EditorAction
{
	private NodePath targetObjectPath;
	private Vector2 oldScale;
	private Vector2 newScale;
	private PlacedObject resolvedTargetObject;

	public ScaleObjectAction(PlacedObject obj, Vector2 oldScl, Vector2 newScl)
	{
		 if (obj == null || !GodotObject.IsInstanceValid(obj)) {
			GD.PrintErr("ScaleObjectAction: Initial PlacedObject is null or invalid.");
			this.targetObjectPath = null;
		} else {
			this.targetObjectPath = obj.GetPath();
		}
		this.resolvedTargetObject = obj;
		this.oldScale = oldScl;
		this.newScale = newScl;
		// Description = $"Scale Object {targetObjectPath?.ToString() ?? "Unknown"} from {oldScl} to {newScl}";
	}

	private PlacedObject GetTargetObject(Node contextNode) {
		if (resolvedTargetObject != null && GodotObject.IsInstanceValid(resolvedTargetObject)) {
			return resolvedTargetObject;
		}
		if (targetObjectPath != null && contextNode != null && contextNode.IsInsideTree()) {
			Node node = contextNode.GetTree().Root.GetNodeOrNull(targetObjectPath);
			if (node is PlacedObject po) {
				resolvedTargetObject = po;
				return po;
			}
		}
		GD.PrintErr($"ScaleObjectAction: Could not resolve target object from path: {targetObjectPath?.ToString() ?? "null"}");
		return null;
	}

	public override void Execute(TileMap tileMapContext)
	{
		PlacedObject target = GetTargetObject(tileMapContext);
		if (target != null)
		{
			target.CurrentScale = newScale; // Uses the property setter in PlacedObject
			// GD.Print($"ScaleObjectAction Execute: Scaled '{target.AssetNameRef}' to {newScale}");
		}
	}

	public override void Undo(TileMap tileMapContext)
	{
		PlacedObject target = GetTargetObject(tileMapContext);
		if (target != null)
		{
			target.CurrentScale = oldScale; // Uses the property setter
			// GD.Print($"ScaleObjectAction Undo: Scaled '{target.AssetNameRef}' back to {oldScale}");
		}
	}
}
