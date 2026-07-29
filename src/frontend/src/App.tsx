import { Routes, Route } from 'react-router'
import { useEffect, useState } from "react";
import LoginPage from './pages/LoginPage'
import RegistrationPage from './pages/RegistrationPage'



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
    </Routes>
  )

}

export default App