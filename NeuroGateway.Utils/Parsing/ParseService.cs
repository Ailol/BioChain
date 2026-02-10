using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using NeuroGateway.Models;

namespace NeuroGateway.Utils;

/// <summary>
/// Stateless service for parsing conversation files (WhatsApp, Discord, CSV, SMS, PlainText)
/// and extracting structured data from LLM JSON responses.
/// </summary>
public static class ParseService
{
    private static readonly JsonSerializerOptions CaseInsensitiveJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Extract and deserialize a JSON array from LLM response text.
    /// Finds the first '[' and last ']' and deserializes the content between them.
    /// </summary>
    public static T? ParseJsonArray<T>(string responseText) where T : class
    {
        try
        {
            var s = responseText.IndexOf('[');
            var e = responseText.LastIndexOf(']') + 1;
            if (s < 0 || e <= s) return null;
            var json = responseText[s..e];
            return JsonSerializer.Deserialize<T>(json, CaseInsensitiveJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ParseJsonArray] Deserialization failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse LLM response JSON into ExtractedTrait objects (topic + explanation pairs).
    /// Expects JSON array: [{"topic":"...","explanation":"..."}]
    /// </summary>
    public static List<ExtractedTrait> ParseExtractedTraits(string json)
    {
        var raw = ParseJsonArray<List<RawTrait>>(json);
        if (raw == null) return [];
        return raw
            .Where(r => !string.IsNullOrWhiteSpace(r.Topic))
            .Select(r => new ExtractedTrait(r.Topic ?? "", r.Explanation ?? "", r.Speaker ?? ""))
            .ToList();
    }

    public static ConversationFormat DetectConversationFormat(string content)
    {
        // WhatsApp: [DD/MM/YYYY, HH:MM:SS] or [M/D/YY, H:MM PM]
        if (Regex.IsMatch(content, @"^\[\d{1,2}/\d{1,2}/\d{2,4},?\s+\d{1,2}:\d{2}", RegexOptions.Multiline))
            return ConversationFormat.WhatsApp;

        // SMS Export: "Received from Name on DATE" or "Sent to Name on DATE"
        if (Regex.IsMatch(content, @"(Received from|Sent to)\s+.+\s+on\s+\d{1,2}/\d{1,2}/\d{4}", RegexOptions.Multiline))
            return ConversationFormat.SMSExport;

        // Discord: [YYYY-MM-DD HH:MM] or username#1234
        if (Regex.IsMatch(content, @"\[\d{4}-\d{2}-\d{2}") ||
            (content.Contains('#') && Regex.IsMatch(content, @"#\d{4}")))
            return ConversationFormat.Discord;

        // CSV: header with sender/message columns
        if (Regex.IsMatch(content, @"^(sender|name|from),", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            return ConversationFormat.CSV;

        return ConversationFormat.PlainText;
    }

    public static List<ConversationMessage> ParseConversation(
        string content,
        ConversationFormat format,
        string targetName,
        string userName)
    {
        return format switch
        {
            ConversationFormat.WhatsApp => ParseWhatsApp(content, targetName, userName),
            ConversationFormat.Discord => ParseDiscord(content, targetName, userName),
            ConversationFormat.CSV => ParseCsv(content, targetName, userName),
            ConversationFormat.SMSExport => ParseSmsExport(content, targetName, userName),
            _ => ParsePlainText(content, targetName, userName)
        };
    }

    public static List<ConversationMessage> ParseWhatsApp(string content, string targetName, string userName)
    {
        var messages = new List<ConversationMessage>();
        var pattern = @"\[(\d{1,2}/\d{1,2}/\d{2,4}),?\s+(\d{1,2}:\d{2}(?::\d{2})?(?:\s*[AP]M)?)\]\s+([^:]+):\s*(.+?)(?=\[\d{1,2}/|\z)";

        foreach (Match match in Regex.Matches(content, pattern, RegexOptions.Singleline))
        {
            var speaker = match.Groups[3].Value.Trim();
            var message = match.Groups[4].Value.Trim();
            var isTarget = IsSpeakerMatch(speaker, targetName);
            var timestamp = ParseWhatsAppDateTime(match.Groups[1].Value, match.Groups[2].Value);

            messages.Add(new ConversationMessage(speaker, message, timestamp, isTarget));
        }
        return messages;
    }

    public static DateTime? ParseWhatsAppDateTime(string date, string time)
    {
        try
        {
            var dateTime = $"{date} {time}";
            string[] formats = [
                "M/d/yy h:mm tt", "M/d/yy H:mm", "M/d/yy H:mm:ss",
                "d/M/yy h:mm tt", "d/M/yy H:mm", "d/M/yy H:mm:ss",
                "MM/dd/yyyy h:mm tt", "MM/dd/yyyy H:mm", "MM/dd/yyyy H:mm:ss",
                "dd/MM/yyyy h:mm tt", "dd/MM/yyyy H:mm", "dd/MM/yyyy H:mm:ss",
                "M/d/yyyy h:mm tt", "M/d/yyyy H:mm", "d/M/yyyy H:mm"
            ];
            return DateTime.TryParseExact(dateTime.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var result) ? result : null;
        }
        catch { return null; }
    }

    public static List<ConversationMessage> ParseDiscord(string content, string targetName, string userName)
    {
        var messages = new List<ConversationMessage>();

        var timestampPattern = @"\[(\d{4}-\d{2}-\d{2}\s+\d{2}:\d{2})\]\s+([^:]+):\s*(.+?)(?=\[\d{4}-|\z)";
        var simplePattern = @"^([^#:\n]+(?:#\d{4})?)\s*:\s*(.+)$";

        var timestampMatches = Regex.Matches(content, timestampPattern, RegexOptions.Singleline);
        if (timestampMatches.Count > 0)
        {
            foreach (Match match in timestampMatches)
            {
                var speaker = match.Groups[2].Value.Trim().Split('#')[0];
                var message = match.Groups[3].Value.Trim();
                var isTarget = IsSpeakerMatch(speaker, targetName);
                DateTime.TryParse(match.Groups[1].Value, out var timestamp);
                messages.Add(new ConversationMessage(speaker, message, timestamp, isTarget));
            }
        }
        else
        {
            foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var match = Regex.Match(line.Trim(), simplePattern);
                if (match.Success)
                {
                    var speaker = match.Groups[1].Value.Trim().Split('#')[0];
                    var message = match.Groups[2].Value.Trim();
                    var isTarget = IsSpeakerMatch(speaker, targetName);
                    messages.Add(new ConversationMessage(speaker, message, null, isTarget));
                }
            }
        }
        return messages;
    }

    public static List<ConversationMessage> ParseCsv(string content, string targetName, string userName)
    {
        var messages = new List<ConversationMessage>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0) return messages;

        var header = lines[0].ToLower();
        var columns = ParseCsvLine(header);

        var senderCol = columns.FindIndex(c =>
            c.Contains("sender") || c.Contains("name") || c.Contains("from") || c.Contains("author"));
        var messageCol = columns.FindIndex(c =>
            c.Contains("message") || c.Contains("content") || c.Contains("text") || c.Contains("body"));
        var timestampCol = columns.FindIndex(c =>
            c.Contains("time") || c.Contains("date") || c.Contains("timestamp"));

        if (senderCol < 0) senderCol = 0;
        if (messageCol < 0) messageCol = Math.Min(1, columns.Count - 1);

        foreach (var line in lines.Skip(1).Where(l => !string.IsNullOrWhiteSpace(l)))
        {
            var parts = ParseCsvLine(line);
            if (parts.Count > Math.Max(senderCol, messageCol))
            {
                var speaker = parts[senderCol].Trim('"', ' ');
                var message = parts[messageCol].Trim('"', ' ');
                var isTarget = IsSpeakerMatch(speaker, targetName);

                DateTime? timestamp = null;
                if (timestampCol >= 0 && timestampCol < parts.Count &&
                    DateTime.TryParse(parts[timestampCol].Trim('"', ' '), out var ts))
                    timestamp = ts;

                messages.Add(new ConversationMessage(speaker, message, timestamp, isTarget));
            }
        }
        return messages;
    }

    public static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var inQuotes = false;
        var current = new StringBuilder();

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        result.Add(current.ToString());
        return result;
    }

    public static List<ConversationMessage> ParseSmsExport(string content, string targetName, string userName)
    {
        var messages = new List<ConversationMessage>();

        var blocks = Regex.Split(content, @"-{5,}");
        var headerPattern = @"(Received from|Sent to)\s+(.+?)\s+on\s+(\d{1,2}/\d{1,2}/\d{4},?\s+\d{1,2}:\d{2}:\d{2}\s*[AP]M)";

        foreach (var block in blocks)
        {
            var trimmedBlock = block.Trim();
            if (string.IsNullOrEmpty(trimmedBlock)) continue;

            var headerMatch = Regex.Match(trimmedBlock, headerPattern, RegexOptions.IgnoreCase);
            if (headerMatch.Success)
            {
                var direction = headerMatch.Groups[1].Value.Trim();
                var nameWithPhone = headerMatch.Groups[2].Value.Trim();
                var dateStr = headerMatch.Groups[3].Value.Trim();

                var name = Regex.Replace(nameWithPhone, @"\s*\([^)]+\)\s*$", "").Trim();

                var isReceived = direction.Equals("Received from", StringComparison.OrdinalIgnoreCase);
                var speaker = isReceived ? name : userName;

                var messageContent = trimmedBlock[(headerMatch.Index + headerMatch.Length)..].Trim();
                if (string.IsNullOrWhiteSpace(messageContent)) continue;

                DateTime? timestamp = null;
                if (DateTime.TryParse(dateStr, out var ts))
                    timestamp = ts;

                var isTarget = IsSpeakerMatch(speaker, targetName);
                messages.Add(new ConversationMessage(speaker, messageContent, timestamp, isTarget));
            }
        }

        return messages.OrderBy(m => m.Timestamp ?? DateTime.MaxValue).ToList();
    }

    public static List<ConversationMessage> ParsePlainText(string content, string targetName, string userName)
    {
        var messages = new List<ConversationMessage>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var colonPattern = @"^([^:]+):\s*(.+)$";
        var hasNamePrefix = lines.Any(l => Regex.IsMatch(l.Trim(), colonPattern));

        if (hasNamePrefix)
        {
            foreach (var line in lines)
            {
                var match = Regex.Match(line.Trim(), colonPattern);
                if (match.Success)
                {
                    var speaker = match.Groups[1].Value.Trim();
                    var message = match.Groups[2].Value.Trim();
                    var isTarget = IsSpeakerMatch(speaker, targetName);
                    messages.Add(new ConversationMessage(speaker, message, null, isTarget));
                }
            }
        }
        else
        {
            var currentSpeaker = targetName;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    var isTarget = currentSpeaker == targetName;
                    messages.Add(new ConversationMessage(currentSpeaker, trimmed, null, isTarget));
                    currentSpeaker = currentSpeaker == targetName ? userName : targetName;
                }
            }
        }
        return messages;
    }

    public static bool IsSpeakerMatch(string speaker, string targetName)
    {
        var s = speaker.ToLower().Trim();
        var t = targetName.ToLower().Trim();

        if (s == t) return true;
        if (s.Contains(t) || t.Contains(s)) return true;

        var sFirst = s.Split(' ').FirstOrDefault() ?? "";
        var tFirst = t.Split(' ').FirstOrDefault() ?? "";
        if (!string.IsNullOrEmpty(sFirst) && sFirst == tFirst) return true;

        return false;
    }

    public static List<ImportantConversation> ParseImportantConversations(string json, List<ConversationMessage> allMessages)
    {
        var parsed = ParseJsonArray<List<RawImportantConversation>>(json);
        if (parsed == null || parsed.Count == 0) return [];

        return parsed.Select(p =>
        {
            var start = Math.Max(0, p.StartIndex);
            var end = Math.Min(allMessages.Count - 1, p.EndIndex);
            var msgs = allMessages.Skip(start).Take(end - start + 1).ToList();
            var traits = p.Traits?.Select(t =>
                new ExtractedTrait(t.Topic ?? "", t.Explanation ?? "", t.Speaker ?? "")).ToList() ?? [];
            return new ImportantConversation(start, end, msgs, p.Reason ?? "", traits);
        }).ToList();
    }

    // ===== Document Extraction Methods =====

    public static string ExtractTextFromDocx(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var paragraph in body.Elements<Paragraph>())
        {
            foreach (var run in paragraph.Elements<Run>())
            {
                foreach (var text in run.Elements<Text>())
                    sb.Append(text.Text);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public static string ExtractTextFromPdf(byte[] data)
    {
        using var doc = PdfDocument.Open(data);
        var sb = new StringBuilder();
        foreach (var page in doc.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            sb.AppendLine(text);
        }
        return sb.ToString();
    }

    private record RawImportantConversation(
        [property: JsonPropertyName("startIndex")] int StartIndex,
        [property: JsonPropertyName("endIndex")] int EndIndex,
        [property: JsonPropertyName("reason")] string? Reason,
        [property: JsonPropertyName("traits")] List<RawTrait>? Traits
    );

    private record RawTrait(
        [property: JsonPropertyName("topic")] string? Topic,
        [property: JsonPropertyName("explanation")] string? Explanation,
        [property: JsonPropertyName("speaker")] string? Speaker
    );

    public static ResponderGroup ParseResponderGroup(string? relationship)
        => !string.IsNullOrWhiteSpace(relationship) && Enum.TryParse<ResponderGroup>(relationship, true, out var g) ? g : ResponderGroup.Dating;

    /// <summary>
    /// Build the LLM prompt for extracting significant conversations from parsed messages.
    /// </summary>
    public static string BuildConversationAnalysisPrompt(
        List<ConversationMessage> messages, string targetName, string userName, ConversationAnalysisConfig config)
    {
        var conversationText = string.Join("\n", messages.Select((m, i) =>
            $"[{i}] {(m.IsTargetPersonality ? targetName : userName)}: {m.Content}"));

        var jsonExample = JsonSerializer.Serialize(config.JsonExample,
            new JsonSerializerOptions { WriteIndented = true })
            .Replace("{targetName}", targetName);

        return config.PromptTemplate
            .Replace("{targetName}", targetName)
            .Replace("{userName}", userName)
            .Replace("{conversationText}", conversationText)
            .Replace("{jsonExample}", jsonExample);
    }
}
