using Godot;

public partial class MapCameraController : Camera2D
{
	[Export(PropertyHint.Range, "0.01,0.5,0.01")] // Min, Max, Step for editor
	public float ZoomFactor { get; set; } = 0.1f; // Percentage change per scroll

	[Export(PropertyHint.Range, "0.1,10.0,0.1")]
	public float MinZoomLevel { get; set; } = 0.2f;

	[Export(PropertyHint.Range, "0.1,10.0,0.1")]
	public float MaxZoomLevel { get; set; } = 5.0f;

	[Export] public MouseButton PanButton { get; set; } = MouseButton.Middle;

	private bool isPanning = false;
	private Vector2 panStartPosition;       // Global mouse position when panning started
	private Vector2 cameraStartGlobalPosition; // Camera's global position when panning started


	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButtonEvent)
		{
			// Handle Pan Button Press/Release
			if (mouseButtonEvent.ButtonIndex == PanButton)
			{
				if (mouseButtonEvent.Pressed)
				{
					isPanning = true;
					panStartPosition = GetGlobalMousePosition();
					cameraStartGlobalPosition = this.GlobalPosition;
					GetViewport().SetInputAsHandled();
					// GD.Print("Panning started. Start Mouse: " + panStartPosition + ", Cam Start: " + cameraStartGlobalPosition);
				}
				else // Button released
				{
					isPanning = false;
					GetViewport().SetInputAsHandled();
					// GD.Print("Panning ended.");
				}
			}
			// Handle Zoom (Wheel events)
			// Ensure this doesn't conflict if PanButton is set to a wheel button (which is unlikely)
			else if (mouseButtonEvent.ButtonIndex == MouseButton.WheelUp)
			{
				// No need to check mouseButtonEvent.Pressed for wheel, it's a discrete event
				AdjustZoom(1.0f - ZoomFactor);
				GetViewport().SetInputAsHandled();
			}
			else if (mouseButtonEvent.ButtonIndex == MouseButton.WheelDown)
			{
				AdjustZoom(1.0f + ZoomFactor);
				GetViewport().SetInputAsHandled();
			}
		}
		else if (@event is InputEventMouseMotion mouseMotionEvent && isPanning)
		{
			// Calculate how much the mouse has moved from the pan start position
			Vector2 mouseDelta = GetGlobalMousePosition() - panStartPosition;

			// Adjust camera position. The delta is applied in the opposite direction
			// of mouse movement to make the world appear to move with the mouse.
			// Division by Zoom makes panning speed consistent across zoom levels.
			// Use Zoom.X assuming uniform zoom; if non-uniform zoom is possible, this might need thought.
			if (this.Zoom.X != 0) // Avoid division by zero if zoom is somehow zero
			{
				this.GlobalPosition = cameraStartGlobalPosition - mouseDelta / this.Zoom.X;
			}
			// GD.Print("Panning. Mouse Delta: " + mouseDelta + ", New Cam Pos: " + this.GlobalPosition);
			GetViewport().SetInputAsHandled();
		}
	}

	private void AdjustZoom(float factor)
	{
		Vector2 pointBeforeZoom = GetGlobalMousePosition(); // World position of the mouse before zoom

		Vector2 prevZoom = this.Zoom;
		Vector2 newZoom = prevZoom * factor;

		// Clamp zoom levels
		newZoom.X = Mathf.Clamp(newZoom.X, MinZoomLevel, MaxZoomLevel);
		newZoom.Y = Mathf.Clamp(newZoom.Y, MinZoomLevel, MaxZoomLevel);

		// If zoom hasn't actually changed (e.g. at min/max limit), no need to adjust position
		if (newZoom.IsEqualApprox(prevZoom))
		{
			// GD.Print("Zoom unchanged, skipping position adjustment.");
			return;
		}

		this.Zoom = newZoom; // Apply the new zoom

		// After zoom, the camera's view has changed. Get where the mouse points now if camera hadn't moved.
		// This requires temporarily making the camera current if it's not, or ensuring transformations are up to date.
		// However, GetGlobalMousePosition() should give the correct current world position under the mouse
		// with the new zoom level already applied to the camera's transform internally for this calculation.
		Vector2 pointAfterZoomNoOffset = GetGlobalMousePosition();

		// The goal is to move the camera such that `pointBeforeZoom` is now under the cursor again.
		// The difference `pointBeforeZoom - pointAfterZoomNoOffset` is the world space vector by which
		// the point under the mouse shifted due to zoom. We need to shift the camera by this amount.
		Vector2 offset = pointBeforeZoom - pointAfterZoomNoOffset;
		this.GlobalPosition += offset;

		// GD.Print($"Zoom: Prev={prevZoom}, New={this.Zoom}. MouseWorldBefore: {pointBeforeZoom}, MouseWorldAfterNoOffset: {pointAfterZoomNoOffset}, OffsetApplied: {offset}, NewCamPos: {this.GlobalPosition}");
	}
}
