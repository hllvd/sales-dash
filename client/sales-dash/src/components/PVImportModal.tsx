import React, { useState } from "react";
import { Title, Text } from "@mantine/core";
import "./PVImportModal.css";
import { apiService, PVRequest } from "../services/apiService";
import StandardModal from "../shared/StandardModal";

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

  const normalizeString = (str: string): string => {
    return str
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .toLowerCase()
      .replace(/\s+/g, "");
  };

  const parseCSV = (content: string): ParsedPV[] => {
    const lines = content.split("\n").filter(line => line.trim());
    if (lines.length === 0) {
      throw new Error("Arquivo CSV vazio");
    }

    const header = lines[0].split(",").map(h => h.trim());
    const normalizedHeaders = header.map(h => normalizeString(h));
    
    let pvIdIndex = -1;
    let pvNameIndex = -1;
    
    const pvIdPatterns = ["codigopv", "códigopv", "pvid", "id"];
    pvIdIndex = normalizedHeaders.findIndex(h => 
      pvIdPatterns.some(pattern => h.includes(pattern))
    );
    
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

    const pvs: ParsedPV[] = [];
    for (let i = 1; i < lines.length; i++) {
      const values = lines[i].split(",").map(v => v.trim());
      
      if (values.length <= Math.max(pvIdIndex, pvNameIndex)) {
        continue;
      }
      
      const idStr = values[pvIdIndex];
      const name = values[pvNameIndex];
      
      if (!idStr || !name) {
        continue;
      }
      
      const id = parseInt(idStr, 10);
      if (isNaN(id)) {
        continue;
      }
      
      pvs.push({ id, name });
    }
    
    return pvs;
  };

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
      const content = await file.text();
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
      for (const pv of parsedPVs) {
        try {
          const pvData: PVRequest = { id: pv.id, name: pv.name };
          await apiService.createPV(pvData);
          result.created.push(pv);
        } catch (err: any) {
          const errorMessage = err.message || "";
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
      
      if (result.created.length > 0) {
        onSuccess();
      }
    } catch (err: any) {
      setError(err.message || "Erro ao importar PVs");
    } finally {
      setLoading(false);
    }
  };

  const renderFooter = () => {
    if (step === "upload") {
      return (
        <>
          <button type="button" className="btn-cancel" onClick={onClose} disabled={loading}>
            Cancelar
          </button>
          <button type="button" className="btn-submit" disabled={!file || loading} onClick={handleUpload}>
            {loading ? "Processando..." : "Próximo"}
          </button>
        </>
      );
    }
    
    if (step === "preview") {
      return (
        <>
          <button type="button" className="btn-cancel" onClick={() => setStep("upload")} disabled={loading}>
            Voltar
          </button>
          <button type="button" className="btn-submit" onClick={handleConfirmImport} disabled={loading}>
            {loading ? "Importando..." : "Confirmar e Importar"}
          </button>
        </>
      );
    }
    
    return (
      <button type="button" className="btn-submit" onClick={onClose}>
        Fechar
      </button>
    );
  };

  return (
    <StandardModal
      isOpen={true}
      onClose={onClose}
      title="Importar Pontos de Venda"
      size="lg"
      footer={renderFooter()}
    >
      {error && <div className="error-message">{error}</div>}

      {step === "upload" && (
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
      )}

      {step === "preview" && (
        <div className="preview-section">
          <Title order={4} mb="md">Visualização dos Dados</Title>
          <div style={{ display: 'flex', gap: '16px', marginBottom: '16px', fontSize: '14px', color: '#6b7280' }}>
            <span>Total de PVs únicos: {parsedPVs.length}</span>
            {duplicatesRemoved > 0 && (
              <span style={{ color: "#fbbf24" }}>
                Duplicados removidos: {duplicatesRemoved}
              </span>
            )}
          </div>

          <div style={{ overflow: 'auto', border: '1px solid #e5e7eb', borderRadius: '8px' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
              <thead>
                <tr style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb' }}>
                  <th style={{ padding: '12px', textAlign: 'left', fontSize: '13px', fontWeight: 600 }}>Código PV</th>
                  <th style={{ padding: '12px', textAlign: 'left', fontSize: '13px', fontWeight: 600 }}>Nome</th>
                </tr>
              </thead>
              <tbody>
                {parsedPVs.slice(0, 10).map((pv: ParsedPV, idx: number) => (
                  <tr key={idx} style={{ borderBottom: '1px solid #e5e7eb' }}>
                    <td style={{ padding: '10px 12px', fontSize: '13px' }}>{pv.id}</td>
                    <td style={{ padding: '10px 12px', fontSize: '13px' }}>{pv.name}</td>
                  </tr>
                ))}
                {parsedPVs.length > 10 && (
                  <tr>
                    <td colSpan={2} style={{ textAlign: "center", padding: '12px', color: "#9ca3af", fontSize: '13px' }}>
                      ... e mais {parsedPVs.length - 10} PVs
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {step === "result" && importResult && (
        <div className="result-section">
          <Title order={4} mb="md">Resultado da Importação</Title>
          
          <div className="result-stats">
            <div className="result-stat" style={{ color: '#059669', marginBottom: '8px' }}>
              <strong>{importResult.created.length}</strong> PVs criados com sucesso
            </div>
            
            {importResult.alreadyExisting.length > 0 && (
              <div className="result-stat" style={{ color: '#d97706', marginBottom: '8px' }}>
                <strong>{importResult.alreadyExisting.length}</strong> PVs já existentes (não criados)
              </div>
            )}
            
            {importResult.failed.length > 0 && (
              <div className="result-stat" style={{ color: '#dc2626', marginBottom: '8px' }}>
                <strong>{importResult.failed.length}</strong> PVs com erro
              </div>
            )}
          </div>

          {importResult.created.length > 0 && (
            <div className="result-detail" style={{ marginTop: '20px' }}>
              <Text fw={600} size="sm">Criados:</Text>
              <ul style={{ listStyle: 'none', padding: 0, marginTop: '8px' }}>
                {importResult.created.map((pv: ParsedPV, idx: number) => (
                  <li key={idx} style={{ fontSize: '13px', color: '#4b5563', padding: '4px 0' }}>
                    ID {pv.id}: {pv.name}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {importResult.alreadyExisting.length > 0 && (
            <div className="result-detail" style={{ marginTop: '20px' }}>
              <Text fw={600} size="sm">Já Existentes:</Text>
              <ul style={{ listStyle: 'none', padding: 0, marginTop: '8px' }}>
                {importResult.alreadyExisting.map((pv: ParsedPV, idx: number) => (
                  <li key={idx} style={{ fontSize: '13px', color: '#4b5563', padding: '4px 0' }}>
                    ID {pv.id}: {pv.name}
                  </li>
                ))}
              </ul>
            </div>
          )}

          {importResult.failed.length > 0 && (
            <div className="result-detail" style={{ marginTop: '20px' }}>
              <Text fw={600} size="sm" style={{ color: '#dc2626' }}>Erros:</Text>
              <ul style={{ listStyle: 'none', padding: 0, marginTop: '8px' }}>
                {importResult.failed.map((item: { pv: ParsedPV; error: string }, idx: number) => (
                  <li key={idx} style={{ fontSize: '13px', color: '#dc2626', padding: '4px 0' }}>
                    ID {item.pv.id}: {item.pv.name} - {item.error}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </StandardModal>
  );
};

export default PVImportModal;
