import React, { useState } from 'react';
import { Button, Title, Progress, Text } from '@mantine/core';
import { IconCheck, IconX, IconFileDownload } from '@tabler/icons-react';
import { apiService } from '../services/apiService';
import StandardModal from '../shared/StandardModal';

interface MatriculaImportModalProps {
  onClose: () => void;
  onSuccess: () => void;
}

interface ImportResult {
  totalProcessed: number;
  successCount: number;
  errorCount: number;
  errors: Array<{
    rowNumber: number;
    matriculaNumber: string;
    userEmail: string;
    error: string;
  }>;
}

const MatriculaImportModal: React.FC<MatriculaImportModalProps> = ({ onClose, onSuccess }) => {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<ImportResult | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files[0]) {
      setFile(e.target.files[0]);
      setError('');
      setResult(null);
    }
  };

  const parseCSV = (text: string): any[] => {
    const lines = text.split('\n').filter(line => line.trim());
    if (lines.length < 2) {
      throw new Error('CSV file is empty or has no data rows');
    }

    const headers = lines[0].split(',').map(h => h.trim().toLowerCase());
    const data: any[] = [];

    for (let i = 1; i < lines.length; i++) {
      const values = lines[i].split(',').map(v => v.trim());
      const row: any = {};

      headers.forEach((header, index) => {
        const value = values[index] || '';
        
        if (header === 'matriculanumber') {
          row.matriculaNumber = value;
        } else if (header === 'isowner') {
          const lowerValue = value.toLowerCase();
          row.isOwner = lowerValue === 'true' || lowerValue === '1' || lowerValue === 'yes';
        } else if (header === 'useremail') {
          row.userEmail = value;
        } else if (header === 'startdate') {
          row.startDate = value;
        } else if (header === 'enddate') {
          row.endDate = value || undefined;
        }
      });

      if (row.matriculaNumber || row.userEmail) {
        data.push(row);
      }
    }

    return data;
  };

  const handleImport = async () => {
    if (!file) {
      setError('Please select a file');
      return;
    }

    setLoading(true);
    setError('');

    try {
      const text = await file.text();
      const matriculas = parseCSV(text);

      if (matriculas.length === 0) {
        setError('No valid data found in CSV');
        setLoading(false);
        return;
      }

      const response = await apiService.bulkCreateMatriculas(matriculas);

      if (response.success && response.data) {
        setResult({
          totalProcessed: response.data.totalProcessed,
          successCount: response.data.successCount,
          errorCount: response.data.errorCount,
          errors: response.data.errors
        });

        if (response.data.successCount > 0) {
          onSuccess();
        }
      }
    } catch (err: any) {
      setError(err.message || 'Failed to import matriculas');
    } finally {
      setLoading(false);
    }
  };

  const downloadErrorReport = () => {
    if (!result || result.errors.length === 0) return;

    const csv = [
      'Row,Matricula Number,User Email,Error',
      ...result.errors.map(e => 
        `${e.rowNumber},"${e.matriculaNumber}","${e.userEmail}","${e.error}"`
      )
    ].join('\n');

    const blob = new Blob([csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'matricula-import-errors.csv';
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <StandardModal
      isOpen={true}
      onClose={onClose}
      title="Importar de Matrículas"
      size="lg"
      footer={
        !result ? (
          <>
            <button className="btn-cancel" onClick={onClose} disabled={loading}>
              Cancelar
            </button>
            <button
              className="btn-submit"
              onClick={handleImport}
              disabled={!file || loading}
            >
              {loading ? 'Importando...' : 'Próximo'}
            </button>
          </>
        ) : (
          <button className="btn-cancel" onClick={onClose}>
            Fechar
          </button>
        )
      }
    >
      <div className="result-section">
        {error && (
          <div className="error-message">
            {error}
          </div>
        )}

        {!result ? (
          <div className="form-group">
            <label>Arquivo CSV</label>
            <div className="file-input-wrapper">
              <input
                type="file"
                accept=".csv"
                onChange={handleFileChange}
              />
            </div>
            <div className="hint">
              Formato esperado: matriculaNumber, isOwner, userEmail, startDate, endDate
              <br />
              Exemplo: MAT-001,true,user@example.com,2024-01-01,2024-12-31
            </div>
          </div>
        ) : (
          <>
            <Title order={4} mb="md">Resultado da Importação</Title>
            
            <div className="result-stats">
              <div className="result-stat">
                Processado: <strong>{result.totalProcessed}</strong>
              </div>
              <div className="result-stat" style={{ color: '#059669' }}>
                <IconCheck size={16} /> Sucesso: <strong>{result.successCount}</strong>
              </div>
              <div className="result-stat" style={{ color: '#dc2626' }}>
                <IconX size={16} /> Erros: <strong>{result.errorCount}</strong>
              </div>
            </div>

            <Progress
              value={(result.successCount / result.totalProcessed) * 100}
              color={result.errorCount === 0 ? 'green' : 'orange'}
              size="lg"
              mb="md"
            />

            {result.errors.length > 0 && (
              <>
                <Text size="sm" fw={600} mb="xs">Erros Identificados:</Text>
                <div className="errors-container">
                  {result.errors.map((err, idx) => (
                    <div key={idx} className="error-item">
                      <strong>Linha {err.rowNumber}:</strong> {err.error}
                      <div style={{ fontSize: '12px', color: '#6b7280', marginTop: '4px' }}>
                        Matrícula: {err.matriculaNumber || 'N/A'} | Email: {err.userEmail || 'N/A'}
                      </div>
                    </div>
                  ))}
                </div>
                <Button
                  size="xs"
                  variant="light"
                  color="orange"
                  onClick={downloadErrorReport}
                  className="btn-download-errors"
                  leftSection={<IconFileDownload size={14} />}
                >
                  Baixar Relatório de Erros
                </Button>
              </>
            )}
          </>
        )}
      </div>
    </StandardModal>
  );
};

export default MatriculaImportModal;
