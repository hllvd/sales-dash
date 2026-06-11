import React, { useState, useEffect, useRef } from 'react';
import { Title, Button, TextInput, PasswordInput, Modal, Loader, Badge, Paper, Grid, Text, Card, Accordion } from '@mantine/core';
import { IconUser, IconMail, IconPencil, IconCheck, IconX, IconLock, IconCalendar, IconChevronDown, IconChevronUp } from '@tabler/icons-react';
import { useCurrentUser } from '../../contexts/CurrentUserContext';
import { apiService, User, UserClassification, UserStats } from '../../services/apiService';
import { UserMetadataSection } from './UserMetadataSection';
import { toast } from '../../utils/toast';
import { validatePassword } from '../../utils/validators';
import './UserProfile.css';

export interface UserProfileProps {
  userId: string;
  mode: 'page' | 'modal';
  onClose?: () => void;
}

export const UserProfile: React.FC<UserProfileProps> = ({ userId, mode, onClose }) => {
  const { currentUser: loggedInUser, refreshCurrentUser: refreshLoggedInUser } = useCurrentUser();
  
  // States
  const [user, setUser] = useState<User | null>(null);
  const [classifications, setClassifications] = useState<UserClassification[]>([]);
  const [stats, setStats] = useState<UserStats | null>(null);
  const [statsLoading, setStatsLoading] = useState(false);
  const [statsError, setStatsError] = useState(false);
  const [statsVisible, setStatsVisible] = useState(false);
  
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [isEditing, setIsEditing] = useState(false);
  
  // Form state
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  
  // Password change state
  const [showPasswordFields, setShowPasswordFields] = useState(false);
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  
  // Matricula state
  const [showAddMatriculaModal, setShowAddMatriculaModal] = useState(false);
  const [newMatriculaNumber, setNewMatriculaNumber] = useState('');
  const [submittingMatricula, setSubmittingMatricula] = useState(false);

  // Metadata changes state
  const [pendingMetadataChanges, setPendingMetadataChanges] = useState<Record<number, string>>({});

  const statsRef = useRef<HTMLDivElement>(null);

  // Can edit logic: currently logged-in user editing their own profile OR an admin/superadmin editing
  const canEdit = loggedInUser?.id === userId || 
                  loggedInUser?.role === 'admin' || 
                  loggedInUser?.role === 'superadmin';

  // Load basic details and classification history
  const loadProfileData = async () => {
    setLoading(true);
    try {
      const [userResponse, historyResponse] = await Promise.all([
        apiService.getUser(userId),
        apiService.getUserClassificationHistory(userId)
      ]);
      
      if (userResponse.success && userResponse.data) {
        setUser(userResponse.data);
        setName(userResponse.data.name);
        setEmail(userResponse.data.email);
        setPendingMetadataChanges({});
      }
      
      if (historyResponse.success && historyResponse.data) {
        setClassifications(historyResponse.data);
      }
    } catch (err: any) {
      toast.error(err.message || 'Falha ao carregar dados do perfil');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadProfileData();
    // Reset stats intersection trigger when userId changes
    setStatsVisible(false);
    setStats(null);
    setStatsError(false);
  }, [userId]);

  // IntersectionObserver for lazy-loading stats
  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) {
          setStatsVisible(true);
        }
      },
      { threshold: 0.1 }
    );

    const currentRef = statsRef.current;
    if (currentRef) {
      observer.observe(currentRef);
    }

    return () => {
      if (currentRef) {
        observer.unobserve(currentRef);
      }
    };
  }, [userId, loading]);

  // Load stats once visible
  useEffect(() => {
    if (!statsVisible || loading || stats || statsLoading) return;
    
    const loadStats = async () => {
      setStatsLoading(true);
      setStatsError(false);
      try {
        const response = await apiService.getUserStats(userId);
        if (response.success && response.data) {
          setStats(response.data);
        } else {
          setStatsError(true);
        }
      } catch (err: any) {
        console.error('Failed to load user stats:', err);
        setStatsError(true);
      } finally {
        setStatsLoading(false);
      }
    };

    loadStats();
  }, [statsVisible, userId, loading]);

  const handleSaveProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!user) return;

    if (!name.trim() || !email.trim()) {
      toast.error('Nome e Email são obrigatórios');
      return;
    }

    // Password validations if requested
    if (showPasswordFields) {
      if (!currentPassword && loggedInUser?.id === userId) {
        toast.error('Senha atual é obrigatória para alterar a senha');
        return;
      }
      if (!newPassword) {
        toast.error('Nova senha é obrigatória');
        return;
      }
      if (newPassword !== confirmPassword) {
        toast.error('As novas senhas não coincidem');
        return;
      }
      const validation = validatePassword(newPassword);
      if (!validation.isValid) {
        toast.error(validation.message);
        return;
      }
    }

    setSaving(true);
    try {
      // 1. Save metadata changes first
      const metadataPayload = Object.entries(pendingMetadataChanges).map(([id, val]) => ({
        fieldId: Number(id),
        value: val,
      }));
      if (metadataPayload.length > 0) {
        await apiService.upsertUserMetadataValues(userId, metadataPayload);
      }

      // 2. Save core profile details
      const updateData: any = { name, email };
      if (showPasswordFields && newPassword) {
        updateData.password = newPassword;
      }

      const response = await apiService.updateUser(userId, updateData);
      if (response.success) {
        toast.success('Perfil atualizado com sucesso');
        setIsEditing(false);
        setShowPasswordFields(false);
        setCurrentPassword('');
        setNewPassword('');
        setConfirmPassword('');
        setPendingMetadataChanges({});
        
        // Refresh local details
        await loadProfileData();
        
        // If updating own profile, sync with global context
        if (loggedInUser?.id === userId) {
          await refreshLoggedInUser();
        }
      }
    } catch (err: any) {
      toast.error(err.message || 'Erro ao atualizar perfil');
    } finally {
      setSaving(false);
    }
  };

  const handleRequestMatricula = async () => {
    if (!newMatriculaNumber.trim()) {
      toast.error('Número da matrícula é obrigatório');
      return;
    }

    setSubmittingMatricula(true);
    try {
      await apiService.requestMatricula(newMatriculaNumber);
      toast.success('Matrícula solicitada com sucesso! Aguardando aprovação.');
      setShowAddMatriculaModal(false);
      setNewMatriculaNumber('');
      
      // Refresh local user details
      await loadProfileData();
      
      if (loggedInUser?.id === userId) {
        await refreshLoggedInUser();
      }
    } catch (err: any) {
      toast.error(err.message || 'Falha ao solicitar matrícula');
    } finally {
      setSubmittingMatricula(false);
    }
  };

  // Helper for rendering initials avatar
  const getInitials = (nameStr: string) => {
    if (!nameStr) return 'U';
    const parts = nameStr.trim().split(/\s+/);
    if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
    return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
  };

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(val);
  };

  const formatPercent = (val: number) => {
    return `${val.toFixed(1)}%`;
  };

  if (loading) {
    return (
      <div className="profile-loading">
        <Loader size="lg" />
        <Text mt="md" size="sm" c="dimmed">Carregando informações do perfil...</Text>
      </div>
    );
  }

  if (!user) {
    return (
      <Paper p="xl" withBorder className="profile-error-card">
        <Text c="red" ta="center" fw={500}>Usuário não encontrado.</Text>
      </Paper>
    );
  }

  // Get active classification level
  const currentLevel = user.currentLevelName || 'Nenhum';

  return (
    <div className="user-profile-container">
      {/* HEADER SECTION */}
      <Paper p="xl" radius="md" withBorder className="profile-header-card">
        <div className="profile-header-layout">
          <div className="profile-avatar-circle">
            {getInitials(user.name)}
          </div>
          
          <div className="profile-header-info">
            <div className="profile-name-row">
              <Title order={2} className="profile-title">{user.name}</Title>
              <div className="profile-badges">
                <Badge 
                  color={user.role === 'superadmin' ? 'red' : user.role === 'admin' ? 'blue' : 'gray'}
                  variant="light"
                  size="md"
                >
                  {user.role === 'superadmin' ? 'Super Admin' : user.role === 'admin' ? 'Admin' : 'Consultor'}
                </Badge>
                <Badge 
                  color={user.isActive ? 'green' : 'red'} 
                  variant="light" 
                  size="md"
                >
                  {user.isActive ? 'Ativo' : 'Inativo'}
                </Badge>
              </div>
            </div>
            
            <Text c="dimmed" size="sm" className="profile-email-row">
              <IconMail size={16} className="text-icon" /> {user.email}
            </Text>
            
            <div className="profile-meta-grid">
              {user.parentUserName && (
                <div className="profile-meta-item">
                  <span className="meta-label">Supervisor / Indicador:</span>
                  <span className="meta-value">{user.parentUserName}</span>
                </div>
              )}
              <div className="profile-meta-item">
                <span className="meta-label">Classificação Atual:</span>
                <span className="meta-value active-level-text">{currentLevel}</span>
              </div>
              <div className="profile-meta-item">
                <span className="meta-label">Equipe:</span>
                <span className="meta-value">
                  {user.currentTeamName ? (
                    <Badge color="indigo" variant="outline" size="sm">
                      {user.currentTeamName}
                    </Badge>
                  ) : (
                    <span className="muted-text">Sem equipe</span>
                  )}
                </span>
              </div>
            </div>
          </div>
          
          {canEdit && !isEditing && (
            <Button 
              variant="outline" 
              leftSection={<IconPencil size={16} />} 
              onClick={() => setIsEditing(true)}
              className="profile-edit-btn"
            >
              Editar Perfil
            </Button>
          )}
        </div>
      </Paper>

      {/* EDIT FORM CONTAINER */}
      {isEditing && (
        <Paper p="xl" radius="md" withBorder className="profile-edit-paper" mt="lg">
          <Title order={3} mb="lg" style={{ color: '#1c1c1e', fontWeight: 600 }}>
            Editar Informações
          </Title>
          <form onSubmit={handleSaveProfile}>
            <Grid>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <TextInput
                  label="Nome Completo"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  required
                  leftSection={<IconUser size={16} />}
                />
              </Grid.Col>
              <Grid.Col span={{ base: 12, md: 6 }}>
                <TextInput
                  label="Endereço de Email"
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  required
                  leftSection={<IconMail size={16} />}
                />
              </Grid.Col>
            </Grid>

            {/* Password Section */}
            <div className="edit-password-section" style={{ marginTop: '20px' }}>
              <div className="password-toggle-row">
                <Text size="sm" fw={500}>Deseja alterar a senha?</Text>
                <Button 
                  type="button" 
                  variant="subtle" 
                  size="xs"
                  onClick={() => setShowPasswordFields(!showPasswordFields)}
                >
                  {showPasswordFields ? 'Cancelar Alteração' : 'Alterar Senha'}
                </Button>
              </div>

              {showPasswordFields && (
                <Grid mt="xs">
                  {loggedInUser?.id === userId && (
                    <Grid.Col span={12}>
                      <PasswordInput
                        label="Senha Atual"
                        placeholder="Digite sua senha atual"
                        value={currentPassword}
                        onChange={(e) => setCurrentPassword(e.target.value)}
                        required
                        leftSection={<IconLock size={16} />}
                      />
                    </Grid.Col>
                  )}
                  <Grid.Col span={{ base: 12, md: 6 }}>
                    <PasswordInput
                      label="Nova Senha"
                      placeholder="Digite a nova senha"
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      required
                      leftSection={<IconLock size={16} />}
                    />
                  </Grid.Col>
                  <Grid.Col span={{ base: 12, md: 6 }}>
                    <PasswordInput
                      label="Confirmar Nova Senha"
                      placeholder="Confirme a nova senha"
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      required
                      leftSection={<IconLock size={16} />}
                    />
                  </Grid.Col>
                </Grid>
              )}
            </div>

            {user?.metadataGroups && user.metadataGroups.length > 0 && (
              <UserMetadataSection
                groups={user.metadataGroups}
                isEditing={isEditing}
                values={pendingMetadataChanges}
                onChange={(fieldId, val) => {
                  setPendingMetadataChanges(prev => ({ ...prev, [fieldId]: val }));
                }}
                canEdit={canEdit}
              />
            )}

            <div className="edit-actions" style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end', marginTop: '24px' }}>
              <Button 
                variant="subtle" 
                onClick={() => {
                  setIsEditing(false);
                  setShowPasswordFields(false);
                  setName(user.name);
                  setEmail(user.email);
                  setPendingMetadataChanges({});
                }}
                disabled={saving}
              >
                Cancelar
              </Button>
              <Button 
                type="submit" 
                loading={saving}
                leftSection={<IconCheck size={16} />}
              >
                Salvar Alterações
              </Button>
            </div>
          </form>
        </Paper>
      )}

      {/* MID SECTION - MATRICULAS & CLASSIFICATIONS */}
      <Grid mt="lg">
        {/* MATRICULAS */}
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Paper p="xl" radius="md" withBorder className="profile-section-card h-100">
            <div className="section-header-row">
              <Title order={3} className="section-title">Matrículas</Title>
              {canEdit && (
                <Button 
                  size="xs" 
                  variant="outline" 
                  onClick={() => setShowAddMatriculaModal(true)}
                >
                  Solicitar Matrícula
                </Button>
              )}
            </div>

            {user.activeMatriculas && user.activeMatriculas.length > 0 ? (
              <div className="matriculas-table-container">
                <table className="profile-data-table matriculas-table">
                  <thead>
                    <tr>
                      <th>Matrícula</th>
                      <th>Status</th>
                      <th>Titular</th>
                      <th>Início</th>
                    </tr>
                  </thead>
                  <tbody>
                    {user.activeMatriculas.map((m) => (
                      <tr key={m.id}>
                        <td><strong>{m.matriculaNumber}</strong></td>
                        <td>
                          <Badge 
                            color={m.status === 'active' ? 'green' : 'orange'} 
                            variant="light" 
                            size="xs"
                          >
                            {m.status === 'active' ? 'Ativa' : 'Pendente'}
                          </Badge>
                        </td>
                        <td>
                          {m.isOwner ? <Badge color="teal" size="xs">Sim</Badge> : <Text size="xs" c="dimmed">Não</Text>}
                        </td>
                        <td>{new Date(m.startDate).toLocaleDateString('pt-BR')}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="empty-state-container">
                <Text size="sm" c="dimmed" style={{ fontStyle: 'italic' }}>Nenhuma matrícula vinculada a este perfil.</Text>
              </div>
            )}
          </Paper>
        </Grid.Col>

        {/* CLASSIFICATIONS */}
        <Grid.Col span={{ base: 12, md: 6 }}>
          <Paper p="xl" radius="md" withBorder className="profile-section-card h-100">
            <Title order={3} className="section-title" mb="md">Classificação do Vendedor</Title>
            
            {/* Highlighted current level */}
            <Card withBorder radius="md" p="md" className="current-level-banner" mb="md">
              <div className="level-banner-layout">
                <span className="level-banner-icon">🏆</span>
                <div>
                  <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Nível Atual</Text>
                  <Text size="lg" fw={700} c="indigo">{currentLevel}</Text>
                </div>
              </div>
            </Card>

            {classifications.length > 0 ? (
              <Accordion variant="separated" chevron={<IconChevronDown size={16} />}>
                <Accordion.Item value="history" className="history-accordion-item">
                  <Accordion.Control>
                    <Text size="sm" fw={600} c="dimmed">Ver histórico completo</Text>
                  </Accordion.Control>
                  <Accordion.Panel>
                    <div className="classifications-table-container">
                      <table className="profile-data-table">
                        <thead>
                          <tr>
                            <th>Nível</th>
                            <th>Início</th>
                            <th>Fim</th>
                          </tr>
                        </thead>
                        <tbody>
                          {classifications.map((c) => (
                            <tr key={c.id}>
                              <td><strong color="indigo">{c.levelName}</strong></td>
                              <td>{new Date(c.startDate).toLocaleDateString('pt-BR')}</td>
                              <td>
                                {c.endDate ? new Date(c.endDate).toLocaleDateString('pt-BR') : (
                                  <Badge color="green" variant="light" size="xs">Atual</Badge>
                                )}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </Accordion.Panel>
                </Accordion.Item>
              </Accordion>
            ) : (
              <div className="empty-state-container">
                <Text size="sm" c="dimmed" style={{ fontStyle: 'italic' }}>Nenhum histórico de classificação encontrado.</Text>
              </div>
            )}
          </Paper>
        </Grid.Col>
      </Grid>

      {/* VIEW-MODE ADDITIONAL USER METADATA SECTION */}
      {!isEditing && user?.metadataGroups && user.metadataGroups.length > 0 && (
        <UserMetadataSection
          groups={user.metadataGroups}
          isEditing={isEditing}
          values={pendingMetadataChanges}
          onChange={(fieldId, val) => {
            setPendingMetadataChanges(prev => ({ ...prev, [fieldId]: val }));
          }}
          canEdit={canEdit}
        />
      )}

      {/* BOTTOM SECTION - LAZY-LOADED STATS */}
      <div ref={statsRef} className="profile-stats-section" style={{ marginTop: '30px' }}>
        <Title order={3} className="section-title" mb="md">Indicadores de Produção</Title>
        
        {statsLoading && (
          <Grid>
            {[1, 2, 3, 4].map((i) => (
              <Grid.Col span={{ base: 12, sm: 6, md: 3 }} key={i}>
                <Paper p="xl" radius="md" withBorder className="stat-card skeleton-pulse">
                  <div className="skeleton-line title"></div>
                  <div className="skeleton-line value"></div>
                </Paper>
              </Grid.Col>
            ))}
          </Grid>
        )}

        {statsError && !statsLoading && (
          <Paper p="xl" radius="md" withBorder className="stats-error-banner">
            <Text c="red" size="sm" ta="center" mb="sm">Não foi possível carregar os indicadores de produção.</Text>
            <div style={{ display: 'flex', justifyContent: 'center' }}>
              <Button size="xs" variant="outline" onClick={() => setStats(null)}>Tentar Novamente</Button>
            </div>
          </Paper>
        )}

        {stats && !statsLoading && (
          <Grid>
            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <Paper p="xl" radius="md" withBorder className="stat-card">
                <Text size="xs" c="dimmed" tt="uppercase" fw={700} mb="xs">
                  Contratos Pendentes
                </Text>
                <Title order={1} className="stat-value pending-contracts">
                  {stats.pendingContractsCount}
                </Title>
                <Text size="xs" c="dimmed" mt="xs">
                  Aguardando validação ou envio.
                </Text>
              </Paper>
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <Paper p="xl" radius="md" withBorder className="stat-card">
                <Text size="xs" c="dimmed" tt="uppercase" fw={700} mb="xs">
                  Produção Acumulada
                </Text>
                <Title order={1} className="stat-value total-production">
                  {formatCurrency(stats.totalProduction)}
                </Title>
                <Text size="xs" c="dimmed" mt="xs">
                  Soma total de todos os contratos ativos.
                </Text>
              </Paper>
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <Paper p="xl" radius="md" withBorder className="stat-card">
                <Text size="xs" c="dimmed" tt="uppercase" fw={700} mb="xs">
                  Retenção (Cancelamentos)
                </Text>
                <Title order={1} className="stat-value total-retention">
                  {formatPercent(stats.totalRetention)}
                </Title>
                <Text size="xs" c="dimmed" mt="xs">
                  Percentual excluindo apenas contratos cancelados.
                </Text>
              </Paper>
            </Grid.Col>

            <Grid.Col span={{ base: 12, sm: 6, md: 3 }}>
              <Paper p="xl" radius="md" withBorder className="stat-card">
                <Text size="xs" c="dimmed" tt="uppercase" fw={700} mb="xs">
                  Retenção Estrita (Atrasos + Cancelamentos)
                </Text>
                <Title order={1} className="stat-value total-retention" style={{ color: '#ef4444' }}>
                  {formatPercent(stats.strictRetention || 0)}
                </Title>
                <Text size="xs" c="dimmed" mt="xs">
                  Sem atrasos nem cancelamentos.
                </Text>
              </Paper>
            </Grid.Col>
          </Grid>
        )}

        {!statsVisible && !stats && !statsLoading && (
          <Grid>
            {[1, 2, 3, 4].map((i) => (
              <Grid.Col span={{ base: 12, sm: 6, md: 3 }} key={i}>
                <Paper p="xl" radius="md" withBorder className="stat-card skeleton-pulse">
                  <div className="skeleton-line title"></div>
                  <div className="skeleton-line value"></div>
                </Paper>
              </Grid.Col>
            ))}
          </Grid>
        )}
      </div>

      {/* ADICIONAR MATRICULA MODAL */}
      <Modal
        opened={showAddMatriculaModal}
        onClose={() => {
          setShowAddMatriculaModal(false);
          setNewMatriculaNumber('');
        }}
        title={<Title order={3} style={{ color: '#1c1c1e', fontWeight: 700 }}>Solicitar Nova Matrícula</Title>}
        centered
      >
        <TextInput
          label="Número da Matrícula"
          placeholder="Digite o número da matrícula"
          value={newMatriculaNumber}
          onChange={(e) => setNewMatriculaNumber(e.target.value)}
          mb="md"
          required
        />
        <div style={{ display: 'flex', gap: '10px', justifyContent: 'flex-end' }}>
          <Button
            variant="subtle"
            onClick={() => {
              setShowAddMatriculaModal(false);
              setNewMatriculaNumber('');
            }}
            disabled={submittingMatricula}
          >
            Cancelar
          </Button>
          <Button
            onClick={handleRequestMatricula}
            loading={submittingMatricula}
          >
            Solicitar
          </Button>
        </div>
      </Modal>
    </div>
  );
};
