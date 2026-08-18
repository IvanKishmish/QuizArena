import { useEffect, useState } from "react";
import { Link } from "react-router";
import { apiRequest } from "../api/client";

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

    useEffect(() => {
        if (!accessToken) {
            return;
        }

        async function loadMyQuizSets(){
            const response = await apiRequest("/api/QuizSets/my", {
                method: "GET",
                headers: {
                    Authorization: "Bearer " + accessToken
                }
            });
            if (!response.ok) {
                console.log("Не удалось получить квизы:", response.status);
                return;
            }

            const data = await response.json();
            setQuizSets(data);
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