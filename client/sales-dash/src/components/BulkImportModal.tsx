import React, { useState } from "react"
import "./BulkImportModal.css"
import { apiService } from "../services/apiService"
import StandardModal from "../shared/StandardModal"
import InfoHelper from "../shared/InfoHelper"
import { downloadFailedRowsCsv } from "../utils/csvDownloader"
import { getFriendlyFieldName } from "../utils/normalization"

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
  const [selectedTemplateName, setSelectedTemplateName] = useState<string>("")
  const [detectedColumns, setDetectedColumns] = useState<string[]>([])
  const [sampleRows, setSampleRows] = useState<Record<string, string>[]>([])
  const [mappings, setMappings] = useState<Record<string, string>>({})
  const [requiredFields, setRequiredFields] = useState<string[]>([])
  const [optionalFields, setOptionalFields] = useState<string[]>([])
  const [dateFormat] = useState<string>("MM/DD/YYYY")
  const [showVideoModal, setShowVideoModal] = useState(false)
  const [skipMissingContractNumber, setSkipMissingContractNumber] = useState<boolean>(true)
  const [allowAutoCreateGroups, setAllowAutoCreateGroups] = useState<boolean>(true)
  const [allowAutoCreatePVs, setAllowAutoCreatePVs] = useState<boolean>(true)
  const [updateMatriculaOnExisting, setUpdateMatriculaOnExisting] = useState<boolean>(false)
  const [updateTotalAmountOnExisting, setUpdateTotalAmountOnExisting] = useState<boolean>(true)
  const [updateStartDateOnExisting, setUpdateStartDateOnExisting] = useState<boolean>(false)

  // Status column validation
  const [statusValidation, setStatusValidation] = useState<{
    isValid: boolean
    invalidValues: string[]
    unrecognizedValues: string[]
  } | null>(null)
  const [statusValidating, setStatusValidating] = useState(false)

  // Step 3: Result
  const [resultMessage, setResultMessage] = useState<string>("")
  const [createdGroups, setCreatedGroups] = useState<string[]>([])
  const [createdPVs, setCreatedPVs] = useState<string[]>([])
  const [warnings, setWarnings] = useState<string[]>([])
  const [desistenteContractNumbers, setDesistenteContractNumbers] = useState<string[]>([])
  
  const user = JSON.parse(localStorage.getItem('user') || '{}');
  const isSuperAdmin = user.role?.toLowerCase() === 'superadmin' || user.roleName?.toLowerCase() === 'superadmin';
  const isAdmin = user.role?.toLowerCase() === 'admin' || user.roleName?.toLowerCase() === 'admin';

  // Debug logging for mapping state
  React.useEffect(() => {
    if (step === "mapping") {
      console.log("--- DEBUG: Mapping State ---");
      console.log("Required Fields:", requiredFields);
      console.log("Mappings:", mappings);
      console.log("Detected Columns:", detectedColumns);
      console.log("Missing Fields:", requiredFields.filter(f => !Object.values(mappings).includes(f)));
      console.log("----------------------------");
    }
  }, [step, mappings, requiredFields, detectedColumns]);

  React.useEffect(() => {
    const fetchTemplates = async () => {
      try {
        const resp = await apiService.getImportTemplates();
        if (resp.success && resp.data) {
          setTemplates(resp.data);
          if (resp.data.length > 0) {
            const dashboardTemplate = resp.data.find((t: any) => t.name === "contractDashboard");
            if (isAdmin && dashboardTemplate) {
              setSelectedTemplate(dashboardTemplate.id);
            } else {
              const found = resp.data.find((t: any) => t.id === templateId);
              if (found) {
                setSelectedTemplate(found.id);
              } else if (!isSuperAdmin || resp.data.length === 1) {
                setSelectedTemplate(resp.data[0].id);
              }
            }
          }
        }
      } catch (err) {
        console.error("Failed to fetch templates", err);
      }
    };
    fetchTemplates();
  }, [templateId, isSuperAdmin, isAdmin]);

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setError(null)
    setDesistenteContractNumbers([])
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
          setSelectedTemplateName(resp.data.templateName)
          setDetectedColumns(resp.data.detectedColumns)
          setSampleRows(resp.data.sampleRows.slice(0, 5))
          setMappings(resp.data.suggestedMappings)
          setRequiredFields(resp.data.requiredFields)
          setOptionalFields(resp.data.optionalFields)
          setStep("mapping")

          // Auto-validate status if already mapped by suggestions
          const autoStatusCol = Object.entries(resp.data.suggestedMappings as Record<string, string>)
            .find(([, v]) => v === 'Status')?.[0]
          if (autoStatusCol) {
            runStatusValidation(autoStatusCol, resp.data.uploadId)
          }
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

  const runStatusValidation = async (columnName: string, uploadIdOverride?: string) => {
    const id = uploadIdOverride ?? uploadId
    if (!id) return
    setStatusValidating(true)
    try {
      const resp = await apiService.validateStatusColumn(id, columnName)
      if (resp.success && resp.data) {
        setStatusValidation({
          isValid: resp.data.isValid,
          invalidValues: resp.data.invalidValues,
          unrecognizedValues: resp.data.unrecognizedValues || [],
        })
      }
    } catch {
      setStatusValidation(null)
    } finally {
      setStatusValidating(false)
    }
  }

  const handleMappingChange = (column: string, targetField: string) => {
    setMappings(prev => ({
      ...prev,
      [column]: targetField
    }))
    if (targetField === 'Status') {
      runStatusValidation(column)
    } else {
      // If user remaps away from Status, clear validation
      const prevStatusColumn = Object.entries(mappings).find(([, v]) => v === 'Status')?.[0]
      if (prevStatusColumn === column) setStatusValidation(null)
    }
  }

  const handleConfirmMapping = async () => {
    setLoading(true)
    setError(null)

    try {
      const explicitlyMapped = Object.fromEntries(
        Object.entries(mappings).filter(([_, targetField]) => targetField !== "")
      )

      const mappingResp = await apiService.configureImportMappings(
        uploadId, 
        explicitlyMapped, 
        allowAutoCreateGroups, 
        allowAutoCreatePVs, 
        skipMissingContractNumber,
        selectedTemplateName,
        updateMatriculaOnExisting,
        updateTotalAmountOnExisting,
        updateStartDateOnExisting
      )
      
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

      const confirmResp = await apiService.confirmImport(
        uploadId, 
        dateFormat, 
        skipMissingContractNumber, 
        allowAutoCreateGroups, 
        allowAutoCreatePVs,
        selectedTemplateName,
        updateMatriculaOnExisting,
        updateTotalAmountOnExisting,
        updateStartDateOnExisting
      )
      
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

        if (confirmResp.data.warnings && confirmResp.data.warnings.length > 0) {
          setWarnings(confirmResp.data.warnings)
        }

        if (confirmResp.data.desistenteContractNumbers && confirmResp.data.desistenteContractNumbers.length > 0) {
          setDesistenteContractNumbers(confirmResp.data.desistenteContractNumbers)
        }
        
        if (failedRows > 0) {
          downloadFailedRowsCsv(confirmResp.data.failedRowsDetails, 'bulk_import_errors');
        }

        setStep("result")
      } else {
        setError(confirmResp.message || "Falha ao confirmar importação")
      }
    } catch (err: any) {
      setError(err.message || "Erro ao importar usuários")
    } finally {
      setLoading(false)
    }
  }

  const getMissingRequiredFields = () => {
    return requiredFields.filter(field => 
      !Object.values(mappings).includes(field)
    )
  }

  const allRequiredFieldsMapped = () => {
    return getMissingRequiredFields().length === 0
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
          <button type="button" className="btn-submit" onClick={handleConfirmMapping} disabled={loading || !allRequiredFieldsMapped() || (statusValidation !== null && !statusValidation.isValid)}>
            {loading ? (
              <>
                <span className="btn-spinner"></span>
                Importando...
              </>
            ) : (
              "Confirmar e Importar"
            )}
          </button>
        </>
      )
    }

    return (
      <button type="button" className="btn-submit" onClick={() => {
        onSuccess() // This reloads data and closes the modal via the parent component
      }}>
        Fechar
      </button>
    )
  }

  const helperContent = (
    <div className="info-helper-card">
      <div className="info-helper-card-icon">
        <svg width="20" height="20" viewBox="0 0 24 24" fill="white" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <polygon points="5 3 19 12 5 21 5 3"></polygon>
        </svg>
      </div>
      <div className="info-helper-card-body">
        <h4>Tutorial em vídeo disponível</h4>
        <p>Veja o passo a passo completo de como preparar sua planilha e importar contratos com sucesso.</p>
        <a href="#" className="info-helper-btn" onClick={(e) => { e.preventDefault(); setShowVideoModal(true); }}>
          Assistir tutorial
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ marginLeft: "4px" }}>
            <line x1="7" y1="17" x2="17" y2="7"></line>
            <polyline points="7 7 17 7 17 17"></polyline>
          </svg>
        </a>
      </div>
    </div>
  );

  return (
    <>
      <StandardModal
        isOpen={true}
        onClose={onClose}
        title={title}
        size="xl"
        footer={renderFooter()}
        headerActions={templateId === 3 ? <InfoHelper label="Como Importar?">{helperContent}</InfoHelper> : undefined}
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

          {!isAdmin && templates.length > 1 && (
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
                          {getFriendlyFieldName(field)} *
                        </option>
                      ))}
                    </optgroup>
                    <optgroup label="Campos Opcionais">
                      {optionalFields.map((field) => (
                        <option key={field} value={field}>
                          {getFriendlyFieldName(field)}
                        </option>
                      ))}
                    </optgroup>
                  </select>
                  {isRequired && !mappings[column] && <span style={{ color: "#d97706", marginLeft: "8px", fontSize: "12px" }}>(Recomendado)</span>}
                  {isRequired && mappings[column] && <span style={{ color: "#059669", marginLeft: "8px" }}>✓</span>}
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
                  <>
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

                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                      <input 
                        type="checkbox" 
                        id="updateMatriculaOnExisting" 
                        checked={updateMatriculaOnExisting} 
                        onChange={(e) => setUpdateMatriculaOnExisting(e.target.checked)}
                      />
                      <label htmlFor="updateMatriculaOnExisting" style={{ fontSize: '13px', color: '#4b5563' }}>
                        Atualizar matrícula em contratos existentes
                      </label>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                      <input 
                        type="checkbox" 
                        id="updateTotalAmountOnExisting" 
                        checked={updateTotalAmountOnExisting} 
                        onChange={(e) => setUpdateTotalAmountOnExisting(e.target.checked)}
                      />
                      <label htmlFor="updateTotalAmountOnExisting" style={{ fontSize: '13px', color: '#4b5563' }}>
                        Atualizar valor total em contratos existentes
                      </label>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                      <input 
                        type="checkbox" 
                        id="updateStartDateOnExisting" 
                        checked={updateStartDateOnExisting} 
                        onChange={(e) => setUpdateStartDateOnExisting(e.target.checked)}
                      />
                      <label htmlFor="updateStartDateOnExisting" style={{ fontSize: '13px', color: '#4b5563' }}>
                        Atualizar data do contrato
                      </label>
                    </div>
                  </>
                )}
              </div>
            </div>
          )}

          {!allRequiredFieldsMapped() && (
            <div className="error-message" style={{ marginTop: '20px', background: '#fff7ed', color: '#9a3412', border: '1px solid #fed7aa' }}>
              <strong>Atenção:</strong> Faltam os seguintes campos obrigatórios: {getMissingRequiredFields().map(getFriendlyFieldName).join(", ")}
            </div>
          )}

          {statusValidating && (
            <div style={{ marginTop: '16px', fontSize: '13px', color: '#6b7280', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span>⏳</span> Verificando valores de status...
            </div>
          )}

          {statusValidation !== null && !statusValidation.isValid && (
            <div
              id="status-validation-warning"
              className="error-message"
              style={{ marginTop: '16px', background: '#fef2f2', color: '#991b1b', border: '1px solid #fca5a5' }}
            >
              <strong>⚠️ Valores de Status Inválidos</strong>
              <p style={{ margin: '6px 0 0' }}>
                A coluna mapeada para <strong>Status</strong> contém valores não reconhecidos:{' '}
                <strong>{statusValidation.invalidValues.join(', ')}</strong>
              </p>
              <p style={{ margin: '4px 0 0', fontSize: '12px', color: '#7f1d1d' }}>
                Verifique se a coluna correta está mapeada para "Status", ou corrija os valores no arquivo.
              </p>
            </div>
          )}

          {loading && (
            <div className="import-loading-indicator">
              <div className="import-loading-spinner"></div>
              <div>
                <strong>Processando importação...</strong>
                <p style={{ margin: '4px 0 0', fontSize: '12px', color: '#1e40af' }}>
                  Arquivos grandes com milhares de registros podem levar alguns instantes. Por favor, aguarde sem fechar a página.
                </p>
              </div>
            </div>
          )}

          {statusValidation !== null && statusValidation.isValid && (
            <div style={{ marginTop: '16px', fontSize: '13px', color: '#15803d', display: 'flex', alignItems: 'center', gap: '6px' }}>
              <span>✅</span> Todos os valores de status são válidos.
            </div>
          )}

          {statusValidation !== null && statusValidation.unrecognizedValues && statusValidation.unrecognizedValues.length > 0 && (
            <div
              id="status-unrecognized-warning"
              style={{ marginTop: '16px', padding: '12px', borderRadius: '6px', background: '#fffbeb', color: '#b45309', border: '1px solid #fde68a' }}
            >
              <strong style={{ display: 'block', marginBottom: '4px' }}>⚠️ Status Não Mapeados Detectados</strong>
              {statusValidation.unrecognizedValues.map((val: string) => (
                <p key={val} style={{ margin: '4px 0 0', fontSize: '13px' }}>
                  We detected the status "{val}" and we will define it as "Nao definido"
                </p>
              ))}
            </div>
          )}
        </div>
      )}

      {step === "result" && (
        <div className="result-section">
          {desistenteContractNumbers.length > 0 && (
            <div className="desistente-skipped-info" style={{ marginTop: '15px', padding: '10px', background: '#fffbeb', borderRadius: '4px', border: '1px solid #fef3c7', textAlign: 'left' }} data-testid="desistente-skipped-warning">
              <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: '#b45309' }}>Contratos com status "DESISTENTE" detectados:</h4>
              <p style={{ margin: '0 0 8px 0', fontSize: '13px', color: '#b45309' }}>
                Detectamos {desistenteContractNumbers.length} contrato(s) com status "DESISTENTE". Eles não foram importados:
              </p>
              <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '13px', color: '#b45309' }}>
                {desistenteContractNumbers.slice(0, 10).map(num => (
                  <li key={num}><strong>{num}</strong></li>
                ))}
                {desistenteContractNumbers.length > 10 && (
                  <li>... e mais {desistenteContractNumbers.length - 10} contrato(s).</li>
                )}
              </ul>
            </div>
          )}

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

          {warnings.length > 0 && (
            <div className="warnings-info" style={{ marginTop: '15px', padding: '10px', background: '#fffbeb', borderRadius: '4px', border: '1px solid #fef3c7', textAlign: 'left' }}>
              <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: '#92400e' }}>Avisos:</h4>
              <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '13px', color: '#92400e' }}>
                {warnings.map((warning, idx) => (
                  <li key={idx} style={{ whiteSpace: 'pre-wrap' }}>{warning}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </StandardModal>

    {showVideoModal && (
      <StandardModal
        isOpen={true}
        onClose={() => setShowVideoModal(false)}
        title="Como Importar Contratos em Lote"
        size="lg"
      >
        <div style={{ position: 'relative', paddingBottom: '56.25%', height: 0, overflow: 'hidden', borderRadius: '8px' }}>
          <iframe
            style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%', border: 0 }}
            src="https://www.youtube.com/embed/2QQs2FGnaqs"
            title="Como Importar Contratos em Lote"
            allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
            allowFullScreen
          ></iframe>
        </div>
      </StandardModal>
    )}
  </>
  )
}

export default BulkImportModal
