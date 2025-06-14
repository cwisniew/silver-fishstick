using Godot;
using System;
using System.Collections.Generic; // For Dictionary

public partial class NetworkManager : Node
{
	public const int DefaultPort = 7777;
	public const int MaxPlayers = 8;
	private ENetMultiplayerPeer _multiplayerPeer;

	// --- Base Signals ---
	[Signal] public delegate void ServerCreatedEventHandler();
	[Signal] public delegate void ServerCreationFailedEventHandler(string reason);
	[Signal] public delegate void ConnectionSucceededEventHandler();
	[Signal] public delegate void ConnectionFailedEventHandler();
	[Signal] public delegate void PeerConnectedEventHandler(long id);
	[Signal] public delegate void PeerDisconnectedEventHandler(long id);
	[Signal] public delegate void ServerDisconnectedEventHandler();
	// --- Feature Specific Signals ---
	[Signal] public delegate void PlayerListUpdatedEventHandler();
	[Signal] public delegate void ChatMessageReceivedEventHandler(long senderId, string senderName, string messageContent);
	[Signal] public delegate void NetworkSpawnTokenRequestedEventHandler(string tokenNodeName, Vector2 globalPosition, string texturePath, string sheetDataJson, bool hasVision, float visionRangeGameUnits, Vector2 size);
	[Signal] public delegate void NetworkTokenPositionUpdatedEventHandler(string tokenNodeName, Vector2 newGlobalPosition);
	[Signal] public delegate void NetworkTokenPathExecutionRequestedEventHandler(string tokenNodeName, Godot.Collections.Array pathPoints);
	[Signal] public delegate void ServerDragRequestReceivedEventHandler(string tokenNodeName, Vector2 newGlobalPosition, long senderId);
	[Signal] public delegate void ServerPathRequestReceivedEventHandler(string tokenNodeName, Vector2 targetGlobalPosition, long senderId);
	[Signal] public delegate void ServerDiceRollRequestedEventHandler(string diceNotation, long senderId);
	[Signal] public delegate void NetworkDiceRollResultReceivedEventHandler(long rollerId, string rollerName, string resultDataJson);
	[Signal] public delegate void NetworkCombatStateReceivedEventHandler(string combatTrackerDataJson);
	[Signal] public delegate void NetworkMapLoadRequestedEventHandler(string mapResourcePath);
	[Signal] public delegate void NetworkCharacterSheetUpdatedEventHandler(string tokenNodeName, string sheetDataJson);
	[Signal] public delegate void ServerSheetUpdateRequestReceivedEventHandler(string tokenNodeName, string sheetDataJson, long senderId);
	[Signal] public delegate void NetworkAddDrawingLineEventHandler(string lineDataJson);
	[Signal] public delegate void NetworkReceiveFullDrawingStateEventHandler(string allLinesDataJson);
	[Signal] public delegate void NetworkAllDecksStateReceivedEventHandler(string allDecksDataJson);
	// Token Ownership Sync Signals
	[Signal] public delegate void ServerSetTokenOwnerRequestedEventHandler(string tokenNodeName, long newOwnerNetId, long requesterId);
	[Signal] public delegate void NetworkTokenOwnerUpdatedEventHandler(string tokenNodeName, long newOwnerNetId);


	private Dictionary<long, NetworkPlayer> _players = new Dictionary<long, NetworkPlayer>();
	// Note: Music related fields like _musicFinishedCallable were removed as they belong in SoundManager
	// private Callable _musicFinishedCallable;
	// private bool _loopMusic = false;
	// private string _currentMusicName;

	public bool IsNetworkActive() => Multiplayer.MultiplayerPeer != null &&
									(Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected ||
									 Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connecting);
	public bool IsServer() => IsNetworkActive() && Multiplayer.IsServer();

	public override void _Ready()
	{
		if (Multiplayer.MultiplayerPeer != null && Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
		{
			Multiplayer.MultiplayerPeer.Close();
		}
		Multiplayer.MultiplayerPeer = null;
	}

	public override void _ExitTree() { if (Multiplayer.MultiplayerPeer != null) { DisconnectNetwork(); } }

	public Error HostGame(int port)
	{
		if (IsNetworkActive()) { EmitSignal(SignalName.ServerCreationFailed, "Network active."); return Error.AlreadyInUse; }
		_multiplayerPeer = new ENetMultiplayerPeer();
		Error err = _multiplayerPeer.CreateServer(port, MaxPlayers);
		if (err != Error.Ok) { GD.PrintErr($"NM: Server create fail: {err}"); Multiplayer.MultiplayerPeer=null; _multiplayerPeer=null; EmitSignal(SignalName.ServerCreationFailed, err.ToString()); return err; }

		Multiplayer.MultiplayerPeer = _multiplayerPeer;
		Multiplayer.PeerConnected += OnPeerConnected_Signal;
		Multiplayer.PeerDisconnected += OnPeerDisconnected_Signal;

		EmitSignal(SignalName.ServerCreated);
		_players.Clear();
		NetworkPlayer hostPlayer = new NetworkPlayer { Id = Multiplayer.GetUniqueId(), Name = $"Host (Player {Multiplayer.GetUniqueId()})" };
		_players.Add(hostPlayer.Id, hostPlayer);
		EmitSignal(SignalName.PlayerListUpdated);
		return Error.Ok;
	}

	public void JoinGame(string ipAddress, int port)
	{
		if (IsNetworkActive()) { EmitSignal(SignalName.ConnectionFailed); return; }
		_multiplayerPeer = new ENetMultiplayerPeer();
		Error err = _multiplayerPeer.CreateClient(ipAddress, port);
		if (err != Error.Ok) { GD.PrintErr($"NM: Client create fail: {err}"); Multiplayer.MultiplayerPeer=null; _multiplayerPeer=null; EmitSignal(SignalName.ConnectionFailed); return; }

		Multiplayer.MultiplayerPeer = _multiplayerPeer;
		Multiplayer.ConnectionSucceeded += OnConnectionSucceeded_Signal;
		Multiplayer.ConnectionFailed += OnConnectionFailed_Signal;
		Multiplayer.ServerDisconnected += OnServerDisconnected_Signal;
	}

	public void DisconnectNetwork()
	{
		if (Multiplayer.MultiplayerPeer == null) return;
		GD.Print("NM: Disconnecting network.");
		bool wasServer = Multiplayer.IsServer();
		try {
			if (Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
			{
				if(wasServer) { Multiplayer.PeerConnected -= OnPeerConnected_Signal; Multiplayer.PeerDisconnected -= OnPeerDisconnected_Signal; }
				else { Multiplayer.ConnectionSucceeded -= OnConnectionSucceeded_Signal; Multiplayer.ConnectionFailed -= OnConnectionFailed_Signal; Multiplayer.ServerDisconnected -= OnServerDisconnected_Signal; }
			}
		} catch (ObjectDisposedException ex) { GD.Print($"NM: MultiplayerPeer disposed: {ex.Message}"); }

		Multiplayer.MultiplayerPeer.Close();
		Multiplayer.MultiplayerPeer = null;
		_multiplayerPeer = null;
		_players.Clear();
		EmitSignal(SignalName.PlayerListUpdated);
		if(wasServer) EmitSignal(SignalName.ServerDisconnected);
	}

	public Godot.Collections.Array<NetworkPlayer> GetPlayerList() { var list = new Godot.Collections.Array<NetworkPlayer>(); foreach(var p in _players.Values) list.Add(p); return list; }
	private Godot.Collections.Array PlayersToGodotArrayDictionary(Dictionary<long, NetworkPlayer> playersDict) { var arr = new Godot.Collections.Array(); foreach (var p in playersDict.Values) arr.Add(p.ToDictionary()); return arr; }

	private void OnPeerConnected_Signal(long id)
	{
		GD.Print($"NM: Peer connected: {id}"); EmitSignal(SignalName.PeerConnected, id);
		if (Multiplayer.IsServer())
		{
			NetworkPlayer newPlayer = new NetworkPlayer { Id = id, Name = $"Player {id}" };
			_players.Add(id, newPlayer);
			string newPlayerJson = Json.Stringify(newPlayer.ToDictionary());
			string allPlayersJson = Json.Stringify(PlayersToGodotArrayDictionary(_players));
			RpcId(id, nameof(RpcReceiveInitialPlayerState), newPlayerJson, allPlayersJson);
			foreach(long existingPlayerId in _players.Keys) if (existingPlayerId != id && existingPlayerId != Multiplayer.GetUniqueId()) RpcId(existingPlayerId, nameof(RpcRemotePlayerJoined), newPlayerJson);
			EmitSignal(SignalName.PlayerListUpdated);

			var mainSceneNode = GetTree().Root.GetNode("MainScene");
			if (mainSceneNode is MainScene mainSceneInstance)
			{
				string mapPath = mainSceneInstance.GetCurrentMapPath();
				if (!string.IsNullOrEmpty(mapPath)) RpcId(id, nameof(RpcClientReceiveCurrentMap), mapPath);
				CombatTracker combat = mainSceneInstance.CombatTracker;
				if (combat != null) { RpcId(id, nameof(RpcClientReceiveFullCombatState), Json.Stringify(combat.GetCombatTrackerData().ToDictionary())); }
				DrawingOverlay drawings = mainSceneInstance.DrawingOverlayNode;
				if (drawings != null) { List<DrawingLineData> drawingDataList = drawings.GetDrawingData(); string allLinesJson = Json.Stringify(DrawingLineData.ListToGodotArrayOfDictionaries(drawingDataList)); RpcId(id, nameof(RpcClientReceiveAllDrawings), allLinesJson); }
				string allDecksJson = DeckManager.SerializeDecksForNetwork(); if (!string.IsNullOrEmpty(allDecksJson)) RpcId(id, nameof(RpcClientReceiveAllDecksState), allDecksJson);
			} else GD.PrintErr("NM: MainScene not found for initial state sync.");
		}
	}
	private void OnPeerDisconnected_Signal(long id) { GD.Print($"NM: Peer disconnected: {id}"); EmitSignal(SignalName.PeerDisconnected, id); if (Multiplayer.IsServer()) { if (_players.Remove(id)) { Rpc(nameof(RpcRemotePlayerLeft), id); EmitSignal(SignalName.PlayerListUpdated); } } }
	private void OnConnectionSucceeded_Signal() { GD.Print("NM: Client Connected."); EmitSignal(SignalName.ConnectionSucceeded); }
	private void OnConnectionFailed_Signal() { GD.PrintErr("NM: Client Connection FAILED."); if (Multiplayer.MultiplayerPeer != null) Multiplayer.MultiplayerPeer.Close(); Multiplayer.MultiplayerPeer = null; _multiplayerPeer = null; _players.Clear(); EmitSignal(SignalName.PlayerListUpdated); EmitSignal(SignalName.ConnectionFailed); }
	private void OnServerDisconnected_Signal() { GD.Print("NM: Client Disconnected from Server."); if (Multiplayer.MultiplayerPeer != null) Multiplayer.MultiplayerPeer.Close(); Multiplayer.MultiplayerPeer = null; _multiplayerPeer = null; _players.Clear(); EmitSignal(SignalName.PlayerListUpdated); EmitSignal(SignalName.ServerDisconnected); }

	// --- RPC Method Implementations ---
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] private void RpcReceiveInitialPlayerState(string s, string a) { if(Multiplayer.IsServer())return; _players.Clear(); var arr=Json.ParseString(a).AsGodotArray(); foreach(var v in arr){NetworkPlayer p=NetworkPlayer.FromDictionary(v.AsGodotDictionary()); if(!_players.ContainsKey(p.Id))_players.Add(p.Id,p);} EmitSignal(SignalName.PlayerListUpdated); }
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=true, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] private void RpcRemotePlayerJoined(string s) { NetworkPlayer p=NetworkPlayer.FromDictionary(Json.ParseString(s).AsGodotDictionary()); if(!_players.ContainsKey(p.Id)){_players.Add(p.Id,p); EmitSignal(SignalName.PlayerListUpdated);}}
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=true, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] private void RpcRemotePlayerLeft(long id) { if(_players.Remove(id)) EmitSignal(SignalName.PlayerListUpdated); }
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcServerRelayChatMessage(string m) { long id=Multiplayer.GetRemoteSenderId(); if(_players.TryGetValue(id,out NetworkPlayer p))Rpc(nameof(RpcClientReceiveChatMessage),p.Id,p.Name,m); }
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=true, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientReceiveChatMessage(long id, string n, string m) { EmitSignal(SignalName.ChatMessageReceived,id,n,m); }
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientDoSpawnToken(string n, Vector2 p, string tp, string sj, bool hv, float vr, Vector2 sz) { EmitSignal(SignalName.NetworkSpawnTokenRequested,n,p,tp,sj,hv,vr,sz); }
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcServerRequestTokenDragMove(string n, Vector2 p) { EmitSignal(SignalName.ServerDragRequestReceived,n,p,Multiplayer.GetRemoteSenderId());}
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcServerRequestTokenPathMove(string n, Vector2 tp) { EmitSignal(SignalName.ServerPathRequestReceived,n,tp,Multiplayer.GetRemoteSenderId());}
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientUpdateTokenPosition(string n, Vector2 p) { EmitSignal(SignalName.NetworkTokenPositionUpdated,n,p);}
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientExecuteTokenPath(string n, Godot.Collections.Array pa) { EmitSignal(SignalName.NetworkTokenPathExecutionRequested,n,pa);}
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcServerDoDiceRoll(string d) { EmitSignal(SignalName.ServerDiceRollRequested,d,Multiplayer.GetRemoteSenderId());}
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=true, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientDisplayDiceRollResult(long rId, string rN, string j) { EmitSignal(SignalName.NetworkDiceRollResultReceived,rId,rN,j);}
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientReceiveCurrentMap(string p) { EmitSignal(SignalName.NetworkMapLoadRequested,p);} // Changed to AnyPeer
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal=false, TransferMode=MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientReceiveFullCombatState(string json) { EmitSignal(SignalName.NetworkCombatStateReceived,json);}
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientAddSingleDrawingLine(string lineDataJson) { EmitSignal(SignalName.NetworkAddDrawingLine, lineDataJson); }
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientReceiveAllDrawings(string allLinesDataJson) { EmitSignal(SignalName.NetworkReceiveFullDrawingState, allLinesDataJson); }
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientReceiveFullSheetUpdate(string tokenNodeName, string sheetDataJson) { EmitSignal(SignalName.NetworkCharacterSheetUpdated, tokenNodeName, sheetDataJson); }
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcServerProcessSheetUpdate(string tokenNodeName, string sheetDataJson) { EmitSignal(SignalName.ServerSheetUpdateRequestReceived, tokenNodeName, sheetDataJson, Multiplayer.GetRemoteSenderId()); }
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)] public void RpcClientReceiveAllDecksState(string allDecksDataJson) { EmitSignal(SignalName.NetworkAllDecksStateReceived, allDecksDataJson); }

	// New Token Ownership RPCs
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcServerRequestSetTokenOwner(string tokenNodeName, long newOwnerNetId)
	{
		EmitSignal(SignalName.ServerSetTokenOwnerRequested, tokenNodeName, newOwnerNetId, Multiplayer.GetRemoteSenderId());
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientNotifyTokenOwnerChanged(string tokenNodeName, long newOwnerNetId)
	{
		EmitSignal(SignalName.NetworkTokenOwnerUpdated, tokenNodeName, newOwnerNetId);
	}
}
