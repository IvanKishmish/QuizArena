import { Routes, Route } from 'react-router'
import { useEffect, useState } from "react";
import LoginPage from './pages/LoginPage'
import RegistrationPage from './pages/RegistrationPage'
import CreateQuizPage from './pages/CreateQuizPage';
import MyQuizSetsPage from './pages/MyQuizSetsPage';
import QuizDetailsPage from './pages/QuizDetailsPage';
import NotFoundPage from './pages/NotFoundPage';


function App() {
  const [accessToken, setAccessToken] = useState("");
  async function refreshAccessToken() {
    const response = await fetch("http://localhost:5000/api/Auth/refresh", {
      method: "POST",
      credentials: "include",
  });

  if (response.ok) {
    const data = await response.json();
    setAccessToken(data.accessToken);
  }
}
  useEffect(() => {
    refreshAccessToken();
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