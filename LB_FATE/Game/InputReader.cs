namespace LB_FATE;

public static class InputReader
{
    public static string? ReadLineWithCompletion(IAutoComplete? autoComplete = null)
    {
        // Check if console is available for interactive input
        if (!IsConsoleAvailable())
        {
            return Console.ReadLine();
        }

        // Record the starting position (after the prompt ">")
        int promptOffset = 0;
        try
        {
            promptOffset = Console.CursorLeft;
        }
        catch
        {
            // If we can't get cursor position, assume no prompt
            promptOffset = 0;
        }

        var buffer = new List<char>();
        var cursorPos = 0;

        while (true)
        {
            ConsoleKeyInfo key;
            try
            {
                key = Console.ReadKey(intercept: true);
            }
            catch
            {
                // Fallback to simple ReadLine if console input fails
                return Console.ReadLine();
            }

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return new string(buffer.ToArray());
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursorPos > 0)
                {
                    buffer.RemoveAt(cursorPos - 1);
                    cursorPos--;
                    RedrawLine(buffer, cursorPos, promptOffset);
                }
                continue;
            }

            if (key.Key == ConsoleKey.Delete)
            {
                if (cursorPos < buffer.Count)
                {
                    buffer.RemoveAt(cursorPos);
                    RedrawLine(buffer, cursorPos, promptOffset);
                }
                continue;
            }

            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (cursorPos > 0)
                {
                    cursorPos--;
                    try { Console.SetCursorPosition(promptOffset + cursorPos, Console.CursorTop); } catch { }
                }
                continue;
            }

            if (key.Key == ConsoleKey.RightArrow)
            {
                if (cursorPos < buffer.Count)
                {
                    cursorPos++;
                    try { Console.SetCursorPosition(promptOffset + cursorPos, Console.CursorTop); } catch { }
                }
                continue;
            }

            if (key.Key == ConsoleKey.Tab && autoComplete is not null)
            {
                var currentInput = new string(buffer.ToArray());
                var completions = autoComplete.GetCompletions(currentInput);

                if (completions.Count == 1)
                {
                    // Single completion - auto-fill
                    var completion = completions[0];
                    var parts = currentInput.TrimEnd().Split(' ');

                    if (parts.Length > 0 && !currentInput.EndsWith(' '))
                    {
                        // Replace last word
                        var lastWord = parts[^1];
                        var prefix = currentInput.Substring(0, currentInput.Length - lastWord.Length);
                        var newInput = prefix + completion;

                        buffer.Clear();
                        buffer.AddRange(newInput);
                        cursorPos = buffer.Count;
                    }
                    else
                    {
                        // Append completion
                        buffer.AddRange(completion);
                        cursorPos = buffer.Count;
                    }

                    // Add space after completion
                    buffer.Add(' ');
                    cursorPos++;

                    RedrawLine(buffer, cursorPos, promptOffset);
                }
                else if (completions.Count > 1)
                {
                    // Multiple completions - show suggestions
                    try
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"  Suggestions: {string.Join(", ", completions)}");
                        Console.ResetColor();
                        // Rewrite the prompt
                        Console.Write("> ");
                        Console.Write(new string(buffer.ToArray()));
                        Console.SetCursorPosition(promptOffset + cursorPos, Console.CursorTop);
                    }
                    catch
                    {
                        // Fallback: just show suggestions without color
                        Console.WriteLine();
                        Console.WriteLine($"  Suggestions: {string.Join(", ", completions)}");
                        Console.Write("> ");
                        Console.Write(new string(buffer.ToArray()));
                    }
                }

                continue;
            }

            if (key.Key == ConsoleKey.Escape)
            {
                // Clear line
                buffer.Clear();
                cursorPos = 0;
                RedrawLine(buffer, cursorPos, promptOffset);
                continue;
            }

            // Regular character input
            if (!char.IsControl(key.KeyChar))
            {
                buffer.Insert(cursorPos, key.KeyChar);
                cursorPos++;
                RedrawLine(buffer, cursorPos, promptOffset);
            }
        }
    }

    private static void RedrawLine(List<char> buffer, int cursorPos, int promptOffset)
    {
        try
        {
            // Move cursor to the start of the input area (after the prompt)
            Console.SetCursorPosition(promptOffset, Console.CursorTop);
            var width = Console.WindowWidth;
            // Clear from prompt offset to end of line
            var clearLength = Math.Max(1, width - promptOffset - 1);
            Console.Write(new string(' ', clearLength));
            // Move back to prompt offset and write buffer
            Console.SetCursorPosition(promptOffset, Console.CursorTop);
            Console.Write(new string(buffer.ToArray()));
            // Set cursor to the correct position within the input
            Console.SetCursorPosition(promptOffset + cursorPos, Console.CursorTop);
        }
        catch
        {
            // If console operations fail, fall back to simple rewrite
            // This won't preserve the prompt, but it's better than crashing
            Console.Write("\r> " + new string(buffer.ToArray()));
        }
    }

    private static bool IsConsoleAvailable()
    {
        try
        {
            // Test if we can access console properties
            var _ = Console.CursorLeft;
            var __ = Console.WindowWidth;
            return true;
        }
        catch
        {
            return false;
        }
    }
}