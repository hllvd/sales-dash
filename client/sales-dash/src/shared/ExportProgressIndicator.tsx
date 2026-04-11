import React, { useEffect, useRef } from 'react';

interface ExportProgressIndicatorProps {
  jobId: string | null;
  pollUrl: (jobId: string) => string;      // e.g. (id) => `/api/contracts/export/${id}`
  downloadUrl: (jobId: string) => string;  // e.g. (id) => `/api/contracts/export/${id}/download`
  onComplete: () => void;
  onError: (message: string) => void;
  token: string;
}

/**
 * Dumb polling indicator.
 * When jobId is set, polls the given URL every 2s.
 * On "completed" status, triggers a file download via a hidden <a> tag.
 */
const ExportProgressIndicator: React.FC<ExportProgressIndicatorProps> = ({
  jobId,
  pollUrl,
  downloadUrl,
  onComplete,
  onError,
  token,
}) => {
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const totalRef = useRef<number>(0);
  const processedRef = useRef<number>(0);
  const [progress, setProgress] = React.useState(0);
  const [statusText, setStatusText] = React.useState('');

  useEffect(() => {
    if (!jobId) {
      if (intervalRef.current) clearInterval(intervalRef.current);
      setProgress(0);
      setStatusText('');
      return;
    }

    const poll = async () => {
      try {
        const res = await fetch(pollUrl(jobId), {
          headers: { Authorization: `Bearer ${token}` },
        });

        if (!res.ok) {
          clearInterval(intervalRef.current!);
          onError('Falha ao verificar status da exportação.');
          return;
        }

        const json = await res.json();
        const data = json.data;

        totalRef.current = data.totalRows || 0;
        processedRef.current = data.processedRows || 0;

        const pct =
          totalRef.current > 0
            ? Math.round((processedRef.current / totalRef.current) * 100)
            : 0;
        setProgress(pct);
        setStatusText(`${processedRef.current} / ${totalRef.current} linhas`);

        if (data.status === 'completed') {
          clearInterval(intervalRef.current!);
          // Trigger browser download via hidden anchor
          const link = document.createElement('a');
          link.href = downloadUrl(jobId);
          // Pass token in query — simple workaround for binary file download
          link.href += `?token=${encodeURIComponent(token)}`;
          link.setAttribute('download', '');
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          onComplete();
        } else if (data.status === 'failed') {
          clearInterval(intervalRef.current!);
          onError(data.errorMessage || 'A exportação falhou.');
        }
      } catch {
        clearInterval(intervalRef.current!);
        onError('Erro de comunicação com o servidor.');
      }
    };

    poll(); // immediate first check
    intervalRef.current = setInterval(poll, 2000);

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [jobId]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!jobId) return null;

  return (
    <div
      id="export-progress-indicator"
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        padding: '6px 12px',
        background: '#f0fdf4',
        border: '1px solid #86efac',
        borderRadius: 8,
        fontSize: 13,
        color: '#15803d',
      }}
    >
      {/* Progress bar */}
      <div
        style={{
          width: 120,
          height: 6,
          background: '#dcfce7',
          borderRadius: 3,
          overflow: 'hidden',
        }}
      >
        <div
          style={{
            width: `${progress}%`,
            height: '100%',
            background: '#22c55e',
            borderRadius: 3,
            transition: 'width 0.4s ease',
          }}
        />
      </div>
      <span>{progress > 0 ? `${progress}% — ${statusText}` : 'Iniciando...'}</span>
    </div>
  );
};

export default ExportProgressIndicator;
