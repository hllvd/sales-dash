import React, { useState, useEffect } from 'react';
import { Modal, TextInput, NumberInput, Select, Button, Group, Title } from '@mantine/core';
import {
  CreateContractRequest,
  UpdateContractRequest,
  Contract,
  User,
  Group as ContractGroup,
  createContract,
  updateContract,
  getUsers,
  getGroups,
} from '../services/contractService';
import { apiService, PV } from '../services/apiService';

interface ContractFormProps {
  contract?: Contract | null;
  onClose: () => void;
  onSuccess: () => void;
}

const ContractForm: React.FC<ContractFormProps> = ({ contract, onClose, onSuccess }) => {
  const isEditMode = !!contract;

  const [formData, setFormData] = useState({
    contractNumber: contract?.contractNumber || '',
    userId: contract?.userId || '',
    groupId: contract?.groupId?.toString() || '0',
    pvId: contract?.pvId?.toString() || '',
    totalAmount: contract?.totalAmount || 0,
    status: contract?.status || 'Active',
    contractStartDate: contract?.contractStartDate?.split('T')[0] || '',
    contractEndDate: contract?.contractEndDate?.split('T')[0] || '',
    isActive: contract?.isActive ?? true,
    contractType: contract?.contractType?.toString() || '',
    quota: contract?.quota || 0,
    customerName: contract?.customerName || '',
  });

  const [users, setUsers] = useState<User[]>([]);
  const [groups, setGroups] = useState<ContractGroup[]>([]);
  const [pvs, setPVs] = useState<PV[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchDropdownData = async () => {
      try {
        const [usersData, groupsData, pvsResponse] = await Promise.all([
          getUsers(),
          getGroups(),
          apiService.getPVs(),
        ]);
        setUsers(usersData);
        setGroups(groupsData);
        if (pvsResponse.success && pvsResponse.data) {
          setPVs(pvsResponse.data);
        }
        
        // For new contracts, set default contract type
        if (!contract) {
          setFormData(prev => ({
            ...prev,
            contractType: '0', // First option: Lar
          }));
        }
      } catch (err) {
        setError('Failed to load users, groups, and PVs');
      }
    };

    fetchDropdownData();
  }, []);

  const handleChange = (name: string, value: any) => {
    setFormData((prev) => ({
      ...prev,
      [name]: value,
    }));
  };

  const validateForm = (): boolean => {
    if (!formData.contractNumber.trim()) {
      setError('Contract number is required');
      return false;
    }

    if (formData.totalAmount < 0.01) {
      setError('Total amount must be at least 0.01');
      return false;
    }

    if (!formData.contractStartDate) {
      setError('Contract start date is required');
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
          groupId: formData.groupId ? parseInt(formData.groupId) : 0,
          pvId: formData.pvId ? parseInt(formData.pvId) : undefined,
          totalAmount: Number(formData.totalAmount),
          status: formData.status as 'Active' | 'Late1' | 'Late2' | 'Late3' | 'Defaulted',
          contractStartDate: formData.contractStartDate,
          contractEndDate: formData.contractEndDate || null,
          isActive: formData.isActive,
          contractType: formData.contractType ? parseInt(formData.contractType) : undefined,
          quota: formData.quota ? Number(formData.quota) : undefined,
          customerName: formData.customerName || undefined,
        };
        await updateContract(contract.id, updateData);
      } else {
        const createData: CreateContractRequest = {
          contractNumber: formData.contractNumber,
          userId: formData.userId || undefined,
          groupId: formData.groupId ? parseInt(formData.groupId) : 0,
          pvId: formData.pvId ? parseInt(formData.pvId) : undefined,
          totalAmount: Number(formData.totalAmount),
          status: formData.status as 'Active' | 'Late1' | 'Late2' | 'Late3' | 'Defaulted',
          contractStartDate: formData.contractStartDate,
          contractEndDate: formData.contractEndDate || null,
          contractType: formData.contractType ? parseInt(formData.contractType) : undefined,
          quota: formData.quota ? Number(formData.quota) : undefined,
          customerName: formData.customerName || undefined,
        };
        await createContract(createData);
      }

      onSuccess();
      onClose();
    } catch (err: any) {
      setError(err.message || 'Failed to save contract');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal opened={true} onClose={onClose} title={<Title order={2} c="rgb(30, 28, 28)">{isEditMode ? 'Editar Contrato' : 'Criar Contrato'}</Title>} size="lg" className="styled-form">
      <form onSubmit={handleSubmit}>
        {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

        <TextInput
          label="Número do Contrato"
          required
          value={formData.contractNumber}
          onChange={(e) => handleChange('contractNumber', e.target.value)}
          maxLength={50}
          mb="md"
        />

        <Select
          label="Usuário"
          value={formData.userId}
          onChange={(value) => handleChange('userId', value)}
          data={[
            { value: '', label: 'Sem usuário atribuído' },
            ...users.map(user => ({ value: user.id, label: `${user.name} (${user.email})` }))
          ]}
          searchable
          mb="md"
        />

        <Select
          label="Grupo (Opcional)"
          value={formData.groupId}
          onChange={(value) => handleChange('groupId', value)}
          data={[
            { value: '0', label: 'Nenhum' },
            ...groups.map(group => ({ value: group.id.toString(), label: group.name }))
          ]}
          mb="md"
        />

        <Select
          label="Ponto de Venda"
          value={formData.pvId}
          onChange={(value) => handleChange('pvId', value)}
          data={[
            { value: '', label: 'Nenhum' },
            ...pvs.map(pv => ({ value: pv.id.toString(), label: pv.name }))
          ]}
          searchable
          mb="md"
        />

        <NumberInput
          label="Valor Total"
          required
          value={formData.totalAmount}
          onChange={(value) => handleChange('totalAmount', value)}
          min={0.01}
          decimalScale={2}
          fixedDecimalScale
          prefix="R$ "
          mb="md"
        />

        <Select
          label="Status"
          value={formData.status}
          onChange={(value) => handleChange('status', value)}
          data={[
            { value: 'Active', label: 'Ativo' },
            { value: 'Late1', label: '1 mês atrasado' },
            { value: 'Late2', label: '2 meses atrasado' },
            { value: 'Late3', label: '3 meses atrasado' },
            { value: 'Defaulted', label: 'Inadimplente' },
          ]}
          mb="md"
        />

        <TextInput
          label="Data de Início"
          required
          type="date"
          value={formData.contractStartDate}
          onChange={(e) => handleChange('contractStartDate', e.target.value)}
          mb="md"
        />

        <TextInput
          label="Data de Término"
          type="date"
          value={formData.contractEndDate}
          onChange={(e) => handleChange('contractEndDate', e.target.value)}
          mb="md"
        />

        <Select
          label="Tipo de Contrato"
          value={formData.contractType}
          onChange={(value) => handleChange('contractType', value)}
          data={[
            { value: '', label: 'Selecione' },
            { value: '0', label: 'Lar' },
            { value: '1', label: 'Motores' },
          ]}
          mb="md"
        />

        <NumberInput
          label="Cota"
          value={formData.quota}
          onChange={(value) => handleChange('quota', value)}
          placeholder="Ex: 10"
          mb="md"
        />

        <TextInput
          label="Nome do Cliente"
          value={formData.customerName}
          onChange={(e) => handleChange('customerName', e.target.value)}
          placeholder="Ex: João Silva"
          maxLength={200}
          mb="md"
        />

        <Group justify="flex-end" mt="xl">
          <Button variant="default" onClick={onClose} disabled={loading}>
            Cancelar
          </Button>
          <Button type="submit" loading={loading}>
            {isEditMode ? 'Salvar Alterações' : 'Criar Contrato'}
          </Button>
        </Group>
      </form>
    </Modal>
  );
};

export default ContractForm;
