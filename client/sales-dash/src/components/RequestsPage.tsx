import React, { useState, useEffect } from 'react';
import {
  Title,
  Text,
  Button,
  Table,
  Badge,
  Group,
  Tabs,
  Modal,
  TextInput,
  Select,
  Textarea,
  Alert,
  LoadingOverlay,
  Card,
  Container,
} from '@mantine/core';
import { IconCheck, IconX, IconClock, IconPlus, IconAlertCircle, IconSend } from '@tabler/icons-react';
import { apiService, ApprovalRequestItem } from '../services/apiService';
import './RequestsPage.css';

const RequestsPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<string | null>('pending');
  const [pendingRequests, setPendingRequests] = useState<ApprovalRequestItem[]>([]);
  const [myRequests, setMyRequests] = useState<ApprovalRequestItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [userRole, setUserRole] = useState<string>('');

  // Reject modal state
  const [rejectModalOpen, setRejectModalOpen] = useState(false);
  const [selectedRequestId, setSelectedRequestId] = useState<number | null>(null);
  const [rejectComment, setRejectComment] = useState('');
  const [rejecting, setRejecting] = useState(false);

  // New Request modal state
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [requestType, setRequestType] = useState<string>('ChangeParentEmail');
  const [parentEmail, setParentEmail] = useState('');
  const [matriculaNumber, setMatriculaNumber] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  useEffect(() => {
    const userJson = localStorage.getItem('user');
    if (userJson) {
      try {
        const user = JSON.parse(userJson);
        setUserRole(user.role || '');
      } catch (e) {
        console.error(e);
      }
    }
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    setError(null);
    try {
      const [pendingRes, myRes] = await Promise.all([
        apiService.getPendingApprovalRequests().catch(() => ({ success: false, data: [] })),
        apiService.getMyApprovalRequests().catch(() => ({ success: false, data: [] })),
      ]);

      if (pendingRes.success && pendingRes.data) {
        setPendingRequests(pendingRes.data);
      }
      if (myRes.success && myRes.data) {
        setMyRequests(myRes.data);
      }
    } catch (err: any) {
      setError(err.message || 'Erro ao carregar solicitações.');
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (id: number) => {
    setLoading(true);
    try {
      await apiService.resolveApprovalRequest(id, { action: 'Approved' });
      await loadData();
    } catch (err: any) {
      setError(err.message || 'Erro ao aprovar solicitação.');
    } finally {
      setLoading(false);
    }
  };

  const handleOpenRejectModal = (id: number) => {
    setSelectedRequestId(id);
    setRejectComment('');
    setRejectModalOpen(true);
  };

  const handleConfirmReject = async () => {
    if (!selectedRequestId) return;
    setRejecting(true);
    try {
      await apiService.resolveApprovalRequest(selectedRequestId, {
        action: 'Rejected',
        comment: rejectComment,
      });
      setRejectModalOpen(false);
      await loadData();
    } catch (err: any) {
      setError(err.message || 'Erro ao rejeitar solicitação.');
    } finally {
      setRejecting(false);
    }
  };

  const handleLater = async (id: number) => {
    setLoading(true);
    try {
      await apiService.resolveApprovalRequest(id, { action: 'Later' });
      await loadData();
    } catch (err: any) {
      setError(err.message || 'Erro ao adiar solicitação.');
    } finally {
      setLoading(false);
    }
  };

  const handleCreateRequest = async () => {
    setCreateError(null);
    setSubmitting(true);
    try {
      let payloadJson = '{}';
      if (requestType === 'ChangeParentEmail') {
        if (!parentEmail.trim()) {
          setCreateError('O e-mail do novo superior é obrigatório.');
          setSubmitting(false);
          return;
        }
        payloadJson = JSON.stringify({ newParentEmail: parentEmail.trim() });
      } else {
        if (!matriculaNumber.trim()) {
          setCreateError('O número da matrícula é obrigatório.');
          setSubmitting(false);
          return;
        }
        payloadJson = JSON.stringify({ matriculaNumber: matriculaNumber.trim() });
      }

      await apiService.createApprovalRequest({
        requestType,
        payloadJson,
      });

      setCreateModalOpen(false);
      setParentEmail('');
      setMatriculaNumber('');
      await loadData();
    } catch (err: any) {
      setCreateError(err.message || 'Erro ao criar solicitação.');
    } finally {
      setSubmitting(false);
    }
  };

  const formatRequestType = (type: string) => {
    switch (type) {
      case 'ChangeParentEmail':
        return 'Alteração de Superior (ParentEmail)';
      case 'RequestMatricula':
        return 'Nova Matrícula (Usuário)';
      case 'AdminRequestMatricula':
        return 'Criação de Matrícula (Admin / Proprietário)';
      default:
        return type;
    }
  };

  const formatPayload = (type: string, jsonStr: string) => {
    try {
      const parsed = JSON.parse(jsonStr);
      if (type === 'ChangeParentEmail') {
        return `Novo Superior: ${parsed.newParentEmail || parsed.NewParentEmail}`;
      }
      if (type === 'RequestMatricula' || type === 'AdminRequestMatricula') {
        return `Matrícula: ${parsed.matriculaNumber || parsed.MatriculaNumber}`;
      }
      return jsonStr;
    } catch {
      return jsonStr;
    }
  };

  const renderStatusBadge = (status: string) => {
    switch (status) {
      case 'Approved':
        return <Badge color="green" variant="filled">Aprovado</Badge>;
      case 'Rejected':
        return <Badge color="red" variant="filled">Rejeitado</Badge>;
      case 'Pending':
        return <Badge color="yellow" variant="filled">Pendente</Badge>;
      default:
        return <Badge color="gray">{status}</Badge>;
    }
  };

  const isApprover = userRole === 'admin' || userRole === 'superadmin';

  return (
    <Container fluid className="requests-container">
      <div className="requests-header">
        <div>
          <Title order={2} c="dark">
            Central de Solicitações
          </Title>
          <Text size="sm" c="dimmed">
            Gerencie e acompanhe solicitações de alteração de dados e matrículas.
          </Text>
        </div>
        <Button
          leftSection={<IconPlus size={18} />}
          color="red"
          onClick={() => setCreateModalOpen(true)}
        >
          Nova Solicitação
        </Button>
      </div>

      {error && (
        <Alert icon={<IconAlertCircle size={16} />} title="Erro" color="red" mb="md" withCloseButton onClose={() => setError(null)}>
          {error}
        </Alert>
      )}

      <Card className="requests-card" style={{ position: 'relative' }}>
        <LoadingOverlay visible={loading} />

        <Tabs value={activeTab} onChange={setActiveTab}>
          <Tabs.List mb="md">
            {isApprover && (
              <Tabs.Tab value="pending" rightSection={pendingRequests.length > 0 ? <Badge size="xs" circle color="red">{pendingRequests.length}</Badge> : null}>
                Solicitações Pendentes
              </Tabs.Tab>
            )}
            <Tabs.Tab value="my-requests">
              Minhas Solicitações
            </Tabs.Tab>
          </Tabs.List>

          {isApprover && (
            <Tabs.Panel value="pending">
              {pendingRequests.length === 0 ? (
                <Text size="sm" c="dimmed" py="xl" ta="center">
                  Nenhuma solicitação pendente para aprovação.
                </Text>
              ) : (
                <Table striped highlightOnHover>
                  <Table.Thead>
                    <Table.Tr>
                      <Table.Th>Solicitante</Table.Th>
                      <Table.Th>Tipo</Table.Th>
                      <Table.Th>Detalhes</Table.Th>
                      <Table.Th>Data</Table.Th>
                      <Table.Th style={{ textAlign: 'right' }}>Ações</Table.Th>
                    </Table.Tr>
                  </Table.Thead>
                  <Table.Tbody>
                    {pendingRequests.map((req) => (
                      <Table.Tr key={req.id}>
                        <Table.Td>
                          <Text fw={500} size="sm">{req.requesterName}</Text>
                          <Text size="xs" c="dimmed">{req.requesterEmail}</Text>
                        </Table.Td>
                        <Table.Td>
                          <Badge color="blue" variant="light">
                            {formatRequestType(req.requestType)}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <span className="payload-details">
                            {formatPayload(req.requestType, req.payloadJson)}
                          </span>
                        </Table.Td>
                        <Table.Td>
                          <Text size="xs">
                            {new Date(req.createdAt).toLocaleDateString('pt-BR', {
                              day: '2-digit',
                              month: '2-digit',
                              year: 'numeric',
                              hour: '2-digit',
                              minute: '2-digit',
                            })}
                          </Text>
                        </Table.Td>
                        <Table.Td style={{ textAlign: 'right' }}>
                          <Group justify="flex-end" gap="xs">
                            <Button
                              size="xs"
                              color="green"
                              leftSection={<IconCheck size={14} />}
                              onClick={() => handleApprove(req.id)}
                            >
                              Sim
                            </Button>
                            <Button
                              size="xs"
                              color="red"
                              leftSection={<IconX size={14} />}
                              onClick={() => handleOpenRejectModal(req.id)}
                            >
                              Não
                            </Button>
                            <Button
                              size="xs"
                              color="gray"
                              variant="outline"
                              leftSection={<IconClock size={14} />}
                              onClick={() => handleLater(req.id)}
                            >
                              Depois
                            </Button>
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    ))}
                  </Table.Tbody>
                </Table>
              )}
            </Tabs.Panel>
          )}

          <Tabs.Panel value="my-requests">
            {myRequests.length === 0 ? (
              <Text size="sm" c="dimmed" py="xl" ta="center">
                Você ainda não realizou nenhuma solicitação.
              </Text>
            ) : (
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Tipo</Table.Th>
                    <Table.Th>Detalhes</Table.Th>
                    <Table.Th>Status</Table.Th>
                    <Table.Th>Aprovador / Comentário</Table.Th>
                    <Table.Th>Data</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {myRequests.map((req) => (
                    <Table.Tr key={req.id}>
                      <Table.Td>
                        <Badge color="blue" variant="light">
                          {formatRequestType(req.requestType)}
                        </Badge>
                      </Table.Td>
                      <Table.Td>
                        <span className="payload-details">
                          {formatPayload(req.requestType, req.payloadJson)}
                        </span>
                      </Table.Td>
                      <Table.Td>{renderStatusBadge(req.status)}</Table.Td>
                      <Table.Td>
                        {req.approverName && (
                          <Text size="xs" fw={500}>{req.approverName}</Text>
                        )}
                        {req.approverComment && (
                          <Text size="xs" c="dimmed">{req.approverComment}</Text>
                        )}
                        {!req.approverName && !req.approverComment && (
                          <Text size="xs" c="dimmed">-</Text>
                        )}
                      </Table.Td>
                      <Table.Td>
                        <Text size="xs">
                          {new Date(req.createdAt).toLocaleDateString('pt-BR', {
                            day: '2-digit',
                            month: '2-digit',
                            year: 'numeric',
                          })}
                        </Text>
                      </Table.Td>
                    </Table.Tr>
                  ))}
                </Table.Tbody>
              </Table>
            )}
          </Tabs.Panel>
        </Tabs>
      </Card>

      {/* Reject Modal */}
      <Modal
        opened={rejectModalOpen}
        onClose={() => setRejectModalOpen(false)}
        title="Rejeitar Solicitação"
        centered
      >
        <Text size="sm" mb="md">
          Por favor, informe um motivo para a rejeição da solicitação (opcional):
        </Text>
        <Textarea
          placeholder="Ex: Matrícula inválida ou e-mail incorreto"
          value={rejectComment}
          onChange={(e) => setRejectComment(e.target.value)}
          mb="md"
        />
        <Group justify="flex-end">
          <Button variant="default" onClick={() => setRejectModalOpen(false)}>
            Cancelar
          </Button>
          <Button color="red" loading={rejecting} onClick={handleConfirmReject}>
            Confirmar Rejeição
          </Button>
        </Group>
      </Modal>

      {/* Create Request Modal */}
      <Modal
        opened={createModalOpen}
        onClose={() => setCreateModalOpen(false)}
        title="Criar Nova Solicitação"
        centered
      >
        {createError && (
          <Alert icon={<IconAlertCircle size={16} />} color="red" mb="md">
            {createError}
          </Alert>
        )}

        <Select
          label="Tipo de Solicitação"
          data={[
            { value: 'ChangeParentEmail', label: 'Alteração de Superior (E-mail)' },
            { value: 'RequestMatricula', label: 'Solicitação de Nova Matrícula' },
            ...(userRole === 'admin'
              ? [{ value: 'AdminRequestMatricula', label: 'Criação de Matrícula (Admin / Proprietário)' }]
              : []),
          ]}
          value={requestType}
          onChange={(val) => setRequestType(val || 'ChangeParentEmail')}
          mb="md"
        />

        {requestType === 'ChangeParentEmail' ? (
          <TextInput
            label="E-mail do Novo Superior"
            placeholder="superior@exemplo.com"
            required
            value={parentEmail}
            onChange={(e) => setParentEmail(e.target.value)}
            mb="md"
          />
        ) : (
          <TextInput
            label="Número da Matrícula"
            placeholder="Ex: 123456"
            required
            value={matriculaNumber}
            onChange={(e) => setMatriculaNumber(e.target.value)}
            mb="md"
          />
        )}

        <Group justify="flex-end">
          <Button variant="default" onClick={() => setCreateModalOpen(false)}>
            Cancelar
          </Button>
          <Button color="red" leftSection={<IconSend size={16} />} loading={submitting} onClick={handleCreateRequest}>
            Enviar Solicitação
          </Button>
        </Group>
      </Modal>
    </Container>
  );
};

export default RequestsPage;
