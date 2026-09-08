import React, { useState } from 'react';
import {
  Text,
  Button,
  FileInput,
  Badge,
  Table,
  Alert,
  Loader,
} from '@mantine/core';
import {
  IconFileSpreadsheet,
  IconFilter,
  IconDownload,
  IconCheck,
  IconAlertCircle,
  IconTable,
  IconFileExport,
} from '@tabler/icons-react';
import {
  apiService,
  RetentionFilterProcessResponse,
} from '../services/apiService';
import Menu from './Menu';
import './RetentionFilterPage.css';

const RetentionFilterPage: React.FC = () => {
  const [fileA, setFileA] = useState<File | null>(null);
  const [fileB, setFileB] = useState<File | null>(null);

  const [loadingPreview, setLoadingPreview] = useState(false);
  const [loadingDownload, setLoadingDownload] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<RetentionFilterProcessResponse | null>(null);

  const handlePreview = async () => {
    if (!fileA || !fileB) {
      setError('Por favor, selecione ambos os arquivos (Modelo A e Modelo B).');
      return;
    }

    setError(null);
    setLoadingPreview(true);

    try {
      const res = await apiService.previewRetentionFilter(fileA, fileB);
      if (res.success && res.data) {
        setResult(res.data);
      } else {
        setError(res.message || 'Erro ao processar filtro.');
      }
    } catch (err: any) {
      setError(err.message || 'Falha ao processar arquivos.');
    } finally {
      setLoadingPreview(false);
    }
  };

  const handleDownload = async () => {
    if (!fileA || !fileB) {
      setError('Por favor, selecione ambos os arquivos (Modelo A e Modelo B).');
      return;
    }

    setError(null);
    setLoadingDownload(true);

    try {
      const blob = await apiService.downloadFilteredRetentionFile(fileA, fileB);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'modelo_retencao_filtrado.xlsx';
      document.body.appendChild(link);
      link.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(link);
    } catch (err: any) {
      setError(err.message || 'Falha ao baixar arquivo filtrado.');
    } finally {
      setLoadingDownload(false);
    }
  };

  return (
    <div style={{ display: 'flex', minHeight: '100vh', backgroundColor: '#f8fafc' }}>
      <Menu />

      <main style={{ flex: 1, padding: '24px 32px' }}>
        <div className="retention-filter-container">
          {/* Header */}
          <div className="retention-filter-header">
            <h1 className="retention-filter-title">
              <IconFilter size={32} color="#3b82f6" />
              Filtro Modelo de Retenção
            </h1>
            <p className="retention-filter-subtitle">
              Filtre a planilha base do Modelo de Retenção mantendo apenas os contratos presentes na lista de referência (Modelo B).
            </p>
          </div>

          {/* Error Alert */}
          {error && (
            <Alert
              icon={<IconAlertCircle size={18} />}
              title="Atenção"
              color="red"
              withCloseButton
              onClose={() => setError(null)}
              mb="lg"
            >
              {error}
            </Alert>
          )}

          {/* Upload Card */}
          <div className="retention-filter-card">
            <div className="files-upload-grid">
              {/* File A: Modelo A (Base) */}
              <div className="upload-box">
                <div className="upload-box-title">
                  <IconFileSpreadsheet size={20} color="#3b82f6" />
                  Modelo A (Base de Retenção)
                </div>
                <div className="upload-box-desc">
                  Arquivo original completo (geralmente <em>modelo_referencia_retencao</em>). A primeira coluna ou coluna de contratos será decomposta e filtrada.
                </div>
                <FileInput
                  placeholder="Selecione o arquivo Modelo A (.xlsx, .csv)"
                  value={fileA}
                  onChange={setFileA}
                  accept=".xlsx,.csv"
                  clearable
                />
                {fileA && (
                  <Badge color="blue" variant="light" size="sm">
                    {fileA.name} ({(fileA.size / 1024).toFixed(1)} KB)
                  </Badge>
                )}
              </div>

              {/* File B: Modelo B (Lista de Contratos) */}
              <div className="upload-box">
                <div className="upload-box-title">
                  <IconTable size={20} color="#8b5cf6" />
                  Modelo B (Lista de Contratos)
                </div>
                <div className="upload-box-desc">
                  Arquivo contendo a coluna única com a lista dos números de contratos que devem ser mantidos no resultado.
                </div>
                <FileInput
                  placeholder="Selecione o arquivo Modelo B (.xlsx, .csv)"
                  value={fileB}
                  onChange={setFileB}
                  accept=".xlsx,.csv"
                  clearable
                />
                {fileB && (
                  <Badge color="grape" variant="light" size="sm">
                    {fileB.name} ({(fileB.size / 1024).toFixed(1)} KB)
                  </Badge>
                )}
              </div>
            </div>

            {/* Actions */}
            <div className="actions-bar">
              <Button
                variant="light"
                color="blue"
                leftSection={loadingPreview ? <Loader size="xs" /> : <IconCheck size={18} />}
                onClick={handlePreview}
                disabled={!fileA || !fileB || loadingPreview || loadingDownload}
              >
                Visualizar Métricas e Amostra
              </Button>
              <Button
                variant="filled"
                color="blue"
                leftSection={loadingDownload ? <Loader size="xs" color="white" /> : <IconDownload size={18} />}
                onClick={handleDownload}
                disabled={!fileA || !fileB || loadingPreview || loadingDownload}
              >
                Baixar Modelo C Filtrado (.xlsx)
              </Button>
            </div>
          </div>

          {/* Results KPIs */}
          {result && (
            <>
              <div className="kpi-grid">
                <div className="kpi-card blue">
                  <div className="kpi-header">
                    <span className="kpi-title">Total Linhas (Modelo A)</span>
                  </div>
                  <div className="kpi-value">{result.stats.totalRowsModelA}</div>
                </div>

                <div className="kpi-card purple">
                  <div className="kpi-header">
                    <span className="kpi-title">Contratos Filtro (Modelo B)</span>
                  </div>
                  <div className="kpi-value">{result.stats.totalContractsModelB}</div>
                </div>

                <div className="kpi-card teal">
                  <div className="kpi-header">
                    <span className="kpi-title">Linhas Mantidas (Modelo C)</span>
                  </div>
                  <div className="kpi-value">{result.stats.matchedRowsModelC}</div>
                </div>

                <div className="kpi-card amber">
                  <div className="kpi-header">
                    <span className="kpi-title">Linhas Descartadas</span>
                  </div>
                  <div className="kpi-value">{result.stats.removedRows}</div>
                </div>

                <div className="kpi-card indigo">
                  <div className="kpi-header">
                    <span className="kpi-title">Taxa de Retenção</span>
                  </div>
                  <div className="kpi-value">{result.stats.retentionRate}%</div>
                </div>
              </div>

              {/* Sample Data Table */}
              <div className="preview-table-card">
                <div className="preview-table-header">
                  <div>
                    <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 600, color: '#1e293b' }}>
                      Prévia dos Dados Filtrados (Amostra de até 50 registros)
                    </h3>
                    <Text size="xs" color="dimmed">
                      Estrutura original de colunas preservada.
                    </Text>
                  </div>
                  <Button
                    size="xs"
                    leftSection={<IconFileExport size={16} />}
                    onClick={handleDownload}
                    loading={loadingDownload}
                  >
                    Baixar Arquivo Completo
                  </Button>
                </div>

                {result.sampleRows.length > 0 ? (
                  <Table striped highlightOnHover withTableBorder withColumnBorders>
                    <Table.Thead>
                      <Table.Tr>
                        {result.headers.map((h, idx) => (
                          <Table.Th key={idx} style={{ whiteSpace: 'nowrap' }}>
                            {h}
                          </Table.Th>
                        ))}
                      </Table.Tr>
                    </Table.Thead>
                    <Table.Tbody>
                      {result.sampleRows.map((row, rIdx) => (
                        <Table.Tr key={rIdx}>
                          {result.headers.map((h, cIdx) => (
                            <Table.Td key={cIdx} style={{ whiteSpace: 'nowrap', fontSize: '13px' }}>
                              {row[h] || '-'}
                            </Table.Td>
                          ))}
                        </Table.Tr>
                      ))}
                    </Table.Tbody>
                  </Table>
                ) : (
                  <Text color="dimmed" ta="center" py="lg">
                    Nenhuma correspondência encontrada entre os contratos do Modelo B e as linhas do Modelo A.
                  </Text>
                )}
              </div>
            </>
          )}
        </div>
      </main>
    </div>
  );
};

export default RetentionFilterPage;
