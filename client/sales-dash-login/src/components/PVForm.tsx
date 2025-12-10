import React, { useState } from 'react';
import './PVForm.css';
import { PV } from '../services/apiService';

interface PVFormProps {
  pv?: PV;
  onSubmit: (pvData: { id: number; name: string }) => Promise<void>;
  onCancel: () => void;
  isEdit?: boolean;
}

const PVForm: React.FC<PVFormProps> = ({ pv, onSubmit, onCancel, isEdit = false }) => {
  const [formData, setFormData] = useState({
    id: pv?.id || 0,
    name: pv?.name || '',
  });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const { name, value } = e.target;
    setFormData((prev) => ({
      ...prev,
      [name]: name === 'id' ? parseInt(value) || 0 : value,
    }));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      await onSubmit(formData);
    } catch (err: any) {
      setError(err.message || 'An error occurred');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onCancel}>
      <div className="modal-content styled-form" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>{isEdit ? 'Editar Ponto de Venda' : 'Criar Novo Ponto de Venda'}</h2>
          <button className="close-button" onClick={onCancel}>
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit} className="pv-form">
          {error && <div className="error-message">{error}</div>}

          <div className="form-group">
            <label htmlFor="id">ID *</label>
            <input
              type="number"
              id="id"
              name="id"
              value={formData.id}
              onChange={handleChange}
              required
              disabled={isEdit}
              placeholder="ID do ponto de venda"
              min="1"
            />
            {!isEdit && (
              <span className="hint">O ID deve ser único e não pode ser alterado depois</span>
            )}
          </div>

          <div className="form-group">
            <label htmlFor="name">Nome *</label>
            <input
              type="text"
              id="name"
              name="name"
              value={formData.name}
              onChange={handleChange}
              required
              maxLength={100}
              placeholder="Nome do ponto de venda"
            />
          </div>

          <div className="form-actions">
            <button type="button" onClick={onCancel} className="btn-cancel" disabled={loading}>
              Cancelar
            </button>
            <button type="submit" className="btn-submit" disabled={loading}>
              {loading ? 'Salvando...' : isEdit ? 'Salvar Alterações' : 'Criar PV'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

export default PVForm;
