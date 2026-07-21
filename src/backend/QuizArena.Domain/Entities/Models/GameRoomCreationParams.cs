using QuizArena.Domain.Enums;

namespace QuizArena.Domain.Entities.Models;

public sealed record GameRoomCreationParams(
    string RoomCode,
    Guid QuizSetId,
    Guid HostId);