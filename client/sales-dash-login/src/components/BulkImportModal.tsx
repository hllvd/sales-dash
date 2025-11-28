import React, { useState } from "react"
import "./BulkImportModal.css"
import { apiService } from "../services/apiService"

interface Props {
  onClose: () => void
  onSuccess: () => void
}

const BulkImportModal: React.FC<Props> = ({ onClose, onSuccess }) => {
  const [file, setFile] = useState<File | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  
  // Step 1: File upload
  const [step, setStep] = useState<"upload" | "mapping" | "result">("upload")
  
  // Step 2: Mapping data
  const [uploadId, setUploadId] = useState<string>("")
  const [detectedColumns, setDetectedColumns] = useState<string[]>([])
  const [sampleRows, setSampleRows] = useState<Record<string, string>[]>([])
  const [mappings, setMappings] = useState<Record<string, string>>({})
  const [requiredFields, setRequiredFields] = useState<string[]>([])
  const [optionalFields, setOptionalFields] = useState<string[]>([])
  
  // Step 3: Result
  const [resultMessage, setResultMessage] = useState<string>("")

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
      // Upload file with templateId=1 (Users template)
      const resp = await apiService.uploadImportFile(file, 1)
      
      if (resp.success && resp.data) {
        setUploadId(resp.data.uploadId)
        setDetectedColumns(resp.data.detectedColumns)
        setSampleRows(resp.data.sampleRows.slice(0, 5)) // First 5 rows
        setMappings(resp.data.suggestedMappings) // Auto-mapped columns
        setRequiredFields(resp.data.requiredFields)
        setOptionalFields(resp.data.optionalFields)
        setStep("mapping")
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
      // Step 1: Configure mappings
      const mappingResp = await apiService.configureImportMappings(uploadId, mappings)
      
      if (!mappingResp.success) {
        setError(mappingResp.message || "Falha ao configurar mapeamentos")
        setLoading(false)
        return
      }

      // Step 2: Confirm import
      const confirmResp = await apiService.confirmImport(uploadId)
      
      if (confirmResp.success && confirmResp.data) {
        const { processedRows, failedRows, errors } = confirmResp.data
        setResultMessage(
          `Importados: ${processedRows}` +
          (failedRows > 0 ? `, Erros: ${failedRows}` : "")
        )
        
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

  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className="modal-content" onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h2>Importar Usuários em Lote</h2>
          <button className="close-button" onClick={onClose}>
            ×
          </button>
        </div>

        <div className="import-form">
          {error && <div className="error-message">{error}</div>}
          {resultMessage && <div className="success-message">{resultMessage}</div>}

          {/* Step 1: Upload */}
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
                  {loading ? "Enviando..." : "Próximo"}
                </button>
              </div>
            </>
          )}

          {/* Step 2: Mapping */}
          {step === "mapping" && (
            <>
              <div className="mapping-section">
                <h3>Mapeamento de Colunas</h3>
                <p className="hint">
                  Mapeie as colunas do arquivo para os campos do sistema.
                  <br />
                  <strong style={{ color: "red" }}>*</strong> = Campo obrigatório
                </p>

                {/* Sample Data Preview */}
                <div className="sample-data-section">
                  <h4>Primeiras 5 linhas do arquivo:</h4>
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

                {/* Column Mappings */}
                <div className="mappings-list">
                  <h4>Mapeamentos:</h4>
                  {detectedColumns.map((column) => {
                    const mappedField = mappings[column] || ""
                    const isRequired = requiredFields.includes(mappedField)
                    const isOptional = optionalFields.includes(mappedField)
                    
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
                        {isOptional && <span style={{ color: "gray", marginLeft: "8px" }}>(Opcional)</span>}
                      </div>
                    )
                  })}
                </div>

                {!allRequiredFieldsMapped() && (
                  <div className="error-message">
                    Todos os campos obrigatórios devem ser mapeados: {requiredFields.join(", ")}
                  </div>
                )}
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
                  onClick={handleConfirmMapping}
                  disabled={loading || !allRequiredFieldsMapped()}
                >
                  {loading ? "Importando..." : "Confirmar e Importar"}
                </button>
              </div>
            </>
          )}

          {/* Step 3: Result */}
          {step === "result" && (
            <>
              <div className="result-section">
                <h3>Resultado da Importação</h3>
                <p>{resultMessage}</p>
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
  )
}

export default BulkImportModal
