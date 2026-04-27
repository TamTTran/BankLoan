import { useTranslation } from 'react-i18next';

const LANGS = [
  { code: 'vi', flag: '🇻🇳', label: 'VI' },
  { code: 'en', flag: '🇬🇧', label: 'EN' },
];

export function LanguageSwitcher() {
  const { i18n } = useTranslation();
  const current = i18n.language?.startsWith('en') ? 'en' : 'vi';

  const toggle = () => {
    const next = current === 'vi' ? 'en' : 'vi';
    i18n.changeLanguage(next);
  };

  const curr = LANGS.find(l => l.code === current);
  const next = LANGS.find(l => l.code !== current);

  return (
    <button
      onClick={toggle}
      title={`Switch to ${next.label}`}
      aria-label={`Switch language to ${next.label}`}
      className="lang-switcher"
    >
      <span className="lang-switcher-flag">{curr.flag}</span>
      {curr.label}
    </button>
  );
}
