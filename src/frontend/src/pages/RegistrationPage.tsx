import { 
    useState,
    type ChangeEvent,
    type FormEvent
} from "react";
import { Link } from "react-router";
type RegistrationPageProps = {
    setAccessToken: (token: string) => void;
};
function RegistrationPage({ setAccessToken }: RegistrationPageProps){
    const [registerNickname, setRegisterNickName] = useState("");
    const [registerPassword, setRegisterPassword] = useState("");
    const [registerEmail, setRegisterEmail] = useState("");

    async function register(event: FormEvent<HTMLFormElement>) {
        event.preventDefault();

        const response = await fetch("http://localhost:5000/api/Auth/register", {
            method: "POST",

            headers: {
                "Content-Type": "application/json"
            },

            credentials: "include",
            body: JSON.stringify({
                email: registerEmail,
                nickName: registerNickname,
                password: registerPassword
            })
        });
        const data = await response.json();
        console.log(data);
        console.log(response);
        if (response.ok) {
            setAccessToken(data.accessToken);
            alert("Регистрация успешна.")
        } else {
            alert("Ошибка регистрации - " + data.detail)
        }
    }
    function handleRegistrationEmailChange(event: ChangeEvent<HTMLInputElement>){
        setRegisterEmail(event.target.value);
    }
    function handleRegisterNicknameChange(event: ChangeEvent<HTMLInputElement>){
        setRegisterNickName(event.target.value);
    }
    function handleRegisterPasswordChange(event: ChangeEvent<HTMLInputElement>){
        setRegisterPassword(event.target.value);
    }
    return(
        <main>
            <h1>Зарегистрируйтесь в QuizArena</h1>
            <form onSubmit={register}>
                <label htmlFor="registerEmail">Введите Email:</label><br />
                <input type="email" id="registerEmail" value={registerEmail} onChange={handleRegistrationEmailChange} /><br />

                <label htmlFor="registerNickname">Придумайте никнейм:</label><br />
                <input type="text" id="registerNickname" value={registerNickname} onChange={handleRegisterNicknameChange} /><br />

                <label htmlFor="registerPassword">Введите пароль:</label><br />
                <input type="password" id="registerPassword" value={registerPassword} onChange={handleRegisterPasswordChange}/><br />
                <button type="submit">Зарегистрироваться</button><br />
                <br /><span>Есть аккаунт? - </span>
                <Link to="/login">Войти в аккаунт</Link>
            </form>
        </main>
    )
}

export default RegistrationPage