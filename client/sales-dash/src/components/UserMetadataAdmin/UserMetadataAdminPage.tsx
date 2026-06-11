import React, { useState, useEffect, useCallback } from "react";
import { Title, Button, Table, ActionIcon, Group, Badge, TextInput, Text, Select, Checkbox, NumberInput, Textarea } from '@mantine/core';
import { IconEdit, IconTrash, IconRefresh, IconPlus } from '@tabler/icons-react';
import "./UserMetadataAdminPage.css";
import Menu from "../Menu";
import StandardModal from '../../shared/StandardModal';
import { apiService, UserMetadataFieldDef } from "../../services/apiService";
import { toast } from "../../utils/toast";

const UserMetadataAdminPage: React.FC = () => {
  const [fields, setFields] = useState<UserMetadataFieldDef[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [search, setSearch] = useState("");
  const [searchDebounce, setSearchDebounce] = useState("");
  
  // Modal states
  const [showFormModal, setShowFormModal] = useState(false);
  const [editingField, setEditingField] = useState<UserMetadataFieldDef | undefined>(undefined);
  const [deleteConfirm, setDeleteConfirm] = useState<number | null>(null);

  // Form states
  const [key, setKey] = useState("");
  const [label, setLabel] = useState("");
  const [groupLabel, setGroupLabel] = useState("");
  const [fieldType, setFieldType] = useState<string>("text");
  const [dropdownOptions, setDropdownOptions] = useState("");
  const [displayOrder, setDisplayOrder] = useState<number>(0);
  const [isRequired, setIsRequired] = useState(false);
  const [saving, setSaving] = useState(false);

  // Fetch field definitions
  const fetchFields = useCallback(async () => {
    try {
      setLoading(true);
      setError("");
      const response = await apiService.getUserMetadataFields();

      if (response.success && response.data) {
        let filtered = response.data;
        
        // Client-side filtering
        if (searchDebounce) {
          const searchLower = searchDebounce.toLowerCase();
          filtered = filtered.filter(f => 
            f.key.toLowerCase().includes(searchLower) ||
            f.label.toLowerCase().includes(searchLower) ||
            (f.groupLabel && f.groupLabel.toLowerCase().includes(searchLower))
          );
        }
        
        setFields(filtered);
      }
    } catch (err: any) {
      setError(err.message || "Failed to load metadata fields");
      toast.error(err.message || "Failed to load metadata fields");
    } finally {
      setLoading(false);
    }
  }, [searchDebounce]);

  // Debounce search input
  useEffect(() => {
    const timer = setTimeout(() => {
      setSearchDebounce(search);
    }, 500);

    return () => clearTimeout(timer);
  }, [search]);

  useEffect(() => {
    fetchFields();
  }, [fetchFields]);

  // Reset form helper
  const resetForm = () => {
    setKey("");
    setLabel("");
    setGroupLabel("");
    setFieldType("text");
    setDropdownOptions("");
    setDisplayOrder(0);
    setIsRequired(false);
    setEditingField(undefined);
  };

  const handleOpenCreateModal = () => {
    resetForm();
    setShowFormModal(true);
  };

  const handleOpenEditModal = (field: UserMetadataFieldDef) => {
    setEditingField(field);
    setKey(field.key);
    setLabel(field.label);
    setGroupLabel(field.groupLabel || "");
    setFieldType(field.fieldType);
    
    // Format options as comma-separated or plain list for easier display/edit
    if (field.dropdownOptions) {
      try {
        const parsed = JSON.parse(field.dropdownOptions);
        if (Array.isArray(parsed)) {
          setDropdownOptions(parsed.join(", "));
        } else {
          setDropdownOptions("");
        }
      } catch {
        setDropdownOptions("");
      }
    } else {
      setDropdownOptions("");
    }
    
    setDisplayOrder(field.displayOrder);
    setIsRequired(field.isRequired);
    setShowFormModal(true);
  };

  const handleSubmitField = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!key.trim() || !label.trim()) {
      toast.error("Chave e Rótulo são obrigatórios");
      return;
    }

    setSaving(true);
    try {
      // Process dropdown options if type is dropdown
      let formattedOptions: string | null = null;
      if (fieldType === "dropdown") {
        const optionsList = dropdownOptions
          .split(",")
          .map(opt => opt.trim())
          .filter(opt => opt.length > 0);
        
        if (optionsList.length === 0) {
          toast.error("Para campos do tipo dropdown, digite pelo menos uma opção.");
          setSaving(false);
          return;
        }
        formattedOptions = JSON.stringify(optionsList);
      }

      const payload = {
        key: key.trim(),
        label: label.trim(),
        groupLabel: groupLabel.trim() || null,
        fieldType,
        dropdownOptions: formattedOptions,
        displayOrder,
        isRequired,
      };

      if (editingField) {
        await apiService.updateMetadataField(editingField.id, payload);
        toast.success("Campo atualizado com sucesso");
      } else {
        await apiService.createMetadataField(payload);
        toast.success("Campo criado com sucesso");
      }

      setShowFormModal(false);
      resetForm();
      fetchFields();
    } catch (err: any) {
      toast.error(err.message || "Erro ao salvar campo de metadados");
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteField = async (id: number) => {
    try {
      await apiService.deleteMetadataField(id);
      toast.success("Campo excluído com sucesso");
      setDeleteConfirm(null);
      fetchFields();
    } catch (err: any) {
      toast.error(err.message || "Erro ao excluir campo");
    }
  };

  return (
    <Menu>
      <div className="metadata-fields-container">
        <div className="metadata-fields-header">
          <div>
            <Title order={2} size="h2">Campos de Metadados Personalizados</Title>
            <p className="metadata-fields-subtitle">
              Configure campos adicionais para os perfis dos usuários.
            </p>
          </div>
          <Group className="metadata-fields-header-actions">
            <Button
              leftSection={<IconRefresh size={16} />}
              onClick={fetchFields}
              variant="light"
            >
              Atualizar
            </Button>
            <Button
              leftSection={<IconPlus size={16} />}
              onClick={handleOpenCreateModal}
            >
              Novo Campo
            </Button>
          </Group>
        </div>

        {error && <div className="error-message">{error}</div>}

        <div className="search-container">
          <TextInput
            placeholder="Buscar por chave, rótulo ou grupo..."
            value={search}
            onChange={(e) => setSearch(e.currentTarget.value)}
            style={{ maxWidth: 400 }}
          />
        </div>

        {loading ? (
          <div className="loading">Carregando campos de metadados...</div>
        ) : (
          <div className="table-container">
            <Table.ScrollContainer minWidth={800}>
              <Table striped highlightOnHover>
                <Table.Thead>
                  <Table.Tr>
                    <Table.Th>Rótulo (Label)</Table.Th>
                    <Table.Th>Chave (Key)</Table.Th>
                    <Table.Th>Grupo</Table.Th>
                    <Table.Th>Tipo</Table.Th>
                    <Table.Th>Obrigatório</Table.Th>
                    <Table.Th>Ordem</Table.Th>
                    <Table.Th>Ativo</Table.Th>
                    <Table.Th>Ações</Table.Th>
                  </Table.Tr>
                </Table.Thead>
                <Table.Tbody>
                  {fields.length === 0 ? (
                    <Table.Tr>
                      <Table.Td colSpan={8} style={{ textAlign: "center" }}>
                        Nenhum campo de metadados encontrado
                      </Table.Td>
                    </Table.Tr>
                  ) : (
                    fields.map((field) => (
                      <Table.Tr key={field.id} style={{ opacity: field.isActive ? 1 : 0.5 }}>
                        <Table.Td>
                          <strong>{field.label}</strong>
                        </Table.Td>
                        <Table.Td>
                          <code>{field.key}</code>
                        </Table.Td>
                        <Table.Td>{field.groupLabel || "-"}</Table.Td>
                        <Table.Td>
                          <Badge color={field.fieldType === "dropdown" ? "violet" : "blue"}>
                            {field.fieldType === "dropdown" ? "Dropdown" : "Texto"}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Badge color={field.isRequired ? "red" : "gray"}>
                            {field.isRequired ? "Sim" : "Não"}
                          </Badge>
                        </Table.Td>
                        <Table.Td>{field.displayOrder}</Table.Td>
                        <Table.Td>
                          <Badge color={field.isActive ? "green" : "gray"}>
                            {field.isActive ? "Ativo" : "Inativo"}
                          </Badge>
                        </Table.Td>
                        <Table.Td>
                          <Group gap="xs">
                            <ActionIcon
                              variant="light"
                              color="blue"
                              title="Editar"
                              onClick={() => handleOpenEditModal(field)}
                            >
                              <IconEdit size={16} />
                            </ActionIcon>
                            {field.isActive && (
                              <ActionIcon
                                variant="light"
                                color="red"
                                title="Inativar"
                                onClick={() => setDeleteConfirm(field.id)}
                              >
                                <IconTrash size={16} />
                              </ActionIcon>
                            )}
                          </Group>
                        </Table.Td>
                      </Table.Tr>
                    ))
                  )}
                </Table.Tbody>
              </Table>
            </Table.ScrollContainer>
          </div>
        )}

        {/* Create/Edit Form Modal */}
        <StandardModal
          isOpen={showFormModal}
          onClose={() => setShowFormModal(false)}
          title={editingField ? "Editar Campo de Metadados" : "Novo Campo de Metadados"}
          size="md"
        >
          <form onSubmit={handleSubmitField} style={{ display: 'flex', flexDirection: 'column', gap: '16px', padding: '10px 0' }}>
            <TextInput
              label="Chave (Identificador Único)"
              placeholder="ex: secretary_name"
              required
              disabled={!!editingField}
              value={key}
              onChange={(e) => setKey(e.currentTarget.value)}
            />

            <TextInput
              label="Rótulo (Exibido para o Usuário)"
              placeholder="ex: Nome da Secretária"
              required
              value={label}
              onChange={(e) => setLabel(e.currentTarget.value)}
            />

            <TextInput
              label="Grupo (Para agrupar campos visualmente)"
              placeholder="ex: Secretaria (opcional)"
              value={groupLabel}
              onChange={(e) => setGroupLabel(e.currentTarget.value)}
            />

            <Select
              label="Tipo de Campo"
              value={fieldType}
              onChange={(val) => setFieldType(val || "text")}
              data={[
                { value: "text", label: "Texto Livre (text)" },
                { value: "dropdown", label: "Seleção de Opções (dropdown)" }
              ]}
              required
            />

            {fieldType === "dropdown" && (
              <Textarea
                label="Opções de Seleção"
                placeholder="Insira as opções separadas por vírgula. Ex: Opção A, Opção B, Opção C"
                required
                value={dropdownOptions}
                onChange={(e) => setDropdownOptions(e.currentTarget.value)}
                rows={3}
              />
            )}

            <NumberInput
              label="Ordem de Exibição"
              placeholder="ex: 0"
              value={displayOrder}
              onChange={(val) => setDisplayOrder(Number(val))}
            />

            <Checkbox
              label="Campo Obrigatório"
              checked={isRequired}
              onChange={(e) => setIsRequired(e.currentTarget.checked)}
            />

            <Group justify="flex-end" mt="md">
              <Button variant="default" onClick={() => setShowFormModal(false)} disabled={saving}>
                Cancelar
              </Button>
              <Button type="submit" loading={saving}>
                Salvar
              </Button>
            </Group>
          </form>
        </StandardModal>

        {/* Delete Confirmation Modal */}
        <StandardModal
          isOpen={deleteConfirm !== null}
          onClose={() => setDeleteConfirm(null)}
          title="Confirmar Inativação"
          size="md"
          footer={
            <>
              <Button variant="default" onClick={() => setDeleteConfirm(null)}>
                Cancelar
              </Button>
              <Button
                color="red"
                onClick={() => handleDeleteField(deleteConfirm!)}
              >
                Inativar
              </Button>
            </>
          }
        >
          <div style={{ padding: '10px 0' }}>
            <Text size="sm">
              Tem certeza que deseja desativar este campo de metadados? Ele não será mais exibido nos perfis de usuários.
            </Text>
          </div>
        </StandardModal>
      </div>
    </Menu>
  );
};

export default UserMetadataAdminPage;
