// In res://ui/gizmos/CombinedTransformGizmo.cs
using Godot;

public partial class CombinedTransformGizmo : Node2D
{
    public PlacedObject CurrentTargetObject { get; private set; }

    // Signals for Translation
    [Signal]
    public delegate void TranslationStartedEventHandler(Vector2 initialPosition, PlacedObject targetObject);
    [Signal]
    public delegate void TranslationUpdatedEventHandler(Vector2 newDesiredPosition, PlacedObject targetObject); // Gizmo emits desired, MainScene snaps & applies
    [Signal]
    public delegate void TranslationFinishedEventHandler(Vector2 finalAppliedPosition, Vector2 originalPosition, PlacedObject targetObject);

    // Signals for Rotation (will mirror RotationGizmo's signals later)
    // [Signal] public delegate void RotationStartedEventHandler(float initialRotationDegrees, PlacedObject targetObject);
    // ... etc.

    // Signals for Scale (will mirror ScaleGizmo's signals later)
    // [Signal] public delegate void ScaleStartedEventHandler(Vector2 initialScale, PlacedObject targetObject);
    // ... etc.

    private bool isVisibleAndTargetValid = false;
    // private bool isDraggingTranslate = false; // For IsDragging() later

    public override void _Ready()
    {
        this.Visible = false; // Ensure hidden initially
    }

    public void ShowForTarget(PlacedObject target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target))
        {
            Hide();
            return;
        }
        CurrentTargetObject = target;
        // Ensure gizmo is positioned at target's pivot for proper handle calculations relative to it
        this.GlobalPosition = target.GlobalPosition;

        // For world-axis aligned translation handles, gizmo's own rotation should be 0.
        // If handles were to be local to object's rotation, then this.GlobalRotation = target.GlobalRotationDegrees;
        this.GlobalRotationDegrees = 0;

        UpdateGizmoHandles();
        this.Visible = true;
        isVisibleAndTargetValid = true;
        // GD.Print($"CombinedTransformGizmo: Shown for {target.AssetNameRef} at {this.GlobalPosition}");
    }

    public void Hide()
    {
        this.Visible = false;
        isVisibleAndTargetValid = false;
        // isDraggingTranslate = false;
        CurrentTargetObject = null;
        // GD.Print("CombinedTransformGizmo: Hidden");
    }

    // This method is called when the target object itself is moved/rotated/scaled externally
    // or if this gizmo's own _Process updates the target, it calls this to reposition handles.
    public void UpdateGizmoHandles()
    {
        if (!isVisibleAndTargetValid || CurrentTargetObject == null || !GodotObject.IsInstanceValid(CurrentTargetObject))
        {
            // If it became invalid while visible, hide it.
            if (this.Visible) Hide();
            return;
        }

        // Match gizmo's core position to target's pivot
        this.GlobalPosition = CurrentTargetObject.GlobalPosition;
        // Ensure world-axis orientation for translate handles (rotation handles will be relative to object or this node)
        this.GlobalRotationDegrees = 0;

        // Logic to position individual handles (Translate X/Y arrows, rotation circle, scale boxes) will go here in next steps.
        // For example, if you have child nodes representing handles:
        // Node2D xAxisHandle = GetNodeOrNull<Node2D>("XAxisHandle");
        // if (xAxisHandle != null) {
        //    // Position handle relative to this gizmo's origin (which is target's pivot)
        //    // Its visual appearance (e.g. an arrow pointing right) will be based on its own local rotation or sprite.
        //    xAxisHandle.Position = Vector2.Zero; // Or offset if needed, e.g., Vector2.Right * HandleOffsetDistance
        // }
        QueueRedraw(); // If handles are drawn in _Draw()
    }

    // Public method for MainScene to check if gizmo is actively dragging
    // public bool IsDragging()
    // {
    //    return isDraggingTranslate; // || isDraggingRotate || isDraggingScale;
    // }

    // _Input or _UnhandledInput will be added later for handle interaction
    // _Draw will be added later for drawing handles if not using Area2D nodes
}
