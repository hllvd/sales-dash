import React, { useState, useEffect } from 'react';
import { TextInput, NumberInput, Select, Button, Group } from '@mantine/core';
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
    matriculaNumber: contract?.matriculaNumber || '',
  });

  const [users, setUsers] = useState<User[]>([]);
  const [groups, setGroups] = useState<ContractGroup[]>([]);
  const [pvs, setPVs] = useState<PV[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchDropdownData = async () => {
      try {
        // Use cached users and groups if available, otherwise fetch
        if (cachedUsers.length > 0 && cachedGroups.length > 0) {
          setUsers(cachedUsers);
          setGroups(cachedGroups);
        } else {
          const [usersData, groupsData] = await Promise.all([
            getUsers(),
            getGroups(),
          ]);
          setUsers(usersData);
          setGroups(groupsData);
        }
        
        // Always fetch PVs (smaller dataset)
        const pvsResponse = await apiService.getPVs();
        if (pvsResponse.success && pvsResponse.data) {
          setPVs(pvsResponse.data);
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
  }, [cachedUsers, cachedGroups, contract]);

  const handleChange = (name: string, value: any) => {
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
          contractNumber: formData.contractNumber,
          userId: formData.userId || undefined,
          groupId: formData.groupId ? parseInt(formData.groupId) : undefined,
          pvId: formData.pvId ? parseInt(formData.pvId) : undefined,
          totalAmount: Number(formData.totalAmount),
          status: formData.status as ContractStatus,
          contractStartDate: formData.contractStartDate,
          isActive: formData.isActive,
          contractType: formData.contractType || undefined,
          quota: formData.quota ? Number(formData.quota) : undefined,
          customerName: formData.customerName || undefined,
          matriculaNumber: formData.matriculaNumber || undefined,
        };
        await updateContract(contract.id, updateData);
      } else {
        const createData: CreateContractRequest = {
          contractNumber: formData.contractNumber,
          userId: formData.userId || undefined,
          groupId: formData.groupId ? parseInt(formData.groupId) : undefined,
          pvId: formData.pvId ? parseInt(formData.pvId) : undefined,
          totalAmount: Number(formData.totalAmount),
          status: formData.status as ContractStatus,
          contractStartDate: formData.contractStartDate,
          contractType: formData.contractType || undefined,
          quota: formData.quota ? Number(formData.quota) : undefined,
          customerName: formData.customerName || undefined,
          matriculaNumber: formData.matriculaNumber || undefined,
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

        <FormField label="Usuário">
          <Select
            value={formData.userId}
            onChange={(value) => handleChange('userId', value)}
            data={[
              { value: '', label: 'Sem usuário atribuído' },
              ...users.map(user => ({ value: user.id, label: `${user.name} (${user.email})` }))
            ]}
            searchable
          />
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



        <Group justify="flex-end" mt="xl">
          <Button variant="default" onClick={onClose} disabled={loading}>
            Cancelar
          </Button>
          <Button type="submit" loading={loading}>
            {isEditMode ? 'Salvar Alterações' : 'Criar Contrato'}
          </Button>
        </Group>
      </form>
    </StyledModal>
  );
};

export default ContractForm;
