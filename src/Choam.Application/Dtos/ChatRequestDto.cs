namespace Choam.Application.Dtos;

public record ChatMessageDto(string Role, string Content);

public record ChatRequestDto(string Message, List<ChatMessageDto>? History);
