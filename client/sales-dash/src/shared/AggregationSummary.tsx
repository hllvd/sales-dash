import React from 'react';
import { DonutChart } from '@mantine/charts';
import './AggregationSummary.css';

interface AggregationSummaryProps {
  total: number;
  totalCancel: number;
  totalActive: number;
  totalLate: number;
  retention: number;
  strictRetention?: number;
}

const AggregationSummary: React.FC<AggregationSummaryProps> = ({ 
  total, 
  totalCancel, 
  totalActive, 
  totalLate, 
  retention,
  strictRetention = 0
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
    { name: 'Cancelados', value: defaultedPercent, color: '#ef4444' }
  ];

  const isValidStrictRetention = !isNaN(strictRetention) && strictRetention !== null && strictRetention !== undefined;
  const strictRetentionPercent = isValidStrictRetention ? strictRetention * 100 : 0;
  const strictLossPercent = isValidStrictRetention ? (1 - strictRetention) * 100 : 0;

  const strictDonutData = [
    { name: 'Adimplentes', value: strictRetentionPercent, color: '#22c55e' },
    { name: 'Atrasados + Cancelados', value: strictLossPercent, color: '#ef4444' }
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
          <div className="aggregation-charts-wrapper" style={{ display: 'flex', flexDirection: 'column', gap: '20px', width: '100%', alignItems: 'stretch' }}>
            <div className="aggregation-chart" style={{ width: '100%', boxSizing: 'border-box' }}>
              <h4 className="chart-title">Retenção (Cancelamentos)</h4>
              <DonutChart
                data={donutData}
                thickness={20}
                size={140}
                chartLabel={formatPercentage(retention)}
                tooltipDataSource="segment"
              />
            </div>
            {isValidStrictRetention && (
              <div className="aggregation-chart" style={{ width: '100%', boxSizing: 'border-box' }}>
                <h4 className="chart-title">Retenção (Atrasos + Cancelamentos)</h4>
                <DonutChart
                  data={strictDonutData}
                  thickness={20}
                  size={140}
                  chartLabel={formatPercentage(strictRetention)}
                  tooltipDataSource="segment"
                />
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default AggregationSummary;
