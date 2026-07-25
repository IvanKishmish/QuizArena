namespace TelegramBot.Application.Contracts.Api;

public sealed record CreateGameRoomRequest(Guid QuizSetId);

public sealed record CreateGameRoomResponse(string RoomCode);

public sealed record JoinGameRoomRequest(string DisplayName);

public sealed record JoinGameRoomResponse(Guid ParticipantId);
