import React from 'react';
import './Menu.css';

const Menu: React.FC = () => {
  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    window.location.href = '/';
  };

  return (
    <div className="menu-sidebar">
      <div className="menu-header">
        <h2>Painel de Vendas</h2>
      </div>
      
      <nav className="menu-nav">
        <a href="/home" className="menu-item">
          <span>Home</span>
        </a>
        <a href="/dashboards" className="menu-item">
          <span>Dashboards</span>
        </a>
        <a href="/usuarios" className="menu-item">
          <span>Usuários</span>
        </a>
        <a href="/grupos" className="menu-item">
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