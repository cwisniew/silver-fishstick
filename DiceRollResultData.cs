using Godot;
using System.Collections.Generic;
using System.Linq; // For Select

public class DiceRollResultData
{
	public string Notation { get; set; }
	public List<int> IndividualRolls { get; set; } // Stored as List<int>
	public int Total { get; set; }
	public string Breakdown { get; set; }
	public bool IsSuccess { get; set; }
	public string ErrorMessage { get; set; }

	public DiceRollResultData()
	{
		IndividualRolls = new List<int>(); // Initialize list
	}

	public static DiceRollResultData FromDiceRollResult(DiceRollResult realResult)
	{
		if (realResult == null) return null;
		return new DiceRollResultData
		{
			Notation = realResult.Notation,
			// Ensure IndividualRolls is copied correctly. If it's already List<int>, this is fine.
			IndividualRolls = realResult.IndividualRolls != null ? new List<int>(realResult.IndividualRolls) : new List<int>(),
			Total = realResult.Total,
			Breakdown = realResult.Breakdown,
			IsSuccess = realResult.IsSuccess,
			ErrorMessage = realResult.ErrorMessage
		};
	}

	public Godot.Collections.Dictionary ToDictionary()
	{
		// Convert List<int> to Godot.Collections.Array for serialization
		var rollsArray = new Godot.Collections.Array();
		if (IndividualRolls != null)
		{
			foreach (int roll in IndividualRolls)
			{
				rollsArray.Add(roll);
			}
		}

		return new Godot.Collections.Dictionary
		{
			{ "Notation", Notation },
			{ "IndividualRolls", rollsArray },
			{ "Total", Total },
			{ "Breakdown", Breakdown },
			{ "IsSuccess", IsSuccess },
			{ "ErrorMessage", ErrorMessage }
		};
	}

	public static DiceRollResultData FromDictionary(Godot.Collections.Dictionary dict)
	{
		var data = new DiceRollResultData();
		data.Notation = dict.GetOrDefault("Notation", "").ToString();

		if (dict.ContainsKey("IndividualRolls") && dict["IndividualRolls"].VariantType == Variant.Type.Array)
		{
			var rollsArray = dict["IndividualRolls"].AsGodotArray();
			data.IndividualRolls = new List<int>();
			foreach (var rollVariant in rollsArray)
			{
				data.IndividualRolls.Add(rollVariant.AsInt32());
			}
		}
		else
		{
			data.IndividualRolls = new List<int>();
		}

		data.Total = dict.GetOrDefault("Total", 0).AsInt32();
		data.Breakdown = dict.GetOrDefault("Breakdown", "").ToString();
		data.IsSuccess = dict.GetOrDefault("IsSuccess", false).AsBool();
		data.ErrorMessage = dict.GetOrDefault("ErrorMessage", "").ToString();
		return data;
	}
}
// Assumes DiceRollResult class exists and has these properties:
// string Notation, List<int> IndividualRolls, int Total, string Breakdown, bool IsSuccess, string ErrorMessage
// The GodotDictionaryExtensions.GetOrDefault should already be in CampaignSaveData.cs,
// but if not, or if this file is compiled separately, it might be needed here or referenced.
// For now, assuming it's globally accessible or will be handled by the existing one.
// If DiceRollResult.IndividualRolls is not List<int> but e.g. Godot.Collections.Array, FromDiceRollResult needs adjustment.
// Assuming it's List<int> as per typical C# collections.
