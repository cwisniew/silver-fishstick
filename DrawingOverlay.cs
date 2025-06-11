using Godot;
using System;
using System.Collections.Generic;
using System.Linq; // Required for .ToArray() on List if not implicitly available

public partial class DrawingOverlay : Node2D
{
	public bool IsDrawingEnabled { get; set; } = false;
	public Color CurrentDrawColor { get; set; } = Colors.Red;
	public float LineThickness { get; set; } = 3.0f; // Changed default to 3.0f for better visibility

	private List<List<Vector2>> _allLines = new List<List<Vector2>>();
	private List<Vector2> _currentLine = null; // The line currently being drawn

	private Vector2 _tempMeasureStart = Vector2.Zero;
	private Vector2 _tempMeasureEnd = Vector2.Zero;
	private string _tempMeasureText = null;
	private bool _drawTemporaryMeasurement = false;

	public override void _Draw()
	{
		// Draw persistent lines
		foreach (var linePoints in _allLines)
		{
			if (linePoints != null && linePoints.Count >= 2)
			{
				DrawPolyline(linePoints.ToArray(), CurrentDrawColor, LineThickness, true); // Antialiased
			}
		}

		if (_currentLine != null && _currentLine.Count >= 2)
		{
			DrawPolyline(_currentLine.ToArray(), CurrentDrawColor, LineThickness, true); // Antialiased
		}

		// Draw temporary measurement line and text
		if (_drawTemporaryMeasurement && _tempMeasureStart != Vector2.Zero && _tempMeasureEnd != Vector2.Zero)
		{
			DrawLine(_tempMeasureStart, _tempMeasureEnd, Colors.Cyan, 2.0f, true); // Antialiased
			if (!string.IsNullOrEmpty(_tempMeasureText))
			{
				// Use default font, consider making font configurable
				Font font = ThemeDB.FallbackFont;
				int fontSize = ThemeDB.FallbackFontSize;
				Vector2 midPoint = (_tempMeasureStart + _tempMeasureEnd) / 2;
				Vector2 textSize = font.GetStringSize(_tempMeasureText, HorizontalAlignment.Left, -1, fontSize);

				// Background for text
				Rect2 textBgRect = new Rect2(midPoint + new Vector2(5, -textSize.Y - 5), textSize + new Vector2(4,4));
				DrawRect(textBgRect, Colors.Black.Transparent(0.5f));

				DrawString(font, midPoint + new Vector2(7, -7), _tempMeasureText, HorizontalAlignment.Left, -1, fontSize, Colors.White);
			}
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Prevent drawing if a UI control has focus (e.g., chat input)
		if (GetViewport().GuiGetFocusOwner() != null && IsDrawingEnabled)
		{
			// If drawing was active and user clicks on UI, cancel current line
			if (_currentLine != null)
			{
				_currentLine = null; // Discard current line
				QueueRedraw(); // Update display to remove partial line
			}
			// Do not set input as handled, let the UI element process it
			return;
		}

		if (!IsDrawingEnabled)
		{
			return; // Don't process input if drawing is disabled
		}

		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_currentLine = new List<Vector2>();
					// Convert mouse position from viewport to local coordinates of this Node2D
					_currentLine.Add(GetLocalMousePosition());
					GetViewport().SetInputAsHandled(); // Consume the event
				}
				else // Mouse button released
				{
					if (_currentLine != null && _currentLine.Count > 0) // Ensure there's something to add
					{
						// Add the last point on release
						_currentLine.Add(GetLocalMousePosition());
						// Only add if it's more than a click (e.g. at least 2 points for a tiny line)
						if (_currentLine.Count >=2) {
							_allLines.Add(new List<Vector2>(_currentLine)); // Add a copy
						}
					}
					_currentLine = null; // Reset for the next line
					QueueRedraw(); // Update display
					GetViewport().SetInputAsHandled(); // Consume the event
				}
			}
		}
		else if (@event is InputEventMouseMotion mm)
		{
			if (_currentLine != null && mm.ButtonMask.HasFlag(MouseButtonMask.Left))
			{
				_currentLine.Add(GetLocalMousePosition());
				QueueRedraw(); // Request a redraw to show the line being drawn
				GetViewport().SetInputAsHandled(); // Consume the event
			}
		}
	}

	public void ClearDrawings()
	{
		_allLines.Clear();
		_currentLine = null;
		QueueRedraw(); // Request a redraw to clear the screen
	}

	public void SetDrawingColor(Color color)
	{
		CurrentDrawColor = color;
		// If _currentLine is active, future points of it will use new color. Existing lines keep old color.
		// This is standard behavior. If wanting to change current line color, more logic needed.
	}

	public void SetLineThickness(float thickness)
	{
		LineThickness = Mathf.Max(1.0f, thickness); // Ensure thickness is at least 1.0
	}

	public void DrawTemporaryMeasurement(Vector2 globalStart, Vector2 globalEnd, string text)
	{
		// Convert global coordinates to local for drawing
		_tempMeasureStart = ToLocal(globalStart);
		_tempMeasureEnd = ToLocal(globalEnd);
		_tempMeasureText = text;
		_drawTemporaryMeasurement = true;
		QueueRedraw();
	}

	public void ClearTemporaryMeasurement()
	{
		_drawTemporaryMeasurement = false;
		_tempMeasureText = null;
		// Reset points to avoid drawing old line if DrawTemporaryMeasurement is called again with only text
		_tempMeasureStart = Vector2.Zero;
		_tempMeasureEnd = Vector2.Zero;
		QueueRedraw();
	}
}
