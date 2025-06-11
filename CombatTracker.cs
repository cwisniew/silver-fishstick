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

	private ChatLog _chatLog; // To be set by MainScene

	public void Initialize(ChatLog chatLog)
	{
		_chatLog = chatLog;
	}

	public override void _Ready()
	{
		// Ensure nodes are valid if not using [Export] or if paths are complex
		// For this setup, [Export] should work if NodePaths in MainScene.cs are correct
		// and MainScene correctly assigns them.
		// However, the NodePaths for trackerListVBox and roundCounterLabel are internal to this scene,
		// so they should be assigned directly in this script if not exported from MainScene.
		// The current MainScene.tscn setup has these as children of CombatTrackerPanel,
		// so we can use GetNode for them if not relying on MainScene to export them.

		// If MainScene doesn't export these specific children, get them directly:
		if (trackerListVBox == null)
			trackerListVBox = GetNode<VBoxContainer>("VBoxContainer/TrackerListVBox");
		if (roundCounterLabel == null)
			roundCounterLabel = GetNode<Label>("VBoxContainer/RoundCounterLabel");

		if (trackerListVBox == null) GD.PrintErr("CombatTracker: TrackerListVBox is null!");
		if (roundCounterLabel == null) GD.PrintErr("CombatTracker: RoundCounterLabel is null!");

		UpdateDisplay();
	}

	public void AddCombatantEntry(Combatant combatant)
	{
		if (combatant == null) return;
		// Prevent adding the same token twice, could check by combatant.LinkedToken if it's not null
		if (combatant.LinkedToken != null && _combatants.Any(c => c.LinkedToken == combatant.LinkedToken)) {
			_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} is already in combat.", Colors.Orange);
			return;
		}

		_combatants.Add(combatant);
		_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} (Init: {combatant.Initiative}) added.", Colors.CornflowerBlue);

		if (_combatStarted)
		{
			SortCombatants();
			// If current turn was affected, might need to adjust _currentTurnIndex
		}
		UpdateDisplay();
	}

	public void RemoveCombatant(Combatant combatant) // TODO: Needs a way to call this (e.g., right-click menu on entry)
	{
		if (combatant == null || !_combatants.Contains(combatant)) return;

		_combatants.Remove(combatant);
		_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} removed.", Colors.Orange);

		if (_combatStarted && _combatants.Count == 0)
		{
			ResetCombat(); // Or handle empty combat differently
		}
		else if (_combatStarted)
		{
			// Adjust _currentTurnIndex if the removed combatant was before or at the current turn
			// This is a simplified adjustment; more robust logic might be needed
			if (_currentTurnIndex >= _combatants.Count) {
				_currentTurnIndex = _combatants.Count - 1; // Ensure index is valid
				if (_currentTurnIndex < 0 && _combatants.Count > 0) _currentTurnIndex = 0; // If list not empty
			}
		}
		UpdateDisplay();
	}

	private void SortCombatants()
	{
		_combatants = _combatants.OrderByDescending(c => c.Initiative)
								 .ThenByDescending(c => c.LinkedToken?.Sheet?.Speed ?? 0) // Tie-breaker (example)
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

		_currentTurnIndex++;
		if (_currentTurnIndex >= _combatants.Count)
		{
			_currentTurnIndex = 0;
			_roundNumber++;
			_chatLog?.AddMessage($"--- Round {_roundNumber} ---", Colors.Crimson);
		}
		LogCurrentTurn();
		UpdateDisplay();
	}

	private void LogCurrentTurn()
	{
		if (_currentTurnIndex >= 0 && _currentTurnIndex < _combatants.Count)
		{
			Combatant current = _combatants[_currentTurnIndex];
			_chatLog?.AddMessage($"Turn: {current.DisplayText} (Init: {current.Initiative})", Colors.LightSeaGreen);
		}
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

		// Clear previous entries
		foreach (Node child in trackerListVBox.GetChildren())
		{
			child.QueueFree();
		}

		roundCounterLabel.Text = _combatStarted ? $"Round: {_roundNumber}" : "Round: 0 (Combat Ended)";
		if (!_combatStarted && _combatants.Count > 0) roundCounterLabel.Text = "Pre-Combat";


		for (int i = 0; i < _combatants.Count; i++)
		{
			Combatant combatant = _combatants[i];
			Label entryLabel = new Label();
			entryLabel.Text = $"I:{combatant.Initiative} - {combatant.DisplayText}";

			if (_combatStarted && i == _currentTurnIndex)
			{
				entryLabel.Modulate = Colors.YellowGreen; // Highlight current turn
				// Consider adding a small indicator like "> "
				entryLabel.Text = $"> {entryLabel.Text}";
			}
			else
			{
				entryLabel.Modulate = Colors.White;
			}
			trackerListVBox.AddChild(entryLabel);
		}
	}
}
