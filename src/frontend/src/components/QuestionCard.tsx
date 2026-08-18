import type { Question } from "../types/question";

type QuestionCardProps = {
    question: Question;
    onDelete: (questionId: string) => void;
};

function QuestionCard({ question, onDelete }: QuestionCardProps) {
    return (
        <>
            <h3>{question.text}</h3>
            <p>Баллы: {question.points}</p>
            <p>Время: {question.timeLimitSeconds} сек.</p>
            {question.options.map((option) => (
                <p key={option.orderIndex}>
                    {option.text}
                    {option.isCorrect && " — правильный"}
                </p>
            ))}
            <button
                type="button"
                onClick={() => onDelete(question.id)}
            >
                Удалить вопрос
            </button>
        </>
    )
}

export default QuestionCard;