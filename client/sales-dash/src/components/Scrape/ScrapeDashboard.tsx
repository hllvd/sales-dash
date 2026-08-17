import React, { useState, useEffect } from 'react';
import {
  Title,
  Paper,
  Table,
  Badge,
  Text,
  Group,
  Button,
  Select,
  TextInput,
  Loader,
  LoadingOverlay,
  Card,
  ActionIcon,
  Tooltip,
  Alert,
  Modal,
  Stack,
  PasswordInput,
  Checkbox,
  Tabs,
  Divider,
  Code,
  Box,
} from '@mantine/core';
import { 
  IconRefresh, 
  IconSettings, 
  IconPlayerPlay, 
  IconAlertCircle, 
  IconCheck, 
  IconPlus, 
  IconTrash, 
  IconFingerprint,
  IconHistory,
  IconUserCheck,
  IconListDetails
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { scrapeService, ScrapeConfig, ScrapeRunSummary } from '../../services/scrapeService';
import Menu from '../Menu';
import './ScrapeDashboard.css';

const STORES = [
    'AHU - PR', 'ALMIRANTE TAMANDARE - PR', 'ALPHAVILLE BARUERI - SP', 'ALPHAVILLE I - BA',
    'ALTO DA XV - PR', 'ANAPOLIS - GO', 'ARAUCARIA - PR', 'ARIQUEMES - RO',
    'ASA NORTE - DF', 'ASA SUL - DF', 'ATIBAIA - SP', 'BALNEARIO CAMBORIU - SC',
    'BARRA DA TIJUCA - RJ', 'BARRA DO GARCAS - MT', 'BAURU - SP', 'BELEM - PA',
    'BELO HORIZONTE - MG', 'BETIM - MG', 'BIGORRILHO - PR', 'BLUMENAU CENTRO - SC',
    'BLUMENAU VILA NOVA - SC', 'BOA VISTA - RR', 'BOA VISTA CURITIBA - PR', 'BOTUCATU - SP',
    'BRASILIA ASA NORTE - DF', 'BRASILIA ASA SUL - DF', 'CABRAL - PR', 'CAMAQUÃ - RS',
    'CAMBORIU - SC', 'CAMPO GRANDE - MS', 'CAMPO LARGO - PR', 'CAMPO MOURAO - PR',
    'CAMPOS DOS GOYTACAZES - RJ', 'CANOAS - RS', 'CASCAVEL - PR', 'CASTELO - MG',
    'CAUE - ES', 'CAXIAS DO SUL - RS', 'CENTRO CURITIBA - PR', 'CENTRO FLORIANOPOLIS - SC',
    'CENTRO JOINVILLE - SC', 'CIDADE INDUSTRIAL DE CURITIBA - PR', 'COLOMBO - PR', 'CONCORDIA - SC',
    'CONTAGEM - MG', 'CRICIUMA - SC', 'CUIABA - MT', 'CURITIBA AHU - PR',
    'CURITIBA ALTO DA XV - PR', 'CURITIBA BIGORRILHO - PR', 'CURITIBA BOA VISTA - PR', 'CURITIBA CABRAL - PR',
    'CURITIBA CENTRO - PR', 'CURITIBA CIDADE INDUSTRIAL - PR', 'CURITIBA FAZENDINHA - PR', 'CURITIBA MERCES - PR',
    'CURITIBA NOVO MUNDO - PR', 'CURITIBA PAROLIN - PR', 'CURITIBA PORTAO - PR', 'CURITIBA SANTA FELICIDADE - PR',
    'CURITIBA SEMINARIO - PR', 'CURITIBA TARUMA - PR', 'CURITIBA XAXIM - PR',
    'CWB - AGUA VERDE - PR', 'CWB - CENTRO - PR', 'CWB - ESTACAO - PR', 'CWB - FAZENDINHA - PR',
    'CWB - PINHEIRINHO - PR', 'CWB - UBERABA - PR', 'CWB - XAXIM - PR', 'DIADEMA - SP',
    'DIVINOPOLIS - MG', 'DOURADOS - MS', 'ERECHIM - RS', 'ESTRONDO - RS',
    'FAZENDINHA - PR', 'FEIRA DE SANTANA - BA', 'FLORIANOPOLIS CENTRO - SC', 'FLORIANOPOLIS TRINDADE - SC',
    'FORTALEZA - CE', 'FOZ DO IGUACU - PR', 'FRANCISCO BELTRAO - PR', 'GAMA - DF',
    'GOIÂNIA - GO', 'GOIANIA BUENO - GO', 'GOIANIA MARISTA - GO', 'GRAVATAI - RS',
    'GUARAPUAVA - PR', 'GUARULHOS - SP', 'INDAIATUBA - SP', 'IRATI - PR',
    'ITAJAI - SC', 'ITARARE - SP', 'JABOATAO DOS GUARARAPES - PE', 'JARAGUA DO SUL - SC',
    'JOAO PESSOA - PB', 'JOINVILLE CENTRO - SC', 'JOINVILLE PIRABEIRABA - SC', 'JOINVILLE SUCUPIRA - SC',
    'JUIZ DE FORA - MG', 'JUNDIAI - SP', 'LAGO SUL - DF', 'LAGES - SC',
    'LARANJEIRAS DO SUL - PR', 'LONDRINA - PR', 'MACEIO - AL', 'MANAUS - AM',
    'MARINGA - PR', 'MEDIANEIRA - PR', 'MERCES - PR', 'NATAL - RN',
    'NITEROI - RJ', 'NOVA FRIBURGO - RJ', 'NOVA IGUACU - RJ', 'NOVA LIMA - MG',
    'NOVO HAMBURGO - RS', 'NOVO MUNDO - PR', 'OSASCO - SP', 'PALHOÇA - SC',
    'PALMAS - TO', 'PARANAGUA - PR', 'PAROLIN - PR', 'PASSO FUNDO - RS',
    'PATO BRANCO - PR', 'PELOTAS - RS', 'PETROPOLIS - RJ', 'PINHAIS - PR',
    'PIRABEIRABA - SC', 'PIRACICABA - SP', 'PONTA GROSSA - PR', 'PONTES E LACERDA - MT',
    'PORTAO - PR', 'PORTO ALEGRE - RS', 'PORTO VELHO - RO', 'POUSO ALEGRE - MG',
    'PRUDENTOPOLIS - PR', 'RECIFE - PE', 'RIBEIRAO PRETO - SP', 'RIO BRANCO - AC',
    'RIO DE JANEIRO - RJ', 'RIO DO SUL - SC', 'ROLANDIA - PR', 'RONDONOPOLIS - MT',
    'SALVADOR - BA', 'SANTA CRUZ DO SUL - RS', 'SANTA FELICIDADE - PR', 'SANTA MARIA - RS',
    'SANTA ROSA - RS', 'SANTO ANDRE - SP', 'SAO BERNARDO DO CAMPO - SP', 'SAO CAETANO DO SUL - SP',
    'SAO JOSE - SC', 'SAO JOSE DO RIO PRETO - SP', 'SAO JOSE DOS CAMPOS - SP', 'SAO JOSE DOS PINHAIS - PR',
    'SAO LEOPOLDO - RS', 'SAO LUIS - MA', 'SAO PAULO - SP', 'SAPUCAIA DO SUL - RS',
    'SEMINARIO - PR', 'SERRA - ES', 'SOROCABA - SP', 'SUCUPIRA - SC',
    'TABOAO DA SERRA - SP', 'TAGUATINGA - DF', 'TARUMA - PR', 'TAUBATE - SP',
    'TELEMACO BORBA - PR', 'TERESINA - PI', 'TOLEDO - PR', 'TRINDADE - SC',
    'TUBARAO - SC', 'UBERLANDIA - MG', 'UMUA RAMA - PR', 'VALPARAISO DE GOIAS - GO',
    'VIANA - ES', 'VILA VELHA - ES', 'VITÓRIA - ES', 'VITÓRIA DA CONQUISTA - BA',
    'VOLTA REDONDA - RJ', 'XAXIM - PR'
].sort();

const ScrapeDashboard: React.FC<{ initialTab?: string }> = ({ initialTab = 'links' }) => {
  const [configs, setConfigs] = useState<ScrapeConfig[]>([]);
  const [runs, setRuns] = useState<ScrapeRunSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [editingConfig, setEditingConfig] = useState<Partial<ScrapeConfig> | null>(null);
  const [activeTab, setActiveTab] = useState<string | null>(initialTab);

  // History Filter state
  const [filterUser, setFilterUser] = useState('');
  const [filterMatricula, setFilterMatricula] = useState('');
  const [filterStore, setFilterStore] = useState<string | null>(null);
  const [filterStatus, setFilterStatus] = useState<string | null>(null);

  // Form state
  const [store, setStore] = useState<string | null>(null);
  const [matricula, setMatricula] = useState('');
  const [password, setPassword] = useState('');
  const [configDefaultStartMonth, setConfigDefaultStartMonth] = useState('');
  const [validateOnSave, setValidateOnSave] = useState(true);
  const [saving, setSaving] = useState(false);
  const [triggering, setTriggering] = useState<number | null>(null);
  const [testingAuth, setTestingAuth] = useState<number | null>(null);
  const [testAuthModalData, setTestAuthModalData] = useState<{ open: boolean; success: boolean; message: string; steps: string[] }>({
    open: false,
    success: false,
    message: '',
    steps: []
  });

  // Update active tab when initialTab prop changes
  useEffect(() => {
    setActiveTab(initialTab);
  }, [initialTab]);

  const fetchData = async () => {
    try {
      setLoading(true);
      const [configsRes, runsRes] = await Promise.all([
        scrapeService.getConfigs(),
        scrapeService.getRuns()
      ]);
      setConfigs(configsRes || []);
      setRuns(runsRes || []);
    } catch (error) {
      notifications.show({
        title: 'Erro',
        message: 'Falha ao carregar dados de extração',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, []);

  const handleOpenModal = (config?: ScrapeConfig) => {
    if (config) {
      setEditingConfig(config);
      setStore(config.store);
      setMatricula(config.matricula);
      setPassword(''); // Don't show existing password
      setConfigDefaultStartMonth(config.defaultStartMonth || '');
    } else {
      setEditingConfig(null);
      setStore(null);
      setMatricula('');
      setPassword('');
      setConfigDefaultStartMonth('');
    }
    setModalOpen(true);
  };

  const handleSaveConfig = async () => {
    if (!store || !matricula || (!editingConfig && !password)) {
      notifications.show({
        title: 'Aviso',
        message: 'Preencha todos os campos obrigatórios',
        color: 'orange',
      });
      return;
    }

    try {
      setSaving(true);
      await scrapeService.saveConfig({
        id: editingConfig?.id,
        store,
        matricula,
        powerBiPassword: password || undefined,
        defaultStartMonth: configDefaultStartMonth || undefined,
        testOnSave: validateOnSave
      });
      
      notifications.show({
        title: 'Sucesso',
        message: 'Configuração salva com sucesso',
        color: 'green',
      });
      
      setModalOpen(false);
      fetchData();
    } catch (error: any) {
      const errMsg = error.response?.data?.message || 'Falha ao salvar configuração';
      const steps = error.response?.data?.steps || [];
      notifications.show({
        title: 'Erro de Salvamento / Autenticação',
        message: errMsg,
        color: 'red',
        autoClose: 10000,
      });

      if (steps && steps.length > 0) {
        setTestAuthModalData({
          open: true,
          success: false,
          message: errMsg,
          steps
        });
      }
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteConfig = async (id: number) => {
    if (!window.confirm('Tem certeza que deseja remover este vínculo de conta?')) return;
    
    try {
      await scrapeService.deleteConfig(id);
      notifications.show({
        title: 'Removido',
        message: 'Vínculo de conta removido',
        color: 'blue',
      });
      fetchData();
    } catch (error) {
      notifications.show({
        title: 'Erro',
        message: 'Falha ao remover vínculo',
        color: 'red',
      });
    }
  };

  const handleTestAuth = async (id: number, force: boolean = false): Promise<void> => {
    const targetConfig = configs.find(c => c.id === id);
    if (!force && targetConfig?.credentialStatus === 'wrong-password') {
      const confirmRetry = window.confirm(
        'Já testamos essas credenciais recentemente e ocorreu um erro de senha. Tem certeza que deseja testar novamente?'
      );
      if (!confirmRetry) return;
      force = true;
    }

    try {
      setTestingAuth(id);
      const result = await scrapeService.testAuth(id, force);
      
      if (result.requiresConfirmation && !force) {
        const confirmRetry = window.confirm(result.message);
        if (confirmRetry) {
          return handleTestAuth(id, true);
        } else {
          return;
        }
      }

      if (result.success) {
        notifications.show({
          title: 'Autenticação OK',
          message: result.message || 'As credenciais são válidas',
          color: 'green',
          icon: <IconCheck size={16} />
        });
      } else {
        notifications.show({
          title: 'Falha na Autenticação',
          message: result.message || 'Credenciais inválidas ou erro ao autenticar',
          color: 'red',
          autoClose: 10000,
          icon: <IconAlertCircle size={16} />
        });
      }

      if (result.steps && result.steps.length > 0) {
        setTestAuthModalData({
          open: true,
          success: result.success,
          message: result.message,
          steps: result.steps
        });
      }
      fetchData();
    } catch (error: any) {
      const errMsg = error.response?.data?.message || 'Falha ao testar autenticação';
      const steps = error.response?.data?.steps || [];
      notifications.show({
        title: 'Erro',
        message: errMsg,
        color: 'red',
        autoClose: 10000,
      });
      if (steps && steps.length > 0) {
        setTestAuthModalData({
          open: true,
          success: false,
          message: errMsg,
          steps
        });
      }
    } finally {
      setTestingAuth(null);
    }
  };

  const handleTrigger = async (configId: number) => {
    const targetConfig = configs.find(c => c.id === configId);
    const startM = targetConfig?.defaultStartMonth;
    try {
      setTriggering(configId);
      await scrapeService.triggerScrape(configId, startM, 3);
      notifications.show({
        title: 'Extração Iniciada',
        message: startM 
          ? `Robô iniciado a partir de ${startM}. Acompanhe o progresso no histórico.`
          : 'Robô iniciado. Acompanhe o progresso no histórico.',
        color: 'green',
      });
      fetchData();
    } catch (error) {
        notifications.show({
            title: 'Erro',
            message: 'Falha ao iniciar extração',
            color: 'red',
        });
    } finally {
      setTriggering(null);
    }
  };

  const getFinalStatusBadge = (status: string) => {
    switch (status) {
      case 'Succeeded': return <Badge color="green" variant="filled">Sucesso Total</Badge>;
      case 'Failed': return <Badge color="red" variant="filled">Falha</Badge>;
      case 'Running': return <Badge color="blue" variant="filled">Executando...</Badge>;
      case 'Pending': return <Badge color="gray" variant="filled">Pendente</Badge>;
      default: return <Badge color="gray">{status}</Badge>;
    }
  };

  const getCredentialStatusBadge = (status: string | null | undefined) => {
    if (status === 'ok') 
        return <Badge color="green" leftSection={<IconCheck size={12}/>} variant="outline">Válida</Badge>;
    if (status === 'wrong-password')
        return <Badge color="red" leftSection={<IconAlertCircle size={12}/>} variant="outline">Senha Incorreta</Badge>;
    return <Badge color="gray" variant="dot">Não Testada</Badge>;
  };

  const configRows = configs.map((config) => (
    <Table.Tr key={config.id}>
      <Table.Td>
        <Text size="sm" fw={500}>{config.store}</Text>
      </Table.Td>
      <Table.Td>
        <Text size="sm">{config.matricula}</Text>
      </Table.Td>
      <Table.Td>
        {getCredentialStatusBadge(config.credentialStatus)}
      </Table.Td>
      <Table.Td>
        <Group gap="xs">
          <Tooltip label="Testar Autenticação">
            <ActionIcon 
                variant="light" 
                color="blue" 
                onClick={() => handleTestAuth(config.id)}
                loading={testingAuth === config.id}
            >
              <IconUserCheck size={18} />
            </ActionIcon>
          </Tooltip>
          <Tooltip label="Editar">
            <ActionIcon variant="light" color="gray" onClick={() => handleOpenModal(config)}>
              <IconSettings size={18} />
            </ActionIcon>
          </Tooltip>
          <Tooltip label="Remover">
            <ActionIcon variant="light" color="red" onClick={() => handleDeleteConfig(config.id)}>
              <IconTrash size={18} />
            </ActionIcon>
          </Tooltip>
          <Button 
            size="compact-xs" 
            variant="filled" 
            color="indigo"
            leftSection={<IconPlayerPlay size={12}/>}
            onClick={() => handleTrigger(config.id)}
            loading={triggering === config.id}
          >
            Extrair
          </Button>
        </Group>
      </Table.Td>
    </Table.Tr>
  ));

  // Filtered runs
  const filteredRuns = (runs || []).filter((run) => {
    if (filterUser && !run.userEmail.toLowerCase().includes(filterUser.toLowerCase())) {
      return false;
    }
    if (filterMatricula && !run.matriculas.some((m) => m.toLowerCase().includes(filterMatricula.toLowerCase()))) {
      return false;
    }
    if (filterStore && !run.stores.some((s) => s === filterStore)) {
      return false;
    }
    if (filterStatus && filterStatus !== 'all' && run.finalStatus !== filterStatus) {
      return false;
    }
    return true;
  });

  const runRows = filteredRuns.map((run) => (
    <Table.Tr 
      key={run.runId} 
      style={{ cursor: 'pointer' }}
      onClick={() => { window.location.hash = `#/scrapes/runs/${run.runId}`; }}
    >
      <Table.Td>{new Date(run.createdAt).toLocaleString('pt-BR')}</Table.Td>
      <Table.Td><Text size="sm" fw={500}>{run.userEmail || 'Desconhecido'}</Text></Table.Td>
      <Table.Td><Text size="sm">{run.matriculas.join(', ') || '-'}</Text></Table.Td>
      <Table.Td><Text size="sm">{run.stores.join(', ') || '-'}</Text></Table.Td>
      <Table.Td>{getFinalStatusBadge(run.finalStatus)}</Table.Td>
      <Table.Td><Text fw={600} size="sm">{run.totalRowCount}</Text></Table.Td>
      <Table.Td>
        <Button size="compact-xs" variant="light" color="indigo">
          Ver Detalhes
        </Button>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Menu>
      <div className="scrape-dashboard">
        <LoadingOverlay visible={loading && configs.length === 0} />
        <LoadingOverlay 
          visible={testingAuth !== null} 
          zIndex={1000} 
          overlayProps={{ radius: "sm", blur: 2 }}
          loaderProps={{ 
            children: (
              <Stack align="center" gap="xs">
                <Loader size="xl" type="bars" />
                <Text fw={600} size="lg" ta="center">Validando acesso ao PowerBI...</Text>
                <Text size="xs" c="dimmed" ta="center">O robô está abrindo um navegador real para testar suas credenciais. Isso pode levar alguns segundos.</Text>
              </Stack>
            )
          }} 
        />
        <LoadingOverlay 
          visible={triggering !== null} 
          zIndex={1000} 
          overlayProps={{ radius: "sm", blur: 1 }}
          loaderProps={{ 
            children: (
              <Stack align="center" gap="xs">
                <Loader size="md" type="dots" color="indigo" />
                <Text fw={600} size="md" ta="center">Iniciando extração...</Text>
              </Stack>
            )
          }} 
        />
        
        <Group justify="space-between" mb="xl">
            <Title order={2}>Extração PowerBI</Title>
            <Button 
                variant="filled" 
                leftSection={<IconRefresh size={18}/>} 
                onClick={fetchData}
                loading={loading}
                color="gray"
            >
                Sincronizar
            </Button>
        </Group>

        <Tabs value={activeTab} onChange={setActiveTab} mb="xl">
          <Tabs.List>
            <Tabs.Tab value="links" leftSection={<IconFingerprint size={16} />}>Vínculos de Contas</Tabs.Tab>
            <Tabs.Tab value="history" leftSection={<IconHistory size={16} />}>Histórico de Extrações</Tabs.Tab>
          </Tabs.List>

          <Tabs.Panel value="links" pt="xl">
            <Card withBorder radius="md" p="md">
              <Group justify="space-between" mb="md">
                <Text fw={600} size="lg">Contas PowerBI Configuradas</Text>
                <Button 
                  leftSection={<IconPlus size={18} />} 
                  onClick={() => handleOpenModal()}
                  variant="light"
                >
                  Nova Conta
                </Button>
              </Group>

              {configs.length === 0 ? (
                <Alert color="blue" variant="light" mt="md">
                  <Text size="sm">
                    Você ainda não configurou nenhuma conta para extração. 
                    Adicione os dados de acesso das suas unidades para começar.
                  </Text>
                </Alert>
              ) : (
                <Table striped highlightOnHover verticalSpacing="sm">
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Unidade</Table.Th>
                      <Table.Th>Matrícula (Username)</Table.Th>
                      <Table.Th>Status Credencial</Table.Th>
                      <Table.Th>Ações</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>{configRows}</Table.Tbody>
                </Table>
              )}
            </Card>
          </Tabs.Panel>

          <Tabs.Panel value="history" pt="xl">
            <Paper withBorder radius="md" p="md">
              <Stack gap="md" mb="md">
                <Text fw={600} size="lg">Filtros de Histórico</Text>
                <Group grow gap="md">
                  <TextInput
                    placeholder="Filtrar por Usuário (Email)"
                    value={filterUser}
                    onChange={(e) => setFilterUser(e.currentTarget.value)}
                  />
                  <TextInput
                    placeholder="Filtrar por Matrícula"
                    value={filterMatricula}
                    onChange={(e) => setFilterMatricula(e.currentTarget.value)}
                  />
                  <Select
                    placeholder="Filtrar por Unidade"
                    data={STORES}
                    value={filterStore}
                    onChange={setFilterStore}
                    searchable
                    clearable
                  />
                  <Select
                    placeholder="Filtrar por Status"
                    data={[
                      { value: 'all', label: 'Todos os Status' },
                      { value: 'Succeeded', label: 'Sucesso Total' },
                      { value: 'Failed', label: 'Falha' },
                      { value: 'Running', label: 'Executando' },
                      { value: 'Pending', label: 'Pendente' },
                    ]}
                    value={filterStatus}
                    onChange={setFilterStatus}
                    clearable
                  />
                </Group>
              </Stack>

              {runRows.length === 0 ? (
                <Text ta="center" c="dimmed" p="xl">Nenhuma extração encontrada com os filtros selecionados.</Text>
              ) : (
                <Table striped highlightOnHover verticalSpacing="sm">
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Data da Execução</Table.Th>
                      <Table.Th>Executado Por (Email)</Table.Th>
                      <Table.Th>Matrícula(s)</Table.Th>
                      <Table.Th>Unidade(s)</Table.Th>
                      <Table.Th>Status Final</Table.Th>
                      <Table.Th>Registros Totais</Table.Th>
                      <Table.Th>Ação</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>{runRows}</Table.Tbody>
                </Table>
              )}
            </Paper>
          </Tabs.Panel>
        </Tabs>

        {/* Configuration Modal */}
        <Modal 
          opened={modalOpen} 
          onClose={() => setModalOpen(false)} 
          title={editingConfig ? "Editar Conta PowerBI" : "Adicionar Nova Conta PowerBI"}
          centered
          size="md"
        >
          <LoadingOverlay 
            visible={saving && validateOnSave} 
            zIndex={1000} 
            overlayProps={{ radius: "sm", blur: 2 }}
            loaderProps={{ 
              children: (
                <Stack align="center" gap="xs">
                  <Loader size="lg" type="dots" />
                  <Text fw={600} size="md" ta="center">Testando credenciais...</Text>
                </Stack>
              )
            }} 
          />
          <Stack gap="md" pt="xs">
            <Select
              label="Unidade (Store)"
              placeholder="Selecione a unidade"
              data={STORES}
              value={store}
              onChange={setStore}
              searchable
              required
            />
            <TextInput
              label="Matrícula"
              placeholder="Ex: 99999"
              value={matricula}
              onChange={(e) => setMatricula(e.currentTarget.value)}
              required
            />
            <PasswordInput
              label="Senha do Portal/Avapro"
              placeholder="Digite sua senha"
              value={password}
              onChange={(e) => setPassword(e.currentTarget.value)}
              description={editingConfig ? "Deixe em branco para manter a senha atual" : undefined}
              required={!editingConfig}
            />

            <TextInput
              label="Mês Inicial Padrão (Opcional)"
              type="month"
              value={configDefaultStartMonth}
              onChange={(e) => setConfigDefaultStartMonth(e.currentTarget.value)}
              description="Define o mês inicial padrão pré-selecionado ao solicitar extração desta conta."
            />
            
            <Divider mt="xs" label="Segurança" labelPosition="center" />
            
            <Checkbox
              label="Validar credenciais ao salvar"
              checked={validateOnSave}
              onChange={(e) => setValidateOnSave(e.currentTarget.checked)}
              description="O robô tentará fazer login no PowerBI para confirmar se a senha está correta."
            />

            <Group justify="flex-end" mt="md">
              <Button variant="subtle" onClick={() => setModalOpen(false)} color="gray">Cancelar</Button>
              <Button 
                onClick={handleSaveConfig} 
                loading={saving}
                color="blue"
              >
                Salvar Configuração
              </Button>
            </Group>
          </Stack>
        </Modal>

        {/* Test Auth Diagnostic Modal */}
        <Modal
          opened={testAuthModalData.open}
          onClose={() => setTestAuthModalData(prev => ({ ...prev, open: false }))}
          title={
            <Group gap="xs">
              <IconFingerprint size={20} />
              <Text fw={600}>Resultado do Teste de Autenticação</Text>
            </Group>
          }
          centered
          size="lg"
        >
          <Stack gap="md">
            <Alert 
              color={testAuthModalData.success ? "green" : "red"}
              title={testAuthModalData.success ? "Autenticação OK" : "Falha na Autenticação"}
              icon={testAuthModalData.success ? <IconCheck size={18} /> : <IconAlertCircle size={18} />}
            >
              <Text size="sm">{testAuthModalData.message}</Text>
            </Alert>

            {testAuthModalData.steps && testAuthModalData.steps.length > 0 && (
              <>
                <Text size="xs" fw={600} c="dimmed">Passos executados pelo robô:</Text>
                <Box style={{ maxHeight: '350px', overflowY: 'auto' }}>
                  <Code block style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word', fontSize: '12px' }}>
                    {testAuthModalData.steps.join('\n')}
                  </Code>
                </Box>
              </>
            )}

            <Group justify="flex-end">
              <Button variant="light" onClick={() => setTestAuthModalData(prev => ({ ...prev, open: false }))}>
                Fechar
              </Button>
            </Group>
          </Stack>
        </Modal>
      </div>
    </Menu>
  );
};

export default ScrapeDashboard;
