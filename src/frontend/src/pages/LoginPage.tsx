import { useState } from "react";
function LoginPage() {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [accessToken, setAccessToken] = useState("");
    async function handleSubmit(event){
        event.preventDefault();

        const response = await fetch("http://localhost:5000/api/Auth/login", {
            method: "POST",
            
            headers: {
                "Content-Type": "application/json"
            },

            credentials: "include",

            body: JSON.stringify({
                email: email,
                password: password
            })
        });
        const data = await response.json();
        console.log(data);
        console.log(response);
        if (response.ok) {
            setAccessToken(data.accessToken);
            alert("Вход успешный.")
        } else {
            alert("Ошибка входа - " + data.detail)
        }
    }
    async function loadMyQuizSets(){
        const response = await fetch("http://localhost:5000/api/QuizSets/my", {
            method: "GET",

            headers:{
                Authorization: "Bearer " + accessToken
            }
        });
        if (!response.ok) {
            console.log("Не удалось получить квизы:", response.status);
            return;
        }
        const data = await response.json();

        console.log(response);
        console.log(data);
    }
    function handleEmailChange(event){
        setEmail(event.target.value);
    }
    function handlePasswordChange(event){
        setPassword(event.target.value);
    }
    return (
        <main>
            <h1>Вход в QuizArena</h1>
            <p>Войдите, чтобы создавать квизы и участвовать в играх.</p>
            <form onSubmit={handleSubmit}>
                <label htmlFor="email">Введите Email:</label><br />
                <input type="email" id="email" value={email} onChange={handleEmailChange} /><br />

                <label htmlFor="password">Введите пароль:</label><br />
                <input type="password" id="password" value={password} onChange={handlePasswordChange}/><br />

                <button type="submit">Войти</button>
                <button type="button" onClick={loadMyQuizSets}>Мои квизы</button>
            </form>
        </main>
    )
}

export default LoginPage