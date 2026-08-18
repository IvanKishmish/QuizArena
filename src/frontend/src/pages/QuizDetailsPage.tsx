import { useCallback, useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { type ChangeEvent, type FormEvent } from "react";
import type { Question } from "../types/question";
import QuestionCard from "../components/QuestionCard";

type QuizDetailsPageProps = {
    accessToken: string;
};

type QuizSet = {
    id: string;
    title: string;
    description: string;
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
    const [correctOptions, setCorrectOptions] = useState([
        false,
        false,
        false,
        false
    ]);
    const navigate = useNavigate();

    const [dataForm, setDataForm] = useState(false);

    const [showHint, setShowHint] = useState(false);
    const [questionType, setQuestionType] = useState(0);



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

        let options;
        if(questionType === 0){
            options = [
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
            console.log("questionType:", questionType);
            console.log("options:", options);
        } else if(questionType === 1){
            options = [
                {
                    text: option1,
                    isCorrect: correctOptions[0],
                    orderIndex: 0
                },
                {
                    text: option2,
                    isCorrect: correctOptions[1],
                    orderIndex: 1
                },
                {
                    text: option3,
                    isCorrect: correctOptions[2],
                    orderIndex: 2
                },
                {
                    text: option4,
                    isCorrect: correctOptions[3],
                    orderIndex: 3
                }
            ];
            console.log("questionType:", questionType);
            console.log("options:", options);
        } else if(questionType === 2){
            options = [
                {
                    text: "True",
                    isCorrect: correctOption === 0,
                    orderIndex: 0
                },
                {
                    text: "False",
                    isCorrect: correctOption === 1,
                    orderIndex: 1
                }
            ];
            console.log("questionType:", questionType);
            console.log("options:", options);
        } else if (questionType === 3) {
            options = [
                {
                    text: option1,
                    isCorrect: false,
                    orderIndex: 0
                },
                {
                    text: option2,
                    isCorrect: false,
                    orderIndex: 1
                },
                {
                    text: option3,
                    isCorrect: false,
                    orderIndex: 2
                },
                {
                    text: option4,
                    isCorrect: false,
                    orderIndex: 3
                }
            ];
            console.log("questionType:", questionType);
            console.log("options:", options);
        }

        
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
                    questionType,
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

        setCorrectOptions([
            false,
            false,
            false,
            false
        ]);
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

    const loadQuestions = useCallback(async () => {
        if (!id || !accessToken) {
            return;
        }

        const response = await fetch(
            `http://localhost:5000/api/quizsets/${id}/questions`,
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
    }, [id, accessToken]);

    function openQuestion(){

        if (dataForm === false) {
            setDataForm(true);
        } else {
            setDataForm(false);
        }
    }

    function showHintText(){
        if (showHint === false){
            setShowHint(true);
        } else {
            setShowHint(false);
        }
    }

    function toggleCorrectOption(index: number){
        const newCorrectOptions = [...correctOptions];
        newCorrectOptions[index] = !newCorrectOptions[index];
        setCorrectOptions(newCorrectOptions);
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
    }, [id, accessToken, loadQuestions]);

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
            <button onClick={openQuestion}>Добавить новый вопрос</button>
            <br />
            
            {dataForm && (
                    <form onSubmit={handleCreateQuestion}>
                    { showHint && (
                        <p>Подсказка: выбери один правильный вариант ответа</p>
                    )}
                    <button type="button" onClick={showHintText}>{ showHint ? "Закрыть подсказку" : "Открыть подсказку"}</button><br />
                    <br />
                    <select 
                        value={questionType}
                        onChange={(event) => {
                            setQuestionType(Number(event.target.value))
                    }}>
                        
                        <option value={0}>SingleChoice</option>
                        <option value={1}>MultipleChoice</option>
                        <option value={2}>TrueFalse</option>
                        <option value={3}>Ordering</option>
                    </select>
                    <br /><label htmlFor="questionText">Текст вопроса:</label><br />

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

                    {questionType === 0 && (
                        <div>
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
                        </div>
                    )}

                    {questionType === 1 && (
                        <div>
                            <input
                                type="text"
                                placeholder="Ответ 1"
                                value={option1}
                                onChange={(event) => setOption1(event.target.value)}
                            />

                            <input
                                type="checkbox"
                                name="correctOption"
                                checked={correctOptions[0]}
                                onChange={() => toggleCorrectOption(0)}
                            />

                            <br />

                            <input
                                type="text"
                                placeholder="Ответ 2"
                                value={option2}
                                onChange={(event) => setOption2(event.target.value)}
                            />

                            <input
                                type="checkbox"
                                name="correctOption"
                                checked={correctOptions[1]}
                                onChange={() => toggleCorrectOption(1)}
                            />

                            <br />

                            <input
                                type="text"
                                placeholder="Ответ 3"
                                value={option3}
                                onChange={(event) => setOption3(event.target.value)}
                            />

                            <input
                                type="checkbox"
                                name="correctOption"
                                checked={correctOptions[2]}
                                onChange={() => toggleCorrectOption(2)}
                            />

                            <br />

                            <input
                                type="text"
                                placeholder="Ответ 4"
                                value={option4}
                                onChange={(event) => setOption4(event.target.value)}
                            />

                            <input
                                type="checkbox"
                                name="correctOption"
                                checked={correctOptions[3]}
                                onChange={() => toggleCorrectOption(3)}
                            />
                            

                            <br /><br />
                        </div>
                    )}
                    { questionType === 2 && (
                       <div>
                            <input 
                                id="trueOption"
                                type="radio"
                                name="correctOption"
                                checked={correctOption === 0}
                                onChange={() => setCorrectOption(0)}
                            /> 
                            <label htmlFor="trueOption">Правда</label> <br />
                            <input 
                                id="falseOption"
                                type="radio"
                                name="correctOption"
                                checked={correctOption === 1}
                                onChange={() => setCorrectOption(1)}
                            /> 
                            <label htmlFor="falseOption">Ложь</label>
                       </div> 
                    )}

                    {questionType === 3 && (
                        <div>
                            <label htmlFor="orderOption1">1.</label>
                            <input
                                id="orderOption1"
                                type="text"
                                value={option1}
                                onChange={(event) => setOption1(event.target.value)}
                            />

                            <br />

                            <label htmlFor="orderOption2">2.</label>
                            <input
                                id="orderOption2"
                                type="text"
                                value={option2}
                                onChange={(event) => setOption2(event.target.value)}
                            />

                            <br />

                            <label htmlFor="orderOption3">3.</label>
                            <input
                                id="orderOption3"
                                type="text"
                                value={option3}
                                onChange={(event) => setOption3(event.target.value)}
                            />

                            <br />

                            <label htmlFor="orderOption4">4.</label>
                            <input
                                id="orderOption4"
                                type="text"
                                value={option4}
                                onChange={(event) => setOption4(event.target.value)}
                            />
                        </div>
                    )}


                    <button type="submit">
                        Добавить вопрос
                    </button>
                </form>
            )}


            {questions.length === 0 && (
                <p>Вопросов пока нет.</p>
            )}

            {questions.map((question) => (
                <QuestionCard
                    key={question.id}
                    question={question}
                    onDelete={handleDeleteQuestion}
                />
            ))}
        </main>
    );
}

export default QuizDetailsPage;