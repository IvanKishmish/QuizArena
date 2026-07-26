import { useState, useEffect } from "react";
import { Link } from "react-router";

function LoginPage({ accessToken, setAccessToken }) {
    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");


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
    async function refreshAccessToken() {
        const response = await fetch("http://localhost:5000/api/Auth/refresh", {
            method: "POST",
            credentials: "include"
        });

        if (!response.ok) {
            console.log("Не удалось обновить токен:", response.status);
            return;
        }

        const data = await response.json();

        setAccessToken(data.accessToken);

        console.log("Новый accessToken получен");
    }
    useEffect(() => {
        refreshAccessToken();
    }, []);





    async function logout(){
        const response = await fetch("http://localhost:5000/api/Auth/logout", {
            method: "POST",
            headers: {
                Authorization: "Bearer " + accessToken
            },
            credentials: "include"
        });
        if(response.ok){
            setAccessToken("");
            alert("Вы вышли с аккаунта");
        } else {
            alert("Ошибка")
        }
    }

    async function loadMyQuizSets(){
        const response = await fetch("http://localhost:5000/api/QuizSets/my", {
            method: "GET",

            headers:{
                Authorization: "Bearer " + accessToken
            },

            credentials: "include"
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
                <button type="button" onClick={logout}>Выйти</button><br />

                <br /><span>Нет аккаунта? - </span>
                <Link to="/register">Зарегистрироваться</Link>
            </form>
        </main>
    )
}

export default LoginPage