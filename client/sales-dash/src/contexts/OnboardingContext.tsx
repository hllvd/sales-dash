import React, { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react';
import { useCurrentUser } from './CurrentUserContext';

export interface OnboardingStep {
  id: string;
  title: string;
  description: string;
  targetPath: string;
  isCompleted: boolean;
}

interface OnboardingContextType {
  steps: OnboardingStep[];
  loading: boolean;
  isOnboardingComplete: boolean;
  markStepDone: (stepId: string) => void;
  resetOnboarding: () => void;
  showWizard: boolean;
  setShowWizard: (show: boolean) => void;
}

const OnboardingContext = createContext<OnboardingContextType | undefined>(undefined);

const getInitialSteps = (role: string): OnboardingStep[] => {
  const roleLower = role?.toLowerCase();

  if (roleLower === 'superadmin') {
    return [];
  }

  if (roleLower === 'admin') {
    return [
      {
        id: 'import_contracts',
        title: 'Importar contratos via Assistente',
        description: 'Faça upload da planilha de contratos no Assistente de Importação.',
        targetPath: '#/import-wizard',
        isCompleted: false,
      },
      {
        id: 'add_teams',
        title: 'Adicionar usuários às Equipes',
        description: 'Vincule seus vendedores a equipes na página de Equipes.',
        targetPath: '#/teams',
        isCompleted: false,
      },
      {
        id: 'push_dashboard',
        title: 'Acessar/Criar Dashboards',
        description: 'Veja ou publique painéis/dashboards para acompanhamento.',
        targetPath: '#/views',
        isCompleted: false,
      },
      {
        id: 'view_reports',
        title: 'Visualizar Relatórios',
        description: 'Consulte os relatórios detalhados de produção.',
        targetPath: '#/reports',
        isCompleted: false,
      },
    ];
  }

  // Regular user steps
  return [
    {
      id: 'check_contracts',
      title: 'Verificar seus contratos',
      description: 'Confira se todos os seus contratos constam na lista "Meus Contratos".',
      targetPath: '#/my-contracts',
      isCompleted: false,
    },
  ];
};

export const OnboardingProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
  const { currentUser } = useCurrentUser();
  const [steps, setSteps] = useState<OnboardingStep[]>([]);
  const [loading, setLoading] = useState(true);
  const [showWizard, setShowWizard] = useState(false);

  // Load state from localStorage on init or user changes
  useEffect(() => {
    if (!currentUser) {
      setSteps([]);
      setLoading(false);
      setShowWizard(false);
      return;
    }

    const storageKey = `onboarding_completed_${currentUser.id}`;
    const initial = getInitialSteps(currentUser.role);
    
    try {
      const savedCompletedIds = localStorage.getItem(storageKey);
      if (savedCompletedIds) {
        const completedIds: string[] = JSON.parse(savedCompletedIds);
        const mergedSteps = initial.map(step => ({
          ...step,
          isCompleted: completedIds.includes(step.id),
        }));
        setSteps(mergedSteps);
        
        // Show wizard if not all steps are completed
        const allDone = mergedSteps.every(s => s.isCompleted);
        setShowWizard(!allDone);
      } else {
        setSteps(initial);
        setShowWizard(true);
      }
    } catch (e) {
      console.error('Failed to parse onboarding progress from localStorage:', e);
      setSteps(initial);
      setShowWizard(true);
    } finally {
      setLoading(false);
    }
  }, [currentUser]);

  const markStepDone = useCallback((stepId: string) => {
    if (!currentUser) return;

    setSteps(prevSteps => {
      const targetStep = prevSteps.find(s => s.id === stepId);
      if (!targetStep || targetStep.isCompleted) return prevSteps;

      const updated = prevSteps.map(step => 
        step.id === stepId ? { ...step, isCompleted: true } : step
      );

      const completedIds = updated.filter(s => s.isCompleted).map(s => s.id);
      const storageKey = `onboarding_completed_${currentUser.id}`;
      localStorage.setItem(storageKey, JSON.stringify(completedIds));

      return updated;
    });
  }, [currentUser]);

  const resetOnboarding = useCallback(() => {
    if (!currentUser) return;
    const storageKey = `onboarding_completed_${currentUser.id}`;
    localStorage.removeItem(storageKey);
    setSteps(getInitialSteps(currentUser.role));
    setShowWizard(true);
  }, [currentUser]);

  const isOnboardingComplete = steps.length > 0 && steps.every(step => step.isCompleted);

  return (
    <OnboardingContext.Provider
      value={{
        steps,
        loading,
        isOnboardingComplete,
        markStepDone,
        resetOnboarding,
        showWizard,
        setShowWizard,
      }}
    >
      {children}
    </OnboardingContext.Provider>
  );
};

export const useOnboarding = () => {
  const context = useContext(OnboardingContext);
  if (context === undefined) {
    throw new Error('useOnboarding must be used within an OnboardingProvider');
  }
  return context;
};
