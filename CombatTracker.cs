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
	private NetworkManager _networkManager;
	private string _gameUnitName = "ft";

	public void Initialize(ChatLog chatLog, SoundManager soundManager, NetworkManager networkManager, string gameUnitName = "ft")
	{
		_chatLog = chatLog;
		_soundManager = soundManager;
		_networkManager = networkManager;
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
		// Set NetworkPlayerId from token owner if not already set
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

	public void RemoveCombatant(Combatant combatant)
	{
		if (combatant == null) return;
		bool removed = _combatants.Remove(combatant);
		if(!removed) {
			if (combatant.LinkedToken != null) removed = _combatants.RemoveAll(c => c.LinkedToken == combatant.LinkedToken) > 0;
			else if (!string.IsNullOrEmpty(combatant.Name)) removed = _combatants.RemoveAll(c => c.Name == combatant.Name && c.LinkedToken == null) > 0;
		}
		if (!removed) { GD.Print($"CombatTracker: Combatant not found for removal: {combatant.DisplayText}"); return; }

		_chatLog?.AddMessage($"Combat Tracker: {combatant.DisplayText} removed.", Colors.Orange);
		if (_combatStarted && _combatants.Count == 0) { ResetCombat(); return; }
		else if (_combatStarted)
		{
			if (_currentTurnIndex >= _combatants.Count) _currentTurnIndex = _combatants.Count > 0 ? 0 : -1;
			else if (_currentTurnIndex == -1 && _combatants.Count > 0) _currentTurnIndex = 0;
		}
		UpdateDisplay();
		BroadcastFullStateIfServer();
	}

	private void SortCombatants() { _combatants = _combatants.OrderByDescending(c => c.Initiative).ThenByDescending(c => c.LinkedToken?.Sheet?.Speed ?? 0).ToList(); }

	public void StartCombat()
	{
		if (_combatants.Count == 0) { _chatLog?.AddMessage("Combat Tracker: No combatants to start.", Colors.OrangeRed); return; }
		_combatStarted = true; _roundNumber = 1; SortCombatants(); _currentTurnIndex = 0;
		_chatLog?.AddMessage($"--- Combat Started! Round {_roundNumber} ---", Colors.Crimson);
		if (_combatants.Count > 0 && _currentTurnIndex < _combatants.Count && _combatants[_currentTurnIndex].LinkedToken != null)
			_combatants[_currentTurnIndex].LinkedToken.ResetTurnMovementStats();
		LogCurrentTurn(); UpdateDisplay(); BroadcastFullStateIfServer();
	}

	public void NextTurn()
	{
		if (!_combatStarted || _combatants.Count == 0) { _chatLog?.AddMessage("Combat Tracker: Combat not started or no combatants.", Colors.Orange); return; }
		_soundManager?.PlaySfx("ui_click.wav.txt"); _currentTurnIndex++;
		if (_currentTurnIndex >= _combatants.Count) { _currentTurnIndex = 0; _roundNumber++; _chatLog?.AddMessage($"--- Round {_roundNumber} ---", Colors.Crimson); }
		if (_combatants.Count > 0 && _currentTurnIndex < _combatants.Count && _combatants[_currentTurnIndex].LinkedToken != null)
			_combatants[_currentTurnIndex].LinkedToken.ResetTurnMovementStats();
		LogCurrentTurn(); UpdateDisplay(); BroadcastFullStateIfServer();
	}

	private void LogCurrentTurn()
	{
		if (_currentTurnIndex >= 0 && _currentTurnIndex < _combatants.Count)
		{
			Combatant current = _combatants[_currentTurnIndex]; string speedInfo = "";
			if (current.LinkedToken != null && current.LinkedToken.Sheet != null) speedInfo = $" (Speed: {current.LinkedToken.Sheet.Speed} {_gameUnitName}, Owner: {current.NetworkPlayerId})";
			else speedInfo = $" (Owner: {current.NetworkPlayerId})";
			_chatLog?.AddMessage($"Turn: {current.DisplayText} (Init: {current.Initiative}){speedInfo}", Colors.LightSeaGreen);
		}
	}

	public CombatTrackerData GetCombatTrackerData()
	{
		var data = new CombatTrackerData { Combatants = new List<CombatantData>(), CurrentTurnIndex = _currentTurnIndex, RoundNumber = _roundNumber, CombatStarted = _combatStarted };
		foreach (var c in _combatants) data.Combatants.Add(new CombatantData { Name = c.Name, Initiative = c.Initiative, LinkedTokenNodeName = c.LinkedToken?.Name.ToString() ?? "", NetworkPlayerId = c.NetworkPlayerId });
		return data;
	}

	public void ApplyCombatTrackerData(CombatTrackerData data, Godot.Collections.Array<Token> allSceneTokens)
	{
		_combatants.Clear(); _currentTurnIndex = -1; _roundNumber = 0; _combatStarted = false;
		if (data == null) { UpdateDisplay(); return; }

		foreach (var cData in data.Combatants)
		{
			Token linkedToken = null;
			if (!string.IsNullOrEmpty(cData.LinkedTokenNodeName))
			{
				foreach(var tVariant in allSceneTokens) if (tVariant.AsGodotObject() is Token t && t.Name == cData.LinkedTokenNodeName) { linkedToken = t; break; }
				if (linkedToken == null) _chatLog?.AddMessage($"Combat Load Warning: Token '{cData.LinkedTokenNodeName}' for '{cData.Name}' not found.", Colors.Yellow);
			}
			_combatants.Add(new Combatant(cData.Name, cData.Initiative, linkedToken) { NetworkPlayerId = cData.NetworkPlayerId });
		}
		_currentTurnIndex = data.CurrentTurnIndex; _roundNumber = data.RoundNumber; _combatStarted = data.CombatStarted;
		if (_combatStarted)
		{
			if (_currentTurnIndex < 0 || _currentTurnIndex >= _combatants.Count) _currentTurnIndex = (_combatants.Count > 0) ? 0 : -1;
			if (_currentTurnIndex != -1 && _combatants.Count > 0 && _currentTurnIndex < _combatants.Count && _combatants[_currentTurnIndex].LinkedToken != null)
				_combatants[_currentTurnIndex].LinkedToken.ResetTurnMovementStats();
		}
		UpdateDisplay();
		if (_combatStarted && _currentTurnIndex != -1 && _combatants.Count > 0 && _currentTurnIndex < _combatants.Count) LogCurrentTurn();
		_chatLog?.AddMessage("Combat state restored from server.", Colors.CornflowerBlue);
	}

	public void ResetCombat()
	{
		_combatants.Clear(); _currentTurnIndex = -1; _roundNumber = 0; _combatStarted = false;
		_chatLog?.AddMessage("--- Combat Reset ---", Colors.MediumPurple);
		UpdateDisplay(); BroadcastFullStateIfServer();
	}

	private void BroadcastFullStateIfServer() { if (_networkManager != null && _networkManager.IsServer()) { CombatTrackerData d = GetCombatTrackerData(); string j = Json.Stringify(d.ToDictionary()); _networkManager.Rpc(nameof(NetworkManager.RpcClientReceiveFullCombatState), j); } }
	private void UpdateDisplay()
	{
		if (trackerListVBox == null || roundCounterLabel == null) { GD.PrintErr("CombatTracker: UI elements null in UpdateDisplay."); return; }
		foreach (Node child in trackerListVBox.GetChildren()) child.QueueFree();
		roundCounterLabel.Text = _combatStarted ? $"Round: {_roundNumber}" : "Round: 0 (Combat Ended)";
		if (!_combatStarted && _combatants.Count > 0) roundCounterLabel.Text = "Pre-Combat";
		for (int i = 0; i < _combatants.Count; i++)
		{
			Combatant c = _combatants[i]; Label l = new Label();
			string ownerInfo = c.NetworkPlayerId != 0 ? $" (P:{c.NetworkPlayerId})" : " (NPC)";
			l.Text = $"I:{c.Initiative} - {c.DisplayText}{ownerInfo}";
			if (_combatStarted && i == _currentTurnIndex) { l.Modulate = Colors.YellowGreen; l.Text = $"> {l.Text}"; }
			else l.Modulate = Colors.White;
			trackerListVBox.AddChild(l);
		}
	}
	public Combatant GetCurrentCombatant()
	{
		if (_combatStarted && _currentTurnIndex >= 0 && _currentTurnIndex < _combatants.Count)
		{
			return _combatants[_currentTurnIndex];
		}
		return null;
	}
}
