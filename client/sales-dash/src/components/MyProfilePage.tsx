import React, { useState } from 'react';
import { Title, Button, Modal, TextInput, Alert, Group } from '@mantine/core';
import { IconMailForward, IconAlertCircle, IconSend } from '@tabler/icons-react';
import Menu from './Menu';
import { useCurrentUser } from '../contexts/CurrentUserContext';
import { UserProfile } from './UserProfile';
import { apiService } from '../services/apiService';

const MyProfilePage: React.FC = () => {
  const { currentUser } = useCurrentUser();
  const [modalOpen, setModalOpen] = useState(false);
  const [newParentEmail, setNewParentEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const handleSubmitRequest = async () => {
    const trimmedEmail = newParentEmail.trim();
    if (!trimmedEmail) {
      setError('Por favor, informe o e-mail do novo superior.');
      return;
    }
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(trimmedEmail)) {
      setError('Por favor, informe um e-mail com formato válido.');
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      await apiService.createApprovalRequest({
        requestType: 'ChangeParentEmail',
        payloadJson: JSON.stringify({ newParentEmail: trimmedEmail }),
      });
      setSuccess('Solicitação de alteração enviada com sucesso!');
      setModalOpen(false);
      setNewParentEmail('');
    } catch (err: any) {
      setError(err.message || 'Erro ao enviar solicitação.');
    } finally {
      setSubmitting(false);
    }
  };

  if (!currentUser) {
    return (
      <Menu>
        <div className="my-profile-page" style={{ display: 'flex', justifyContent: 'center', padding: '60px' }}>
          <p>Carregando perfil...</p>
        </div>
      </Menu>
    );
  }

  return (
    <Menu>
      <div className="my-profile-page" style={{ padding: '24px', maxWidth: '1200px', margin: '0 auto' }}>
        <div className="my-profile-header" style={{ marginBottom: '20px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <Title order={2} size="h2" className="page-title-break">Meu Perfil</Title>
          <Button
            leftSection={<IconMailForward size={18} />}
            color="red"
            variant="outline"
            onClick={() => {
              setError(null);
              setModalOpen(true);
            }}
          >
            Solicitar Alteração de Superior
          </Button>
        </div>

        {success && (
          <Alert color="green" mb="md" withCloseButton onClose={() => setSuccess(null)}>
            {success}
          </Alert>
        )}

        <UserProfile userId={currentUser.id} mode="page" />

        <Modal
          opened={modalOpen}
          onClose={() => setModalOpen(false)}
          title="Solicitar Alteração de Superior"
          centered
        >
          {error && (
            <Alert icon={<IconAlertCircle size={16} />} color="red" mb="md">
              {error}
            </Alert>
          )}

          <TextInput
            label="E-mail do Novo Superior"
            placeholder="superior@exemplo.com"
            required
            value={newParentEmail}
            onChange={(e) => setNewParentEmail(e.target.value)}
            mb="md"
          />

          <Group justify="flex-end">
            <Button variant="default" onClick={() => setModalOpen(false)}>
              Cancelar
            </Button>
            <Button color="red" leftSection={<IconSend size={16} />} loading={submitting} onClick={handleSubmitRequest}>
              Enviar Solicitação
            </Button>
          </Group>
        </Modal>
      </div>
    </Menu>
  );
};

export default MyProfilePage;
