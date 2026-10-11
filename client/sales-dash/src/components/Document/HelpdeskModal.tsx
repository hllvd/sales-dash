import React, { useState, useEffect } from 'react';
import {
  Modal,
  TextInput,
  Textarea,
  Button,
  Group,
  Stack,
  Text,
  Badge,
  Card,
  Divider,
  Select,
  Loader,
  Center,
  ScrollArea,
  ActionIcon,
  FileButton,
  Image
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import {
  IconSend,
  IconEdit,
  IconCheck,
  IconHistory,
  IconPhoto,
  IconTrash
} from '@tabler/icons-react';
import { helpdeskApiService, HelpdeskQuestionDto } from '../../services/apiService';

interface HelpdeskModalProps {
  opened: boolean;
  onClose: () => void;
  questionId?: string | null;
  onSaved?: () => void;
}

export const HelpdeskModal: React.FC<HelpdeskModalProps> = ({
  opened,
  onClose,
  questionId,
  onSaved
}) => {
  const [currentUser, setCurrentUser] = useState<any>(null);
  const [loading, setLoading] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [question, setQuestion] = useState<HelpdeskQuestionDto | null>(null);

  // New question form state
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [uploadingImage, setUploadingImage] = useState(false);

  // Reply message form state
  const [replyContent, setReplyContent] = useState('');
  const [replyImageUrl, setReplyImageUrl] = useState<string | null>(null);
  const [uploadingReplyImage, setUploadingReplyImage] = useState(false);

  // Edit original content state
  const [isEditingOriginal, setIsEditingOriginal] = useState(false);
  const [editedContent, setEditedContent] = useState('');
  const [showHistory, setShowHistory] = useState(false);

  // Confirmation modal for AI Knowledge Base summary generation
  const [confirmResolveModalOpen, setConfirmResolveModalOpen] = useState(false);
  const [targetResolveStatus, setTargetResolveStatus] = useState<string>('Resolvido');

  useEffect(() => {
    const userJson = localStorage.getItem('user');
    if (userJson) {
      try {
        setCurrentUser(JSON.parse(userJson));
      } catch (e) {
        console.error(e);
      }
    }
  }, []);

  useEffect(() => {
    if (opened) {
      if (questionId) {
        loadQuestion(questionId);
      } else {
        setQuestion(null);
        setTitle('');
        setContent('');
        setImageUrl(null);
        setReplyContent('');
        setReplyImageUrl(null);
        setIsEditingOriginal(false);
      }
    }
  }, [opened, questionId]);

  const handleImageUpload = async (file: File | null) => {
    if (!file) return;

    // Validate type strictly to image (no videos)
    if (!file.type.startsWith('image/')) {
      notifications.show({
        title: 'Formato inválido',
        message: 'Apenas arquivos de imagem são permitidos. Vídeos não são aceitos.',
        color: 'red'
      });
      return;
    }

    if (file.size > 10 * 1024 * 1024) {
      notifications.show({
        title: 'Arquivo muito grande',
        message: 'A imagem deve ter no máximo 10MB.',
        color: 'red'
      });
      return;
    }

    setUploadingImage(true);
    try {
      const res = await helpdeskApiService.uploadImage(file);
      if (res.success && res.data?.imageUrl) {
        setImageUrl(res.data.imageUrl);
        notifications.show({
          title: 'Imagem anexada',
          message: 'Imagem carregada com sucesso!',
          color: 'green'
        });
      } else {
        notifications.show({
          title: 'Erro',
          message: res.message || 'Falha ao carregar imagem.',
          color: 'red'
        });
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao fazer upload da imagem.',
        color: 'red'
      });
    } finally {
      setUploadingImage(false);
    }
  };

  const loadQuestion = async (id: string) => {
    setLoading(true);
    try {
      const res = await helpdeskApiService.getQuestionById(id);
      if (res.success && res.data) {
        setQuestion(res.data);
        setEditedContent(res.data.currentContent);
      } else {
        notifications.show({
          title: 'Erro',
          message: res.message || 'Falha ao carregar pergunta.',
          color: 'red'
        });
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao carregar pergunta.',
        color: 'red'
      });
    } finally {
      setLoading(false);
    }
  };

  const handleCreateQuestion = async () => {
    if (!title.trim()) {
      notifications.show({ title: 'Atenção', message: 'Preencha o título da dúvida.', color: 'yellow' });
      return;
    }
    if (!content.trim()) {
      notifications.show({ title: 'Atenção', message: 'Preencha a descrição da dúvida.', color: 'yellow' });
      return;
    }

    setSubmitting(true);
    try {
      const res = await helpdeskApiService.createQuestion({
        title: title.trim(),
        content: content.trim(),
        sourcePageUrl: window.location.hash || window.location.pathname,
        imageUrl: imageUrl || undefined
      });

      if (res.success) {
        notifications.show({
          title: 'Pergunta Enviada',
          message: 'Sua dúvida foi registrada com sucesso!',
          color: 'green'
        });
        setImageUrl(null);
        onSaved?.();
        onClose();
      } else {
        notifications.show({
          title: 'Erro',
          message: res.message || 'Falha ao enviar pergunta.',
          color: 'red'
        });
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao enviar pergunta.',
        color: 'red'
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleSendReply = async () => {
    if (!question || !replyContent.trim()) return;

    setSubmitting(true);
    try {
      const res = await helpdeskApiService.addMessage(question.questionId, replyContent.trim());
      if (res.success && res.data) {
        setQuestion(res.data);
        setReplyContent('');
        notifications.show({
          title: 'Resposta enviada',
          message: 'Sua mensagem foi adicionada à conversa.',
          color: 'green'
        });
        onSaved?.();
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao enviar resposta.',
        color: 'red'
      });
    } finally {
      setSubmitting(false);
    }
  };

  const handleSaveEditedContent = async () => {
    if (!question || !editedContent.trim()) return;

    setSubmitting(true);
    try {
      const res = await helpdeskApiService.updateQuestion(question.questionId, editedContent.trim());
      if (res.success && res.data) {
        setQuestion(res.data);
        setIsEditingOriginal(false);
        notifications.show({
          title: 'Pergunta atualizada',
          message: 'A versão anterior foi arquivada no histórico.',
          color: 'green'
        });
        onSaved?.();
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao atualizar conteúdo.',
        color: 'red'
      });
    } finally {
      setSubmitting(false);
    }
  };

  const requestChangeStatus = (newStatus: string) => {
    if (!question || !newStatus) return;
    if (newStatus === 'Resolvido') {
      setTargetResolveStatus('Resolvido');
      setConfirmResolveModalOpen(true);
    } else {
      executeChangeStatus(newStatus, false);
    }
  };

  const executeChangeStatus = async (newStatus: string, generateSummary: boolean) => {
    if (!question || !newStatus) return;

    try {
      const res = await helpdeskApiService.updateStatus(question.questionId, newStatus, generateSummary);
      if (res.success && res.data) {
        setQuestion(res.data);
        notifications.show({
          title: 'Status atualizado',
          message: generateSummary
            ? `Status alterado para "${newStatus}". A IA está gerando o resumo para a base de conhecimento em segundo plano!`
            : `Status alterado para "${newStatus}".`,
          color: 'blue'
        });
        onSaved?.();
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro ao mudar status.',
        color: 'red'
      });
    } finally {
      setConfirmResolveModalOpen(false);
    }
  };

  const getStatusBadge = (st: string) => {
    switch (st) {
      case 'Não lido':
        return <Badge color="red" variant="filled">Não lido</Badge>;
      case 'Lida':
        return <Badge color="blue" variant="filled">Lida</Badge>;
      case 'Pendente':
        return <Badge color="yellow" variant="filled">Pendente</Badge>;
      case 'Resolvido':
        return <Badge color="green" variant="filled">Resolvido</Badge>;
      default:
        return <Badge color="gray">{st}</Badge>;
    }
  };

  const isAuthor = currentUser?.id === question?.userId;
  const isSuperAdmin = currentUser?.role?.toLowerCase() === 'superadmin';

  return (
    <Modal
      opened={opened}
      onClose={onClose}
      title={
        <Text fw={700} size="lg">
          {question ? `Dúvida: ${question.title}` : 'Fazer uma Pergunta'}
        </Text>
      }
      size="lg"
      centered
    >
      {loading ? (
        <Center py="xl">
          <Loader size="md" />
        </Center>
      ) : !question ? (
        /* Create question form */
        <Stack gap="md">
          <Text size="sm" c="dimmed">
            Tem alguma dúvida sobre regras de comissão, contratos, equipes ou relatórios? Envie abaixo para ser respondido pelo SuperAdmin.
          </Text>

          <TextInput
            label="Título da Dúvida"
            placeholder="Ex: Como funciona a retenção de contratos com parcelas atrasadas?"
            required
            maxLength={150}
            value={title}
            onChange={(e) => setTitle(e.target.value)}
          />

          <Textarea
            label="Descrição detalhada"
            placeholder="Explique o cenário ou a situação que gerou a dúvida..."
            required
            minRows={4}
            maxRows={8}
            maxLength={2000}
            value={content}
            onChange={(e) => setContent(e.target.value)}
          />

          {/* Image Upload for New Question */}
          <div>
            <Group justify="space-between" align="center" mb={6}>
              <Text size="xs" fw={500} c="dimmed">
                Anexar Imagem (Opcional - Prints de tela, recibos, etc.)
              </Text>
              <FileButton
                onChange={handleImageUpload}
                accept="image/png,image/jpeg,image/gif,image/webp"
                disabled={uploadingImage}
              >
                {(props) => (
                  <Button
                    {...props}
                    size="xs"
                    variant="light"
                    color="blue"
                    leftSection={<IconPhoto size={14} />}
                    loading={uploadingImage}
                  >
                    {imageUrl ? 'Trocar Imagem' : 'Adicionar Imagem'}
                  </Button>
                )}
              </FileButton>
            </Group>

            {imageUrl && (
              <Card withBorder padding="xs" radius="sm" style={{ backgroundColor: '#f1f5f9', position: 'relative' }}>
                <Group justify="space-between" align="flex-start">
                  <Image
                    src={imageUrl}
                    alt="Preview da dúvida"
                    radius="sm"
                    mah={160}
                    fit="contain"
                    style={{ maxHeight: 160, width: 'auto' }}
                  />
                  <ActionIcon
                    color="red"
                    variant="subtle"
                    size="sm"
                    onClick={() => setImageUrl(null)}
                    title="Remover imagem"
                  >
                    <IconTrash size={14} />
                  </ActionIcon>
                </Group>
              </Card>
            )}
          </div>

          <Text size="xs" c="dimmed">
            Página de contexto capturada: <code>{window.location.hash || window.location.pathname}</code>
          </Text>

          <Group justify="flex-end" mt="md">
            <Button variant="default" onClick={onClose}>
              Cancelar
            </Button>
            <Button
              color="blue"
              leftSection={<IconSend size={16} />}
              loading={submitting}
              onClick={handleCreateQuestion}
            >
              Enviar Pergunta
            </Button>
          </Group>
        </Stack>
      ) : (
        /* View question thread */
        <Stack gap="md">
          <Group justify="space-between" align="center">
            <Group gap="xs">
              {getStatusBadge(question.status)}
              <Text size="xs" c="dimmed">
                Por {question.userName} ({question.userEmail}) em {new Date(question.createdAt).toLocaleDateString('pt-BR')}
              </Text>
            </Group>

            <Select
              size="xs"
              style={{ width: 140 }}
              value={question.status}
              data={['Não lido', 'Lida', 'Pendente', 'Resolvido']}
              onChange={(val) => val && requestChangeStatus(val)}
            />
          </Group>

          {question.sourcePageUrl && (
            <Text size="xs" c="dimmed">
              Origem da dúvida: <code>{question.sourcePageUrl}</code>
            </Text>
          )}

          <Divider />

          {/* Original question card */}
          <Card withBorder padding="sm" radius="md" style={{ backgroundColor: '#f8fafc' }}>
            <Group justify="space-between" mb="xs">
              <Text fw={600} size="sm">Pergunta Original</Text>
              <Group gap="xs">
                {question.editHistory && question.editHistory.length > 0 && (
                  <Button
                    size="compact-xs"
                    variant="subtle"
                    leftSection={<IconHistory size={12} />}
                    onClick={() => setShowHistory(!showHistory)}
                  >
                    Histórico ({question.editHistory.length})
                  </Button>
                )}
                {isAuthor && !isEditingOriginal && (
                  <ActionIcon size="sm" variant="subtle" onClick={() => setIsEditingOriginal(true)}>
                    <IconEdit size={14} />
                  </ActionIcon>
                )}
              </Group>
            </Group>

            {isEditingOriginal ? (
              <Stack gap="xs">
                <Textarea
                  value={editedContent}
                  onChange={(e) => setEditedContent(e.target.value)}
                  minRows={3}
                  maxLength={2000}
                />
                <Group justify="flex-end" gap="xs">
                  <Button size="xs" variant="default" onClick={() => setIsEditingOriginal(false)}>
                    Cancelar
                  </Button>
                  <Button size="xs" color="blue" loading={submitting} onClick={handleSaveEditedContent}>
                    Salvar Alteração
                  </Button>
                </Group>
              </Stack>
            ) : (
              <Stack gap="xs">
                <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
                  {question.currentContent}
                </Text>
                {question.imageUrl && (
                  <div style={{ marginTop: 8 }}>
                    <Text size="xs" fw={500} c="dimmed" mb={4}>Imagem anexada:</Text>
                    <Image
                      src={question.imageUrl}
                      alt="Imagem da dúvida"
                      radius="md"
                      mah={280}
                      fit="contain"
                      style={{ maxHeight: 280, width: 'auto', border: '1px solid #e2e8f0' }}
                    />
                  </div>
                )}
              </Stack>
            )}

            {showHistory && question.editHistory && (
              <Stack gap="xs" mt="sm" pt="xs" style={{ borderTop: '1px dashed #cbd5e1' }}>
                <Text size="xs" fw={600} c="dimmed">Versões Anteriores:</Text>
                {question.editHistory.map((h, idx) => (
                  <Card key={idx} padding="xs" radius="xs" style={{ backgroundColor: '#ffffff' }}>
                    <Text size="xs" c="dimmed">
                      Versão {h.version} em {new Date(h.editedAt).toLocaleString('pt-BR')}:
                    </Text>
                    <Text size="xs" style={{ whiteSpace: 'pre-wrap' }}>{h.previousContent}</Text>
                  </Card>
                ))}
              </Stack>
            )}
          </Card>

          {/* Messages conversation thread */}
          <Text fw={600} size="sm">Respostas e Mensagens ({question.messages?.length || 0})</Text>

          <ScrollArea.Autosize mah={280}>
            <Stack gap="xs">
              {(!question.messages || question.messages.length === 0) ? (
                <Text size="sm" c="dimmed" fs="italic">
                  Nenhuma resposta ainda. Você pode enviar uma mensagem abaixo.
                </Text>
              ) : (
                question.messages.map((msg) => {
                  const isSuper = msg.senderRole?.toLowerCase() === 'superadmin';
                  return (
                    <Card
                      key={msg.messageId}
                      withBorder
                      padding="xs"
                      radius="md"
                      style={{
                        backgroundColor: isSuper ? '#f0fdf4' : '#f8fafc',
                        borderColor: isSuper ? '#bbf7d0' : '#e2e8f0'
                      }}
                    >
                      <Group justify="space-between" mb={4}>
                        <Group gap="xs">
                          <Text fw={600} size="xs">
                            {msg.senderName}
                          </Text>
                          <Badge size="xs" color={isSuper ? 'green' : 'blue'}>
                            {msg.senderRole}
                          </Badge>
                        </Group>
                        <Text size="xs" c="dimmed">
                          {new Date(msg.createdAt).toLocaleString('pt-BR')}
                        </Text>
                      </Group>
                      <Text size="sm" style={{ whiteSpace: 'pre-wrap' }}>
                        {msg.content}
                      </Text>
                      {msg.imageUrl && (
                        <div style={{ marginTop: 6 }}>
                          <Image
                            src={msg.imageUrl}
                            alt="Imagem da resposta"
                            radius="sm"
                            mah={200}
                            fit="contain"
                            style={{ maxHeight: 200, width: 'auto', border: '1px solid #e2e8f0' }}
                          />
                        </div>
                      )}
                    </Card>
                  );
                })
              )}
            </Stack>
          </ScrollArea.Autosize>

          {/* Reply input */}
          <Divider />
          <Stack gap="xs">
            <Textarea
              placeholder="Digite sua resposta ou nova mensagem..."
              minRows={2}
              maxRows={4}
              maxLength={5000}
              value={replyContent}
              onChange={(e) => setReplyContent(e.target.value)}
            />
            <Group justify="space-between">
              <Button
                size="xs"
                variant="light"
                color={question.status === 'Resolvido' ? 'gray' : 'green'}
                onClick={() =>
                  question.status === 'Resolvido'
                    ? executeChangeStatus('Pendente', false)
                    : requestChangeStatus('Resolvido')
                }
              >
                {question.status === 'Resolvido' ? 'Reabrir Dúvida' : 'Marcar como Resolvido'}
              </Button>

              <Button
                size="xs"
                color="blue"
                leftSection={<IconSend size={14} />}
                loading={submitting}
                onClick={handleSendReply}
              >
                Enviar Mensagem
              </Button>
            </Group>
          </Stack>
        </Stack>
      )}

      {/* Confirmation modal for AI Summary generation */}
      <Modal
        opened={confirmResolveModalOpen}
        onClose={() => setConfirmResolveModalOpen(false)}
        title={
          <Group gap="xs">
            <IconCheck size={20} color="#16a34a" />
            <Text fw={600}>Marcar como Resolvido</Text>
          </Group>
        }
        size="md"
        centered
      >
        <Stack gap="md">
          <Text size="sm">
            Deseja que a <strong>Inteligência Artificial (Gemini)</strong> gere um resumo automático desta solução e alimente a nossa Central de Conhecimento / FAQ?
          </Text>
          <Text size="xs" c="dimmed">
            O resumo será gerado em segundo plano e adicionado como rascunho/artigo na documentação para futura resposta automática aos usuários.
          </Text>
          <Group justify="flex-end" gap="sm" mt="sm">
            <Button
              variant="default"
              size="xs"
              onClick={() => executeChangeStatus('Resolvido', false)}
            >
              Não, apenas resolver
            </Button>
            <Button
              color="green"
              size="xs"
              onClick={() => executeChangeStatus('Resolvido', true)}
            >
              Sim, gerar resumo com IA
            </Button>
          </Group>
        </Stack>
      </Modal>
    </Modal>
  );
};

