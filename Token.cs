using Godot;
using System;
using System.Collections.Generic;

public partial class Token : CharacterBody2D
{
	public CharacterSheet Sheet { get; set; }
	public bool IsSelected { get; set; } = false;

	[Export] public bool HasVision { get; set; } = true;
	[Export] public float VisionRangeGameUnits { get; set; } = 6.0f;
	[Export] public float MoveSpeed { get; set; } = 200.0f; // Speed of animation/movement along path
	[Export] public Vector2 Size { get; set; } = new Vector2(128, 128);

	// Movement limit fields
	private float _distanceMovedThisTurnPixels = 0.0f;
	private float _pixelsPerUnitToken = 50.0f; // For internal calculations, set by MainScene
	private float _gameUnitsPerGridSquareToken = 5.0f; // For internal calculations, set by MainScene

	private float _pixelsPerUnit = 50.0f; // For vision, set by MainScene
	private Light2D _visionLight;
	private Sprite2D _spriteVisuals;
	private CollisionShape2D _collisionShape;
	// InputCollisionShape2D is used by Godot to send input events when pickable is true.

	private Texture2D _tokenTexture;
	[Export]
	public Texture2D TokenTexture
	{
		get => _tokenTexture;
		set
		{
			_tokenTexture = value;
			if (_spriteVisuals != null) _spriteVisuals.Texture = _tokenTexture;
		}
	}

	private List<Vector2> _currentPath = null;
	private int _currentPathIndex = 0;
	private Vector2 _originalDragGlobalPosition; // Store global position at drag start
	private bool _isBeingDragged = false;

	public override void _Ready()
	{
		_spriteVisuals = GetNodeOrNull<Sprite2D>("TokenSpriteVisuals");
		if (_spriteVisuals == null) GD.PrintErr($"Token {Name}: TokenSpriteVisuals node not found!");

		if (_tokenTexture != null && _spriteVisuals != null) _spriteVisuals.Texture = _tokenTexture;
		else if (_spriteVisuals != null)
		{
			_spriteVisuals.Texture = ResourceLoader.Load<Texture2D>("res://icon.svg");
			_tokenTexture = _spriteVisuals.Texture;
		}

		_visionLight = GetNodeOrNull<Light2D>("VisionLight");
		UpdateVisionLightProperties();

		_collisionShape = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
		CollisionShape2D inputShape = GetNodeOrNull<CollisionShape2D>("InputCollisionShape2D");

		if (_collisionShape != null && _collisionShape.Shape is RectangleShape2D rectShape) rectShape.Size = Size;
		if (inputShape != null && inputShape.Shape is RectangleShape2D inputRectShape) inputRectShape.Size = Size;
	}

	public bool GetIsBeingDraggedState() => _isBeingDragged;

	public void StartDrag()
	{
		if (_currentPath != null)
		{
			_currentPath = null;
			Velocity = Vector2.Zero;
			// Potentially emit signal: PathCancelledByDrag
		}
		_isBeingDragged = true;
		_originalDragGlobalPosition = GlobalPosition; // Store current global position
		// SetProcessInput(true); // If input is usually off, enable for drag
	}

	public void UpdateDragPosition(Vector2 newGlobalPosition)
	{
		if (_isBeingDragged) GlobalPosition = newGlobalPosition; // Directly set GlobalPosition during drag
	}

	// Called when drag operation finishes. Server might call this after tentatively setting position.
	// Client might call this before sending request if it wants to do a pre-check (though server is authoritative).
	public bool PerformCollisionCheckAndRevertIfFailed()
	{
		// This method assumes GlobalPosition has been set to the desired new position.
		// It checks if this new position is valid.
		// CharacterBody2D.TestMove() is good for this.
		// It requires a MotionParameters object.
		// For simplicity, if we are checking current position after a direct move (not physics step),
		// we can try a zero-length MoveAndSlide and see if it collides immediately,
		// or use PhysicsDirectSpaceState2D.

		// Using TestMove for a more direct check without actually moving (if it were a future move)
		// However, since GlobalPosition is already set, we check for overlaps.
		// A common way is to enable contact monitoring and check for bodies_colliding.
		// Or, simpler for this case: try a tiny movement and see if it collides.
		// If it does, the current spot is bad (already overlapping).

		var spaceState = GetWorld2D().DirectSpaceState;
		var parameters = new PhysicsShapeQueryParameters2D
		{
			Shape = _collisionShape.Shape, // Use the token's actual collision shape
			Transform = GlobalTransform,
			CollisionMask = this.CollisionMask, // Check against what this token is set to collide with (e.g., walls)
			Exclude = new Godot.Collections.Array<Rid>(new[] { GetRid() }) // Exclude self
		};

		var intersectingShapes = spaceState.IntersectShape(parameters);
		if (intersectingShapes.Count > 0)
		{
			// Check if any of the intersections are with something that should block
			// For now, any intersection is considered a block.
			GlobalPosition = _originalDragGlobalPosition; // Revert to position before drag started
			// Need to call MoveAndSlide after changing GlobalPosition directly if physics engine needs to be aware
			Velocity = Vector2.Zero; MoveAndSlide();
			GD.Print($"Token {Sheet?.Name ?? Name}: Placement collision at {GlobalPosition}, reverted to {_originalDragGlobalPosition}. Intersecting shapes: {intersectingShapes.Count}");
			return false; // Collision detected
		}

		// If no collision, ensure velocity is zeroed out from any prior movement.
		Velocity = Vector2.Zero;
		MoveAndSlide(); // Ensure physics state is updated with current GlobalPosition and zero velocity.
		return true; // Position is valid
	}

	public void EndDragCleanup() // Called after server confirms position or client reverts.
	{
		_isBeingDragged = false;
		// SetProcessInput(false); // If input was enabled for drag
	}


	public Vector2 GetOriginalDragPosition() => _originalDragGlobalPosition;


	public void SetSelectionVisual(bool selected)
	{
		IsSelected = selected;
		if (_spriteVisuals != null) _spriteVisuals.Modulate = IsSelected ? Colors.LightBlue : Colors.White;
	}

	public void InitializeVision(float pixelsPerUnit)
	{
		_pixelsPerUnit = pixelsPerUnit;
		UpdateVisionLightProperties();
	}

	private void UpdateVisionLightProperties()
	{
		if (_visionLight == null) return;
		_visionLight.Enabled = HasVision;
		if (HasVision)
		{
			float lightTextureOriginalDiameter = 128.0f;
			if (_visionLight.Texture != null) lightTextureOriginalDiameter = _visionLight.Texture.GetWidth();
			if (lightTextureOriginalDiameter > 0)
			{
				float desiredLightDiameterInPixels = VisionRangeGameUnits * _pixelsPerUnit * 2.0f;
				float scale = desiredLightDiameterInPixels / lightTextureOriginalDiameter;
				_visionLight.TextureScale = new Vector2(scale, scale);
			}
			else
			{
				_visionLight.TextureScale = Vector2.One;
				if (_visionLight.Texture == null) GD.PrintErr($"Token {Name}: VisionLight texture is null.");
				else GD.PrintErr($"Token {Name}: VisionLight texture has zero width.");
			}
		}
	}

	public void MoveAlongPath(List<Vector2> path)
	{
		if (path == null || path.Count == 0)
		{
			_currentPath = null;
			Velocity = Vector2.Zero;
			return;
		}
		_currentPath = path;
		_currentPathIndex = 0;
		_isBeingDragged = false;
		Velocity = Vector2.Zero;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isBeingDragged)
		{
			if (Velocity != Vector2.Zero) Velocity = Vector2.Zero;
			return;
		}

		if (_currentPath != null && _currentPathIndex < _currentPath.Count)
		{
			float maxAllowedPixelDistanceThisTurn = float.MaxValue; // Default to effectively infinite
			if (Sheet != null && Sheet.Speed > 0 && _gameUnitsPerGridSquareToken > 0 && _pixelsPerUnitToken > 0)
			{
				maxAllowedPixelDistanceThisTurn = (Sheet.Speed / _gameUnitsPerGridSquareToken) * _pixelsPerUnitToken;
			}
			else if (Sheet != null && Sheet.Speed <= 0) // Speed is 0 or less, no movement allowed
			{
				maxAllowedPixelDistanceThisTurn = 0;
			}

			float remainingMovementBudget = maxAllowedPixelDistanceThisTurn - _distanceMovedThisTurnPixels;

			if (remainingMovementBudget <= 0.01f) // Effectively no budget left (use small epsilon)
			{
				if (_currentPath != null) // If there was a path, it's now interrupted by lack of movement
				{
					// GD.Print($"Token {Sheet?.Name ?? Name}: Movement budget exhausted for this turn.");
					// Consider emitting signal for chat log
				}
				_currentPath = null;
				Velocity = Vector2.Zero;
				MoveAndSlide();
				return;
			}

			Vector2 currentPosition = GlobalPosition;
			Vector2 nextWaypoint = _currentPath[_currentPathIndex];
			float distanceToNextWaypoint = currentPosition.DistanceTo(nextWaypoint);
			Vector2 direction = (nextWaypoint - currentPosition).Normalized();

			if (distanceToNextWaypoint > remainingMovementBudget) // Can only move partially towards next waypoint
			{
				Vector2 partialTarget = currentPosition + direction * remainingMovementBudget;
				Velocity = direction * MoveSpeed;

				float distToPartialTarget = currentPosition.DistanceTo(partialTarget);
				float moveThisFrame = Velocity.Length() * (float)delta;

				if (moveThisFrame >= distToPartialTarget - 0.1f) // If we can reach or overshoot partial target (with tolerance)
				{
					GlobalPosition = partialTarget; // Snap to partial target
					Velocity = Vector2.Zero;        // Stop
				}
				// else: Velocity is already set to move towards partialTarget

				MoveAndSlide();
				_distanceMovedThisTurnPixels += currentPosition.DistanceTo(GlobalPosition); // Add actual distance moved

				// GD.Print($"Token {Sheet?.Name ?? Name}: Partially moved. Budget exhausted.");
				// Consider emitting signal for chat log
				_currentPath = null; // Stop further path movement this turn
			}
			else // Can reach next waypoint and potentially more
			{
				Velocity = direction * MoveSpeed;
				float distToNextWaypoint = currentPosition.DistanceTo(nextWaypoint);
				float moveThisFrame = Velocity.Length() * (float)delta;

				Vector2 previousPosBeforeMove = currentPosition;

				if (moveThisFrame >= distToNextWaypoint - 0.1f) // If we can reach or overshoot waypoint (with tolerance)
				{
					GlobalPosition = nextWaypoint; // Snap to waypoint
					Velocity = Vector2.Zero;       // Stop precisely at waypoint before advancing index
				}
				// else: Velocity is already set to move towards nextWaypoint

				MoveAndSlide(); // Apply movement
				_distanceMovedThisTurnPixels += previousPosBeforeMove.DistanceTo(GlobalPosition); // Add actual distance moved

				// Check if at waypoint after moving (especially if we didn't snap)
				if (GlobalPosition.IsEqualApprox(nextWaypoint, 0.5f)) // Use IsEqualApprox for float comparison
				{
					GlobalPosition = nextWaypoint; // Ensure exact position
					_currentPathIndex++;
					if (_currentPathIndex >= _currentPath.Count)
					{
						_currentPath = null; // Path complete
						// Velocity already zero if snapped, or will be zeroed below
					}
				}
			}

			if (_currentPath == null && Velocity != Vector2.Zero) // Path completed or budget exhausted
			{
				Velocity = Vector2.Zero;
				MoveAndSlide(); // Ensure velocity is applied if it was changed to zero
			}
		}
		else // No path, or path completed in a previous frame
		{
			if (Velocity != Vector2.Zero)
			{
				Velocity = Vector2.Zero;
				MoveAndSlide();
			}
		}

	public void ApplyTokenData(TokenData data)
	{
		if (data == null) return;

		if (data.Position != null) GlobalPosition = data.Position.ToVector2();
		RotationDegrees = data.RotationDegrees;

		if (!string.IsNullOrEmpty(data.TexturePath) && ResourceLoader.Exists(data.TexturePath))
		{
			TokenTexture = ResourceLoader.Load<Texture2D>(data.TexturePath);
		}
		else if (!string.IsNullOrEmpty(data.TexturePath))
		{
			GD.PrintErr($"Token {Name}: Failed to load texture from saved path: {data.TexturePath}");
		}
		// else, keep existing/default texture if no path is provided

		if (data.SheetData != null)
		{
			Sheet = data.SheetData.ToCharacterSheet(); // Assumes CharacterSheet class has matching properties
		}

		HasVision = data.HasVision;
		VisionRangeGameUnits = data.VisionRangeGameUnits;
		UpdateVisionLightProperties(); // Apply vision changes

		if (data.Size != null)
		{
			Size = data.Size.ToVector2(); // Update Size property
			// And re-apply to collision shapes if necessary
			if (_collisionShape != null && _collisionShape.Shape is RectangleShape2D rectShape) rectShape.Size = Size;
			CollisionShape2D inputShape = GetNodeOrNull<CollisionShape2D>("InputCollisionShape2D");
			if (inputShape != null && inputShape.Shape is RectangleShape2D inputRectShape) inputRectShape.Size = Size;
		}


		// Name might be set by MainScene during spawn if needed, or here from data.NodeName
		// For now, let MainScene handle NodeName if it's used for tracking during load.
	}
	}

	public void InitializeMovementLimits(float pixelsPerUnit, float gameUnitsPerGridSquare)
	{
		_pixelsPerUnitToken = pixelsPerUnit;
		_gameUnitsPerGridSquareToken = gameUnitsPerGridSquare;
	}

	public void ResetTurnMovementStats()
	{
		_distanceMovedThisTurnPixels = 0.0f;
		if (_currentPath != null)
		{
			// GD.Print($"Token {Sheet?.Name ?? Name}: Path interrupted by turn ending.");
			// Consider emitting signal for chat log
		}
		_currentPath = null;
		Velocity = Vector2.Zero;
		// Call MoveAndSlide here if there's a chance velocity wasn't applied,
		// but usually _PhysicsProcess handles applying zero velocity.
	}
}
