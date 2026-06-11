import React, { useState, useEffect } from 'react';
import { MantineProvider } from '@mantine/core';
import { Notifications } from '@mantine/notifications';
import LoginPage from './components/LoginPage';
import UsersPage from './components/UsersPage';
import UserTreePage from './components/UserTreePage';
import ContractsPage from './components/ContractsPage';
import PVPage from './components/PVPage';
import MyContractsPage from './components/MyContractsPage';
import MatriculasPage from './components/MatriculasPage';
import TeamsPage from './components/TeamsPage';
import ClassificationsPage from './components/ClassificationsPage';
import MyProfilePage from './components/MyProfilePage';
import ImportHistoryPage from './components/ImportHistoryPage';
import ImportWizardPage from './components/ImportWizardPage';
import AccessControlPage from './components/AccessControlPage';
import ScrapeDashboard from './components/Scrape/ScrapeDashboard';
import ReportsRouter from './components/Reports/ReportsRouter';
import ViewsRouter from './components/Reports/ViewsRouter';
import MatriculaHealthPage from './components/Monitoring/MatriculaHealthPage';
import UserMetadataAdminPage from './components/UserMetadataAdmin/UserMetadataAdminPage';
import { ContractsProvider } from './contexts/ContractsContext';
import { UsersProvider } from './contexts/UsersContext';
import { CurrentUserProvider } from './contexts/CurrentUserContext';
import { BuildInfoProvider } from './contexts/BuildInfoContext';
import '@mantine/core/styles.css';
import '@mantine/notifications/styles.css';
import '@mantine/charts/styles.css';
import ErrorBoundary from './components/ErrorBoundary';
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
    const routePath = currentRoute.split('?')[0];

    if (routePath.startsWith('#/reports')) {
      return <ReportsRouter currentRoute={currentRoute} />;
    }

    if (routePath.startsWith('#/views')) {
      return <ViewsRouter currentRoute={currentRoute} />;
    }

    switch (routePath) {
      case '#/users':
        return <UsersPage />;
      case '#/users/tree':
        return <UserTreePage />;
      case '#/contracts':
        return <ContractsPage />;
      case '#/point-of-sale':
        return <PVPage />;
      case '#/my-contracts':
        return <MyContractsPage />;
      case '#/matriculas':
        return <MatriculasPage />;
      case '#/teams':
        return <TeamsPage />;
      case '#/classifications':
        return <ClassificationsPage />;
      case '#/my-profile':
        return <MyProfilePage />;
      case '#/import-history':
        return <ImportHistoryPage />;
      case '#/import-wizard':
        return <ImportWizardPage />;
      case '#/access-control':
        return <AccessControlPage />;
      case '#/scrapes':
      case '#/scrapes/historial':
        return <ScrapeDashboard initialTab={currentRoute === '#/scrapes/historial' ? 'history' : 'links'} />;
      case '#/monitoring/matricula-health':
        return <MatriculaHealthPage />;
      case '#/user-metadata-fields':
        return <UserMetadataAdminPage />;
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
              <ErrorBoundary>
                {renderPage()}
              </ErrorBoundary>
            </MantineProvider>
          </ContractsProvider>
        </UsersProvider>
      </CurrentUserProvider>
    </BuildInfoProvider>
  );
}

export default App;