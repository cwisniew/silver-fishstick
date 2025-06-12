using Godot;
using System.Collections.Generic;

public partial class SoundManager : Node
{
	private AudioStreamPlayer _sfxPlayer;
	private AudioStreamPlayer _musicPlayer;
	private Dictionary<string, AudioStream> _sfxCache = new Dictionary<string, AudioStream>();

	// Keep track of the finished signal handler for music looping
	private Callable _musicFinishedCallable;
	private bool _loopMusic = false;
	private string _currentMusicName; // To store the name of the current music for looping

	public override void _Ready()
	{
		_sfxPlayer = new AudioStreamPlayer();
		AddChild(_sfxPlayer);

		_musicPlayer = new AudioStreamPlayer();
		// Attempt to set Bus. If "Music" bus doesn't exist, it will use "Master".
		// User should create "Music" bus in Godot Editor Audio tab for best results.
		_musicPlayer.Bus = "Music";
		AddChild(_musicPlayer);

		// Prepare callable for the Finished signal
		_musicFinishedCallable = new Callable(this, MethodName.OnMusicFinished);
	}

	private AudioStream LoadSfx(string soundName)
	{
		if (string.IsNullOrEmpty(soundName)) return null;

		if (_sfxCache.TryGetValue(soundName, out AudioStream cachedStream))
		{
			return cachedStream;
		}

		string path = $"res://assets/sounds/{soundName}";
		if (!FileAccess.FileExists(path) && !FileAccess.FileExists(path + ".import")) // Check for actual file or imported version
		{
			// If it's a .txt placeholder, this check will also fail, which is intended.
			GD.Print($"SoundManager: SFX file or placeholder not found at '{path}'. Cannot load.");
			return null;
		}

		AudioStream stream = ResourceLoader.Load<AudioStream>(path, cacheMode: ResourceLoader.CacheMode.Reuse);
		if (stream != null)
		{
			_sfxCache[soundName] = stream;
		}
		else
		{
			GD.PrintErr($"SoundManager: Failed to load sound '{soundName}' at '{path}'. ResourceLoader returned null.");
		}
		return stream;
	}

	public void PlaySfx(string soundName, float volumeDb = 0.0f)
	{
		AudioStream stream = LoadSfx(soundName);
		if (stream != null)
		{
			_sfxPlayer.Stream = stream;
			_sfxPlayer.VolumeDb = volumeDb;
			_sfxPlayer.Play();
		}
		else
		{
			// This will now only print if LoadSfx itself printed an error or if soundName was empty
			// GD.Print($"SoundManager: Simulated playing SFX '{soundName}' (stream was null).");
		}
	}

	public void PlayMusic(string musicName, float volumeDb = 0.0f, bool loop = true)
	{
		if (string.IsNullOrEmpty(musicName)) return;

		_currentMusicName = musicName; // Store for looping
		_loopMusic = loop;

		string path = $"res://assets/sounds/{musicName}";
		if (!FileAccess.FileExists(path) && !FileAccess.FileExists(path + ".import"))
		{
			GD.Print($"SoundManager: Music file or placeholder not found at '{path}'. Cannot play.");
			return;
		}

		AudioStream stream = ResourceLoader.Load<AudioStream>(path, cacheMode: ResourceLoader.CacheMode.Reuse);
		if (stream != null)
		{
			// Disconnect previous listener if any, before connecting again or changing stream
			if (_musicPlayer.IsConnected(AudioStreamPlayer.SignalName.Finished, _musicFinishedCallable))
			{
				_musicPlayer.Disconnect(AudioStreamPlayer.SignalName.Finished, _musicFinishedCallable);
			}

			_musicPlayer.Stream = stream;
			_musicPlayer.VolumeDb = volumeDb;
			_musicPlayer.Play();

			if (_loopMusic)
			{
				_musicPlayer.Connect(AudioStreamPlayer.SignalName.Finished, _musicFinishedCallable);
			}
		}
		else
		{
			GD.PrintErr($"SoundManager: Failed to load music '{musicName}' at '{path}'.");
		}
	}

	private void OnMusicFinished()
	{
		if (_loopMusic && !string.IsNullOrEmpty(_currentMusicName) && _musicPlayer.Stream != null)
		{
			// Re-play the current stream.
			// No need to call PlayMusic again, just Play on the player.
			_musicPlayer.Play();
		}
		// If not looping, or if stream somehow became null, it will just stop.
	}

	public void StopMusic()
	{
		_loopMusic = false; // Prevent loop from restarting if explicitly stopped
		if (_musicPlayer.IsConnected(AudioStreamPlayer.SignalName.Finished, _musicFinishedCallable))
		{
			_musicPlayer.Disconnect(AudioStreamPlayer.SignalName.Finished, _musicFinishedCallable);
		}
		_musicPlayer.Stop();
	}

	public void SetMusicVolume(float volumeDb) => _musicPlayer.VolumeDb = volumeDb;
	public void SetSfxVolumeDb(float volumeDb) => _sfxPlayer.VolumeDb = volumeDb; // For next SFX
}
