import { useTranslation } from 'react-i18next';

/**
 * Thin wrapper around react-i18next's useTranslation.
 * Use this everywhere instead of importing useTranslation directly,
 * so we can swap the implementation in one place if needed.
 */
export function useT() {
  return useTranslation();
}
