using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class CombatTracker : PanelContainer
{
	[Export] private VBoxContainer trackerListVBox;
	[Export] private Label roundCounterLabel;

	private List<Combatant> _combatants = new List<Combatant>();
	private int _currentTurnIndex = -1;
	private int _roundNumber = 0;
	private bool _combatStarted = false;

	private ChatLog _chatLog;
	private SoundManager _soundManager;
	private string _gameUnitName = "ft";

	public void Initialize(ChatLog chatLog, SoundManager soundManager, string gameUnitName = "ft")
	{
		_chatLog = chatLog;
		_soundManager = soundManager;
		_gameUnitName = gameUnitName;
	}

	public override void _Ready()
	{
		if (trackerListVBox == null)
			trackerListVBox = GetNodeOrNull<VBoxContainer>("VBoxContainer/TrackerListVBox");
		if (roundCounterLabel == null)
			roundCounterLabel = GetNodeOrNull<Label>("VBoxContainer/RoundCounterLabel");

		if (trackerListVBox == null) GD.PrintErr("CombatTracker: TrackerListVBox is null!");
		if (roundCounterLabel == null) GD.PrintErr("CombatTracker: RoundCounterLabel is null!");

		UpdateDisplay();
	}

	public void AddCombatantEntry(Combatant combatant)
	{
		if (combatant == null) return;
		if (combatant.LinkedToken != null && _combatants.Any(c => c.LinkedToken == combatant.LinkedToken)) {
			_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} is already in combat.", Colors.Orange);
			return;
		}
		_combatants.Add(combatant);
		_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} (Init: {combatant.Initiative}) added.", Colors.CornflowerBlue);
		if (_combatStarted) SortCombatants();
		UpdateDisplay();
	}

	public void RemoveCombatant(Combatant combatant)
	{
		if (combatant == null || !_combatants.Contains(combatant)) return;
		_combatants.Remove(combatant);
		_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} removed.", Colors.Orange);
		if (_combatStarted && _combatants.Count == 0) ResetCombat();
		else if (_combatStarted)
		{
			if (_currentTurnIndex >= _combatants.Count) {
				_currentTurnIndex = _combatants.Count > 0 ? 0 : -1;
			}
		}
		UpdateDisplay();
	}

	private void SortCombatants()
	{
		_combatants = _combatants.OrderByDescending(c => c.Initiative)
								 .ThenByDescending(c => c.LinkedToken?.Sheet?.Speed ?? 0)
								 .ToList();
	}

	public void StartCombat()
	{
		if (_combatants.Count == 0)
		{
			_chatLog?.AddMessage("Combat Tracker: Cannot start combat. No combatants added.", Colors.OrangeRed);
			return;
		}
		_combatStarted = true;
		_roundNumber = 1;
		SortCombatants();
		_currentTurnIndex = 0;
		_chatLog?.AddMessage($"--- Combat Started! Round {_roundNumber} ---", Colors.Crimson);
		if (_combatants.Count > 0 && _combatants[_currentTurnIndex].LinkedToken != null)
		{
			_combatants[_currentTurnIndex].LinkedToken.ResetTurnMovementStats();
		}
		LogCurrentTurn();
		UpdateDisplay();
	}

	public void NextTurn()
	{
		if (!_combatStarted || _combatants.Count == 0)
		{
			_chatLog?.AddMessage("Combat Tracker: Combat not started or no combatants.", Colors.Orange);
			return;
		}
		_soundManager?.PlaySfx("ui_click.wav.txt");
		_currentTurnIndex++;
		if (_currentTurnIndex >= _combatants.Count)
		{
			_currentTurnIndex = 0;
			_roundNumber++;
			_chatLog?.AddMessage($"--- Round {_roundNumber} ---", Colors.Crimson);
		}
		if (_combatants.Count > 0 && _combatants[_currentTurnIndex].LinkedToken != null)
		{
			_combatants[_currentTurnIndex].LinkedToken.ResetTurnMovementStats();
		}
		LogCurrentTurn();
		UpdateDisplay();
	}

	private void LogCurrentTurn()
	{
		if (_currentTurnIndex >= 0 && _currentTurnIndex < _combatants.Count)
		{
			Combatant current = _combatants[_currentTurnIndex];
			string speedInfo = "";
			if (current.LinkedToken != null && current.LinkedToken.Sheet != null)
			{
				speedInfo = $" (Speed: {current.LinkedToken.Sheet.Speed} {_gameUnitName})";
			}
			_chatLog?.AddMessage($"Turn: {current.DisplayText} (Init: {current.Initiative}){speedInfo}", Colors.LightSeaGreen);
		}
	}

	public CombatTrackerData GetCombatTrackerData()
	{
		var data = new CombatTrackerData
		{
			Combatants = new List<CombatantData>(),
			CurrentTurnIndex = _currentTurnIndex,
			RoundNumber = _roundNumber,
			CombatStarted = _combatStarted
		};
		foreach (var combatant in _combatants)
		{
			data.Combatants.Add(new CombatantData {
				Name = combatant.Name, // Or DisplayText
				Initiative = combatant.Initiative,
				LinkedTokenNodeName = combatant.LinkedToken?.Name.ToString() ?? string.Empty
			});
		}
		return data;
	}

	public void ApplyCombatTrackerData(CombatTrackerData data, Godot.Collections.Array<Token> allSceneTokens)
	{
		// Don't call ResetCombat() here as it logs and might clear things unnecessarily before load.
		// Instead, directly reset internal state.
		_combatants.Clear();
		_currentTurnIndex = -1;
		_roundNumber = 0;
		_combatStarted = false;
		// UpdateDisplay(); // Update to clear visuals immediately

		if (data == null) {
			UpdateDisplay(); // Ensure UI is cleared if data is null
			return;
		}

		foreach (var cData in data.Combatants)
		{
			Token linkedToken = null;
			if (!string.IsNullOrEmpty(cData.LinkedTokenNodeName))
			{
				foreach(var t in allSceneTokens)
				{
					if (t.Name == cData.LinkedTokenNodeName)
					{
						linkedToken = t;
						break;
					}
				}
				if (linkedToken == null)
					_chatLog?.AddMessage($"CombatTracker Load Warning: Could not find token with name '{cData.LinkedTokenNodeName}' for combatant '{cData.Name}'.", Colors.Yellow);
			}
			_combatants.Add(new Combatant(cData.Name, cData.Initiative, linkedToken));
		}

		_currentTurnIndex = data.CurrentTurnIndex;
		_roundNumber = data.RoundNumber;
		_combatStarted = data.CombatStarted;

		if (_combatStarted)
		{
			if (_currentTurnIndex < 0 || _currentTurnIndex >= _combatants.Count)
			{
				_currentTurnIndex = (_combatants.Count > 0) ? 0 : -1;
			}
			if (_currentTurnIndex != -1 && _combatants.Count > 0)
			{
				_combatants[_currentTurnIndex].LinkedToken?.ResetTurnMovementStats();
			}
		}

		UpdateDisplay();
		if (_combatStarted && _currentTurnIndex != -1 && _combatants.Count > 0)
		{
			LogCurrentTurn();
		}
		_chatLog?.AddMessage("Combat state restored.", Colors.CornflowerBlue);
	}

	public void ResetCombat()
	{
		_combatants.Clear();
		_currentTurnIndex = -1;
		_roundNumber = 0;
		_combatStarted = false;
		_chatLog?.AddMessage("--- Combat Reset ---", Colors.MediumPurple);
		UpdateDisplay();
	}

	private void UpdateDisplay()
	{
		if (trackerListVBox == null || roundCounterLabel == null)
		{
			GD.PrintErr("CombatTracker: UI elements not ready for display update.");
			return;
		}
		foreach (Node child in trackerListVBox.GetChildren()) child.QueueFree();
		roundCounterLabel.Text = _combatStarted ? $"Round: {_roundNumber}" : "Round: 0 (Combat Ended)";
		if (!_combatStarted && _combatants.Count > 0) roundCounterLabel.Text = "Pre-Combat";

		for (int i = 0; i < _combatants.Count; i++)
		{
			Combatant combatant = _combatants[i];
			Label entryLabel = new Label();
			entryLabel.Text = $"I:{combatant.Initiative} - {combatant.DisplayText}";
			if (_combatStarted && i == _currentTurnIndex)
			{
				entryLabel.Modulate = Colors.YellowGreen;
				entryLabel.Text = $"> {entryLabel.Text}";
			}
			else entryLabel.Modulate = Colors.White;
			trackerListVBox.AddChild(entryLabel);
		}
	}
}
