using QuizArena.Domain.Common;
using ErrorOr;

namespace QuizArena.Domain.Entities;

public sealed class Player : Entity
{
    public string NickName { get; private set; } = string.Empty;

    public int TotalGamesPlayed { get; private set; } = 0;

    public int TotalScore { get; private set; } = 0;

    private Player()
    { } // ef

    private Player(Guid id, string nickName) : base(id)
    {
        NickName = nickName;
    }

    public static ErrorOr<Player> Create(Guid id, string nickName)
    {
        var validationResult = ValidateInvariants(nickName);

        if (validationResult.IsError)
            return validationResult.Errors;

        return new Player(id, nickName);
    }

    public ErrorOr<Updated> UpdateNickName(string newNickName)
    {
        var validationResult = ValidateInvariants(newNickName);
        
        if (validationResult.IsError)
            return validationResult.Errors;
        
        NickName = newNickName;

        return Result.Updated;
    }

    public ErrorOr<Updated> RecordGameResult(int score)
    {
        if (score <= 0)
            return Error.Validation("Player.NegativeOrEqualToZeroScore", "Score cannot be either negative or 0.");

        TotalGamesPlayed++;
        TotalScore += score;
        
        return Result.Updated;
    }
    
    private static ErrorOr<Success> ValidateInvariants(string nickName)
    {
        if (string.IsNullOrWhiteSpace(nickName) || nickName.Length < 5)
            return Error.Validation("Player.InvalidNickName", "Nickname must be at least 5 characters long.");

        return Result.Success;
    }
}