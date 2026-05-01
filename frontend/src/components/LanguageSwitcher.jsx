import { useTranslation } from 'react-i18next';
import { LOCALES, getCurrentLocale, getLocaleConfig, normalizeLocale } from '../i18n/locales';

export function LanguageSwitcher() {
  const { i18n } = useTranslation();
  const currentCode = normalizeLocale(i18n.resolvedLanguage || i18n.language || getCurrentLocale());
  const current = getLocaleConfig(currentCode);
  const next = LOCALES.find(locale => locale.code !== current.code) || LOCALES[0];

  const toggle = async () => {
    try {
      await i18n.changeLanguage(next.code);
    } catch (error) {
      console.error('Failed to switch language', error);
    }
  };

  return (
    <button
      type="button"
      onClick={toggle}
      title={`Switch to ${next.label}`}
      aria-label={`Switch language to ${next.label}`}
      className="lang-switcher"
    >
      <span className="lang-switcher-code">{current.shortLabel}</span>
    </button>
  );
}
