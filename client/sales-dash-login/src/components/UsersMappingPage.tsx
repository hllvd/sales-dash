import React, { useState } from 'react';
import { apiService } from '../services/apiService';
import Menu from './Menu';
import './UsersMappingPage.css';

interface ProcessedRow {
  matricula: string;
  name: string;
  email: string;
  found: boolean;
  hasDuplicates: boolean;
  duplicateCount: number;
  originalData: string[];
}

const UsersMappingPage: React.FC = () => {
  const [file, setFile] = useState<File | null>(null);
  const [processing, setProcessing] = useState(false);
  const [progress, setProgress] = useState(0);
  const [processedRows, setProcessedRows] = useState<ProcessedRow[]>([]);
  const [headers, setHeaders] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [statusMessage, setStatusMessage] = useState('');
  const [showPreview, setShowPreview] = useState(false);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      setFile(e.target.files[0]);
      setProcessedRows([]);
      setHeaders([]);
      setError(null);
      setProgress(0);
      setStatusMessage('');
      setShowPreview(false);
    }
  };

  const processFile = async () => {
    if (!file) return;

    setProcessing(true);
    setError(null);
    setProgress(0);
    setStatusMessage('Lendo arquivo...');
    setShowPreview(false);

    const reader = new FileReader();
    reader.onload = async (e) => {
      try {
        const text = e.target?.result as string;
        const lines = text.split(/\r\n|\n/);
        
        if (lines.length === 0) {
          throw new Error('O arquivo está vazio.');
        }

        // Parse header
        const headerLine = lines[0];
        const parsedHeaders = headerLine.split(',').map(h => h.trim());
        const matriculaIndex = parsedHeaders.findIndex(h => h.toLowerCase() === 'matricula');

        if (matriculaIndex === -1) {
          throw new Error('Coluna "matricula" não encontrada no CSV.');
        }

        // Add name and email columns if not exists
        let nameIndex = parsedHeaders.findIndex(h => h.toLowerCase() === 'name' || h.toLowerCase() === 'nome');
        if (nameIndex === -1) {
          parsedHeaders.push('name');
          nameIndex = parsedHeaders.length - 1;
        }

        let emailIndex = parsedHeaders.findIndex(h => h.toLowerCase() === 'email');
        if (emailIndex === -1) {
          parsedHeaders.push('email');
          emailIndex = parsedHeaders.length - 1;
        }

        setHeaders(parsedHeaders);

        const rows: ProcessedRow[] = [];
        const totalRows = lines.length - 1; // Exclude header
        let processedCount = 0;

        for (let i = 1; i < lines.length; i++) {
          const line = lines[i];
          
          processedCount++;
          const currentProgress = Math.round((processedCount / totalRows) * 100);
          setProgress(currentProgress);
          
          if (!line.trim()) {
            setStatusMessage(`Processando linha ${processedCount} de ${totalRows}...`);
            continue;
          }

          // Parse the line into columns
          const columns = line.split(',').map(c => c.trim());
          const matricula = columns[matriculaIndex] || '';

          // Create a new row with all the original columns
          const newRow = [...columns];
          
          // Ensure the row has enough columns to match headers
          while (newRow.length < parsedHeaders.length) {
            newRow.push('');
          }

          let foundUser = false;
          let userName = newRow[nameIndex] || ''; // Keep original name from CSV
          let userEmail = '';

          let hasDuplicates = false;
          let duplicateCount = 0;

          // If we have both matricula and name, try to fetch and filter user data
          if (matricula && userName) {
            try {
              const response = await apiService.getUsersByMatricula(matricula);
              if (response.success && response.data && response.data.length > 0) {
                // Filter users by name (case-insensitive)
                const matchedUsers = response.data.filter(
                  user => user.name.toLowerCase() === userName.toLowerCase()
                );
                
                if (matchedUsers.length > 0) {
                  // Use the first matched user
                  const matchedUser = matchedUsers[0];
                  userEmail = matchedUser.email || '';
                  newRow[emailIndex] = userEmail;
                  foundUser = true;
                  
                  // Check for duplicates
                  if (matchedUsers.length > 1) {
                    hasDuplicates = true;
                    duplicateCount = matchedUsers.length;
                  }
                }
                // If no match found, foundUser remains false
              }
            } catch (err) {
              console.error(`Erro ao buscar usuário para matrícula ${matricula}:`, err);
            }
          }

          rows.push({
            matricula,
            name: userName,
            email: userEmail,
            found: foundUser,
            hasDuplicates,
            duplicateCount,
            originalData: newRow
          });

          setStatusMessage(`Processando linha ${processedCount} de ${totalRows}...`);
        }

        setProgress(100);
        setProcessedRows(rows);
        setShowPreview(true);
        setStatusMessage('Processamento concluído! Revise os dados abaixo.');
      } catch (err: any) {
        setError(err.message || 'Erro ao processar o arquivo.');
      } finally {
        setProcessing(false);
      }
    };

    reader.onerror = () => {
      setError('Erro ao ler o arquivo.');
      setProcessing(false);
    };

    reader.readAsText(file);
  };

  const downloadCsv = () => {
    if (processedRows.length === 0) return;

    const csvLines = [headers.join(',')];
    processedRows.forEach(row => {
      csvLines.push(row.originalData.join(','));
    });

    const csvContent = csvLines.join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `mapped_users_${new Date().getTime()}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);
  };

  const resetProcess = () => {
    setProcessedRows([]);
    setHeaders([]);
    setShowPreview(false);
    setProgress(0);
    setStatusMessage('');
  };

  return (
    <div className="users-mapping-layout">
      <Menu />
      <div className="users-mapping-container">
        <div className="users-mapping-header">
          <h2>Mapeamento de Usuários</h2>
          <p>Faça upload de um arquivo CSV contendo uma coluna "matricula". O sistema irá buscar os usuários correspondentes e adicionar colunas "name" e "email".</p>
        </div>

        {!showPreview && (
          <div className="upload-section">
            <input 
              type="file" 
              accept=".csv" 
              onChange={handleFileChange} 
              className="file-input"
              disabled={processing}
            />
            <button 
              onClick={processFile} 
              disabled={!file || processing}
              className="process-button"
            >
              {processing ? 'Processando...' : 'Processar Arquivo'}
            </button>
          </div>
        )}

        {error && (
          <div className="error-message">
            {error}
          </div>
        )}

        {(processing || (statusMessage && !showPreview)) && (
          <div className="status-section">
            <div className="progress-bar-container">
              <div 
                className="progress-bar" 
                style={{ width: `${progress}%` }}
              ></div>
            </div>
            <p className="status-text">{statusMessage}</p>
          </div>
        )}

        {showPreview && processedRows.length > 0 && (
          <div className="preview-section">
            <h3>Revisão dos Dados Processados</h3>
            <p className="preview-info">
              Total de linhas: {processedRows.length} | 
              Usuários encontrados: {processedRows.filter(r => r.found).length} | 
              Não encontrados: {processedRows.filter(r => !r.found).length} | 
              Duplicados: {processedRows.filter(r => r.hasDuplicates).length}
            </p>
            
            <div className="preview-table-container">
              <table className="preview-table">
                <thead>
                  <tr>
                    <th>Matrícula</th>
                    <th>Nome</th>
                    <th>Email</th>
                    <th>Status</th>
                  </tr>
                </thead>
                <tbody>
                  {processedRows.map((row, index) => (
                    <tr 
                      key={index} 
                      className={
                        row.hasDuplicates ? 'has-duplicates' : 
                        !row.found ? 'not-found' : ''
                      }
                    >
                      <td>{row.matricula}</td>
                      <td>{row.name || '-'}</td>
                      <td>{row.email || '-'}</td>
                      <td>
                        {row.hasDuplicates ? (
                          <span className="status-badge duplicates">
                            ⚠️ {row.duplicateCount} duplicados
                          </span>
                        ) : row.found ? (
                          <span className="status-badge found">✓ Encontrado</span>
                        ) : (
                          <span className="status-badge not-found">✗ Não encontrado</span>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="preview-actions">
              <button onClick={resetProcess} className="reset-button">
                ← Processar Novamente
              </button>
              <button onClick={downloadCsv} className="download-button">
                <span>⬇️</span> Confirmar e Baixar CSV
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};

export default UsersMappingPage;
