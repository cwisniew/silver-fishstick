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
	private NetworkManager _networkManagerCache; // Corrected: was _networkManager in some versions
	private string _gameUnitName = "ft";

	// Ensure parameter order matches MainScene's call
	public void Initialize(ChatLog chatLog, SoundManager soundManager, NetworkManager networkManager, string gameUnitName = "ft")
	{
		_chatLog = chatLog;
		_soundManager = soundManager;
		_networkManagerCache = networkManager; // Store reference
		_gameUnitName = gameUnitName;
	}

	public override void _Ready()
	{
		if (trackerListVBox == null) trackerListVBox = GetNodeOrNull<VBoxContainer>("VBoxContainer/TrackerListVBox");
		if (roundCounterLabel == null) roundCounterLabel = GetNodeOrNull<Label>("VBoxContainer/RoundCounterLabel");
		if (trackerListVBox == null) GD.PrintErr("CombatTracker: TrackerListVBox is null!");
		if (roundCounterLabel == null) GD.PrintErr("CombatTracker: RoundCounterLabel is null!");
		UpdateDisplay();
	}

	public void AddCombatantEntry(Combatant combatant)
	{
		if (combatant == null) return;
		if (combatant.LinkedToken != null && combatant.NetworkPlayerId == 0)
		{
			combatant.NetworkPlayerId = combatant.LinkedToken.OwningPlayerId;
		}
		if (combatant.LinkedToken != null && _combatants.Any(c => c.LinkedToken == combatant.LinkedToken && c.LinkedToken != null)) {
			_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} is already in combat.", Colors.Orange);
			return;
		}
		_combatants.Add(combatant);
		_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} (Init: {combatant.Initiative}, OwnerID: {combatant.NetworkPlayerId}) added.", Colors.CornflowerBlue);
		if (_combatStarted) SortCombatants();
		UpdateDisplay();
		BroadcastFullStateIfServer();
	}

	public void RemoveCombatant(Combatant combatant) { /* ... (existing logic) ... */ UpdateDisplay(); BroadcastFullStateIfServer(); }
	private void SortCombatants() { /* ... (existing logic) ... */ }

	public void StartCombat()
	{
		if (_combatants.Count == 0) { _chatLog?.AddMessage("Combat Tracker: No combatants to start.", Colors.OrangeRed); return; }
		_combatStarted = true; _roundNumber = 1; SortCombatants(); _currentTurnIndex = 0;
		_chatLog?.AddMessage($"--- Combat Started! Round {_roundNumber} ---", Colors.Crimson);

		// Reset movement for the first combatant
		if (_combatants.Count > 0 && _currentTurnIndex < _combatants.Count && _combatants[_currentTurnIndex].LinkedToken != null)
		{
			_combatants[_currentTurnIndex].LinkedToken.ResetTurnMovementStats();
		}
		LogCurrentTurn(); UpdateDisplay(); BroadcastFullStateIfServer();
	}

	public void NextTurn()
	{
		if (!_combatStarted || _combatants.Count == 0)
		{
			_chatLog?.AddMessage("Combat Tracker: Combat not started or no combatants.", Colors.Orange);
			return;
		}
		_soundManager?.PlaySfx("ui_click.wav.txt");

		// Identify combatant whose turn is ending
		Combatant combatantWhoseTurnEnded = null;
		if (_currentTurnIndex >= 0 && _currentTurnIndex < _combatants.Count)
		{
			combatantWhoseTurnEnded = _combatants[_currentTurnIndex];
		}

		// Advance turn
		_currentTurnIndex++;
		if (_currentTurnIndex >= _combatants.Count)
		{
			_currentTurnIndex = 0;
			_roundNumber++;
			_chatLog?.AddMessage($"--- Round {_roundNumber} ---", Colors.Crimson);
		}

		// Reset movement for the new current combatant
		if (_combatants.Count > 0 && _currentTurnIndex < _combatants.Count && _combatants[_currentTurnIndex].LinkedToken != null)
		{
			_combatants[_currentTurnIndex].LinkedToken.ResetTurnMovementStats();
		}

		LogCurrentTurn(); // Log who's turn it is now

		// Decrement status effects for the combatant whose turn just ended (server-side)
		if (_networkManagerCache != null && _networkManagerCache.IsServer() && combatantWhoseTurnEnded?.LinkedToken?.Sheet != null)
		{
			Token tokenThatEndedTurn = combatantWhoseTurnEnded.LinkedToken;
			bool sheetWasChangedByDecrement = tokenThatEndedTurn.Sheet.DecrementStatusEffectDurations();

			if (sheetWasChangedByDecrement)
			{
				// Log locally on server for now. Client will log when sheet update is received.
				_chatLog?.AddMessage($"Server: Decremented status effects for '{tokenThatEndedTurn.Name}'. New HP: {tokenThatEndedTurn.Sheet.CurrentHealthPoints}, Statuses: {tokenThatEndedTurn.Sheet.ActiveStatusEffects.Count}", Colors.DarkSlateGray, isCombatLog: true);

				var sheetDict = new Godot.Collections.Dictionary();
				tokenThatEndedTurn.Sheet.ToDictionary(sheetDict);
				string updatedSheetJson = Json.Stringify(sheetDict);

				_networkManagerCache.Rpc(nameof(NetworkManager.RpcClientReceiveFullSheetUpdate), tokenThatEndedTurn.Name.ToString(), updatedSheetJson);

				// Important: The token's visual for status effects might need an update on the server too
				tokenThatEndedTurn.UpdateStatusEffectVisuals();
			}
		}

		UpdateDisplay(); // Update tracker display for everyone (highlights, round number)
		BroadcastFullStateIfServer(); // Sync overall tracker state (current turn, round, combatant list order)
	}

	private void LogCurrentTurn() { /* ... (existing logic, ensure it's called after _currentTurnIndex is updated) ... */ }
	public CombatTrackerData GetCombatTrackerData() { /* ... (existing logic) ... */ return new CombatTrackerData(); }
	public void ApplyCombatTrackerData(CombatTrackerData data, Godot.Collections.Array<Token> allSceneTokens) { /* ... (existing logic) ... */ }
	public void ResetCombat() { /* ... (existing logic) ... */ UpdateDisplay(); BroadcastFullStateIfServer(); }
	private void BroadcastFullStateIfServer() { if (_networkManagerCache != null && _networkManagerCache.IsServer()) { CombatTrackerData d = GetCombatTrackerData(); string j = Json.Stringify(d.ToDictionary()); _networkManagerCache.Rpc(nameof(NetworkManager.RpcClientReceiveFullCombatState), j); } }
	private void UpdateDisplay() { /* ... (existing logic) ... */ }
	public Combatant GetCurrentCombatant() { if (_combatStarted && _currentTurnIndex >= 0 && _currentTurnIndex < _combatants.Count) return _combatants[_currentTurnIndex]; return null; }

	// New method to update combatant's NetworkPlayerId if token ownership changes
    public void UpdateCombatantOwnerByToken(Token token, long newOwnerNetId)
    {
        if (token == null) return;
        Combatant combatantToUpdate = _combatants.FirstOrDefault(c => c.LinkedToken == token);
        if (combatantToUpdate != null)
        {
            if (combatantToUpdate.NetworkPlayerId != newOwnerNetId)
            {
                combatantToUpdate.NetworkPlayerId = newOwnerNetId;
                _chatLog?.AddMessage($"Combatant '{combatantToUpdate.DisplayText}' owner updated to Player ID: {newOwnerNetId}.", Colors.AliceBlue);
                UpdateDisplay(); // Refresh display to show new owner if applicable
                BroadcastFullStateIfServer(); // Sync the updated combat list
            }
        }
    }
}
