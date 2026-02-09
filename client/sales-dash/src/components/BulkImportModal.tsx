import React, { useState } from "react"
import "./BulkImportModal.css"
import { apiService } from "../services/apiService"
import StandardModal from "../shared/StandardModal"
import { Progress, Select } from "@mantine/core"

interface Props {
  onClose: () => void
  onSuccess: () => void
  templateId: number
  title: string
}

const BulkImportModal: React.FC<Props> = ({ onClose, onSuccess, templateId, title }) => {
  const [file, setFile] = useState<File | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  // Template selection
  const [templates, setTemplates] = useState<any[]>([])
  const [selectedTemplate, setSelectedTemplate] = useState<number>(templateId || 0)
  
  // Step 1: File upload
  const [step, setStep] = useState<"upload" | "verification" | "mapping" | "result">("upload")
  const [mismatchWarning, setMismatchWarning] = useState<string | null>(null)
  const [pendingPreviewData, setPendingPreviewData] = useState<any>(null)
  
  // Step 2: Mapping data
  const [uploadId, setUploadId] = useState<string>("")
  const [detectedColumns, setDetectedColumns] = useState<string[]>([])
  const [sampleRows, setSampleRows] = useState<Record<string, string>[]>([])
  const [mappings, setMappings] = useState<Record<string, string>>({})
  const [requiredFields, setRequiredFields] = useState<string[]>([])
  const [optionalFields, setOptionalFields] = useState<string[]>([])
  const [dateFormat, setDateFormat] = useState<string>("MM/DD/YYYY")
  const [skipMissingContractNumber, setSkipMissingContractNumber] = useState<boolean>(true)
  const [allowAutoCreateGroups, setAllowAutoCreateGroups] = useState<boolean>(true)
  const [allowAutoCreatePVs, setAllowAutoCreatePVs] = useState<boolean>(true)
  
  // Step 3: Result
  const [resultMessage, setResultMessage] = useState<string>("")
  const [createdGroups, setCreatedGroups] = useState<string[]>([])
  const [createdPVs, setCreatedPVs] = useState<string[]>([])
  
  const user = JSON.parse(localStorage.getItem('user') || '{}');
  const isSuperAdmin = user.role?.toLowerCase() === 'superadmin' || user.roleName?.toLowerCase() === 'superadmin';

  React.useEffect(() => {
    const fetchTemplates = async () => {
      try {
        const resp = await apiService.getImportTemplates();
        if (resp.success && resp.data) {
          setTemplates(resp.data);
          if (resp.data.length > 0) {
            const found = resp.data.find((t: any) => t.id === templateId);
            if (found) {
              setSelectedTemplate(found.id);
            } else if (!isSuperAdmin || resp.data.length === 1) {
              setSelectedTemplate(resp.data[0].id);
            }
          }
        }
      } catch (err) {
        console.error("Failed to fetch templates", err);
      }
    };
    fetchTemplates();
  }, [templateId, isSuperAdmin]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setError(null)
    const files = e.target.files
    if (files && files.length > 0) {
      setFile(files[0])
    } else {
      setFile(null)
    }
  }

  const handleUpload = async () => {
    if (!file) {
      setError("Nenhum arquivo selecionado")
      return
    }

    setLoading(true)
    setError(null)
    
    try {
      const resp = await apiService.uploadImportFile(file, selectedTemplate)
      
      if (resp.success && resp.data) {
        if (resp.data.isTemplateMatch === false) {
          setMismatchWarning(resp.data.matchMessage || "Aviso: O arquivo não parece corresponder ao modelo selecionado.")
          setPendingPreviewData(resp.data)
          setStep("verification")
        } else {
          setUploadId(resp.data.uploadId)
          setDetectedColumns(resp.data.detectedColumns)
          setSampleRows(resp.data.sampleRows.slice(0, 5))
          setMappings(resp.data.suggestedMappings)
          setRequiredFields(resp.data.requiredFields)
          setOptionalFields(resp.data.optionalFields)
          setStep("mapping")
        }
      } else {
        setError(resp.message || "Falha ao fazer upload do arquivo")
      }
    } catch (err: any) {
      setError(err.message || "Erro ao fazer upload do arquivo")
    } finally {
      setLoading(false)
    }
  }

  const handleMappingChange = (column: string, targetField: string) => {
    setMappings(prev => ({
      ...prev,
      [column]: targetField
    }))
  }

  const handleConfirmMapping = async () => {
    setLoading(true)
    setError(null)

    try {
      const explicitlyMapped = Object.fromEntries(
        Object.entries(mappings).filter(([_, targetField]) => targetField !== "")
      )

      const mappingResp = await apiService.configureImportMappings(uploadId, explicitlyMapped, allowAutoCreateGroups, allowAutoCreatePVs, skipMissingContractNumber)
      
      if (!mappingResp.success) {
        setError(mappingResp.message || "Falha ao configurar mapeamentos")
        setLoading(false)
        return
      }

      if (mappingResp.data?.errors && mappingResp.data.errors.length > 0) {
        setError(mappingResp.data.errors.join("\n"))
        setLoading(false)
        return
      }

      const confirmResp = await apiService.confirmImport(uploadId, dateFormat, skipMissingContractNumber, allowAutoCreateGroups, allowAutoCreatePVs)
      
      if (confirmResp.success && confirmResp.data) {
        const { processedRows, failedRows, errors, createdGroups: newlyCreatedGroups, createdPVs: newlyCreatedPVs } = confirmResp.data
        setResultMessage(
          `Importados: ${processedRows}` +
          (failedRows > 0 ? `, Erros: ${failedRows}` : "")
        )
        
        if (newlyCreatedGroups && newlyCreatedGroups.length > 0) {
          setCreatedGroups(newlyCreatedGroups)
        }

        if (newlyCreatedPVs && newlyCreatedPVs.length > 0) {
          setCreatedPVs(newlyCreatedPVs)
        }
        
        if (errors && errors.length > 0) {
          setError(errors.join("\n"))
        }
        
        setStep("result")
        
        if (failedRows === 0) {
          onSuccess()
        }
      } else {
        setError(confirmResp.message || "Falha ao confirmar importação")
      }
    } catch (err: any) {
      setError(err.message || "Erro ao importar usuários")
    } finally {
      setLoading(false)
    }
  }

  const allRequiredFieldsMapped = () => {
    return requiredFields.every(field => 
      Object.values(mappings).includes(field)
    )
  }

  const renderFooter = () => {
    if (step === "upload") {
      return (
        <>
          <button type="button" className="btn-cancel" onClick={onClose} disabled={loading}>
            Cancelar
          </button>
          <button type="button" className="btn-submit" disabled={!file || loading || !selectedTemplate} onClick={handleUpload}>
            {loading ? "Enviando..." : "Próximo"}
          </button>
        </>
      )
    }

    if (step === "verification") {
      return (
        <>
          <button type="button" className="btn-cancel" onClick={() => setStep("upload")}>
            Tentar Outro
          </button>
          <button
            type="button"
            className="btn-submit"
            onClick={() => {
              const data = pendingPreviewData
              setUploadId(data.uploadId)
              setDetectedColumns(data.detectedColumns)
              setSampleRows(data.sampleRows.slice(0, 5))
              setMappings(data.suggestedMappings)
              setRequiredFields(data.requiredFields)
              setOptionalFields(data.optionalFields)
              setStep("mapping")
            }}
            style={{ background: '#ffa000' }}
          >
            Prosseguir Assim Mesmo
          </button>
        </>
      )
    }

    if (step === "mapping") {
      return (
        <>
          <button type="button" className="btn-cancel" onClick={() => setStep("upload")} disabled={loading}>
            Voltar
          </button>
          <button type="button" className="btn-submit" onClick={handleConfirmMapping} disabled={loading || !allRequiredFieldsMapped()}>
            {loading ? "Importando..." : "Confirmar e Importar"}
          </button>
        </>
      )
    }

    return (
      <button type="button" className="btn-submit" onClick={onClose}>
        Fechar
      </button>
    )
  }

  return (
    <StandardModal
      isOpen={true}
      onClose={onClose}
      title={title}
      size="xl"
      footer={renderFooter()}
    >
      {error && <div className="error-message">{error}</div>}
      {resultMessage && <div className="success-message">{resultMessage}</div>}

      {step === "upload" && (
        <>
          <div className="form-group">
            <label htmlFor="file">Arquivo CSV ou XLSX</label>
            <input
              id="file"
              name="file"
              type="file"
              accept=".csv,.xlsx,text/csv,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
              onChange={handleFileChange}
            />
            <p className="hint">
              Formato esperado: Colunas como name, email, role, etc.
            </p>
          </div>

          {isSuperAdmin && templates.length > 1 && (
            <div className="form-group">
              <label htmlFor="templateSelection">Selecione o Modelo de Importação</label>
              <select 
                id="templateSelection"
                value={selectedTemplate}
                onChange={(e) => setSelectedTemplate(Number(e.target.value))}
                className="template-select"
              >
                <option value={0} disabled>-- Selecione um modelo --</option>
                {templates.map(t => (
                  <option key={t.id} value={t.id}>{t.name}</option>
                ))}
              </select>
            </div>
          )}
        </>
      )}

      {step === "verification" && (
        <div className="verification-warning-section" style={{ textAlign: 'center', padding: '20px' }}>
          <div className="warning-icon" style={{ fontSize: '48px', marginBottom: '15px' }}>⚠️</div>
          <h3 style={{ color: '#ffcc00', marginBottom: '15px', fontSize: '1.2rem' }}>Modelo Divergente</h3>
          <p style={{ fontSize: '15px', marginBottom: '25px', lineHeight: '1.5', color: '#374151' }}>
            {mismatchWarning}
          </p>
          <p className="hint" style={{ marginBottom: '30px' }}>
            Este arquivo pode não ser processado corretamente com o modelo selecionado. Deseja prosseguir com o mapeamento manual ou carregar outro arquivo?
          </p>
        </div>
      )}

      {step === "mapping" && (
        <div className="mapping-section">
          <p className="hint" style={{ marginBottom: '20px' }}>
            Mapeie as colunas do arquivo para os campos do sistema.
            <br />
            <strong style={{ color: "red" }}>*</strong> = Campo obrigatório
          </p>

          <div className="sample-data-section">
            <h4 style={{ fontSize: '14px', marginBottom: '12px' }}>Primeiras 5 linhas do arquivo:</h4>
            <div className="preview-table-wrapper">
              <table className="preview-table">
                <thead>
                  <tr>
                    {detectedColumns.map((col) => (
                      <th key={col}>{col}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {sampleRows.map((row, idx) => (
                    <tr key={idx}>
                      {detectedColumns.map((col) => (
                        <td key={col}>{row[col] || ""}</td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>

          <div className="mappings-list">
            <h4 style={{ fontSize: '14px', marginBottom: '12px' }}>Mapeamentos:</h4>
            {detectedColumns.map((column) => {
              const mappedField = mappings[column] || ""
              const isRequired = requiredFields.includes(mappedField)
              
              return (
                <div key={column} className="mapping-row">
                  <label>
                    <strong>{column}</strong> →
                  </label>
                  <select
                    value={mappedField}
                    onChange={(e) => handleMappingChange(column, e.target.value)}
                  >
                    <option value="">-- Não mapear --</option>
                    <optgroup label="Campos Obrigatórios">
                      {requiredFields.map((field) => (
                        <option key={field} value={field}>
                          {field} *
                        </option>
                      ))}
                    </optgroup>
                    <optgroup label="Campos Opcionais">
                      {optionalFields.map((field) => (
                        <option key={field} value={field}>
                          {field}
                        </option>
                      ))}
                    </optgroup>
                  </select>
                  {isRequired && <span style={{ color: "red", marginLeft: "8px" }}>*</span>}
                </div>
              )
            })}
          </div>

          {(selectedTemplate === 2 || selectedTemplate === 3) && (
            <div className="import-options" style={{ marginTop: '20px', padding: '15px', background: '#f9fafb', border: '1px solid #e5e7eb', borderRadius: '8px' }}>
              <h4 style={{ fontSize: '14px', marginBottom: '12px' }}>Opções de Importação:</h4>
              
              <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                {selectedTemplate === 3 && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <input 
                      type="checkbox" 
                      id="skipMissingContractNumber" 
                      checked={skipMissingContractNumber} 
                      onChange={(e) => setSkipMissingContractNumber(e.target.checked)}
                    />
                    <label htmlFor="skipMissingContractNumber" style={{ fontSize: '13px', color: '#4b5563' }}>
                      Pular linhas sem número de contrato (útil para arquivos com subtotais ou lixo)
                    </label>
                  </div>
                )}

                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <input 
                    type="checkbox" 
                    id="allowAutoCreateGroups" 
                    checked={allowAutoCreateGroups} 
                    onChange={(e) => setAllowAutoCreateGroups(e.target.checked)}
                  />
                  <label htmlFor="allowAutoCreateGroups" style={{ fontSize: '13px', color: '#4b5563' }}>
                    Permitir criação automática de grupos
                  </label>
                </div>

                {selectedTemplate === 3 && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <input 
                      type="checkbox" 
                      id="allowAutoCreatePVs" 
                      checked={allowAutoCreatePVs} 
                      onChange={(e) => setAllowAutoCreatePVs(e.target.checked)}
                    />
                    <label htmlFor="allowAutoCreatePVs" style={{ fontSize: '13px', color: '#4b5563' }}>
                      Permitir criação automática de PV
                    </label>
                  </div>
                )}
              </div>
            </div>
          )}

          {!allRequiredFieldsMapped() && (
            <div className="error-message" style={{ marginTop: '20px' }}>
              Todos os campos obrigatórios devem ser mapeados: {requiredFields.join(", ")}
            </div>
          )}
        </div>
      )}

      {step === "result" && (
        <div className="result-section">
          {createdGroups.length > 0 && (
            <div className="created-groups-info" style={{ marginTop: '15px', padding: '10px', background: '#f0fdf4', borderRadius: '4px', border: '1px solid #dcfce7', textAlign: 'left' }}>
              <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: '#166534' }}>Grupos Criados Automaticamente:</h4>
              <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '13px', color: '#166534' }}>
                {createdGroups.map(group => (
                  <li key={group}>{group}</li>
                ))}
              </ul>
            </div>
          )}

          {createdPVs.length > 0 && (
            <div className="created-groups-info" style={{ marginTop: '15px', padding: '10px', background: '#f0fdf4', borderRadius: '4px', border: '1px solid #dcfce7', textAlign: 'left' }}>
              <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: '#166534' }}>PVs Criados Automaticamente:</h4>
              <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '13px', color: '#166534' }}>
                {createdPVs.map(pv => (
                  <li key={pv}>{pv}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </StandardModal>
  )
}

export default BulkImportModal
