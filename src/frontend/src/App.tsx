import { Routes, Route } from 'react-router'
import { useEffect, useState } from "react";
import LoginPage from './pages/LoginPage'
import RegistrationPage from './pages/RegistrationPage'
import CreateQuizPage from './pages/CreateQuizPage';
import MyQuizSetsPage from './pages/MyQuizSetsPage';
import QuizDetailsPage from './pages/QuizDetailsPage';
import NotFoundPage from './pages/NotFoundPage';
import { refreshAccessToken } from "./api/auth";


function App() {
  const [accessToken, setAccessToken] = useState("");

  useEffect(() => {
    async function restoreSession() {
      const token = await refreshAccessToken();

      if (token) {
        setAccessToken(token);
      }
    }

    restoreSession();
  }, []);
  return(
    <Routes>
      <Route path="/login" element={<LoginPage accessToken={accessToken} setAccessToken={setAccessToken} />} />
      <Route path="/register" element={<RegistrationPage setAccessToken={setAccessToken} />} />
      <Route path="/create-quiz" element={<CreateQuizPage accessToken={accessToken} />} />
      <Route path="/my-quizzes" element={<MyQuizSetsPage accessToken={accessToken} />}/>
      <Route path="/quiz/:id" element={<QuizDetailsPage accessToken={accessToken} />}/>
      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )

}

export default App