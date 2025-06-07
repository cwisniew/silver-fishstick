using Godot;

public class RotateObjectAction : EditorAction
{
	private NodePath targetObjectPath;
	private float oldRotationDegrees;
	private float newRotationDegrees;
	private PlacedObject resolvedTargetObject;

	public RotateObjectAction(PlacedObject obj, float oldRotDeg, float newRotDeg)
	{
		if (obj == null || !GodotObject.IsInstanceValid(obj)) {
			GD.PrintErr("RotateObjectAction: Initial PlacedObject is null or invalid.");
			this.targetObjectPath = null;
		} else {
			this.targetObjectPath = obj.GetPath();
		}
		this.resolvedTargetObject = obj;
		this.oldRotationDegrees = oldRotDeg;
		this.newRotationDegrees = newRotDeg;
		// Description = $"Rotate Object {targetObjectPath?.ToString() ?? "Unknown"} from {oldRotDeg}° to {newRotDeg}°";
	}

	private PlacedObject GetTargetObject(Node contextNode) {
		if (resolvedTargetObject != null && GodotObject.IsInstanceValid(resolvedTargetObject)) {
			return resolvedTargetObject;
		}
		if (targetObjectPath != null && contextNode != null && contextNode.IsInsideTree()) { // Ensure contextNode is in tree
			Node node = contextNode.GetTree().Root.GetNodeOrNull(targetObjectPath); // Use GetNodeOrNull
			if (node is PlacedObject po) {
				resolvedTargetObject = po; // Cache if re-acquired
				return po;
			}
		}
		GD.PrintErr($"RotateObjectAction: Could not resolve target object from path: {targetObjectPath?.ToString() ?? "null"}");
		return null;
	}

	public override void Execute(TileMap tileMapContext)
	{
		PlacedObject target = GetTargetObject(tileMapContext);
		if (target != null)
		{
			target.CurrentRotationDegrees = newRotationDegrees;
			// GD.Print($"RotateObjectAction Execute: Rotated '{target.AssetNameRef}' to {newRotationDegrees} deg");
		}
	}

	public override void Undo(TileMap tileMapContext)
	{
		PlacedObject target = GetTargetObject(tileMapContext);
		if (target != null)
		{
			target.CurrentRotationDegrees = oldRotationDegrees;
			// GD.Print($"RotateObjectAction Undo: Rotated '{target.AssetNameRef}' back to {oldRotationDegrees} deg");
		}
	}
}
