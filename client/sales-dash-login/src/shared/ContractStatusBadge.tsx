import React from 'react';
import { Badge } from '@mantine/core';

interface ContractStatusBadgeProps {
  status: string;
}

export const getStatusLabel = (status: string): string => {
  switch (status.toLowerCase()) {
    case 'active':
      return 'Ativo';
    case 'late1':
      return 'Atrasado 1';
    case 'late2':
      return 'Atrasado 2';
    case 'late3':
      return 'Atrasado 3';
    case 'defaulted':
      return 'Cancelado';
    case 'transferred':
      return 'Transferido';
    case 'paid_off':
      return 'Quitado';
    case 'delinquent':
      return 'Inadimplente';
    default:
      return status;
  }
};

export const getStatusColor = (status: string): string => {
  const s = status.toLowerCase();
  switch (s) {
    case 'active':
      return 'green';
    case 'late1':
      return 'yellow.4';
    case 'late2':
      return 'yellow.6';
    case 'late3':
      return 'yellow.8';
    case 'delinquent':
      return 'red';
    case 'defaulted':
      return 'red';
    case 'transferred':
      return 'blue';
    case 'paid_off':
      return 'teal';
    default:
      return 'gray';
  }
};

export const CONTRACT_STATUS_OPTIONS = [
  { value: 'active', label: 'Ativo' },
  { value: 'late1', label: 'Atrasado 1' },
  { value: 'late2', label: 'Atrasado 2' },
  { value: 'late3', label: 'Atrasado 3' },
  { value: 'delinquent', label: 'Inadimplente' },
  { value: 'defaulted', label: 'Cancelado' },
  { value: 'transferred', label: 'Transferido' },
  { value: 'paid_off', label: 'Quitado' },
];

const ContractStatusBadge: React.FC<ContractStatusBadgeProps> = ({ status }) => {
  return (
    <Badge 
      color={getStatusColor(status)}
      title={getStatusLabel(status)}
      style={{ cursor: 'help' }}
    >
      {getStatusLabel(status)}
    </Badge>
  );
};

export default ContractStatusBadge;
