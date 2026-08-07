import { useState } from "react";
import { useEffect } from "react";
import { Link } from "react-router";

type QuizSet = {
    id: string;
    title: string;
    description: string;
}
type MyQuizSetsPageProps = {
    accessToken: string;
};
function MyQuizSetsPage({ accessToken }: MyQuizSetsPageProps) {
    const[quizSets, setQuizSets] = useState<QuizSet[]>([]);
    async function loadMyQuizSets(){
        const response = await fetch("http://localhost:5000/api/QuizSets/my", {
            method: "GET",
            headers: {
                Authorization: "Bearer " + accessToken
            },
            credentials: "include"
        });
        if (!response.ok) {
            console.log("Не удалось получить квизы:", response.status);
            return;
        }

        const data = await response.json();
        setQuizSets(data);
    }
    useEffect(() => {
        if (!accessToken) {
            return;
        }

        loadMyQuizSets();
    }, [accessToken]);

    return (
        <main>
            <h1>Мои квизы</h1>
            <p>Здесь будут отображаться созданные квизы.</p>
            
            {quizSets.length === 0 && (
                <p>У вас пока нет квизов.</p>
            )}
            {quizSets.map((quiz) => (
                <div key={quiz.id}>
                    <h2>
                        <Link to={`/quiz/${quiz.id}`}>
                            {quiz.title}
                        </Link>
                    </h2>

                    <p>{quiz.description}</p>
                </div>
            ))}
        </main>
    );
}

export default MyQuizSetsPage;