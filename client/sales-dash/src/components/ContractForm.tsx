import React, { useState, useEffect } from 'react';
import { TextInput, NumberInput, Select, Button, Group, Text } from '@mantine/core';
import { normalizeNumber } from '../utils/normalization';
import {
  CreateContractRequest,
  UpdateContractRequest,
  Contract,
  ContractStatus,
  User,
  Group as ContractGroup,
  createContract,
  updateContract,
  getUsers,
  getGroups,
} from '../services/contractService';
import { apiService, PV } from '../services/apiService';
import { useContractsContext } from '../contexts/ContractsContext';
import { useCurrentUser } from '../contexts/CurrentUserContext';
import { toast } from '../utils/toast';
import StyledModal from './StyledModal';
import FormField from './FormField';
import { CONTRACT_STATUS_OPTIONS } from '../shared/ContractStatusBadge';
import { ContractType, ContractTypeLabels } from '../types/ContractType';

interface ContractFormProps {
  contract?: Contract | null;
  onClose: () => void;
  onSuccess: () => void;
}

const ContractForm: React.FC<ContractFormProps> = ({ contract, onClose, onSuccess }) => {
  const isEditMode = !!contract;
  
  // Get current user context
  const { currentUser } = useCurrentUser();
  const isUserAdmin = currentUser?.role?.toLowerCase() === 'admin';
  
  // Get cached data from context
  const { users: cachedUsers, groups: cachedGroups } = useContractsContext();

  const [formData, setFormData] = useState({
    contractNumber: contract?.contractNumber || '',
    userId: contract?.userId || '',
    groupId: contract?.groupId?.toString() || '',
    pvId: contract?.pvId?.toString() || '',
    totalAmount: contract?.totalAmount || 0,
    status: contract?.status || ContractStatus.Active,
    contractStartDate: contract?.contractStartDate?.split('T')[0] || '',
    isActive: contract?.isActive ?? true,
    contractType: contract?.contractType || '',
    quota: contract?.quota || 0,
    customerName: contract?.customerName || '',
    matriculaNumber: contract?.matriculaNumber || null,
  });

  const [users, setUsers] = useState<User[]>([]);
  const [groups, setGroups] = useState<ContractGroup[]>([]);
  const [pvs, setPVs] = useState<PV[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!currentUser) return;

    const fetchDropdownData = async () => {
      try {
        const [usersData, groupsData] = await Promise.all([
          getUsers(isUserAdmin),
          getGroups(),
        ]);
        
        let listUsers = usersData;
        if (contract && contract.userId && !usersData.some(u => u.id === contract.userId)) {
          listUsers = [...usersData, {
            id: contract.userId,
            name: contract.userName || 'Vendedor Atual',
            email: '',
            role: '',
            isActive: true,
            activeMatriculas: contract.matriculaNumber ? [{
              id: contract.userMatriculaId || 0,
              matriculaNumber: contract.matriculaNumber,
              isOwner: true,
              status: 'active',
              startDate: '',
              endDate: null
            }] : []
          } as unknown as User];
        }
        
        setUsers(listUsers);
        setGroups(groupsData);
        
        // Always fetch PVs (smaller dataset)
        try {
          const pvsResponse = await apiService.getPVs();
          if (pvsResponse.success && pvsResponse.data) {
            setPVs(pvsResponse.data);
          }
        } catch (pvErr) {
          console.warn('Failed to fetch PVs', pvErr);
        }
        
        // For new contracts, set default contract type
        if (!contract) {
          setFormData(prev => ({
            ...prev,
            contractType: ContractType.Lar, // Default to Lar
          }));
        }
      } catch (err: any) {
        const errorMessage = err.message || 'Falha ao carregar dados do formulário';
        setError(errorMessage);
        toast.error(errorMessage);
      }
    };

    fetchDropdownData();
  }, [cachedUsers, cachedGroups, contract, currentUser, isUserAdmin]);

  const handleChange = (name: string, value: any) => {
    if (name === 'userId') {
      const selectedUser = users.find((u) => u.id === value);
      const activeMatriculas = selectedUser?.activeMatriculas || [];
      const ownerMatriculas = activeMatriculas.filter((m) => m.isOwner);

      let defaultMatricula: string | null = null;
      if (ownerMatriculas.length === 1) {
        defaultMatricula = ownerMatriculas[0].matriculaNumber;
      }

      setFormData((prev) => ({
        ...prev,
        userId: value || '',
        matriculaNumber: defaultMatricula,
      }));
      return;
    }

    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const validateForm = (): boolean => {
    if (!formData.contractNumber.trim()) {
      const errorMessage = 'Número do contrato é obrigatório';
      setError(errorMessage);
      toast.error(errorMessage);
      return false;
    }

    if (formData.totalAmount < 0.01) {
      const errorMessage = 'Valor total deve ser pelo menos 0.01';
      setError(errorMessage);
      toast.error(errorMessage);
      return false;
    }

    if (!formData.contractStartDate) {
      const errorMessage = 'Data de início do contrato é obrigatória';
      setError(errorMessage);
      toast.error(errorMessage);
      return false;
    }

    if (formData.userId) {
      const selectedUser = users.find((u) => u.id === formData.userId);
      const activeMatriculas = selectedUser?.activeMatriculas || [];
      if (activeMatriculas.length === 0) {
        const errorMessage = 'Este usuário não possui matrícula, por favor vá em matrícula e atribua uma a ele antes de atribuir este contrato';
        setError(errorMessage);
        toast.error(errorMessage);
        return false;
      }
      if (!formData.matriculaNumber) {
        const errorMessage = 'Por favor, selecione uma matrícula para o vendedor';
        setError(errorMessage);
        toast.error(errorMessage);
        return false;
      }
    }

    return true;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!validateForm()) {
      return;
    }

    setLoading(true);

    try {
      if (isEditMode && contract) {
        const updateData: UpdateContractRequest = {
          contractNumber: normalizeNumber(formData.contractNumber),
          userId: formData.userId || null,
          groupId: formData.groupId ? parseInt(formData.groupId) : null,
          pvId: formData.pvId ? parseInt(formData.pvId) : null,
          totalAmount: Number(formData.totalAmount),
          status: formData.status as ContractStatus,
          contractStartDate: formData.contractStartDate,
          isActive: formData.isActive,
          contractType: formData.contractType || null,
          quota: formData.quota ? Number(formData.quota) : null,
          customerName: formData.customerName || null,
          matriculaNumber: normalizeNumber(formData.matriculaNumber) || null,
        };
        await updateContract(contract.id, updateData);
      } else {
        const createData: CreateContractRequest = {
          contractNumber: normalizeNumber(formData.contractNumber),
          userId: formData.userId || null,
          groupId: formData.groupId ? parseInt(formData.groupId) : null,
          pvId: formData.pvId ? parseInt(formData.pvId) : null,
          totalAmount: Number(formData.totalAmount),
          status: formData.status as ContractStatus,
          contractStartDate: formData.contractStartDate,
          contractType: formData.contractType || null,
          quota: formData.quota ? Number(formData.quota) : null,
          customerName: formData.customerName || null,
          matriculaNumber: normalizeNumber(formData.matriculaNumber) || null,
        };
        await createContract(createData);
      }

      toast.success(isEditMode ? 'Contrato atualizado com sucesso' : 'Contrato criado com sucesso');
      onSuccess();
      onClose();
    } catch (err: any) {
      const errorMessage = err.message || 'Falha ao salvar contrato';
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  };

  return (
    <StyledModal 
      opened={true} 
      onClose={onClose} 
      title={isEditMode ? 'Editar Contrato' : 'Criar Contrato'}
      size="lg"
    >
      <form onSubmit={handleSubmit}>
        {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

        <FormField label="Número do Contrato" required>
          <TextInput
            required
            value={formData.contractNumber}
            onChange={(e) => handleChange('contractNumber', e.target.value)}
            maxLength={50}
          />
        </FormField>

        <FormField label="Vendedor">
          <Select
            placeholder="Selecione o vendedor"
            data={[
              { value: '', label: 'Sem vendedor atribuído' },
              ...users.map((u) => ({
                value: u.id,
                label: `${u.name} (${u.email})`,
              })),
            ]}
            value={formData.userId}
            onChange={(value) => handleChange('userId', value)}
            searchable
            clearable
          />
          {(() => {
            if (!formData.userId) return null;
            const selectedUser = users.find((u) => u.id === formData.userId);
            const activeMatriculas = selectedUser?.activeMatriculas || [];
            
            if (activeMatriculas.length === 0) {
              return (
                <Text color="red" size="sm" mt="xs">
                  Este usuário não possui matrícula, por favor vá em matrícula e atribua uma a ele antes de atribuir este contrato
                </Text>
              );
            }
            
            return (
              <Text color="dimmed" size="xs" mt="xs">
                Matrículas associadas: {activeMatriculas.map((m) => `${m.matriculaNumber}${m.isOwner ? ' (Dona)' : ''}`).join(', ')}
              </Text>
            );
          })()}
        </FormField>

        <FormField label="Grupo (Opcional)">
          <Select
            value={formData.groupId}
            onChange={(value) => handleChange('groupId', value)}
            data={[
              { value: '', label: 'Nenhum' },
              ...groups.map(group => ({ value: group.id.toString(), label: group.name }))
            ]}
            clearable
          />
        </FormField>

        <FormField label="Ponto de Venda">
          <Select
            value={formData.pvId}
            onChange={(value) => handleChange('pvId', value)}
            data={[
              { value: '', label: 'Nenhum' },
              ...pvs.map(pv => ({ value: pv.id.toString(), label: pv.name }))
            ]}
            searchable
          />
        </FormField>

        <FormField label="Valor Total" required>
          <NumberInput
            required
            value={formData.totalAmount}
            onChange={(value) => handleChange('totalAmount', value)}
            min={0.01}
            decimalScale={2}
            fixedDecimalScale
            prefix="R$ "
          />
        </FormField>

        <FormField label="Status">
          <Select
            value={formData.status}
            onChange={(value) => handleChange('status', value)}
            data={CONTRACT_STATUS_OPTIONS}
          />
        </FormField>

        <FormField label="Data de Início" required>
          <input
            type="date"
            required
            value={formData.contractStartDate}
            onChange={(e) => handleChange('contractStartDate', e.target.value)}
            style={{
              width: '100%',
              padding: '8px',
              borderRadius: '4px',
              border: '1px solid #ced4da',
              fontSize: '14px'
            }}
          />
        </FormField>

        <FormField label="Tipo de Contrato">
          <Select
            value={formData.contractType}
            onChange={(value) => handleChange('contractType', value)}
            data={[
              { value: '', label: 'Selecione' },
              { value: ContractType.Lar, label: ContractTypeLabels[ContractType.Lar] },
              { value: ContractType.Motores, label: ContractTypeLabels[ContractType.Motores] },
            ]}
          />
        </FormField>

        <FormField label="Cota (Opcional)">
          <NumberInput
            value={formData.quota}
            onChange={(value) => handleChange('quota', value)}
            placeholder="Ex: 10"
          />
        </FormField>

        <FormField label="Nome do Cliente">
          <TextInput
            value={formData.customerName}
            onChange={(e) => handleChange('customerName', e.target.value)}
            placeholder="Ex: João Silva"
            maxLength={200}
          />
        </FormField>

        {(() => {
          if (!formData.userId) {
            return (
              <FormField label="Número da Matrícula (Opcional)">
                <TextInput
                  value=""
                  placeholder="Ex: MAT-001"
                  disabled
                />
              </FormField>
            );
          }

          const selectedUser = users.find((u) => u.id === formData.userId);
          const activeMatriculas = selectedUser?.activeMatriculas || [];

          if (activeMatriculas.length === 0) {
            return null;
          }

          return (
            <FormField label="Número da Matrícula" required>
              <Select
                placeholder="Selecione a matrícula"
                data={activeMatriculas.map((m) => ({
                  value: m.matriculaNumber,
                  label: `${m.matriculaNumber}${m.isOwner ? ' (Dona)' : ''}`,
                }))}
                value={formData.matriculaNumber}
                onChange={(value) => handleChange('matriculaNumber', value)}
                clearable
              />
            </FormField>
          );
        })()}


        <Group justify="flex-end" mt="xl">
          <Button variant="default" onClick={onClose} disabled={loading}>
            Cancelar
          </Button>
          <Button
            type="submit"
            loading={loading}
            disabled={(() => {
              if (!formData.userId) return false;
              const selectedUser = users.find((u) => u.id === formData.userId);
              return !selectedUser || !selectedUser.activeMatriculas || selectedUser.activeMatriculas.length === 0;
            })()}
          >
            {isEditMode ? 'Salvar Alterações' : 'Criar Contrato'}
          </Button>
        </Group>
      </form>
    </StyledModal>
  );
};

export default ContractForm;
