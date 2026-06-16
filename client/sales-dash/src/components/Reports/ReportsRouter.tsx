import React, { useEffect } from 'react';
import ReportListPage from './ReportListPage';
import ReportFormPage from './ReportFormPage';
import ReportResultsPage from './ReportResultsPage';
import { useOnboarding } from '../../contexts/OnboardingContext';

interface ReportsRouterProps {
  currentRoute: string;
}

const ReportsRouter: React.FC<ReportsRouterProps> = ({ currentRoute }) => {
  const { markStepDone } = useOnboarding();

  useEffect(() => {
    markStepDone('view_reports');
  }, [markStepDone]);
  // Routes:
  // #/reports
  // #/reports/new
  // #/reports/:id/edit
  // #/reports/:id/results

  if (currentRoute === '#/reports' || currentRoute === '#/reports/') {
    return <ReportListPage />;
  }

  if (currentRoute === '#/reports/new') {
    return <ReportFormPage />;
  }

  // Check for dynamic routes
  const editMatch = currentRoute.match(/^#\/reports\/([^/]+)\/edit$/);
  if (editMatch) {
    return <ReportFormPage filterId={editMatch[1]} />;
  }

  const resultsMatch = currentRoute.match(/^#\/reports\/([^/]+)\/results$/);
  if (resultsMatch) {
    return <ReportResultsPage filterId={resultsMatch[1]} />;
  }

  // Fallback to list
  return <ReportListPage />;
};

export default ReportsRouter;
