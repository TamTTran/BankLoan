export const LOCALE_STORAGE_KEY = 'loanflow_lang';
export const DEFAULT_LOCALE = 'vi-VN';

export const LOCALES = [
  {
    code: 'vi-VN',
    shortLabel: 'VI',
    label: 'Tiếng Việt',
    fontFamily: "'Segoe UI Variable', 'Segoe UI', 'Noto Sans', 'Arial', sans-serif",
  },
  {
    code: 'en-US',
    shortLabel: 'EN',
    label: 'English',
    fontFamily: "'Inter', 'Segoe UI Variable', 'Segoe UI', 'Helvetica Neue', 'Arial', sans-serif",
  },
];

const ALIAS_TO_LOCALE = {
  vi: 'vi-VN',
  'vi-vn': 'vi-VN',
  'vi-vi': 'vi-VN',
  en: 'en-US',
  'en-us': 'en-US',
  'en-gb': 'en-US',
  'en-en': 'en-US',
};

export function normalizeLocale(value) {
  const raw = String(value || '').trim();
  if (!raw) return DEFAULT_LOCALE;

  const normalized = raw.replace('_', '-').toLowerCase();
  if (ALIAS_TO_LOCALE[normalized]) return ALIAS_TO_LOCALE[normalized];
  if (normalized.startsWith('vi')) return 'vi-VN';
  if (normalized.startsWith('en')) return 'en-US';

  return DEFAULT_LOCALE;
}

export function getCurrentLocale() {
  if (typeof window === 'undefined') return DEFAULT_LOCALE;

  const stored = window.localStorage?.getItem(LOCALE_STORAGE_KEY);
  if (stored) return normalizeLocale(stored);

  return normalizeLocale(window.navigator?.language);
}

export function getLocaleConfig(locale) {
  const normalized = normalizeLocale(locale);
  return LOCALES.find(item => item.code === normalized) || LOCALES[0];
}

export function syncDocumentLocale(locale) {
  if (typeof document === 'undefined') return;

  const current = getLocaleConfig(locale);
  document.documentElement.lang = current.code;
  document.documentElement.dataset.locale = current.code;
  document.documentElement.dir = 'ltr';
  document.documentElement.style.setProperty('--locale-font-sans', current.fontFamily);

  try {
    window.localStorage?.setItem(LOCALE_STORAGE_KEY, current.code);
  } catch {
    // Local storage can be blocked in hardened browsers; UI should still switch.
  }
}
