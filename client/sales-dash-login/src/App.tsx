import React from 'react';
import LoginPage from './components/LoginPage';
import WelcomePage from './components/WelcomePage';
import './App.css';

function App() {
  const isAuthenticated = localStorage.getItem('token');

  return (
    <div className="App">
      {isAuthenticated ? <WelcomePage /> : <LoginPage />}
    </div>
  );
}

export default App;