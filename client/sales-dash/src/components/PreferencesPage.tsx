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
  Container,
} from '@mantine/core';
import {
  IconAdjustments,
  IconFileImport,
  IconFilter,
} from '@tabler/icons-react';
import Menu from './Menu';
import { apiService, UserPreferences } from '../services/apiService';
import { toast } from '../utils/toast';

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
      <Container size="md" py="xl">
        <Stack gap="lg">
          <div>
            <Group gap="xs" mb={4}>
              <ThemeIcon size="lg" radius="md" color="red" variant="light">
                <IconAdjustments size={22} />
              </ThemeIcon>
              <Title order={2} size="h2" c="white">
                Preferências
              </Title>
            </Group>
            <Text c="dimmed" size="sm">
              Gerencie suas preferências de importação, visualização e filtros no sistema.
            </Text>
          </div>

          <Divider color="#374151" />

          {loading ? (
            <Center py={60}>
              <Loader color="red" size="md" />
            </Center>
          ) : preferences ? (
            <Stack gap="md">
              {/* Seção 1: Importações do Ava Pro */}
              <Paper p="lg" radius="md" style={{ backgroundColor: '#1f2937', border: '1px solid #374151' }}>
                <Group justify="space-between" align="flex-start" wrap="nowrap">
                  <Group align="flex-start" gap="md" wrap="nowrap">
                    <ThemeIcon size="lg" radius="md" color="blue" variant="light" mt={2}>
                      <IconFileImport size={20} />
                    </ThemeIcon>
                    <div>
                      <Text fw={600} size="md" c="white">
                        Atribuir novos contratos do Ava Pro ao dono da matrícula
                      </Text>
                      <Text size="sm" c="dimmed" mt={4}>
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
                    color="teal"
                  />
                </Group>
              </Paper>

              {/* Seção 2: Visualização e Filtros */}
              <Paper p="lg" radius="md" style={{ backgroundColor: '#1f2937', border: '1px solid #374151' }}>
                <Stack gap="lg">
                  <Group justify="space-between" align="flex-start" wrap="nowrap">
                    <Group align="flex-start" gap="md" wrap="nowrap">
                      <ThemeIcon size="lg" radius="md" color="violet" variant="light" mt={2}>
                        <IconFilter size={20} />
                      </ThemeIcon>
                      <div>
                        <Text fw={600} size="md" c="white">
                          Tratar cotas ativas sem pagamento como aguardando pagamento
                        </Text>
                        <Text size="sm" c="dimmed" mt={4}>
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
                      color="teal"
                    />
                  </Group>

                  <Divider color="#374151" />

                  <Group justify="space-between" align="flex-start" wrap="nowrap">
                    <Group align="flex-start" gap="md" wrap="nowrap">
                      <ThemeIcon size="lg" radius="md" color="gray" variant="light" mt={2}>
                        <IconFilter size={20} />
                      </ThemeIcon>
                      <div>
                        <Text fw={600} size="md" c="white">
                          Incluir usuários inativos nos filtros
                        </Text>
                        <Text size="sm" c="dimmed" mt={4}>
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
                      color="teal"
                    />
                  </Group>
                </Stack>
              </Paper>
            </Stack>
          ) : (
            <Center py={40}>
              <Text c="dimmed">Não foi possível carregar as preferências.</Text>
            </Center>
          )}
        </Stack>
      </Container>
    </Menu>
  );
};

export default PreferencesPage;
