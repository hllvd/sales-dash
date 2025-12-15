import React from 'react';
import './AggregationSummary.css';

interface AggregationSummaryProps {
  total: number;
  totalCancel: number;
  retention: number;
}

const AggregationSummary: React.FC<AggregationSummaryProps> = ({ total, totalCancel, retention }) => {
  const formatCurrency = (value: number): string => {
    if (isNaN(value) || value === null || value === undefined) {
      return '--';
    }
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(value);
  };

  const formatPercentage = (value: number): string => {
    if (isNaN(value) || value === null || value === undefined) {
      return '--';
    }
    return `${(value * 100).toFixed(1)}%`;
  };

  return (
    <div className="aggregation-summary">
      <h3>Resumo</h3>
      <div className="aggregation-grid">
        <div className="aggregation-item">
          <span className="aggregation-label">Total Geral:</span>
          <span className="aggregation-value">
            {formatCurrency(total)}
          </span>
        </div>
        <div className="aggregation-item">
          <span className="aggregation-label">Total Cancelado:</span>
          <span className="aggregation-value canceled">
            {formatCurrency(totalCancel)}
          </span>
        </div>
        <div className="aggregation-item">
          <span className="aggregation-label">Taxa de Retenção:</span>
          <span className="aggregation-value retention">
            {formatPercentage(retention)}
          </span>
        </div>
      </div>
    </div>
  );
};

export default AggregationSummary;
