using Godot;

public class NetworkPlayer
{
	public long Id { get; set; }
	public string Name { get; set; }

	// Parameterless constructor for deserialization
	public NetworkPlayer() { }

	public NetworkPlayer(long id, string name)
	{
		Id = id;
		Name = name;
	}

	public override string ToString()
	{
		return $"Player (ID: {Id}, Name: {Name})";
	}

	// For Godot JSON serialization (manual conversion to Dictionary)
	public Godot.Collections.Dictionary ToDictionary()
	{
		return new Godot.Collections.Dictionary
		{
			{ "Id", Id },
			{ "Name", Name }
		};
	}

	public static NetworkPlayer FromDictionary(Godot.Collections.Dictionary dict)
	{
		var player = new NetworkPlayer();
		player.Id = dict.ContainsKey("Id") ? dict["Id"].AsInt64() : 0; // Default to 0 or handle error if ID is crucial
		player.Name = dict.ContainsKey("Name") ? dict["Name"].ToString() : "Unnamed Player";
		return player;
	}
}
