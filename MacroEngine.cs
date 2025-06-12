using Godot;
using System.Text.RegularExpressions;

public static class MacroEngine
{
	// Simple regex for CHAT command: CHAT "message"
	private static readonly Regex ChatCommandRegex = new Regex(@"^CHAT\s+""([^""]*)""\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
	// Simple regex for ROLL command: ROLL <dice_notation> (e.g., ROLL 2d6+3)
	private static readonly Regex RollCommandRegex = new Regex(@"^ROLL\s+([^\s]+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	public static void ExecuteMacro(string macroScript, MacroContext context)
	{
		if (string.IsNullOrWhiteSpace(macroScript) || context == null)
		{
			return;
		}

		var lines = macroScript.Split('\n');

		foreach (var rawLine in lines)
		{
			string line = rawLine.Trim();
			if (string.IsNullOrEmpty(line) || line.StartsWith("#")) // Skip empty lines and comments
			{
				continue;
			}

			bool commandMatched = false;

			// Try CHAT command
			Match chatMatch = ChatCommandRegex.Match(line);
			if (chatMatch.Success)
			{
				string message = chatMatch.Groups[1].Value;
				context.Chat?.AddMessage($"[MACRO] {message}", Colors.Orange);
				GD.Print($"Macro Action: CHAT \"{message}\"");
				commandMatched = true;
			}

			// Try ROLL command if CHAT didn't match
			if (!commandMatched)
			{
				Match rollMatch = RollCommandRegex.Match(line);
				if (rollMatch.Success)
				{
					string diceNotation = rollMatch.Groups[1].Value;
					DiceRollResult result = DiceRoller.Roll(diceNotation); // DiceRoller is static

					string outputMessage;
					Color messageColor;

					if (result.IsSuccess)
					{
						outputMessage = $"Macro Roll ({result.Notation}): {result.Breakdown}";
						messageColor = Colors.DarkOrange; // Using a different shade of orange
					}
					else
					{
						outputMessage = $"Macro Roll Error ({result.Notation}): {result.ErrorMessage}";
						messageColor = Colors.DarkRed;
					}

					context.Chat?.AddMessage($"[MACRO] {outputMessage}", messageColor);
					GD.Print(outputMessage);
					commandMatched = true;
				}
			}

			// Unknown command
			if (!commandMatched)
			{
				context.Chat?.AddMessage($"[MACRO ERROR] Unknown command: \"{line}\"", Colors.Red);
				GD.PrintErr($"Macro Error: Unknown command: \"{line}\"");
			}
		}
	}

	/*
	DESIGN NOTES & REFLECTIONS ON MACRO SCRIPTING SYSTEM:

	**1. Current Approach & Limitations:**
	*   The current engine is extremely basic. It processes a script line by line, using simple regular expressions to identify commands.
	*   Supported Commands: `CHAT "message"` and `ROLL <dice_notation>`.
	*   No variables, control flow (IF/ELSE, LOOPs), functions, or complex expressions.
	*   Error handling is rudimentary (logs unknown commands).
	*   Context is passed in, providing limited access to game systems (Chat, SelectedToken, etc.).

	**2. Alternative Parsing/Execution Strategies:**

	*   **Full Custom Parser (Lexer/Parser):**
		*   Create a formal grammar for the macro language.
		*   Use tools like ANTLR to generate a lexer and parser, or build them manually.
		*   The parser would build an Abstract Syntax Tree (AST).
		*   An interpreter would then "walk" the AST to execute the macro.
		*   Pros: Maximum control over syntax and features. Highly optimized for the specific DSL.
		*   Cons: Very complex and time-consuming to implement correctly. Steep learning curve for parser generators. Debugging can be hard.

	*   **Embedded Scripting Languages:**
		*   Integrate an existing scripting language interpreter into the Godot application.
		*   Examples for C#:
			*   **NLua (Lua):** Mature Lua interpreter for .NET. Lua is lightweight, fast, and designed for embedding.
			*   **IronPython (Python):** Python implementation on .NET. More powerful but heavier.
			*   **Jint (JavaScript):** JavaScript interpreter. Good if web familiarity is a plus.
			*   **MoonSharp (Lua):** Another popular Lua interpreter for C#.
		*   Pros: Leverages powerful, well-tested languages with existing syntax, libraries, and often good documentation. Users might already know the language.
		*   Cons:
			*   Adds external dependencies to the project.
			*   Interop between C# (Godot) and the scripting language can be complex (exposing game objects/functions, handling data types).
			*   Performance overhead compared to a native or highly optimized custom solution.
			*   Larger security surface: these languages are often powerful and could potentially access things outside the intended scope if not sandboxed properly.

	*   **C# Scripting (Roslyn Scripting API):**
		*   Allows compiling and running C# code snippets dynamically at runtime.
		*   Pros: Uses full C# language features. Seamless interop with existing Godot C# code. Potentially very powerful.
		*   Cons:
			*   Can have compilation overhead for small, frequently run scripts.
			*   Larger dependency (Roslyn).
			*   Setting up the scripting environment, passing context, and managing security (sandboxing) can be complex.
			*   Might be overkill for simple macros if users are not C# developers.

	*   **Godot's `Expression` Class:**
		*   `var expression = new Expression(); expression.Parse("2 + 2 * (some_var + MyNode.property)"); var result = expression.Execute(null, this);`
		*   Pros: Built into Godot. Very simple for evaluating mathematical expressions or accessing properties on nodes.
		*   Cons: Extremely limited. Not a DSL. No statements, no control flow, no function calls (beyond simple method calls on objects passed in). Not suitable for a multi-command macro script.

	**3. Security Considerations:**
	*   **Sandboxing:** If using powerful embedded languages (Lua, Python, JS, C# scripting), it's CRUCIAL to implement sandboxing. This means restricting what the scripts can access (file system, network, arbitrary .NET classes/Godot nodes). Untrusted scripts could otherwise pose a security risk.
	*   **API Design:** The `MacroContext` is a step towards this, providing a controlled interface. This API should be carefully designed to expose only necessary and safe functionality.
	*   **Resource Limits:** For more complex scripts, consider imposing limits on execution time, memory usage, or loop iterations to prevent denial-of-service by runaway scripts.
	*   **Current Regex Approach:** Relatively safe as it only parses specific, simple patterns. The main risk is if the executed actions (like CHAT) have vulnerabilities (e.g., if chat messages could somehow inject other commands, which is not the case here).

	**4. Future Expansion Ideas:**
	*   **Variables:** `SET myVar = ROLL 1d20` ; `CHAT "You rolled {myVar}"`
	*   **Functions:** `DEFINE myRoll(numDice) { RETURN ROLL {numDice}d6 }` ; `CALL myRoll(3)`
	*   **Control Flow:** `IF (ROLL 1d20 > 10) { CHAT "Success!" } ELSE { CHAT "Failure." }` ; `LOOP 5 { CHAT "Hello" }`
	*   **Targeting:** `TARGET @NearestEnemy ; CHAT "{target.name} is hit!"` (Requires API to get game objects).
	*   **Game System Integration:**
		*   `DECK DrawCard "Main Deck"`
		*   `SOUND Play "explosion.wav"`
		*   `COMBAT NextTurn`
		*   `TOKEN @Selected SetHP -5`
	*   **User Interface:** A dedicated macro editor panel, list of saved macros, quick slots.
	*   **Error Reporting:** More detailed error messages with line numbers.

	**5. MacroContext Design:**
	*   The `MacroContext` acts as the bridge between the macro script and the game's systems.
	*   It should be designed to be "minimal but sufficient," exposing only what macros need.
	*   Avoid passing direct references to sensitive Godot nodes or managers if possible; instead, provide wrapper methods or properties that perform validation and controlled actions (e.g., `context.TargetedToken.TakeDamage(10)` instead of direct access to `_selectedToken.Sheet.CurrentHealthPoints -= 10`).
	*   The `Variables` dictionary in the context allows for simple state to be stored *within* a single macro execution or potentially across multiple calls if the context is persisted (though the current implementation creates a new context per execution).
	*   For more advanced scenarios (like user-defined functions or persistent state between macro runs), the context might need to be managed differently or the `MacroEngine` would need to store global/user-defined functions and variables.
	*/
}
