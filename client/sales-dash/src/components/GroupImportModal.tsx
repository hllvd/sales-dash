import React, { useState } from 'react';
import {
  Modal,
  Title,
  Text,
  Button,
  Group,
  Stack,
  Alert,
  List,
  FileInput,
  Badge,
} from '@mantine/core';
import {
  IconUpload,
  IconDownload,
  IconCheck,
  IconAlertCircle,
  IconFileSpreadsheet,
} from '@tabler/icons-react';
import {
  importGroups,
  downloadGroupImportTemplate,
  GroupImportResult,
} from '../services/contractService';

interface GroupImportModalProps {
  opened: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const GroupImportModal: React.FC<GroupImportModalProps> = ({
  opened,
  onClose,
  onSuccess,
}) => {
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [downloadingTemplate, setDownloadingTemplate] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<GroupImportResult | null>(null);

  const resetState = () => {
    setFile(null);
    setUploading(false);
    setError(null);
    setResult(null);
  };

  const handleClose = () => {
    resetState();
    onClose();
  };

  const handleDownloadTemplate = async () => {
    setDownloadingTemplate(true);
    try {
      const blob = await downloadGroupImportTemplate();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', 'modelo_importacao_grupos.xlsx');
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch (err: any) {
      setError(err.message || 'Erro ao baixar modelo de importação');
    } finally {
      setDownloadingTemplate(false);
    }
  };

  const handleImport = async () => {
    if (!file) {
      setError('Por favor, selecione um arquivo .xlsx para importar.');
      return;
    }

    setUploading(true);
    setError(null);
    setResult(null);

    try {
      const res = await importGroups(file);
      setResult(res);
      onSuccess();
    } catch (err: any) {
      setError(err.message || 'Erro ao processar importação de grupos');
    } finally {
      setUploading(false);
    }
  };

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title={
        <Title order={3} component="span" style={{ color: '#1c1c1e', fontWeight: 700 }}>
          Importar Grupos via Excel (.xlsx)
        </Title>
      }
      size="lg"
      centered
    >
      <Stack gap="md">
        <Text size="sm" c="dimmed">
          Faça upload de uma planilha contendo as colunas <strong>Grupo</strong> e{' '}
          <strong>Segmento</strong>. O sistema realizará o <em>upsert</em>: grupos existentes terão o
          segmento atualizado e novos grupos serão cadastrados.
        </Text>

        <Group justify="space-between" align="center">
          <Text size="xs" c="dimmed">
            Segmentos aceitos: <strong>Lar</strong>, <strong>Motores</strong>, <strong>Serviços</strong> ou em branco (vazio).
          </Text>
          <Button
            variant="light"
            size="xs"
            leftSection={<IconDownload size={14} />}
            onClick={handleDownloadTemplate}
            loading={downloadingTemplate}
          >
            Baixar Modelo (.xlsx)
          </Button>
        </Group>

        {error && (
          <Alert
            icon={<IconAlertCircle size={16} />}
            title="Erro"
            color="red"
            withCloseButton
            onClose={() => setError(null)}
          >
            {error}
          </Alert>
        )}

        {result && (
          <Alert
            icon={<IconCheck size={16} />}
            title="Importação Concluída"
            color={result.errorCount > 0 ? 'orange' : 'green'}
          >
            <Stack gap="xs">
              <Group gap="xs">
                <Badge color="green" variant="light">
                  {result.createdCount} criados
                </Badge>
                <Badge color="blue" variant="light">
                  {result.updatedCount} atualizados
                </Badge>
                {result.errorCount > 0 && (
                  <Badge color="red" variant="light">
                    {result.errorCount} erros
                  </Badge>
                )}
              </Group>

              {result.errors.length > 0 && (
                <div>
                  <Text size="xs" fw={600} mt="xs" mb={4}>
                    Inconsistências encontradas:
                  </Text>
                  <List size="xs" withPadding spacing={2}>
                    {result.errors.map((err, i) => (
                      <List.Item key={i}>
                        Linha {err.rowNumber} {err.groupName ? `(Grupo "${err.groupName}")` : ''}:{' '}
                        {err.error}
                      </List.Item>
                    ))}
                  </List>
                </div>
              )}
            </Stack>
          </Alert>
        )}

        <FileInput
          label="Selecionar Arquivo Excel"
          placeholder="Escolha o arquivo .xlsx"
          accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
          leftSection={<IconFileSpreadsheet size={18} />}
          value={file}
          onChange={(newFile) => {
            setFile(newFile);
            setError(null);
            setResult(null);
          }}
          clearable
          disabled={uploading}
        />

        <Group justify="flex-end" mt="md">
          <Button variant="default" onClick={handleClose} disabled={uploading}>
            {result ? 'Fechar' : 'Cancelar'}
          </Button>
          <Button
            leftSection={<IconUpload size={16} />}
            onClick={handleImport}
            loading={uploading}
            disabled={!file}
          >
            Importar
          </Button>
        </Group>
      </Stack>
    </Modal>
  );
};

export default GroupImportModal;
