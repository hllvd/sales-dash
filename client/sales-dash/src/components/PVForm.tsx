import React, { useState } from 'react';
import { TextInput, NumberInput, Button, Group } from '@mantine/core';
import { PV } from '../services/apiService';
import StyledModal from './StyledModal';
import FormField from './FormField';

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

  const handleChange = (name: string, value: any) => {
    setFormData((prev) => ({
      ...prev,
      [name]: value,
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
    <StyledModal
      opened={true}
      onClose={onCancel}
      title={isEdit ? 'Editar Ponto de Venda' : 'Criar Novo Ponto de Venda'}
      size="md"
    >
      <form onSubmit={handleSubmit}>
        {error && <div style={{ color: 'red', marginBottom: '1rem' }}>{error}</div>}

        <FormField 
          label="ID" 
          required
          description={!isEdit ? "O ID deve ser único e não pode ser alterado depois" : undefined}
        >
          <NumberInput
            required
            value={formData.id}
            onChange={(value) => handleChange('id', value)}
            disabled={isEdit}
            placeholder="ID do ponto de venda"
            min={1}
          />
        </FormField>

        <FormField label="Nome" required>
          <TextInput
            required
            value={formData.name}
            onChange={(e) => handleChange('name', e.target.value)}
            maxLength={100}
            placeholder="Nome do ponto de venda"
          />
        </FormField>

        <Group justify="flex-end" mt="xl">
          <Button variant="default" onClick={onCancel} disabled={loading}>
            Cancelar
          </Button>
          <Button type="submit" loading={loading}>
            {isEdit ? 'Salvar Alterações' : 'Criar PV'}
          </Button>
        </Group>
      </form>
    </StyledModal>
  );
};

export default PVForm;
