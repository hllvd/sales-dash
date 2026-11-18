import React, { useEffect, useState } from 'react';
import {
  Title,
  Text,
  Paper,
  Stack,
  Group,
  Switch,
  Loader,
  Center,
  ThemeIcon,
  Divider,
} from '@mantine/core';
import {
  IconAdjustments,
  IconFileImport,
  IconFilter,
  IconUsers,
} from '@tabler/icons-react';
import Menu from './Menu';
import { apiService, UserPreferences } from '../services/apiService';
import { toast } from '../utils/toast';
import './PreferencesPage.css';

const PreferencesPage: React.FC = () => {
  const [preferences, setPreferences] = useState<UserPreferences | null>(null);
  const [loading, setLoading] = useState(true);
  const [updatingKey, setUpdatingKey] = useState<string | null>(null);

  useEffect(() => {
    loadPreferences();
  }, []);

  const loadPreferences = async () => {
    try {
      setLoading(true);
      const res = await apiService.getUserPreferences();
      if (res.success && res.data) {
        setPreferences(res.data);
      } else {
        toast.error(res.message || 'Erro ao carregar preferências.');
      }
    } catch (err: any) {
      toast.error(err.message || 'Erro ao comunicar com o servidor.');
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
        <Stack gap="lg">
          <div className="preferences-header">
            <Group gap="xs" mb={4}>
              <ThemeIcon size="lg" radius="md" color="red" variant="light">
                <IconAdjustments size={22} />
              </ThemeIcon>
              <Title order={2} size="h2" className="preferences-title">
                Preferências
              </Title>
            </Group>
            <Text className="preferences-subtitle">
              Gerencie suas preferências de importação, visualização e filtros no sistema.
            </Text>
          </div>

          <Divider color="#e9ecef" />

          {loading ? (
            <Center py={60}>
              <Loader color="red" size="md" />
            </Center>
          ) : preferences ? (
            <Stack gap="lg">
              {/* Seção 1: Importações do Ava Pro */}
              <div>
                <Text className="preferences-section-label">Importações</Text>
                <Paper className="preferences-card" radius="md">
                  <Group justify="space-between" align="flex-start" wrap="nowrap">
                    <Group align="flex-start" gap="md" wrap="nowrap">
                      <ThemeIcon size="lg" radius="md" color="blue" variant="light" mt={2}>
                        <IconFileImport size={20} />
                      </ThemeIcon>
                      <div>
                        <Text className="preferences-item-title">
                          Atribuir novos contratos do Ava Pro ao dono da matrícula
                        </Text>
                        <Text className="preferences-item-description">
                          Ao importar novos contratos originados do Ava Pro com matrícula, vincula o novo contrato
                          automaticamente ao usuário proprietário daquela matrícula no sistema.
                        </Text>
                      </div>
                    </Group>
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
                      color="red"
                    />
                  </Group>
                </Paper>
              </div>

              {/* Seção 2: Visualização e Filtros */}
              <div>
                <Text className="preferences-section-label">Visualização e Filtros</Text>
                <Paper className="preferences-card" radius="md">
                  <Stack gap="lg">
                    <Group justify="space-between" align="flex-start" wrap="nowrap">
                      <Group align="flex-start" gap="md" wrap="nowrap">
                        <ThemeIcon size="lg" radius="md" color="indigo" variant="light" mt={2}>
                          <IconFilter size={20} />
                        </ThemeIcon>
                        <div>
                          <Text className="preferences-item-title">
                            Tratar cotas ativas sem pagamento como aguardando pagamento
                          </Text>
                          <Text className="preferences-item-description">
                            Ajusta a exibição de contratos com status ativo que ainda não possuem registros
                            de parcelas pagas para &apos;Aguardando Pagamento&apos;.
                          </Text>
                        </div>
                      </Group>
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
                        color="red"
                      />
                    </Group>

                    <Divider className="preferences-divider" />

                    <Group justify="space-between" align="flex-start" wrap="nowrap">
                      <Group align="flex-start" gap="md" wrap="nowrap">
                        <ThemeIcon size="lg" radius="md" color="orange" variant="light" mt={2}>
                          <IconUsers size={20} />
                        </ThemeIcon>
                        <div>
                          <Text className="preferences-item-title">
                            Incluir usuários inativos nos filtros
                          </Text>
                          <Text className="preferences-item-description">
                            Permite selecionar e visualizar usuários inativos nas listagens, relatórios e filtros
                            de contratos.
                          </Text>
                        </div>
                      </Group>
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
                        color="red"
                      />
                    </Group>
                  </Stack>
                </Paper>
              </div>
            </Stack>
          ) : (
            <Center py={40}>
              <Text c="dimmed">Não foi possível carregar as preferências.</Text>
            </Center>
          )}
        </Stack>
      </div>
    </Menu>
  );
};

export default PreferencesPage;
