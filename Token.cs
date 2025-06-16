using Godot;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Select

public partial class Token : CharacterBody2D
{
	// Exports
	[Export] public bool HasVision { get; set; } = true;
	[Export] public float VisionRangeGameUnits { get; set; } = 6.0f;
	[Export] public float MoveSpeed { get; set; } = 200.0f;
	[Export] public Vector2 Size { get; set; } = new Vector2(128, 128);
	[Export] public long OwningPlayerId { get; set; } = 1;
	[Export] private NodePath _statusEffectIconContainerPath;

	// Public properties
	public bool IsSelected { get; set; } = false;

	private CharacterSheet _sheet;
	public CharacterSheet Sheet
	{
		get => _sheet;
		set
		{
			_sheet = value;
			if (IsNodeReady() && _statusEffectIconContainer != null)
			{
				UpdateStatusEffectVisuals();
			}
		}
	}

	private Texture2D _tokenTexture;
	[Export]
	public Texture2D TokenTexture
	{
		get => _tokenTexture;
		set { _tokenTexture = value; if (_spriteVisuals != null) _spriteVisuals.Texture = _tokenTexture; }
	}

	// Private fields
	private float _pixelsPerUnit = 50.0f;
	private Light2D _visionLight;
	private Sprite2D _spriteVisuals;
	private CollisionShape2D _collisionShape;
	private HBoxContainer _statusEffectIconContainer;

	private List<Vector2> _currentPath = null;
	private int _currentPathIndex = 0;
	private Vector2 _originalDragGlobalPosition;
	private bool _isBeingDragged = false;
	private float _distanceMovedThisTurnPixels = 0.0f;
	private float _pixelsPerUnitToken = 50.0f;
	private float _gameUnitsPerGridSquareToken = 5.0f;

	public Token() { Sheet = new CharacterSheet(); }

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

		if (_statusEffectIconContainerPath != null)
		{
			_statusEffectIconContainer = GetNodeOrNull<HBoxContainer>(_statusEffectIconContainerPath);
			if (_statusEffectIconContainer == null) GD.PrintErr($"Token {Name}: StatusEffectIconContainer not found at path: {_statusEffectIconContainerPath}");
		}
		else GD.PrintErr($"Token {Name}: _statusEffectIconContainerPath is not set in editor!");
	}

	public override void _Notification(int what)
	{
		if (what == NotificationReady)
		{
			if (Sheet != null && _statusEffectIconContainer != null) UpdateStatusEffectVisuals();
		}
	}

	public void UpdateStatusEffectVisuals()
	{
		if (_statusEffectIconContainer == null || !IsInstanceValid(_statusEffectIconContainer)) return;
		foreach (Node child in _statusEffectIconContainer.GetChildren()) child.QueueFree();
		if (Sheet == null || Sheet.ActiveStatusEffects == null) return;
		foreach (StatusEffect effect in Sheet.ActiveStatusEffects)
		{
			if (!string.IsNullOrEmpty(effect.IconPath))
			{
				Texture2D iconTex = null;
				if (ResourceLoader.Exists(effect.IconPath)) iconTex = ResourceLoader.Load<Texture2D>(effect.IconPath);
				else GD.PrintErr($"Token '{Name}': Status icon not found at '{effect.IconPath}' for '{effect.Name}'.");
				if (iconTex != null)
				{
					TextureRect iconRect = new TextureRect { Texture = iconTex, CustomMinimumSize = new Vector2(24, 24), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
					iconRect.TooltipText = $"{effect.Name}: {effect.Description}\nSource: {effect.Source}\nTurns: {(effect.DurationTurns <= 0 ? "Permanent" : $"{effect.RemainingTurns}/{effect.DurationTurns}")}";
					_statusEffectIconContainer.AddChild(iconRect);
				}
			}
		}
	}

	public void ApplyTokenData(TokenData data)
	{
		if (data == null) return;
		if (data.Position != null) GlobalPosition = data.Position.ToVector2();
		RotationDegrees = data.RotationDegrees;

		if (!string.IsNullOrEmpty(data.TexturePath) && ResourceLoader.Exists(data.TexturePath)) TokenTexture = ResourceLoader.Load<Texture2D>(data.TexturePath);
		else if (!string.IsNullOrEmpty(data.TexturePath)) GD.PrintErr($"Token {Name}: Failed to load texture from saved path: {data.TexturePath}");

		if (data.SheetData != null && data.SheetData.SheetAsDictionary != null)
		{
			// Use CharacterSheet's static FromDictionary method with the dictionary from the DTO
			Sheet = CharacterSheet.FromDictionary(data.SheetData.SheetAsDictionary);
		}
		else
		{
			Sheet = new CharacterSheet(); // Fallback to default if no sheet data
			GD.PrintErr($"Token {Name}: SheetData or SheetAsDictionary was null in TokenData. Applied default sheet.");
		}

		HasVision = data.HasVision;
		VisionRangeGameUnits = data.VisionRangeGameUnits;
		UpdateVisionLightProperties(); // Apply vision changes based on loaded data

		if (data.Size != null)
		{
			Size = data.Size.ToVector2();
			if (_collisionShape != null && _collisionShape.Shape is RectangleShape2D rectShape) rectShape.Size = Size;
			CollisionShape2D inputShape = GetNodeOrNull<CollisionShape2D>("InputCollisionShape2D");
			if (inputShape != null && inputShape.Shape is RectangleShape2D inputRectShape) inputRectShape.Size = Size;
		}
		OwningPlayerId = data.OwningPlayerId;

		// Sheet setter should call UpdateStatusEffectVisuals if node is ready.
		// If called during _Ready or before, NotificationReady will handle the first visual update.
		// Explicitly call here if Sheet setter doesn't or if called after _Ready.
		if(IsNodeReady()) UpdateStatusEffectVisuals();
	}

	// --- Other existing methods (abbreviated) ---
	public bool GetIsBeingDraggedState() => _isBeingDragged;
	public void StartDrag() { if (_currentPath != null) { _currentPath = null; Velocity = Vector2.Zero; } _isBeingDragged = true; _originalDragGlobalPosition = GlobalPosition; }
	public void UpdateDragPosition(Vector2 newGlobalPosition) { if (_isBeingDragged) GlobalPosition = newGlobalPosition; }
	public bool PerformCollisionCheckAndRevertIfFailed() { var ss = GetWorld2D().DirectSpaceState; var p = new PhysicsShapeQueryParameters2D { Shape = _collisionShape.Shape, Transform = GlobalTransform, CollisionMask = CollisionMask, Exclude = new Godot.Collections.Array<Rid>(new[] { GetRid() })}; var i = ss.IntersectShape(p); if (i.Count > 0) { GlobalPosition = _originalDragGlobalPosition; Velocity = Vector2.Zero; MoveAndSlide(); GD.Print($"Token {Sheet?.Name ?? Name}: Placement collision, reverted."); return false; } Velocity = Vector2.Zero; MoveAndSlide(); return true; }
	public void EndDragCleanup() { _isBeingDragged = false; }
	public Vector2 GetOriginalDragPosition() => _originalDragGlobalPosition;
	public void SetSelectionVisual(bool selected) { IsSelected = selected; if (_spriteVisuals != null) _spriteVisuals.Modulate = IsSelected ? Colors.LightBlue : Colors.White; }
	public void InitializeVision(float pixelsPerUnit) { _pixelsPerUnit = pixelsPerUnit; UpdateVisionLightProperties(); }
	private void UpdateVisionLightProperties() { if (_visionLight == null) return; _visionLight.Enabled = HasVision; if (HasVision) { float d = 128f; if (_visionLight.Texture != null) d = _visionLight.Texture.GetWidth(); if (d > 0) { float vd = VisionRangeGameUnits * _pixelsPerUnit * 2f; float s = vd / d; _visionLight.TextureScale = new Vector2(s, s); } else _visionLight.TextureScale = Vector2.One; } }
	public void MoveAlongPath(List<Vector2> path) { if (path == null || path.Count == 0) { _currentPath = null; Velocity = Vector2.Zero; return; } _currentPath = path; _currentPathIndex = 0; _isBeingDragged = false; Velocity = Vector2.Zero; }
	public override void _PhysicsProcess(double delta) { /* ... existing movement logic ... */ }
	public void InitializeMovementLimits(float pixelsPerUnit, float gameUnitsPerGridSquare) { _pixelsPerUnitToken = pixelsPerUnit; _gameUnitsPerGridSquareToken = gameUnitsPerGridSquare; }
	public void ResetTurnMovementStats() { _distanceMovedThisTurnPixels = 0.0f; _currentPath = null; Velocity = Vector2.Zero; }
}
