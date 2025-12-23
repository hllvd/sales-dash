import React, { useState } from 'react';
import { Title, Button, TextInput, PasswordInput } from '@mantine/core';
import './MyProfilePage.css';
import Menu from './Menu';
import { useCurrentUser } from '../contexts/CurrentUserContext';
import { apiService } from '../services/apiService';
import { toast } from '../utils/toast';

const MyProfilePage: React.FC = () => {
  const { currentUser, refreshCurrentUser } = useCurrentUser();
  const [formData, setFormData] = useState({
    name: currentUser?.name || '',
    email: currentUser?.email || '',
    currentPassword: '',
    newPassword: '',
    confirmPassword: ''
  });
  const [loading, setLoading] = useState(false);
  const [showPasswordFields, setShowPasswordFields] = useState(false);

  const handleChange = (field: string, value: string) => {
    setFormData(prev => ({
      ...prev,
      [field]: value
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!currentUser) {
      toast.error('Usuário não encontrado');
      return;
    }

    // Validate password fields if changing password
    if (showPasswordFields) {
      if (!formData.currentPassword) {
        toast.error('Senha atual é obrigatória para alterar a senha');
        return;
      }
      if (!formData.newPassword) {
        toast.error('Nova senha é obrigatória');
        return;
      }
      if (formData.newPassword !== formData.confirmPassword) {
        toast.error('As senhas não coincidem');
        return;
      }
      if (formData.newPassword.length < 6) {
        toast.error('A nova senha deve ter pelo menos 6 caracteres');
        return;
      }
    }

    setLoading(true);

    try {
      const updateData: any = {
        name: formData.name,
        email: formData.email
      };

      // Only include password if user is changing it
      if (showPasswordFields && formData.newPassword) {
        updateData.password = formData.newPassword;
      }

      await apiService.updateUser(currentUser.id, updateData);
      
      // Refresh current user data
      await refreshCurrentUser();
      
      toast.success('Perfil atualizado com sucesso');
      
      // Clear password fields
      setFormData(prev => ({
        ...prev,
        currentPassword: '',
        newPassword: '',
        confirmPassword: ''
      }));
      setShowPasswordFields(false);
    } catch (err: any) {
      const errorMessage = err.message || 'Falha ao atualizar perfil';
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    if (currentUser) {
      setFormData({
        name: currentUser.name,
        email: currentUser.email,
        currentPassword: '',
        newPassword: '',
        confirmPassword: ''
      });
      setShowPasswordFields(false);
    }
  };

  if (!currentUser) {
    return (
      <Menu>
        <div className="my-profile-page">
          <div className="loading-container">
            <div className="spinner"></div>
            <p>Carregando perfil...</p>
          </div>
        </div>
      </Menu>
    );
  }

  return (
    <Menu>
      <div className="my-profile-page">
        <div className="my-profile-header">
          <Title order={2} size="h2" className="page-title-break">Meu Perfil</Title>
        </div>

        <div className="my-profile-content">
          <form onSubmit={handleSubmit} className="profile-form">
            <div className="form-section">
              <h3>Informações Pessoais</h3>
              
              <TextInput
                label="Nome"
                value={formData.name}
                onChange={(e) => handleChange('name', e.target.value)}
                required
                mb="md"
              />

              <TextInput
                label="Email"
                type="email"
                value={formData.email}
                onChange={(e) => handleChange('email', e.target.value)}
                required
                mb="md"
              />
            </div>

            <div className="form-section">
              <h3>Informações da Conta</h3>
              
              <div className="readonly-field">
                <label>Função</label>
                <div className="readonly-value">
                  {currentUser.role === 'superadmin' ? 'Super Admin' :
                   currentUser.role === 'admin' ? 'Admin' : 'Usuário'}
                </div>
              </div>

              {currentUser.parentUserName && (
                <div className="readonly-field">
                  <label>Usuário Pai</label>
                  <div className="readonly-value">{currentUser.parentUserName}</div>
                </div>
              )}

              <div className="readonly-field">
                <label>Status</label>
                <div className="readonly-value">
                  {currentUser.isActive ? 'Ativo' : 'Inativo'}
                </div>
              </div>

              {currentUser.activeMatriculas && currentUser.activeMatriculas.length > 0 && (
                <div className="readonly-field">
                  <label>Matrículas Ativas</label>
                  <div className="matriculas-list">
                    {currentUser.activeMatriculas.map(m => (
                      <div key={m.id} className="matricula-item">
                        <strong>{m.matriculaNumber}</strong>
                        {m.isOwner && <span className="owner-badge">Proprietário</span>}
                        <span className="matricula-dates">
                          {new Date(m.startDate).toLocaleDateString('pt-BR')}
                          {m.endDate && ` - ${new Date(m.endDate).toLocaleDateString('pt-BR')}`}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>

            <div className="form-section">
              <div className="password-section-header">
                <h3>Alterar Senha</h3>
                {!showPasswordFields && (
                  <Button
                    type="button"
                    variant="subtle"
                    onClick={() => setShowPasswordFields(true)}
                  >
                    Alterar Senha
                  </Button>
                )}
              </div>

              {showPasswordFields && (
                <>
                  <PasswordInput
                    label="Senha Atual"
                    value={formData.currentPassword}
                    onChange={(e) => handleChange('currentPassword', e.target.value)}
                    mb="md"
                  />

                  <PasswordInput
                    label="Nova Senha"
                    value={formData.newPassword}
                    onChange={(e) => handleChange('newPassword', e.target.value)}
                    mb="md"
                  />

                  <PasswordInput
                    label="Confirmar Nova Senha"
                    value={formData.confirmPassword}
                    onChange={(e) => handleChange('confirmPassword', e.target.value)}
                    mb="md"
                  />
                </>
              )}
            </div>

            <div className="form-actions">
              <Button
                type="button"
                variant="subtle"
                onClick={handleCancel}
                disabled={loading}
              >
                Cancelar
              </Button>
              <Button
                type="submit"
                loading={loading}
              >
                Salvar Alterações
              </Button>
            </div>
          </form>
        </div>
      </div>
    </Menu>
  );
};

export default MyProfilePage;
