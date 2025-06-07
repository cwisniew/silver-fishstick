using Godot;
using Godot.Collections; // For Dictionary
// using System.Text.Json; // Not strictly needed if using Godot.Json for parsing

public partial class GlobalSettings : Node
{
	public const string DefaultGlobalAssetPath = "user://global_dungeon_assets/";
	private const string SettingsFilePath = "user://dungeon_tool_settings.json";

	private string currentGlobalAssetPath;

	public override void _Ready()
	{
		LoadSettings();
		EnsureDefaultAssetDirectoryExists(); // Ensures directories exist at the currentGlobalAssetPath
	}

	public string GetGlobalAssetPath()
	{
		// Ensure path is loaded if called before _Ready somehow, or rely on _Ready always first.
		if (string.IsNullOrEmpty(currentGlobalAssetPath)) {
			LoadSettings(); // Defensive: ensure settings are loaded
		}
		return currentGlobalAssetPath;
	}

	public bool SetGlobalAssetPath(string newPath)
	{
		if (string.IsNullOrWhiteSpace(newPath))
		{
			GD.PrintErr("GlobalSettings: Global asset path cannot be empty.");
			return false;
		}

		if (!newPath.StartsWith("user://") && !DirAccess.DirExistsAbsolute(newPath))
		{
			GD.Print($"GlobalSettings: Warning: Path '{newPath}' is not 'user://' and may not yet exist or be ideal for cross-platform use.");
		}

		string oldPath = currentGlobalAssetPath;
		currentGlobalAssetPath = newPath.EndsWith("/") ? newPath : newPath + "/";

		if (currentGlobalAssetPath == oldPath) {
			// GD.Print("GlobalSettings: Path not changed.");
			EnsureDefaultAssetDirectoryExists(); // Still ensure it exists
			return true;
		}

		if (SaveSettings()) {
			GD.Print($"GlobalSettings: Global asset path changed to: {currentGlobalAssetPath}. Consider app restart if assets were loaded from old path.");
			EnsureDefaultAssetDirectoryExists(); // Attempt to create/verify new path
			return true;
		} else {
			currentGlobalAssetPath = oldPath; // Revert on save failure
			GD.PrintErr($"GlobalSettings: Failed to save new path, reverted to {oldPath}.");
			return false;
		}
	}

	private void LoadSettings()
	{
		if (FileAccess.FileExists(SettingsFilePath))
		{
			using var file = FileAccess.Open(SettingsFilePath, FileAccess.ModeFlags.Read);
			if (file == null) {
				GD.PrintErr($"GlobalSettings: Failed to open settings file '{SettingsFilePath}'. Error: {FileAccess.GetOpenError()}");
				currentGlobalAssetPath = DefaultGlobalAssetPath;
				SaveSettings(); // Attempt to save a default one
				return;
			}
			string content = file.GetAsText();

			Variant parsedVariant = Json.ParseString(content);
			if (parsedVariant.VariantType == Variant.Type.Nil) {
				GD.PrintErr($"GlobalSettings: Error parsing settings JSON from '{SettingsFilePath}'. Content: '{content}'. Error: {Json.GetErrorMessage()} at line {Json.GetErrorLine()}. Using default path.");
			} else {
				var settingsData = parsedVariant.AsGodotDictionary<string, Variant>();
				if (settingsData != null && settingsData.TryGetValue("global_asset_library_path", out Variant pathVar) && pathVar.VariantType == Variant.Type.String)
				{
					currentGlobalAssetPath = pathVar.AsString();
					if (string.IsNullOrWhiteSpace(currentGlobalAssetPath)) {
						currentGlobalAssetPath = DefaultGlobalAssetPath;
					}
					currentGlobalAssetPath = currentGlobalAssetPath.EndsWith("/") ? currentGlobalAssetPath : currentGlobalAssetPath + "/";
					GD.Print($"GlobalSettings: Loaded global asset path: {currentGlobalAssetPath}");
					return;
				} else {
					GD.PrintErr($"GlobalSettings: 'global_asset_library_path' not found or invalid in '{SettingsFilePath}'. Using default path.");
				}
			}
		}
		else
		{
			GD.Print($"GlobalSettings: Settings file '{SettingsFilePath}' not found. Using default path and creating file.");
		}

		currentGlobalAssetPath = DefaultGlobalAssetPath;
		SaveSettings(); // Create settings file with default path
	}

	private bool SaveSettings()
	{
		var settingsData = new Godot.Collections.Dictionary<string, Variant>
		{
			{ "global_asset_library_path", currentGlobalAssetPath }
		};

		string jsonString = Json.Stringify(settingsData, "\t"); // Pretty print

		using var file = FileAccess.Open(SettingsFilePath, FileAccess.ModeFlags.Write);
		if (file == null) {
			GD.PrintErr($"GlobalSettings: Failed to write settings file '{SettingsFilePath}'. Error: {FileAccess.GetOpenError()}");
			return false;
		}
		file.StoreString(jsonString);
		// GD.Print($"GlobalSettings: Settings saved to {SettingsFilePath} with path {currentGlobalAssetPath}");
		return true;
	}

	private void EnsureDefaultAssetDirectoryExists()
	{
		// Ensures base global asset directory and subdirectories exist at currentGlobalAssetPath
		if (string.IsNullOrEmpty(currentGlobalAssetPath)) {
			GD.PrintErr("GlobalSettings: Cannot ensure asset directory, path is not set.");
			return;
		}

		string path = currentGlobalAssetPath; // Already ends with "/"
		string[] subDirs = { "images", "metadata", "tilesets" };

		// Ensure the base path itself exists first
		if (!DirAccess.DirExistsAbsolute(path)) {
			Error errBase = DirAccess.MakeDirRecursiveAbsolute(path);
			if (errBase != Error.Ok) {
				GD.PrintErr($"GlobalSettings: Failed to create base directory: {path}, Error: {errBase}");
				return; // If base fails, subdirs will also fail.
			} else {
				GD.Print($"GlobalSettings: Created base directory: {path}");
			}
		}

		foreach (string subDir in subDirs)
		{
			string fullSubDirPath = path.PlusFile(subDir); // Correctly joins path segments
			if (!DirAccess.DirExistsAbsolute(fullSubDirPath))
			{
				Error err = DirAccess.MakeDirRecursiveAbsolute(fullSubDirPath);
				if (err != Error.Ok)
				{
					GD.PrintErr($"GlobalSettings: Failed to create subdirectory: {fullSubDirPath}, Error: {err}");
				}
				else
				{
					GD.Print($"GlobalSettings: Created subdirectory: {fullSubDirPath}");
				}
			}
		}
	}
}
