import React, { useEffect } from 'react';
import ViewsListPage from './ViewsListPage';
import ViewFormPage from './ViewFormPage';
import ViewExecutionPage from './ViewExecutionPage';
import { useOnboarding } from '../../contexts/OnboardingContext';

interface ViewsRouterProps {
  currentRoute: string;
}

const ViewsRouter: React.FC<ViewsRouterProps> = ({ currentRoute }) => {
  const { markStepDone } = useOnboarding();

  useEffect(() => {
    markStepDone('push_dashboard');
  }, [markStepDone]);
  // Routes:
  // #/views
  // #/views/new
  // #/views/:id/edit
  // #/views/:id

  if (currentRoute === '#/views' || currentRoute === '#/views/') {
    return <ViewsListPage />;
  }

  if (currentRoute === '#/views/new') {
    return <ViewFormPage />;
  }

  // Check for dynamic edit route: #/views/:id/edit
  const editMatch = currentRoute.match(/^#\/views\/([^/]+)\/edit$/);
  if (editMatch) {
    return <ViewFormPage viewId={editMatch[1]} />;
  }

  // Check for dynamic view execution route: #/views/:id
  const viewMatch = currentRoute.match(/^#\/views\/([^/]+)$/);
  if (viewMatch) {
    return <ViewExecutionPage viewId={viewMatch[1]} />;
  }

  // Fallback to views list
  return <ViewsListPage />;
};

export default ViewsRouter;
