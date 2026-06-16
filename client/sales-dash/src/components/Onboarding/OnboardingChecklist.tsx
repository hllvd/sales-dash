import React, { useState } from 'react';
import { useOnboarding, OnboardingStep } from '../../contexts/OnboardingContext';
import { IconCheck, IconChevronDown, IconChevronUp, IconX, IconTrophy } from '@tabler/icons-react';
import './OnboardingChecklist.css';

export const OnboardingChecklist: React.FC = () => {
  const { steps, loading, isOnboardingComplete, showWizard, setShowWizard } = useOnboarding();
  const [isMinimized, setIsMinimized] = useState(false);

  React.useEffect(() => {
    const checkHash = () => {
      const currentHash = window.location.hash;
      const path = currentHash.split('?')[0];
      const isLandingPage = path === '#/my-contracts' || path === '#/home' || path === '#/' || path === '' || path === '#';
      if (!isLandingPage) {
        setIsMinimized(true);
      }
    };
    
    checkHash();
    
    window.addEventListener('hashchange', checkHash);
    return () => window.removeEventListener('hashchange', checkHash);
  }, []);

  if (loading || !showWizard || steps.length === 0) {
    return null;
  }

  const completedCount = steps.filter(s => s.isCompleted).length;
  const totalCount = steps.length;
  const progressPercent = totalCount > 0 ? (completedCount / totalCount) * 100 : 0;

  const handleStepClick = (step: OnboardingStep) => {
    if (step.isCompleted) return;
    // Navigate using the step target path
    window.location.hash = step.targetPath;
  };

  return (
    <div className={`onboarding-widget ${isMinimized ? 'minimized' : ''}`} role="region" aria-label="Passos de Configuração">
      {/* Header */}
      <div className="onboarding-header" onClick={() => setIsMinimized(!isMinimized)}>
        <div className="onboarding-title-group">
          <h3>Guia de Configuração</h3>
          <span className="onboarding-subtitle">
            {isOnboardingComplete 
              ? 'Tudo pronto! 🎉' 
              : `${completedCount} de ${totalCount} tarefas concluídas`
            }
          </span>
        </div>
        <div className="onboarding-header-actions" onClick={e => e.stopPropagation()}>
          <button 
            className="onboarding-toggle-btn"
            onClick={() => setIsMinimized(!isMinimized)}
            title={isMinimized ? "Expandir" : "Minimizar"}
          >
            {isMinimized ? <IconChevronUp size={16} /> : <IconChevronDown size={16} />}
          </button>
          <button 
            className="onboarding-close-btn"
            onClick={() => setShowWizard(false)}
            title="Fechar guia"
          >
            <IconX size={16} />
          </button>
        </div>
      </div>

      {/* Progress Bar */}
      <div className="onboarding-progress-container">
        <div 
          className="onboarding-progress-bar" 
          style={{ width: `${progressPercent}%` }} 
        />
      </div>

      {/* Body */}
      {!isMinimized && (
        <div className="onboarding-body">
          {isOnboardingComplete ? (
            <div className="onboarding-celebration">
              <div className="onboarding-celebration-icon">
                <IconTrophy size={40} />
              </div>
              <h4>Configuração Concluída!</h4>
              <p>Parabéns! Todos os passos iniciais foram executados com sucesso. Agora você pode explorar a plataforma normalmente.</p>
              <button 
                className="onboarding-celebration-btn"
                onClick={() => setShowWizard(false)}
              >
                Começar a usar
              </button>
            </div>
          ) : (
            <div className="onboarding-steps">
              {steps.map(step => (
                <div
                  key={step.id}
                  className={`onboarding-step-card ${step.isCompleted ? 'completed' : ''}`}
                  onClick={() => handleStepClick(step)}
                >
                  <div className="onboarding-checkbox-wrapper">
                    <div className={`onboarding-checkbox ${step.isCompleted ? 'checked' : ''}`}>
                      {step.isCompleted && <IconCheck size={12} color="#ffffff" strokeWidth={3} />}
                    </div>
                  </div>
                  <div className="onboarding-step-content">
                    <span className="onboarding-step-title">{step.title}</span>
                    <span className="onboarding-step-desc">{step.description}</span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
};
