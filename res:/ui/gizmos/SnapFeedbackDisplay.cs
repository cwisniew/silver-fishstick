// In res://ui/gizmos/SnapFeedbackDisplay.cs
using Godot;
using System.Collections.Generic;

public partial class SnapFeedbackDisplay : Node2D
{
    private struct SnapLineInfo
    {
        public Vector2 From;
        public Vector2 To;
        public Color Color;
        public float Width;
    }

    private List<SnapLineInfo> linesToDraw = new List<SnapLineInfo>();

    private Camera2D mapCamera;
    private const float DefaultLineWidth = 1.5f; // Made slightly thicker for visibility
    private string mapCameraPath = "/root/MainScene/MapCamera"; // Default path, can be overridden

    public void SetMapCameraPath(string path)
    {
        mapCameraPath = path;
        // If called after _Ready, try to re-acquire camera
        if (IsInsideTree())
        {
            mapCamera = GetNodeOrNull<Camera2D>(mapCameraPath);
            if (mapCamera == null)
            {
                GD.PrintRich($"[SnapFeedbackDisplay] Warning: MapCamera not found at path '{mapCameraPath}'. Line thickness will not adjust to zoom.");
            }
        }
    }

    public override void _Ready()
    {
        // Attempt to get the camera using the potentially customized path
        if (!string.IsNullOrEmpty(mapCameraPath))
        {
            mapCamera = GetNodeOrNull<Camera2D>(mapCameraPath);
        }

        if (mapCamera == null) // Fallback or if path was never set
        {
             // Try a common default path if specific one failed or wasn't set
            mapCamera = GetTree().Root.GetNodeOrNull<Camera2D>("MainScene/MapCamera");
            if (mapCamera == null) { // Try another common pattern if the above is not found
                 Node parent = GetParent();
                 while(parent != null && !(parent is MainScene)) { // Assuming MainScene is a known ancestor
                     parent = parent.GetParent();
                 }
                 if (parent is MainScene mainSceneNode) {
                     mapCamera = mainSceneNode.GetNodeOrNull<Camera2D>("MapCamera");
                 }
            }
        }

        if (mapCamera == null)
        {
            GD.PrintRich("[SnapFeedbackDisplay] Warning: MapCamera not found. Line thickness will not adjust to zoom. Ensure MapCameraPath is correct or camera is at a known default location.");
        }
    }

    public void AddSnapLine(Vector2 globalFrom, Vector2 globalTo, Color color)
    {
        float lineWidth = DefaultLineWidth;
        if (mapCamera != null && mapCamera.Zoom.X > 0.001f) // Ensure Zoom.X is positive and not excessively small
        {
            lineWidth = Mathf.Max(0.5f, DefaultLineWidth / mapCamera.Zoom.X); // Ensure minimum thickness
        }
        linesToDraw.Add(new SnapLineInfo { From = globalFrom, To = globalTo, Color = color, Width = lineWidth });
        QueueRedraw();
    }

    public void ClearAllLines()
    {
        if (linesToDraw.Count > 0)
        {
            linesToDraw.Clear();
            QueueRedraw();
        }
    }

    // _Process is not needed if MainScene calls ClearAllLines() each frame before AddSnapLine()
    // public override void _Process(double delta)
    // {
    //     // Example: Timed self-clear if MainScene doesn't manage it.
    // }

    public override void _Draw()
    {
        if (linesToDraw.Count == 0)
        {
            return;
        }

        foreach (SnapLineInfo lineInfo in linesToDraw)
        {
            // Positions are global, but _Draw draws in local space of this Node2D.
            // Convert global line points to local space for drawing.
            Vector2 localFrom = ToLocal(lineInfo.From);
            Vector2 localTo = ToLocal(lineInfo.To);
            DrawLine(localFrom, localTo, lineInfo.Color, lineInfo.Width, true); // Antialiased = true
        }
    }
}
