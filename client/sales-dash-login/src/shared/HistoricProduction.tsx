import React, { useState, useEffect } from 'react';
import { LineChart } from '@mantine/charts';
import { getHistoricProduction, HistoricProductionResponse } from '../services/contractService';
import './HistoricProduction.css';

interface HistoricProductionProps {
  startDate?: string;
  endDate?: string;
  userId?: string;
}

const HistoricProduction: React.FC<HistoricProductionProps> = ({ startDate, endDate, userId }) => {
  const [data, setData] = useState<HistoricProductionResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    loadData();
  }, [startDate, endDate, userId]);

  const loadData = async () => {
    setLoading(true);
    setError('');

    try {
      const result = await getHistoricProduction(startDate, endDate, userId);
      setData(result);
    } catch (err: any) {
      setError(err.message || 'Falha ao carregar produção histórica');
    } finally {
      setLoading(false);
    }
  };

  const formatCurrency = (value: number): string => {
    return new Intl.NumberFormat('pt-BR', {
      style: 'currency',
      currency: 'BRL',
    }).format(value);
  };

  const formatAbbreviated = (value: number): string => {
    if (value >= 1000000) {
      return `R$ ${(value / 1000000).toFixed(1)}M`;
    } else if (value >= 1000) {
      return `R$ ${(value / 1000).toFixed(0)}K`;
    }
    return formatCurrency(value);
  };

  const formatPeriod = (period: string): string => {
    const [year, month] = period.split('-');
    const monthNames = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
    return `${monthNames[parseInt(month) - 1]} ${year}`;
  };

  if (loading) {
    return (
      <div className="historic-production">
        <h3>Produção Histórica</h3>
        <div className="historic-production-loading">
          <div className="spinner"></div>
          <p>Carregando...</p>
        </div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="historic-production">
        <h3>Produção Histórica</h3>
        <div className="historic-production-error">
          <p>{error}</p>
        </div>
      </div>
    );
  }

  if (!data || data.monthlyData.length === 0) {
    return (
      <div className="historic-production">
        <h3>Produção Histórica</h3>
        <div className="historic-production-empty">
          <p>Nenhum dado de produção disponível para o período selecionado.</p>
        </div>
      </div>
    );
  }

  // Prepare chart data
  const chartData = data.monthlyData.map(item => ({
    month: formatPeriod(item.period),
    producao: item.totalProduction
  }));

  return (
    <div className="historic-production">
      <h3>Produção Histórica</h3>
      
      <div className="historic-production-summary">
        <div className="summary-item">
          <span className="summary-label">Total Produzido:</span>
          <span className="summary-value">{formatCurrency(data.totalProduction)}</span>
        </div>
        <div className="summary-item">
          <span className="summary-label">Total de Contratos:</span>
          <span className="summary-value">{data.totalContracts}</span>
        </div>
      </div>

      <div className="historic-production-chart">
        <LineChart
          h={300}
          data={chartData}
          dataKey="month"
          series={[{ name: 'producao', color: 'indigo.6' }]}
          curveType="linear"
          connectNulls
          valueFormatter={(value) => formatAbbreviated(value)}
        />
      </div>
    </div>
  );
};

export default HistoricProduction;
