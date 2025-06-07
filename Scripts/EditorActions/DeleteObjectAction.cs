using Godot;

// Assuming PlacedObject, AssetManager, TileDrawer, MapSaverLoader.PlacedObjectSaveData are accessible
// This might require:
// using YourProject.Scripts.Objects; // For PlacedObject
// using YourProject.Scripts.Core;    // For AssetManager
// (If they were namespaced. Currently, they are in the default namespace)

public class DeleteObjectAction : EditorAction
{
	private MapSaverLoader.PlacedObjectSaveData deletedObjectData; // Stores all data needed to recreate
	private NodePath placedObjectsRootPath;     // Path to the parent node for objects
	private AssetManager assetManagerInstance;
	private string placedObjectPackedScenePath; // Store path to PlacedObject.tscn

	// To keep a temporary reference to the node that was just deleted by Execute,
	// so that Undo can re-parent it if it was merely removed (not freed) by a more complex Execute.
	// However, current plan is Execute frees, Undo re-instances. So this isn't strictly needed for that.
	// private Node recentlyDeletedNodeForPotentialImmediateUndoOptimization;

	public DeleteObjectAction(PlacedObject objectToDelete, NodePath rootPath, AssetManager manager, string objectScenePath)
	{
		this.deletedObjectData = new MapSaverLoader.PlacedObjectSaveData
		{
			AssetNameRef = objectToDelete.AssetNameRef,
			MapLayerIndex = objectToDelete.MapLayerIndex,
			PosX = objectToDelete.GlobalPosition.X,
			PosY = objectToDelete.GlobalPosition.Y,
			RotationDegrees = objectToDelete.CurrentRotationDegrees,
			ScaleX = objectToDelete.CurrentScale.X,
			ScaleY = objectToDelete.CurrentScale.Y
		};
		this.placedObjectsRootPath = rootPath;
		this.assetManagerInstance = manager;
		this.placedObjectPackedScenePath = objectScenePath; // e.g., TileDrawer.PlacedObjectScenePath
		// Description = $"Delete Object '{deletedObjectData.AssetNameRef}'";
	}

	public override void Execute(TileMap tileMap) // tileMap provides context to find nodes via GetTree().Root
	{
		Node objectsRoot = tileMap.GetTree().Root.GetNode(placedObjectsRootPath);
		if (objectsRoot == null || !GodotObject.IsInstanceValid(objectsRoot)) {
			GD.PrintErr("DeleteObjectAction Execute: PlacedObjectsRoot not found or invalid via path: " + placedObjectsRootPath);
			return;
		}

		PlacedObject nodeToDelete = FindObjectInScene(objectsRoot);
		if (nodeToDelete != null && GodotObject.IsInstanceValid(nodeToDelete))
		{
			GD.Print($"DeleteObjectAction Execute: Removing object '{deletedObjectData.AssetNameRef}' (Instance ID: {nodeToDelete.GetInstanceId()})");
			nodeToDelete.GetParent()?.RemoveChild(nodeToDelete); // Should be objectsRoot
			nodeToDelete.QueueFree();
			// recentlyDeletedNodeForPotentialImmediateUndoOptimization = nodeToDelete; // Only if not freeing
		} else {
			// GD.Print($"DeleteObjectAction Execute: Object '{deletedObjectData.AssetNameRef}' matching criteria not found in scene or already deleted.");
		}
	}

	public override void Undo(TileMap tileMap)
	{
		Node objectsRoot = tileMap.GetTree().Root.GetNode(placedObjectsRootPath);
		if (objectsRoot == null || !GodotObject.IsInstanceValid(objectsRoot)) {
			GD.PrintErr("DeleteObjectAction Undo: PlacedObjectsRoot not found or invalid via path: " + placedObjectsRootPath);
			return;
		}
		if (assetManagerInstance == null) {
			GD.PrintErr("DeleteObjectAction Undo: AssetManager instance is null.");
			return;
		}

		// Check if object (matching criteria) already exists (e.g. if undo was called twice by mistake or state is weird)
		// This is somewhat handled by FindObjectInScene if Execute is called again for Redo.
		// If FindObjectInScene in Execute is robust, this check isn't strictly needed here before re-adding.

		AssetData assetData = assetManagerInstance.GetAsset(deletedObjectData.AssetNameRef); // Use GetAsset
		if (assetData == null) {
			GD.PrintErr($"DeleteObjectAction Undo: AssetData '{deletedObjectData.AssetNameRef}' not found in AssetManager.");
			return;
		}
		if (assetData.Category != AssetManager.AssetCategory.PlaceableObject) {
			GD.PrintErr($"DeleteObjectAction Undo: Asset '{deletedObjectData.AssetNameRef}' is not a PlaceableObject. Cannot re-instance for undo.");
			return;
		}

		PackedScene scene = ResourceLoader.Load<PackedScene>(placedObjectPackedScenePath);
		if (scene == null) {
			 GD.PrintErr($"DeleteObjectAction Undo: Failed to load PackedScene '{placedObjectPackedScenePath}'.");
			 return;
		}

		Node newInstance = scene.Instantiate();
		if (newInstance is PlacedObject placedObjectScript)
		{
			placedObjectScript.Initialize(assetData, deletedObjectData.MapLayerIndex);
			placedObjectScript.GlobalPosition = new Vector2(deletedObjectData.PosX, deletedObjectData.PosY);
			placedObjectScript.CurrentRotationDegrees = deletedObjectData.RotationDegrees;
			placedObjectScript.CurrentScale = new Vector2(deletedObjectData.ScaleX, deletedObjectData.ScaleY);

			objectsRoot.AddChild(placedObjectScript);
			// GD.Print($"DeleteObjectAction Undo: Re-created object '{deletedObjectData.AssetNameRef}' (New Instance ID: {placedObjectScript.GetInstanceId()})");
			// After re-instancing, if we wanted Execute (for Redo) to operate on *this specific instance*,
			// we'd need to update a reference. But current Execute finds by data.
		} else {
			GD.PrintErr("DeleteObjectAction Undo: Failed to instance or cast to PlacedObject script from scene '{placedObjectPackedScenePath}'.");
			newInstance.QueueFree(); // Clean up
		}
	}

	private PlacedObject FindObjectInScene(Node objectsRoot)
	{
		if (objectsRoot == null || !GodotObject.IsInstanceValid(objectsRoot)) return null;

		foreach (Node child in objectsRoot.GetChildren())
		{
			if (child is PlacedObject po)
			{
				// More robust check: Use a unique ID if objects have one.
				// For now, matching all stored properties.
				if (po.AssetNameRef == deletedObjectData.AssetNameRef &&
					po.MapLayerIndex == deletedObjectData.MapLayerIndex &&
					po.GlobalPosition.IsEqualApprox(new Vector2(deletedObjectData.PosX, deletedObjectData.PosY)) &&
					Mathf.IsEqualApprox(po.CurrentRotationDegrees, deletedObjectData.RotationDegrees) &&
					po.CurrentScale.IsEqualApprox(new Vector2(deletedObjectData.ScaleX, deletedObjectData.ScaleY)) )
				{
					return po;
				}
			}
		}
		return null;
	}
}
