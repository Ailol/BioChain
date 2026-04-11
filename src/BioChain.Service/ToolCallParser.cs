using System.Text.Json;
using System.Text.RegularExpressions;

namespace BioChain.Service;

/// <summary>
/// Parses Qwen3.5 tool call format from text content.
/// Format: &lt;tool_call&gt;&lt;function=NAME&gt;&lt;parameter=KEY&gt;VALUE&lt;/parameter&gt;&lt;/function&gt;&lt;/tool_call&gt;
/// </summary>
internal static class ToolCallParser
{
    public static List<ToolCall> Parse(string content)
    {
        var calls = new List<ToolCall>();
        var pos = 0;
        var callId = 0;

        while (pos < content.Length)
        {
            var tcStart = content.IndexOf("<tool_call>", pos, StringComparison.OrdinalIgnoreCase);
            if (tcStart < 0) break;

            var tcEnd = content.IndexOf("</tool_call>", tcStart, StringComparison.OrdinalIgnoreCase);
            if (tcEnd < 0) break;

            var block = content[(tcStart + "<tool_call>".Length)..tcEnd];

            var fnMatch = Regex.Match(block, @"<function=(\w+)>");
            if (!fnMatch.Success)
            {
                pos = tcEnd + "</tool_call>".Length;
                continue;
            }

            var funcName = fnMatch.Groups[1].Value;

            var paramDict = new Dictionary<string, object>();
            var paramMatches = Regex.Matches(block, @"<parameter=(\w+)>\s*([\s\S]*?)\s*</parameter>");
            foreach (Match pm in paramMatches)
            {
                var key = pm.Groups[1].Value;
                var value = pm.Groups[2].Value.Trim();

                if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var fval))
                    paramDict[key] = fval;
                else if (value.StartsWith('['))
                {
                    try { paramDict[key] = JsonSerializer.Deserialize<JsonElement>(value); }
                    catch { paramDict[key] = value; }
                }
                else
                    paramDict[key] = value;
            }

            calls.Add(new ToolCall
            {
                Id = $"call_{callId++}",
                Name = funcName,
                Arguments = JsonSerializer.Serialize(paramDict)
            });

            pos = tcEnd + "</tool_call>".Length;
        }

        return calls;
    }

    public static string StripToolCallBlocks(string text)
    {
        while (true)
        {
            var start = text.IndexOf("<tool_call>", StringComparison.OrdinalIgnoreCase);
            if (start < 0) break;
            var end = text.IndexOf("</tool_call>", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0) { text = text[..start]; break; }
            text = text[..start] + text[(end + "</tool_call>".Length)..];
        }
        return text;
    }
}

internal sealed class ToolCall
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Arguments { get; set; } = "{}";
}
