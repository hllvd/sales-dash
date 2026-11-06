import React, { useState, useEffect, useMemo, useCallback } from 'react';
import {
  Title,
  Button,
  Table,
  ActionIcon,
  Group,
  Badge,
  TextInput,
  Text,
  Select,
  Modal,
  Alert,
  Stack,
  NumberInput,
  Tooltip,
} from '@mantine/core';
import {
  IconEdit,
  IconTrash,
  IconRefresh,
  IconPlus,
  IconUsersGroup,
  IconAlertCircle,
  IconRotateClockwise,
} from '@tabler/icons-react';
import { notifications } from '@mantine/notifications';
import Menu from './Menu';
import {
  Group as ContractGroup,
  getGroups,
  createGroup,
  updateGroup,
  deleteGroup,
} from '../services/contractService';
import { Segment, SegmentLabels, getSegmentLabel } from '../types/Segment';
import './GroupsPage.css';

const SEGMENT_OPTIONS = [
  { value: '', label: 'Nenhum' },
  { value: String(Segment.Lar), label: SegmentLabels[Segment.Lar] },
  { value: String(Segment.Motores), label: SegmentLabels[Segment.Motores] },
  { value: String(Segment.Servicos), label: SegmentLabels[Segment.Servicos] },
];

export const sortGroupsBySegmentAndName = (a: ContractGroup, b: ContractGroup): number => {
  const aHasSegment = a.segment != null;
  const bHasSegment = b.segment != null;

  // Groups without segment come first
  if (!aHasSegment && bHasSegment) return -1;
  if (aHasSegment && !bHasSegment) return 1;

  // If both have segments, sort numerically by segment enum value
  if (aHasSegment && bHasSegment && a.segment !== b.segment) {
    return (a.segment as number) - (b.segment as number);
  }

  // Secondary sort by group name (natural alphanumeric comparison)
  return (a.name || '').localeCompare(b.name || '', 'pt-BR', { numeric: true, sensitivity: 'base' });
};

const GroupsPage: React.FC = () => {
  const [groups, setGroups] = useState<ContractGroup[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [activeFilter, setActiveFilter] = useState<string>('active');

  // Modal State (Create / Edit)
  const [modalOpen, setModalOpen] = useState(false);
  const [editingGroup, setEditingGroup] = useState<ContractGroup | null>(null);
  const [formName, setFormName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [formCommission, setFormCommission] = useState<number | ''>(0);
  const [formSegment, setFormSegment] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Deactivate Confirm State
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const [groupToDelete, setGroupToDelete] = useState<ContractGroup | null>(null);
  const [deleting, setDeleting] = useState(false);

  const fetchGroups = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getGroups(true); // fetch all including inactive
      setGroups(data);
    } catch (err: any) {
      setError(err.message || 'Erro ao carregar grupos');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchGroups();
  }, [fetchGroups]);

  const handleOpenCreate = () => {
    setEditingGroup(null);
    setFormName('');
    setFormDescription('');
    setFormCommission(0);
    setFormSegment('');
    setFormError(null);
    setModalOpen(true);
  };

  const handleOpenEdit = (group: ContractGroup) => {
    setEditingGroup(group);
    setFormName(group.name);
    setFormDescription(group.description || '');
    setFormCommission(group.commission ?? 0);
    setFormSegment(group.segment ? String(group.segment) : '');
    setFormError(null);
    setModalOpen(true);
  };

  const handleCloseModal = () => {
    setModalOpen(false);
    setEditingGroup(null);
    setFormError(null);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formName.trim()) {
      setFormError('O nome do grupo é obrigatório.');
      return;
    }

    setSubmitting(true);
    setFormError(null);

    const segmentValue = formSegment ? parseInt(formSegment, 10) : null;
    const commissionValue = typeof formCommission === 'number' ? formCommission : 0;

    try {
      if (editingGroup) {
        await updateGroup(editingGroup.id, {
          name: formName.trim(),
          description: formDescription.trim(),
          commission: commissionValue,
          segment: segmentValue,
        });
        notifications.show({
          title: 'Sucesso',
          message: 'Grupo atualizado com sucesso!',
          color: 'green',
        });
      } else {
        await createGroup({
          name: formName.trim(),
          description: formDescription.trim(),
          commission: commissionValue,
          segment: segmentValue,
        });
        notifications.show({
          title: 'Sucesso',
          message: 'Grupo criado com sucesso!',
          color: 'green',
        });
      }
      handleCloseModal();
      await fetchGroups();
    } catch (err: any) {
      setFormError(err.message || 'Erro ao salvar grupo');
    } finally {
      setSubmitting(false);
    }
  };

  const handleOpenDelete = (group: ContractGroup) => {
    setGroupToDelete(group);
    setDeleteConfirmOpen(true);
  };

  const handleConfirmDelete = async () => {
    if (!groupToDelete) return;
    setDeleting(true);
    try {
      await deleteGroup(groupToDelete.id);
      notifications.show({
        title: 'Sucesso',
        message: 'Grupo desativado com sucesso!',
        color: 'green',
      });
      setDeleteConfirmOpen(false);
      setGroupToDelete(null);
      await fetchGroups();
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao desativar grupo',
        color: 'red',
      });
    } finally {
      setDeleting(false);
    }
  };

  const handleReactivate = async (group: ContractGroup) => {
    try {
      await updateGroup(group.id, { isActive: true });
      notifications.show({
        title: 'Sucesso',
        message: 'Grupo reativado com sucesso!',
        color: 'green',
      });
      await fetchGroups();
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao reativar grupo',
        color: 'red',
      });
    }
  };

  const filteredGroups = useMemo(() => {
    const list = groups.filter((group) => {
      // Active filter
      if (activeFilter === 'active' && !group.isActive) return false;
      if (activeFilter === 'inactive' && group.isActive) return false;

      // Search filter
      if (search.trim()) {
        const query = search.toLowerCase();
        const matchesName = group.name?.toLowerCase().includes(query);
        const matchesDesc = group.description?.toLowerCase().includes(query);
        const segmentName = getSegmentLabel(group.segment).toLowerCase();
        const matchesSegment = segmentName.includes(query);
        return matchesName || matchesDesc || matchesSegment;
      }

      return true;
    });

    return [...list].sort(sortGroupsBySegmentAndName);
  }, [groups, activeFilter, search]);

  return (
    <Menu>
      <div className="groups-container">
        <div className="groups-header">
          <div>
            <Title order={2} size="h2" style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <IconUsersGroup size={28} />
              Gerenciamento de Grupos
            </Title>
            <p className="groups-subtitle">
              {groups.length} {groups.length === 1 ? 'grupo' : 'grupos'}{' '}
              {activeFilter === 'active' ? 'ativo' : activeFilter === 'inactive' ? 'inativo' : 'cadastrado'}
              {groups.length === 1 ? '' : 's'}. Configure grupos e seus respectivos segmentos.
            </p>
          </div>
          <Group gap="xs">
            <Button
              variant="default"
              leftSection={<IconRefresh size={16} />}
              onClick={fetchGroups}
              loading={loading}
            >
              Atualizar
            </Button>
            <Button
              leftSection={<IconPlus size={16} />}
              onClick={handleOpenCreate}
            >
              Novo Grupo
            </Button>
          </Group>
        </div>

        {error && (
          <Alert
            icon={<IconAlertCircle size={16} />}
            title="Erro"
            color="red"
            mb="md"
            withCloseButton
            onClose={() => setError(null)}
          >
            {error}
          </Alert>
        )}

        {/* Filters Bar */}
        <div className="groups-search-bar">
          <Group justify="space-between" align="center">
            <TextInput
              placeholder="Buscar por nome, descrição ou segmento..."
              value={search}
              onChange={(e) => setSearch(e.currentTarget.value)}
              style={{ width: '380px' }}
            />

            <Select
              value={activeFilter}
              onChange={(val) => setActiveFilter(val || 'active')}
              data={[
                { value: 'active', label: 'Somente Ativos' },
                { value: 'inactive', label: 'Somente Inativos' },
                { value: 'all', label: 'Todos os Status' },
              ]}
              style={{ width: '180px' }}
              allowDeselect={false}
            />
          </Group>
        </div>

        {/* Groups Table Card Container */}
        <div className="groups-table-container">
          <Table.ScrollContainer minWidth={700}>
            <Table highlightOnHover withTableBorder>
              <Table.Thead>
                <Table.Tr>
                  <Table.Th style={{ width: '80px' }}>ID</Table.Th>
                  <Table.Th>Nome</Table.Th>
                  <Table.Th>Descrição</Table.Th>
                  <Table.Th>Segmento</Table.Th>
                  <Table.Th>Comissão</Table.Th>
                  <Table.Th>Status</Table.Th>
                  <Table.Th style={{ width: '120px', textAlign: 'center' }}>Ações</Table.Th>
                </Table.Tr>
              </Table.Thead>
              <Table.Tbody>
                {loading ? (
                  <Table.Tr>
                    <Table.Td colSpan={7} style={{ textAlign: 'center', padding: '32px' }}>
                      <Text c="dimmed">Carregando grupos...</Text>
                    </Table.Td>
                  </Table.Tr>
                ) : filteredGroups.length === 0 ? (
                  <Table.Tr>
                    <Table.Td colSpan={7} style={{ textAlign: 'center', padding: '32px' }}>
                      <Text c="dimmed">Nenhum grupo encontrado.</Text>
                    </Table.Td>
                  </Table.Tr>
                ) : (
                  filteredGroups.map((group) => {
                    const segmentLabel = getSegmentLabel(group.segment);
                    const isUnassigned = group.segment == null;
                    return (
                      <Table.Tr
                        key={group.id}
                        className={isUnassigned ? 'group-row-unassigned-segment' : undefined}
                      >
                        <Table.Td>{group.id}</Table.Td>
                        <Table.Td style={{ fontWeight: 600 }}>{group.name}</Table.Td>
                        <Table.Td>{group.description || '-'}</Table.Td>
                        <Table.Td>
                          {segmentLabel ? (
                            <Badge
                              color={
                                group.segment === Segment.Lar
                                  ? 'teal'
                                  : group.segment === Segment.Motores
                                  ? 'blue'
                                  : 'orange'
                              }
                              variant="light"
                            >
                              {segmentLabel}
                            </Badge>
                          ) : (
                            <Badge color="red" variant="light" size="sm">
                              Não Definido
                            </Badge>
                          )}
                        </Table.Td>
                        <Table.Td>{group.commission != null ? `${group.commission}%` : '-'}</Table.Td>
                        <Table.Td>
                          <Badge color={group.isActive ? 'green' : 'gray'} variant="light">
                            {group.isActive ? 'Ativo' : 'Inativo'}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Group gap="xs" justify="center">
                            <Tooltip label="Editar Grupo">
                              <ActionIcon
                                variant="subtle"
                                color="blue"
                                onClick={() => handleOpenEdit(group)}
                              >
                                <IconEdit size={16} />
                              </ActionIcon>
                            </Tooltip>

                            {group.isActive ? (
                              <Tooltip label="Desativar Grupo">
                                <ActionIcon
                                  variant="subtle"
                                  color="red"
                                  onClick={() => handleOpenDelete(group)}
                                >
                                  <IconTrash size={16} />
                                </ActionIcon>
                              </Tooltip>
                            ) : (
                              <Tooltip label="Reativar Grupo">
                                <ActionIcon
                                  variant="subtle"
                                  color="green"
                                  onClick={() => handleReactivate(group)}
                                >
                                  <IconRotateClockwise size={16} />
                                </ActionIcon>
                              </Tooltip>
                            )}
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    );
                  })
                )}
              </Table.Tbody>
            </Table>
          </Table.ScrollContainer>
        </div>

        {/* Create / Edit Modal */}
        <Modal
          opened={modalOpen}
          onClose={handleCloseModal}
          title={
            <Title order={3} component="span" style={{ color: '#1c1c1e', fontWeight: 700 }}>
              {editingGroup ? 'Editar Grupo' : 'Novo Grupo'}
            </Title>
          }
          centered
        >
          <form onSubmit={handleSave}>
            <Stack gap="md">
              {formError && (
                <Alert icon={<IconAlertCircle size={16} />} color="red">
                  {formError}
                </Alert>
              )}

              <TextInput
                label="Nome do Grupo"
                placeholder="Ex: 101, Grupo Alfa..."
                value={formName}
                onChange={(e) => setFormName(e.currentTarget.value)}
                required
                data-autofocus
              />

              <TextInput
                label="Descrição"
                placeholder="Descrição ou detalhe do grupo (opcional)"
                value={formDescription}
                onChange={(e) => setFormDescription(e.currentTarget.value)}
              />

              <Select
                label="Segmento"
                placeholder="Selecione o segmento"
                value={formSegment}
                onChange={(val) => setFormSegment(val || '')}
                data={SEGMENT_OPTIONS}
                clearable
              />

              <NumberInput
                label="Comissão (%)"
                placeholder="Ex: 10"
                value={formCommission}
                onChange={(val) => setFormCommission(typeof val === 'number' ? val : '')}
                min={0}
                max={100}
                decimalScale={2}
                suffix="%"
              />

              <Group justify="flex-end" mt="md">
                <Button variant="default" onClick={handleCloseModal} disabled={submitting}>
                  Cancelar
                </Button>
                <Button type="submit" loading={submitting}>
                  {editingGroup ? 'Salvar Alterações' : 'Cadastrar'}
                </Button>
              </Group>
            </Stack>
          </form>
        </Modal>

        {/* Deactivate Confirm Modal */}
        <Modal
          opened={deleteConfirmOpen}
          onClose={() => setDeleteConfirmOpen(false)}
          title={
            <Title order={3} component="span" style={{ color: '#1c1c1e', fontWeight: 700 }}>
              Desativar Grupo
            </Title>
          }
          centered
        >
          <Stack gap="md">
            <Text size="sm">
              Tem certeza de que deseja desativar o grupo{' '}
              <strong>{groupToDelete?.name}</strong>?
            </Text>
            <Text size="xs" c="dimmed">
              O grupo deixará de ser selecionável em novos contratos, mas os contratos existentes continuarão associados a ele.
            </Text>

            <Group justify="flex-end" mt="md">
              <Button
                variant="default"
                onClick={() => setDeleteConfirmOpen(false)}
                disabled={deleting}
              >
                Cancelar
              </Button>
              <Button
                color="red"
                onClick={handleConfirmDelete}
                loading={deleting}
              >
                Desativar
              </Button>
            </Group>
          </Stack>
        </Modal>
      </div>
    </Menu>
  );
};

export default GroupsPage;
