using Choam.Application.Dtos;

namespace Choam.Application.Interfaces;

public interface IOllamaClient
{
    IAsyncEnumerable<string> StreamChatAsync(
        List<ChatMessageDto> messages,
        CancellationToken ct);

    Task<bool> IsAvailableAsync(CancellationToken ct);
}
