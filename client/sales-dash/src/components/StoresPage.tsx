import React, { useState, useEffect, useMemo, useCallback } from "react";
import { Title, Button, Table, ActionIcon, Group, Badge, TextInput, Text, Select, Modal, Alert, Stack, Switch } from '@mantine/core';
import { IconEdit, IconTrash, IconRefresh, IconPlus, IconBuildingStore, IconAlertCircle } from '@tabler/icons-react';
import dayjs from 'dayjs';
import Menu from "./Menu";
import { apiService, Store } from "../services/apiService";

export const BRAZILIAN_STATES = [
  { value: 'AC', label: 'Acre (AC)' },
  { value: 'AL', label: 'Alagoas (AL)' },
  { value: 'AP', label: 'Amapá (AP)' },
  { value: 'AM', label: 'Amazonas (AM)' },
  { value: 'BA', label: 'Bahia (BA)' },
  { value: 'CE', label: 'Ceará (CE)' },
  { value: 'DF', label: 'Distrito Federal (DF)' },
  { value: 'ES', label: 'Espírito Santo (ES)' },
  { value: 'GO', label: 'Goiás (GO)' },
  { value: 'MA', label: 'Maranhão (MA)' },
  { value: 'MT', label: 'Mato Grosso (MT)' },
  { value: 'MS', label: 'Mato Grosso do Sul (MS)' },
  { value: 'MG', label: 'Minas Gerais (MG)' },
  { value: 'PA', label: 'Pará (PA)' },
  { value: 'PB', label: 'Paraíba (PB)' },
  { value: 'PR', label: 'Paraná (PR)' },
  { value: 'PE', label: 'Pernambuco (PE)' },
  { value: 'PI', label: 'Piauí (PI)' },
  { value: 'RJ', label: 'Rio de Janeiro (RJ)' },
  { value: 'RN', label: 'Rio Grande do Norte (RN)' },
  { value: 'RS', label: 'Rio Grande do Sul (RS)' },
  { value: 'RO', label: 'Rondônia (RO)' },
  { value: 'RR', label: 'Roraima (RR)' },
  { value: 'SC', label: 'Santa Catarina (SC)' },
  { value: 'SP', label: 'São Paulo (SP)' },
  { value: 'SE', label: 'Sergipe (SE)' },
  { value: 'TO', label: 'Tocantins (TO)' }
];

export const getStateLabel = (uf: string) => {
  const found = BRAZILIAN_STATES.find(s => s.value === uf);
  return found ? found.label : uf;
};

const StoresPage: React.FC = () => {
  const [stores, setStores] = useState<Store[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [activeFilter, setActiveFilter] = useState<string>("active");

  // Modal State
  const [modalOpen, setModalOpen] = useState(false);
  const [editingStore, setEditingStore] = useState<Store | null>(null);
  const [formName, setFormName] = useState("");
  const [formState, setFormState] = useState<string | null>("PR");
  const [formIsActive, setFormIsActive] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [formError, setFormError] = useState<string | null>(null);

  // Delete Confirm State
  const [deleteConfirmOpen, setDeleteConfirmOpen] = useState(false);
  const [storeToDelete, setStoreToDelete] = useState<Store | null>(null);
  const [deleting, setDeleting] = useState(false);

  const fetchStores = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await apiService.getStores();
      if (res.success && res.data) {
        setStores(res.data);
      } else {
        setError(res.message || "Erro ao carregar lojas");
      }
    } catch (err: any) {
      setError(err.message || "Erro de conexão ao carregar lojas");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchStores();
  }, [fetchStores]);

  const handleOpenCreate = () => {
    setEditingStore(null);
    setFormName("");
    setFormState("PR");
    setFormIsActive(true);
    setFormError(null);
    setModalOpen(true);
  };

  const handleOpenEdit = (store: Store) => {
    setEditingStore(store);
    setFormName(store.name);
    setFormState(store.state);
    setFormIsActive(store.isActive);
    setFormError(null);
    setModalOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!formName.trim()) {
      setFormError("Informe o nome da loja");
      return;
    }
    if (!formState) {
      setFormError("Selecione o estado");
      return;
    }

    setSubmitting(true);
    setFormError(null);

    try {
      if (editingStore) {
        const res = await apiService.updateStore(editingStore.id, {
          name: formName.trim(),
          state: formState,
          isActive: formIsActive
        });
        if (res.success) {
          setModalOpen(false);
          fetchStores();
        } else {
          setFormError(res.message || "Erro ao atualizar loja");
        }
      } else {
        const res = await apiService.createStore({
          name: formName.trim(),
          state: formState
        });
        if (res.success) {
          setModalOpen(false);
          fetchStores();
        } else {
          setFormError(res.message || "Erro ao criar loja");
        }
      }
    } catch (err: any) {
      setFormError(err.message || "Erro ao salvar loja");
    } finally {
      setSubmitting(false);
    }
  };

  const handleConfirmDelete = (store: Store) => {
    setStoreToDelete(store);
    setDeleteConfirmOpen(true);
  };

  const handleDelete = async () => {
    if (!storeToDelete) return;
    setDeleting(true);
    try {
      const res = await apiService.deleteStore(storeToDelete.id);
      if (res.success) {
        setDeleteConfirmOpen(false);
        setStoreToDelete(null);
        fetchStores();
      } else {
        alert(res.message || "Erro ao excluir loja");
      }
    } catch (err: any) {
      alert(err.message || "Erro ao excluir loja");
    } finally {
      setDeleting(false);
    }
  };

  const filteredStores = useMemo(() => {
    let list = stores;

    if (activeFilter === "active") {
      list = list.filter(s => s.isActive);
    } else if (activeFilter === "inactive") {
      list = list.filter(s => !s.isActive);
    }

    if (search.trim()) {
      const query = search.trim().toLowerCase();
      list = list.filter(s => {
        const nameMatch = s.name.toLowerCase().includes(query);
        const ufMatch = s.state.toLowerCase().includes(query);
        const stateNameMatch = getStateLabel(s.state).toLowerCase().includes(query);
        return nameMatch || ufMatch || stateNameMatch;
      });
    }

    return list;
  }, [stores, activeFilter, search]);

  return (
    <div style={{ display: 'flex', minHeight: '100vh', backgroundColor: '#f8f9fa' }}>
      <Menu />
      <div style={{ flex: 1, padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
        <Group justify="space-between" mb="lg">
          <div>
            <Group gap="xs">
              <IconBuildingStore size={28} color="#e03131" />
              <Title order={2}>Lojas</Title>
              <Badge color="red" variant="light" size="lg">{stores.length}</Badge>
            </Group>
            <Text c="dimmed" size="sm" mt={4}>
              Gerenciamento de unidades e lojas da empresa
            </Text>
          </div>

          <Group gap="xs">
            <Button
              variant="default"
              leftSection={<IconRefresh size={16} />}
              onClick={fetchStores}
              loading={loading}
            >
              Atualizar
            </Button>
            <Button
              color="red"
              leftSection={<IconPlus size={16} />}
              onClick={handleOpenCreate}
            >
              Nova Loja
            </Button>
          </Group>
        </Group>

        {error && (
          <Alert icon={<IconAlertCircle size={16} />} color="red" mb="md">
            {error}
          </Alert>
        )}

        <Group mb="md">
          <TextInput
            placeholder="Buscar por nome ou estado..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ flex: 1 }}
          />
          <Select
            value={activeFilter}
            onChange={(val) => setActiveFilter(val || "all")}
            data={[
              { value: "all", label: "Todas as lojas" },
              { value: "active", label: "Ativas" },
              { value: "inactive", label: "Inativas" }
            ]}
            style={{ width: 180 }}
          />
        </Group>

        <Table highlightOnHover withTableBorder withColumnBorders bg="white">
          <Table.Thead>
            <Table.Tr>
              <Table.Th>Nome</Table.Th>
              <Table.Th>Estado</Table.Th>
              <Table.Th>Status</Table.Th>
              <Table.Th>Criado Em</Table.Th>
              <Table.Th>Atualizado Em</Table.Th>
              <Table.Th style={{ width: 100, textAlign: 'center' }}>Ações</Table.Th>
            </Table.Tr>
          </Table.Thead>
          <Table.Tbody>
            {loading ? (
              <Table.Tr>
                <Table.Td colSpan={6} align="center">Carregando...</Table.Td>
              </Table.Tr>
            ) : filteredStores.length === 0 ? (
              <Table.Tr>
                <Table.Td colSpan={6} align="center">
                  <Text c="dimmed" py="md">Nenhuma loja encontrada.</Text>
                </Table.Td>
              </Table.Tr>
            ) : (
              filteredStores.map(store => (
                <Table.Tr key={store.id}>
                  <Table.Td>
                    <Text fw={600}>{store.name}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Badge variant="outline" color="blue">
                      {getStateLabel(store.state)}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    <Badge color={store.isActive ? "green" : "gray"}>
                      {store.isActive ? "Ativa" : "Inativa"}
                    </Badge>
                  </Table.Td>
                  <Table.Td>
                    <Text size="sm">{dayjs(store.createdAt).format('DD/MM/YYYY HH:mm')}</Text>
                  </Table.Td>
                  <Table.Td>
                    <Text size="sm">{dayjs(store.updatedAt).format('DD/MM/YYYY HH:mm')}</Text>
                  </Table.Td>
                  <Table.Td align="center">
                    <Group gap={4} justify="center">
                      <ActionIcon variant="subtle" color="blue" onClick={() => handleOpenEdit(store)}>
                        <IconEdit size={16} />
                      </ActionIcon>
                      <ActionIcon variant="subtle" color="red" onClick={() => handleConfirmDelete(store)}>
                        <IconTrash size={16} />
                      </ActionIcon>
                    </Group>
                  </Table.Td>
                </Table.Tr>
              ))
            )}
          </Table.Tbody>
        </Table>

        {/* Create / Edit Modal */}
        <Modal
          opened={modalOpen}
          onClose={() => setModalOpen(false)}
          title={editingStore ? "Editar Loja" : "Nova Loja"}
          centered
        >
          <form onSubmit={handleSubmit}>
            <Stack gap="md">
              {formError && (
                <Alert icon={<IconAlertCircle size={16} />} color="red">
                  {formError}
                </Alert>
              )}

              <TextInput
                label="Nome da Loja"
                placeholder="Ex: BALNEARIO CAMBORIU"
                required
                value={formName}
                onChange={(e) => setFormName(e.target.value)}
              />

              <Select
                label="Estado (UF)"
                placeholder="Selecione o estado"
                required
                searchable
                data={BRAZILIAN_STATES}
                value={formState}
                onChange={(val) => setFormState(val)}
              />

              {editingStore && (
                <Switch
                  label="Loja Ativa"
                  checked={formIsActive}
                  onChange={(e) => setFormIsActive(e.currentTarget.checked)}
                />
              )}

              <Group justify="flex-end" mt="md">
                <Button variant="default" onClick={() => setModalOpen(false)}>Cancelar</Button>
                <Button color="red" type="submit" loading={submitting}>
                  {editingStore ? "Salvar Alterações" : "Criar Loja"}
                </Button>
              </Group>
            </Stack>
          </form>
        </Modal>

        {/* Delete Confirmation Modal */}
        <Modal
          opened={deleteConfirmOpen}
          onClose={() => setDeleteConfirmOpen(false)}
          title="Confirmar Exclusão"
          centered
        >
          <Text size="sm" mb="lg">
            Tem certeza que deseja excluir a loja <b>{storeToDelete?.name}</b>?
            Equipes vinculadas a esta loja ficarão sem loja atribuída.
          </Text>
          <Group justify="flex-end">
            <Button variant="default" onClick={() => setDeleteConfirmOpen(false)}>Cancelar</Button>
            <Button color="red" onClick={handleDelete} loading={deleting}>Excluir</Button>
          </Group>
        </Modal>
      </div>
    </div>
  );
};

export default StoresPage;
