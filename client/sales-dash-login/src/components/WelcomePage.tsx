import React from 'react';
import Menu from './Menu';
import './WelcomePage.css';

const WelcomePage: React.FC = () => {
  const user = JSON.parse(localStorage.getItem('user') || '{}');

  return (
    <div className="welcome-layout">
      <Menu />
      <div className="welcome-content">
        <div className="welcome-container">
          <h1 className="welcome-title">Bem-vindo, {user.name || 'Usuário'}!</h1>
          <p className="welcome-subtitle">Painel de Vendas</p>
          <div className="welcome-card">
            <h2>Dashboard Principal</h2>
            <p>Acesse suas métricas e relatórios de vendas através do menu lateral.</p>
          </div>
        </div>
      </div>
    </div>
  );
};

export default WelcomePage;