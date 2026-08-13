import React, { useState, useEffect, useCallback } from 'react';
import {
  Title,
  Paper,
  Table,
  Badge,
  Text,
  Group,
  Button,
  LoadingOverlay,
  Card,
  Stack,
  SimpleGrid,
  ThemeIcon,
  Modal,
  Code,
  Tooltip,
  ActionIcon,
  Box
} from '@mantine/core';
import {
  IconArrowLeft,
  IconCheck,
  IconClock,
  IconUser,
  IconId,
  IconDatabase,
  IconRefresh,
  IconListDetails,
  IconAlertTriangle,
  IconX
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import { scrapeService, ScrapeRunDetail, ScrapeJob } from '../../services/scrapeService';
import Menu from '../Menu';
import './ScrapeDashboard.css';

interface ScrapeRunDetailPageProps {
  runId: string;
}

const ScrapeRunDetailPage: React.FC<ScrapeRunDetailPageProps> = ({ runId }) => {
  const [detail, setDetail] = useState<ScrapeRunDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [selectedJobSteps, setSelectedJobSteps] = useState<{ job: ScrapeJob; steps: string[] } | null>(null);

  const fetchDetail = useCallback(async () => {
    try {
      setLoading(true);
      const data = await scrapeService.getRunDetail(runId);
      setDetail(data);
    } catch (error: any) {
      notifications.show({
        title: 'Erro',
        message: 'Falha ao carregar detalhes da execução',
        color: 'red',
      });
    } finally {
      setLoading(false);
    }
  }, [runId]);

  useEffect(() => {
    if (runId) {
      fetchDetail();
    }
  }, [runId, fetchDetail]);

  const handleBack = () => {
    window.location.hash = '#/scrapes/historial';
  };

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Pending':
        return <Badge color="gray" variant="light">Pendente</Badge>;
      case 'Running':
        return <Badge color="blue" variant="filled">Executando...</Badge>;
      case 'Succeeded':
        return <Badge color="green" variant="light">Sucesso</Badge>;
      case 'Failed':
        return <Badge color="red" variant="light">Falha</Badge>;
      default:
        return <Badge color="gray">{status}</Badge>;
    }
  };

  const getFinalStatusBadge = (status: string) => {
    switch (status) {
      case 'Succeeded':
        return <Badge color="green" size="lg" variant="filled">Sucesso Total</Badge>;
      case 'Failed':
        return <Badge color="red" size="lg" variant="filled">Falha</Badge>;
      case 'Running':
        return <Badge color="blue" size="lg" variant="filled">Executando...</Badge>;
      case 'Pending':
        return <Badge color="gray" size="lg" variant="filled">Pendente</Badge>;
      default:
        return <Badge color="gray" size="lg">{status}</Badge>;
    }
  };

  const getAuthStatusBadge = (authStatus?: string) => {
    switch (authStatus) {
      case 'success':
        return <Badge color="green" variant="dot" size="xs">Autenticação OK</Badge>;
      case 'invalid-credentials':
        return <Badge color="red" variant="filled" size="xs">Senha / Usuário Inválido</Badge>;
      case 'timeout':
        return <Badge color="orange" variant="light" size="xs">Timeout na Autenticação</Badge>;
      case 'error':
        return <Badge color="red" variant="light" size="xs">Erro de Autenticação</Badge>;
      default:
        return <Text size="xs" c="dimmed">-</Text>;
    }
  };

  const totalRowCount = detail?.jobs.reduce((acc, job) => acc + (job.rowCount || 0), 0) || 0;
  const uniqueMatriculas = Array.from(new Set(detail?.jobs.map((j) => j.matricula) || [])).filter(Boolean);

  const jobRows = (detail?.jobs || []).map((job) => (
    <Table.Tr key={job.jobId}>
      <Table.Td>{new Date(job.createdAt).toLocaleString('pt-BR')}</Table.Td>
      <Table.Td><Text fw={500} size="sm">{job.store}</Text></Table.Td>
      <Table.Td><Text size="sm">{job.matricula}</Text></Table.Td>
      <Table.Td>{getStatusBadge(job.status)}</Table.Td>
      <Table.Td>{getAuthStatusBadge(job.authStatus)}</Table.Td>
      <Table.Td>
        {job.powerBiLoaded ? (
          <Badge color="cyan" variant="outline" size="xs" leftSection={<IconCheck size={10} />}>Carregado</Badge>
        ) : (
          <Badge color="gray" variant="dot" size="xs">Não Detectado</Badge>
        )}
      </Table.Td>
      <Table.Td><Text fw={600} size="sm">{job.rowCount || 0}</Text></Table.Td>
      <Table.Td>
        <Stack gap={4}>
          {job.status === 'Failed' ? (
            <Text c="red" size="xs" lineClamp={2} title={job.errorMessage || job.authMessage || 'Erro na extração'}>
              {job.errorMessage || job.authMessage || 'Erro na extração'}
            </Text>
          ) : job.status === 'Succeeded' ? (
            <Group gap="xs">
              <IconCheck size={16} color="green" />
              <Text size="xs" c="dimmed">Concluído</Text>
            </Group>
          ) : (
            <Text size="xs" c="dimmed">-</Text>
          )}

          {job.authSteps && job.authSteps.length > 0 && (
            <Tooltip label="Ver passos detalhados da execução">
              <Button
                variant="subtle"
                size="compact-xs"
                color="blue"
                leftSection={<IconListDetails size={12} />}
                onClick={() => setSelectedJobSteps({ job, steps: job.authSteps || [] })}
              >
                Ver Passos ({job.authSteps.length})
              </Button>
            </Tooltip>
          )}
        </Stack>
      </Table.Td>
    </Table.Tr>
  ));

  return (
    <Menu>
      <div className="scrape-dashboard">
        <LoadingOverlay visible={loading} />

        <Group justify="space-between" mb="xl">
          <Group gap="sm">
            <Button
              variant="subtle"
              color="gray"
              leftSection={<IconArrowLeft size={18} />}
              onClick={handleBack}
            >
              Voltar ao Histórico
            </Button>
          </Group>
          <Button
            variant="light"
            color="gray"
            leftSection={<IconRefresh size={18} />}
            onClick={fetchDetail}
            loading={loading}
          >
            Atualizar
          </Button>
        </Group>

        {detail && (
          <Stack gap="lg">
            <Card withBorder radius="md" p="lg">
              <Group justify="space-between" align="flex-start" mb="md">
                <Stack gap="xs">
                  <Group gap="xs">
                    <Title order={2}>Detalhes da Execução</Title>
                    {getFinalStatusBadge(detail.finalStatus)}
                  </Group>
                  <Text size="xs" c="dimmed" style={{ fontFamily: 'monospace' }}>
                    Run ID: {detail.runId}
                  </Text>
                </Stack>
              </Group>

              <SimpleGrid cols={{ base: 1, sm: 2, md: 4 }} spacing="md">
                <Paper withBorder p="sm" radius="md">
                  <Group gap="xs">
                    <ThemeIcon variant="light" color="blue" size="md">
                      <IconUser size={18} />
                    </ThemeIcon>
                    <div>
                      <Text size="xs" c="dimmed">Executado por (Email)</Text>
                      <Text fw={600} size="sm">{detail.userEmail || 'Desconhecido'}</Text>
                    </div>
                  </Group>
                </Paper>

                <Paper withBorder p="sm" radius="md">
                  <Group gap="xs">
                    <ThemeIcon variant="light" color="cyan" size="md">
                      <IconClock size={18} />
                    </ThemeIcon>
                    <div>
                      <Text size="xs" c="dimmed">Data da Execução</Text>
                      <Text fw={600} size="sm">{new Date(detail.createdAt).toLocaleString('pt-BR')}</Text>
                    </div>
                  </Group>
                </Paper>

                <Paper withBorder p="sm" radius="md">
                  <Group gap="xs">
                    <ThemeIcon variant="light" color="indigo" size="md">
                      <IconId size={18} />
                    </ThemeIcon>
                    <div>
                      <Text size="xs" c="dimmed">Matrícula(s)</Text>
                      <Text fw={600} size="sm">{uniqueMatriculas.join(', ') || '-'}</Text>
                    </div>
                  </Group>
                </Paper>

                <Paper withBorder p="sm" radius="md">
                  <Group gap="xs">
                    <ThemeIcon variant="light" color="green" size="md">
                      <IconDatabase size={18} />
                    </ThemeIcon>
                    <div>
                      <Text size="xs" c="dimmed">Registros Extraídos</Text>
                      <Text fw={600} size="sm">{totalRowCount}</Text>
                    </div>
                  </Group>
                </Paper>
              </SimpleGrid>
            </Card>

            <Paper withBorder radius="md" p="md">
              <Text fw={600} size="lg" mb="md">Processos Individuais ({detail.jobs.length})</Text>
              <Table striped highlightOnHover verticalSpacing="sm">
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Data/Hora</Table.Th>
                    <Table.Th>Unidade</Table.Th>
                    <Table.Th>Matrícula</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Autenticação</Table.Th>
                    <Table.Th>PowerBI</Table.Th>
                    <Table.Th>Registros</Table.Th>
                    <Table.Th>Observações / Passos</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>{jobRows}</Table.Tbody>
              </Table>
            </Paper>
          </Stack>
        )}

        {/* Diagnostic Step Log Modal */}
        <Modal
          opened={!!selectedJobSteps}
          onClose={() => setSelectedJobSteps(null)}
          title={`Log de Passos da Execução (${selectedJobSteps?.job.store || ''} - ${selectedJobSteps?.job.matricula || ''})`}
          size="lg"
        >
          {selectedJobSteps && (
            <Stack gap="md">
              {selectedJobSteps.job.errorMessage && (
                <Paper withBorder p="sm" bg="red.0" radius="sm">
                  <Text c="red.9" fw={600} size="xs">Mensagem de Erro:</Text>
                  <Text c="red.9" size="sm">{selectedJobSteps.job.errorMessage}</Text>
                </Paper>
              )}
              
              <Text size="xs" c="dimmed">Passos gravados durante a tentativa de login e navegação:</Text>
              
              <Box style={{ maxHeight: '400px', overflowY: 'auto' }}>
                <Code block style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word', fontSize: '12px' }}>
                  {selectedJobSteps.steps.join('\n')}
                </Code>
              </Box>

              <Group justify="flex-end">
                <Button variant="light" onClick={() => setSelectedJobSteps(null)}>Fechar</Button>
              </Group>
            </Stack>
          )}
        </Modal>
      </div>
    </Menu>
  );
};

export default ScrapeRunDetailPage;
