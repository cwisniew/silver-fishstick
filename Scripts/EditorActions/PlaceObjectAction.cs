using Godot;

// Assuming AssetData, AssetManager, PlacedObject, EditorAction are accessible.
// Ensure necessary using directives if these are in different namespaces.
// e.g. using YourProject.Scripts.Core; for AssetManager, AssetData
// e.g. using YourProject.Scripts.Objects; for PlacedObject script

public class PlaceObjectAction : EditorAction
{
	private string placedObjectScenePath;
	private string assetNameForObject;
	private Vector2 globalPosition;
	private int mapLayerIndex;
	private float initialRotationDegrees; // Added
	// private Vector2 initialScale;    // Future extension

	private Node instancedObjectNode;
	private AssetManager assetManagerInstance;
	private Node2D placedObjectsRootNode;

	public PlaceObjectAction(string scenePath, string assetName, Vector2 position, int layerIdx,
							 float rotation, // New parameter
							 AssetManager manager, Node2D objectsRoot)
	{
		this.placedObjectScenePath = scenePath;
		this.assetNameForObject = assetName;
		this.globalPosition = position;
		this.mapLayerIndex = layerIdx;
		this.initialRotationDegrees = rotation; // Store rotation
		this.assetManagerInstance = manager;
		this.placedObjectsRootNode = objectsRoot;
		// Description = $"Place Object '{assetName}' at {position}, Rot: {rotation}°, Layer: {layerIdx}";
	}

	public override void Execute(TileMap tileMap)
	{
		if (assetManagerInstance == null) {
			GD.PrintErr("PlaceObjectAction Execute: AssetManager instance is null.");
			return;
		}
		if (placedObjectsRootNode == null) {
			 GD.PrintErr("PlaceObjectAction Execute: PlacedObjectsRootNode is null. Cannot find parent for object.");
			 return;
		}
		if (!GodotObject.IsInstanceValid(placedObjectsRootNode)) {
			GD.PrintErr("PlaceObjectAction Execute: PlacedObjectsRootNode is not valid (e.g. freed).");
			return;
		}

		AssetData assetData = assetManagerInstance.GetAsset(assetNameForObject); // Use GetAsset
		if (assetData == null || assetData.Category != AssetManager.AssetCategory.PlaceableObject)
		{
			GD.PrintErr($"PlaceObjectAction Execute: AssetData '{assetNameForObject}' not found or not a PlaceableObject.");
			return;
		}

		PackedScene scene = ResourceLoader.Load<PackedScene>(placedObjectScenePath);
		if (scene == null)
		{
			GD.PrintErr($"PlaceObjectAction Execute: Failed to load PackedScene from '{placedObjectScenePath}'.");
			return;
		}

		// If redoing and object already exists (e.g. from a previous execute not properly undone), remove old one first
		if (instancedObjectNode != null && GodotObject.IsInstanceValid(instancedObjectNode))
		{
			GD.PrintWarn("PlaceObjectAction Execute: instancedObjectNode already exists. Removing previous before re-instancing for Redo.");
			instancedObjectNode.GetParent()?.RemoveChild(instancedObjectNode);
			instancedObjectNode.QueueFree();
			instancedObjectNode = null;
		}

		instancedObjectNode = scene.Instantiate();
		if (instancedObjectNode is PlacedObject placedObjectScript)
		{
			placedObjectScript.Initialize(assetData, mapLayerIndex); // Sets default rotation/scale from PlacedObject.cs
			placedObjectScript.GlobalPosition = globalPosition;
			placedObjectScript.CurrentRotationDegrees = this.initialRotationDegrees; // Apply specific rotation
			// placedObjectScript.CurrentScale = this.initialScale; // If scale was also passed
		}
		else
		{
			 GD.PrintErr($"PlaceObjectAction Execute: Instanced scene root from '{placedObjectScenePath}' is not a PlacedObject script or cannot be cast.");
			 instancedObjectNode.QueueFree();
			 instancedObjectNode = null;
			 return;
		}

		placedObjectsRootNode.AddChild(instancedObjectNode);
		// GD.Print($"PlaceObjectAction: Executed - Placed '{assetNameForObject}' (Instance ID: {instancedObjectNode.GetInstanceId()}) at {globalPosition}");
	}

	public override void Undo(TileMap tileMap)
	{
		if (instancedObjectNode != null && GodotObject.IsInstanceValid(instancedObjectNode))
		{
			// GD.Print($"PlaceObjectAction: Undoing - Removing '{assetNameForObject}' (Instance ID: {instancedObjectNode.GetInstanceId()})");
			// It's important that placedObjectsRootNode is still valid here too.
			// If placedObjectsRootNode could be deleted, this would fail.
			// For now, assume it's persistent.
			if (instancedObjectNode.GetParent() == placedObjectsRootNode) { // Check it's still parented correctly
				placedObjectsRootNode.RemoveChild(instancedObjectNode);
			} else if (instancedObjectNode.GetParent() != null) { // Parented elsewhere unexpectedly
				GD.PrintWarn("PlaceObjectAction Undo: Instanced object was reparented. Removing from current parent.");
				instancedObjectNode.GetParent().RemoveChild(instancedObjectNode);
			} else {
				// No parent, might already be removed from tree but not freed.
			}
			instancedObjectNode.QueueFree();
			// instancedObjectNode = null; // Keep reference for redo, Execute will handle if it exists
		}
		else
		{
			GD.PrintWarn("PlaceObjectAction Undo: instancedObjectNode is null or invalid. Already undone or error in Execute?");
		}
	}
}
