import React, { useState } from 'react';
import { Button, Group, Title, Progress, Text } from '@mantine/core';
import { IconUpload, IconCheck, IconX } from '@tabler/icons-react';
import { apiService } from '../services/apiService';
import StyledModal from './StyledModal';

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
          // Parse boolean values
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
    <StyledModal
      opened={true}
      onClose={onClose}
      title="Importar Matrículas (CSV)"
      size="lg"
    >
      <div style={{ padding: '10px 0' }}>
        {error && (
          <div style={{ 
            color: 'red', 
            marginBottom: '1rem', 
            padding: '10px', 
            backgroundColor: '#fee', 
            borderRadius: '4px' 
          }}>
            {error}
          </div>
        )}

        {!result ? (
          <>
            <div style={{ marginBottom: '1rem' }}>
              <Text size="sm" c="dimmed" mb="xs">
                Formato do CSV: matriculaNumber, isOwner, userEmail, startDate, endDate
              </Text>
              <Text size="sm" c="dimmed" mb="md">
                Exemplo: MAT-001,true,user@example.com,2024-01-01,2024-12-31
              </Text>
              
              <input
                type="file"
                accept=".csv"
                onChange={handleFileChange}
                style={{
                  width: '100%',
                  padding: '10px',
                  border: '2px dashed #ddd',
                  borderRadius: '4px',
                  cursor: 'pointer'
                }}
              />
            </div>

            {file && (
              <Text size="sm" c="blue" mb="md">
                Arquivo selecionado: {file.name}
              </Text>
            )}

            <Group justify="flex-end" mt="xl">
              <Button variant="light" onClick={onClose} disabled={loading}>
                Cancelar
              </Button>
              <Button
                onClick={handleImport}
                loading={loading}
                disabled={!file}
                leftSection={<IconUpload size={16} />}
              >
                Importar
              </Button>
            </Group>
          </>
        ) : (
          <>
            <div style={{ marginBottom: '1.5rem' }}>
              <Title order={4} mb="md">Resultado da Importação</Title>
              
              <div style={{ marginBottom: '1rem' }}>
                <Text size="sm" mb="xs">
                  Total processado: <strong>{result.totalProcessed}</strong>
                </Text>
                <Text size="sm" c="green" mb="xs">
                  <IconCheck size={16} style={{ verticalAlign: 'middle', marginRight: '4px' }} />
                  Sucesso: <strong>{result.successCount}</strong>
                </Text>
                <Text size="sm" c="red" mb="md">
                  <IconX size={16} style={{ verticalAlign: 'middle', marginRight: '4px' }} />
                  Erros: <strong>{result.errorCount}</strong>
                </Text>

                <Progress
                  value={(result.successCount / result.totalProcessed) * 100}
                  color={result.errorCount === 0 ? 'green' : 'orange'}
                  size="lg"
                  mb="md"
                />
              </div>

              {result.errors.length > 0 && (
                <div>
                  <Title order={5} mb="sm">Erros:</Title>
                  <div style={{ 
                    maxHeight: '200px', 
                    overflowY: 'auto', 
                    border: '1px solid #ddd', 
                    borderRadius: '4px',
                    padding: '10px',
                    backgroundColor: '#fafafa'
                  }}>
                    {result.errors.map((err, idx) => (
                      <div key={idx} style={{ marginBottom: '8px', fontSize: '13px' }}>
                        <strong>Linha {err.rowNumber}:</strong> {err.error}
                        <br />
                        <Text size="xs" c="dimmed">
                          Matrícula: {err.matriculaNumber || 'N/A'} | Email: {err.userEmail || 'N/A'}
                        </Text>
                      </div>
                    ))}
                  </div>
                  <Button
                    size="xs"
                    variant="light"
                    onClick={downloadErrorReport}
                    mt="sm"
                  >
                    Baixar Relatório de Erros
                  </Button>
                </div>
              )}
            </div>

            <Group justify="flex-end">
              <Button onClick={onClose}>
                Fechar
              </Button>
            </Group>
          </>
        )}
      </div>
    </StyledModal>
  );
};

export default MatriculaImportModal;
