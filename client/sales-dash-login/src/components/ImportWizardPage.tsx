import React, { useState } from 'react';
import { Title, Button, Stepper, Group, FileInput, Text, Paper, Badge, Alert, Stack, List } from '@mantine/core';
import { IconUpload, IconDownload, IconCheck, IconAlertCircle, IconChevronRight, IconChevronLeft } from '@tabler/icons-react';
import Menu from './Menu';
import { apiService } from '../services/apiService';
import { toast } from '../utils/toast';

const ImportWizardPage: React.FC = () => {
  const [activeStep, setActiveStep] = useState(0);
  const [loading, setLoading] = useState(false);
  
  // Step 1: Upload Contract File
  const [contractFile, setContractFile] = useState<File | null>(null);
  const [uploadData, setUploadData] = useState<any>(null);

  // Step 2: Users File
  const [usersFile, setUsersFile] = useState<File | null>(null);
  const [importResult, setImportResult] = useState<any>(null);

  const handleStep1Upload = async () => {
    if (!contractFile) {
      toast.error('Por favor, selecione o arquivo de contratos');
      return;
    }

    setLoading(true);
    try {
      const response = await apiService.uploadWizardStep1(contractFile);
      if (response.success) {
        setUploadData(response.data);
        setActiveStep(1);
        toast.success('Arquivo processado com sucesso');
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
      a.download = 'users.csv';
      document.body.appendChild(a);
      a.click();
      window.URL.revokeObjectURL(url);
      document.body.removeChild(a);
      toast.success('Modelo users.csv baixado');
    } catch (err: any) {
      toast.error('Falha ao baixar modelo');
    }
  };

  const handleStep2Import = async () => {
    if (!usersFile) {
      toast.error('Por favor, selecione o arquivo de usuários preenchido');
      return;
    }

    setLoading(true);
    try {
      const response = await apiService.runWizardStep2(uploadData.uploadId, usersFile);
      if (response.success) {
        setImportResult(response.data);
        setActiveStep(2);
        toast.success('Usuários e matrículas importados com sucesso');
      }
    } catch (err: any) {
      toast.error(err.message || 'Falha na importação de usuários');
    } finally {
      setLoading(false);
    }
  };

  const handleDownloadContracts = async () => {
    if (!uploadData?.uploadId) return;

    setLoading(true);
    try {
      await apiService.downloadWizardContracts(uploadData.uploadId);
      toast.success('Arquivo contracts.csv baixado com sucesso');
    } catch (err: any) {
      toast.error('Falha ao baixar contratos enriquecidos');
    } finally {
      setLoading(false);
    }
  };

  const prevStep = () => setActiveStep((current) => (current > 0 ? current - 1 : current));

  return (
    <Menu>
      <div style={{ maxWidth: '1000px', margin: '0 auto', padding: '20px' }}>
        <Title order={2} mb="xl" className="page-title-break">Assistente de Importação Completa</Title>

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
                  onChange={setContractFile}
                  accept=".csv,.xlsx"
                  leftSection={<IconUpload size={16} />}
                />

                <Group justify="flex-end" mt="md">
                  <Button 
                    onClick={handleStep1Upload} 
                    loading={loading}
                    disabled={!contractFile}
                    rightSection={<IconChevronRight size={16} />}
                  >
                    Próximo Passo
                  </Button>
                </Group>
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
                    Baixar users.csv para Preencher
                  </Button>
                </Group>

                <Text size="sm" mt="md" fw={600}>
                  Após preencher e salvar o arquivo, carregue-o abaixo:
                </Text>

                <FileInput
                  label="Upload do arquivo users.csv preenchido"
                  placeholder="Selecione o arquivo users.csv atualizado"
                  required
                  value={usersFile}
                  onChange={setUsersFile}
                  accept=".csv"
                  leftSection={<IconUpload size={16} />}
                />

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
            label="Download de Contratos" 
            description="Baixar arquivo final enriquecido"
            icon={<IconDownload size={18} />}
          >
            <Paper withBorder p="md" mt="md" style={{ backgroundColor: '#f0f9ff' }}>
              <Stack gap="md">
                <Alert icon={<IconCheck size={16} />} title="Usuários Importados!" color="green">
                  Os {importResult?.processedRows || 0} vendedores e suas matrículas foram importados com sucesso. 
                  Agora você pode baixar o arquivo de contratos final.
                </Alert>
                
                <Text size="sm">
                  O sistema processou os usuários e agora preparou uma versão otimizada do arquivo <strong>modelo_referencia_retencao</strong>.
                  Este arquivo contém os e-mails resolvidos e está pronto para ser carregado na tela de Mapeamento.
                </Text>

                <Group justify="center" py="xl">
                  <Button 
                    size="lg"
                    variant="filled"
                    leftSection={<IconDownload size={20} />} 
                    onClick={handleDownloadContracts}
                    loading={loading}
                  >
                    Baixar contracts.csv Enriquecido
                  </Button>
                </Group>

                <Group justify="space-between" mt="md">
                  <Button variant="default" onClick={prevStep} leftSection={<IconChevronLeft size={16} />}>
                    Voltar
                  </Button>
                  <Button 
                    variant="outline"
                    onClick={() => window.location.hash = '#/contracts'}
                    rightSection={<IconChevronRight size={16} />}
                  >
                    Ir para Mapeamento
                  </Button>
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
                }}>
                  Nova Importação Completa
                </Button>
              </Group>
            </Paper>
          </Stepper.Completed>
        </Stepper>
      </div>
    </Menu>
  );
};

export default ImportWizardPage;
