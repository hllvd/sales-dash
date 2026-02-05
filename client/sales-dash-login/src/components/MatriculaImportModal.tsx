import React, { useState } from 'react';
import { Button, Group, Title, Progress, Text, Alert } from '@mantine/core';
import { IconUpload, IconCheck, IconX, IconAlertCircle, IconFileDownload } from '@tabler/icons-react';
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
          <Alert icon={<IconAlertCircle size={16} />} title="Erro" color="red" mb="md" variant="light">
            {error}
          </Alert>
        )}

        {!result ? (
          <>
            <div style={{ marginBottom: '1.5rem' }}>
              <Text size="sm" c="gray.4" mb="xs">
                Formato do CSV: matriculaNumber, isOwner, userEmail, startDate, endDate
              </Text>
              <Text size="sm" c="gray.5" mb="md">
                Exemplo: MAT-001,true,user@example.com,2024-01-01,2024-12-31
              </Text>
              
              <div style={{ 
                border: '2px dashed rgba(255, 255, 255, 0.1)', 
                borderRadius: '8px', 
                padding: '32px 24px', 
                textAlign: 'center',
                backgroundColor: 'rgba(255, 255, 255, 0.02)',
                position: 'relative',
                transition: 'all 0.2s ease',
                cursor: 'pointer'
              }}>
                <input
                  type="file"
                  accept=".csv"
                  onChange={handleFileChange}
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    height: '100%',
                    opacity: 0,
                    cursor: 'pointer',
                    zIndex: 2
                  }}
                />
                <IconUpload size={40} color="#b2342b" style={{ marginBottom: '12px', opacity: 0.8 }} />
                <Text size="sm" fw={500} c="white">
                  {file ? file.name : "Clique para selecionar ou arraste o arquivo CSV"}
                </Text>
                {file && (
                  <Text size="xs" c="dimmed" mt="xs">
                    {(file.size / 1024).toFixed(1)} KB
                  </Text>
                )}
              </div>
            </div>

            <Group justify="flex-end" mt="xl">
              <Button variant="subtle" color="gray" onClick={onClose} disabled={loading}>
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
              <Title order={4} mb="md" c="white">Resultado da Importação</Title>
              
              <div style={{ marginBottom: '2rem' }}>
                <Group justify="space-between" mb="xs">
                  <Text size="sm" c="gray.4">
                    Processado: <strong>{result.totalProcessed}</strong>
                  </Text>
                  <Group gap="md">
                    <Text size="sm" c="green.4">
                      <IconCheck size={14} style={{ verticalAlign: 'middle', marginRight: '4px' }} />
                      Sucesso: <strong>{result.successCount}</strong>
                    </Text>
                    <Text size="sm" c="red.4">
                      <IconX size={14} style={{ verticalAlign: 'middle', marginRight: '4px' }} />
                      Erros: <strong>{result.errorCount}</strong>
                    </Text>
                  </Group>
                </Group>

                <Progress
                  value={(result.successCount / result.totalProcessed) * 100}
                  color={result.errorCount === 0 ? 'green' : 'orange'}
                  size="xl"
                  radius="xl"
                  animated={loading}
                />
              </div>

              {result.errors.length > 0 && (
                <div>
                  <Title order={5} mb="sm" c="gray.3">Erros Identificados:</Title>
                  <div style={{ 
                    maxHeight: '220px', 
                    overflowY: 'auto', 
                    border: '1px solid rgba(255, 255, 255, 0.1)', 
                    borderRadius: '8px',
                    padding: '12px',
                    backgroundColor: 'rgba(0, 0, 0, 0.2)'
                  }}>
                    {result.errors.map((err, idx) => (
                      <div key={idx} style={{ 
                        marginBottom: '10px', 
                        paddingBottom: '10px', 
                        borderBottom: '1px solid rgba(255, 255, 255, 0.05)',
                        fontSize: '13px' 
                      }}>
                        <Text fw={600} c="red.4">Linha {err.rowNumber}: {err.error}</Text>
                        <Text size="xs" c="dimmed" mt={4}>
                          Matrícula: {err.matriculaNumber || 'N/A'} | Email: {err.userEmail || 'N/A'}
                        </Text>
                      </div>
                    ))}
                  </div>
                  <Button
                    size="xs"
                    variant="light"
                    color="orange"
                    onClick={downloadErrorReport}
                    mt="md"
                    leftSection={<IconFileDownload size={14} />}
                  >
                    Baixar Relatório de Erros
                  </Button>
                </div>
              )}
            </div>

            <Group justify="flex-end" mt="xl">
              <Button onClick={onClose} variant="default">
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
