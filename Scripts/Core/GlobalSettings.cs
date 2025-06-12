using Godot;
using Godot.Collections; // For Dictionary
// using System.Text.Json; // Not strictly needed if using Godot.Json for parsing

public partial class GlobalSettings : Node
{
	public const string DefaultGlobalAssetPath = "user://global_dungeon_assets/";
	private const string SettingsFilePath = "user://dungeon_tool_settings.json";

	private string currentGlobalAssetPath;

	// Grid Settings
	public Vector2I GridSize { get; private set; } = new Vector2I(16, 16);
	public bool IsSnapToGridEnabled { get; private set; } = false;

	[Signal]
	public delegate void GridSettingsChangedEventHandler(Vector2I newGridSize, bool newSnapEnabled);

	// Object Snapping Settings
	public bool IsObjectSnapEnabled { get; private set; } = false;
	public float ObjectSnapDistanceWorld { get; private set; } = 8.0f;
	public bool ObjectSnapToPivots { get; private set; } = true;
	public bool ObjectSnapToEdges { get; private set; } = true;

	[Signal]
	public delegate void ObjectSnapSettingsChangedEventHandler(bool enabled, float distance, bool snapToPivots, bool snapToEdges);


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
					// GD.Print($"GlobalSettings: Loaded global asset path: {currentGlobalAssetPath}");
					// Don't return yet, load other settings
				} else {
					GD.Print($"GlobalSettings: 'global_asset_library_path' not found or invalid in '{SettingsFilePath}'. Using default path.");
					currentGlobalAssetPath = DefaultGlobalAssetPath; // Ensure it's set before potential SaveSettings if other keys are also missing
				}

				// Load Grid Size
				Vector2I loadedGridSize = new Vector2I(16, 16); // Default for this load attempt
				if (settingsData != null && settingsData.TryGetValue("grid_size_x", out Variant gxVar) && gxVar.VariantType == Variant.Type.Int &&
					settingsData.TryGetValue("grid_size_y", out Variant gyVar) && gyVar.VariantType == Variant.Type.Int)
				{
					loadedGridSize = new Vector2I((int)gxVar, (int)gyVar);
				}
				GridSize = new Vector2I(Mathf.Max(1, loadedGridSize.X), Mathf.Max(1, loadedGridSize.Y));

				// Load Snap Enabled state
				bool loadedSnapEnabled = false; // Default for this load attempt
				if (settingsData != null && settingsData.TryGetValue("snap_to_grid_enabled", out Variant snapVar) && snapVar.VariantType == Variant.Type.Bool)
				{
					loadedSnapEnabled = snapVar.AsBool();
				}
				IsSnapToGridEnabled = loadedSnapEnabled;

				// Load Object Snapping Settings
				bool loadedObjSnapEnabled = false;
				if (settingsData != null && settingsData.TryGetValue("obj_snap_enabled", out Variant oseVar) && oseVar.VariantType == Variant.Type.Bool) {
					loadedObjSnapEnabled = oseVar.AsBool();
				}
				IsObjectSnapEnabled = loadedObjSnapEnabled;

				float loadedObjSnapDist = 8.0f;
				if (settingsData != null && settingsData.TryGetValue("obj_snap_dist_world", out Variant osdVar) && (osdVar.VariantType == Variant.Type.Float || osdVar.VariantType == Variant.Type.Int) ) {
					loadedObjSnapDist = osdVar.AsSingle();
				}
				ObjectSnapDistanceWorld = Mathf.Max(1.0f, loadedObjSnapDist);

				bool loadedObjSnapPivots = true;
				if (settingsData != null && settingsData.TryGetValue("obj_snap_to_pivots", out Variant ospVar) && ospVar.VariantType == Variant.Type.Bool) {
					loadedObjSnapPivots = ospVar.AsBool();
				}
				ObjectSnapToPivots = loadedObjSnapPivots;

				bool loadedObjSnapEdges = true;
				if (settingsData != null && settingsData.TryGetValue("obj_snap_to_edges", out Variant ose2Var) && ose2Var.VariantType == Variant.Type.Bool) { // Renamed oseVar to ose2Var
					loadedObjSnapEdges = ose2Var.AsBool();
				}
				ObjectSnapToEdges = loadedObjSnapEdges;

				GD.Print($"GlobalSettings: Loaded Path='{currentGlobalAssetPath}', GridSize={GridSize}, SnapEnabled={IsSnapToGridEnabled}, ObjSnapEnabled={IsObjectSnapEnabled}");
				return;
			}
		}
		else
		{
			GD.Print($"GlobalSettings: Settings file '{SettingsFilePath}' not found. Using default values and creating file.");
		}

		// If file did not exist or was totally unparsable, set all defaults here before saving.
		currentGlobalAssetPath = DefaultGlobalAssetPath;
		GridSize = new Vector2I(16, 16);
		IsSnapToGridEnabled = false;
		// Object snapping defaults
		IsObjectSnapEnabled = false;
		ObjectSnapDistanceWorld = 8.0f;
		ObjectSnapToPivots = true;
		ObjectSnapToEdges = true;

		SaveSettings(); // Create settings file with all current (default) values
	}

	private bool SaveSettings()
	{
		var settingsData = new Godot.Collections.Dictionary<string, Variant>
		{
			{ "global_asset_library_path", currentGlobalAssetPath },
			{ "grid_size_x", GridSize.X },
			{ "grid_size_y", GridSize.Y },
			{ "snap_to_grid_enabled", IsSnapToGridEnabled },
			// Add new object snapping settings
			{ "obj_snap_enabled", IsObjectSnapEnabled },
			{ "obj_snap_dist_world", ObjectSnapDistanceWorld },
			{ "obj_snap_to_pivots", ObjectSnapToPivots },
			{ "obj_snap_to_edges", ObjectSnapToEdges }
		};

		string jsonString = Json.Stringify(settingsData, "\t"); // Pretty print

		using var file = FileAccess.Open(SettingsFilePath, FileAccess.ModeFlags.Write);
		if (file == null) {
			GD.PrintErr($"GlobalSettings: Failed to write settings file '{SettingsFilePath}'. Error: {FileAccess.GetOpenError()}");
			return false;
		}
		file.StoreString(jsonString);
		// GD.Print($"GlobalSettings: Settings saved to {SettingsFilePath}. Path='{currentGlobalAssetPath}', GridSize={GridSize}, SnapEnabled={IsSnapToGridEnabled}");
		return true;
	}

	public void SetGridSize(Vector2I newSize)
	{
		Vector2I validatedSize = new Vector2I(Mathf.Max(1, newSize.X), Mathf.Max(1, newSize.Y));
		if (GridSize == validatedSize) return;

		GridSize = validatedSize;
		GD.Print($"GlobalSettings: GridSize set to {GridSize}");
		SaveSettings();
		EmitSignal(SignalName.GridSettingsChanged, GridSize, IsSnapToGridEnabled);
	}

	public void SetSnapToGridEnabled(bool enabled)
	{
		if (IsSnapToGridEnabled == enabled) return;

		IsSnapToGridEnabled = enabled;
		GD.Print($"GlobalSettings: IsSnapToGridEnabled set to {IsSnapToGridEnabled}");
		SaveSettings();
		EmitSignal(SignalName.GridSettingsChanged, GridSize, IsSnapToGridEnabled);
	}

	// --- Object Snapping Setters ---
	public void SetObjectSnapEnabled(bool enabled)
	{
		if (IsObjectSnapEnabled == enabled) return;
		IsObjectSnapEnabled = enabled;
		SaveSettingsAndNotifyObjectSnapChange();
	}

	public void SetObjectSnapDistanceWorld(float distance)
	{
		float validatedDistance = Mathf.Max(1.0f, distance);
		if (Mathf.IsEqualApprox(ObjectSnapDistanceWorld, validatedDistance)) return;
		ObjectSnapDistanceWorld = validatedDistance;
		SaveSettingsAndNotifyObjectSnapChange();
	}

	public void SetObjectSnapToPivots(bool enabled)
	{
		if (ObjectSnapToPivots == enabled) return;
		ObjectSnapToPivots = enabled;
		SaveSettingsAndNotifyObjectSnapChange();
	}

	public void SetObjectSnapToEdges(bool enabled)
	{
		if (ObjectSnapToEdges == enabled) return;
		ObjectSnapToEdges = enabled;
		SaveSettingsAndNotifyObjectSnapChange();
	}

	private void SaveSettingsAndNotifyObjectSnapChange()
	{
		SaveSettings();
		EmitSignal(SignalName.ObjectSnapSettingsChanged, IsObjectSnapEnabled, ObjectSnapDistanceWorld, ObjectSnapToPivots, ObjectSnapToEdges);
		GD.Print($"GlobalSettings: Object Snap Settings Changed - Enabled: {IsObjectSnapEnabled}, Dist: {ObjectSnapDistanceWorld}, Pivots: {ObjectSnapToPivots}, Edges: {ObjectSnapToEdges}");
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

	/// <summary>
	/// Snaps a given 2D position to the specified grid.
	/// An optional offset can be provided if the snapping origin is not (0,0)
	/// or if snapping the corner of an object rather than its pivot.
	/// </summary>
	/// <param name="position">The original position to snap.</param>
	/// <param name="gridCellSize">The size of each cell in the grid (X and Y dimensions).</param>
	/// <param name="gridOffset">An optional offset for the grid's origin. Defaults to (0,0).</param>
	/// <returns>The new position, snapped to the grid.</returns>
	public static Vector2 SnapPositionToGrid(Vector2 position, Vector2I gridCellSize, Vector2 gridOffset = default)
	{
		if (gridCellSize.X == 0 || gridCellSize.Y == 0)
		{
			// GD.PrintWarn("SnapPositionToGrid: Grid cell size components cannot be zero. Snapping disabled for this call.");
			return position;
		}

		float snappedX = Mathf.Round((position.X - gridOffset.X) / gridCellSize.X) * gridCellSize.X + gridOffset.X;
		float snappedY = Mathf.Round((position.Y - gridOffset.Y) / gridCellSize.Y) * gridCellSize.Y + gridOffset.Y;

		return new Vector2(snappedX, snappedY);
	}
}
