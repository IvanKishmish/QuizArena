namespace QuizArena.Application.Common.Interfaces;

public interface IRoomCodeGenerator
{
    Task<string> GenerateUniqueCodeAsync(CancellationToken ct = default);
}