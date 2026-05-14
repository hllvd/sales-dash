import React, { Component, ErrorInfo, ReactNode } from 'react';
import { Title, Text, Button, Stack, Paper, Box } from '@mantine/core';
import { IconAlertTriangle } from '@tabler/icons-react';

interface Props {
  children?: ReactNode;
}

interface State {
  hasError: boolean;
  error: Error | null;
  errorInfo: ErrorInfo | null;
}

class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
    error: null,
    errorInfo: null
  };

  public static getDerivedStateFromError(error: Error): State {
    // Update state so the next render will show the fallback UI.
    return { hasError: true, error, errorInfo: null };
  }

  public componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error("Uncaught error:", error, errorInfo);
    this.setState({
      error: error,
      errorInfo: errorInfo
    });
  }

  public render() {
    if (this.state.hasError) {
      return (
        <Box style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', backgroundColor: '#f8fafc' }}>
          <Paper withBorder p="xl" shadow="md" style={{ maxWidth: 600, width: '100%' }}>
            <Stack align="center" gap="md">
              <IconAlertTriangle size={64} color="orange" />
              <Title order={2} ta="center">Ops! Algo deu errado.</Title>
              <Text ta="center" c="dimmed">
                A página encontrou um erro inesperado e precisou ser interrompida.
              </Text>
              
              <Box mt="md" p="md" style={{ backgroundColor: '#f1f5f9', borderRadius: 8, width: '100%', overflowX: 'auto' }}>
                <Text fw={600} size="sm" c="red">{this.state.error?.toString()}</Text>
                {this.state.errorInfo && (
                  <Text size="xs" mt="xs" style={{ whiteSpace: 'pre-wrap', fontFamily: 'monospace' }}>
                    {this.state.errorInfo.componentStack}
                  </Text>
                )}
              </Box>

              <Button 
                mt="xl" 
                size="md" 
                onClick={() => window.location.reload()}
              >
                Recarregar Página
              </Button>
            </Stack>
          </Paper>
        </Box>
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
