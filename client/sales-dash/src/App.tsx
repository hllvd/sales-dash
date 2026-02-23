import React, { useState, useEffect } from 'react';
import { MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import LoginPage from './components/LoginPage';
import UsersPage from './components/UsersPage';
import ContractsPage from './components/ContractsPage';
import PVPage from './components/PVPage';
import MyContractsPage from './components/MyContractsPage';
import MatriculasPage from './components/MatriculasPage';
import MyProfilePage from './components/MyProfilePage';
import ImportHistoryPage from './components/ImportHistoryPage';
import ImportWizardPage from './components/ImportWizardPage';
import AccessControlPage from './components/AccessControlPage';
import { ContractsProvider } from './contexts/ContractsContext';
import { UsersProvider } from './contexts/UsersContext';
import { CurrentUserProvider } from './contexts/CurrentUserContext';
import { BuildInfoProvider } from './contexts/BuildInfoContext';
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
      case '#/point-of-sale':
        return <PVPage />;
      case '#/my-contracts':
        return <MyContractsPage />;
      case '#/matriculas':
        return <MatriculasPage />;
      case '#/my-profile':
        return <MyProfilePage />;
      case '#/import-history':
        return <ImportHistoryPage />;
      case '#/import-wizard':
        return <ImportWizardPage />;
      case '#/access-control':
        return <AccessControlPage />;
      case '#/dashboards':
      case '#/grupos':
      case '#/home':
      default:
        return <MyContractsPage />;
    }
  };

  return (
    <BuildInfoProvider>
      <CurrentUserProvider>
        <UsersProvider>
          <ContractsProvider>
            <MantineProvider>
              <Notifications />
              {renderPage()}
            </MantineProvider>
          </ContractsProvider>
        </UsersProvider>
      </CurrentUserProvider>
    </BuildInfoProvider>
  );
}

export default App;