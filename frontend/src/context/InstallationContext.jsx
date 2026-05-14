import { createContext, useCallback, useContext, useEffect, useState } from 'react';
import { api } from '../services/api';

const STORAGE_KEY = 'solar.selectedInstallationId';
const InstallationContext = createContext(null);

export function InstallationProvider({ children }) {
  const [installations, setInstallations] = useState([]);
  const [selectedId, setSelectedId] = useState(() => localStorage.getItem(STORAGE_KEY));
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  const refresh = useCallback(async () => {
    try {
      const list = await api.installations();
      setInstallations(list);
      setError(null);
      const stored = localStorage.getItem(STORAGE_KEY);
      const valid = stored && list.some((i) => i.id === stored);
      if (!valid && list.length > 0) {
        setSelectedId(list[0].id);
        localStorage.setItem(STORAGE_KEY, list[0].id);
      } else if (list.length === 0) {
        setSelectedId(null);
        localStorage.removeItem(STORAGE_KEY);
      }
      return list;
    } catch (e) {
      setError(e);
      return [];
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  const select = (id) => {
    setSelectedId(id);
    localStorage.setItem(STORAGE_KEY, id);
  };

  const selected = installations.find((i) => i.id === selectedId) ?? null;

  return (
    <InstallationContext.Provider
      value={{ installations, selected, selectedId, select, loading, error, refresh }}
    >
      {children}
    </InstallationContext.Provider>
  );
}

export function useInstallation() {
  const ctx = useContext(InstallationContext);
  if (!ctx) throw new Error('useInstallation must be used inside InstallationProvider');
  return ctx;
}
