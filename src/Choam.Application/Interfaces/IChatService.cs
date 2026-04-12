using Choam.Application.Dtos;

namespace Choam.Application.Interfaces;

public interface IChatService
{
    IAsyncEnumerable<string> StreamResponseAsync(
        ChatRequestDto request,
        CancellationToken ct);

    Task<TransactionCreateDto?> TryParseProposalAsync(
        string fullResponse,
        CancellationToken ct);

    Task<bool> IsAvailableAsync(CancellationToken ct);
}
