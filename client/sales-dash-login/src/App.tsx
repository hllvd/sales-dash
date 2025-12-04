import React, { useState, useEffect } from 'react';
import LoginPage from './components/LoginPage';
import WelcomePage from './components/WelcomePage';
import UsersPage from './components/UsersPage';
import ContractsPage from './components/ContractsPage';
import UsersMappingPage from './components/UsersMappingPage';
import PVPage from './components/PVPage';
import MyContractsPage from './components/MyContractsPage';
import './App.css';

function App() {
  const isAuthenticated = localStorage.getItem('token');
  const [currentRoute, setCurrentRoute] = useState(window.location.hash || '#/home');

  useEffect(() => {
    const handleHashChange = () => {
      setCurrentRoute(window.location.hash || '#/home');
    };

    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  if (!isAuthenticated) {
    return (
      <div className="App">
        <LoginPage />
      </div>
    );
  }

  const renderPage = () => {
    switch (currentRoute) {
      case '#/users':
        return <UsersPage />;
      case '#/contracts':
        return <ContractsPage />;
      case '#/users-mapping':
        return <UsersMappingPage />;
      case '#/point-of-sale':
        return <PVPage />;
      case '#/my-contracts':
        return <MyContractsPage />;
      case '#/dashboards':
        return <WelcomePage title="Dashboards" />;
      case '#/grupos':
        return <WelcomePage title="Grupos" />;
      case '#/home':
      default:
        return <WelcomePage title="Home" />;
    }
  };

  return (
    <div className="App">
      {renderPage()}
    </div>
  );
}

export default App;