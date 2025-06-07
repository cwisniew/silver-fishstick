using Godot;

public partial class RotationGizmo : Node2D
{
	[Signal] public delegate void RotationStartedEventHandler(float initialRotationDegrees);
	[Signal] public delegate void RotationUpdatedEventHandler(float newRotationDegrees);
	[Signal] public delegate void RotationFinishedEventHandler(float finalRotationDegrees, float originalRotationDegrees);

	private Sprite2D gizmoHandleSprite;
	private Area2D handleArea;
	private CollisionShape2D handleCollisionShape;

	private bool isDragging = false;
	private float angleAtDragStartRad; // Angle of vector (mouse - gizmo_center) at drag start
	private float objectInitialRotationOnDragStartRad; // Object's rotation (in radians) when drag began

	private PlacedObject currentTargetObject = null;

	public override void _Ready()
	{
		gizmoHandleSprite = GetNode<Sprite2D>("GizmoHandleSprite");
		handleArea = GetNode<Area2D>("HandleArea"); // Assuming HandleArea is direct child
		handleCollisionShape = handleArea?.GetNode<CollisionShape2D>("HandleCollisionShape");

		if (gizmoHandleSprite == null) GD.PrintErr("RotationGizmo: GizmoHandleSprite node not found!");
		if (handleArea == null) GD.PrintErr("RotationGizmo: HandleArea node not found!");
		if (handleCollisionShape == null) GD.PrintErr("RotationGizmo: HandleCollisionShape node not found!");

		if (gizmoHandleSprite != null && gizmoHandleSprite.Texture == null)
		{
			string iconPath = "res://assets/icons/rotate_gizmo_handle.png";
			if (ResourceLoader.Exists(iconPath)) {
				gizmoHandleSprite.Texture = ResourceLoader.Load<Texture2D>(iconPath);
			} else {
				GD.Print($"RotationGizmo: Texture not found at {iconPath}. Creating placeholder.");
				Image img = Image.Create(24, 24, false, Image.Format.Rgba8);
				img.Fill(new Color(0,0,0,0));
				Vector2 center = new Vector2(11.5f, 11.5f);
				float radius = 10f;
				Color gizmoColor = new Color(0.2f, 0.8f, 0.8f, 0.9f);
				for (int y = 0; y < 24; y++) {
					for (int x = 0; x < 24; x++) {
						float distSq = (x - center.X) * (x - center.X) + (y - center.Y) * (y - center.Y);
						if (distSq >= (radius - 2f) * (radius - 2f) && distSq <= radius * radius) {
							img.SetPixel(x, y, gizmoColor);
						}
					}
				}
				img.DrawLine(new Vector2I(12, 12), new Vector2I(22, 12), gizmoColor, 2);
				gizmoHandleSprite.Texture = ImageTexture.CreateFromImage(img);
			}
		}
		if (gizmoHandleSprite != null && !gizmoHandleSprite.Centered) gizmoHandleSprite.Centered = true;

		if (gizmoHandleSprite != null && gizmoHandleSprite.Texture != null && handleCollisionShape != null && handleCollisionShape.Shape == null)
		{
			CircleShape2D shape = new CircleShape2D();
			Rect2 spriteRect = gizmoHandleSprite.GetRect();
			shape.Radius = Mathf.Max(spriteRect.Size.X, spriteRect.Size.Y) / 2.0f * gizmoHandleSprite.Scale.X;
			handleCollisionShape.Shape = shape;
			handleCollisionShape.Position = gizmoHandleSprite.Position;
		}

		if (handleArea != null) {
			handleArea.InputEvent += _OnHandleAreaInputEvent;
			handleArea.CollisionLayer = 1u << 1; // Gizmo on physics layer 2 (bit 1)
			handleArea.CollisionMask = 0;
		}
	}

	public void SetTargetObject(PlacedObject target)
	{
		this.currentTargetObject = target;
		// UpdateGizmoVisuals potentially called by MainScene after this
	}

	public void UpdateGizmoVisuals(PlacedObject selectedObject)
	{
		SetTargetObject(selectedObject);

		if (selectedObject != null && GodotObject.IsInstanceValid(selectedObject))
		{
			// Position the gizmo root at the object's global position
			this.GlobalPosition = selectedObject.GlobalPosition;
			// The gizmo's handle sprite might have an offset, but the gizmo root itself doesn't need initial rotation.
			// Its rotation will be set by the drag or by matching object if needed for display.
			// For a rotation gizmo, its own rotation usually matches the target's.
			this.RotationDegrees = selectedObject.CurrentRotationDegrees;
			this.Visible = true;
		}
		else
		{
			this.Visible = false;
			if(isDragging) {
				isDragging = false; // Stop drag if object becomes invalid/deselected
				// Optionally emit RotationFinished if a drag was interrupted
			}
		}
	}

	private void _OnHandleAreaInputEvent(Node viewport, InputEvent @event, int shapeIdx)
	{
		if (currentTargetObject == null || !GodotObject.IsInstanceValid(currentTargetObject) || !Visible) return;

		if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseButtonEvent.Pressed)
			{
				isDragging = true;
				Vector2 gizmoCenterGlobal = this.GlobalPosition;
				// Vector from gizmo center to mouse, in global space, then get its angle.
				angleAtDragStartRad = (GetGlobalMousePosition() - gizmoCenterGlobal).Angle();
				objectInitialRotationOnDragStartRad = currentTargetObject.Rotation; // Store in radians for consistency

				EmitSignal(SignalName.RotationStarted, Mathf.RadToDeg(objectInitialRotationOnDragStartRad));
				GetViewport().SetInputAsHandled();
			}
			else // Mouse button released
			{
				if (isDragging)
				{
					isDragging = false;
					// currentTargetObject.CurrentRotationDegrees should already be the final value from _Process
					EmitSignal(SignalName.RotationFinished, currentTargetObject.CurrentRotationDegrees, Mathf.RadToDeg(objectInitialRotationOnDragStartRad));
					GetViewport().SetInputAsHandled();
				}
			}
		}
	}

	public override void _Process(double delta)
	{
		if (isDragging && currentTargetObject != null && GodotObject.IsInstanceValid(currentTargetObject))
		{
			Vector2 gizmoCenterGlobal = this.GlobalPosition;
			Vector2 currentMouseDirectionGlobal = GetGlobalMousePosition() - gizmoCenterGlobal;

			float currentMouseAngleRad = currentMouseDirectionGlobal.Angle();
			float rotationDeltaRad = currentMouseAngleRad - angleAtDragStartRad;
			float newObjectRotationRad = objectInitialRotationOnDragStartRad + rotationDeltaRad;

			float newObjectRotationDeg = Mathf.RadToDeg(newObjectRotationRad);
			// Normalize degrees if you prefer (e.g., to -180 to 180 or 0 to 360)
			// newObjectRotationDeg = Mathf.Wrap(newObjectRotationDeg, -180f, 180f);

			currentTargetObject.CurrentRotationDegrees = newObjectRotationDeg;
			this.RotationDegrees = newObjectRotationDeg; // Make gizmo visual rotate with object

			EmitSignal(SignalName.RotationUpdated, newObjectRotationDeg);
		}
	}
}
