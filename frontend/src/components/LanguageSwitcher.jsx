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
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '0.35rem',
        padding: '0.38rem 0.65rem',
        borderRadius: '10px',
        border: '1px solid rgba(255,255,255,0.14)',
        background: 'rgba(255,255,255,0.07)',
        color: 'rgba(255,255,255,0.82)',
        fontSize: '0.78rem',
        fontWeight: 700,
        fontFamily: 'inherit',
        cursor: 'pointer',
        transition: 'all 0.18s',
        letterSpacing: '0.04em',
      }}
      onMouseEnter={e => { e.currentTarget.style.background = 'rgba(255,255,255,0.14)'; e.currentTarget.style.color = '#fff'; }}
      onMouseLeave={e => { e.currentTarget.style.background = 'rgba(255,255,255,0.07)'; e.currentTarget.style.color = 'rgba(255,255,255,0.82)'; }}
    >
      <span style={{ fontSize: '1rem', lineHeight: 1 }}>{curr.flag}</span>
      {curr.label}
    </button>
  );
}
