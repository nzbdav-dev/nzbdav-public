import { useState, useEffect } from 'react';
import type { ConnectionStatsResponse } from '~/clients/backend-client.server';

export function useConnectionStats(refreshInterval: number = 10000) {
  const [connectionStats, setConnectionStats] = useState<ConnectionStatsResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;
    let intervalId: NodeJS.Timeout;

    const fetchStats = async () => {
      try {
        const response = await fetch('/api/frontend/connection-stats');
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}`);
        }
        const stats = await response.json();
        if (mounted) {
          setConnectionStats(stats);
          setError(null);
          setLoading(false);
        }
      } catch (err) {
        if (mounted) {
          setError(err instanceof Error ? err.message : 'Failed to fetch connection stats');
          setLoading(false);
        }
      }
    };

    fetchStats();

    if (refreshInterval > 0) {
      intervalId = setInterval(fetchStats, refreshInterval);
    }

    return () => {
      mounted = false;
      if (intervalId) {
        clearInterval(intervalId);
      }
    };
  }, [refreshInterval]);

  return { connectionStats, loading, error };
}