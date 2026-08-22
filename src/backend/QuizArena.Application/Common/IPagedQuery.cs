namespace QuizArena.Application.Common;

public interface IPagedQuery
{
    int PageNumber { get; }
    int PageSize { get; }
}
