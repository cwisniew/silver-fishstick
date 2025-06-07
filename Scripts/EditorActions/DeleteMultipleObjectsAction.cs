using Godot;
using System.Collections.Generic;

public class DeleteMultipleObjectsAction : EditorAction
{
	// List to store data of all objects deleted in this single action
	private List<MapSaverLoader.PlacedObjectSaveData> deletedObjectsDataList;
	private NodePath placedObjectsRootPath;
	private AssetManager assetManagerInstance;
	private string placedObjectScenePath; // Path to PlacedObject.tscn for re-instancing

	public DeleteMultipleObjectsAction(
		List<PlacedObject> objectsToDelete,
		NodePath rootNodePath,
		AssetManager manager,
		string scenePath)
	{
		this.deletedObjectsDataList = new List<MapSaverLoader.PlacedObjectSaveData>();
		foreach (PlacedObject obj in objectsToDelete)
		{
			if (obj != null && GodotObject.IsInstanceValid(obj))
			{
				this.deletedObjectsDataList.Add(new MapSaverLoader.PlacedObjectSaveData
				{
					AssetNameRef = obj.AssetNameRef,
					MapLayerIndex = obj.MapLayerIndex,
					PosX = obj.GlobalPosition.X,
					PosY = obj.GlobalPosition.Y,
					RotationDegrees = obj.CurrentRotationDegrees,
					ScaleX = obj.CurrentScale.X,
					ScaleY = obj.CurrentScale.Y
				});
			}
		}
		this.placedObjectsRootPath = rootNodePath;
		this.assetManagerInstance = manager;
		this.placedObjectScenePath = scenePath;
		// Description = $"Delete {deletedObjectsDataList.Count} objects";
	}

	public override void Execute(TileMap tileMapContext) // tileMapContext provides tree access
	{
		Node objectsRoot = tileMapContext.GetTree().Root.GetNode(placedObjectsRootPath);
		if (objectsRoot == null || !GodotObject.IsInstanceValid(objectsRoot)) {
			GD.PrintErr("DeleteMultipleObjectsAction Execute: PlacedObjectsRoot not found or invalid via path: " + placedObjectsRootPath);
			return;
		}

		int foundAndDeletedCount = 0;
		// On first execute, the objects are live and passed to constructor to get data.
		// This Execute primarily handles REDO. For REDO, we need to find the objects again.
		// If this is the FIRST execute, this loop might re-find objects that were just used for data collection.
		// This is fine as long as FindObjectInScene is based on data, not instance IDs.
		// The actual deletion in MainScene happens *after* this action is created.
		// So, for first execute, FindObjectInScene might not find anything IF called AFTER MainScene deletes.
		// The prompt for MainScene.cs has action.Execute() called *before* MainScene's Deselect.
		// This means the objects are still in the scene for the first Execute().

		List<PlacedObject> objectsInSceneToDelete = new List<PlacedObject>();
		foreach (var objectData in deletedObjectsDataList)
		{
			PlacedObject objInScene = FindObjectInScene(objectsRoot, objectData);
			if (objInScene != null && GodotObject.IsInstanceValid(objInScene))
			{
				objectsInSceneToDelete.Add(objInScene);
			}
		}

		if (objectsInSceneToDelete.Count == 0 && deletedObjectsDataList.Count > 0) {
			// GD.Print("DeleteMultipleObjectsAction Execute: No matching objects found to delete (perhaps already deleted or state changed).");
			return; // Nothing to do if none of the targets for deletion are found.
		}

		foreach (PlacedObject objToDel in objectsInSceneToDelete)
		{
			// GD.Print($"DeleteMultipleObjectsAction Execute: Removing '{objToDel.AssetNameRef}' (Instance ID: {objToDel.GetInstanceId()})");
			objToDel.GetParent()?.RemoveChild(objToDel);
			objToDel.QueueFree();
			foundAndDeletedCount++;
		}
		if (foundAndDeletedCount > 0) GD.Print($"DeleteMultipleObjectsAction Execute: Removed {foundAndDeletedCount} objects.");

	}

	public override void Undo(TileMap tileMapContext)
	{
		Node objectsRoot = tileMapContext.GetTree().Root.GetNode(placedObjectsRootPath);
		if (objectsRoot == null || !GodotObject.IsInstanceValid(objectsRoot)) {
			GD.PrintErr("DeleteMultipleObjectsAction Undo: PlacedObjectsRoot not found or invalid.");
			return;
		}
		if (assetManagerInstance == null) {
			GD.PrintErr("DeleteMultipleObjectsAction Undo: AssetManager instance is null.");
			return;
		}

		PackedScene scene = ResourceLoader.Load<PackedScene>(this.placedObjectScenePath);
		if (scene == null) {
			 GD.PrintErr($"DeleteMultipleObjectsAction Undo: Failed to load PackedScene '{this.placedObjectScenePath}'.");
			 return;
		}

		int restoredCount = 0;
		foreach (var objectData in deletedObjectsDataList)
		{
			// Optional: Check if an object matching objectData already exists (e.g. from a complex undo/redo sequence)
			// if (FindObjectInScene(objectsRoot, objectData) != null) {
			//     GD.PrintWarn($"DeleteMultipleObjectsAction Undo: Object '{objectData.AssetNameRef}' matching data seems to exist. Skipping re-creation.");
			//     continue;
			// }

			AssetData assetData = assetManagerInstance.GetAsset(objectData.AssetNameRef); // Use GetAsset
			if (assetData == null) {
				GD.PrintErr($"DeleteMultipleObjectsAction Undo: AssetData '{objectData.AssetNameRef}' not found. Skipping object restoration.");
				continue;
			}
			if (assetData.Category != AssetManager.AssetCategory.PlaceableObject) {
				GD.PrintErr($"DeleteMultipleObjectsAction Undo: Asset '{objectData.AssetNameRef}' is not PlaceableObject. Skipping.");
				continue;
			}

			Node newInstance = scene.Instantiate();
			if (newInstance is PlacedObject placedObjectScript)
			{
				placedObjectScript.Initialize(assetData, objectData.MapLayerIndex);
				placedObjectScript.GlobalPosition = new Vector2(objectData.PosX, objectData.PosY);
				placedObjectScript.CurrentRotationDegrees = objectData.RotationDegrees;
				placedObjectScript.CurrentScale = new Vector2(objectData.ScaleX, objectData.ScaleY);
				objectsRoot.AddChild(placedObjectScript);
				restoredCount++;
			} else {
				GD.PrintErr($"DeleteMultipleObjectsAction Undo: Failed to instance or cast to PlacedObject from '{this.placedObjectScenePath}'.");
				newInstance.QueueFree();
			}
		}
		if (restoredCount > 0) GD.Print($"DeleteMultipleObjectsAction Undo: Restored {restoredCount} objects.");
	}

	private PlacedObject FindObjectInScene(Node objectsRoot, MapSaverLoader.PlacedObjectSaveData searchData)
	{
		if (objectsRoot == null || searchData == null || !GodotObject.IsInstanceValid(objectsRoot)) return null;

		foreach (Node child in objectsRoot.GetChildren())
		{
			if (child is PlacedObject po)
			{
				// A more robust method would involve unique IDs per PlacedObject instance.
				// Matching by all properties is a heuristic and might not be perfectly unique in all edge cases.
				if (po.AssetNameRef == searchData.AssetNameRef &&
					po.MapLayerIndex == searchData.MapLayerIndex &&
					po.GlobalPosition.IsEqualApprox(new Vector2(searchData.PosX, searchData.PosY)) &&
					Mathf.IsEqualApprox(po.CurrentRotationDegrees, searchData.RotationDegrees) &&
					po.CurrentScale.IsEqualApprox(new Vector2(searchData.ScaleX, searchData.ScaleY)))
				{
					return po;
				}
			}
		}
		return null;
	}
}
