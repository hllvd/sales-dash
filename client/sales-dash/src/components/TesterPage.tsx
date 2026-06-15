import React, { useState } from 'react';
import config from '../config';
import './TesterPage.css';

const TesterPage: React.FC = () => {
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const handleSendTestEmail = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setSuccess('');

    try {
      const response = await fetch(`${config.apiUrl}/users/forgot-password`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ email }),
      });

      const data = await response.json();

      if (data.success) {
        setSuccess(data.message || 'Solicitação enviada com sucesso! Verifique a caixa de entrada.');
      } else {
        setError(data.message || 'Erro ao enviar e-mail de teste.');
      }
    } catch (err) {
      setError('Erro de conexão com a API.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="tester-container">
      <div className="tester-card">
        <h1 className="tester-title">Painel de Testes (Tester)</h1>
        <p className="tester-subtitle">
          Utilize esta interface para testar funcionalidades do sistema, como o envio de e-mails via Amazon SES.
        </p>

        <div className="tester-section">
          <h2 className="section-title">Teste de Envio de E-mail</h2>
          <p className="section-description">
            Informe um e-mail cadastrado no sistema para disparar uma nova senha temporária. O sistema utilizará o template de recuperação de senha.
          </p>

          <form onSubmit={handleSendTestEmail} className="tester-form">
            {error && <div className="tester-error">{error}</div>}
            {success && <div className="tester-success">{success}</div>}

            <div className="form-group-tester">
              <label htmlFor="test-email">E-mail de Destino</label>
              <input
                id="test-email"
                type="email"
                placeholder="nome@empresa.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                disabled={loading}
                className="tester-input"
              />
            </div>

            <button type="submit" disabled={loading} className="tester-button">
              {loading ? 'Enviando...' : 'Disparar E-mail de Recuperação'}
            </button>
          </form>
        </div>

        <div className="tester-navigation">
          <a href="#/my-contracts" className="back-link">← Voltar para Minhas Vendas</a>
        </div>
      </div>
    </div>
  );
};

export default TesterPage;
