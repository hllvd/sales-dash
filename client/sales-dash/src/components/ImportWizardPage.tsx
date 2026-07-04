import React, { useState } from 'react';
import { Title, Button, Stepper, Group, FileInput, Text, Paper, Badge, Alert, Stack, List, LoadingOverlay, Box, Modal } from '@mantine/core';
import { IconUpload, IconDownload, IconCheck, IconAlertCircle, IconChevronRight, IconChevronLeft } from '@tabler/icons-react';
import Menu from './Menu';
import { apiService } from '../services/apiService';
import { toast } from '../utils/toast';
import { downloadFailedRowsCsv } from '../utils/csvDownloader';
import '../shared/InfoHelper.css';

const ImportWizardPage: React.FC = () => {
  const [activeStep, setActiveStep] = useState(0);
  const [loading, setLoading] = useState(false);
  const [loadingMessage, setLoadingMessage] = useState('Aguarde...');
  const [videoModalOpen, setVideoModalOpen] = useState(false);
  
  // Step 1: Upload Contract File
  const [contractFile, setContractFile] = useState<File | null>(null);
  const [uploadData, setUploadData] = useState<any>(null);
  const [mismatchWarning, setMismatchWarning] = useState<string | null>(null);
  const [duplicateContracts, setDuplicateContracts] = useState<string[]>([]);
  const [allowDuplicates, setAllowDuplicates] = useState(false);
  const [desistenteContracts, setDesistenteContracts] = useState<string[]>([]);
  const [allowDesistentes, setAllowDesistentes] = useState(false);
  const [allowInconsistencies, setAllowInconsistencies] = useState(false);

  // Step 2: Users File
  const [usersFile, setUsersFile] = useState<File | null>(null);
  const [importResult, setImportResult] = useState<any>(null);
  const [duplicateEmailConflicts, setDuplicateEmailConflicts] = useState<string[]>([]);
  const [step2Error, setStep2Error] = useState<string | null>(null);

  // Step 3: Contract import options (defaults all ON)
  const [skipMissingContractNumber, setSkipMissingContractNumber] = useState(true);
  const [allowAutoCreateGroups, setAllowAutoCreateGroups] = useState(true);
  const [allowAutoCreatePVs, setAllowAutoCreatePVs] = useState(true);
  const [contractImportResult, setContractImportResult] = useState<any>(null);
  const [contractImportLoading, setContractImportLoading] = useState(false);
  const [tempFileReady, setTempFileReady] = useState(false);

  const handleStep1Upload = async (forceMatch: boolean = false) => {
    if (!contractFile) {
      toast.error('Por favor, selecione o arquivo de contratos');
      return;
    }

    setLoadingMessage('Processando arquivo de contratos…');
    setLoading(true);
    setMismatchWarning(null);
    setDuplicateContracts([]);
    setAllowDuplicates(false);
    setDesistenteContracts([]);
    setAllowDesistentes(false);
    setAllowInconsistencies(false);
    try {
      const response = await apiService.uploadWizardStep1(contractFile);
      if (response.success) {
        if (!forceMatch && response.data.isTemplateMatch === false) {
          setMismatchWarning(response.data.matchMessage || 'Aviso: O arquivo não parece corresponder ao modelo selecionado.');
          setUploadData(response.data);
          toast.warning('Aviso: Modelo de arquivo divergente');
        } else {
          setUploadData(response.data);
          const dupes: string[] = response.data.duplicateContractNumbers ?? [];
          const desistentes: string[] = response.data.desistenteContractNumbers ?? [];
          const confNames: string[] = response.data.conflictingUserNames ?? [];
          const confMats: string[] = response.data.conflictingMatriculas ?? [];
          
          let hasWarning = false;
          if (dupes.length > 0) {
            setDuplicateContracts(dupes);
            toast.warning(`${dupes.length} número(s) de contrato duplicado(s) encontrado(s)`);
            hasWarning = true;
          }
          if (desistentes.length > 0) {
            setDesistenteContracts(desistentes);
            toast.warning(`${desistentes.length} contrato(s) com status "desistente" encontrado(s)`);
            hasWarning = true;
          }
          if (confNames.length > 0 || confMats.length > 0) {
            toast.warning('Aviso: Inconsistências de cadastro de usuários encontradas');
          }

          if (!hasWarning) {
            setActiveStep(1);
            toast.success('Arquivo processado com sucesso');
          }
        }
      }
    } catch (err: any) {
      toast.error(err.message || 'Falha ao processar arquivo');
    } finally {
      setLoading(false);
    }
  };

  const handleDownloadTemplate = async () => {
    if (!uploadData?.uploadId) return;

    try {
      const blob = await apiService.downloadWizardTemplate(uploadData.uploadId);
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = 'users.xlsx';
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      toast.success('Modelo users.xlsx baixado');
    } catch (err: any) {
      toast.error('Falha ao baixar modelo');
    }
  };

  const handleStep2Import = async () => {
    if (!usersFile) {
      toast.error('Por favor, selecione o arquivo de usuários preenchido');
      return;
    }

    setLoadingMessage('Importando usuários e matrículas…');
    setLoading(true);
    setStep2Error(null);
    setDuplicateEmailConflicts([]);
    try {
      const response = await apiService.runWizardStep2(uploadData.uploadId, usersFile);
      if (response.success && response.data) {
        setImportResult(response.data);
        if (response.data.failedRows > 0) {
          downloadFailedRowsCsv(response.data.failedRowsDetails, 'wizard_users_errors');
        }
        setActiveStep(2);
        toast.success('Usuários e matrículas importados com sucesso');
      }
    } catch (err: any) {
      const errorMsg = err.message || 'Falha na importação de usuários';
      if (errorMsg.includes('está associado a múltiplos usuários')) {
        const lines = errorMsg.split('\n').filter(Boolean);
        setDuplicateEmailConflicts(lines);
      } else {
        setStep2Error(errorMsg);
      }
      toast.error(errorMsg);
    } finally {
      setLoading(false);
    }
  };

  const handleDownloadContracts = async () => {
    if (!uploadData?.uploadId) return;

    setLoading(true);
    try {
      await apiService.downloadWizardContracts(uploadData.uploadId);
      setTempFileReady(true);
      toast.success('Arquivo contracts.xlsx baixado com sucesso');
    } catch (err: any) {
      toast.error('Falha ao baixar contratos enriquecidos');
    } finally {
      setLoading(false);
    }
  };

  const handleImportContracts = async () => {
    if (!uploadData?.uploadId) return;

    // If the user hasn't downloaded yet, generate the temp file first
    if (!tempFileReady) {
      setLoadingMessage('Gerando arquivo temporário…');
      setLoading(true);
      try {
        await apiService.downloadWizardContracts(uploadData.uploadId);
        setTempFileReady(true);
      } catch (err: any) {
        toast.error('Falha ao preparar arquivo de contratos');
        setLoading(false);
        return;
      } finally {
        setLoading(false);
      }
    }

    setLoadingMessage('Importando contratos…');
    setContractImportLoading(true);
    try {
      const response = await apiService.runWizardStep3Import(uploadData.uploadId, {
        skipMissingContractNumber,
        allowAutoCreateGroups,
        allowAutoCreatePVs,
        dateFormat: 'MM/DD/YYYY',
      });
      if (response.success && response.data) {
        setContractImportResult(response.data);
        if (response.data.failedRows > 0) {
          downloadFailedRowsCsv(response.data.failedRowsDetails, 'wizard_contracts_errors');
        }
        toast.success('Contratos importados com sucesso');
      }
    } catch (err: any) {
      toast.error(err.message || 'Falha ao importar contratos');
    } finally {
      setContractImportLoading(false);
    }
  };

  const prevStep = () => setActiveStep((current) => (current > 0 ? current - 1 : current));

  return (
    <Menu>
      <Box 
        translate="no" 
        pos="relative" 
        style={{ maxWidth: '1000px', margin: '0 auto', padding: '20px' }}
      >
        <LoadingOverlay
          visible={loading}
          overlayProps={{ radius: 'sm', blur: 3, backgroundOpacity: 0.55 }}
          loaderProps={{ type: 'dots', size: 'xl', color: 'blue' }}
          zIndex={300}
        />

        {loading && (
          <Box
            style={{
              position: 'fixed',
              bottom: '2rem',
              left: '50%',
              transform: 'translateX(-50%)',
              zIndex: 400,
              background: 'rgba(0,0,0,0.75)',
              color: '#fff',
              padding: '10px 24px',
              borderRadius: '2rem',
              fontSize: '0.9rem',
              fontWeight: 500,
              letterSpacing: '0.02em',
              pointerEvents: 'none',
            }}
          >
            <span translate="no">{loadingMessage}</span>
          </Box>
        )}
        
        <Group justify="space-between" align="center" mb="xl">
          <Title order={2} className="page-title-break">Assistente de Importação Completa</Title>
          <button 
            type="button" 
            className="info-helper-trigger" 
            onClick={() => setVideoModalOpen(true)}
          >
            <span className="info-helper-icon">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10"></circle>
                <path d="M9.09 9a3 3 0 0 1 5.83 1c0 2-3 3-3 3"></path>
                <line x1="12" y1="17" x2="12.01" y2="17"></line>
              </svg>
            </span>
            <span className="info-helper-label">Como importar o modelo de retenção</span>
          </button>
        </Group>

        <Modal 
          opened={videoModalOpen} 
          onClose={() => setVideoModalOpen(false)} 
          title={<Text fw={600} c="#4b5563">Tutorial: Como importar o modelo de retenção</Text>}
          size="xl"
          centered
        >
          <div style={{ position: 'relative', paddingBottom: '56.25%', height: 0, overflow: 'hidden', borderRadius: '8px' }}>
            <iframe 
              src="https://www.youtube.com/embed/9F7Uvd30Tuk" 
              style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%', border: 'none' }}
              allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" 
              allowFullScreen 
              title="Tutorial de Importação"
            />
          </div>
        </Modal>

        <Stepper active={activeStep} allowNextStepsSelect={false}>
          <Stepper.Step 
            label="Upload de Contratos" 
            description="Carregar modelo histórico"
            icon={<IconUpload size={18} />}
          >
            <Paper withBorder p="md" mt="md">
              <Stack gap="md">
                <Text size="sm">
                  O primeiro passo é carregar o arquivo histórico de contratos. 
                  Geralmente este arquivo tem o nome <strong>"modelo_referencia_retencao"</strong>.
                </Text>
                
                <FileInput
                  label="Arquivo de Contratos"
                  placeholder="Selecione o arquivo .csv ou .xlsx"
                  required
                  value={contractFile}
                  onChange={(f) => {
                    setContractFile(f);
                    setMismatchWarning(null);
                    setDuplicateContracts([]);
                    setAllowDuplicates(false);
                    setDesistenteContracts([]);
                    setAllowDesistentes(false);
                    setAllowInconsistencies(false);
                  }}
                  accept=".csv,.xlsx"
                  fileInputProps={{ id: 'wizard-step1-input' }}
                  leftSection={<IconUpload size={16} />}
                />

                {mismatchWarning && (
                  <Alert icon={<IconAlertCircle size={16} />} title="Modelo Divergente" color="orange" mt="md">
                    <Text size="sm" mb="md">{mismatchWarning}</Text>
                    <Group>
                      <Button variant="outline" color="orange" size="xs" onClick={() => setContractFile(null)}>
                        Tentar outro
                      </Button>
                      <Button color="orange" size="xs" onClick={() => setActiveStep(1)}>
                        Prosseguir assim mesmo
                      </Button>
                    </Group>
                  </Alert>
                )}

                {duplicateContracts.length > 0 && (
                  <Alert icon={<IconAlertCircle size={16} />} title="Contratos Duplicados Encontrados" color="orange" mt="md" data-testid="duplicate-warning">
                    <Text size="sm" mb="sm">
                      Os seguintes números de contrato aparecem mais de uma vez no arquivo:
                    </Text>
                    <List size="xs" mb="sm">
                      {duplicateContracts.slice(0, 20).map((c) => (
                        <List.Item key={c}><strong>{c}</strong></List.Item>
                      ))}
                      {duplicateContracts.length > 20 && (
                        <List.Item>... e mais {duplicateContracts.length - 20} contrato(s) duplicado(s).</List.Item>
                      )}
                    </List>
                    <Group gap="xs" align="center" mt="xs">
                      <input
                        type="checkbox"
                        id="wiz-allow-duplicates"
                        checked={allowDuplicates}
                        onChange={(e) => setAllowDuplicates(e.target.checked)}
                      />
                      <label htmlFor="wiz-allow-duplicates" style={{ fontSize: 13, color: '#92400e', cursor: 'pointer', fontWeight: 500 }}>
                        Permitir duplicatas e prosseguir para o próximo passo
                      </label>
                    </Group>
                  </Alert>
                )}

                {desistenteContracts.length > 0 && (
                  <Alert icon={<IconAlertCircle size={16} />} title="Contratos Desistentes Encontrados" color="orange" mt="md" data-testid="desistente-warning">
                    <Text size="sm" mb="sm">
                      We've detected some contract with status "desistente", we won't import it
                    </Text>
                    <List size="xs" mb="sm">
                      {desistenteContracts.slice(0, 20).map((c) => (
                        <List.Item key={c}><strong>{c}</strong></List.Item>
                      ))}
                      {desistenteContracts.length > 20 && (
                        <List.Item>... e mais {desistenteContracts.length - 20} contrato(s) desistente(s).</List.Item>
                      )}
                    </List>
                    <Group gap="xs" align="center" mt="xs">
                      <input
                        type="checkbox"
                        id="wiz-allow-desistentes"
                        checked={allowDesistentes}
                        onChange={(e) => setAllowDesistentes(e.target.checked)}
                      />
                      <label htmlFor="wiz-allow-desistentes" style={{ fontSize: 13, color: '#92400e', cursor: 'pointer', fontWeight: 500 }}>
                        Confirmar ciência e prosseguir
                      </label>
                    </Group>
                  </Alert>
                )}

                {((uploadData?.conflictingUserNames && uploadData.conflictingUserNames.length > 0) || 
                  (uploadData?.conflictingMatriculas && uploadData.conflictingMatriculas.length > 0)) && (
                  <Alert icon={<IconAlertCircle size={16} />} title="Inconsistências no Cadastro de Vendedores Detectadas" color="orange" mt="md" data-testid="user-inconsistency-warning">
                    <Text size="sm" mb="sm">
                      Detectamos as seguintes inconsistências no cadastro de vendedores ativos que correspondem a dados deste arquivo:
                    </Text>
                    <List size="xs" mb="sm">
                      {uploadData.conflictingUserNames?.map((msg: string, idx: number) => (
                        <List.Item key={`name-conf-${idx}`}>{msg}</List.Item>
                      ))}
                      {uploadData.conflictingMatriculas?.map((msg: string, idx: number) => (
                        <List.Item key={`mat-conf-${idx}`}>{msg}</List.Item>
                      ))}
                    </List>

                  </Alert>
                )}

                {(() => {
                  return (
                    <>
                      {(duplicateContracts.length > 0 || desistenteContracts.length > 0) &&
                       (duplicateContracts.length === 0 || allowDuplicates) &&
                       (desistenteContracts.length === 0 || allowDesistentes) && (
                        <Group justify="flex-end" mt="md">
                          <Button
                            color="orange"
                            rightSection={<IconChevronRight size={16} />}
                            onClick={() => {
                              setActiveStep(1);
                              toast.success('Prosseguindo para o próximo passo');
                            }}
                            id="btn-advance-with-warnings"
                          >
                            Avançar para Passo 2
                          </Button>
                        </Group>
                      )}

                      {!mismatchWarning && duplicateContracts.length === 0 && desistenteContracts.length === 0 && (
                        <Group justify="flex-end" mt="md">
                          <Button 
                            onClick={() => handleStep1Upload()} 
                            loading={loading}
                            disabled={!contractFile}
                            rightSection={<IconChevronRight size={16} />}
                          >
                            Próximo Passo
                          </Button>
                        </Group>
                      )}
                    </>
                  );
                })()}
              </Stack>
            </Paper>
          </Stepper.Step>

          <Stepper.Step 
            label="Preenchimento de Usuários" 
            description="Definir e-mails e hierarquias"
            icon={<IconDownload size={18} />}
          >
            <Paper withBorder p="md" mt="md">
              <Stack gap="md">
                <Alert icon={<IconAlertCircle size={16} />} title="Instruções" color="blue">
                  O sistema identificou os vendedores no arquivo. Baixe a planilha abaixo e preencha 
                  obrigatoriamente as colunas <strong>Email</strong>, <strong>ParentEmail</strong> (e-mail do superior) 
                  e <strong>Owner_Matricula</strong> (1 para sim, 0 para não).
                </Alert>

                <Group>
                  <Button variant="outline" leftSection={<IconDownload size={16} />} onClick={handleDownloadTemplate}>
                    Baixar users.xlsx para Preencher
                  </Button>
                </Group>

                <Text size="sm" mt="md" fw={600}>
                  Após preencher e salvar o arquivo, carregue-o abaixo:
                </Text>

                <FileInput
                  label="Upload do arquivo users.xlsx preenchido"
                  placeholder="Selecione o arquivo users.xlsx atualizado"
                  required
                  value={usersFile}
                  onChange={(file) => {
                    setUsersFile(file);
                    setStep2Error(null);
                    setDuplicateEmailConflicts([]);
                  }}
                  accept=".csv,.xlsx"
                  fileInputProps={{ id: 'wizard-step2-input' }}
                  leftSection={<IconUpload size={16} />}
                />

                {duplicateEmailConflicts.length > 0 && (
                  <Alert icon={<IconAlertCircle size={16} />} title="E-mails Duplicados com Nomes Diferentes" color="red" mt="md" data-testid="duplicate-email-error">
                    <Text size="sm" mb="sm">
                      Não é permitido o mesmo e-mail para usuários com nomes diferentes. Por favor, corrija as seguintes linhas no arquivo de usuários:
                    </Text>
                    <List size="xs" mb="sm">
                      {duplicateEmailConflicts.slice(0, 20).map((c, idx) => (
                        <List.Item key={idx}><strong>{c}</strong></List.Item>
                      ))}
                      {duplicateEmailConflicts.length > 20 && (
                        <List.Item>... e mais {duplicateEmailConflicts.length - 20} e-mail(s) duplicado(s).</List.Item>
                      )}
                    </List>
                  </Alert>
                )}

                {step2Error && (
                  <Alert icon={<IconAlertCircle size={16} />} title="Falha na Importação de Usuários" color="red" mt="md" data-testid="step2-error">
                    <Text size="sm">{step2Error}</Text>
                  </Alert>
                )}

                <Group justify="space-between" mt="md">
                  <Button variant="default" onClick={prevStep} leftSection={<IconChevronLeft size={16} />}>
                    Voltar
                  </Button>
                  <Button 
                    onClick={handleStep2Import} 
                    loading={loading}
                    disabled={!usersFile}
                    color="green"
                    rightSection={<IconChevronRight size={16} />}
                  >
                    Importar Usuários e Avançar
                  </Button>
                </Group>
              </Stack>
            </Paper>
          </Stepper.Step>

          <Stepper.Step 
            label="Importação de Contratos" 
            description="Baixar, conferir e importar"
            icon={<IconDownload size={18} />}
          >
            <Paper withBorder p="md" mt="md" style={{ backgroundColor: '#f0f9ff' }}>
              <Stack gap="md">
                <Alert 
                  icon={importResult?.processedRows > 0 ? <IconCheck size={16} /> : <IconAlertCircle size={16} />} 
                  title={importResult?.processedRows > 0 ? "Usuários Importados!" : "Nenhum usuário importado"} 
                  color={importResult?.processedRows > 0 ? "green" : "orange"}
                >
                  <span>
                    {importResult?.processedRows > 0 
                      ? `Os ${importResult?.processedRows} vendedores e suas matrículas foram importados com sucesso. Agora você pode importar os contratos abaixo.`
                      : "Não foi possível importar nenhum vendedor. Verifique se as colunas Nome, Email e Matricula estão preenchidas no arquivo users.xlsx."}
                  </span>
                </Alert>
                
                <Text size="sm">
                  O sistema preparou uma versão enriquecida do arquivo <strong>modelo_referencia_retencao</strong> com
                  os e-mails resolvidos. Você pode baixá-lo para conferência ou importar diretamente clicando em
                  <strong> "Importar Contratos"</strong>.
                </Text>

                {/* ── Import options ─────────────────────────────────────── */}
                {!contractImportResult && (
                  <Paper withBorder p="sm" radius="sm" style={{ background: '#f9fafb' }}>
                    <Text size="sm" fw={600} mb="xs">Opções de Importação</Text>
                    <Stack gap="xs">
                      <Group gap="xs">
                        <input
                          type="checkbox"
                          id="wiz-skip-missing"
                          checked={skipMissingContractNumber}
                          onChange={e => setSkipMissingContractNumber(e.target.checked)}
                        />
                        <label htmlFor="wiz-skip-missing" style={{ fontSize: 13, color: '#4b5563', cursor: 'pointer' }}>
                          Pular linhas sem número de contrato
                        </label>
                      </Group>
                      <Group gap="xs">
                        <input
                          type="checkbox"
                          id="wiz-auto-groups"
                          checked={allowAutoCreateGroups}
                          onChange={e => setAllowAutoCreateGroups(e.target.checked)}
                        />
                        <label htmlFor="wiz-auto-groups" style={{ fontSize: 13, color: '#4b5563', cursor: 'pointer' }}>
                          Permitir criação automática de grupos
                        </label>
                      </Group>
                      <Group gap="xs">
                        <input
                          type="checkbox"
                          id="wiz-auto-pvs"
                          checked={allowAutoCreatePVs}
                          onChange={e => setAllowAutoCreatePVs(e.target.checked)}
                        />
                        <label htmlFor="wiz-auto-pvs" style={{ fontSize: 13, color: '#4b5563', cursor: 'pointer' }}>
                          Permitir criação automática de PV
                        </label>
                      </Group>
                    </Stack>
                  </Paper>
                )}

                {/* ── Action buttons ─────────────────────────────────────── */}
                {!contractImportResult && (
                  <Group justify="center" gap="md" py="md">
                    <Button
                      variant="outline"
                      leftSection={<IconDownload size={18} />}
                      onClick={handleDownloadContracts}
                      loading={loading}
                    >
                      Baixar contracts.xlsx
                    </Button>
                    <Button
                      size="lg"
                      color="green"
                      leftSection={<IconCheck size={20} />}
                      onClick={handleImportContracts}
                      loading={contractImportLoading}
                    >
                      Importar Contratos
                    </Button>
                  </Group>
                )}

                {/* ── Import result summary ──────────────────────────────── */}
                {contractImportResult && (
                  <Stack gap="sm">
                    <Alert
                      icon={contractImportResult.failedRows > 0 ? <IconAlertCircle size={16} /> : <IconCheck size={16} />}
                      title={contractImportResult.failedRows > 0 ? 'Importação com erros' : 'Contratos importados!'}
                      color={contractImportResult.failedRows > 0 ? 'orange' : 'green'}
                    >
                      <span>
                        {contractImportResult.failedRows > 0
                          ? `${contractImportResult.processedRows} contratos criados, ${contractImportResult.failedRows} com erro.`
                          : `${contractImportResult.processedRows} contratos criados com sucesso.`}
                      </span>
                    </Alert>

                    <Group grow>
                      <Paper withBorder p="sm" style={{ textAlign: 'center' }}>
                        <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Contratos</Text>
                        <Text size="xl" fw={700}>{contractImportResult.processedRows ?? 0}</Text>
                      </Paper>
                      <Paper withBorder p="sm" style={{ textAlign: 'center' }}>
                        <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Erros</Text>
                        <Text size="xl" fw={700} c={contractImportResult.failedRows > 0 ? 'red' : 'gray'}>
                          {contractImportResult.failedRows ?? 0}
                        </Text>
                      </Paper>
                      {contractImportResult.createdGroups?.length > 0 && (
                        <Paper withBorder p="sm" style={{ textAlign: 'center' }}>
                          <Text size="xs" c="dimmed" tt="uppercase" fw={700}>Grupos Criados</Text>
                          <Text size="xl" fw={700}>{contractImportResult.createdGroups.length}</Text>
                        </Paper>
                      )}
                      {contractImportResult.createdPVs?.length > 0 && (
                        <Paper withBorder p="sm" style={{ textAlign: 'center' }}>
                          <Text size="xs" c="dimmed" tt="uppercase" fw={700}>PVs Criados</Text>
                          <Text size="xl" fw={700}>{contractImportResult.createdPVs.length}</Text>
                        </Paper>
                      )}
                    </Group>

                    {contractImportResult.createdGroups?.length > 0 && (
                      <Stack gap="xs">
                        <Text fw={600} size="sm">Grupos criados automaticamente:</Text>
                        <Group gap="xs">
                          {contractImportResult.createdGroups.map((g: string) => (
                            <Badge key={g} variant="outline" color="blue">{g}</Badge>
                          ))}
                        </Group>
                      </Stack>
                    )}

                    {contractImportResult.createdPVs?.length > 0 && (
                      <Stack gap="xs">
                        <Text fw={600} size="sm">PVs criados automaticamente:</Text>
                        <Group gap="xs">
                          {contractImportResult.createdPVs.map((pv: string) => (
                            <Badge key={pv} variant="outline" color="orange">{pv}</Badge>
                          ))}
                        </Group>
                      </Stack>
                    )}

                    {contractImportResult.desistenteContractNumbers?.length > 0 && (
                      <Alert icon={<IconAlertCircle size={16} />} title="Contratos com status 'DESISTENTE' detectados" color="yellow" mt="md" data-testid="desistente-skipped-warning">
                        <Text size="sm" mb="xs">
                          Detectamos {contractImportResult.desistenteContractNumbers.length} contrato(s) com status "DESISTENTE". Eles não foram importados conforme a regra de negócio:
                        </Text>
                        <List size="xs">
                          {contractImportResult.desistenteContractNumbers.slice(0, 10).map((num: string) => (
                            <List.Item key={num}><strong>{num}</strong></List.Item>
                          ))}
                          {contractImportResult.desistenteContractNumbers.length > 10 && (
                            <List.Item>... e mais {contractImportResult.desistenteContractNumbers.length - 10} contrato(s).</List.Item>
                          )}
                        </List>
                      </Alert>
                    )}

                    {contractImportResult.errors?.length > 0 && (
                      <Alert icon={<IconAlertCircle size={16} />} title="Erros encontrados" color="red">
                        <List size="xs">
                          {contractImportResult.errors.slice(0, 10).map((err: string, i: number) => (
                            <List.Item key={i}>{err}</List.Item>
                          ))}
                          {contractImportResult.errors.length > 10 && (
                            <List.Item>... e mais {contractImportResult.errors.length - 10} erros.</List.Item>
                          )}
                        </List>
                      </Alert>
                    )}

                    {contractImportResult.warnings?.length > 0 && (
                      <Alert icon={<IconAlertCircle size={16} />} title="Avisos Importantes" color="yellow" mt="md" data-testid="import-warnings">
                        <Text size="sm" mb="xs">
                          Identificamos os seguintes avisos durante a importação:
                        </Text>
                        <List size="xs">
                          {contractImportResult.warnings.slice(0, 10).map((warn: string, i: number) => (
                            <List.Item key={i}>{warn}</List.Item>
                          ))}
                          {contractImportResult.warnings.length > 10 && (
                            <List.Item>... e mais {contractImportResult.warnings.length - 10} avisos.</List.Item>
                          )}
                        </List>
                      </Alert>
                    )}

                    {contractImportResult.userContractCountDeltas?.length > 0 && (
                      <Stack gap="xs" mt="md">
                        <Text fw={600} size="sm">Resumo de Atribuição por Usuário:</Text>
                        <div style={{ overflowX: 'auto', border: '1px solid #e2e8f0', borderRadius: '6px' }}>
                          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
                            <thead>
                              <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
                                <th style={{ padding: '8px 12px', fontWeight: 600 }}>Usuário</th>
                                <th style={{ padding: '8px 12px', fontWeight: 600 }}>E-mail</th>
                                <th style={{ padding: '8px 12px', fontWeight: 600, textAlign: 'right' }}>Antes</th>
                                <th style={{ padding: '8px 12px', fontWeight: 600, textAlign: 'right' }}>Depois</th>
                                <th style={{ padding: '8px 12px', fontWeight: 600, textAlign: 'right' }}>Δ</th>
                              </tr>
                            </thead>
                            <tbody>
                              {contractImportResult.userContractCountDeltas.map((delta: any, idx: number) => (
                                <tr key={idx} style={{ borderBottom: '1px solid #f1f5f9' }}>
                                  <td style={{ padding: '8px 12px' }}>{delta.userName}</td>
                                  <td style={{ padding: '8px 12px', color: '#64748b' }}>{delta.email}</td>
                                  <td style={{ padding: '8px 12px', textAlign: 'right' }}>{delta.before}</td>
                                  <td style={{ padding: '8px 12px', textAlign: 'right' }}>{delta.after}</td>
                                  <td style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 600, color: delta.delta > 0 ? '#16a34a' : '#475569' }}>
                                    {delta.delta > 0 ? `+${delta.delta}` : delta.delta}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      </Stack>
                    )}

                    <Group justify="center" mt="sm">
                      <Button variant="filled" onClick={() => window.location.hash = '#/contracts'}>
                        Ir para Lista de Contratos
                      </Button>
                    </Group>
                  </Stack>
                )}

                <Group justify="space-between" mt="md">
                  <Button variant="default" onClick={prevStep} leftSection={<IconChevronLeft size={16} />}
                    disabled={contractImportLoading}>
                    Voltar
                  </Button>
                  {!contractImportResult && (
                    <Text size="xs" c="dimmed">O arquivo será salvo no servidor para auditoria.</Text>
                  )}
                </Group>
              </Stack>
            </Paper>
          </Stepper.Step>

          <Stepper.Completed>
            <Paper withBorder p="xl" mt="md" style={{ backgroundColor: '#f8fafc' }}>
              <Stack align="center" gap="sm">
                <IconCheck size={48} color="green" />
                <Title order={3}>Importação Concluída!</Title>
                <Text color="dimmed" mb="lg">
                  O processo de importação foi finalizado com sucesso.
                </Text>
              </Stack>

              <Group grow mb="xl">
                <Paper withBorder p="md" style={{ textAlign: 'center' }}>
                  <Text size="xs" color="dimmed" tt="uppercase" fw={700}>Linhas Processadas</Text>
                  <Text size="xl" fw={700}>{importResult?.processedRows || 0}</Text>
                </Paper>
                <Paper withBorder p="md" style={{ textAlign: 'center' }}>
                  <Text size="xs" color="dimmed" tt="uppercase" fw={700}>Falhas Técnicas</Text>
                  <Text size="xl" fw={700} color={importResult?.failedRows > 0 ? 'red' : 'gray'}>
                    {importResult?.failedRows || 0}
                  </Text>
                </Paper>
              </Group>

              {importResult?.createdPVs?.length > 0 && (
                <Stack gap="xs" mb="md">
                  <Text fw={600} size="sm">Pontos de Venda detectados e criados:</Text>
                  <Group gap="xs">
                    {importResult.createdPVs.map((pv: string) => (
                      <Badge key={pv} variant="outline" color="blue">{pv}</Badge>
                    ))}
                  </Group>
                </Stack>
              )}

              {importResult?.createdGroups?.length > 0 && (
                <Stack gap="xs" mb="md">
                  <Text fw={600} size="sm">Novos Grupos criados:</Text>
                  <Group gap="xs">
                    {importResult.createdGroups.map((group: string) => (
                      <Badge key={group} variant="outline" color="orange">{group}</Badge>
                    ))}
                  </Group>
                </Stack>
              )}

              {importResult?.errors?.length > 0 && (
                <Alert icon={<IconAlertCircle size={16} />} title="Algumas linhas falharam" color="red" mt="xl">
                  <Text size="xs" mb="xs">Os seguintes erros foram encontrados durante a importação:</Text>
                  <List size="xs">
                    {importResult.errors.slice(0, 5).map((err: string, i: number) => (
                      <List.Item key={i}>{err}</List.Item>
                    ))}
                    {importResult.errors.length > 5 && <List.Item>... e mais {importResult.errors.length - 5} erros.</List.Item>}
                  </List>
                </Alert>
              )}

              <Group justify="center" mt="xl">
                <Button variant="filled" onClick={() => window.location.hash = '#/contracts'}>
                  Ir para Lista de Contratos
                </Button>
                 <Button variant="outline" onClick={() => {
                  setActiveStep(0);
                  setContractFile(null);
                  setUsersFile(null);
                  setUploadData(null);
                  setImportResult(null);
                  setDuplicateContracts([]);
                  setAllowDuplicates(false);
                  setDesistenteContracts([]);
                  setAllowDesistentes(false);
                  setAllowInconsistencies(false);
                }}>
                  Nova Importação Completa
                </Button>
              </Group>
            </Paper>
          </Stepper.Completed>
        </Stepper>
      </Box>
    </Menu>
  );
};

export default ImportWizardPage;
