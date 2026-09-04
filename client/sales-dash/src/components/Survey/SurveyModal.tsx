import React, { useState, useEffect } from 'react';
import {
  Modal,
  Button,
  Radio,
  Checkbox,
  Group,
  Stack,
  Text,
  Title,
  Badge,
} from '@mantine/core';
import { notifications } from '@mantine/notifications';
import { IconCheck, IconX, IconQuestionMark } from '@tabler/icons-react';
import { SurveyAssignmentDto } from '../../types/Survey';
import { apiService } from '../../services/apiService';
import { surveyPollingService } from '../../services/surveyPollingService';
import './SurveyModal.css';

interface SurveyModalProps {
  explicitAssignment?: SurveyAssignmentDto | null;
  onClose?: () => void;
}

export const SurveyModal: React.FC<SurveyModalProps> = ({ explicitAssignment, onClose }) => {
  const [currentAssignment, setCurrentAssignment] = useState<SurveyAssignmentDto | null>(null);
  const [opened, setOpened] = useState<boolean>(false);
  const [selectedRadio, setSelectedRadio] = useState<string>('');
  const [selectedChecks, setSelectedChecks] = useState<string[]>([]);
  const [submitting, setSubmitting] = useState<boolean>(false);

  // Function to evaluate whether to show modal automatically
  const checkAutoPrompt = () => {
    if (explicitAssignment) return; // explicit control takes precedence
    const token = localStorage.getItem('token');
    if (!token) {
      setOpened(false);
      setCurrentAssignment(null);
      return;
    }

    const next = surveyPollingService.getNextPromptableQuestion();
    if (next) {
      setCurrentAssignment(next);
      setOpened(true);
      surveyPollingService.recordQuestionPrompt(next.assignmentId);
      setSelectedRadio('');
      setSelectedChecks([]);
    } else {
      setOpened(false);
      setCurrentAssignment(null);
    }
  };

  useEffect(() => {
    if (explicitAssignment) {
      setCurrentAssignment(explicitAssignment);
      setOpened(true);
      setSelectedRadio('');
      setSelectedChecks([]);
      return;
    }

    checkAutoPrompt();

    const handleUpdate = () => {
      checkAutoPrompt();
    };

    window.addEventListener('survey:updated', handleUpdate);
    return () => {
      window.removeEventListener('survey:updated', handleUpdate);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [explicitAssignment]);

  if (!currentAssignment) return null;

  const handleClose = () => {
    setOpened(false);
    setSelectedRadio('');
    setSelectedChecks([]);
    if (onClose) {
      onClose();
    }
  };

  const handleNotSure = () => {
    notifications.show({
      title: 'Pergunta adiada',
      message: 'Você poderá responder a esta pergunta mais tarde.',
      color: 'blue',
      icon: <IconQuestionMark size={16} />,
    });
    handleClose();
  };

  const handleSubmit = async () => {
    let finalAnswer = '';

    if (currentAssignment.questionType === 'yesno') {
      if (selectedRadio === 'unsure') {
        handleNotSure();
        return;
      }
      if (!selectedRadio) {
        notifications.show({
          title: 'Atenção',
          message: 'Por favor, selecione uma resposta.',
          color: 'orange',
          icon: <IconX size={16} />,
        });
        return;
      }
      finalAnswer = selectedRadio === 'yes' ? 'Sim' : 'Não';
    } else if (currentAssignment.questionType === 'singlechoice') {
      if (!selectedRadio) {
        notifications.show({
          title: 'Atenção',
          message: 'Por favor, selecione uma opção.',
          color: 'orange',
          icon: <IconX size={16} />,
        });
        return;
      }
      finalAnswer = selectedRadio;
    } else if (currentAssignment.questionType === 'multichoice') {
      if (selectedChecks.length === 0) {
        notifications.show({
          title: 'Atenção',
          message: 'Por favor, selecione pelo menos uma opção.',
          color: 'orange',
          icon: <IconX size={16} />,
        });
        return;
      }
      finalAnswer = JSON.stringify(selectedChecks);
    }

    try {
      setSubmitting(true);
      const res = await apiService.answerSurvey({
        assignmentId: currentAssignment.assignmentId,
        answer: finalAnswer,
      });

      if (res.success) {
        notifications.show({
          title: 'Sucesso',
          message: 'Resposta enviada com sucesso!',
          color: 'green',
          icon: <IconCheck size={16} />,
        });

        surveyPollingService.removePendingQuestion(currentAssignment.assignmentId);
        handleClose();
      } else {
        notifications.show({
          title: 'Erro',
          message: res.message || 'Erro ao enviar resposta.',
          color: 'red',
          icon: <IconX size={16} />,
        });
      }
    } catch (err: any) {
      notifications.show({
        title: 'Erro',
        message: err.message || 'Erro inesperado ao enviar resposta.',
        color: 'red',
        icon: <IconX size={16} />,
      });
    } finally {
      setSubmitting(false);
    }
  };

  const expiresDate = new Date(currentAssignment.expiresAt);
  const formattedExpires = expiresDate.toLocaleDateString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <Modal
      opened={opened}
      onClose={handleClose}
      title={
        <Group justify="space-between" style={{ width: '100%', paddingRight: '1rem' }}>
          <Title order={2} className="survey-modal-header" style={{ color: '#1c1c1e', fontWeight: 700, fontSize: '1.4rem' }}>
            {currentAssignment.title}
          </Title>
          <Badge color="orange" variant="light" size="sm">
            Expira em: {formattedExpires}
          </Badge>
        </Group>
      }
      size="md"
      centered
      closeOnClickOutside={false}
      overlayProps={{
        backgroundOpacity: 0.55,
        blur: 3,
      }}
      styles={{
        header: {
          borderBottom: '1px solid #f3f4f6',
          paddingBottom: '12px',
          marginBottom: '16px',
        },
        title: {
          width: '100%',
          color: '#1c1c1e',
        }
      }}
    >
      <Stack gap="md">
        <Text className="survey-question-text">{currentAssignment.questionText}</Text>

        {currentAssignment.questionType === 'yesno' && (
          <Radio.Group value={selectedRadio} onChange={setSelectedRadio} className="survey-options-group">
            <Stack gap="xs">
              <div
                className={`survey-option-card ${selectedRadio === 'yes' ? 'selected' : ''}`}
                onClick={() => setSelectedRadio('yes')}
              >
                <Radio value="yes" label="Sim" />
              </div>
              <div
                className={`survey-option-card ${selectedRadio === 'no' ? 'selected' : ''}`}
                onClick={() => setSelectedRadio('no')}
              >
                <Radio value="no" label="Não" />
              </div>
              <div
                className={`survey-option-card ${selectedRadio === 'unsure' ? 'selected' : ''}`}
                onClick={() => setSelectedRadio('unsure')}
              >
                <Radio value="unsure" label="Não tenho certeza ainda" />
              </div>
            </Stack>
          </Radio.Group>
        )}

        {currentAssignment.questionType === 'singlechoice' && currentAssignment.options && (
          <Radio.Group value={selectedRadio} onChange={setSelectedRadio} className="survey-options-group">
            <Stack gap="xs">
              {currentAssignment.options.map((option, idx) => (
                <div
                  key={idx}
                  className={`survey-option-card ${selectedRadio === option ? 'selected' : ''}`}
                  onClick={() => setSelectedRadio(option)}
                >
                  <Radio value={option} label={option} />
                </div>
              ))}
            </Stack>
          </Radio.Group>
        )}

        {currentAssignment.questionType === 'multichoice' && currentAssignment.options && (
          <Checkbox.Group value={selectedChecks} onChange={setSelectedChecks} className="survey-options-group">
            <Stack gap="xs">
              {currentAssignment.options.map((option, idx) => {
                const isChecked = selectedChecks.includes(option);
                return (
                  <div
                    key={idx}
                    className={`survey-option-card ${isChecked ? 'selected' : ''}`}
                    onClick={() => {
                      if (isChecked) {
                        setSelectedChecks(selectedChecks.filter((c) => c !== option));
                      } else {
                        setSelectedChecks([...selectedChecks, option]);
                      }
                    }}
                  >
                    <Checkbox value={option} label={option} />
                  </div>
                );
              })}
            </Stack>
          </Checkbox.Group>
        )}

        <div className="survey-modal-footer">
          <Button variant="subtle" color="gray" onClick={handleClose} disabled={submitting}>
            Responder depois
          </Button>
          <Button
            color="red"
            onClick={handleSubmit}
            loading={submitting}
            disabled={
              currentAssignment.questionType === 'multichoice'
                ? selectedChecks.length === 0
                : !selectedRadio
            }
          >
            {selectedRadio === 'unsure' ? 'Adiar resposta' : 'Enviar resposta'}
          </Button>
        </div>
      </Stack>
    </Modal>
  );
};
