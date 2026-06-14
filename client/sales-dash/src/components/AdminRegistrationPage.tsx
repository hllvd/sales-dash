import React, { useState, useEffect } from 'react';
import config from '../config';
import { apiService } from '../services/apiService';
import './AdminRegistrationPage.css';

// WhatsApp icon SVG component
const WhatsAppIcon: React.FC<{ className?: string }> = ({ className }) => (
  <svg viewBox="0 0 448 512" className={className} xmlns="http://www.w3.org/2000/svg">
    <path d="M380.9 97.1C339 55.1 283.2 32 223.9 32c-122.4 0-222 99.6-222 222 0 39.1 10.2 77.3 29.6 111L0 480l117.7-30.9c32.4 17.7 68.9 27 106.1 27h.1c122.3 0 224.1-99.6 224.1-222 0-59.3-25.2-115-67.1-157zm-157 341.6c-33.2 0-65.7-8.9-94-25.7l-6.7-4-69.8 18.3L72 359.2l-4.4-7c-18.5-29.4-28.2-63.3-28.2-98.2 0-101.7 82.8-184.5 184.6-184.5 49.3 0 95.6 19.2 130.4 54.1 34.8 34.9 56.2 81.2 56.1 130.5 0 101.8-84.9 184.6-186.6 184.6zm101.2-138.2c-5.5-2.8-32.8-16.2-37.9-18-5.1-1.9-8.8-2.8-12.5 2.8-3.7 5.6-14.3 18-17.6 21.8-3.2 3.7-6.5 4.2-12 1.4-32.6-16.3-54-29.1-75.5-66-5.7-9.8 5.7-9.1 16.3-30.3 1.8-3.7.9-6.9-.5-9.7-1.4-2.8-12.5-30.1-17.1-41.2-4.5-10.8-9.1-9.3-12.5-9.5-3.2-.2-6.9-.2-10.6-.2-3.7 0-9.7 1.4-14.8 6.9-5.1 5.6-19.4 19-19.4 46.3 0 27.3 19.9 53.7 22.6 57.4 2.8 3.7 39.1 59.7 94.8 83.8 35.2 15.2 49 16.5 66.6 13.9 10.7-1.6 32.8-13.4 37.4-26.4 4.6-13 4.6-24.1 3.2-26.4-1.3-2.5-5-3.9-10.5-6.6z" />
  </svg>
);

// Decodes an encoded string safely (supports decimal ASCII with or without hyphens), returning decoded string if it matches Date format, otherwise empty string
const decodeToken = (token: string): string => {
  if (!token) return '';

  // 1. Try Decimal Encoding with hyphens (e.g. 50-48-50-54-45-48-54-45-49-53-58-49-56-58-48-48)
  if (/^(\d+-)+\d+$/.test(token)) {
    try {
      const decoded = token.split('-').map(code => String.fromCharCode(parseInt(code, 10))).join('');
      if (/^\d{4}-\d{2}-\d{2}:\d{2}:\d{2}$/.test(decoded)) {
        return decoded;
      }
    } catch (e) {
      // ignore parsing errors
    }
  }

  // 2. Try Decimal Encoding without hyphens (e.g. 50485054454854454953584956584848)
  if (/^\d{32}$/.test(token)) {
    try {
      let decoded = '';
      for (let i = 0; i < token.length; i += 2) {
        const code = parseInt(token.substring(i, i + 2), 10);
        decoded += String.fromCharCode(code);
      }
      if (/^\d{4}-\d{2}-\d{2}:\d{2}:\d{2}$/.test(decoded)) {
        return decoded;
      }
    } catch (e) {
      // ignore parsing errors
    }
  }
  return '';
};

// Hook to parse expiration date and update countdown
const useCountdown = (dParam: string | null) => {
  const [remaining, setRemaining] = useState<number | null>(null);
  const [expired, setExpired] = useState<boolean>(false);

  useEffect(() => {
    if (!dParam) {
      // If no d param is present, default to no restriction (not expired)
      setExpired(false);
      return;
    }

    const dateStr = decodeToken(dParam);
    const match = dateStr.match(/^(\d{4})-(\d{2})-(\d{2}):(\d{2}):(\d{2})$/);
    if (!match) {
      setExpired(true);
      return;
    }

    const [, year, month, day, hour, minute] = match;
    const expiryDate = new Date(
      parseInt(year, 10),
      parseInt(month, 10) - 1,
      parseInt(day, 10),
      parseInt(hour, 10),
      parseInt(minute, 10),
      0
    );

    const updateTimer = () => {
      const now = new Date();
      const diff = Math.floor((expiryDate.getTime() - now.getTime()) / 1000);
      if (diff <= 0) {
        setRemaining(0);
        setExpired(true);
      } else {
        setRemaining(diff);
        setExpired(false);
      }
    };

    updateTimer();
    const interval = setInterval(updateTimer, 1000);
    return () => clearInterval(interval);
  }, [dParam]);

  return { remaining, expired };
};

const AdminRegistrationPage: React.FC = () => {
  // Query param parsing
  const hash = window.location.hash;
  const queryString = hash.includes('?') ? hash.split('?')[1] : '';
  const params = new URLSearchParams(queryString);
  const dParam = params.get('d');

  const { remaining, expired } = useCountdown(dParam);

  // Wizard state
  const [step, setStep] = useState<1 | 2 | 3>(1);

  // Form Field values
  const [managerEmail, setManagerEmail] = useState('');
  const [emailChecked, setEmailChecked] = useState(false);
  const [emailExists, setEmailExists] = useState(false);
  const [contactPhone, setContactPhone] = useState('47989133138'); // default admin fallback

  // Basic info (Step 1 when email doesn't exist)
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const [teamName, setTeamName] = useState('');
  const [levels, setLevels] = useState<{ id: number; name: string }[]>([]);
  const [selectedLevelId, setSelectedLevelId] = useState<number | ''>('');
  const [startDate, setStartDate] = useState('');

  // Parent autocomplete suggestions
  const [parentEmail, setParentEmail] = useState('');
  const [parentSuggestions, setParentSuggestions] = useState<{ name: string; email: string }[]>([]);
  const [showSuggestions, setShowSuggestions] = useState(false);

  useEffect(() => {
    if (!parentEmail || parentEmail.length < 2) {
      setParentSuggestions([]);
      return;
    }

    const fetchSuggestions = async () => {
      try {
        const res = await apiService.autocompleteParents(parentEmail);
        if (res.success && Array.isArray(res.data)) {
          setParentSuggestions(res.data);
        }
      } catch (err) {
        console.error('Failed to fetch parent suggestions:', err);
      }
    };

    const delay = setTimeout(fetchSuggestions, 300);
    return () => clearTimeout(delay);
  }, [parentEmail]);

  // Role selections (Step 2)
  const [selectedRole, setSelectedRole] = useState<'manager' | 'secretary'>('manager');
  const [secretaryName, setSecretaryName] = useState('');
  const [secretaryEmail, setSecretaryEmail] = useState('');
  const [secretaryWhatsapp, setSecretaryWhatsapp] = useState('');

  // Step 3 Login
  const [loginEmail, setLoginEmail] = useState('');
  const [loginPassword, setLoginPassword] = useState('');
  const [loginError, setLoginError] = useState('');
  const [loginLoading, setLoginLoading] = useState(false);

  // Global UI
  const [globalLoading, setGlobalLoading] = useState(false);
  const [globalError, setGlobalError] = useState('');

  // Fetch classification levels publicly
  useEffect(() => {
    const fetchLevels = async () => {
      try {
        const response = await fetch(`${config.apiUrl}/classifications/levels`);
        const json = await response.json();
        if (json.success && Array.isArray(json.data)) {
          setLevels(json.data);
          if (json.data.length > 0) {
            setSelectedLevelId(json.data[0].id);
          }
        }
      } catch (err) {
        console.error('Failed to fetch classification levels:', err);
      }
    };
    fetchLevels();
  }, []);

  // Format countdown text
  const formatCountdown = (totalSeconds: number | null): string => {
    if (totalSeconds === null) return '--:--:--';
    const days = Math.floor(totalSeconds / (3600 * 24));
    const hours = Math.floor((totalSeconds % (3600 * 24)) / 3600);
    const minutes = Math.floor((totalSeconds % 3600) / 60);
    const seconds = totalSeconds % 60;

    let str = '';
    if (days > 0) str += `${days}d `;
    str += `${hours.toString().padStart(2, '0')}h `;
    str += `${minutes.toString().padStart(2, '0')}m `;
    str += `${seconds.toString().padStart(2, '0')}s`;
    return str;
  };

  // Step 1: Handle check email
  const handleEmailBlur = async () => {
    if (!managerEmail || !managerEmail.includes('@')) return;
    setGlobalLoading(true);
    setGlobalError('');
    try {
      const response = await apiService.checkEmail(managerEmail);
      if (response.success && response.data) {
        setEmailChecked(true);
        setEmailExists(response.data.exists);
        if (response.data.contactPhone) {
          setContactPhone(response.data.contactPhone);
        }
      } else {
        setGlobalError('Erro ao verificar email.');
      }
    } catch (err: any) {
      setGlobalError(err.message || 'Erro de conexão.');
    } finally {
      setGlobalLoading(false);
    }
  };

  // Step 1 Submission
  const handleStep1Submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name || !password || !teamName || !selectedLevelId) {
      setGlobalError('Por favor, preencha todos os campos obrigatórios.');
      return;
    }
    setGlobalError('');
    setStep(2);
  };

  // Final Registration Submission (Step 2)
  const handleRegisterSubmit = async () => {
    setGlobalLoading(true);
    setGlobalError('');
    try {
      const payload = {
        email: managerEmail,
        name,
        password,
        teamName,
        classificationLevelId: Number(selectedLevelId),
        classificationStartDate: startDate || undefined,
        role: selectedRole,
        secretaryName: selectedRole === 'secretary' ? secretaryName : undefined,
        secretaryEmail: selectedRole === 'secretary' ? secretaryEmail : undefined,
        secretaryWhatsapp: selectedRole === 'secretary' ? secretaryWhatsapp : undefined,
        parentEmail: parentEmail || undefined,
      };

      const res = await apiService.adminRegister(payload);
      if (res.success) {
        setStep(3);
        // Pre-fill login email for convenience
        setLoginEmail(managerEmail);
      } else {
        setGlobalError(res.message || 'Falha ao realizar cadastro.');
      }
    } catch (err: any) {
      setGlobalError(err.message || 'Erro de conexão ao cadastrar.');
    } finally {
      setGlobalLoading(false);
    }
  };

  // Step 3: Login form submission
  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoginLoading(true);
    setLoginError('');
    try {
      const response = await fetch(`${config.apiUrl}/users/login`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ email: loginEmail, password: loginPassword }),
      });
      const data = await response.json();
      if (data.success) {
        localStorage.setItem('token', data.data.token);
        localStorage.setItem('user', JSON.stringify(data.data.user));
        // Redirect to dashboard
        window.location.hash = '#/my-contracts';
        window.location.reload();
      } else {
        setLoginError(data.message || 'Credenciais inválidas.');
      }
    } catch (err) {
      setLoginError('Erro de conexão ao tentar fazer login.');
    } finally {
      setLoginLoading(false);
    }
  };

  return (
    <div className="admin-reg-container">
      <div className="admin-reg-card">
        {/* Lock Overlay when expired */}
        {expired && (
          <div className="lock-overlay">
            <div className="lock-icon">🔒</div>
            <h2 className="lock-title">Link Expirado</h2>
            <p className="lock-subtitle">
              Este link de cadastro expirou. Por favor, solicite um novo link ao administrador.
            </p>
          </div>
        )}

        {/* Countdown Banner */}
        {dParam && !expired && remaining !== null && (
          <div className="countdown-banner">
            <div className="countdown-label">
              <div className="countdown-pulse" />
              <span>Limite de Tempo Restante</span>
            </div>
            <div className="countdown-timer">{formatCountdown(remaining)}</div>
          </div>
        )}

        {/* Header */}
        <div className="reg-logo-container">
          <img src="/salesapp.logo.png" alt="Logo" className="reg-logo" />
        </div>
        <h1 className="reg-title">Cadastro de Novo Gestor</h1>
        <p className="reg-subtitle">Preencha o formulário abaixo para configurar seu acesso</p>

        {/* Steps Progress */}
        <div className="steps-indicator">
          <div className="step-dot-container">
            <div className={`step-dot ${step === 1 ? 'active' : step > 1 ? 'completed' : ''}`}>
              {step > 1 ? '✓' : '1'}
            </div>
            <div className={`step-label ${step === 1 ? 'active' : step > 1 ? 'completed' : ''}`}>Dados do Gestor</div>
          </div>
          <div className="step-dot-container">
            <div className={`step-dot ${step === 2 ? 'active' : step > 2 ? 'completed' : ''}`}>
              {step > 2 ? '✓' : '2'}
            </div>
            <div className={`step-label ${step === 2 ? 'active' : step > 2 ? 'completed' : ''}`}>Perfil de Trabalho</div>
          </div>
          <div className="step-dot-container">
            <div className={`step-dot ${step === 3 ? 'active' : ''}`}>3</div>
            <div className={`step-label ${step === 3 ? 'active' : ''}`}>Acesso & Vídeo</div>
          </div>
        </div>

        {globalError && <div className="alert-error" style={{ marginBottom: 20 }}>{globalError}</div>}

        {/* STEP 1: Email verify + registration basics */}
        {step === 1 && (
          <form onSubmit={handleStep1Submit} className="form-section">
            <div className="form-group-reg">
              <label className="form-label-reg" htmlFor="managerEmail">E-mail do Gestor *</label>
              <input
                id="managerEmail"
                type="email"
                className="form-input-reg"
                placeholder="exemplo@gestor.com"
                value={managerEmail}
                onChange={(e) => {
                  setManagerEmail(e.target.value);
                  setEmailChecked(false);
                }}
                onBlur={handleEmailBlur}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    if (managerEmail && managerEmail.includes('@') && !globalLoading) {
                      handleEmailBlur();
                    }
                  }
                }}
                required
                disabled={globalLoading || expired}
              />
            </div>

            {!emailChecked && (
              <div className="buttons-row" style={{ marginTop: 15 }}>
                <button
                  type="button"
                  className="btn-reg btn-primary-reg"
                  onClick={handleEmailBlur}
                  disabled={globalLoading || expired || !managerEmail || !managerEmail.includes('@')}
                >
                  {globalLoading ? 'Verificando...' : 'Verificar E-mail'}
                </button>
              </div>
            )}

            {emailChecked && emailExists && (
              <div className="user-exists-banner">
                <p className="user-exists-text">
                  Usuário já cadastrado. Faça login ou entre em contato via WhatsApp para tirar suas dúvidas.
                </p>
                <a
                  href={`https://wa.me/55${contactPhone}?text=Olá,%20gostaria%20de%20tirar%20uma%20dúvida%20sobre%20o%20cadastro.`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="whatsapp-btn"
                >
                  <WhatsAppIcon className="whatsapp-icon" />
                  Falar com Suporte ({contactPhone})
                </a>
              </div>
            )}

            {emailChecked && !emailExists && (
              <>
                <div className="form-group-reg">
                  <label className="form-label-reg" htmlFor="name">Nome Completo *</label>
                  <input
                    id="name"
                    type="text"
                    className="form-input-reg"
                    placeholder="Seu nome"
                    value={name}
                    onChange={(e) => setName(e.target.value)}
                    required
                    disabled={globalLoading || expired}
                  />
                </div>

                <div className="form-group-reg">
                  <label className="form-label-reg" htmlFor="password">Senha de Acesso *</label>
                  <input
                    id="password"
                    type="password"
                    className="form-input-reg"
                    placeholder="Mínimo 6 caracteres"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    required
                    disabled={globalLoading || expired}
                  />
                </div>

                <div className="form-group-reg">
                  <label className="form-label-reg" htmlFor="teamName">Equipe (Nome do Time) *</label>
                  <input
                    id="teamName"
                    type="text"
                    className="form-input-reg"
                    placeholder="ex. Equipe Alfa"
                    value={teamName}
                    onChange={(e) => setTeamName(e.target.value)}
                    required
                    disabled={globalLoading || expired}
                  />
                </div>

                <div className="form-group-reg">
                  <label className="form-label-reg" htmlFor="classification">Nível Atual (Classificação do gestor) *</label>
                  <select
                    id="classification"
                    className="form-input-reg form-select-reg"
                    value={selectedLevelId}
                    onChange={(e) => setSelectedLevelId(Number(e.target.value))}
                    required
                    disabled={globalLoading || expired}
                  >
                    {levels.map((lvl) => (
                      <option key={lvl.id} value={lvl.id}>
                        {lvl.name}
                      </option>
                    ))}
                  </select>
                </div>

                <div className="form-group-reg">
                  <label className="form-label-reg" htmlFor="startDate">Data de Início na Classificação (Opcional)</label>
                  <input
                    id="startDate"
                    type="date"
                    className="form-input-reg"
                    value={startDate}
                    onChange={(e) => setStartDate(e.target.value)}
                    disabled={globalLoading || expired}
                  />
                </div>
                <div className="form-group-reg" style={{ position: 'relative' }}>
                  <label className="form-label-reg" htmlFor="parentEmail">E-mail do Gestor Superior (Opcional)</label>
                  <input
                    id="parentEmail"
                    type="email"
                    className="form-input-reg"
                    placeholder="Busque por nome ou e-mail..."
                    value={parentEmail}
                    onChange={(e) => {
                      setParentEmail(e.target.value);
                      setShowSuggestions(true);
                    }}
                    onFocus={() => setShowSuggestions(true)}
                    onBlur={() => setTimeout(() => setShowSuggestions(false), 200)}
                    disabled={globalLoading || expired}
                  />
                  {showSuggestions && parentSuggestions.length > 0 && (
                    <ul className="autocomplete-suggestions">
                      {parentSuggestions.map((suggestion, index) => (
                        <li
                          key={index}
                          onClick={() => {
                            setParentEmail(suggestion.email);
                            setShowSuggestions(false);
                          }}
                          className="suggestion-item"
                        >
                          <span className="suggestion-name">{suggestion.name}</span>
                          <span className="suggestion-email">({suggestion.email})</span>
                        </li>
                      ))}
                    </ul>
                  )}
                  <p className="field-helper-text">
                    É importante colocar o nome do gestor acima de "{name || 'seu nome'}". Se não souber, deixe em branco e busque contato com{' '}
                    <a
                      href={`https://wa.me/55${contactPhone}?text=Olá,%20gostaria%20de%20saber%20quem%20é%20meu%20gestor%20superior.`}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="whatsapp-link-inline"
                    >
                      <WhatsAppIcon className="whatsapp-icon-inline" />
                      {contactPhone}
                    </a>
                  </p>
                </div>


                <div className="buttons-row">
                  <button type="submit" className="btn-reg btn-primary-reg" disabled={globalLoading || expired}>
                    Continuar
                  </button>
                </div>
              </>
            )}
          </form>
        )}

        {/* STEP 2: Role card + Optional secretary contact */}
        {step === 2 && (
          <div className="form-section">
            <h3 className="form-label-reg" style={{ marginBottom: 10 }}>Quem está realizando este cadastro?</h3>
            <div className="role-cards-container">
              <div
                className={`role-card ${selectedRole === 'manager' ? 'selected' : ''}`}
                onClick={() => { if (expired) return; setSelectedRole('manager'); }}
              >
                <div className="role-icon">👤</div>
                <div className="role-name">Eu sou o Gestor</div>
                <div className="role-desc">Vou gerenciar e acompanhar os meus contratos diretamente.</div>
              </div>

              <div
                className={`role-card ${selectedRole === 'secretary' ? 'selected' : ''}`}
                onClick={() => { if (expired) return; setSelectedRole('secretary'); }}
              >
                <div className="role-icon">💼</div>
                <div className="role-name">Eu sou Secretária</div>
                <div className="role-desc">Estou cadastrando em nome do gestor e gerencio as vendas.</div>
              </div>
            </div>

            {selectedRole === 'secretary' && (
              <div style={{ marginTop: 24, display: 'flex', flexDirection: 'column', gap: 20 }}>
                <h4 className="login-section-title">Dados de Contato da Secretária</h4>
                <div className="form-group-reg">
                  <label className="form-label-reg" htmlFor="secName">Nome da Secretária</label>
                  <input
                    id="secName"
                    type="text"
                    className="form-input-reg"
                    placeholder="Seu nome"
                    value={secretaryName}
                    onChange={(e) => setSecretaryName(e.target.value)}
                    disabled={globalLoading || expired}
                  />
                </div>

                <div className="form-group-reg">
                  <label className="form-label-reg" htmlFor="secEmail">E-mail da Secretária</label>
                  <input
                    id="secEmail"
                    type="email"
                    className="form-input-reg"
                    placeholder="exemplo@secretaria.com"
                    value={secretaryEmail}
                    onChange={(e) => setSecretaryEmail(e.target.value)}
                    disabled={globalLoading || expired}
                  />
                </div>

                <div className="form-group-reg">
                  <label className="form-label-reg" htmlFor="secWhatsapp">WhatsApp da Secretária</label>
                  <input
                    id="secWhatsapp"
                    type="tel"
                    className="form-input-reg"
                    placeholder="ex. 47999999999"
                    value={secretaryWhatsapp}
                    onChange={(e) => setSecretaryWhatsapp(e.target.value)}
                    disabled={globalLoading || expired}
                  />
                </div>
              </div>
            )}

            <div className="buttons-row">
              <button
                type="button"
                className="btn-reg btn-secondary-reg"
                onClick={() => setStep(1)}
                disabled={globalLoading || expired}
              >
                Voltar
              </button>
              <button
                type="button"
                className="btn-reg btn-primary-reg"
                onClick={handleRegisterSubmit}
                disabled={globalLoading || expired}
              >
                {globalLoading ? 'Cadastrando...' : 'Finalizar Cadastro'}
              </button>
            </div>
          </div>
        )}

        {/* STEP 3: Video + Login direction */}
        {step === 3 && (
          <div className="form-section">
            <div className="success-header">
              <div className="success-badge">✓</div>
              <h2 className="reg-title" style={{ fontSize: 22 }}>Cadastro Realizado com Sucesso!</h2>
            </div>

            <p className="instruction-text">
              Bom, você agora tem um usuário. Você precisa assistir ao vídeo abaixo e ir para{' '}
              <a href="https://ademicon.hagadev.com" target="_blank" rel="noopener noreferrer">
                https://ademicon.hagadev.com
              </a>{' '}
              e realizar o login.
            </p>

            <div className="video-wrapper">
              <iframe
                src="https://www.youtube.com/embed/9F7Uvd30Tuk"
                title="Vídeo de Instrução"
                allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                allowFullScreen
              />
            </div>

            <form onSubmit={handleLoginSubmit} className="login-form-reg">
              <div>
                <h3 className="login-section-title">Faça Login Abaixo</h3>
                <p className="login-section-desc">Ou acesse a URL indicada acima.</p>
              </div>

              {loginError && <div className="alert-error">{loginError}</div>}

              <div className="form-group-reg">
                <label className="form-label-reg" htmlFor="loginEmail">E-mail</label>
                <input
                  id="loginEmail"
                  type="email"
                  className="form-input-reg"
                  value={loginEmail}
                  onChange={(e) => setLoginEmail(e.target.value)}
                  required
                  disabled={loginLoading}
                />
              </div>

              <div className="form-group-reg">
                <label className="form-label-reg" htmlFor="loginPassword">Senha</label>
                <input
                  id="loginPassword"
                  type="password"
                  className="form-input-reg"
                  value={loginPassword}
                  onChange={(e) => setLoginPassword(e.target.value)}
                  required
                  disabled={loginLoading}
                />
              </div>

              <button type="submit" className="btn-reg btn-primary-reg" disabled={loginLoading}>
                {loginLoading ? 'Entrando...' : 'Entrar na Plataforma'}
              </button>
            </form>
          </div>
        )}
      </div>
    </div>
  );
};

export default AdminRegistrationPage;
