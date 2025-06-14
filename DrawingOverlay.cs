using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DrawingOverlay : Node2D
{
	public bool IsDrawingEnabled { get; set; } = false;
	public Color CurrentDrawColor { get; set; } = Colors.Red;
	public float LineThickness { get; set; } = 3.0f;

	// Changed to store DrawingLineData objects to include style per line
	private List<DrawingLineData> _allLinesData = new List<DrawingLineData>();
	private List<Vector2> _activeLinePoints = null; // Points for the line currently being drawn by local user

	// For temporary measurement lines
	private Vector2 _tempMeasureStart = Vector2.Zero;
	private Vector2 _tempMeasureEnd = Vector2.Zero;
	private string _tempMeasureText = null;
	private bool _drawTemporaryMeasurement = false;

	private NetworkManager _networkManagerCache;

	public override void _Ready()
	{
		// Cache NetworkManager if in a multiplayer session
		if (Multiplayer.HasMultiplayerPeer())
		{
			// Adjust path if NetworkManagerNode is elsewhere or MainScene has a different name/path
			_networkManagerCache = GetNodeOrNull<NetworkManager>("/root/MainScene/NetworkManagerNode");
			if (_networkManagerCache == null)
			{
				GD.PrintErr("DrawingOverlay: Could not find NetworkManagerNode at /root/MainScene/NetworkManagerNode. Networked drawing might not work.");
			}
		}
	}

	public override void _Draw()
	{
		// Draw all persistent lines using their stored styles
		foreach (var lineData in _allLinesData)
		{
			if (lineData?.Points != null && lineData.Points.Count >= 2)
			{
				// Convert List<Vector2Data> to Vector2[] for DrawPolyline
				Vector2[] pointsToDraw = lineData.Points.Select(p => p.ToVector2()).ToArray();
				Color colorToDraw = lineData.LineColor?.ToColor() ?? Colors.White; // Default to white if color data is missing
				float thicknessToDraw = lineData.Thickness > 0 ? lineData.Thickness : 2.0f; // Default thickness
				DrawPolyline(pointsToDraw, colorToDraw, thicknessToDraw, true); // Antialiased
			}
		}

		// Draw the currently active line (being drawn by local user) with current overlay settings
		if (_activeLinePoints != null && _activeLinePoints.Count >= 2)
		{
			DrawPolyline(_activeLinePoints.ToArray(), CurrentDrawColor, LineThickness, true);
		}

		// Draw temporary measurement line and text
		if (_drawTemporaryMeasurement && _tempMeasureStart != Vector2.Zero && _tempMeasureEnd != Vector2.Zero)
		{
			DrawLine(_tempMeasureStart, _tempMeasureEnd, Colors.Cyan, 2.0f, true);
			if (!string.IsNullOrEmpty(_tempMeasureText))
			{
				Font font = ThemeDB.FallbackFont; int fontSize = ThemeDB.FallbackFontSize;
				Vector2 midPoint = (_tempMeasureStart + _tempMeasureEnd) / 2;
				Vector2 textSize = font.GetStringSize(_tempMeasureText, HorizontalAlignment.Left, -1, fontSize);
				Rect2 textBgRect = new Rect2(midPoint + new Vector2(5, -textSize.Y - 5), textSize + new Vector2(4,4));
				DrawRect(textBgRect, Colors.Black.Transparent(0.5f));
				DrawString(font, midPoint + new Vector2(7, -7), _tempMeasureText, HorizontalAlignment.Left, -1, fontSize, Colors.White);
			}
		}
	}

	private void ClearCurrentActiveLine()
	{
		_activeLinePoints = null;
		QueueRedraw();
	}

	public override void _Input(InputEvent @event)
	{
		// If UI has focus, cancel any ongoing local drawing action
		if (GetViewport().GuiGetFocusOwner() != null)
		{
			if (IsDrawingEnabled && _activeLinePoints != null) ClearCurrentActiveLine();
			return;
		}

		// Only server (or offline user) can draw new lines. Clients receive lines via RPC.
		if (Multiplayer.HasMultiplayerPeer() && !Multiplayer.IsServer())
		{
			// If drawing was somehow enabled on client, disable it and clear current line.
			if (IsDrawingEnabled) IsDrawingEnabled = false;
			if (_activeLinePoints != null) ClearCurrentActiveLine();
			return;
		}

		if (!IsDrawingEnabled) return;

		if (@event is InputEventMouseButton mb)
		{
			if (mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_activeLinePoints = new List<Vector2> { GetLocalMousePosition() };
					GetViewport().SetInputAsHandled();
				}
				else // Mouse button released
				{
					if (_activeLinePoints != null && _activeLinePoints.Count > 0)
					{
						_activeLinePoints.Add(GetLocalMousePosition());
						if (_activeLinePoints.Count >= 2)
						{
							// Create DrawingLineData for the completed line
							DrawingLineData completedLineData = new DrawingLineData(_activeLinePoints, CurrentDrawColor, LineThickness);
							_allLinesData.Add(completedLineData); // Add to local list

							// If server, broadcast this new line
							if (Multiplayer.IsServer() && _networkManagerCache != null)
							{
								string lineJson = Json.Stringify(completedLineData.ToDictionary());
								_networkManagerCache.Rpc(nameof(NetworkManager.RpcClientAddSingleDrawingLine), lineJson);
							}
						}
					}
					_activeLinePoints = null; // Reset for the next line
					QueueRedraw();
					GetViewport().SetInputAsHandled();
				}
			}
		}
		else if (@event is InputEventMouseMotion mm)
		{
			if (_activeLinePoints != null && mm.ButtonMask.HasFlag(MouseButtonMask.Left))
			{
				_activeLinePoints.Add(GetLocalMousePosition());
				QueueRedraw();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public void ClearDrawings() // Clears ALL drawings
	{
		_allLinesData.Clear();
		_activeLinePoints = null;
		QueueRedraw();
		// If server, should also broadcast a "clear all" event or send empty state.
		// For now, relying on new clients getting full (empty) state.
		// Existing clients won't clear until server sends new line or full state.
		// This can be improved with a specific RpcClientClearDrawings.
		if (Multiplayer.IsServer() && _networkManagerCache != null)
		{
			// Simplest way to clear for everyone is to send an empty full state
			string emptyLinesJson = Json.Stringify(new Godot.Collections.Array()); // Empty array
			_networkManagerCache.Rpc(nameof(NetworkManager.RpcClientReceiveAllDrawings), emptyLinesJson);
		}
	}

	public void AddNetworkedLine(DrawingLineData lineData)
	{
		if (lineData == null || lineData.Points == null || lineData.Points.Count < 2) return;
		_allLinesData.Add(lineData);
		QueueRedraw();
	}

	// Used by CampaignManager and NetworkManager (for new client sync)
	public List<DrawingLineData> GetDrawingData() => new List<DrawingLineData>(_allLinesData); // Return a copy

	public void ApplyDrawingData(List<DrawingLineData> drawingDataList)
	{
		_allLinesData.Clear(); // Clear existing local drawings
		if (drawingDataList != null)
		{
			_allLinesData.AddRange(drawingDataList);
		}
		QueueRedraw();
		GD.Print("DrawingOverlay: Applied full drawing data state.");
	}


	public void SetDrawingColor(Color color) { CurrentDrawColor = color; }
	public void SetLineThickness(float thickness) { LineThickness = Mathf.Max(1.0f, thickness); }
	public void DrawTemporaryMeasurement(Vector2 globalStart, Vector2 globalEnd, string text) { _tempMeasureStart = ToLocal(globalStart); _tempMeasureEnd = ToLocal(globalEnd); _tempMeasureText = text; _drawTemporaryMeasurement = true; QueueRedraw(); }
	public void ClearTemporaryMeasurement() { _drawTemporaryMeasurement = false; _tempMeasureText = null; _tempMeasureStart = Vector2.Zero; _tempMeasureEnd = Vector2.Zero; QueueRedraw(); }
}
