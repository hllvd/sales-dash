import React, { useState } from 'react';
import config from '../config';
import './LoginPage.css';

const LoginPage: React.FC = () => {
  const [mode, setMode] = useState<'login' | 'recovery'>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [successMessage, setSuccessMessage] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setSuccessMessage('');

    try {
      if (mode === 'login') {
        const response = await fetch(`${config.apiUrl}/users/login`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ email, password }),
        });

        const data = await response.json();

        if (data.success) {
          localStorage.setItem('token', data.data.token);
          localStorage.setItem('user', JSON.stringify(data.data.user));
          window.location.reload();
        } else {
          setError(data.message || 'Credenciais inválidas');
        }
      } else {
        const response = await fetch(`${config.apiUrl}/users/forgot-password`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ email }),
        });

        const data = await response.json();

        if (data.success) {
          setSuccessMessage(data.message || 'Se este e-mail estiver cadastrado, uma nova senha será enviada em breve.');
        } else {
          setError(data.message || 'Erro ao processar solicitação.');
        }
      }
    } catch (err) {
      setError('Erro de conexão. Tente novamente.');
    } finally {
      setLoading(false);
    }
  };

  const handleBackToLogin = (e?: React.MouseEvent) => {
    if (e) e.preventDefault();
    setMode('login');
    setSuccessMessage('');
    setError('');
    setPassword('');
  };

  const title = mode === 'login' ? 'Bem-vindo de Volta' : 'Recuperar Senha';
  const subtitle = mode === 'login' ? 'Entre na sua conta' : 'Digite seu e-mail cadastrado';

  return (
    <div className="login-container">
      <div className={`login-card ${mode === 'recovery' ? 'recovery-mode' : ''}`}>
        <div className="login-logo-container">
          <img src="/salesapp.logo.png" alt="SalesApp Logo" className="login-logo" />
        </div>
        <h1 className="login-title">{title}</h1>
        <p className="login-subtitle">{subtitle}</p>
        
        {successMessage ? (
          <div className="success-state">
            <div className="success-message">{successMessage}</div>
            <button
              onClick={() => handleBackToLogin()}
              className="login-button"
            >
              Voltar ao login
            </button>
          </div>
        ) : (
          <>
            <form onSubmit={handleSubmit} className="login-form">
              {error && <div className="error-message">{error}</div>}
              
              <div className="form-group">
                <input
                  type="email"
                  placeholder="E-mail"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="form-input"
                  required
                  disabled={loading}
                />
              </div>
              
              {mode === 'login' && (
                <div className="form-group">
                  <input
                    type="password"
                    placeholder="Senha"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="form-input"
                    required
                    disabled={loading}
                  />
                </div>
              )}
              
              <button type="submit" className="login-button" disabled={loading}>
                {loading 
                  ? (mode === 'login' ? 'Entrando...' : 'Processando...') 
                  : (mode === 'login' ? 'Entrar' : 'Recuperar Senha')
                }
              </button>
            </form>
            
            <p className="forgot-password">
              {mode === 'login' ? (
                <a href="#forgot" onClick={(e) => { e.preventDefault(); setMode('recovery'); setError(''); }}>
                  Esqueceu sua senha?
                </a>
              ) : (
                <a href="#login" onClick={handleBackToLogin} className="back-to-login">
                  Voltar ao login
                </a>
              )}
            </p>
          </>
        )}
      </div>
    </div>
  );
};

export default LoginPage;