import { notifications } from '@mantine/notifications';

export const toast = {
  success: (message: string, title?: string) => {
    notifications.show({
      title: title || 'Sucesso',
      message,
      color: 'green',
      autoClose: 4000,
      position: 'top-right',
    });
  },

  error: (message: string, title?: string) => {
    notifications.show({
      title: title || 'Erro',
      message,
      color: 'red',
      autoClose: 5000,
      position: 'top-right',
    });
  },

  warning: (message: string, title?: string) => {
    notifications.show({
      title: title || 'Atenção',
      message,
      color: 'yellow',
      autoClose: 4000,
      position: 'top-right',
    });
  },

  info: (message: string, title?: string) => {
    notifications.show({
      title: title || 'Informação',
      message,
      color: 'blue',
      autoClose: 4000,
      position: 'top-right',
    });
  },
};
