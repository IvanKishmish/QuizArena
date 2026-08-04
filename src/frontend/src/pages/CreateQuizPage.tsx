import { useState, type ChangeEvent, type FormEvent } from "react";
import { useNavigate } from "react-router";

type CreateQuizPageProps = {
    accessToken: string;
};



function CreateQuizPage({ accessToken }: CreateQuizPageProps) {
    const navigate = useNavigate();

    const [title, setTitle] = useState("");
    const [description, setDescription] = useState("");

    function handleTitleChange(event: ChangeEvent<HTMLInputElement>) {
        setTitle(event.target.value);
    };

    function handleDescriptionChange(event: ChangeEvent<HTMLTextAreaElement>) {
        setDescription(event.target.value);
    };

    async function handleSubmit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();
        const response = await fetch("http://localhost:5000/api/QuizSets", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                Authorization: "Bearer " + accessToken
            },
            credentials: "include",
            body: JSON.stringify({
                title,
                description
            })
        });
        if (!response.ok) {
            console.log("Не удалось создать квиз:", response.status);
            return;
        }

        const data = await response.json();

        console.log("Квиз создан:", data);

        navigate("/my-quizzes");
    }
    return (
        <main>
            <h1>Создать квиз</h1>

            <form onSubmit={handleSubmit}>
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
                    Создать квиз
                </button>
            </form>
        </main>
    );
}

export default CreateQuizPage;