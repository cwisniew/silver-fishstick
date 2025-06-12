using Godot;

public partial class GridDisplay : Node2D
{
    private GlobalSettings globalSettings;
    private Camera2D mapCamera;

    private Color gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Adjusted alpha for subtlety
    // private Color subGridColor = new Color(0.5f, 0.5f, 0.5f, 0.05f); // For future use
    // private int subDivisions = 0;

    private Vector2 lastCameraPosition = Vector2.Inf; // Initialize to force first draw
    private Vector2 lastCameraZoom = Vector2.Zero;    // Initialize to force first draw

    public override void _Ready()
    {
        globalSettings = GetNode<GlobalSettings>("/root/GlobalSettings");
        // Path to camera might vary based on scene structure.
        // Assuming MainScene is root, and MapCamera is a direct child.
        mapCamera = GetTree().Root.GetNode<Camera2D>("MainScene/MapCamera");

        if (globalSettings == null)
        {
            GD.PrintErr("GridDisplay: GlobalSettings not found! Grid will not function.");
            SetProcess(false); // Disable processing if settings are missing
            return;
        }
        if (mapCamera == null)
        {
            GD.PrintErr("GridDisplay: MapCamera not found at 'MainScene/MapCamera'! Grid will not function correctly.");
            // It might still draw based on default viewport if mapCamera is null, but won't follow game camera.
            // Depending on desired behavior, could disable processing or try a fallback.
        }

        globalSettings.GridSettingsChanged += OnGridSettingsChanged;
        this.Visible = globalSettings.IsSnapToGridEnabled;

        // Ensure ZIndex is low so it draws behind everything. Can also be set in scene editor.
        this.ZIndex = -10;
        this.ZAsRelative = false; // Ensure ZIndex is absolute
    }

    public override void _ExitTree()
    {
        if (globalSettings != null)
        {
            globalSettings.GridSettingsChanged -= OnGridSettingsChanged;
        }
    }

    private void OnGridSettingsChanged(Vector2I newGridSize, bool newSnapEnabled)
    {
        this.Visible = newSnapEnabled;
        if (this.Visible) // Only queue redraw if it's becoming visible or settings changed while visible
        {
          QueueRedraw();
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible || mapCamera == null) return;

        // Check if camera has moved or zoomed enough to warrant a redraw
        // Using a small epsilon for float comparisons might be good for zoom.
        if (mapCamera.GlobalPosition.DistanceSquaredTo(lastCameraPosition) > 1.0f || // Redraw if moved more than 1 pixel
            !mapCamera.Zoom.IsEqualApprox(lastCameraZoom))
        {
            QueueRedraw();
            lastCameraPosition = mapCamera.GlobalPosition;
            lastCameraZoom = mapCamera.Zoom;
        }
    }

    public override void _Draw()
    {
        if (mapCamera == null || globalSettings == null || !globalSettings.IsSnapToGridEnabled || !this.Visible)
        {
            return;
        }

        Vector2I gridSize = globalSettings.GridSize;
        if (gridSize.X <= 0 || gridSize.Y <= 0) return;

        // Get the visible rectangle in global world coordinates
        // GetViewportRect gives screen coordinates, so transform by camera's global transform inverse
        Rect2 viewportRectOnScreen = GetViewport().GetVisibleRect(); // This is the actual screen viewport
        Transform2D camCanvasTransform = mapCamera.GetCanvasTransform(); // Transform from canvas to screen
        Transform2D screenToCanvasTransform = camCanvasTransform.AffineInverse();
        Transform2D camGlobalTransform = mapCamera.GlobalTransform; // Transform from camera local to global world

        // Transform viewport from screen coordinates to global world coordinates
        Rect2 visibleWorldRect = (camGlobalTransform * screenToCanvasTransform) * viewportRectOnScreen;

        // Alternative, potentially simpler way if camera is current:
        // visibleWorldRect = mapCamera.GetViewportRect(); // This is in camera's local space if camera is current
        // visibleWorldRect.Position = mapCamera.GlobalPosition - visibleWorldRect.Size / 2.0f / mapCamera.Zoom; // Approximate center
        // visibleWorldRect.Size /= mapCamera.Zoom; // Adjust size by zoom

        // The method used in the prompt is generally more robust:
        // Rect2 viewportRectScreen = GetViewport().GetVisibleRect(); // Screen space
        // Rect2 visibleWorldRect = mapCamera.GetTransform().AffineInverse() * viewportRectScreen; // This is for canvas items.
        // For Node2D _Draw in global space, we need what part of the world the camera sees.
        // A common way:
        Rect2 camera_rect = mapCamera.GetViewportRect(); // This is screen rect
        Vector2 world_top_left = mapCamera.GetGlobalTransformWithCanvas().AffineInverse().Xform(camera_rect.Position);
        Vector2 world_bottom_right = mapCamera.GetGlobalTransformWithCanvas().AffineInverse().Xform(camera_rect.Position + camera_rect.Size);
        visibleWorldRect = new Rect2(world_top_left, world_bottom_right - world_top_left);


        float minX = visibleWorldRect.Position.X;
        float minY = visibleWorldRect.Position.Y;
        float maxX = visibleWorldRect.Position.X + visibleWorldRect.Size.X;
        float maxY = visibleWorldRect.Position.Y + visibleWorldRect.Size.Y;

        // Extend slightly beyond viewport and snap to grid
        float startX = Mathf.Floor(minX / gridSize.X) * gridSize.X - gridSize.X;
        float endX   = Mathf.Ceil(maxX / gridSize.X) * gridSize.X + gridSize.X;
        float startY = Mathf.Floor(minY / gridSize.Y) * gridSize.Y - gridSize.Y;
        float endY   = Mathf.Ceil(maxY / gridSize.Y) * gridSize.Y + gridSize.Y;

        float minLineSpacingPixels = 4.0f;
        float lineWidth = Mathf.Max(0.5f, 1.0f / mapCamera.Zoom.X); // Ensure line width is at least 0.5px screen space

        // Draw vertical lines
        if (gridSize.X * mapCamera.Zoom.X >= minLineSpacingPixels) {
            for (float x = startX; x <= endX; x += gridSize.X)
            {
                DrawLine(new Vector2(x, startY), new Vector2(x, endY), gridColor, lineWidth, true); // Antialiased true
            }
        }

        // Draw horizontal lines
        if (gridSize.Y * mapCamera.Zoom.Y >= minLineSpacingPixels) {
            for (float y = startY; y <= endY; y += gridSize.Y)
            {
                DrawLine(new Vector2(startX, y), new Vector2(endX, y), gridColor, lineWidth, true); // Antialiased true
            }
        }
    }
}
