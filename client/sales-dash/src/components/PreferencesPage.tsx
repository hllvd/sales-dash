import React, { useEffect, useState } from 'react';
import { Title, Switch } from '@mantine/core';
import Menu from './Menu';
import { apiService, UserPreferences } from '../services/apiService';
import { toast } from '../utils/toast';
import './PreferencesPage.css';

const PreferencesPage: React.FC = () => {
  const [preferences, setPreferences] = useState<UserPreferences | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [updatingKey, setUpdatingKey] = useState<string | null>(null);

  useEffect(() => {
    loadPreferences();
  }, []);

  const loadPreferences = async () => {
    try {
      setLoading(true);
      setError(null);
      const res = await apiService.getUserPreferences();
      if (res.success && res.data) {
        setPreferences(res.data);
      } else {
        const msg = res.message || 'Erro ao carregar preferências.';
        setError(msg);
        toast.error(msg);
      }
    } catch (err: any) {
      const msg = err.message || 'Erro ao comunicar com o servidor.';
      setError(msg);
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  };

  const handleToggle = async (key: keyof UserPreferences, currentValue: boolean) => {
    if (!preferences) return;
    const newValue = !currentValue;

    // Optimistic update
    setPreferences({
      ...preferences,
      [key]: newValue,
    });
    setUpdatingKey(key);

    try {
      const res = await apiService.updateUserPreferences({
        [key]: newValue,
      });

      if (res.success && res.data) {
        setPreferences(res.data);
        toast.success('Preferência atualizada com sucesso.');
      } else {
        // Rollback
        setPreferences({
          ...preferences,
          [key]: currentValue,
        });
        toast.error(res.message || 'Falha ao salvar preferência.');
      }
    } catch (err: any) {
      // Rollback
      setPreferences({
        ...preferences,
        [key]: currentValue,
      });
      toast.error(err.message || 'Falha ao comunicar com o servidor.');
    } finally {
      setUpdatingKey(null);
    }
  };

  return (
    <Menu>
      <div className="preferences-container">
        <div className="preferences-header">
          <div>
            <Title order={2} size="h2">Preferências</Title>
            <p className="preferences-subtitle">
              Gerencie suas preferências de importação, visualização e filtros no sistema.
            </p>
          </div>
        </div>

        {error && <div className="error-banner">{error}</div>}

        {loading ? (
          <div className="loading-container">
            <div className="spinner"></div>
            <p>Carregando preferências...</p>
          </div>
        ) : preferences ? (
          <div className="preferences-card">
            {/* Seção 1: Importações */}
            <div className="preferences-section">
              <h3 className="preferences-section-title">Importações</h3>
              <div className="preferences-item">
                <div className="preferences-item-info">
                  <div className="preferences-item-title">
                    Atribuir novos contratos do Ava Pro ao dono da matrícula
                  </div>
                  <div className="preferences-item-description">
                    Ao importar novos contratos originados do Ava Pro com matrícula, vincula o novo contrato
                    automaticamente ao usuário proprietário daquela matrícula no sistema.
                  </div>
                </div>
                <Switch
                  checked={preferences.autoAssignNewContractsToMatriculaOwner}
                  disabled={updatingKey === 'autoAssignNewContractsToMatriculaOwner'}
                  onChange={() =>
                    handleToggle(
                      'autoAssignNewContractsToMatriculaOwner',
                      preferences.autoAssignNewContractsToMatriculaOwner
                    )
                  }
                  size="md"
                />
              </div>
            </div>

            {/* Seção 2: Visualização e Filtros */}
            <div className="preferences-section">
              <h3 className="preferences-section-title">Visualização e Filtros</h3>

              <div className="preferences-item">
                <div className="preferences-item-info">
                  <div className="preferences-item-title">
                    Tratar cotas ativas sem pagamento como aguardando pagamento
                  </div>
                  <div className="preferences-item-description">
                    Ajusta a exibição de contratos com status ativo que ainda não possuem registros
                    de parcelas pagas para &apos;Aguardando Pagamento&apos;.
                  </div>
                </div>
                <Switch
                  checked={preferences.treatUnpaidActiveAsAwaitingPayment}
                  disabled={updatingKey === 'treatUnpaidActiveAsAwaitingPayment'}
                  onChange={() =>
                    handleToggle(
                      'treatUnpaidActiveAsAwaitingPayment',
                      preferences.treatUnpaidActiveAsAwaitingPayment
                    )
                  }
                  size="md"
                />
              </div>

              <div className="preferences-item">
                <div className="preferences-item-info">
                  <div className="preferences-item-title">
                    Incluir usuários inativos nos filtros
                  </div>
                  <div className="preferences-item-description">
                    Permite selecionar e visualizar usuários inativos nas listagens, relatórios e filtros
                    de contratos.
                  </div>
                </div>
                <Switch
                  checked={preferences.includeInactiveUsersInFilter}
                  disabled={updatingKey === 'includeInactiveUsersInFilter'}
                  onChange={() =>
                    handleToggle(
                      'includeInactiveUsersInFilter',
                      preferences.includeInactiveUsersInFilter
                    )
                  }
                  size="md"
                />
              </div>
            </div>
          </div>
        ) : (
          <div className="preferences-card" style={{ textAlign: 'center', color: '#666', padding: '40px' }}>
            <p>Não foi possível carregar as preferências.</p>
          </div>
        )}
      </div>
    </Menu>
  );
};

export default PreferencesPage;
