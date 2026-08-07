import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { type ChangeEvent, type FormEvent } from "react";

type QuizDetailsPageProps = {
    accessToken: string;
};

type QuizSet = {
    id: string;
    title: string;
    description: string;
};

type AnswerOption = {
    text: string;
    isCorrect: boolean;
    orderIndex: number;
};

type Question = {
    id: string;
    text: string;
    questionType: number;
    timeLimitSeconds: number;
    points: number;
    options: AnswerOption[];
};

function QuizDetailsPage({ accessToken }: QuizDetailsPageProps) {
    const { id } = useParams();
    const [quiz, setQuiz] = useState<QuizSet | null>(null);
    const [title, setTitle] = useState("");
    const [description, setDescription] = useState("");

    const [questions, setQuestions] = useState<Question[]>([]);
    const [questionText, setQuestionText] = useState("");
    const [questionPoints, setQuestionPoints] = useState(100);
    const [questionTime, setQuestionTime] = useState(30);

    const [option1, setOption1] = useState("");
    const [option2, setOption2] = useState("");
    const [option3, setOption3] = useState("");
    const [option4, setOption4] = useState("");
    const [correctOption, setCorrectOption] = useState(0);
    const navigate = useNavigate();

    function handleTitleChange(event: ChangeEvent<HTMLInputElement>) {
        setTitle(event.target.value);
    }

    function handleDescriptionChange(event: ChangeEvent<HTMLTextAreaElement>) {
        setDescription(event.target.value);
    }

    function handleQuestionTextChange(event: ChangeEvent<HTMLInputElement>) {
        setQuestionText(event.target.value);
    }

    function handleQuestionPointsChange(event: ChangeEvent<HTMLInputElement>) {
        setQuestionPoints(Number(event.target.value));
    }

    function handleQuestionTimeChange(event: ChangeEvent<HTMLInputElement>) {
        setQuestionTime(Number(event.target.value));
    }

    async function handleCreateQuestion(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (!id) {
            return;
        }

        const options = [
            {
                text: option1,
                isCorrect: correctOption === 0,
                orderIndex: 0
            },
            {
                text: option2,
                isCorrect: correctOption === 1,
                orderIndex: 1
            },
            {
                text: option3,
                isCorrect: correctOption === 2,
                orderIndex: 2
            },
            {
                text: option4,
                isCorrect: correctOption === 3,
                orderIndex: 3
            }
        ];
        
        const response = await fetch(`http://localhost:5000/api/quizsets/${id}/questions`,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    Authorization: "Bearer " + accessToken
                },
                credentials: "include",
                body: JSON.stringify({
                    text: questionText,
                    questionType: 0,
                    timeLimitSeconds: questionTime,
                    points: questionPoints,
                    options
                })
            }
        );
        
        if (!response.ok) {
            console.log("Не удалось создать вопрос:", response.status);
            return;
        }

        console.log("Вопрос создан");
        
        await loadQuestions();

        setQuestionText("");
        setQuestionPoints(100);
        setQuestionTime(30);

        setOption1("");
        setOption2("");
        setOption3("");
        setOption4("");

        setCorrectOption(0);
    }

    async function handleUpdate(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        if (!id) {
            return;
        }

        const response = await fetch(
            `http://localhost:5000/api/QuizSets/${id}`,
            {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                    Authorization: "Bearer " + accessToken
                },
                credentials: "include",
                body: JSON.stringify({
                    title,
                    description
                })
            }
        );

        if (!response.ok) {
            console.log("Не удалось обновить квиз:", response.status);
            return;
        }

        console.log("Квиз обновлён");
    }

    async function handleDelete() {
        if (!id){
            return;
        }

        const response = await fetch(`http://localhost:5000/api/QuizSets/${id}`,
            {
                method: "DELETE",
                headers: {
                    Authorization: "Bearer " + accessToken
                },
                credentials: "include"
            }
        );
        if (!response.ok) {
            console.log("Не удалось удалить квиз: ", response.status);
            return;
        }

        navigate("/my-quizzes");
    }
    async function handleDeleteQuestion(questionId: string) {
        if (!id) {
            return;
        }

        const response = await fetch(
            `http://localhost:5000/api/quizsets/${id}/questions/${questionId}`,
            {
                method: "DELETE",
                headers: {
                    Authorization: "Bearer " + accessToken
                },
                credentials: "include"
            }
        );

        if (!response.ok) {
            console.log("Не удалось удалить вопрос:", response.status);
            return;
        }

        await loadQuestions();
    }

    async function loadQuestions(){
        if (!id || !accessToken) {
            return;
        }

        const response = await fetch(`http://localhost:5000/api/quizsets/${id}/questions`,
            {
                method: "GET",
                headers: {
                    Authorization: "Bearer " + accessToken
                },
                credentials: "include"
            }
        );

        if (!response.ok) {
            console.log("Не удалось получить вопросы:", response.status);
            return;
        }

        const data = await response.json();
        console.log(data);
        setQuestions(data);
    }

    useEffect(() => {
        async function loadQuiz() {
            if (!id || !accessToken) {
                return;
            }

            const response = await fetch(
                `http://localhost:5000/api/QuizSets/${id}`,
                {
                    method: "GET",
                    headers: {
                        Authorization: "Bearer " + accessToken
                    },
                    credentials: "include"
                }
            );

            if (!response.ok) {
                console.log("Не удалось получить квиз:", response.status);
                return;
            }

            const data = await response.json();
            setQuiz(data);
            setTitle(data.title);
            setDescription(data.description);
        }

        loadQuiz();
        loadQuestions();
    }, [id, accessToken]);

    return (
        <main>
            <h1>Квиз</h1>
            <p>ID квиза: {id}</p>
            {quiz && (
                <>
                    <h2>{quiz.title}</h2>
                    <p>{quiz.description}</p>
                </>
            )}
            <form onSubmit={handleUpdate}>
                <label htmlFor="title">Название:</label><br />

                <input
                    id="title"
                    type="text"
                    value={title}
                    onChange={handleTitleChange}
                />

                <br /><br />

                <label htmlFor="description">Описание:</label><br />

                <textarea
                    id="description"
                    value={description}
                    onChange={handleDescriptionChange}
                />

                <br /><br />

                <button type="submit">
                    Сохранить изменения
                </button>
            </form>
            <button type="button" onClick={handleDelete}>
                Удалить квиз
            </button>

            <h2>Вопросы</h2>
            <form onSubmit={handleCreateQuestion}>
                <label htmlFor="questionText">Текст вопроса:</label><br />

                <input
                    id="questionText"
                    type="text"
                    value={questionText}
                    onChange={handleQuestionTextChange}
                />

                <br /><br />

                <label htmlFor="questionPoints">Баллы:</label><br />

                <input
                    id="questionPoints"
                    type="number"
                    value={questionPoints}
                    onChange={handleQuestionPointsChange}
                />

                <br /><br />

                <label htmlFor="questionTime">Время:</label><br />

                <input
                    id="questionTime"
                    type="number"
                    value={questionTime}
                    onChange={handleQuestionTimeChange}
                />

                <br /><br />

                <h3>Варианты ответа</h3>

                <input
                    type="text"
                    placeholder="Ответ 1"
                    value={option1}
                    onChange={(event) => setOption1(event.target.value)}
                />

                <input
                    type="radio"
                    name="correctOption"
                    checked={correctOption === 0}
                    onChange={() => setCorrectOption(0)}
                />

                <br />

                <input
                    type="text"
                    placeholder="Ответ 2"
                    value={option2}
                    onChange={(event) => setOption2(event.target.value)}
                />

                <input
                    type="radio"
                    name="correctOption"
                    checked={correctOption === 1}
                    onChange={() => setCorrectOption(1)}
                />

                <br />

                <input
                    type="text"
                    placeholder="Ответ 3"
                    value={option3}
                    onChange={(event) => setOption3(event.target.value)}
                />

                <input
                    type="radio"
                    name="correctOption"
                    checked={correctOption === 2}
                    onChange={() => setCorrectOption(2)}
                />

                <br />

                <input
                    type="text"
                    placeholder="Ответ 4"
                    value={option4}
                    onChange={(event) => setOption4(event.target.value)}
                />

                <input
                    type="radio"
                    name="correctOption"
                    checked={correctOption === 3}
                    onChange={() => setCorrectOption(3)}
                />

                <br /><br />

                <button type="submit">
                    Добавить вопрос
                </button>
            </form>

            {questions.length === 0 && (
                <p>Вопросов пока нет.</p>
            )}

            {questions.map((question) => (
                <div key={question.id}>
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
                        onClick={() => handleDeleteQuestion(question.id)}
                    >
                        Удалить вопрос
                    </button>
                </div>
            ))}
        </main>
    );
}

export default QuizDetailsPage;