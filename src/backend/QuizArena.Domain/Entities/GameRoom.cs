using System.Text.Json.Serialization;
using QuizArena.Domain.Common;
using QuizArena.Domain.Entities.Models;
using QuizArena.Domain.Enums;
using ErrorOr;

namespace QuizArena.Domain.Entities;

public sealed class GameRoom : TransientEntity
{
    public string RoomCode { get; private init; } = string.Empty;
    
    public Guid QuizSetId { get; private init; }
    
    public Guid HostId { get; private init; }

    public GameRoomStatus Status { get; private set; } = GameRoomStatus.Waiting;
    
    public int CurrentQuestionIndex { get; private set; }
    
    public DateTimeOffset? CurrentQuestionStartedAt { get; private set; }
    
    private readonly List<Participant> _participants = [];
    public IReadOnlyList<Participant> Participants => _participants.AsReadOnly();
    
    public DateTimeOffset? StartedAt { get; private set; }
    
    public DateTimeOffset? FinishedAt { get; private set; }

    private GameRoom()
    { } // ef

    private GameRoom(Guid id, GameRoomCreationParams args) : base(id)
    {
        RoomCode = args.RoomCode;
        QuizSetId = args.QuizSetId;
        HostId = args.HostId;
    }
    
    internal GameRoom(
        Guid id,
        string roomCode,
        Guid quizSetId,
        Guid hostId,
        GameRoomStatus status,
        int currentQuestionIndex,
        DateTimeOffset? currentQuestionStartedAt,
        List<Participant> participants,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt) : base(id)
    {
        RoomCode = roomCode;
        QuizSetId = quizSetId;
        HostId = hostId;
        Status = status;
        CurrentQuestionIndex = currentQuestionIndex;
        CurrentQuestionStartedAt = currentQuestionStartedAt;
        _participants = participants;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
    }

    public static ErrorOr<GameRoom> Create(GameRoomCreationParams args)
    {
        var validationResult = ValidateInvariants(args);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new GameRoom(Guid.CreateVersion7(), args);
    }

    public ErrorOr<Updated> AddParticipant(Guid? userId, Guid? guestId, string displayName)
    {
        if (Status != GameRoomStatus.Waiting)
            return Error.Validation("GameRoom.NotAcceptingParticipants", "Cannot join a room that has already started or finished.");

        var alreadyJoined = _participants.Any(p =>
            (userId is not null && p.UserId == userId) ||
            (guestId is not null && p.GuestId == guestId));
        
        if (alreadyJoined)
            return Error.Validation("GameRoom.AlreadyJoined", "This participant has already joined the room.");

        var participantResult = Participant.Create(userId, guestId, displayName);
        
        if (participantResult.IsError)
            return participantResult.Errors;
        
        _participants.Add(participantResult.Value);

        return Result.Updated;
    }

    public ErrorOr<Updated> Start()
    {
        if (Status != GameRoomStatus.Waiting)
            return Error.Validation("GameRoom.InvalidStateForStart", "Room can only be started from the Waiting state.");

        if (_participants.Count == 0)
            return Error.Validation("GameRoom.NotEnoughParticipants", "At least one participant is required to start the game.");

        Status = GameRoomStatus.InProgress;
        StartedAt = DateTimeOffset.UtcNow;
        CurrentQuestionIndex = 0;

        return Result.Updated;
    }

    public ErrorOr<Updated> NextQuestion()
    {
        if(Status != GameRoomStatus.InProgress)
            return Error.Validation("GameRoom.NotInProgress", "Cannot advance questions when the game is not in progress.");
        
        CurrentQuestionIndex++;
        CurrentQuestionStartedAt = DateTimeOffset.UtcNow;
        
        return Result.Updated;
    }
    
    public ErrorOr<Updated> Finish()
    {
        if (Status != GameRoomStatus.InProgress)
            return Error.Validation("GameRoom.NotInProgress", "Only an in-progress game can be finished.");

        Status = GameRoomStatus.Finished;
        FinishedAt = DateTimeOffset.UtcNow;

        return Result.Updated;
    }
    
    private static ErrorOr<Success> ValidateInvariants(GameRoomCreationParams args)
    {
        var errors = new List<Error>();

        if (string.IsNullOrWhiteSpace(args.RoomCode))
            errors.Add(Error.Validation("GameRoom.RoomCodeRequired", "Room code is required"));

        if (args.QuizSetId == Guid.Empty)
            errors.Add(Error.Validation("GameRoom.QuizSetIdRequired", "QuizSetId is required"));

        if (args.HostId == Guid.Empty)
            errors.Add(Error.Validation("GameRoom.HostIdRequired", "HostId is required"));

        return errors.Count > 0 ? errors : Result.Success;
    }
}