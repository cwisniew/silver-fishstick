using Godot;
using System.Collections.Generic; // For List
using System.Linq; // For Select

public static class CampaignManager
{
	private const string CampaignFileExtension = ".vttcamp"; // Not used actively yet, but good for FileDialog

	public static void SaveCampaign(string filePath, MainScene mainScene)
	{
		if (mainScene == null)
		{
			GD.PrintErr("CampaignManager: MainScene instance is null. Cannot save campaign.");
			return;
		}

		var rootData = new CampaignRootData();

		// 1. Populate Map Data
		if (mainScene.MapBackgroundSprite?.Texture != null)
		{
			rootData.CurrentMapPath = mainScene.MapBackgroundSprite.Texture.ResourcePath;
		} else {
			rootData.CurrentMapPath = string.Empty; // Or null, depending on how Load handles it
		}

		// 2. Populate Token Data
		rootData.Tokens = new List<TokenData>();
		foreach (Token token in mainScene.GetTokens()) // Assumes GetTokens() returns IEnumerable<Token> or similar
		{
			var tokenData = new TokenData
			{
				Position = new Vector2Data(token.GlobalPosition),
				RotationDegrees = token.RotationDegrees,
				TexturePath = token.TokenTexture?.ResourcePath ?? string.Empty,
				SheetData = token.Sheet != null ? new CharacterSheetData(token.Sheet) : new CharacterSheetData(), // Save sheet
				HasVision = token.HasVision,
				VisionRangeGameUnits = token.VisionRangeGameUnits,
				NodeName = token.Name.ToString(), // Godot.StringName to string
				Size = new Vector2Data(token.Size)
			};
			rootData.Tokens.Add(tokenData);
		}

		// 3. Populate Combat State (Simplified: just get current state)
		if (mainScene.CombatTracker != null) // Assuming mainScene has a CombatTracker property
		{
			rootData.CombatState = mainScene.CombatTracker.GetCombatTrackerData(); // New method needed in CombatTracker
		}


		// 4. Populate Drawings (Simplified: get current drawings)
		if (mainScene.DrawingOverlay != null) // Assuming mainScene has a DrawingOverlay property
		{
			rootData.Drawings = mainScene.DrawingOverlay.GetDrawingData(); // New method needed in DrawingOverlay
		}


		// Serialize rootData to Godot Dictionary then to JSON string
		var dataDict = rootData.ToDictionary();
		string jsonString = Json.Stringify(dataDict, "\t"); // Pretty print

		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			Error err = FileAccess.GetOpenError();
			mainScene.ChatLogNode?.AddMessage($"Error saving campaign: Cannot open file. Code: {err}", Colors.Red);
			GD.PrintErr($"Error saving campaign to '{filePath}': {err}");
			return;
		}
		file.StoreString(jsonString);
		// file.Close(); // Using 'using' statement handles this

		mainScene.ChatLogNode?.AddMessage($"Campaign saved to: {filePath.GetFile()}", Colors.Green);
		GD.Print($"Campaign saved to: {filePath}");
	}

	public static bool LoadCampaign(string filePath, MainScene mainScene)
	{
		if (mainScene == null)
		{
			GD.PrintErr("CampaignManager: MainScene instance is null. Cannot load campaign.");
			return false;
		}
		if (!FileAccess.FileExists(filePath))
		{
			mainScene.ChatLogNode?.AddMessage($"Error loading campaign: File not found at '{filePath}'.", Colors.Red);
			GD.PrintErr($"Error loading campaign: File not found at '{filePath}'.");
			return false;
		}

		using var file = FileAccess.Open(filePath, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			Error err = FileAccess.GetOpenError();
			mainScene.ChatLogNode?.AddMessage($"Error loading campaign: Cannot open file. Code: {err}", Colors.Red);
			GD.PrintErr($"Error loading campaign from '{filePath}': {err}");
			return false;
		}

		string jsonString = file.GetAsText();
		// file.Close(); // Using 'using' handles this

		if (string.IsNullOrWhiteSpace(jsonString))
		{
			mainScene.ChatLogNode?.AddMessage($"Error loading campaign: File '{filePath.GetFile()}' is empty.", Colors.Red);
			GD.PrintErr($"Error loading campaign: File '{filePath.GetFile()}' is empty.");
			return false;
		}

		Json jsonParser = new Json();
		Error parseError = jsonParser.Parse(jsonString);
		if (parseError != Error.Ok)
		{
			mainScene.ChatLogNode?.AddMessage($"Error loading campaign: Failed to parse JSON. Error: {jsonParser.GetErrorMessage()} at line {jsonParser.GetErrorLine()}", Colors.Red);
			GD.PrintErr($"Error parsing campaign JSON: {jsonParser.GetErrorMessage()} at line {jsonParser.GetErrorLine()}");
			return false;
		}

		if (jsonParser.Data.VariantType != Variant.Type.Dictionary)
		{
			mainScene.ChatLogNode?.AddMessage("Error loading campaign: Invalid JSON format (root is not a Dictionary).", Colors.Red);
			GD.PrintErr("Error loading campaign: Invalid JSON format (root is not a Dictionary).");
			return false;
		}

		var rootDataDict = jsonParser.Data.AsGodotDictionary();
		CampaignRootData rootData = CampaignRootData.FromDictionary(rootDataDict);

		// Apply loaded data
		mainScene.ClearExistingCampaignState();

		// 1. Load Map
		if (!string.IsNullOrEmpty(rootData.CurrentMapPath))
		{
			mainScene.LoadMap(rootData.CurrentMapPath); // Assumes LoadMap handles UI updates like chat log
		} else {
			mainScene.LoadMap(null); // Clear map if path is empty
		}

		// 2. Spawn Tokens
		if (rootData.Tokens != null)
		{
			foreach (TokenData tokenData in rootData.Tokens)
			{
				// SpawnToken now returns the instance
				Token newToken = mainScene.SpawnTokenAndApplyData(tokenData); // New method to prevent double logging / default sheet
				// ApplyTokenData is now called within SpawnTokenAndApplyData
			}
		}

		// 3. Restore Combat State (Simplified: apply to existing tracker)
		if (rootData.CombatState != null && mainScene.CombatTracker != null)
		{
			mainScene.CombatTracker.ApplyCombatTrackerData(rootData.CombatState, mainScene.GetTokens()); // New method in CombatTracker
		}

		// 4. Restore Drawings (Simplified: apply to existing overlay)
		if (rootData.Drawings != null && mainScene.DrawingOverlay != null)
		{
			mainScene.DrawingOverlay.ApplyDrawingData(rootData.Drawings); // New method in DrawingOverlay
		}


		mainScene.ChatLogNode?.AddMessage($"Campaign loaded from: {filePath.GetFile()}", Colors.Green);
		GD.Print($"Campaign loaded from: {filePath}");
		return true;
	}
}

// We'll need to add GetCombatTrackerData, ApplyCombatTrackerData to CombatTracker.cs
// And GetDrawingData, ApplyDrawingData to DrawingOverlay.cs
// And a new SpawnTokenAndApplyData in MainScene.cs
// And CharacterSheetData/CombatantData will need From/ToDictionary
// This iteration focuses on map/token basics. The DTOs are there but full application is for future.
// The DTOs for CombatantData and CombatTrackerData, DrawingLineData are already in CampaignSaveData.cs
// We need to add the methods to CombatTracker.cs and DrawingOverlay.cs for them to provide/apply their data.
// And a way for CombatTracker to re-link tokens on load (e.g. via NodeName).
// CharacterSheet DTO is also there, Token.ApplyTokenData already handles it.
// For this step, focus on map and token (pos, texture, basic sheet, vision, size)
// The CampaignManager structure is set up to call the methods.
// Need to create placeholder methods in CombatTracker and DrawingOverlay, and SpawnTokenAndApplyData in MainScene.
