import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import vi from './locales/vi.json';
import en from './locales/en.json';
import { DEFAULT_LOCALE, LOCALE_STORAGE_KEY, normalizeLocale, syncDocumentLocale } from './locales';

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      'vi-VN': { translation: vi },
      'en-US': { translation: en },
    },
    fallbackLng: DEFAULT_LOCALE,
    supportedLngs: ['vi-VN', 'en-US'],
    load: 'currentOnly',
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupLocalStorage: LOCALE_STORAGE_KEY,
      convertDetectedLanguage: normalizeLocale,
    },
    interpolation: {
      escapeValue: false, // React already handles XSS
    },
  })
  .then(() => syncDocumentLocale(i18n.resolvedLanguage || i18n.language || DEFAULT_LOCALE));

i18n.on('languageChanged', syncDocumentLocale);

export default i18n;
