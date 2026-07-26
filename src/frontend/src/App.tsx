import { Routes, Route } from 'react-router'
import { useState } from "react";
import LoginPage from './pages/LoginPage'
import RegistrationPage from './pages/RegistrationPage'



function App() {
  const [accessToken, setAccessToken] = useState("");
  return(
    <Routes>
      <Route path="/login" element={<LoginPage accessToken={accessToken} setAccessToken={setAccessToken} />} />
      <Route path="/register" element={<RegistrationPage setAccessToken={setAccessToken} />} />
    </Routes>
  )

}

export default App