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
  SegmentedControl,
  Accordion,
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
  IconUserCheck
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

type DateSelectionMode = '1' | '3' | '12' | '15' | 'custom' | 'all';

function calculateRelativeMonth(months: number): string {
  const d = new Date();
  d.setMonth(d.getMonth() - (months - 1));
  const year = d.getFullYear();
  const month = String(d.getMonth() + 1).padStart(2, '0');
  return `${year}-${month}`;
}

function resolveDateMode(startMonth?: string | null): DateSelectionMode {
  if (!startMonth || !startMonth.trim()) return 'all';
  const trimmed = startMonth.trim();
  if (trimmed === calculateRelativeMonth(1)) return '1';
  if (trimmed === calculateRelativeMonth(3)) return '3';
  if (trimmed === calculateRelativeMonth(12)) return '12';
  if (trimmed === calculateRelativeMonth(15)) return '15';
  return 'custom';
}

function formatMonthRangeHelper(mode: DateSelectionMode, startMonth: string): string {
  if (mode === 'all') {
    return 'Sem filtro de data: o robô buscará todos os contratos disponíveis no portal.';
  }
  if (!startMonth) {
    return 'Nenhum mês selecionado.';
  }
  const parts = startMonth.split('-');
  if (parts.length === 2) {
    const [y, m] = parts;
    return `Extrairá dados a partir de 01/${m}/${y} até o mês atual.`;
  }
  return `Mês inicial: ${startMonth}`;
}

  // Form state
  const [store, setStore] = useState<string | null>(null);
  const [matricula, setMatricula] = useState('');
  const [password, setPassword] = useState('');
  const [dateMode, setDateMode] = useState<DateSelectionMode>('all');
  const [configDefaultStartMonth, setConfigDefaultStartMonth] = useState('');
  const [scrapeType, setScrapeType] = useState<'geral' | 'consultor'>('consultor');
  const [outputMode, setOutputMode] = useState<'direct' | 'sqs'>('direct');
  const [autoImportSqs, setAutoImportSqs] = useState(true);
  const [skipMissingContractNumber, setSkipMissingContractNumber] = useState(true);
  const [allowAutoCreateGroups, setAllowAutoCreateGroups] = useState(true);
  const [allowAutoCreatePVs, setAllowAutoCreatePVs] = useState(true);
  const [updateMatriculaOnExisting, setUpdateMatriculaOnExisting] = useState(false);
  const [updateTotalAmountOnExisting, setUpdateTotalAmountOnExisting] = useState(true);
  const [updateStartDateOnExisting, setUpdateStartDateOnExisting] = useState(true);
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
      setStore(config.store || '');
      setMatricula(config.matricula);
      setPassword(''); // Don't show existing password
      const initialMonth = config.defaultStartMonth || '';
      setConfigDefaultStartMonth(initialMonth);
      setDateMode(resolveDateMode(initialMonth));
      setScrapeType(config.scrapeType === 'consultor' ? 'consultor' : 'geral');
      setOutputMode(config.outputMode === 'sqs' ? 'sqs' : 'direct');
      setAutoImportSqs(config.autoImportSqs ?? true);
      setSkipMissingContractNumber(config.skipMissingContractNumber ?? true);
      setAllowAutoCreateGroups(config.allowAutoCreateGroups ?? true);
      setAllowAutoCreatePVs(config.allowAutoCreatePVs ?? true);
      setUpdateMatriculaOnExisting(config.updateMatriculaOnExisting ?? false);
      setUpdateTotalAmountOnExisting(config.updateTotalAmountOnExisting ?? true);
      setUpdateStartDateOnExisting(config.updateStartDateOnExisting ?? true);
    } else {
      setEditingConfig(null);
      setStore('');
      setMatricula('');
      setPassword('');
      setConfigDefaultStartMonth('');
      setDateMode('all');
      setScrapeType('consultor');
      setOutputMode('direct');
      setAutoImportSqs(true);
      setSkipMissingContractNumber(true);
      setAllowAutoCreateGroups(true);
      setAllowAutoCreatePVs(true);
      setUpdateMatriculaOnExisting(false);
      setUpdateTotalAmountOnExisting(true);
      setUpdateStartDateOnExisting(true);
    }
    setModalOpen(true);
  };

  const handleSaveConfig = async () => {
    if (!matricula || (!editingConfig && !password)) {
      notifications.show({
        title: 'Aviso',
        message: 'Preencha todos os campos obrigatórios (Matrícula e Senha)',
        color: 'orange',
      });
      return;
    }

    try {
      setSaving(true);
      await scrapeService.saveConfig({
        id: editingConfig?.id,
        store: store || undefined,
        matricula,
        powerBiPassword: password || undefined,
        defaultStartMonth: configDefaultStartMonth || undefined,
        scrapeType,
        outputMode,
        autoImportSqs,
        skipMissingContractNumber,
        allowAutoCreateGroups,
        allowAutoCreatePVs,
        updateMatriculaOnExisting,
        updateTotalAmountOnExisting,
        updateStartDateOnExisting,
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
    if (targetConfig?.credentialStatus === 'wrong-password') {
      notifications.show({
        title: 'Extração Bloqueada',
        message: 'Esta conta está com falha de autenticação ("wrong-password"). Atualize e teste a senha para evitar bloqueio no AVA PRO.',
        color: 'red',
        icon: <IconAlertCircle size={16} />,
        autoClose: 8000,
      });
      return;
    }

    const startM = targetConfig?.defaultStartMonth;
    const sType = targetConfig?.scrapeType || 'geral';
    try {
      setTriggering(configId);
      await scrapeService.triggerScrape(configId, startM, 3, sType);
      notifications.show({
        title: 'Extração Iniciada',
        message: startM 
          ? `Robô iniciado a partir de ${startM}. Acompanhe o progresso no histórico.`
          : 'Robô iniciado. Acompanhe o progresso no histórico.',
        color: 'green',
      });
      fetchData();
    } catch (error: any) {
      const errMsg = error.response?.data?.message || 'Falha ao iniciar extração';
      notifications.show({
        title: 'Erro ao Iniciar Extração',
        message: errMsg,
        color: 'red',
        autoClose: 10000,
        icon: <IconAlertCircle size={16} />
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

  const formatStartMonth = (startMonthStr?: string | null) => {
    if (!startMonthStr) {
      return { relative: 'Todas as datas', dateFormatted: 'Sem filtro' };
    }

    const parts = startMonthStr.trim().split('-');
    if (parts.length < 2) {
      return { relative: 'Todas as datas', dateFormatted: 'Sem filtro' };
    }

    const year = parseInt(parts[0], 10);
    const month = parseInt(parts[1], 10);

    if (isNaN(year) || isNaN(month)) {
      return { relative: 'Todas as datas', dateFormatted: 'Sem filtro' };
    }

    const now = new Date();
    const currentYear = now.getFullYear();
    const currentMonth = now.getMonth() + 1; // 1-indexed

    const totalMonthsDiff = (currentYear - year) * 12 + (currentMonth - month);

    let relative = '';
    if (totalMonthsDiff <= 0) {
      relative = 'Mês atual';
    } else if (totalMonthsDiff === 1) {
      relative = '1 mês atrás';
    } else {
      relative = `${totalMonthsDiff} meses atrás`;
    }

    const dateFormatted = `01/${String(month).padStart(2, '0')}/${year}`;

    return { relative, dateFormatted };
  };

  const configRows = configs.map((config) => {
    const startInfo = formatStartMonth(config.defaultStartMonth);
    return (
      <Table.Tr key={config.id}>
        <Table.Td>
          <Group gap="xs">
            {config.scrapeType === 'consultor' ? (
              <Badge color="violet" variant="light">Consultor</Badge>
            ) : (
              <Badge color="blue" variant="light">Geral</Badge>
            )}
            {config.outputMode === 'sqs' ? (
              <Badge color="teal" variant="outline">
                {config.autoImportSqs === false ? 'SQS / S3 (Manual)' : 'SQS / S3 (Auto)'}
              </Badge>
            ) : (
              <Badge color="gray" variant="outline">Direto</Badge>
            )}
          </Group>
        </Table.Td>
        <Table.Td>
          {config.store ? (
            <Text size="sm" fw={500}>{config.store}</Text>
          ) : (
            <Text size="sm" c="dimmed" fs="italic">Tentar selecionar automaticamente</Text>
          )}
        </Table.Td>
        <Table.Td>
          <Text size="sm">{config.matricula}</Text>
        </Table.Td>
        <Table.Td>
          <Stack gap={0}>
            <Text size="sm" fw={500}>{startInfo.relative}</Text>
            <Text size="xs" c="dimmed">{startInfo.dateFormatted}</Text>
          </Stack>
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
              <ActionIcon data-testid="edit-scrape-config-btn" aria-label="Editar" variant="light" color="gray" onClick={() => handleOpenModal(config)}>
                <IconSettings size={18} />
              </ActionIcon>
            </Tooltip>
            <Tooltip label="Remover">
              <ActionIcon data-testid="delete-scrape-config-btn" variant="light" color="red" onClick={() => handleDeleteConfig(config.id)} aria-label="Remover">
                <IconTrash size={18} />
              </ActionIcon>
            </Tooltip>
            <Button 
              data-testid="trigger-scrape-btn"
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
    );
  });

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

  const runRows = filteredRuns.map((run) => {
    const firstDate = run.scrapeDates && run.scrapeDates.length > 0 ? run.scrapeDates[0] : null;
    const startInfo = formatStartMonth(firstDate);

    return (
      <Table.Tr 
        key={run.runId} 
        style={{ cursor: 'pointer' }}
        onClick={() => { window.location.hash = `#/scrapes/runs/${run.runId}`; }}
      >
        <Table.Td>{new Date(run.createdAt).toLocaleString('pt-BR')}</Table.Td>
        <Table.Td><Text size="sm" fw={500}>{run.userEmail || 'Desconhecido'}</Text></Table.Td>
        <Table.Td><Text size="sm">{run.matriculas.join(', ') || '-'}</Text></Table.Td>
        <Table.Td><Text size="sm">{run.stores.join(', ') || '-'}</Text></Table.Td>
        <Table.Td>
          <Stack gap={0}>
            <Text size="sm" fw={500}>{startInfo.relative}</Text>
            <Text size="xs" c="dimmed">{startInfo.dateFormatted}</Text>
          </Stack>
        </Table.Td>
        <Table.Td>{getFinalStatusBadge(run.finalStatus)}</Table.Td>
        <Table.Td><Badge variant="light" color="blue" size="sm">{run.durationFormatted || '0s'}</Badge></Table.Td>
        <Table.Td><Text fw={600} size="sm">{run.totalRowCount}</Text></Table.Td>
        <Table.Td>
          <Button size="compact-xs" variant="light" color="indigo">
            Ver Detalhes
          </Button>
        </Table.Td>
      </Table.Tr>
    );
  });

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
                      <Table.Th>Tipo</Table.Th>
                      <Table.Th>Unidade</Table.Th>
                      <Table.Th>Matrícula (Username)</Table.Th>
                      <Table.Th>Início da Extração</Table.Th>
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
                      <Table.Th>Início da Extração</Table.Th>
                      <Table.Th>Status Final</Table.Th>
                      <Table.Th>Tempo Total</Table.Th>
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
            <div>
              <Text size="sm" fw={500} mb={4}>Tipo de Relatório / Scraping</Text>
              <SegmentedControl
                fullWidth
                value={scrapeType}
                onChange={(val) => setScrapeType(val as 'geral' | 'consultor')}
                data={[
                  { label: 'Relatório Geral (Loja/PV)', value: 'geral' },
                  { label: 'Relatório Consultor (Individual)', value: 'consultor' },
                ]}
              />
              <Text size="xs" c="dimmed" mt={4}>
                {scrapeType === 'consultor' 
                  ? 'Modo direto e rápido via API dedicada PowerBI, focado na carteira de contratos da matrícula.'
                  : 'Modo padrão por unidade/loja no dashboard Geral.'}
              </Text>
            </div>

            <div>
              <Text size="sm" fw={500} mb={4}>Destino dos Resultados</Text>
              <SegmentedControl
                fullWidth
                value={outputMode}
                onChange={(val) => setOutputMode(val as 'direct' | 'sqs')}
                data={[
                  { label: 'Importação Direta (Padrão)', value: 'direct' },
                  { label: 'Fila AWS SQS / S3 (Worker Local)', value: 'sqs' },
                ]}
              />
              <Text size="xs" c="dimmed" mt={4}>
                {outputMode === 'sqs'
                  ? 'O scraper subirá o CSV no S3 e notificará a fila SQS. Um worker local ou o backend fará a importação.'
                  : 'O scraper salva o CSV no volume compartilhado e a API importa automaticamente ao concluir.'}
              </Text>
              {outputMode === 'sqs' && (
                <Checkbox
                  mt="xs"
                  label="Importar SQS automaticamente no backend"
                  description="A API processará os arquivos do S3 em segundo plano assim que chegarem na fila SQS."
                  checked={autoImportSqs}
                  onChange={(e) => setAutoImportSqs(e.currentTarget.checked)}
                />
              )}
            </div>

            <Select
              label="Unidade (Store)"
              placeholder="Tentar selecionar automaticamente"
              data={[
                { value: '', label: 'Tentar selecionar automaticamente' },
                ...STORES
              ]}
              value={store || ''}
              onChange={(val) => setStore(val || '')}
              searchable
              clearable
              disabled={scrapeType === 'consultor'}
              description={scrapeType === 'consultor' ? 'Não aplicável para relatório de consultor.' : 'Opcional. Caso vazia, o robô identificará a unidade automaticamente no portal.'}
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

            <Select
              label="Período de Extração Padrão"
              description="Define o período retroativo ou mês inicial pré-selecionado para esta conta."
              value={dateMode}
              onChange={(val) => {
                const mode = (val || 'all') as DateSelectionMode;
                setDateMode(mode);
                if (mode === '1') setConfigDefaultStartMonth(calculateRelativeMonth(1));
                else if (mode === '3') setConfigDefaultStartMonth(calculateRelativeMonth(3));
                else if (mode === '12') setConfigDefaultStartMonth(calculateRelativeMonth(12));
                else if (mode === '15') setConfigDefaultStartMonth(calculateRelativeMonth(15));
                else if (mode === 'all') setConfigDefaultStartMonth('');
                else if (mode === 'custom' && !configDefaultStartMonth) {
                  setConfigDefaultStartMonth(calculateRelativeMonth(1));
                }
              }}
              data={[
                { value: '1', label: 'Último 1 mês (Mês atual)' },
                { value: '3', label: 'Últimos 3 meses' },
                { value: '12', label: 'Últimos 12 meses (1 ano)' },
                { value: '15', label: 'Últimos 15 meses (Máximo)' },
                { value: 'custom', label: 'Mês Específico (Personalizado)' },
                { value: 'all', label: 'Todas as datas (Sem filtro)' },
              ]}
            />

            {dateMode === 'custom' && (
              <TextInput
                label="Mês Inicial Personalizado"
                type="month"
                value={configDefaultStartMonth}
                onChange={(e) => setConfigDefaultStartMonth(e.currentTarget.value)}
                description="Escolha livremente o ano e mês inicial (ex: 2026-05)."
              />
            )}

            <Text size="xs" c="dimmed" mt={-4}>
              {formatMonthRangeHelper(dateMode, configDefaultStartMonth)}
            </Text>

            <Accordion variant="separated" radius="md">
              <Accordion.Item value="advanced-options" style={{ backgroundColor: '#ffffff', borderColor: '#e5e7eb' }}>
                <Accordion.Control>
                  <Group gap="xs">
                    <IconSettings size={18} color="#4b5563" />
                    <Text size="sm" fw={600} c="#374151">Opções Avançadas</Text>
                  </Group>
                </Accordion.Control>
                <Accordion.Panel>
                  <Stack gap="sm" pt="xs">
                    <Text size="sm" fw={600} c="#374151">Opções de Importação:</Text>
                    
                    <Checkbox
                      checked={skipMissingContractNumber}
                      onChange={(e) => setSkipMissingContractNumber(e.currentTarget.checked)}
                      label="Pular linhas sem número de contrato (útil para arquivos com subtotais ou lixo)"
                    />
                    
                    <Checkbox
                      checked={allowAutoCreateGroups}
                      onChange={(e) => setAllowAutoCreateGroups(e.currentTarget.checked)}
                      label="Permitir criação automática de grupos"
                    />
                    
                    <Checkbox
                      checked={allowAutoCreatePVs}
                      onChange={(e) => setAllowAutoCreatePVs(e.currentTarget.checked)}
                      label="Permitir criação automática de PV"
                    />
                    
                    <Checkbox
                      checked={updateMatriculaOnExisting}
                      onChange={(e) => setUpdateMatriculaOnExisting(e.currentTarget.checked)}
                      label="Atualizar matrícula em contratos existentes"
                    />
                    
                    <Checkbox
                      checked={updateTotalAmountOnExisting}
                      onChange={(e) => setUpdateTotalAmountOnExisting(e.currentTarget.checked)}
                      label="Atualizar valor total em contratos existentes"
                    />
                    
                    <Checkbox
                      checked={updateStartDateOnExisting}
                      onChange={(e) => setUpdateStartDateOnExisting(e.currentTarget.checked)}
                      label="Atualizar data do contrato"
                    />
                  </Stack>
                </Accordion.Panel>
              </Accordion.Item>
            </Accordion>
            
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
