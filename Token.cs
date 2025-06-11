using Godot;
using System;

public partial class Token : Sprite2D
{
	[Export]
	public Texture2D TokenTexture { get; set; }

	[Export]
	public Vector2 Size { get; set; } = new Vector2(128, 128); // Default size

	private bool _isDragging = false;
	private CollisionShape2D _collisionShape;

	public CharacterSheet Sheet { get; set; }
	public bool IsSelected { get; set; } = false;

	private Texture2D _tokenTexture;
	[Export]
	public Texture2D TokenTexture
	{
		get => _tokenTexture;
		set
		{
			_tokenTexture = value;
			// Since this script is on the Sprite2D itself (TokenSprite)
			this.Texture = _tokenTexture;
		}
	}

	public override void _Ready()
	{
		if (_tokenTexture != null)
		{
			this.Texture = _tokenTexture;
		}
		else
		{
			// Load a default if TokenTexture is null at ready
			this.Texture = ResourceLoader.Load<Texture2D>("res://icon.svg");
			_tokenTexture = this.Texture; // Ensure the backing field is also updated
		}

		// Assuming Size is meant to control the collision shape size for now
		_collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
		if (_collisionShape != null && _collisionShape.Shape is RectangleShape2D rectShape)
		{
			rectShape.Size = Size;
		}
		else
		{
			GD.PrintErr("CollisionShape2D not found or is not a RectangleShape2D.");
		}
	}

	public override void _Process(double delta)
	{
		if (_isDragging)
		{
			GlobalPosition = GetGlobalMousePosition();
		}
	}

	public override void _InputEvent(Viewport viewport, InputEvent @event, int shapeIdx)
	{
		if (@event is InputEventMouseButton mouseButtonEvent)
		{
			if (mouseButtonEvent.ButtonIndex == MouseButton.Left)
			{
				// Dragging logic remains the same
				if (mouseButtonEvent.Pressed)
				{
					// The actual selection change and ensuring single selection
					// will be handled by MainScene connecting to this token's InputEvent.
					// Here, we only set _isDragging if it's a new press.
					// If it was already selected and we click again, it might be a drag start.
					if (!IsSelected) // If not selected, it could be a click to select OR start drag
					{
						// Defer selection state change to MainScene to ensure single selection
					}
					_isDragging = true;
				}
				else // Mouse button released
				{
					// If it was a drag, stop dragging.
					// If it was a click (not a drag that moved the mouse significantly),
					// MainScene would have handled the selection toggle.
					if(_isDragging) _isDragging = false;
				}
			}
			// Right-click or other buttons could be handled here if needed
		}
	}

	public bool IsDragging()
	{
		return _isDragging;
	}

	// Method for MainScene to call to update visual selection state
	public void SetSelectionVisual(bool selected)
	{
		IsSelected = selected;
		if (IsSelected)
		{
			this.Modulate = Colors.LightBlue;
		}
		else
		{
			this.Modulate = Colors.White;
		}
	}
}
