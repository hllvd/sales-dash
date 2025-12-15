import React, { useState, useEffect } from 'react';
import { MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import LoginPage from './components/LoginPage';
import WelcomePage from './components/WelcomePage';
import UsersPage from './components/UsersPage';
import ContractsPage from './components/ContractsPage';
import UsersMappingPage from './components/UsersMappingPage';
import PVPage from './components/PVPage';
import MyContractsPage from './components/MyContractsPage';
import '@mantine/core/styles.css';
import '@mantine/notifications/styles.css';
import '@mantine/charts/styles.css';
import './App.css';

function App() {
  const isAuthenticated = localStorage.getItem('token');
  const [currentRoute, setCurrentRoute] = useState(window.location.hash || '#/my-contracts');

  useEffect(() => {
    const handleHashChange = () => {
      setCurrentRoute(window.location.hash || '#/my-contracts');
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
      case '#/grupos':
      case '#/home':
      default:
        return <MyContractsPage />;
    }
  };

  return (
    <MantineProvider>
      <Notifications />
      {renderPage()}
    </MantineProvider>
  );
}

export default App;