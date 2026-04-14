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
  LoadingOverlay,
  Card,
  ActionIcon,
  Tooltip,
} from '@mantine/core';
import { IconRefresh, IconSettings, IconPlay, IconAlertCircle, IconCheck } from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { scrapeService, ScrapeConfig, ScrapeJob } from '../../services/scrapeService';
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
    'CURITIBA SEMINARIO - PR', 'CURITIBA TARUMA - PR', 'CURITIBA XAXIM - PR', 'DIADEMA - SP',
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

const ScrapeDashboard: React.FC = () => {
  const [configs, setConfigs] = useState<ScrapeConfig[]>([]);
  const [jobs, setJobs] = useState<ScrapeJob[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [triggering, setTriggering] = useState<number | null>(null);

  // Form state
  const [selectedStore, setSelectedStore] = useState<string | null>(null);
  const [matricula, setMatricula] = useState('');

  const fetchData = async () => {
    try {
      setLoading(true);
      const [configsRes, jobsRes] = await Promise.all([
        scrapeService.getConfigs(),
        scrapeService.getJobs()
      ]);
      setConfigs(configsRes);
      setJobs(jobsRes);

      if (configsRes.length > 0) {
        setSelectedStore(configsRes[0].store);
        setMatricula(configsRes[0].matricula);
      }
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
    const interval = setInterval(fetchData, 30000); // Auto refresh every 30s
    return () => clearInterval(interval);
  }, []);

  const handleSaveConfig = async () => {
    if (!selectedStore || !matricula) {
      notifications.show({
        title: 'Aviso',
        message: 'Preencha a Unidade e a Matrícula',
        color: 'orange',
      });
      return;
    }

    try {
      setSaving(true);
      await scrapeService.saveConfig({
        store: selectedStore,
        matricula: matricula
      });
      notifications.show({
        title: 'Sucesso',
        message: 'Configuração salva com sucesso',
        color: 'green',
      });
      fetchData();
    } catch (error) {
        notifications.show({
            title: 'Erro',
            message: 'Falha ao salvar configuração',
            color: 'red',
        });
    } finally {
      setSaving(false);
    }
  };

  const handleTrigger = async (configId: number) => {
    try {
      setTriggering(configId);
      await scrapeService.triggerScrape(configId);
      notifications.show({
        title: 'Sucesso',
        message: 'Extração iniciada. Você poderá acompanhar o progresso na tabela abaixo.',
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

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Pending': return <Badge color="gray">Pendente</Badge>;
      case 'Running': return <Badge color="blue" variant="filled">Executando...</Badge>;
      case 'Succeeded': return <Badge color="green">Sucesso</Badge>;
      case 'Failed': return <Badge color="red">Falha</Badge>;
      default: return <Badge color="gray">{status}</Badge>;
    }
  };

  const jobRows = jobs.map((job) => (
    <Table.Tr key={job.jobId}>
      <Table.Td>{new Date(job.createdAt).toLocaleString('pt-BR')}</Table.Td>
      <Table.Td>{job.store}</Table.Td>
      <Table.Td>{job.matricula}</Table.Td>
      <Table.Td>{getStatusBadge(job.status)}</Table.Td>
      <Table.Td>{job.rowCount || 0}</Table.Td>
      <Table.Td>
        {job.errorMessage ? (
          <Tooltip label={job.errorMessage}>
            <IconAlertCircle size={18} color="red" style={{ cursor: 'help' }} />
          </Tooltip>
        ) : (
          job.status === 'Succeeded' && <IconCheck size={18} color="green" />
        )}
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Menu>
      <div className="scrape-dashboard">
        <LoadingOverlay visible={loading && jobs.length === 0} />
        
        <Title order={2} mb="xl">Extração Automática PowerBI</Title>

        <Card withBorder radius="md" p="xl" mb="xl" className="config-card">
          <Group justify="space-between" mb="lg">
            <Group>
              <IconSettings size={24} />
              <Text fw={700} size="lg">Configuração da Unidade</Text>
            </Group>
            <Button 
                variant="light" 
                leftSection={<IconRefresh size={16}/>} 
                onClick={fetchData}
                loading={loading}
            >
                Atualizar Histórico
            </Button>
          </Group>

          <Group grow align="flex-end">
            <Select
              label="Unidade (Store)"
              placeholder="Selecione a unidade"
              data={STORES}
              value={selectedStore}
              onChange={setSelectedStore}
              searchable
            />
            <TextInput
              label="Matrícula"
              placeholder="Sua matrícula com acesso à unidade"
              value={matricula}
              onChange={(e) => setMatricula(e.currentTarget.value)}
            />
            <Button 
                onClick={handleSaveConfig} 
                loading={saving}
                color="blue"
            >
              Salvar Configuração
            </Button>
          </Group>

          {configs.length > 0 && (
            <div style={{ marginTop: '20px' }}>
                <Alert color="blue" icon={<IconPlay size={16}/>}>
                    <Group justify="space-between">
                        <Text size="sm">
                            Sua configuração está pronta. Clique no botão ao lado para iniciar uma extração agora.
                        </Text>
                        <Button 
                            color="blue" 
                            size="xs"
                            leftSection={<IconPlay size={14}/>}
                            onClick={() => handleTrigger(configs[0].id)}
                            loading={triggering === configs[0].id}
                        >
                            Trigger Scrape
                        </Button>
                    </Group>
                </Alert>
            </div>
          )}
        </Card>

        <Paper withBorder radius="md" p="md">
          <Title order={4} mb="md">Histórico de Extrações</Title>
          {jobs.length === 0 ? (
            <Text ta="center" c="dimmed" p="xl">Nenhuma extração realizada ainda.</Text>
          ) : (
            <Table striped highlightOnHover verticalSpacing="sm">
              <Table.Thead>
                <Table.Tr>
                  <Table.Th>Data</Table.Th>
                  <Table.Th>Unidade</Table.Th>
                  <Table.Th>Matrícula</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th>Registros</Table.Th>
                  <Table.Th>Obs</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>{jobRows}</Table.Tbody>
            </Table>
          )}
        </Paper>
      </div>
    </Menu>
  );
};

export default ScrapeDashboard;
