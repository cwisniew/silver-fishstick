using Godot;
using System;
using System.Collections.Generic;

public partial class Token : CharacterBody2D
{
	public CharacterSheet Sheet { get; set; }
	public bool IsSelected { get; set; } = false;

	[Export] public bool HasVision { get; set; } = true;
	[Export] public float VisionRangeGameUnits { get; set; } = 6.0f;
	[Export] public float MoveSpeed { get; set; } = 200.0f;
	[Export] public Vector2 Size { get; set; } = new Vector2(128, 128);
	[Export] public long OwningPlayerId { get; set; } = 1; // Default to 1 (server/GM)

	private float _pixelsPerUnit = 50.0f;
	private Light2D _visionLight;
	private Sprite2D _spriteVisuals;
	private CollisionShape2D _collisionShape;

	private Texture2D _tokenTexture;
	[Export]
	public Texture2D TokenTexture
	{
		get => _tokenTexture;
		set { _tokenTexture = value; if (_spriteVisuals != null) _spriteVisuals.Texture = _tokenTexture; }
	}

	private List<Vector2> _currentPath = null;
	private int _currentPathIndex = 0;
	private Vector2 _originalDragGlobalPosition;
	private bool _isBeingDragged = false;

	public Token() { Sheet = new CharacterSheet(); } // Initialize default sheet

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
		if (Sheet == null) Sheet = new CharacterSheet();
	}

	public bool GetIsBeingDraggedState() => _isBeingDragged;

	public void StartDrag()
	{
		if (_currentPath != null) { _currentPath = null; Velocity = Vector2.Zero; }
		_isBeingDragged = true;
		_originalDragGlobalPosition = GlobalPosition;
	}

	public void UpdateDragPosition(Vector2 newGlobalPosition) { if (_isBeingDragged) GlobalPosition = newGlobalPosition; }

	public bool PerformCollisionCheckAndRevertIfFailed()
	{
		var spaceState = GetWorld2D().DirectSpaceState;
		var parameters = new PhysicsShapeQueryParameters2D
		{
			Shape = _collisionShape.Shape, Transform = GlobalTransform, CollisionMask = this.CollisionMask, Exclude = new Godot.Collections.Array<Rid>(new[] { GetRid() })
		};
		var intersectingShapes = spaceState.IntersectShape(parameters);
		if (intersectingShapes.Count > 0)
		{
			GlobalPosition = _originalDragGlobalPosition;
			Velocity = Vector2.Zero; MoveAndSlide();
			GD.Print($"Token {Sheet?.Name ?? Name}: Placement collision, reverted to {_originalDragGlobalPosition}. Intersecting: {intersectingShapes.Count}");
			return false;
		}
		Velocity = Vector2.Zero; MoveAndSlide();
		return true;
	}

	public void EndDragCleanup() { _isBeingDragged = false; }
	public Vector2 GetOriginalDragPosition() => _originalDragGlobalPosition;
	public void SetSelectionVisual(bool selected) { IsSelected = selected; if (_spriteVisuals != null) _spriteVisuals.Modulate = IsSelected ? Colors.LightBlue : Colors.White; }
	public void InitializeVision(float pixelsPerUnit) { _pixelsPerUnit = pixelsPerUnit; UpdateVisionLightProperties(); }

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
			} else { _visionLight.TextureScale = Vector2.One; if (_visionLight.Texture == null) GD.PrintErr($"Token {Name}: VisionLight texture null."); else GD.PrintErr($"Token {Name}: VisionLight texture zero width.");}
		}
	}

	public void MoveAlongPath(List<Vector2> path)
	{
		if (path == null || path.Count == 0) { _currentPath = null; Velocity = Vector2.Zero; return; }
		_currentPath = path; _currentPathIndex = 0; _isBeingDragged = false; Velocity = Vector2.Zero;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isBeingDragged) { if (Velocity != Vector2.Zero) Velocity = Vector2.Zero; return; }
		if (_currentPath != null && _currentPathIndex < _currentPath.Count)
		{
			float maxAllowedPixelDistanceThisTurn = float.MaxValue;
			if (Sheet != null && Sheet.Speed > 0 && _gameUnitsPerGridSquareToken > 0 && _pixelsPerUnitToken > 0) maxAllowedPixelDistanceThisTurn = (Sheet.Speed / _gameUnitsPerGridSquareToken) * _pixelsPerUnitToken;
			else if (Sheet != null && Sheet.Speed <= 0) maxAllowedPixelDistanceThisTurn = 0;
			float remainingMovementBudget = maxAllowedPixelDistanceThisTurn - _distanceMovedThisTurnPixels;

			if (remainingMovementBudget <= 0.01f) { _currentPath = null; Velocity = Vector2.Zero; MoveAndSlide(); return; }

			Vector2 currentPosition = GlobalPosition;
			Vector2 nextWaypoint = _currentPath[_currentPathIndex];
			float distanceToNextWaypoint = currentPosition.DistanceTo(nextWaypoint);
			Vector2 direction = (nextWaypoint - currentPosition).Normalized();

			if (distanceToNextWaypoint > remainingMovementBudget)
			{
				Vector2 partialTarget = currentPosition + direction * remainingMovementBudget;
				Velocity = direction * MoveSpeed;
				float distToPartialTarget = currentPosition.DistanceTo(partialTarget);
				float moveThisFrame = Velocity.Length() * (float)delta;
				if (moveThisFrame >= distToPartialTarget - 0.1f) { GlobalPosition = partialTarget; Velocity = Vector2.Zero; }
				MoveAndSlide();
				_distanceMovedThisTurnPixels += currentPosition.DistanceTo(GlobalPosition);
				_currentPath = null;
			}
			else
			{
				Velocity = direction * MoveSpeed;
				Vector2 previousPosBeforeMove = currentPosition;
				if (Velocity.Length() * (float)delta >= distanceToNextWaypoint - 0.1f) { GlobalPosition = nextWaypoint; Velocity = Vector2.Zero; }
				MoveAndSlide();
				_distanceMovedThisTurnPixels += previousPosBeforeMove.DistanceTo(GlobalPosition);
				if (GlobalPosition.IsEqualApprox(nextWaypoint, 0.5f)) { GlobalPosition = nextWaypoint; _currentPathIndex++; if (_currentPathIndex >= _currentPath.Count) _currentPath = null; }
			}
			if (_currentPath == null && Velocity != Vector2.Zero) { Velocity = Vector2.Zero; MoveAndSlide(); }
		} else { if (Velocity != Vector2.Zero) { Velocity = Vector2.Zero; MoveAndSlide(); } }
	}

	public void InitializeMovementLimits(float pixelsPerUnit, float gameUnitsPerGridSquare) { _pixelsPerUnitToken = pixelsPerUnit; _gameUnitsPerGridSquareToken = gameUnitsPerGridSquare; }
	public void ResetTurnMovementStats() { _distanceMovedThisTurnPixels = 0.0f; if (_currentPath != null) {} _currentPath = null; Velocity = Vector2.Zero; }

	public void ApplyTokenData(TokenData data)
	{
		if (data == null) return;
		if (data.Position != null) GlobalPosition = data.Position.ToVector2();
		RotationDegrees = data.RotationDegrees;
		if (!string.IsNullOrEmpty(data.TexturePath) && ResourceLoader.Exists(data.TexturePath)) TokenTexture = ResourceLoader.Load<Texture2D>(data.TexturePath);
		else if (!string.IsNullOrEmpty(data.TexturePath)) GD.PrintErr($"Token {Name}: Failed to load texture from saved path: {data.TexturePath}");
		if (data.SheetData != null) Sheet = data.SheetData.ToCharacterSheet(); else Sheet = new CharacterSheet(); // Ensure sheet is not null
		HasVision = data.HasVision;
		VisionRangeGameUnits = data.VisionRangeGameUnits;
		UpdateVisionLightProperties();
		if (data.Size != null)
		{
			Size = data.Size.ToVector2();
			if (_collisionShape != null && _collisionShape.Shape is RectangleShape2D rectShape) rectShape.Size = Size;
			CollisionShape2D inputShape = GetNodeOrNull<CollisionShape2D>("InputCollisionShape2D");
			if (inputShape != null && inputShape.Shape is RectangleShape2D inputRectShape) inputRectShape.Size = Size;
		}
		OwningPlayerId = data.OwningPlayerId;
	}
}
