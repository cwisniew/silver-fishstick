using Godot;
using System;

public partial class NetworkManager : Node
{
	public const int DefaultPort = 7777;
	public const int MaxPlayers = 8;
	private ENetMultiplayerPeer _multiplayerPeer;

	// --- Signals ---
	[Signal] public delegate void ServerCreatedEventHandler();
	[Signal] public delegate void ServerCreationFailedEventHandler(string reason); // Added for better feedback
	[Signal] public delegate void ConnectionSucceededEventHandler();
	[Signal] public delegate void ConnectionFailedEventHandler();
	[Signal] public delegate void PeerConnectedEventHandler(long id);
	[Signal] public delegate void PeerDisconnectedEventHandler(long id);
	[Signal] public delegate void ServerDisconnectedEventHandler();
	[Signal] public delegate void PlayerListUpdatedEventHandler();
	[Signal] public delegate void ChatMessageReceivedEventHandler(long senderId, string senderName, string messageContent);
	[Signal] public delegate void NetworkSpawnTokenRequestedEventHandler(string tokenNodeName, Vector2 globalPosition, string texturePath, string sheetDataJson, bool hasVision, float visionRangeGameUnits, Vector2 size);
	// Signals for movement
	[Signal] public delegate void NetworkTokenPositionUpdatedEventHandler(string tokenNodeName, Vector2 newGlobalPosition);
	[Signal] public delegate void NetworkTokenPathExecutionRequestedEventHandler(string tokenNodeName, Godot.Collections.Array pathPoints);
	[Signal] public delegate void ServerDragRequestReceivedEventHandler(string tokenNodeName, Vector2 newGlobalPosition, long senderId);
	[Signal] public delegate void ServerPathRequestReceivedEventHandler(string tokenNodeName, Vector2 targetGlobalPosition, long senderId);
	// Signals for Dice Rolls
	[Signal] public delegate void ServerDiceRollRequestedEventHandler(string diceNotation, long senderId);
	[Signal] public delegate void NetworkDiceRollResultReceivedEventHandler(long rollerId, string rollerName, string resultDataJson);
	// Signal for Combat Tracker Sync
	[Signal] public delegate void NetworkCombatStateReceivedEventHandler(string combatTrackerDataJson);
	// Signal for Map Sync
	[Signal] public delegate void NetworkMapLoadRequestedEventHandler(string mapResourcePath);
	// Signal for Handout Sync
	[Signal] public delegate void NetworkHandoutDisplayRequestedEventHandler(string handoutImageResourcePath);


	private Dictionary<long, NetworkPlayer> _players = new Dictionary<long, NetworkPlayer>();

	public bool IsNetworkActive() => Multiplayer.MultiplayerPeer != null &&
									(Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connected ||
									 Multiplayer.MultiplayerPeer.GetConnectionStatus() == MultiplayerPeer.ConnectionStatus.Connecting);

	public bool IsServer() => IsNetworkActive() && Multiplayer.IsServer();

	public override void _Ready()
	{
		// Ensure the Multiplayer anager is clean when this node is ready,
		// especially important if it's part of a scene that might be reloaded.
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;
		}
	}

	public override void _ExitTree()
	{
		// Clean up when the node is removed from the scene
		if (Multiplayer.MultiplayerPeer != null)
		{
			Multiplayer.MultiplayerPeer.Close(); // Close connection
			// Unsubscribe from signals if connected directly in this class
			// Note: signals are connected to Multiplayer singleton, not _multiplayerPeer instance after assignment
			if(Multiplayer.IsServer())
			{
				// These are connected to the global Multiplayer object
				Multiplayer.PeerConnected -= OnPeerConnected_Signal;
				Multiplayer.PeerDisconnected -= OnPeerDisconnected_Signal;
			} else {
				Multiplayer.ConnectionSucceeded -= OnConnectionSucceeded_Signal;
				Multiplayer.ConnectionFailed -= OnConnectionFailed_Signal;
				Multiplayer.ServerDisconnected -= OnServerDisconnected_Signal;
			}
			Multiplayer.MultiplayerPeer = null; // Clear it
		}
		_multiplayerPeer = null; // Also clear our local reference
	}


	public Error HostGame(int port)
	{
		if (IsNetworkActive())
		{
			GD.Print("NetworkManager: Network is already active. Please disconnect first.");
			EmitSignal(SignalName.ServerCreationFailed, "Network already active.");
			return Error.AlreadyInUse;
		}

		_multiplayerPeer = new ENetMultiplayerPeer();
		Error err = _multiplayerPeer.CreateServer(port, MaxPlayers);

		if (err != Error.Ok)
		{
			GD.PrintErr($"NetworkManager: Failed to create server. Error: {err}");
			Multiplayer.MultiplayerPeer = null; // Ensure it's cleared
			_multiplayerPeer = null;
			EmitSignal(SignalName.ServerCreationFailed, err.ToString());
			return err;
		}

		Multiplayer.MultiplayerPeer = _multiplayerPeer;
		Multiplayer.PeerConnected += OnPeerConnected_Signal;
		Multiplayer.PeerDisconnected += OnPeerDisconnected_Signal;

		EmitSignal(SignalName.ServerCreated);
		GD.Print($"NetworkManager: Server created on port {port}. Waiting for players...");

		// Add host as player 1
		_players.Clear(); // Clear any previous state
		NetworkPlayer hostPlayer = new NetworkPlayer { Id = Multiplayer.GetUniqueId(), Name = $"Host (Player {Multiplayer.GetUniqueId()})" };
		_players.Add(hostPlayer.Id, hostPlayer);
		EmitSignal(SignalName.PlayerListUpdated); // Notify UI on server about the host player

		return Error.Ok;
	}

	public void JoinGame(string ipAddress, int port)
	{
		if (IsNetworkActive())
		{
			GD.Print("NetworkManager: Network is already active. Please disconnect first.");
			EmitSignal(SignalName.ConnectionFailed);
			return;
		}

		_multiplayerPeer = new ENetMultiplayerPeer();
		Error err = _multiplayerPeer.CreateClient(ipAddress, port);

		if (err != Error.Ok)
		{
			GD.PrintErr($"NetworkManager: Failed to create client. Error: {err}");
			Multiplayer.MultiplayerPeer = null;
			_multiplayerPeer = null;
			EmitSignal(SignalName.ConnectionFailed);
			return;
		}

		Multiplayer.MultiplayerPeer = _multiplayerPeer;
		Multiplayer.ConnectionSucceeded += OnConnectionSucceeded_Signal;
		Multiplayer.ConnectionFailed += OnConnectionFailed_Signal;
		Multiplayer.ServerDisconnected += OnServerDisconnected_Signal;
		GD.Print($"NetworkManager: Attempting to connect to {ipAddress}:{port}...");
	}

	public void DisconnectNetwork()
	{
		if (Multiplayer.MultiplayerPeer != null)
		{
			GD.Print("NetworkManager: Disconnecting network.");
			try
			{
				// Check if peer is still valid before trying to unsubscribe
				bool isServer = Multiplayer.IsServer(); // Store before peer potentially becomes invalid
				if (Multiplayer.MultiplayerPeer.GetConnectionStatus() != MultiplayerPeer.ConnectionStatus.Disconnected)
				{
					if(isServer)
					{
						Multiplayer.PeerConnected -= OnPeerConnected_Signal;
						Multiplayer.PeerDisconnected -= OnPeerDisconnected_Signal;
					} else {
						Multiplayer.ConnectionSucceeded -= OnConnectionSucceeded_Signal;
						Multiplayer.ConnectionFailed -= OnConnectionFailed_Signal;
						Multiplayer.ServerDisconnected -= OnServerDisconnected_Signal;
					}
				}
			} catch (ObjectDisposedException ex) {
				GD.Print($"NetworkManager: MultiplayerPeer was already disposed. {ex.Message}");
			}

			Multiplayer.MultiplayerPeer.Close();
			Multiplayer.MultiplayerPeer = null;

			// If it was a server, it should also emit ServerDisconnected for itself to update UI consistently.
			// Clients automatically get ServerDisconnected signal from the Multiplayer singleton when server closes connection.
			// However, if the server *itself* calls DisconnectNetwork, it needs to signal its own UI.
			bool wasServer = IsServer(); // Check before _multiplayerPeer is nulled
			_multiplayerPeer = null;

			if(wasServer) EmitSignal(SignalName.ServerDisconnected);
		}
		_players.Clear();
		EmitSignal(SignalName.PlayerListUpdated);
	}

	public Godot.Collections.Array<NetworkPlayer> GetPlayerList()
	{
		var playerList = new Godot.Collections.Array<NetworkPlayer>();
		foreach(var player in _players.Values)
		{
			playerList.Add(player);
		}
		return playerList;
	}

	private Godot.Collections.Array PlayersToGodotArrayDictionary(Dictionary<long, NetworkPlayer> playersDict)
	{
		var array = new Godot.Collections.Array();
		foreach (var player in playersDict.Values)
		{
			array.Add(player.ToDictionary());
		}
		return array;
	}

	// --- Private Signal Relay Methods & Server Logic ---
	private void OnPeerConnected_Signal(long id)
	{
		GD.Print($"NetworkManager: Peer connected: {id}");
		EmitSignal(SignalName.PeerConnected, id);

		if (Multiplayer.IsServer())
		{
			NetworkPlayer newPlayer = new NetworkPlayer { Id = id, Name = $"Player {id}" };
			_players.Add(id, newPlayer);

			string newPlayerJson = Json.Stringify(newPlayer.ToDictionary());
			string allPlayersJson = Json.Stringify(PlayersToGodotArrayDictionary(_players));

			// 1. Send initial player state (self and all current players) to the new peer
			RpcId(id, nameof(RpcReceiveInitialPlayerState), newPlayerJson, allPlayersJson);

			// 2. Notify all *other* existing players about the new peer
			foreach(long existingPlayerId in _players.Keys)
			{
				if (existingPlayerId != id && existingPlayerId != Multiplayer.GetUniqueId()) // Don't send to self or the new peer
				{
					RpcId(existingPlayerId, nameof(RpcRemotePlayerJoined), newPlayerJson);
				}
			}
			// Server updates its own list via PlayerListUpdated signal triggered below (or RpcRemotePlayerJoined if CallLocal was true)
			EmitSignal(SignalName.PlayerListUpdated);

			// 3. Send current map to the new peer
			// It's crucial that MainScene is consistently at this path or passed in.
			// Using GetTree().Root assumes MainScene is a direct child of root, which it usually is.
			var mainSceneNode = GetTree().Root.GetNode("MainScene"); // Default name for main scene node
			if (mainSceneNode is MainScene mainSceneInstance)
			{
				string currentMapPath = mainSceneInstance.GetCurrentMapPath();
				if (!string.IsNullOrEmpty(currentMapPath))
				{
					RpcId(id, nameof(RpcClientReceiveCurrentMap), currentMapPath);
					GD.Print($"Server: Sent current map '{currentMapPath}' to new peer {id}.");
				}
				else
				{
					// Optionally, send an RPC to explicitly clear map if server has no map
					// RpcId(id, nameof(RpcClientReceiveCurrentMap), ""); // Send empty string
					GD.Print($"Server: No current map to send to new peer {id}.");
				}
			}
			else
			{
				GD.PrintErr("NetworkManager: Could not find MainScene node to get current map path. Path might be incorrect or scene not named 'MainScene'.");
			}
		}
	}

	private void OnPeerDisconnected_Signal(long id)
	{
		GD.Print($"NetworkManager: Peer disconnected: {id}");
		EmitSignal(SignalName.PeerDisconnected, id);

		if (Multiplayer.IsServer())
		{
			if (_players.Remove(id))
			{
				Rpc(nameof(RpcRemotePlayerLeft), id); // Notify all remaining clients (and server via CallLocal)
				EmitSignal(SignalName.PlayerListUpdated); // Server updates its list
			}
		}

	// --- Token Spawning RPC ---
	// Called by the server, executed on all other peers (clients).
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientDoSpawnToken(string tokenNodeName, Vector2 globalPosition, string texturePath, string sheetDataJson, bool hasVision, float visionRangeGameUnits, Vector2 size)
	{
		// This RPC is received by clients. It tells them to spawn a token based on server's data.
		// Emit a signal that MainScene (or a dedicated TokenSyncManager) can connect to.
		EmitSignal(SignalName.NetworkSpawnTokenRequested, tokenNodeName, globalPosition, texturePath, sheetDataJson, hasVision, visionRangeGameUnits, size);
	}

	// --- Movement RPCs ---

	// Client requests server to validate and broadcast a drag move
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcServerRequestTokenDragMove(string tokenNodeName, Vector2 newGlobalPosition)
	{
		long senderId = Multiplayer.GetRemoteSenderId();
		// Server's MainScene will handle validation and then call RpcClientUpdateTokenPosition
		EmitSignal(SignalName.ServerDragRequestReceived, tokenNodeName, newGlobalPosition, senderId);
	}

	// Client requests server to calculate and broadcast a path move
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcServerRequestTokenPathMove(string tokenNodeName, Vector2 targetGlobalPosition)
	{
		long senderId = Multiplayer.GetRemoteSenderId();
		// Server's MainScene will calculate path, execute locally, and then call RpcClientExecuteTokenPath
		EmitSignal(SignalName.ServerPathRequestReceived, tokenNodeName, targetGlobalPosition, senderId);
	}

	// Server broadcasts the final position of a token (after drag or other direct move)
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientUpdateTokenPosition(string tokenNodeName, Vector2 newGlobalPosition)
	{
		// Clients receive this and update their local token's position
		EmitSignal(SignalName.NetworkTokenPositionUpdated, tokenNodeName, newGlobalPosition);
	}

	// Server broadcasts a path for a token to execute
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientExecuteTokenPath(string tokenNodeName, Godot.Collections.Array pathPoints)
	{
		// Clients receive this and tell their local token to move along the path
		EmitSignal(SignalName.NetworkTokenPathExecutionRequested, tokenNodeName, pathPoints);
	}

	// --- Dice Roll RPCs ---

	// Called by a client, executed on the server (Authority)
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcServerDoDiceRoll(string diceNotation)
	{
		long senderId = Multiplayer.GetRemoteSenderId();
		// Server's MainScene will handle performing the roll and then call RpcClientDisplayDiceRollResult
		EmitSignal(SignalName.ServerDiceRollRequested, diceNotation, senderId);
	}

	// Called by the server, executed on all peers (AnyPeer) including the server itself (CallLocal = true)
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientDisplayDiceRollResult(long rollerId, string rollerName, string resultDataJson)
	{
		// Emit a signal that MainScene (or other UI controllers) can connect to display the result.
		EmitSignal(SignalName.NetworkDiceRollResultReceived, rollerId, rollerName, resultDataJson);
	}

	// --- Combat Tracker Sync RPC ---
	// Called by the server, executed on all other peers (clients).
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientReceiveFullCombatState(string combatTrackerDataJson)
	{
		// Clients receive this and update their local CombatTracker state.
		EmitSignal(SignalName.NetworkCombatStateReceived, combatTrackerDataJson);
	}

	// --- Map Sync RPC ---
	// Called by the server, executed on a specific client.
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientReceiveCurrentMap(string mapResourcePath)
	{
		// This RPC is received by a specific client.
		// It instructs that client to load the specified map.
		EmitSignal(SignalName.NetworkMapLoadRequested, mapResourcePath);
	}

	// --- Handout Sync RPC ---
	// Called by the server, executed on all other peers (clients).
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientShowHandout(string handoutImageResourcePath)
	{
		// Clients receive this and display the handout.
		EmitSignal(SignalName.NetworkHandoutDisplayRequested, handoutImageResourcePath);
	}
	}

	private void OnConnectionSucceeded_Signal()
	{
		GD.Print("NetworkManager: Connection succeeded (Client). Waiting for initial state from server.");
		EmitSignal(SignalName.ConnectionSucceeded);
	}

	private void OnConnectionFailed_Signal()
	{
		GD.PrintErr("NetworkManager: Connection failed (Client).");
		if (Multiplayer.MultiplayerPeer != null) Multiplayer.MultiplayerPeer.Close();
		Multiplayer.MultiplayerPeer = null;
		_multiplayerPeer = null;
		_players.Clear();
		EmitSignal(SignalName.PlayerListUpdated);
		EmitSignal(SignalName.ConnectionFailed);
	}

	private void OnServerDisconnected_Signal()
	{
		GD.Print("NetworkManager: Disconnected from server (Client).");
		if (Multiplayer.MultiplayerPeer != null) Multiplayer.MultiplayerPeer.Close();
		Multiplayer.MultiplayerPeer = null;
		_multiplayerPeer = null;
		_players.Clear();
		EmitSignal(SignalName.PlayerListUpdated);
		EmitSignal(SignalName.ServerDisconnected);
	}

	// --- RPC Methods ---
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveInitialPlayerState(string selfDataJson, string allPlayersDataJson)
	{
		// Called on the newly connected client by the server.
		if (Multiplayer.IsServer()) return;

		_players.Clear();
		var allPlayersList = Json.ParseString(allPlayersDataJson).AsGodotArray(); // Use ParseString
		foreach (var playerDataVariant in allPlayersList)
		{
			NetworkPlayer player = NetworkPlayer.FromDictionary(playerDataVariant.AsGodotDictionary());
			if (!_players.ContainsKey(player.Id))
			{
				_players.Add(player.Id, player);
			}
		}
		GD.Print($"Client {Multiplayer.GetUniqueId()}: Received initial player state. Players: {_players.Count}");
		EmitSignal(SignalName.PlayerListUpdated);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRemotePlayerJoined(string newPlayerDataJson)
	{
		NetworkPlayer newPlayer = NetworkPlayer.FromDictionary(Json.ParseString(newPlayerDataJson).AsGodotDictionary());

		if (newPlayer.Id == Multiplayer.GetUniqueId() && !Multiplayer.IsServer())
		{
			// This client is the one who joined. Their list is primarily populated by RpcReceiveInitialPlayerState.
			// This RPC might arrive before or after. If it arrives before, this adds the player.
			// If it arrives after, the ContainsKey check prevents duplication.
		}

		if (!_players.ContainsKey(newPlayer.Id))
		{
			_players.Add(newPlayer.Id, newPlayer);
			GD.Print($"Node {Multiplayer.GetUniqueId()}: Player {newPlayer.Id} ({newPlayer.Name}) added to list via RPC.");
			EmitSignal(SignalName.PlayerListUpdated);
		}
		// If server receives this (due to CallLocal=true), it already has the player from OnPeerConnected_Signal.
		// This ensures server's list is also updated and PlayerListUpdated is emitted.
		// No, server should not re-add. OnPeerConnected_Signal handles server list.
		// RpcRemotePlayerJoined is for *other* peers.
		// If CallLocal=true, server will execute this. Server should not add itself or existing players again.
		// The check `!_players.ContainsKey(newPlayer.Id)` handles this.
		// For server, it will only emit PlayerListUpdated if a new player was somehow missed and added here.
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRemotePlayerLeft(long id)
	{
		// Server already removed the player in OnPeerDisconnected_Signal before calling this RPC.
		// This RPC is for clients to remove the player.
		// If CallLocal=true, server will execute this. The Remove will return false if already removed.
		if (_players.Remove(id))
		{
			GD.Print($"Node {Multiplayer.GetUniqueId()}: Player {id} removed from list via RPC.");
			EmitSignal(SignalName.PlayerListUpdated);
		}
	}

	// --- Chat RPCs ---

	// Called by a client, executed on the server (Authority)
	[Rpc(MultiplayerApi.RpcMode.Authority, CallLocal = false, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcServerRelayChatMessage(string messageContent)
	{
		long senderId = Multiplayer.GetRemoteSenderId();
		if (_players.TryGetValue(senderId, out NetworkPlayer senderPlayer))
		{
			// Server now broadcasts this message to all clients (including itself locally for consistency)
			Rpc(nameof(RpcClientReceiveChatMessage), senderPlayer.Id, senderPlayer.Name, messageContent);
		}
		else
		{
			GD.PrintErr($"Chat message from unknown sender ID: {senderId}. Message: {messageContent}");
			// Optionally, could still broadcast but with "Unknown (ID:X)"
		}
	}

	// Called by the server, executed on all peers (AnyPeer) including the server itself (CallLocal = true)
	[Rpc(MultiplayerApi.RpcMode.AnyPeer, CallLocal = true, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	public void RpcClientReceiveChatMessage(long senderId, string senderName, string messageContent)
	{
		// Emit a signal that MainScene (or other UI controllers) can connect to.
		// This keeps NetworkManager decoupled from direct ChatLog access.
		EmitSignal(SignalName.ChatMessageReceived, senderId, senderName, messageContent);
	}
}
