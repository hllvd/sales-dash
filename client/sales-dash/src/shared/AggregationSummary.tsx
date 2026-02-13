import React from 'react';
import { DonutChart } from '@mantine/charts';
import './AggregationSummary.css';

interface AggregationSummaryProps {
  total: number;
  totalCancel: number;
  totalActive: number;
  totalLate: number;
  retention: number;
}

const AggregationSummary: React.FC<AggregationSummaryProps> = ({ 
  total, 
  totalCancel, 
  totalActive, 
  totalLate, 
  retention 
}) => {
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
    return `${(value * 100).toFixed(2)}%`;
  };

  // Prepare donut chart data
  const isValidRetention = !isNaN(retention) && retention !== null && retention !== undefined;
  const retentionPercent = isValidRetention ? retention * 100 : 0;
  const defaultedPercent = isValidRetention ? (1 - retention) * 100 : 0;

  const donutData = [
    { name: 'Retidos', value: retentionPercent, color: '#22c55e' },
    { name: 'Inadimplentes', value: defaultedPercent, color: '#ef4444' }
  ];


  return (
    <div className="aggregation-summary">
      <h3>Resumo</h3>
      <div className="aggregation-container">
        <div className="aggregation-grid">
          <div className="aggregation-item">
            <span className="aggregation-label">Total Geral:</span>
            <span className="aggregation-value">
              {formatCurrency(total)}
            </span>
          </div>
          <div className="aggregation-item">
            <span className="aggregation-label">Total Ativo:</span>
            <span className="aggregation-value active">
              {formatCurrency(totalActive)}
            </span>
          </div>
          <div className="aggregation-item">
            <span className="aggregation-label">Total Atrasado:</span>
            <span className="aggregation-value late">
              {formatCurrency(totalLate)}
            </span>
          </div>
          <div className="aggregation-item">
            <span className="aggregation-label">Total Cancelado:</span>
            <span className="aggregation-value canceled">
              {formatCurrency(totalCancel)}
            </span>
          </div>
        </div>
        
        {isValidRetention && (
          <div className="aggregation-chart">
            <h4 className="chart-title">Retenção</h4>
            <DonutChart
              data={donutData}
              thickness={30}
              size={180}
              chartLabel={formatPercentage(retention)}
              tooltipDataSource="segment"
            />
          </div>
        )}
      </div>
    </div>
  );
};

export default AggregationSummary;
