using Godot;
using System.Collections.Generic;

public partial class ScaleGizmo : Node2D
{
	public PlacedObject CurrentTargetObject { get; private set; }

	public enum HandleType { TopLeft, TopRight, BottomLeft, BottomRight, Left, Right, Top, Bottom }
	private Dictionary<HandleType, Area2D> handles = new Dictionary<HandleType, Area2D>();
	// private Dictionary<HandleType, Sprite2D> handleSprites = new Dictionary<HandleType, Sprite2D>(); // Not strictly needed if not accessing sprites individually later
	private Texture2D handleTexturePlaceholder = null;
	private const string HandleIconPath = "res://assets/icons/scale_gizmo_handle.png";

	// Signals for drag interaction
	[Signal] public delegate void ScaleStartedEventHandler(Vector2 initialScale, HandleType handleType); // Added HandleType
	[Signal] public delegate void ScaleUpdatedEventHandler(Vector2 newScale);
	[Signal] public delegate void ScaleFinishedEventHandler(Vector2 finalScale, Vector2 originalScaleOnDragStart);

	// Drag state variables
	private bool isDragging = false;
	private HandleType currentDragHandleType;
	private Vector2 objectOriginalScaleOnDragStart;
	private Vector2 objectCenterGlobalOnDragStart; // Pivot of the object in global space
	private float initialDistanceMouseToCenter;    // For uniform corner scaling


	public override void _Ready()
	{
		if (ResourceLoader.Exists(HandleIconPath)) {
			handleTexturePlaceholder = ResourceLoader.Load<Texture2D>(HandleIconPath);
		} else {
			GD.Print($"ScaleGizmo: Handle icon not found at {HandleIconPath}. Creating placeholder.");
			Image img = Image.Create(10, 10, false, Image.Format.Rgba8);
			img.Fill(new Color(0.9f, 0.9f, 0.1f, 0.8f));
			img.DrawRect(new Rect2I(0,0,10,10), Colors.DarkSlateGray, false, 1);
			handleTexturePlaceholder = ImageTexture.CreateFromImage(img);
		}

		SetupHandle(HandleType.TopLeft, "TopLeftHandleArea");
		SetupHandle(HandleType.TopRight, "TopRightHandleArea");
		SetupHandle(HandleType.BottomLeft, "BottomLeftHandleArea");
		SetupHandle(HandleType.BottomRight, "BottomRightHandleArea");
	}

	private void SetupHandle(HandleType type, string areaNodeName)
	{
		Area2D area = GetNodeOrNull<Area2D>(areaNodeName);
		if (area == null) { GD.PrintErr($"ScaleGizmo: Could not find handle Area2D: {areaNodeName}"); return; }
		handles[type] = area;

		Sprite2D sprite = area.GetNodeOrNull<Sprite2D>("HandleSprite");
		if (sprite != null) {
			// handleSprites[type] = sprite; // Store if needed
			if (sprite.Texture == null && handleTexturePlaceholder != null) {
				sprite.Texture = handleTexturePlaceholder;
			}
			sprite.Centered = true;
		} else { GD.PrintErr($"ScaleGizmo: Could not find HandleSprite in {areaNodeName}"); }

		CollisionShape2D collision = area.GetNodeOrNull<CollisionShape2D>("HandleCollision");
		if (collision != null && sprite != null && sprite.Texture != null) {
			CircleShape2D shape = new CircleShape2D();
			Rect2 spriteRect = sprite.GetRect();
			shape.Radius = (Mathf.Max(spriteRect.Size.X * sprite.Scale.X, spriteRect.Size.Y * sprite.Scale.Y) / 2.0f) * 1.4f;
			collision.Shape = shape;
			collision.Position = sprite.Position;
		} else { if(collision == null) GD.PrintErr($"ScaleGizmo: Could not find HandleCollision in {areaNodeName}");}

		area.CollisionLayer = 1u << 2;
		area.CollisionMask = 0;

		// Ensure not connecting multiple times if _Ready is somehow called again (unlikely for node in scene)
		if (!area.IsConnected(Area2D.SignalName.InputEvent, Callable.From((Node vp, InputEvent ev, int shapeIdx) => _OnHandleInputEvent(vp, ev, shapeIdx, type))))
		{
			area.InputEvent += (Node vp, InputEvent ev, int shapeIdx) => _OnHandleInputEvent(vp, ev, shapeIdx, type);
		}
	}

	public void UpdateGizmoHandlesLocation(PlacedObject target)
	{
		if (target == null || !GodotObject.IsInstanceValid(target)) return;
		Sprite2D targetSprite = target.GetNodeOrNull<Sprite2D>("ObjectSprite");
		if (targetSprite == null) return;

		Rect2 localRect = targetSprite.GetRect();
		Vector2 currentTargetScale = target.CurrentScale;
		float halfWidthScaled = (localRect.Size.X / 2.0f) * currentTargetScale.X;
		float halfHeightScaled = (localRect.Size.Y / 2.0f) * currentTargetScale.Y;

		if (handles.TryGetValue(HandleType.TopLeft,     out var tlArea)) tlArea.Position = new Vector2(-halfWidthScaled, -halfHeightScaled);
		if (handles.TryGetValue(HandleType.TopRight,    out var trArea)) trArea.Position = new Vector2( halfWidthScaled, -halfHeightScaled);
		if (handles.TryGetValue(HandleType.BottomLeft,  out var blArea)) blArea.Position = new Vector2(-halfWidthScaled,  halfHeightScaled);
		if (handles.TryGetValue(HandleType.BottomRight, out var brArea)) brArea.Position = new Vector2( halfWidthScaled,  halfHeightScaled);
	}

	public void ShowForTarget(PlacedObject targetObject)
	{
		if (targetObject == null || !GodotObject.IsInstanceValid(targetObject)) { Hide(); return; }
		this.CurrentTargetObject = targetObject;
		this.GlobalPosition = targetObject.GlobalPosition;
		this.RotationDegrees = targetObject.CurrentRotationDegrees;
		this.Scale = Vector2.One;
		UpdateGizmoHandlesLocation(targetObject);
		this.Visible = true;
	}

	public void Hide()
	{
		this.Visible = false;
		if (isDragging) isDragging = false;
		this.CurrentTargetObject = null;
	}

	private void _OnHandleInputEvent(Node viewport, InputEvent @event, int shapeIdx, HandleType handleType)
	{
		if (CurrentTargetObject == null || !GodotObject.IsInstanceValid(CurrentTargetObject) || !Visible) {
			isDragging = false; return;
		}

		if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseButtonEvent.Pressed)
			{
				isDragging = true;
				currentDragHandleType = handleType;
				objectOriginalScaleOnDragStart = CurrentTargetObject.CurrentScale;
				objectCenterGlobalOnDragStart = CurrentTargetObject.GlobalPosition;

				Vector2 mouseGlobalPos = mouseButtonEvent.GlobalPosition; // Use event's position for accuracy
				initialDistanceMouseToCenter = (mouseGlobalPos - objectCenterGlobalOnDragStart).Length();

				if (initialDistanceMouseToCenter < 1.0f) initialDistanceMouseToCenter = 1.0f;

				EmitSignal(SignalName.ScaleStarted, objectOriginalScaleOnDragStart, (long)handleType); // Cast enum for signal
				GetViewport().SetInputAsHandled();
				// GD.Print($"ScaleGizmo: Drag Start. Handle: {handleType}, Initial Scale: {objectOriginalScaleOnDragStart}, Initial Dist: {initialDistanceMouseToCenter}");
			}
			else // Mouse button released
			{
				if (isDragging)
				{
					isDragging = false;
					EmitSignal(SignalName.ScaleFinished, CurrentTargetObject.CurrentScale, objectOriginalScaleOnDragStart);
					GetViewport().SetInputAsHandled();
					// GD.Print($"ScaleGizmo: Drag Finished. Final Scale: {CurrentTargetObject.CurrentScale}");
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		if (!isDragging || CurrentTargetObject == null || !GodotObject.IsInstanceValid(CurrentTargetObject)) return;

		Vector2 currentMouseGlobalPos = GetGlobalMousePosition();
		float currentDistanceMouseToCenter = (currentMouseGlobalPos - objectCenterGlobalOnDragStart).Length();

		if (initialDistanceMouseToCenter < 0.001f) { isDragging = false; return; }

		float scaleFactor = 1.0f;
		if (initialDistanceMouseToCenter > 0.001f) {
			 scaleFactor = currentDistanceMouseToCenter / initialDistanceMouseToCenter;
		}

		if (scaleFactor < 0.01f) scaleFactor = 0.01f;

		Vector2 newUniformScale = objectOriginalScaleOnDragStart * scaleFactor;

		CurrentTargetObject.CurrentScale = newUniformScale;

		this.RotationDegrees = CurrentTargetObject.CurrentRotationDegrees; // Ensure gizmo rotation matches object
		UpdateGizmoHandlesLocation(CurrentTargetObject); // Reposition handles based on new scale

		EmitSignal(SignalName.ScaleUpdated, CurrentTargetObject.CurrentScale);
	}

	public bool IsDragging() => isDragging;
}
