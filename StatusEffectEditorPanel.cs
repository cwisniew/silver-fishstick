using Godot;
using System;
using System.Linq; // For FirstOrDefault

public partial class StatusEffectEditorPanel : PanelContainer
{
	[Export] private Label selectedTokenNameLabel;
	[Export] private ItemList currentEffectsList;
	[Export] private Button removeSelectedEffectButton;
	[Export] private LineEdit effectNameInput;
	[Export] private LineEdit effectDescriptionInput;
	[Export] private SpinBox effectDurationInput;
	[Export] private LineEdit effectIconPathInput;
	[Export] private LineEdit effectSourceInput;
	[Export] private Button addEffectButton;

	private Token _currentTargetToken = null;
	private SoundManager _soundManager;

	// Events to notify MainScene (server-side) about requested changes
	[Signal] public delegate void AddStatusEffectRequestedEventHandler(Token targetToken, StatusEffect effectData);
	[Signal] public delegate void RemoveStatusEffectRequestedEventHandler(Token targetToken, string effectName);

	public override void _Ready()
	{
		_soundManager = GetNodeOrNull<SoundManager>("/root/SoundManager");

		addEffectButton.Pressed += OnAddEffectButtonPressed;
		removeSelectedEffectButton.Pressed += OnRemoveSelectedEffectButtonPressed;
		currentEffectsList.ItemSelected += OnEffectListItemSelected; // To enable/disable remove button

		SetTargetToken(null); // Initialize UI in a disabled/default state
	}

	public void SetTargetToken(Token token)
	{
		_currentTargetToken = token;
		if (_currentTargetToken != null && IsInstanceValid(_currentTargetToken)) // Check if token is valid (not freed)
		{
			selectedTokenNameLabel.Text = $"Token: {_currentTargetToken.Sheet?.Name ?? _currentTargetToken.Name}";
			addEffectButton.Disabled = false;
			// Remove button is handled by item selection
		}
		else
		{
			selectedTokenNameLabel.Text = "Selected Token: None";
			addEffectButton.Disabled = true;
			removeSelectedEffectButton.Disabled = true;
		}
		RefreshEffectList();
	}

	private void RefreshEffectList()
	{
		currentEffectsList.Clear();
		removeSelectedEffectButton.Disabled = true; // Disable by default

		if (_currentTargetToken != null && IsInstanceValid(_currentTargetToken) && _currentTargetToken.Sheet != null)
		{
			foreach (var effect in _currentTargetToken.Sheet.ActiveStatusEffects)
			{
				currentEffectsList.AddItem(effect.ToString());
			}
		}
	}

	private void OnEffectListItemSelected(long index)
	{
		// Enable remove button only if a valid item is selected
		removeSelectedEffectButton.Disabled = index < 0 ||
											  _currentTargetToken == null ||
											  _currentTargetToken.Sheet == null ||
											  index >= _currentTargetToken.Sheet.ActiveStatusEffects.Count;
	}


	private void OnAddEffectButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (_currentTargetToken == null || !IsInstanceValid(_currentTargetToken))
		{
			GD.PrintErr("StatusEffectEditor: No target token or token is invalid.");
			return;
		}
		if (string.IsNullOrWhiteSpace(effectNameInput.Text))
		{
			GD.PrintErr("StatusEffectEditor: Effect name cannot be empty.");
			// Optionally, show an error message to the user in UI
			return;
		}

		StatusEffect newEffect = new StatusEffect(
			effectNameInput.Text.Trim(),
			(int)effectDurationInput.Value,
			effectDescriptionInput.Text,
			effectIconPathInput.Text,
			effectSourceInput.Text
		);

		EmitSignal(SignalName.AddStatusEffectRequested, _currentTargetToken, newEffect);

		// Clear inputs after emitting, but RefreshEffectList will be called by MainScene after state change
		// effectNameInput.Clear();
		// effectDescriptionInput.Clear();
		// effectDurationInput.Value = 0;
		// effectIconPathInput.Clear();
		// effectSourceInput.Clear();
	}

	private void OnRemoveSelectedEffectButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (_currentTargetToken == null || !IsInstanceValid(_currentTargetToken) || currentEffectsList.GetSelectedItems().Length == 0)
		{
			GD.PrintErr("StatusEffectEditor: No target token or no effect selected for removal.");
			return;
		}

		int selectedIndex = (int)currentEffectsList.GetSelectedItems()[0];
		if (selectedIndex >= 0 && selectedIndex < _currentTargetToken.Sheet.ActiveStatusEffects.Count)
		{
			string effectNameToRemove = _currentTargetToken.Sheet.ActiveStatusEffects[selectedIndex].Name;
			EmitSignal(SignalName.RemoveStatusEffectRequested, _currentTargetToken, effectNameToRemove);
		}
		// RefreshEffectList will be called by MainScene after state change
	}
}
