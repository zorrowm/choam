using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Choam.Application.Dtos;
using Choam.Application.Interfaces;
using Choam.Domain.Entities;

namespace Choam.Application.Services;

public sealed partial class ChatService(
    IOllamaClient ollamaClient,
    ICategoryService categoryService,
    ICurrentUserService currentUser) : IChatService
{
    public async IAsyncEnumerable<string> StreamResponseAsync(
        ChatRequestDto request,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var categories = await categoryService.GetAllAsync(ct);
        var systemPrompt = BuildSystemPrompt(categories);

        var messages = new List<ChatMessageDto> { new("system", systemPrompt) };

        if (request.History is { Count: > 0 })
            messages.AddRange(request.History);

        messages.Add(new("user", request.Message));

        await foreach (var token in ollamaClient.StreamChatAsync(messages, ct))
        {
            yield return token;
        }
    }

    public async Task<TransactionCreateDto?> TryParseProposalAsync(
        string fullResponse,
        CancellationToken ct)
    {
        var json = ExtractJson(fullResponse);
        if (json is null)
            return null;

        try
        {
            var raw = JsonSerializer.Deserialize<RawProposal>(json, JsonOptions);
            if (raw?.Title is null || raw.Amount is null || raw.Type is null)
                return null;

            if (!Enum.TryParse<TransactionType>(raw.Type, ignoreCase: true, out var type))
                return null;

            var date = ParseDate(raw.Date);
            var categoryId = await ResolveCategoryIdAsync(raw.Category, ct);

            return new TransactionCreateDto(
                Title: raw.Title,
                Amount: Math.Abs(raw.Amount.Value),
                Description: null,
                Date: date,
                Type: type,
                CategoryId: categoryId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task<bool> IsAvailableAsync(CancellationToken ct)
        => ollamaClient.IsAvailableAsync(ct);

    private string BuildSystemPrompt(List<CategoryReadDto> categories)
    {
        var categoryList = string.Join(", ", categories.Select(c => $"{c.Name} (id:{c.Id})"));
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

        return $$"""
            You are the CHOAM Finance Assistant for user {{currentUser.UserName}}.
            Today's date is {{today}}.

            Available categories: {{categoryList}}

            When a user describes a financial transaction, respond ONLY with a JSON object:
            {"title": "...", "amount": 0.00, "type": "Income|Expense|Investment", "date": "YYYY-MM-DD", "category": "exact category name from the list above"}

            Rules:
            - amount is always positive (type determines income vs expense)
            - If no date is mentioned, use today's date
            - If the user says "yesterday", "last Monday", etc., calculate the actual date
            - Pick the closest matching category from the list. If none fits, use "Uncategorized"
            - No markdown fences, no extra text — ONLY the raw JSON object
            - If the message is NOT about a transaction, respond normally as a helpful finance assistant
            """;
    }

    private async Task<int> ResolveCategoryIdAsync(string? categoryName, CancellationToken ct)
    {
        var categories = await categoryService.GetAllAsync(ct);

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var match = categories.FirstOrDefault(c =>
                c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Id;
        }

        return categories.First(c =>
            c.Name.Equals("Uncategorized", StringComparison.OrdinalIgnoreCase)).Id;
    }

    private static string? ExtractJson(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var fenceMatch = JsonFenceRegex().Match(trimmed);
        if (fenceMatch.Success)
            return fenceMatch.Groups[1].Value.Trim();

        var braceMatch = JsonBraceRegex().Match(trimmed);
        if (braceMatch.Success)
            return braceMatch.Value;

        return null;
    }

    private static DateTime ParseDate(string? dateStr)
    {
        if (dateStr is not null && DateTime.TryParse(dateStr, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);

        return DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
    }

    [GeneratedRegex(@"```(?:json)?\s*\n?(.*?)\n?```", RegexOptions.Singleline)]
    private static partial Regex JsonFenceRegex();

    [GeneratedRegex(@"\{[^{}]*\}", RegexOptions.Singleline)]
    private static partial Regex JsonBraceRegex();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class RawProposal
    {
        public string? Title { get; set; }
        public decimal? Amount { get; set; }
        public string? Type { get; set; }
        public string? Date { get; set; }
        public string? Category { get; set; }
    }
}
