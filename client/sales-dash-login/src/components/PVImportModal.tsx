import React, { useState } from "react";
import "./PVImportModal.css";
import { apiService, PVRequest } from "../services/apiService";

interface Props {
  onClose: () => void;
  onSuccess: () => void;
}

interface ParsedPV {
  id: number;
  name: string;
}

interface ImportResult {
  created: ParsedPV[];
  alreadyExisting: ParsedPV[];
  failed: { pv: ParsedPV; error: string }[];
}

const PVImportModal: React.FC<Props> = ({ onClose, onSuccess }) => {
  const [file, setFile] = useState<File | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  
  const [step, setStep] = useState<"upload" | "preview" | "result">("upload");
  
  // Preview data
  const [parsedPVs, setParsedPVs] = useState<ParsedPV[]>([]);
  const [duplicatesRemoved, setDuplicatesRemoved] = useState(0);
  
  // Result data
  const [importResult, setImportResult] = useState<ImportResult | null>(null);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setError(null);
    const files = e.target.files;
    if (files && files.length > 0) {
      setFile(files[0]);
    } else {
      setFile(null);
    }
  };

  // Normalize string: remove accents, lowercase, remove spaces
  const normalizeString = (str: string): string => {
    return str
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase()
      .replace(/\s+/g, "");
  };

  // Parse CSV content
  const parseCSV = (content: string): ParsedPV[] => {
    const lines = content.split("\n").filter(line => line.trim());
    if (lines.length === 0) {
      throw new Error("Arquivo CSV vazio");
    }

    // Parse header
    const header = lines[0].split(",").map(h => h.trim());
    
    // Find column indices (case and accent insensitive)
    const normalizedHeaders = header.map(h => normalizeString(h));
    
    let pvIdIndex = -1;
    let pvNameIndex = -1;
    
    // Look for PV ID column
    const pvIdPatterns = ["codigopv", "códigopv", "pvid", "id"];
    pvIdIndex = normalizedHeaders.findIndex(h => 
      pvIdPatterns.some(pattern => h.includes(pattern))
    );
    
    // Look for PV Name column
    const pvNamePatterns = ["pv", "nome", "name", "pontodevenda"];
    pvNameIndex = normalizedHeaders.findIndex(h => 
      pvNamePatterns.some(pattern => h === pattern || h.includes("pontodevenda"))
    );
    
    if (pvIdIndex === -1 || pvNameIndex === -1) {
      throw new Error(
        `Colunas necessárias não encontradas. Esperado: "Código PV" e "PV". ` +
        `Encontrado: ${header.join(", ")}`
      );
    }

    // Parse data rows
    const pvs: ParsedPV[] = [];
    for (let i = 1; i < lines.length; i++) {
      const values = lines[i].split(",").map(v => v.trim());
      
      if (values.length <= Math.max(pvIdIndex, pvNameIndex)) {
        continue; // Skip incomplete rows
      }
      
      const idStr = values[pvIdIndex];
      const name = values[pvNameIndex];
      
      if (!idStr || !name) {
        continue; // Skip rows with missing data
      }
      
      const id = parseInt(idStr, 10);
      if (isNaN(id)) {
        continue; // Skip rows with invalid ID
      }
      
      pvs.push({ id, name });
    }
    
    return pvs;
  };

  // Remove duplicates by PV ID
  const removeDuplicates = (pvs: ParsedPV[]): ParsedPV[] => {
    const seen = new Set<number>();
    const unique: ParsedPV[] = [];
    
    for (const pv of pvs) {
      if (!seen.has(pv.id)) {
        seen.add(pv.id);
        unique.push(pv);
      }
    }
    
    return unique;
  };

  const handleUpload = async () => {
    if (!file) {
      setError("Nenhum arquivo selecionado");
      return;
    }

    setLoading(true);
    setError(null);
    
    try {
      // Read file content
      const content = await file.text();
      
      // Parse CSV
      const allPVs = parseCSV(content);
      const uniquePVs = removeDuplicates(allPVs);
      
      setDuplicatesRemoved(allPVs.length - uniquePVs.length);
      setParsedPVs(uniquePVs);
      setStep("preview");
    } catch (err: any) {
      setError(err.message || "Erro ao processar arquivo");
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmImport = async () => {
    setLoading(true);
    setError(null);

    const result: ImportResult = {
      created: [],
      alreadyExisting: [],
      failed: [],
    };

    try {
      // Attempt to create each PV
      for (const pv of parsedPVs) {
        try {
          const pvData: PVRequest = { id: pv.id, name: pv.name };
          await apiService.createPV(pvData);
          result.created.push(pv);
        } catch (err: any) {
          const errorMessage = err.message || "";
          
          // Check if error indicates PV already exists
          if (
            errorMessage.toLowerCase().includes("already exists") ||
            errorMessage.toLowerCase().includes("já existe") ||
            errorMessage.toLowerCase().includes("duplicate")
          ) {
            result.alreadyExisting.push(pv);
          } else {
            result.failed.push({ pv, error: errorMessage });
          }
        }
      }
      
      setImportResult(result);
      setStep("result");
      
      // Refresh PV list if any were created
      if (result.created.length > 0) {
        onSuccess();
      }
    } catch (err: any) {
      setError(err.message || "Erro ao importar PVs");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Importar Pontos de Venda</h2>
          <button className="close-button" onClick={onClose}>
            ×
          </button>
        </div>

        <div className="import-form">
          {error && <div className="error-message">{error}</div>}

          {/* Step 1: Upload */}
          {step === "upload" && (
            <>
              <div className="form-group">
                <label htmlFor="file">Arquivo CSV</label>
                <input
                  id="file"
                  name="file"
                  type="file"
                  accept=".csv,text/csv"
                  onChange={handleFileChange}
                />
                <p className="hint">
                  Formato esperado: Colunas "Código PV" (ID numérico) e "PV" (nome).
                  <br />
                  A detecção de colunas é insensível a maiúsculas/minúsculas e acentuação.
                </p>
              </div>

              <div className="form-actions">
                <button
                  type="button"
                  className="btn-cancel"
                  onClick={onClose}
                  disabled={loading}
                >
                  Cancelar
                </button>
                <button
                  type="button"
                  className="btn-submit"
                  disabled={!file || loading}
                  onClick={handleUpload}
                >
                  {loading ? "Processando..." : "Próximo"}
                </button>
              </div>
            </>
          )}

          {/* Step 2: Preview */}
          {step === "preview" && (
            <>
              <div className="preview-section">
                <h3>Visualização dos Dados</h3>
                <div className="preview-stats">
                  <span>Total de PVs únicos: {parsedPVs.length}</span>
                  {duplicatesRemoved > 0 && (
                    <span style={{ color: "#fbbf24" }}>
                      Duplicados removidos: {duplicatesRemoved}
                    </span>
                  )}
                </div>

                <div className="preview-table-wrapper">
                  <table className="preview-table">
                    <thead>
                      <tr>
                        <th>Código PV</th>
                        <th>Nome</th>
                      </tr>
                    </thead>
                    <tbody>
                      {parsedPVs.slice(0, 10).map((pv, idx) => (
                        <tr key={idx}>
                          <td>{pv.id}</td>
                          <td>{pv.name}</td>
                        </tr>
                      ))}
                      {parsedPVs.length > 10 && (
                        <tr>
                          <td colSpan={2} style={{ textAlign: "center", color: "#9ca3af" }}>
                            ... e mais {parsedPVs.length - 10} PVs
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>

              <div className="form-actions">
                <button
                  type="button"
                  className="btn-cancel"
                  onClick={() => setStep("upload")}
                  disabled={loading}
                >
                  Voltar
                </button>
                <button
                  type="button"
                  className="btn-submit"
                  onClick={handleConfirmImport}
                  disabled={loading}
                >
                  {loading ? "Importando..." : "Confirmar e Importar"}
                </button>
              </div>
            </>
          )}

          {/* Step 3: Result */}
          {step === "result" && importResult && (
            <>
              <div className="result-section">
                <h3>Resultado da Importação</h3>
                
                <div className="result-stats">
                  <div className="result-stat success">
                    <strong>{importResult.created.length}</strong> PVs criados com sucesso
                  </div>
                  
                  {importResult.alreadyExisting.length > 0 && (
                    <div className="result-stat warning">
                      <strong>{importResult.alreadyExisting.length}</strong> PVs já existentes (não criados)
                    </div>
                  )}
                  
                  {importResult.failed.length > 0 && (
                    <div className="result-stat error">
                      <strong>{importResult.failed.length}</strong> PVs com erro
                    </div>
                  )}
                </div>

                {/* Show created PVs */}
                {importResult.created.length > 0 && (
                  <div className="result-detail">
                    <h4>Criados:</h4>
                    <ul>
                      {importResult.created.map((pv, idx) => (
                        <li key={idx}>
                          ID {pv.id}: {pv.name}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                {/* Show already existing PVs */}
                {importResult.alreadyExisting.length > 0 && (
                  <div className="result-detail">
                    <h4>Já Existentes:</h4>
                    <ul>
                      {importResult.alreadyExisting.map((pv, idx) => (
                        <li key={idx}>
                          ID {pv.id}: {pv.name}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}

                {/* Show failed PVs */}
                {importResult.failed.length > 0 && (
                  <div className="result-detail">
                    <h4>Erros:</h4>
                    <ul>
                      {importResult.failed.map((item, idx) => (
                        <li key={idx}>
                          ID {item.pv.id}: {item.pv.name} - {item.error}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </div>

              <div className="form-actions">
                <button
                  type="button"
                  className="btn-submit"
                  onClick={onClose}
                >
                  Fechar
                </button>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  );
};

export default PVImportModal;
