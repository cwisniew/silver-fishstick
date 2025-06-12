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

	// --- Snap Points Logic ---
	public struct GlobalSnapPoints
	{
		public Vector2 Center;
		public float LeftX;
		public float RightX;
		public float TopY;
		public float BottomY;
		public float HorizontalCenterY => Center.Y;
		public float VerticalCenterX => Center.X;
	}

	public GlobalSnapPoints GetCurrentGlobalSnapPoints()
	{
		if (objectSprite == null || objectSprite.Texture == null)
		{
			GD.PrintWarn($"PlacedObject '{AssetNameRef ?? Name ?? "Unnamed"}': Cannot get accurate snap points without sprite texture. Using GlobalPosition as center only.");
			return new GlobalSnapPoints
			{
				Center = this.GlobalPosition,
				LeftX = this.GlobalPosition.X,
				RightX = this.GlobalPosition.X,
				TopY = this.GlobalPosition.Y,
				BottomY = this.GlobalPosition.Y
			};
		}

		Vector2 textureSize = objectSprite.Texture.GetSize();
		// Sprite is centered, so its GlobalPosition is its pivot/center.
		float scaledHalfWidth = (textureSize.X * this.CurrentScale.X) / 2.0f;
		float scaledHalfHeight = (textureSize.Y * this.CurrentScale.Y) / 2.0f;

		Vector2 globalCenter = this.GlobalPosition;

		return new GlobalSnapPoints
		{
			Center = globalCenter,
			LeftX = globalCenter.X - scaledHalfWidth,
			RightX = globalCenter.X + scaledHalfWidth,
			TopY = globalCenter.Y - scaledHalfHeight,
			BottomY = globalCenter.Y + scaledHalfHeight
		};
	}

	public Vector2[] GetGlobalRotatedCorners()
	{
		if (objectSprite == null)
		{
			GD.PrintErr($"PlacedObject '{AssetNameRef ?? Name ?? "Unnamed"}': ObjectSprite node not found. Cannot get corners.");
			return new Vector2[] { GlobalPosition, GlobalPosition, GlobalPosition, GlobalPosition };
		}
		if (objectSprite.Texture == null)
		{
			GD.PrintWarn($"PlacedObject '{AssetNameRef ?? Name ?? "Unnamed"}': ObjectSprite has no texture. Using GlobalPosition for corners.");
			return new Vector2[] { GlobalPosition, GlobalPosition, GlobalPosition, GlobalPosition };
		}

		// Assuming ObjectSprite has Centered = true and Offset = (0,0)
		Rect2 localSpriteRect = objectSprite.GetRect();

		Vector2[] localCorners = new Vector2[4];
		localCorners[0] = localSpriteRect.Position; // Top-Left
		localCorners[1] = new Vector2(localSpriteRect.Position.X + localSpriteRect.Size.X, localSpriteRect.Position.Y); // Top-Right
		localCorners[2] = localSpriteRect.Position + localSpriteRect.Size; // Bottom-Right
		localCorners[3] = new Vector2(localSpriteRect.Position.X, localSpriteRect.Position.Y + localSpriteRect.Size.Y); // Bottom-Left

		Vector2[] globalCorners = new Vector2[4];
		Transform2D globalTransform = this.GlobalTransform;

		for (int i = 0; i < 4; i++)
		{
			// GlobalTransform already includes this Node2D's scale (this.CurrentScale) and rotation.
			// localCorners are relative to the sprite's origin (center).
			globalCorners[i] = globalTransform * localCorners[i];
		}
		return globalCorners;
	}
}
