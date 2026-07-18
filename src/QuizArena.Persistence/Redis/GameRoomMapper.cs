using QuizArena.Domain.Entities;
using QuizArena.Persistence.Redis.Documents;

namespace QuizArena.Persistence.Redis;

public static class GameRoomMapper
{
    public static GameRoomSnapshot ToSnapshot(this GameRoom gameRoom)
        => new GameRoomSnapshot
        {
            Id = gameRoom.Id,
            RoomCode = gameRoom.RoomCode,
            QuizSetId = gameRoom.QuizSetId,
            HostId = gameRoom.HostId,
            Status = gameRoom.Status,
            CurrentQuestionIndex = gameRoom.CurrentQuestionIndex,
            CurrentQuestionStartedAt = gameRoom.CurrentQuestionStartedAt,
            Participants = gameRoom.Participants.Select(p => p.ToSnapshot()).ToList(),
            StartedAt = gameRoom.StartedAt,
            FinishedAt = gameRoom.FinishedAt
        };
    
    private static ParticipantSnapshot ToSnapshot(this Participant participant)
        => new ParticipantSnapshot
        {
            Id = participant.Id,
            DisplayName = participant.DisplayName,
            UserId = participant.UserId,
            GuestId = participant.GuestId,
            Score = participant.Score
        };
    
    public static GameRoom ToDomain(this GameRoomSnapshot snapshot)
    {
        var participants = snapshot.Participants.Select(p => p.ToDomain()).ToList();

        return new GameRoom(
            snapshot.Id,
            snapshot.RoomCode,
            snapshot.QuizSetId,
            snapshot.HostId,
            snapshot.Status,
            snapshot.CurrentQuestionIndex,
            snapshot.CurrentQuestionStartedAt,
            participants,
            snapshot.StartedAt,
            snapshot.FinishedAt);
    }

    private static Participant ToDomain(this ParticipantSnapshot snapshot)
        => new Participant(
            snapshot.Id,
            snapshot.DisplayName,
            snapshot.UserId,
            snapshot.GuestId,
            snapshot.Score);
    
}