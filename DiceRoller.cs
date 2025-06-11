using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot; // For GD.Print, if used for internal debugging, though not strictly necessary for logic

public class DiceRollResult
{
	public int Total { get; }
	public List<int> IndividualRolls { get; }
	public string Notation { get; }
	public string Breakdown { get; }
	public bool IsSuccess { get; }
	public string ErrorMessage { get; }

	// Constructor for successful rolls
	public DiceRollResult(string notation, List<int> individualRolls, int total, string breakdown)
	{
		Notation = notation;
		IndividualRolls = individualRolls;
		Total = total;
		Breakdown = breakdown;
		IsSuccess = true;
		ErrorMessage = string.Empty;
	}

	// Constructor for failed rolls or flat numbers that don't "roll"
	public DiceRollResult(string notation, string message, bool success, int flatValue = 0, string breakdown = "")
	{
		Notation = notation;
		IsSuccess = success;
		ErrorMessage = success ? string.Empty : message;
		IndividualRolls = success ? new List<int> { flatValue } : new List<int>();
		Total = success ? flatValue : 0;
		Breakdown = success ? breakdown : string.Empty;
	}
}

public static class DiceRoller
{
	private static Random _random = new Random();
	// Regex to capture:
	// 1. (Optional) Number of dice (e.g., "3" in "3d6")
	// 2. The 'd' literal
	// 3. Dice type (e.g., "6" in "3d6", or "20" in "d20")
	// 4. (Optional) Modifier sign (+ or -)
	// 5. (Optional) Modifier value (e.g., "5" in "d20+5")
	// OR, capture a flat number.
	private static readonly Regex DiceNotationRegex = new Regex(
		@"^(?:(\d+)?d(\d+)(?:([+-])(\d+))?|(\d+)$)",
		RegexOptions.IgnoreCase | RegexOptions.Compiled
	);

	public static DiceRollResult Roll(string diceNotation)
	{
		if (string.IsNullOrWhiteSpace(diceNotation))
		{
			return new DiceRollResult(diceNotation, "Input notation cannot be empty.", false);
		}

		Match match = DiceNotationRegex.Match(diceNotation.Trim());

		if (!match.Success)
		{
			return new DiceRollResult(diceNotation, $"Invalid dice notation format: '{diceNotation}'. Expected examples: '2d6', 'd20+3', '10'.", false);
		}

		// Case 1: Flat number (e.g., "10")
		if (match.Groups[5].Success)
		{
			if (int.TryParse(match.Groups[5].Value, out int flatNumber))
			{
				return new DiceRollResult(diceNotation, string.Empty, true, flatNumber, $"[{flatNumber}] = {flatNumber}");
			}
			else
			{
				// Should not happen if regex matches group 5, but as a safeguard
				return new DiceRollResult(diceNotation, "Invalid flat number.", false);
			}
		}

		// Case 2: Dice roll (e.g., "3d6+2", "d20")
		int numberOfDice = 1;
		if (match.Groups[1].Success && !string.IsNullOrEmpty(match.Groups[1].Value))
		{
			if (!int.TryParse(match.Groups[1].Value, out numberOfDice) || numberOfDice <= 0)
			{
				return new DiceRollResult(diceNotation, "Number of dice must be a positive integer.", false);
			}
		}

		if (!int.TryParse(match.Groups[2].Value, out int diceType) || diceType <= 0)
		{
			return new DiceRollResult(diceNotation, "Dice type must be a positive integer.", false);
		}
		if (numberOfDice > 1000) // Safety limit
		{
			return new DiceRollResult(diceNotation, "Cannot roll more than 1000 dice.", false);
		}
		if (diceType > 1000) // Safety limit
		{
			return new DiceRollResult(diceNotation, "Dice type cannot exceed 1000 sides.", false);
		}


		int modifier = 0;
		string modifierString = string.Empty;
		if (match.Groups[3].Success && match.Groups[4].Success)
		{
			if (!int.TryParse(match.Groups[4].Value, out int modValue))
			{
				return new DiceRollResult(diceNotation, "Invalid modifier value.", false);
			}
			modifier = match.Groups[3].Value == "+" ? modValue : -modValue;
			modifierString = $" {match.Groups[3].Value} {modValue}";
		}
		else if (match.Groups[3].Success && !match.Groups[4].Success) // e.g. "2d6+"
		{
			return new DiceRollResult(diceNotation, "Missing modifier value after sign.", false);
		}


		List<int> individualRolls = new List<int>();
		int total = 0;

		for (int i = 0; i < numberOfDice; i++)
		{
			int roll = _random.Next(1, diceType + 1);
			individualRolls.Add(roll);
			total += roll;
		}

		total += modifier;
		string breakdown = $"[{string.Join(", ", individualRolls)}]{modifierString} = {total}";

		return new DiceRollResult(diceNotation, individualRolls, total, breakdown);
	}
}
