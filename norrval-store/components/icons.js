// Single-weight 1.5px line icons, drawn on a 24px grid, stroke = currentColor.
const s = (d, extra = '') =>
  `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"${extra}>${d}</svg>`;

export const ICONS = {
  bag: s('<path d="M5 8h14l-1 12H6L5 8Z"/><path d="M9 8V6a3 3 0 0 1 6 0v2"/>'),
  menu: s('<path d="M4 8h16M4 16h16"/>'),
  close: s('<path d="M6 6l12 12M18 6 6 18"/>'),
  arrow: s('<path d="M5 12h14M13 6l6 6-6 6"/>', ' class="arrow" width="16" height="16"'),
  left: s('<path d="M15 6l-6 6 6 6"/>'),
  right: s('<path d="M9 6l6 6-6 6"/>'),
  check: s('<path d="M5 12.5l4.5 4.5L19 7.5"/>'),
  truck: s('<path d="M3 7h11v9H3zM14 10h4l3 3v3h-7"/><circle cx="7" cy="17.5" r="1.5"/><circle cx="17" cy="17.5" r="1.5"/>'),
  returns: s('<path d="M4 12a8 8 0 1 0 2.3-5.6"/><path d="M4 4v4h4"/>'),
  lock: s('<rect x="5" y="11" width="14" height="9" rx="1"/><path d="M8 11V8a4 4 0 0 1 8 0v3"/>'),
  chat: s('<path d="M4 5h16v11H9l-5 4V5Z"/>'),
  pin: s('<path d="M12 21s7-6.2 7-11.5A7 7 0 0 0 5 9.5C5 14.8 12 21 12 21Z"/><circle cx="12" cy="9.5" r="2.5"/>'),
  gift: s('<rect x="4" y="9" width="16" height="11"/><path d="M3 9h18M12 9v11M12 9S10.5 4 8 4.5 7.5 9 12 9Zm0 0s1.5-5 4-4.5S16.5 9 12 9Z"/>'),
  dial: s('<circle cx="12" cy="12" r="8"/><path d="M12 7v5l3 2"/>'),
  layers: s('<path d="M12 4 3 9l9 5 9-5-9-5Z"/><path d="m3 14 9 5 9-5"/>'),
  wrist: s('<rect x="8" y="8" width="8" height="8" rx="4"/><path d="M9 8 10 3h4l1 5M9 16l1 5h4l1-5"/>'),
  globe: s('<circle cx="12" cy="12" r="8.5"/><path d="M3.5 12h17M12 3.5c2.5 2.6 2.5 14.4 0 17M12 3.5c-2.5 2.6-2.5 14.4 0 17"/>'),
  mail: s('<rect x="3" y="5" width="18" height="14"/><path d="m3 6 9 7 9-7"/>'),
  clock: s('<circle cx="12" cy="12" r="8.5"/><path d="M12 7.5V12l3 2"/>'),
  plus: s('<path d="M12 5v14M5 12h14"/>'),
  minus: s('<path d="M5 12h14"/>'),
};

/** NORRVAL wordmark with its dial mark: a ring with one hand at ten past. */
export const LOGO = `<svg viewBox="0 0 196 28" role="img" aria-label="NORRVAL"><g fill="none" stroke="currentColor" stroke-width="1.6"><circle cx="14" cy="14" r="12"/><path d="M14 14 7.5 9.5" stroke-linecap="round"/><path d="M14 14l5-7" stroke="#3fb8c9" stroke-linecap="round"/></g><circle cx="14" cy="14" r="1.8" fill="currentColor"/><text x="38" y="20.5" fill="currentColor" font-family="Manrope, system-ui, sans-serif" font-size="17" font-weight="500" letter-spacing="6.2">NORRVAL</text></svg>`;
