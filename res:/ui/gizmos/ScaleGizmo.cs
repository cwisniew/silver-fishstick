using Godot;
using System.Collections.Generic;

public partial class ScaleGizmo : Node2D
{
    public enum HandleType
    {
        TopLeft, TopRight, BottomLeft, BottomRight,
        Left, Right, Top, Bottom // New side handles
    }

    [Signal] public delegate void ScaleStartedEventHandler(Vector2 originalScale, HandleType handleType);
    [Signal] public delegate void ScaleUpdatedEventHandler(Vector2 newScale);
    [Signal] public delegate void ScaleFinishedEventHandler(Vector2 finalScale, Vector2 originalScale);

    private Dictionary<HandleType, Area2D> handles = new Dictionary<HandleType, Area2D>();
    private PlacedObject activeTarget = null;
    private bool isDragging = false;
    private Vector2 dragStartMousePosition; // Global mouse position when drag started
    private Vector2 originalScaleOnDragStart;
    private HandleType activeHandleType;
    private Line2D selectionBox;

    // New members for Shift-key uniform scaling
    private Vector2 objectOriginalSpriteSize;
    private Vector2 objectCenterGlobalOnDragStart;
    private float initialDistanceMouseToCenter;

    private Texture2D gizmoHandleTexture;
    private Vector2 handleSize = new Vector2(10, 10); // Default size, can be adjusted

    public override void _Ready()
    {
        gizmoHandleTexture = ResourceLoader.Load<Texture2D>("res://assets/editor/gizmo_handle.png"); // Ensure this path is correct
        if (gizmoHandleTexture == null) GD.PrintErr("ScaleGizmo: Failed to load gizmo_handle.png");

        selectionBox = GetNode<Line2D>("SelectionBox");

        SetupHandle(HandleType.TopLeft, "TopLeftHandleArea");
        SetupHandle(HandleType.TopRight, "TopRightHandleArea");
        SetupHandle(HandleType.BottomLeft, "BottomLeftHandleArea");
        SetupHandle(HandleType.BottomRight, "BottomRightHandleArea");
        SetupHandle(HandleType.Left, "LeftHandleArea");
        SetupHandle(HandleType.Right, "RightHandleArea");
        SetupHandle(HandleType.Top, "TopHandleArea");
        SetupHandle(HandleType.Bottom, "BottomHandleArea");

        Hide(); // Initially hidden
    }

    private void SetupHandle(HandleType type, string areaNodeName)
    {
        Area2D area = GetNode<Area2D>(areaNodeName);
        if (area == null)
        {
            GD.PrintErr($"ScaleGizmo: Could not find handle Area2D: {areaNodeName}");
            return;
        }

        Sprite2D sprite = area.GetNode<Sprite2D>("HandleSprite");
        if (sprite == null)
        {
            GD.PrintErr($"ScaleGizmo: Could not find HandleSprite in {areaNodeName}");
            sprite = new Sprite2D { Centered = true }; // Create if missing, for robustness
            area.AddChild(sprite);
        }
        if (gizmoHandleTexture != null) sprite.Texture = gizmoHandleTexture;
        // Scale sprite to achieve desired handleSize if texture is not already that size
        if (gizmoHandleTexture != null && gizmoHandleTexture.GetSize() != Vector2.Zero && gizmoHandleTexture.GetSize() != handleSize) // Check for Zero size
        {
             sprite.Scale = handleSize / gizmoHandleTexture.GetSize();
        }


        CollisionShape2D collision = area.GetNode<CollisionShape2D>("HandleCollision");
        if (collision == null)
        {
             GD.PrintErr($"ScaleGizmo: Could not find HandleCollision in {areaNodeName}");
             collision = new CollisionShape2D();  // Create if missing
             area.AddChild(collision);
        }
        RectangleShape2D shape = new RectangleShape2D();
        shape.Size = handleSize * 1.5f; // Make collision shape slightly larger than visual
        collision.Shape = shape;

        area.InputEvent += (Node viewport, InputEvent ev, long shapeIdx) =>
            _OnHandleAreaInputEvent(ev, type, area); // Changed from lambda to pass area

        area.SetMeta("HandleType", (int)type);
        handles[type] = area;

        area.CollisionLayer = 1 << 2;
        area.CollisionMask = 0;
    }

    public void ShowForTarget(PlacedObject target)
    {
        activeTarget = target;
        if (activeTarget == null || !GodotObject.IsInstanceValid(activeTarget))
        {
            Hide();
            return;
        }
        UpdateGizmoHandlesLocation(activeTarget);
        GlobalPosition = activeTarget.GlobalPosition;
        RotationDegrees = activeTarget.CurrentRotationDegrees;
        Show();
    }

    public new void Hide()
    {
        activeTarget = null;
        isDragging = false;
        base.Hide();
    }

    public void UpdateGizmoHandlesLocation(PlacedObject targetObject)
    {
        if (targetObject == null || !GodotObject.IsInstanceValid(targetObject)) return;
        Sprite2D targetSprite = targetObject.GetNode<Sprite2D>("ObjectSprite");
        if (targetSprite == null || targetSprite.Texture == null) return;

        Vector2 textureSize = targetSprite.Texture.GetSize();
        float baseHalfWidth = textureSize.X / 2.0f;
        float baseHalfHeight = textureSize.Y / 2.0f;

        float scaledHalfWidth = baseHalfWidth * targetObject.CurrentScale.X;
        float scaledHalfHeight = baseHalfHeight * targetObject.CurrentScale.Y;

        if (handles.TryGetValue(HandleType.TopLeft,     out var tlArea)) tlArea.Position = new Vector2(-scaledHalfWidth, -scaledHalfHeight);
        if (handles.TryGetValue(HandleType.TopRight,    out var trArea)) trArea.Position = new Vector2( scaledHalfWidth, -scaledHalfHeight);
        if (handles.TryGetValue(HandleType.BottomLeft,  out var blArea)) blArea.Position = new Vector2(-scaledHalfWidth,  scaledHalfHeight);
        if (handles.TryGetValue(HandleType.BottomRight, out var brArea)) brArea.Position = new Vector2( scaledHalfWidth,  scaledHalfHeight);

        if (handles.TryGetValue(HandleType.Left,   out var lArea))  lArea.Position = new Vector2(-scaledHalfWidth, 0);
        if (handles.TryGetValue(HandleType.Right,  out var rArea))  rArea.Position = new Vector2( scaledHalfWidth, 0);
        if (handles.TryGetValue(HandleType.Top,    out var tArea))  tArea.Position = new Vector2(0, -scaledHalfHeight);
        if (handles.TryGetValue(HandleType.Bottom, out var bArea))  bArea.Position = new Vector2(0,  scaledHalfHeight);

        if (selectionBox != null)
        {
            selectionBox.ClearPoints();
            selectionBox.AddPoint(new Vector2(-scaledHalfWidth, -scaledHalfHeight));
            selectionBox.AddPoint(new Vector2( scaledHalfWidth, -scaledHalfHeight));
            selectionBox.AddPoint(new Vector2( scaledHalfWidth,  scaledHalfHeight));
            selectionBox.AddPoint(new Vector2(-scaledHalfWidth,  scaledHalfHeight));
            selectionBox.AddPoint(new Vector2(-scaledHalfWidth, -scaledHalfHeight));
        }
    }

    // Keep track of which area is currently hovered for _Process
    private Area2D _hoveredArea = null;

    private void _OnHandleAreaInputEvent(InputEvent @event, HandleType type, Area2D area)
    {
        if (activeTarget == null || !GodotObject.IsInstanceValid(activeTarget)) return;

        if (@event is InputEventMouseButton mouseButtonEvent)
        {
            if (mouseButtonEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseButtonEvent.Pressed)
                {
                    isDragging = true;
                    activeHandleType = type;
                    dragStartMousePosition = GetGlobalMousePosition(); // Global mouse pos
                    originalScaleOnDragStart = activeTarget.CurrentScale;

                    // Store additional info for scaling logic
                    Sprite2D targetSpriteNode = activeTarget.GetNodeOrNull<Sprite2D>("ObjectSprite");
                    if (targetSpriteNode != null && targetSpriteNode.Texture != null) {
                        objectOriginalSpriteSize = targetSpriteNode.Texture.GetSize();
                    } else {
                        objectOriginalSpriteSize = Vector2.One * 16; // Fallback
                        GD.PrintErr("ScaleGizmo: Could not get target sprite size. Using fallback.");
                    }
                    objectCenterGlobalOnDragStart = activeTarget.GlobalPosition; // Pivot is object's global position
                    initialDistanceMouseToCenter = (dragStartMousePosition - objectCenterGlobalOnDragStart).Length();

                    EmitSignal(SignalName.ScaleStarted, originalScaleOnDragStart, (long)activeHandleType);
                    GetViewport().SetInputAsHandled();
                }
                else if (isDragging)
                {
                    isDragging = false;
                    EmitSignal(SignalName.ScaleFinished, activeTarget.CurrentScale, originalScaleOnDragStart);
                    GetViewport().SetInputAsHandled();
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotionEvent && isDragging)
        {
            if (activeTarget == null) return;

            Vector2 currentGlobalMousePos = GetGlobalMousePosition();
            Vector2 newScale;
            bool uniformLock = Input.IsKeyPressed(Key.Shift);

            if (activeHandleType == HandleType.TopLeft || activeHandleType == HandleType.TopRight ||
                activeHandleType == HandleType.BottomLeft || activeHandleType == HandleType.BottomRight)
            {
                if (uniformLock) // SHIFT HELD: Uniform scaling for corners
                {
                    float currentDistanceMouseToCenter = (currentGlobalMousePos - objectCenterGlobalOnDragStart).Length();
                    float scaleRatio = 1.0f;
                    if (initialDistanceMouseToCenter > 0.001f) { // Avoid division by zero
                        scaleRatio = currentDistanceMouseToCenter / initialDistanceMouseToCenter;
                    }
                    newScale = originalScaleOnDragStart * scaleRatio;
                }
                else // SHIFT NOT HELD: Free (non-uniform) scaling for corners (existing logic)
                {
                    Vector2 dragVectorGlobal = currentGlobalMousePos - dragStartMousePosition;
                    Vector2 dragVectorLocal = dragVectorGlobal.Rotated(-Rotation);
                    // Vector2 textureSize = objectOriginalSpriteSize; // Already stored
                    if (Mathf.IsZeroApprox(objectOriginalSpriteSize.X) || Mathf.IsZeroApprox(objectOriginalSpriteSize.Y)) return;

                    float scaleFactorX = 1.0f;
                    float scaleFactorY = 1.0f;
                    float originalEffectualWidth = objectOriginalSpriteSize.X * originalScaleOnDragStart.X;
                    float originalEffectualHeight = objectOriginalSpriteSize.Y * originalScaleOnDragStart.Y;

                    switch (activeHandleType)
                    {
                        case HandleType.TopLeft:
                            if (!Mathf.IsZeroApprox(originalEffectualWidth)) scaleFactorX = (originalEffectualWidth - dragVectorLocal.X * 2) / originalEffectualWidth;
                            if (!Mathf.IsZeroApprox(originalEffectualHeight)) scaleFactorY = (originalEffectualHeight - dragVectorLocal.Y * 2) / originalEffectualHeight;
                            break;
                        case HandleType.TopRight:
                            if (!Mathf.IsZeroApprox(originalEffectualWidth)) scaleFactorX = (originalEffectualWidth + dragVectorLocal.X * 2) / originalEffectualWidth;
                            if (!Mathf.IsZeroApprox(originalEffectualHeight)) scaleFactorY = (originalEffectualHeight - dragVectorLocal.Y * 2) / originalEffectualHeight;
                            break;
                        case HandleType.BottomLeft:
                            if (!Mathf.IsZeroApprox(originalEffectualWidth)) scaleFactorX = (originalEffectualWidth - dragVectorLocal.X * 2) / originalEffectualWidth;
                            if (!Mathf.IsZeroApprox(originalEffectualHeight)) scaleFactorY = (originalEffectualHeight + dragVectorLocal.Y * 2) / originalEffectualHeight;
                            break;
                        case HandleType.BottomRight:
                            if (!Mathf.IsZeroApprox(originalEffectualWidth)) scaleFactorX = (originalEffectualWidth + dragVectorLocal.X * 2) / originalEffectualWidth;
                            if (!Mathf.IsZeroApprox(originalEffectualHeight)) scaleFactorY = (originalEffectualHeight + dragVectorLocal.Y * 2) / originalEffectualHeight;
                            break;
                    }
                    newScale = new Vector2(originalScaleOnDragStart.X * scaleFactorX, originalScaleOnDragStart.Y * scaleFactorY);
                }
            }
            else // Side handles: Always free scaling along one axis (existing logic)
            {
                Vector2 dragVectorGlobal = currentGlobalMousePos - dragStartMousePosition;
                Vector2 dragVectorLocal = dragVectorGlobal.Rotated(-Rotation);
                // Vector2 textureSize = objectOriginalSpriteSize; // Already stored
                if (Mathf.IsZeroApprox(objectOriginalSpriteSize.X) || Mathf.IsZeroApprox(objectOriginalSpriteSize.Y)) return;

                float scaleFactorX = 1.0f;
                float scaleFactorY = 1.0f;
                float originalEffectualWidth = objectOriginalSpriteSize.X * originalScaleOnDragStart.X;
                float originalEffectualHeight = objectOriginalSpriteSize.Y * originalScaleOnDragStart.Y;

                switch (activeHandleType)
                {
                     case HandleType.Left:
                        if (!Mathf.IsZeroApprox(originalEffectualWidth)) scaleFactorX = (originalEffectualWidth - dragVectorLocal.X * 2) / originalEffectualWidth;
                        scaleFactorY = 1.0f;
                        break;
                    case HandleType.Right:
                        if (!Mathf.IsZeroApprox(originalEffectualWidth)) scaleFactorX = (originalEffectualWidth + dragVectorLocal.X * 2) / originalEffectualWidth;
                        scaleFactorY = 1.0f;
                        break;
                    case HandleType.Top:
                        if (!Mathf.IsZeroApprox(originalEffectualHeight)) scaleFactorY = (originalEffectualHeight - dragVectorLocal.Y * 2) / originalEffectualHeight;
                        scaleFactorX = 1.0f;
                        break;
                    case HandleType.Bottom:
                        if (!Mathf.IsZeroApprox(originalEffectualHeight)) scaleFactorY = (originalEffectualHeight + dragVectorLocal.Y * 2) / originalEffectualHeight;
                        scaleFactorX = 1.0f;
                        break;
                }
                newScale = new Vector2(originalScaleOnDragStart.X * scaleFactorX, originalScaleOnDragStart.Y * scaleFactorY);
            }

            float minScale = 0.05f; // Minimum scale factor for each dimension
            newScale.X = Mathf.Max(newScale.X, minScale);
            newScale.Y = Mathf.Max(newScale.Y, minScale);

            activeTarget.CurrentScale = newScale;
            UpdateGizmoHandlesLocation(activeTarget);
            EmitSignal(SignalName.ScaleUpdated, newScale);
            GetViewport().SetInputAsHandled();
        }
         else if (@event is InputEventMouse) // Generic mouse event for hover effects
        {
            // This logic is too simple for robust hover. Proper hover via signals is better.
            // For now, we'll remove direct SetMeta here and rely on MainScene for visual feedback if needed.
            // Or, connect mouse_entered/exited signals from Area2D in SetupHandle.
        }
    }

    // The _Process method for hover check was very basic and likely not robust.
    // Proper hover effects are often handled by connecting Area2D.mouse_entered and mouse_exited signals
    // to change sprite appearance or set a flag. For this subtask, we focus on the core logic.
    // Removing the previous _Process hover logic.
    /*
    public override void _Process(double delta)
    {
        // Simple hover check (can be improved with mouse_entered/exited signals)
        // if (!isDragging) { ... }
    }
    */

    public bool IsDragging() => isDragging;

    public bool IsMouseOverAnyHandle()
    {
        // This method would require Area2D signals (mouse_entered/exited) to be connected
        // to reliably track hover state. The previous meta-based approach was not robust.
        // For now, this method might not be fully functional without those signal connections.
        // Returning false as a placeholder or it could check a flag set by those signals.
        return false;
    }
}
