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
import TeamCalendarPage from './components/TeamCalendarPage';
import StoresPage from './components/StoresPage';
import ClassificationsPage from './components/ClassificationsPage';
import MyProfilePage from './components/MyProfilePage';
import ImportHistoryPage from './components/ImportHistoryPage';
import ImportWizardPage from './components/ImportWizardPage';
import AccessControlPage from './components/AccessControlPage';
import ScrapeDashboard from './components/Scrape/ScrapeDashboard';
import ScrapeRunDetailPage from './components/Scrape/ScrapeRunDetailPage';
import ReportsRouter from './components/Reports/ReportsRouter';
import ViewsRouter from './components/Reports/ViewsRouter';
import MatriculaHealthPage from './components/Monitoring/MatriculaHealthPage';
import LicensingPage from './components/Monitoring/LicensingPage';
import UserMetadataAdminPage from './components/UserMetadataAdmin/UserMetadataAdminPage';
import AdminRegistrationPage from './components/AdminRegistrationPage';
import TesterPage from './components/TesterPage';
import BatchPage from './components/BatchPage';
import ContractReconciliationPage from './components/ContractReconciliationPage';
import RetentionFilterPage from './components/RetentionFilterPage';
import RequestsPage from './components/RequestsPage';
import SurveyPage from './components/Survey/SurveyPage';
import MyQAPage from './components/Survey/MyQAPage';
import { SurveyModal } from './components/Survey/SurveyModal';
import { surveyPollingService } from './services/surveyPollingService';
import { ContractsProvider } from './contexts/ContractsContext';
import { UsersProvider } from './contexts/UsersContext';
import { CurrentUserProvider } from './contexts/CurrentUserContext';
import { BuildInfoProvider } from './contexts/BuildInfoContext';
import { ReferenceDataProvider } from './contexts/ReferenceDataContext';
import { NotificationProvider } from './contexts/NotificationContext';
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

  useEffect(() => {
    if (typeof (window as any).gtag === 'function') {
      (window as any).gtag('event', 'page_view', {
        page_path: currentRoute,
        page_title: document.title,
      });
    }
  }, [currentRoute]);

  useEffect(() => {
    if (isAuthenticated) {
      surveyPollingService.start();
    }
    return () => {
      surveyPollingService.stop();
    };
  }, [isAuthenticated]);

  const routePath = currentRoute.split('?')[0];
  const isPublicRoute = routePath === '#/user/registration/admin';

  if (!isAuthenticated && !isPublicRoute) {
    return (
      <div className="App">
        <LoginPage />
      </div>
    );
  }

  const renderPage = () => {
    if (routePath.startsWith('#/reports')) {
      return <ReportsRouter currentRoute={currentRoute} />;
    }

    if (routePath.startsWith('#/views')) {
      return <ViewsRouter currentRoute={currentRoute} />;
    }

    if (routePath.startsWith('#/scrapes/runs/')) {
      const runId = routePath.replace('#/scrapes/runs/', '');
      return <ScrapeRunDetailPage runId={runId} />;
    }

    switch (routePath) {
      case '#/user/registration/admin':
        return <AdminRegistrationPage />;
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
      case '#/teams/calendar':
        return <TeamCalendarPage />;
      case '#/stores':
        return <StoresPage />;
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
      case '#/monitoring/licensing':
        return <LicensingPage />;
      case '#/user-metadata-fields':
        return <UserMetadataAdminPage />;
      case '#/tester':
        return <TesterPage />;
      case '#/batch':
        return <BatchPage />;
      case '#/contract-reconciliation':
        return <ContractReconciliationPage />;
      case '#/retention-filter':
        return <RetentionFilterPage />;
      case '#/requests':
        return <RequestsPage />;
      case '#/surveys':
        return <SurveyPage />;
      case '#/qa':
        return <MyQAPage />;
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
            <ReferenceDataProvider>
              <MantineProvider>
                <Notifications />
                <NotificationProvider>
                  <SurveyModal />
                  <ErrorBoundary>
                    {renderPage()}
                  </ErrorBoundary>
                </NotificationProvider>
              </MantineProvider>
            </ReferenceDataProvider>
          </ContractsProvider>
        </UsersProvider>
      </CurrentUserProvider>
    </BuildInfoProvider>
  );
}

export default App;