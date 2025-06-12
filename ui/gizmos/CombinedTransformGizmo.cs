using Godot;
using System;

public partial class CombinedTransformGizmo : Node2D
{
    [Signal]
    public delegate void TranslationHandleGrabbedEventHandler(string axisOrType, PlacedObject targetObject);
    [Signal]
    public delegate void TranslationUpdatedEventHandler(Vector2 rawNewDesiredPosition, PlacedObject targetObject);
    [Signal]
    public delegate void TranslationHandleReleasedEventHandler(Vector2 finalRawDesiredPosition, PlacedObject targetObject);

    // Placeholder for future Rotation/Scale signals

    private Area2D _xAxisHandleArea;
    private Area2D _yAxisHandleArea;
    private Area2D _freeMoveHandleArea;

    private Sprite2D _xAxisSprite;
    private Sprite2D _yAxisSprite;
    private Sprite2D _freeMoveSprite;

    private enum DraggingTool { None, Translation, Rotation, Scale }
    private enum DraggingState
    {
        None,
        TranslateX, TranslateY, TranslateFree,
        Rotate, ScaleX, ScaleY, ScaleUniform
    }
    private DraggingState _currentDraggingState = DraggingState.None;
    private DraggingTool _activeTool = DraggingTool.Translation;

    private bool _isMouseOverXHandle = false;
    private bool _isMouseOverYHandle = false;
    private bool _isMouseOverFreeHandle = false;

    private Vector2 _initialDragMousePosition; // Global mouse position when drag started
    private Vector2 _objectOriginalPositionOnDragStart; // Object's global position when drag started
    private PlacedObject _currentTargetObject;

    public override void _Ready()
    {
        Visible = false;
        _xAxisHandleArea = GetNode<Area2D>("XAxisHandleArea");
        _yAxisHandleArea = GetNode<Area2D>("YAxisHandleArea");
        _freeMoveHandleArea = GetNode<Area2D>("FreeMoveHandleArea");

        if (_xAxisHandleArea != null) _xAxisSprite = _xAxisHandleArea.GetNode<Sprite2D>("Sprite2D");
        if (_yAxisHandleArea != null) _yAxisSprite = _yAxisHandleArea.GetNode<Sprite2D>("Sprite2D");
        if (_freeMoveHandleArea != null) _freeMoveSprite = _freeMoveHandleArea.GetNode<Sprite2D>("Sprite2D");

        ConnectTranslationHandle(_xAxisHandleArea, "x");
        ConnectTranslationHandle(_yAxisHandleArea, "y");
        ConnectTranslationHandle(_freeMoveHandleArea, "free");
    }

    private void ConnectTranslationHandle(Area2D handleArea, string axisOrType)
    {
        if (handleArea == null)
        {
            GD.PushWarning($"CombinedTransformGizmo: Translation Handle Area for '{axisOrType}' not found.");
            return;
        }
        handleArea.InputEvent += (viewport, ev, shapeIdx) => OnTranslationHandleInputEvent(ev, axisOrType);
        switch (axisOrType)
        {
            case "x":
                handleArea.MouseEntered += () => { _isMouseOverXHandle = true; GD.Print("Gizmo Mouse on X Handle"); };
                handleArea.MouseExited += () => { _isMouseOverXHandle = false; GD.Print("Gizmo Mouse off X Handle"); };
                break;
            case "y":
                handleArea.MouseEntered += () => { _isMouseOverYHandle = true; GD.Print("Gizmo Mouse on Y Handle"); };
                handleArea.MouseExited += () => { _isMouseOverYHandle = false; GD.Print("Gizmo Mouse off Y Handle"); };
                break;
            case "free":
                handleArea.MouseEntered += () => { _isMouseOverFreeHandle = true; GD.Print("Gizmo Mouse on Free Handle"); };
                handleArea.MouseExited += () => { _isMouseOverFreeHandle = false; GD.Print("Gizmo Mouse off Free Handle"); };
                break;
        }
    }

    public void ShowForTarget(PlacedObject target)
    {
        if (target == null || !GodotObject.IsInstanceValid(target))
        {
            HideGizmo();
            return;
        }
        _currentTargetObject = target;
        GlobalPosition = _currentTargetObject.GlobalPosition; // Gizmo base to object's current pos
        Visible = true;
        UpdateGizmoHandles();
    }

    public void HideGizmo()
    {
        Visible = false;
        if (_currentDraggingState != DraggingState.None) // If was dragging
        {
            if (_activeTool == DraggingTool.Translation && _currentTargetObject != null && GodotObject.IsInstanceValid(_currentTargetObject))
            {
                Vector2 currentGlobalMousePos = GetGlobalMousePosition();
                Vector2 totalDragDelta = currentGlobalMousePos - _initialDragMousePosition;
                Vector2 finalRawPos = _objectOriginalPositionOnDragStart + CalculateConstrainedDelta(totalDragDelta);
                EmitSignal(SignalName.TranslationHandleReleased, finalRawPos, _currentTargetObject);
            }
            _currentDraggingState = DraggingState.None;
        }
        _currentTargetObject = null;
        _isMouseOverXHandle = false;
        _isMouseOverYHandle = false;
        _isMouseOverFreeHandle = false;
    }

    public void SetActiveTool(DraggingTool tool)
    {
        _activeTool = tool;
    }

    public void UpdateGizmoHandles() // Called by MainScene when object has actually moved
    {
        if (_currentTargetObject != null && Visible && GodotObject.IsInstanceValid(_currentTargetObject))
        {
            GlobalPosition = _currentTargetObject.GlobalPosition;
        }
    }

    private Vector2 CalculateConstrainedDelta(Vector2 totalDragDelta)
    {
        Vector2 constrainedDelta = Vector2.Zero;
        if (_currentDraggingState == DraggingState.TranslateX)
        {
            constrainedDelta = GlobalTransform.X.Normalized() * totalDragDelta.Dot(GlobalTransform.X.Normalized());
        }
        else if (_currentDraggingState == DraggingState.TranslateY)
        {
            constrainedDelta = GlobalTransform.Y.Normalized() * totalDragDelta.Dot(GlobalTransform.Y.Normalized());
        }
        else // TranslateFree or other states
        {
            constrainedDelta = totalDragDelta;
        }
        return constrainedDelta;
    }

    private void OnTranslationHandleInputEvent(InputEvent @event, string axisOrType)
    {
        if (!Visible || _currentTargetObject == null || !GodotObject.IsInstanceValid(_currentTargetObject) || _activeTool != DraggingTool.Translation) return;

        if (@event is InputEventMouseButton mouseButtonEvent && mouseButtonEvent.ButtonIndex == MouseButton.Left)
        {
            if (mouseButtonEvent.Pressed)
            {
                _initialDragMousePosition = GetGlobalMousePosition();
                _objectOriginalPositionOnDragStart = _currentTargetObject.GlobalPosition;

                switch(axisOrType)
                {
                    case "x": _currentDraggingState = DraggingState.TranslateX; break;
                    case "y": _currentDraggingState = DraggingState.TranslateY; break;
                    case "free": _currentDraggingState = DraggingState.TranslateFree; break;
                }
                EmitSignal(SignalName.TranslationHandleGrabbed, axisOrType, _currentTargetObject);
                GetViewport().SetInputAsHandled();
            }
            else // Released
            {
                if (_currentDraggingState == DraggingState.TranslateX ||
                    _currentDraggingState == DraggingState.TranslateY ||
                    _currentDraggingState == DraggingState.TranslateFree)
                {
                    Vector2 currentGlobalMousePos = GetGlobalMousePosition();
                    Vector2 totalDragDelta = currentGlobalMousePos - _initialDragMousePosition;
                    Vector2 finalRawPos = _objectOriginalPositionOnDragStart + CalculateConstrainedDelta(totalDragDelta);

                    EmitSignal(SignalName.TranslationHandleReleased, finalRawPos, _currentTargetObject);
                    _currentDraggingState = DraggingState.None;
                }
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || _currentTargetObject == null || !GodotObject.IsInstanceValid(_currentTargetObject) || _currentDraggingState == DraggingState.None) return;

        if (@event is InputEventMouseMotion mouseMotionEvent)
        {
            if (_activeTool == DraggingTool.Translation &&
               (_currentDraggingState == DraggingState.TranslateX ||
                _currentDraggingState == DraggingState.TranslateY ||
                _currentDraggingState == DraggingState.TranslateFree))
            {
                Vector2 currentGlobalMousePos = GetGlobalMousePosition();
                Vector2 totalDragDelta = currentGlobalMousePos - _initialDragMousePosition;
                Vector2 constrainedDelta = CalculateConstrainedDelta(totalDragDelta);
                Vector2 rawNewDesiredPosition = _objectOriginalPositionOnDragStart + constrainedDelta;

                EmitSignal(SignalName.TranslationUpdated, rawNewDesiredPosition, _currentTargetObject);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public bool IsInputCapturedByGizmo()
    {
        return _currentDraggingState != DraggingState.None;
    }

    public bool IsMouseOverAnyHandle()
    {
        if (!Visible) return false;
        if (_activeTool == DraggingTool.Translation)
        {
            return _isMouseOverXHandle || _isMouseOverYHandle || _isMouseOverFreeHandle;
        }
        return false;
    }
}
