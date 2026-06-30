import React from 'react';
import { Badge, Tooltip } from '@mantine/core';

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
    case 'awaitingpayment':
      return 'Aguardando Pagamento';
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
    case 'awaitingpayment':
      return 'orange';
    default:
      return 'gray';
  }
};

export const CONTRACT_STATUS_OPTIONS = [
  { value: 'Active', label: 'Ativo' },
  { value: 'Late1', label: 'Atrasado 1' },
  { value: 'Late2', label: 'Atrasado 2' },
  { value: 'Late3', label: 'Atrasado 3' },
  { value: 'Delinquent', label: 'Inadimplente' },
  { value: 'Defaulted', label: 'Cancelado' },
  { value: 'Transferred', label: 'Transferido' },
  { value: 'PaidOff', label: 'Quitado' },
  { value: 'AwaitingPayment', label: 'Aguardando Pagamento' },
];

const ContractStatusBadge: React.FC<ContractStatusBadgeProps> = ({ status }) => {
  const isAwaitingPayment = status.toLowerCase() === 'awaitingpayment';
  
  const badgeElement = (
    <Badge 
      color={getStatusColor(status)}
      title={isAwaitingPayment ? undefined : getStatusLabel(status)}
      style={{ cursor: 'help' }}
    >
      {getStatusLabel(status)}
    </Badge>
  );

  if (isAwaitingPayment) {
    return (
      <Tooltip 
        label="Este contrato não é utilizado para calcular qualquer retenção ou somar ao total" 
        withArrow 
        position="top"
      >
        {badgeElement}
      </Tooltip>
    );
  }

  return badgeElement;
};

export default ContractStatusBadge;
