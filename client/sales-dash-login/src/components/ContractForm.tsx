import React, { useState, useEffect } from 'react';
import './ContractForm.css';
import {
  CreateContractRequest,
  UpdateContractRequest,
  Contract,
  User,
  Group,
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
    groupId: contract?.groupId?.toString() || '',
    pvId: contract?.pvId?.toString() || '',
    totalAmount: contract?.totalAmount?.toString() || '',
    status: contract?.status || 'Active',
    contractStartDate: contract?.contractStartDate?.split('T')[0] || '',
    contractEndDate: contract?.contractEndDate?.split('T')[0] || '',
    isActive: contract?.isActive ?? true,
    contractType: contract?.contractType?.toString() || '',
    quota: contract?.quota?.toString() || '',
    customerName: contract?.customerName || '',
  });

  const [users, setUsers] = useState<User[]>([]);
  const [groups, setGroups] = useState<Group[]>([]);
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
        
        // For new contracts, auto-select first group and first contract type
        if (!contract && groupsData.length > 0) {
          setFormData(prev => ({
            ...prev,
            groupId: groupsData[0].id.toString(),
            contractType: '0', // First option: Lar
          }));
        }
      } catch (err) {
        setError('Failed to load users, groups, and PVs');
      }
    };

    fetchDropdownData();
  }, []);

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value, type } = e.target;
    const checked = (e.target as HTMLInputElement).checked;

    setFormData((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value,
    }));
  };

  const validateForm = (): boolean => {
    if (!formData.contractNumber.trim()) {
      setError('Contract number is required');
      return false;
    }


    if (!formData.groupId) {
      setError('Group is required');
      return false;
    }

    const amount = parseFloat(formData.totalAmount);
    if (!formData.totalAmount || isNaN(amount) || amount < 0.01) {
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
          groupId: parseInt(formData.groupId),
          pvId: formData.pvId ? parseInt(formData.pvId) : undefined,
          totalAmount: parseFloat(formData.totalAmount),
          status: formData.status as 'Active' | 'Late1' | 'Late2' | 'Late3' | 'Defaulted',
          contractStartDate: formData.contractStartDate,
          contractEndDate: formData.contractEndDate || null,
          isActive: formData.isActive,
          contractType: formData.contractType ? parseInt(formData.contractType) : undefined,
          quota: formData.quota ? parseInt(formData.quota) : undefined,
          customerName: formData.customerName || undefined,
        };
        await updateContract(contract.id, updateData);
      } else {
        const createData: CreateContractRequest = {
          contractNumber: formData.contractNumber,
          userId: formData.userId || undefined,
          groupId: parseInt(formData.groupId),
          pvId: formData.pvId ? parseInt(formData.pvId) : undefined,
          totalAmount: parseFloat(formData.totalAmount),
          status: formData.status as 'Active' | 'Late1' | 'Late2' | 'Late3' | 'Defaulted',
          contractStartDate: formData.contractStartDate,
          contractEndDate: formData.contractEndDate || null,
          contractType: formData.contractType ? parseInt(formData.contractType) : undefined,
          quota: formData.quota ? parseInt(formData.quota) : undefined,
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
    <div className="contract-form-overlay" onClick={onClose}>
      <div className="contract-form-modal" onClick={(e) => e.stopPropagation()}>
        <div className="contract-form-header">
          <h2>{isEditMode ? 'Editar Contrato' : 'Criar Contrato'}</h2>
          <button className="contract-form-close" onClick={onClose}>
            ✕
          </button>
        </div>

        <form onSubmit={handleSubmit} className="contract-form">
          {error && <div className="contract-form-error">{error}</div>}

          <div className="contract-form-group">
            <label htmlFor="contractNumber">Número do Contrato *</label>
            <input
              type="text"
              id="contractNumber"
              name="contractNumber"
              value={formData.contractNumber}
              onChange={handleChange}
              required
              maxLength={50}
            />
          </div>

          <div className="contract-form-group">
            <label htmlFor="userId">Usuário</label>
            <select
              id="userId"
              name="userId"
              value={formData.userId}
              onChange={handleChange}
            >
              <option value="">Sem usuário atribuído</option>
              {users.map((user) => (
                <option key={user.id} value={user.id}>
                  {user.name} ({user.email})
                </option>
              ))}
            </select>
          </div>

          <div className="contract-form-group">
            <label htmlFor="groupId">Grupo *</label>
            <select
              id="groupId"
              name="groupId"
              value={formData.groupId}
              onChange={handleChange}
              required
            >
              <option value="">Selecione um grupo</option>
              {groups.map((group) => (
                <option key={group.id} value={group.id}>
                  {group.name}
                </option>
              ))}
            </select>
          </div>

          <div className="contract-form-group">
            <label htmlFor="pvId">Ponto de Venda</label>
            <select
              id="pvId"
              name="pvId"
              value={formData.pvId}
              onChange={handleChange}
            >
              <option value="">Nenhum</option>
              {pvs.map((pv) => (
                <option key={pv.id} value={pv.id}>
                  {pv.name}
                </option>
              ))}
            </select>
          </div>

          <div className="contract-form-group">
            <label htmlFor="totalAmount">Valor Total *</label>
            <input
              type="number"
              id="totalAmount"
              name="totalAmount"
              value={formData.totalAmount}
              onChange={handleChange}
              required
              min="0.01"
              step="0.01"
            />
          </div>

          <div className="contract-form-group">
            <label htmlFor="status">Status</label>
            <select
              id="status"
              name="status"
              value={formData.status}
              onChange={handleChange}
            >
              <option value="Active">Ativo</option>
              <option value="Late1">1 mês atrasado</option>
              <option value="Late2">2 meses atrasado</option>
              <option value="Late3">3 meses atrasado</option>
              <option value="Defaulted">Inadimplente</option>
            </select>
          </div>

          <div className="contract-form-group">
            <label htmlFor="contractStartDate">Data de Início *</label>
            <input
              type="date"
              id="contractStartDate"
              name="contractStartDate"
              value={formData.contractStartDate}
              onChange={handleChange}
              required
            />
          </div>

          <div className="contract-form-group">
            <label htmlFor="contractEndDate">Data de Término</label>
            <input
              type="date"
              id="contractEndDate"
              name="contractEndDate"
              value={formData.contractEndDate}
              onChange={handleChange}
            />
          </div>

          <div className="contract-form-group">
            <label htmlFor="contractType">Tipo de Contrato</label>
            <select
              id="contractType"
              name="contractType"
              value={formData.contractType}
              onChange={handleChange}
            >
              <option value="">Selecione</option>
              <option value="0">Lar</option>
              <option value="1">Motores</option>
            </select>
          </div>

          <div className="contract-form-group">
            <label htmlFor="quota">Cota</label>
            <input
              type="number"
              id="quota"
              name="quota"
              value={formData.quota}
              onChange={handleChange}
              placeholder="Ex: 10"
            />
          </div>

          <div className="contract-form-group">
            <label htmlFor="customerName">Nome do Cliente</label>
            <input
              type="text"
              id="customerName"
              name="customerName"
              value={formData.customerName}
              onChange={handleChange}
              placeholder="Ex: João Silva"
              maxLength={200}
            />
          </div>

          {isEditMode && (
            <div className="contract-form-group contract-form-checkbox">
              <label>
                <input
                  type="checkbox"
                  name="isActive"
                  checked={formData.isActive}
                  onChange={handleChange}
                />
                Contrato Ativo
              </label>
            </div>
          )}

          <div className="contract-form-actions">
            <button type="button" onClick={onClose} className="contract-form-cancel">
              Cancelar
            </button>
            <button type="submit" className="contract-form-submit" disabled={loading}>
              {loading ? 'Salvando...' : isEditMode ? 'Salvar Alterações' : 'Criar Contrato'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default ContractForm;
