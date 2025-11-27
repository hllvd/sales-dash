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
    totalAmount: contract?.totalAmount?.toString() || '',
    status: contract?.status || 'active',
    contractStartDate: contract?.contractStartDate?.split('T')[0] || '',
    contractEndDate: contract?.contractEndDate?.split('T')[0] || '',
    isActive: contract?.isActive ?? true,
  });

  const [users, setUsers] = useState<User[]>([]);
  const [groups, setGroups] = useState<Group[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    const fetchDropdownData = async () => {
      try {
        const [usersData, groupsData] = await Promise.all([getUsers(), getGroups()]);
        setUsers(usersData);
        setGroups(groupsData);
      } catch (err) {
        setError('Failed to load users and groups');
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

    if (!formData.userId) {
      setError('User is required');
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
          userId: formData.userId,
          groupId: parseInt(formData.groupId),
          totalAmount: parseFloat(formData.totalAmount),
          status: formData.status as 'active' | 'delinquent' | 'paid_off',
          contractStartDate: formData.contractStartDate,
          contractEndDate: formData.contractEndDate || null,
          isActive: formData.isActive,
        };
        await updateContract(contract.id, updateData);
      } else {
        const createData: CreateContractRequest = {
          contractNumber: formData.contractNumber,
          userId: formData.userId,
          groupId: parseInt(formData.groupId),
          totalAmount: parseFloat(formData.totalAmount),
          status: formData.status as 'active' | 'delinquent' | 'paid_off',
          contractStartDate: formData.contractStartDate,
          contractEndDate: formData.contractEndDate || null,
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
            <label htmlFor="userId">Usuário *</label>
            <select
              id="userId"
              name="userId"
              value={formData.userId}
              onChange={handleChange}
              required
            >
              <option value="">Selecione um usuário</option>
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
              <option value="active">Ativo</option>
              <option value="delinquent">Inadimplente</option>
              <option value="paid_off">Quitado</option>
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
