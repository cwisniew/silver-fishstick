using Godot;

public partial class PlacedObject : Node2D
{
	// Properties to be saved and loaded, and used for identification/rendering
	public string AssetNameRef { get; set; }
	public int MapLayerIndex { get; set; }

	private Sprite2D objectSprite;
	private CollisionShape2D objectCollisionShape;

	private float currentRotationDegrees = 0.0f;
	public float CurrentRotationDegrees
	{
		get => currentRotationDegrees;
		set
		{
			currentRotationDegrees = value;
			this.RotationDegrees = value; // Directly update the Node2D's rotation
		}
	}

	private Vector2 currentScale = Vector2.One;
	public Vector2 CurrentScale
	{
		get => currentScale;
		set
		{
			// Ensure scale is not zero to avoid issues (invisibility or errors)
			Vector2 newScale = value;
			if (Mathf.IsZeroApprox(newScale.X)) newScale.X = 0.001f;
			if (Mathf.IsZeroApprox(newScale.Y)) newScale.Y = 0.001f;

			currentScale = newScale;
			this.Scale = currentScale; // Directly update the Node2D's scale
		}
	}

	public override void _Ready()
	{
		objectSprite = GetNode<Sprite2D>("ObjectSprite");
		if (objectSprite == null)
		{
			GD.PrintErr($"PlacedObject '{this.Name ?? "Unnamed"}': Could not find ObjectSprite child node!");
		}

		objectCollisionShape = GetNode<CollisionShape2D>("ObjectClickArea/ObjectCollisionShape");
		if (objectCollisionShape == null)
		{
			GD.PrintErr($"PlacedObject '{this.Name ?? "Unnamed"}': Could not find ObjectCollisionShape child!");
		}

		// Set collision layer for the Area2D for picking
		Area2D objectClickArea = GetNode<Area2D>("ObjectClickArea");
		if (objectClickArea != null)
		{
			objectClickArea.CollisionLayer = 1; // Assuming physics layer 1 (bit 0) for interactable objects
			objectClickArea.CollisionMask = 0;  // Does not need to detect other areas/bodies for clicking itself
		}
		else
		{
			GD.PrintErr($"PlacedObject '{this.Name ?? "Unnamed"}': Could not find ObjectClickArea node to set collision layer!");
		}
	}

	public void Initialize(AssetData assetData, int mapLayerIndex)
	{
		if (assetData == null)
		{
			GD.PrintErr("PlacedObject.Initialize: assetData is null!");
			QueueFree();
			return;
		}

		// Ensure objectSprite is valid. _Ready should have run if instanced correctly.
		if (objectSprite == null) // This check might be redundant if _Ready always precedes Initialize.
		{
			objectSprite = GetNode<Sprite2D>("ObjectSprite"); // Try to get it again
			if (objectSprite == null) {
				GD.PrintErr("PlacedObject.Initialize: ObjectSprite is still null after _Ready attempt! Cannot set texture. Freeing instance.");
				QueueFree();
				return;
			}
		}
		if (objectCollisionShape == null) // Also check collision shape
		{
			objectCollisionShape = GetNode<CollisionShape2D>("ObjectClickArea/ObjectCollisionShape");
			if (objectCollisionShape == null) {
				GD.PrintErr("PlacedObject.Initialize: ObjectCollisionShape is still null! Cannot set shape. May affect picking.");
				// Not freeing for this, as object might still be visually usable.
			}
		}


		this.Name = assetData.Name;
		this.AssetNameRef = assetData.Name;
		this.MapLayerIndex = mapLayerIndex;

		// Initialize transform properties
		this.CurrentRotationDegrees = 0.0f;
		this.CurrentScale = Vector2.One;

		if (assetData.PreviewTexture != null)
		{
			objectSprite.Texture = assetData.PreviewTexture;

			if (objectCollisionShape != null)
			{
				RectangleShape2D shape = new RectangleShape2D();
				shape.Size = objectSprite.GetRect().Size;
				objectCollisionShape.Shape = shape;
			}
		}
		else
		{
			GD.PrintErr($"PlacedObject.Initialize: AssetData '{assetData.Name}' has no PreviewTexture. Object will be invisible or use default sprite icon, no collision shape set.");
		}

		// The object's actual visual layer (CanvasItem.Layer) or ZIndex can be set here
		// based on MapLayerIndex or other properties if needed for rendering order.
		// For now, default Node2D ZIndex (0) and Layer (0) are used.
		// Example: this.ZIndex = mapLayerIndex * 10; // Simple way to separate layers visually by ZIndex
	}

	// Future methods for selection, saving state, etc. can be added here.
	// public Godot.Collections.Dictionary<string, Variant> GetSaveData()
	// {
	//     return new Godot.Collections.Dictionary<string, Variant>
	//     {
	//         { "AssetNameRef", AssetNameRef },
	//         { "MapLayerIndex", MapLayerIndex },
	//         { "PositionX", Position.X },
	//         { "PositionY", Position.Y },
	//         { "Rotation", RotationDegrees },
	//         { "ScaleX", Scale.X },
	//         { "ScaleY", Scale.Y }
	//         // Add other custom properties if any
	//     };
	// }

	// public void LoadState(Godot.Collections.Dictionary<string, Variant> data)
	// {
	//     AssetNameRef = data["AssetNameRef"].AsString();
	//     MapLayerIndex = data["MapLayerIndex"].AsInt32();
	//     Position = new Vector2(data["PositionX"].AsSingle(), data["PositionY"].AsSingle());
	//     RotationDegrees = data["Rotation"].AsSingle();
	//     Scale = new Vector2(data["ScaleX"].AsSingle(), data["ScaleY"].AsSingle());
	//     // Load other custom properties
	//     // After loading, may need to re-fetch texture from AssetManager if not saved directly
	// }
}
