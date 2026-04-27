import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './i18n/index.js';    // must be before App
import { App } from './app/App';
import './index.css';


createRoot(document.getElementById('root')).render(
  <StrictMode>
    <App />
  </StrictMode>
);
