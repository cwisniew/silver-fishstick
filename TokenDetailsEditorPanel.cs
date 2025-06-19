using Godot;
using System;
using System.Linq;

public partial class TokenDetailsEditorPanel : PanelContainer
{
	// Status Effect Exports
	[Export] private Label selectedTokenNameLabel;
	[Export] private ItemList currentEffectsList;
	[Export] private Button removeSelectedEffectButton;
	[Export] private LineEdit effectNameInput;
	[Export] private LineEdit effectDescriptionInput;
	[Export] private SpinBox effectDurationInput;
	[Export] private LineEdit effectIconPathInput;
	[Export] private LineEdit effectSourceInput;
	[Export] private Button addEffectButton;

	// Inventory Exports
	[Export] private ItemList inventoryDisplayList;
	[Export] private Button removeSelectedItemButton;
	[Export] private Button equipSelectedItemButton;
	[Export] private LineEdit itemNameInput;
	[Export] private LineEdit itemDescriptionInput;
	[Export] private SpinBox itemQuantityInput;
	[Export] private LineEdit itemTypeInput;
	[Export] private SpinBox itemWeightInput;
	[Export] private CheckBox itemIsEquippableCheckbox;
	[Export] private Button addItemButton;


	private Token _currentTargetToken = null;
	private SoundManager _soundManager;

	// Status Effect Signals
	[Signal] public delegate void AddStatusEffectRequestedEventHandler(Token targetToken, StatusEffect effectData);
	[Signal] public delegate void RemoveStatusEffectRequestedEventHandler(Token targetToken, string effectName);
	// Inventory Signals
	[Signal] public delegate void AddItemToInventoryRequestedEventHandler(Token targetToken, InventoryItem itemData);
	[Signal] public delegate void RemoveItemFromInventoryRequestedEventHandler(Token targetToken, string itemName, int quantity); // For now, always remove 1
	[Signal] public delegate void ToggleEquipItemRequestedEventHandler(Token targetToken, string itemName);


	public override void _Ready()
	{
		_soundManager = GetNodeOrNull<SoundManager>("/root/SoundManager");

		// Status Effect UI
		addEffectButton.Pressed += OnAddEffectButtonPressed;
		removeSelectedEffectButton.Pressed += OnRemoveSelectedEffectButtonPressed;
		currentEffectsList.ItemSelected += OnEffectListItemSelected;

		// Inventory UI
		addItemButton.Pressed += OnAddItemButtonPressed;
		removeSelectedItemButton.Pressed += OnRemoveSelectedItemButtonPressed;
		equipSelectedItemButton.Pressed += OnEquipSelectedItemButtonPressed;
		inventoryDisplayList.ItemSelected += OnInventoryListItemSelected;

		SetTargetToken(null);
	}

	public void SetTargetToken(Token token)
	{
		_currentTargetToken = token;
		bool tokenValid = _currentTargetToken != null && IsInstanceValid(_currentTargetToken);

		if (tokenValid)
		{
			selectedTokenNameLabel.Text = $"Token: {_currentTargetToken.Sheet?.Name ?? _currentTargetToken.Name}";
			// Status Effects
			addEffectButton.Disabled = false;
			// Inventory
			addItemButton.Disabled = false;
		}
		else
		{
			selectedTokenNameLabel.Text = "Selected Token: None";
			// Status Effects
			addEffectButton.Disabled = true;
			removeSelectedEffectButton.Disabled = true;
			// Inventory
			addItemButton.Disabled = true;
			removeSelectedItemButton.Disabled = true;
			equipSelectedItemButton.Disabled = true;
		}
		RefreshEffectList();
		RefreshInventoryList();
	}

	// --- Status Effect Methods ---
	private void RefreshEffectList()
	{
		currentEffectsList.Clear();
		removeSelectedEffectButton.Disabled = true;
		if (_currentTargetToken != null && IsInstanceValid(_currentTargetToken) && _currentTargetToken.Sheet != null)
		{
			foreach (var effect in _currentTargetToken.Sheet.ActiveStatusEffects)
			{
				currentEffectsList.AddItem(effect.ToString());
			}
		}
	}
	private void OnEffectListItemSelected(long index) { removeSelectedEffectButton.Disabled = index < 0 || _currentTargetToken?.Sheet?.ActiveStatusEffects == null || index >= _currentTargetToken.Sheet.ActiveStatusEffects.Count; }
	private void OnAddEffectButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (_currentTargetToken == null || !IsInstanceValid(_currentTargetToken) || string.IsNullOrWhiteSpace(effectNameInput.Text)) return;
		StatusEffect newEffect = new StatusEffect(effectNameInput.Text.Trim(), (int)effectDurationInput.Value, effectDescriptionInput.Text, effectIconPathInput.Text, effectSourceInput.Text);
		EmitSignal(SignalName.AddStatusEffectRequested, _currentTargetToken, newEffect);
	}
	private void OnRemoveSelectedEffectButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (_currentTargetToken?.Sheet?.ActiveStatusEffects == null || currentEffectsList.GetSelectedItems().Length == 0) return;
		int idx = (int)currentEffectsList.GetSelectedItems()[0];
		if (idx >= 0 && idx < _currentTargetToken.Sheet.ActiveStatusEffects.Count)
		{
			EmitSignal(SignalName.RemoveStatusEffectRequested, _currentTargetToken, _currentTargetToken.Sheet.ActiveStatusEffects[idx].Name);
		}
	}

	// --- Inventory Methods ---
	private void RefreshInventoryList()
	{
		inventoryDisplayList.Clear();
		removeSelectedItemButton.Disabled = true;
		equipSelectedItemButton.Disabled = true;
		if (_currentTargetToken != null && IsInstanceValid(_currentTargetToken) && _currentTargetToken.Sheet != null)
		{
			foreach (var item in _currentTargetToken.Sheet.Inventory)
			{
				inventoryDisplayList.AddItem(item.ToString());
			}
		}
	}
	private void OnInventoryListItemSelected(long index)
	{
		bool validSelection = index >= 0 && _currentTargetToken?.Sheet?.Inventory != null && index < _currentTargetToken.Sheet.Inventory.Count;
		removeSelectedItemButton.Disabled = !validSelection;
		equipSelectedItemButton.Disabled = !validSelection || (validSelection && !_currentTargetToken.Sheet.Inventory[(int)index].IsEquippable);
	}
	private void OnAddItemButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (_currentTargetToken == null || !IsInstanceValid(_currentTargetToken) || string.IsNullOrWhiteSpace(itemNameInput.Text)) return;

		InventoryItem newItem = new InventoryItem(
			itemNameInput.Text.Trim(),
			(int)itemQuantityInput.Value,
			itemDescriptionInput.Text,
			itemTypeInput.Text.Trim(),
			(float)itemWeightInput.Value,
			itemIsEquippableCheckbox.ButtonPressed
		);
		EmitSignal(SignalName.AddItemToInventoryRequested, _currentTargetToken, newItem);
		// Clear inputs
		itemNameInput.Clear(); itemDescriptionInput.Clear(); itemQuantityInput.Value = 1;
		itemTypeInput.Clear(); itemWeightInput.Value = 0; itemIsEquippableCheckbox.ButtonPressed = false;
	}
	private void OnRemoveSelectedItemButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (_currentTargetToken?.Sheet?.Inventory == null || inventoryDisplayList.GetSelectedItems().Length == 0) return;
		int idx = (int)inventoryDisplayList.GetSelectedItems()[0];
		if (idx >= 0 && idx < _currentTargetToken.Sheet.Inventory.Count)
		{
			// For now, remove the whole stack (or the single item if not stackable)
			// Quantity handling can be added later (e.g. remove 1 from stack)
			EmitSignal(SignalName.RemoveItemFromInventoryRequested, _currentTargetToken, _currentTargetToken.Sheet.Inventory[idx].Name, _currentTargetToken.Sheet.Inventory[idx].Quantity);
		}
	}
	private void OnEquipSelectedItemButtonPressed()
	{
		_soundManager?.PlaySfx("ui_click.wav.txt");
		if (_currentTargetToken?.Sheet?.Inventory == null || inventoryDisplayList.GetSelectedItems().Length == 0) return;
		int idx = (int)inventoryDisplayList.GetSelectedItems()[0];
		if (idx >= 0 && idx < _currentTargetToken.Sheet.Inventory.Count)
		{
			if (_currentTargetToken.Sheet.Inventory[idx].IsEquippable)
			{
				EmitSignal(SignalName.ToggleEquipItemRequested, _currentTargetToken, _currentTargetToken.Sheet.Inventory[idx].Name);
			}
		}
	}
}
