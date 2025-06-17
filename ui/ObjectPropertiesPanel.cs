using Godot;
using System;
using System.Collections.Generic; // For List
using System.Globalization; // For CultureInfo

public partial class ObjectPropertiesPanel : PanelContainer
{
    [Signal]
    public delegate void PositionChangeRequestedEventHandler(NodePath objectPath, Vector2 newRawPosition);
    [Signal]
    public delegate void RotationChangeRequestedEventHandler(NodePath objectPath, float newRawRotationDegrees);
    [Signal]
    public delegate void ScaleChangeRequestedEventHandler(NodePath objectPath, Vector2 newRawScale);

    private LineEdit positionXEdit;
    private LineEdit positionYEdit;
    private LineEdit rotationEdit;
    private LineEdit scaleXEdit;
    private LineEdit scaleYEdit;

    private NodePath currentSingleSelectedObjectPath; // Stores path to the currently edited single object

    public override void _Ready()
    {
        // Adjust paths if your .tscn structure is different
        positionXEdit = GetNode<LineEdit>("PropertiesVBox/PositionXHBox/PositionXEdit");
        positionYEdit = GetNode<LineEdit>("PropertiesVBox/PositionYHBox/PositionYEdit");
        rotationEdit = GetNode<LineEdit>("PropertiesVBox/RotationHBox/RotationEdit");
        scaleXEdit = GetNode<LineEdit>("PropertiesVBox/ScaleXHBox/ScaleXEdit");
        scaleYEdit = GetNode<LineEdit>("PropertiesVBox/ScaleYHBox/ScaleYEdit");


        if (positionXEdit != null) positionXEdit.TextSubmitted += OnPositionXSubmitted;
        else GD.PrintErr("ObjectPropertiesPanel: PositionXEdit not found.");

        if (positionYEdit != null) positionYEdit.TextSubmitted += OnPositionYSubmitted;
        else GD.PrintErr("ObjectPropertiesPanel: PositionYEdit not found.");

        if (rotationEdit != null) rotationEdit.TextSubmitted += OnRotationSubmitted;
        else GD.PrintErr("ObjectPropertiesPanel: RotationEdit not found.");

        if (scaleXEdit != null) scaleXEdit.TextSubmitted += OnScaleXSubmitted;
        else GD.PrintErr("ObjectPropertiesPanel: ScaleXEdit not found.");

        if (scaleYEdit != null) scaleYEdit.TextSubmitted += OnScaleYSubmitted;
        else GD.PrintErr("ObjectPropertiesPanel: ScaleYEdit not found.");
    }

    private void OnPositionXSubmitted(string newText)
    {
        if (currentSingleSelectedObjectPath == null || positionXEdit == null || !positionXEdit.Editable)
        {
            return;
        }

        if (float.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out float newX))
        {
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null)
            {
                EmitSignal(SignalName.PositionChangeRequested, currentSingleSelectedObjectPath, new Vector2(newX, obj.GlobalPosition.Y));
            }
        }
        else
        {
            // Optionally revert text or show an error in a status label within this panel
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null) positionXEdit.Text = obj.GlobalPosition.X.ToString("F2");
            GD.Print("ObjectPropertiesPanel: Invalid X position input.");
        }
    }

    private void OnPositionYSubmitted(string newText)
    {
        if (currentSingleSelectedObjectPath == null || positionYEdit == null || !positionYEdit.Editable)
        {
            return;
        }

        if (float.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out float newY))
        {
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null)
            {
                EmitSignal(SignalName.PositionChangeRequested, currentSingleSelectedObjectPath, new Vector2(obj.GlobalPosition.X, newY));
            }
        }
        else
        {
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null) positionYEdit.Text = obj.GlobalPosition.Y.ToString("F2");
            GD.Print("ObjectPropertiesPanel: Invalid Y position input.");
        }
    }

    public void DisplayControls(List<PlacedObject> selectedObjects, MainScene mainScene) // Renamed to DisplayControls, added MainScene ref
    {
        currentSingleSelectedObjectPath = null; // Reset

        if (positionXEdit == null || positionYEdit == null)
        {
            GD.PrintErr("ObjectPropertiesPanel: Position LineEdits are null in DisplayControls. Cannot update.");
            return;
        }

        // Make panel visible if there are selected objects, hide if not.
        // MainScene's UpdateObjectPropertiesPanel will still handle the this.Visible overall.
        // This method just focuses on the content.

        if (selectedObjects == null || selectedObjects.Count == 0)
        {
            positionXEdit.Text = "";
            positionXEdit.Editable = false;
            positionYEdit.Text = "";
            positionYEdit.Editable = false;
            if (rotationEdit != null) { rotationEdit.Text = ""; rotationEdit.Editable = false; }
            if (scaleXEdit != null) { scaleXEdit.Text = ""; scaleXEdit.Editable = false; }
            if (scaleYEdit != null) { scaleYEdit.Text = ""; scaleYEdit.Editable = false; }
        }
        else if (selectedObjects.Count == 1)
        {
            PlacedObject obj = selectedObjects[0];
            if (obj != null && GodotObject.IsInstanceValid(obj))
            {
                currentSingleSelectedObjectPath = obj.GetPath();
                positionXEdit.Text = obj.GlobalPosition.X.ToString("F2");
                positionXEdit.Editable = true;
                positionYEdit.Text = obj.GlobalPosition.Y.ToString("F2");
                positionYEdit.Editable = true;
                if (rotationEdit != null) { rotationEdit.Text = obj.RotationDegrees.ToString("F2"); rotationEdit.Editable = true; }
                if (scaleXEdit != null) { scaleXEdit.Text = obj.Scale.X.ToString("F2"); scaleXEdit.Editable = true; }
                if (scaleYEdit != null) { scaleYEdit.Text = obj.Scale.Y.ToString("F2"); scaleYEdit.Editable = true; }
            }
            else // Object might be invalid
            {
                positionXEdit.Text = "[Error]"; positionXEdit.Editable = false;
                positionYEdit.Text = "[Error]"; positionYEdit.Editable = false;
                if (rotationEdit != null) { rotationEdit.Text = "[Error]"; rotationEdit.Editable = false; }
                if (scaleXEdit != null) { scaleXEdit.Text = "[Error]"; scaleXEdit.Editable = false; }
                if (scaleYEdit != null) { scaleYEdit.Text = "[Error]"; scaleYEdit.Editable = false; }
            }
        }
        else // Multiple objects selected
        {
            // Position (already handled this way)
            bool xMixed = false; bool yMixed = false;
            Vector2 firstPos = selectedObjects[0].GlobalPosition;
            for (int i = 1; i < selectedObjects.Count; i++) {
                if (!Mathf.IsEqualApprox(selectedObjects[i].GlobalPosition.X, firstPos.X)) xMixed = true;
                if (!Mathf.IsEqualApprox(selectedObjects[i].GlobalPosition.Y, firstPos.Y)) yMixed = true;
                if (xMixed && yMixed) break;
            }
            positionXEdit.Text = xMixed ? "[Mixed]" : firstPos.X.ToString("F2");
            positionXEdit.Editable = !xMixed;
            positionYEdit.Text = yMixed ? "[Mixed]" : firstPos.Y.ToString("F2");
            positionYEdit.Editable = !yMixed;

            // Rotation & Scale - For simplicity, non-editable and "[Mixed]" for multi-selection for now.
            if (rotationEdit != null) { rotationEdit.Text = "[Mixed]"; rotationEdit.Editable = false; }
            if (scaleXEdit != null) { scaleXEdit.Text = "[Mixed]"; scaleXEdit.Editable = false; }
            if (scaleYEdit != null) { scaleYEdit.Text = "[Mixed]"; scaleYEdit.Editable = false; }
        }
    }

    public void UpdateDisplayedPositionFromSnap(Vector2 snappedPosition)
    {
        if (positionXEdit == null || positionYEdit == null) return;
        positionXEdit.Text = snappedPosition.X.ToString("F2");
        positionYEdit.Text = snappedPosition.Y.ToString("F2");
    }

    public void UpdateDisplayedRotation(float newRotationDegrees)
    {
        if (rotationEdit != null) rotationEdit.Text = newRotationDegrees.ToString("F2");
    }

    public void UpdateDisplayedScale(Vector2 newScale)
    {
        if (scaleXEdit != null) scaleXEdit.Text = newScale.X.ToString("F2");
        if (scaleYEdit != null) scaleYEdit.Text = newScale.Y.ToString("F2");
    }

    // --- TextSubmitted Handlers ---
    private void OnRotationSubmitted(string newText)
    {
        if (currentSingleSelectedObjectPath == null || rotationEdit == null || !rotationEdit.Editable) return;
        if (float.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out float newRotation))
        {
            EmitSignal(SignalName.RotationChangeRequested, currentSingleSelectedObjectPath, newRotation);
        }
        else
        {
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null) rotationEdit.Text = obj.RotationDegrees.ToString("F2");
            GD.PrintErr("ObjectPropertiesPanel: Invalid rotation input.");
        }
    }

    private void OnScaleXSubmitted(string newText)
    {
        if (currentSingleSelectedObjectPath == null || scaleXEdit == null || !scaleXEdit.Editable) return;
        if (float.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out float newScaleX))
        {
            if (newScaleX < 0.01f) newScaleX = 0.01f; // Enforce minimum scale
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null)
            {
                EmitSignal(SignalName.ScaleChangeRequested, currentSingleSelectedObjectPath, new Vector2(newScaleX, obj.Scale.Y));
            }
        }
        else
        {
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null) scaleXEdit.Text = obj.Scale.X.ToString("F2");
            GD.PrintErr("ObjectPropertiesPanel: Invalid Scale X input.");
        }
    }

    private void OnScaleYSubmitted(string newText)
    {
        if (currentSingleSelectedObjectPath == null || scaleYEdit == null || !scaleYEdit.Editable) return;
        if (float.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out float newScaleY))
        {
            if (newScaleY < 0.01f) newScaleY = 0.01f; // Enforce minimum scale
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null)
            {
                EmitSignal(SignalName.ScaleChangeRequested, currentSingleSelectedObjectPath, new Vector2(obj.Scale.X, newScaleY));
            }
        }
        else
        {
            PlacedObject obj = GetNodeOrNull<PlacedObject>(currentSingleSelectedObjectPath);
            if (obj != null) scaleYEdit.Text = obj.Scale.Y.ToString("F2");
            GD.PrintErr("ObjectPropertiesPanel: Invalid Scale Y input.");
        }
    }
}
