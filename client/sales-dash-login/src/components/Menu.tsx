import React, { useState, useEffect } from 'react';
import './Menu.css';

const Menu: React.FC = () => {
  const [userRole, setUserRole] = useState('');
  const [currentPath, setCurrentPath] = useState(window.location.hash || '#/home');

  useEffect(() => {
    const user = JSON.parse(localStorage.getItem('user') || '{}');
    setUserRole(user.role || '');

    const handleHashChange = () => {
      setCurrentPath(window.location.hash || '#/home');
    };

    window.addEventListener('hashchange', handleHashChange);
    return () => window.removeEventListener('hashchange', handleHashChange);
  }, []);

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/';
  };

  const isActive = (path: string) => {
    return currentPath === path ? 'menu-item active' : 'menu-item';
  };

  return (
    <div className="menu-sidebar">
      <div className="menu-header">
        <h2>Painel de Vendas</h2>
      </div>
      
      <nav className="menu-nav">
        <a href="#/home" className={isActive('#/home')}>
          <span>Home</span>
        </a>
        <a href="#/dashboards" className={isActive('#/dashboards')}>
          <span>Dashboards</span>
        </a>
        {userRole === 'superadmin' && (
          <a href="#/users" className={isActive('#/users')}>
            <span>Usuários</span>
          </a>
        )}
        {userRole === 'superadmin' && (
          <a href="#/contracts" className={isActive('#/contracts')}>
            <span>Contratos</span>
          </a>
        )}
        {userRole === 'superadmin' && (
          <a href="#/users-mapping" className={isActive('#/users-mapping')}>
            <span>Mapeamento</span>
          </a>
        )}
        {(userRole === 'admin' || userRole === 'superadmin') && (
          <a href="#/point-of-sale" className={isActive('#/point-of-sale')}>
            <span>Pontos de Venda</span>
          </a>
        )}
        <a href="#/my-contracts" className={isActive('#/my-contracts')}>
          <span>Meus Contratos</span>
        </a>
        <a href="#/grupos" className={isActive('#/grupos')}>
          <span>Grupos</span>
        </a>
        <button onClick={handleLogout} className="menu-item logout">
          <span>Logout</span>
        </button>
      </nav>
    </div>
  );
};

export default Menu;