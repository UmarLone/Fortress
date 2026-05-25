// options.js  -  Fortress Options Page
'use strict';

const $ = (id) => document.getElementById(id);

//  Theme 

async function applyTheme(themeVal) {
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  const isDark = themeVal === 'dark' || (themeVal === 'system' && prefersDark);
  document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');
  // Sync active pill
  document.querySelectorAll('.theme-pill').forEach(p => {
    p.classList.toggle('active', p.dataset.themeVal === themeVal);
  });
  await chrome.storage.sync.set({ theme: themeVal });
}

async function loadTheme() {
  const { theme } = await chrome.storage.sync.get({ theme: 'system' });
  await applyTheme(theme);
}

// Theme pills handler
document.querySelectorAll('.theme-pill').forEach(pill => {
  pill.addEventListener('click', () => applyTheme(pill.dataset.themeVal));
});

//  Navigation 

let _lastVaultNavPage = 'logins'; // track which vault sub-page (logins/auth/cards) is active

document.querySelectorAll('.snav-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    const page        = btn.dataset.page;
    const vaultFilter = btn.dataset.vaultFilter; // set on logins/authenticators/cards buttons

    document.querySelectorAll('.snav-btn').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
    document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));

    if (vaultFilter !== undefined) {
      // Filtered vault views: Logins, Authenticators, Cards
      _lastVaultNavPage = page;
      const vaultPage = document.getElementById('page-vault');
      if (vaultPage) vaultPage.classList.add('active');
      _optActiveFilter = vaultFilter;
      // Sync filter chips in vault page
      document.querySelectorAll('#page-vault .opt-chip[data-filter]').forEach(c => {
        c.classList.toggle('active', c.dataset.filter === vaultFilter);
      });
      loadVault();
    } else {
      const pg = document.getElementById(`page-${page}`);
      if (pg) pg.classList.add('active');
      if (page === 'secure-items') loadSecureItems();
      if (page === 'dashboard')    loadDashboard();
      if (page === 'ai-insights')  { /* loaded on scan */ }
    }
  });
});

//  Toast / Snackbar 

const TOAST_ICONS = {
  success: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" width="15" height="15"><polyline points="20 6 9 17 4 12"/></svg>`,
  error:   `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" width="15" height="15"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>`,
  warn:    `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" width="15" height="15"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>`,
  info:    `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" width="15" height="15"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`,
  default: `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" width="15" height="15"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg>`,
};

function toast(msg, type = 'info') {
  const container = $('toast-container');
  if (!container) return;
  // Limit stack to 5
  while (container.children.length >= 5) container.firstChild?.remove();

  const t = type === 'success' ? 'success' : type === 'error' ? 'error' : type === 'warn' ? 'warn' : 'info';
  const item = document.createElement('div');
  item.className = `toast-item toast-${t}`;
  item.innerHTML = `
    <div class="toast-stripe"></div>
    <div class="toast-icon">${TOAST_ICONS[t] ?? TOAST_ICONS.info}</div>
    <div class="toast-msg">${msg.replace(/</g,'&lt;').replace(/>/g,'&gt;')}</div>
    <button class="toast-close" aria-label="Close">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
    </button>`;

  const dismiss = () => {
    item.classList.add('toast-exit');
    item.addEventListener('animationend', () => item.remove(), { once: true });
    clearTimeout(tid);
  };
  item.querySelector('.toast-close').addEventListener('click', dismiss);
  container.appendChild(item);

  const tid = setTimeout(dismiss, 3200);
}

//  Load / save settings 

const DEFAULTS = {
  autoLockSeconds:  300,
  lockOnClose:      true,
  lockOnSleep:      true,
  clearClipboard:   true,
  clipboardTimeout: 20,
  hibp:             true,
  weakWarn:         true,
  inlineBtn:        true,
  autofillOnLoad:   false,
  savePrompt:       true,
  updatePrompt:     true,
  notifBreach:      true,
  notifLock:        true,
  notifWeak:        false,
  genLength:        20,
  genUpper:         true,
  genLower:         true,
  genDigits:        true,
  genSymbols:       true,
  genAmbiguous:     false,
  genWords:         4,
  genSep:           '-',
  excludedSites:    [],
};

async function loadSettings() {
  const s = await chrome.storage.sync.get(DEFAULTS);

  $('opt-autolock').value               = s.autoLockSeconds;
  $('opt-lock-on-close').checked        = s.lockOnClose;
  $('opt-lock-on-sleep').checked        = s.lockOnSleep;
  $('opt-clear-clipboard').checked      = s.clearClipboard;
  $('opt-clipboard-timeout').value      = s.clipboardTimeout;
  $('opt-hibp').checked                 = s.hibp;
  $('opt-weak-warn').checked            = s.weakWarn;

  $('opt-inline-btn').checked           = s.inlineBtn;
  $('opt-autofill-on-load').checked     = s.autofillOnLoad;
  $('opt-save-prompt').checked          = s.savePrompt;
  $('opt-update-prompt').checked        = s.updatePrompt;

  $('opt-notif-breach').checked         = s.notifBreach;
  $('opt-notif-lock').checked           = s.notifLock;
  $('opt-notif-weak').checked           = s.notifWeak;

  $('opt-gen-len').value                = s.genLength;
  $('opt-gen-len-val').textContent      = s.genLength;
  $('opt-gen-upper').checked            = s.genUpper;
  $('opt-gen-lower').checked            = s.genLower;
  $('opt-gen-digits').checked           = s.genDigits;
  $('opt-gen-symbols').checked          = s.genSymbols;
  $('opt-gen-ambiguous').checked        = s.genAmbiguous;
  $('opt-gen-words').value              = s.genWords;
  $('opt-gen-sep').value                = s.genSep;

  renderExcludedSites(s.excludedSites ?? []);
}

function collectSettings() {
  return {
    autoLockSeconds:  +$('opt-autolock').value,
    lockOnClose:      $('opt-lock-on-close').checked,
    lockOnSleep:      $('opt-lock-on-sleep').checked,
    clearClipboard:   $('opt-clear-clipboard').checked,
    clipboardTimeout: +$('opt-clipboard-timeout').value,
    hibp:             $('opt-hibp').checked,
    weakWarn:         $('opt-weak-warn').checked,
    inlineBtn:        $('opt-inline-btn').checked,
    autofillOnLoad:   $('opt-autofill-on-load').checked,
    savePrompt:       $('opt-save-prompt').checked,
    updatePrompt:     $('opt-update-prompt').checked,
    notifBreach:      $('opt-notif-breach').checked,
    notifLock:        $('opt-notif-lock').checked,
    notifWeak:        $('opt-notif-weak').checked,
    genLength:        +$('opt-gen-len').value,
    genUpper:         $('opt-gen-upper').checked,
    genLower:         $('opt-gen-lower').checked,
    genDigits:        $('opt-gen-digits').checked,
    genSymbols:       $('opt-gen-symbols').checked,
    genAmbiguous:     $('opt-gen-ambiguous').checked,
    genWords:         +$('opt-gen-words').value,
    genSep:           $('opt-gen-sep').value,
  };
}

//  Generator range live update 

$('opt-gen-len').addEventListener('input', (e) => {
  $('opt-gen-len-val').textContent = e.target.value;
});

//  Save buttons 

$('btn-save-security').addEventListener('click', async () => {
  const s = collectSettings();
  await chrome.storage.sync.set(s);
  await chrome.runtime.sendMessage({ type: 'RESCHEDULE_AUTOLOCK', seconds: s.autoLockSeconds }).catch(() => {});
  toast('Security settings saved', 'success');
});

$('btn-save-autofill').addEventListener('click', async () => {
  await chrome.storage.sync.set(collectSettings());
  toast('Autofill settings saved', 'success');
});

$('btn-save-generator').addEventListener('click', async () => {
  await chrome.storage.sync.set(collectSettings());
  toast('Generator settings saved', 'success');
});

$('btn-save-notifications').addEventListener('click', async () => {
  await chrome.storage.sync.set(collectSettings());
  toast('Notification settings saved', 'success');
});

//  Excluded sites 

let _excludedSites = [];

function renderExcludedSites(sites) {
  _excludedSites = sites;
  const list = $('excluded-list');
  list.innerHTML = '';
  if (!sites.length) {
    list.innerHTML = `<div style="font-size:13px;color:var(--text-muted);padding:8px 0;">No excluded sites.</div>`;
    return;
  }
  sites.forEach((site, i) => {
    const row = document.createElement('div');
    row.className = 'excluded-site';
    row.innerHTML = `<span>${site}</span><button class="excluded-remove" data-i="${i}" title="Remove">&times;</button>`;
    list.appendChild(row);
  });
  list.querySelectorAll('.excluded-remove').forEach(btn => {
    btn.addEventListener('click', async () => {
      _excludedSites.splice(+btn.dataset.i, 1);
      await chrome.storage.sync.set({ excludedSites: _excludedSites });
      renderExcludedSites(_excludedSites);
    });
  });
}

$('btn-add-excluded').addEventListener('click', async () => {
  const input = $('excluded-input');
  const url = input.value.trim();
  if (!url) return;
  try {
    const host = new URL(url.startsWith('http') ? url : 'https://' + url).hostname;
    if (!_excludedSites.includes(host)) {
      _excludedSites.push(host);
      await chrome.storage.sync.set({ excludedSites: _excludedSites });
      renderExcludedSites(_excludedSites);
    }
    input.value = '';
  } catch {
    toast('Invalid URL', 'error');
  }
});

$('excluded-input').addEventListener('keydown', (e) => {
  if (e.key === 'Enter') $('btn-add-excluded').click();
});

//  Data actions 

$('btn-export-encrypted').addEventListener('click', async () => {
  try {
    const r = await chrome.runtime.sendMessage({ type: 'EXPORT_VAULT_ENCRYPTED' });
    if (!r?.data) { toast('Export failed  vault may be locked', 'error'); return; }
    downloadFile('fortress-vault-encrypted.json', r.data, 'application/json');
    toast('Encrypted export downloaded', 'success');
  } catch (e) {
    toast(e.message, 'error');
  }
});

$('btn-export-plain').addEventListener('click', async () => {
  if (!confirm('This will export your vault as an unencrypted CSV file.\nAnyone who accesses this file can read all your passwords.\n\nAre you sure?')) return;
  try {
    const r = await chrome.runtime.sendMessage({ type: 'EXPORT_VAULT_PLAIN' });
    if (!r?.csv) { toast('Export failed  vault may be locked', 'error'); return; }
    downloadFile('fortress-vault-plain.csv', r.csv, 'text/csv');
    toast('Plain CSV exported', 'success');
  } catch (e) {
    toast(e.message, 'error');
  }
});

$('btn-import').addEventListener('click', () => $('import-file-input').click());

$('import-file-input').addEventListener('change', async (e) => {
  const file = e.target.files?.[0];
  if (!file) return;
  const format = $('import-format').value;
  const statusEl = $('import-status');
  statusEl.hidden = false;
  statusEl.className = 'import-status';
  statusEl.textContent = `Importing ${file.name}`;

  try {
    const text = await file.text();
    const r = await chrome.runtime.sendMessage({ type: 'IMPORT_VAULT', format, data: text });
    if (r?.ok) {
      statusEl.className = 'import-status success';
      statusEl.textContent = `Imported ${r.count ?? 'items'} successfully.`;
    } else {
      throw new Error(r?.error ?? 'Import failed');
    }
  } catch (err) {
    statusEl.className = 'import-status error';
    statusEl.textContent = `Error: ${err.message}`;
  }
  e.target.value = '';
});

$('btn-clear-data').addEventListener('click', async () => {
  if (!confirm('Clear all local extension data? The vault on the service will not be affected.')) return;
  await chrome.storage.session.clear();
  await chrome.storage.local.clear();
  toast('Local data cleared', 'success');
});

//  About status 

let _statusPollTimer = null;

async function checkStatus() {
  const dot     = $('sidebar-status-dot');
  const overlay = $('options-lock-overlay');
  try {
    const r = await chrome.runtime.sendMessage({ type: 'GET_STATUS' });
    $('host-status-badge').className    = 'status-badge status-ok';
    $('host-status-badge').textContent  = 'Connected';
    const unlocked = !!r?.isUnlocked;
    $('session-status-badge').className   = unlocked ? 'status-badge status-ok' : 'status-badge status-error';
  $('session-status-badge').textContent = unlocked ? 'Unlocked' : 'Locked';
    if (dot) dot.classList.add('online');
    if (overlay) overlay.hidden = unlocked;

    // While locked, poll every 2 s so the overlay dismisses the moment the
    // user unlocks in the popup. Stop polling once unlocked.
    if (!unlocked) {
      if (!_statusPollTimer)
        _statusPollTimer = setInterval(checkStatus, 2000);
    } else {
    clearInterval(_statusPollTimer);
      _statusPollTimer = null;
    }
  } catch {
    $('host-status-badge').className      = 'status-badge status-error';
    $('host-status-badge').textContent  = 'Not running';
    $('session-status-badge').className   = 'status-badge status-error';
    $('session-status-badge').textContent = 'Unknown';
    if (dot) dot.classList.remove('online');
    if (overlay) overlay.hidden = false;
    // Keep polling — service may start up
    if (!_statusPollTimer)
      _statusPollTimer = setInterval(checkStatus, 2000);
  }
}

// Also react immediately when the session token appears in storage
// (background.js calls chrome.storage.session.set({ sessionToken }) on unlock)
chrome.storage.session.onChanged.addListener((changes) => {
  if ('sessionToken' in changes) {
    // Token was set (unlock) or removed (lock) — re-check immediately
    checkStatus();
  }
});

// Unlock button on the lock overlay — open the popup so the user can unlock
$('btn-lock-overlay-unlock')?.addEventListener('click', () => {
  chrome.runtime.sendMessage({ type: 'OPEN_POPUP' }).catch(() => {
    chrome.tabs.create({ url: chrome.runtime.getURL('popup/popup.html') });
  });
});

//  Utilities 

function downloadFile(name, content, mimeType) {
  const blob = new Blob([content], { type: mimeType });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href = url; a.download = name;
  document.body.appendChild(a); a.click();
  setTimeout(() => { URL.revokeObjectURL(url); a.remove(); }, 1000);
}

// =========================================================================
//  TOTP  (ported from popup.js so Authenticators have live codes here too)
// =========================================================================

function base32Decode(s) {
  const alpha = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  s = s.toUpperCase().replace(/[= \n]/g, '');
  let bits = 0, value = 0;
  const out = [];
  for (const c of s) {
    const idx = alpha.indexOf(c);
    if (idx < 0) continue;
    value = (value << 5) | idx;
    bits += 5;
    if (bits >= 8) { bits -= 8; out.push((value >> bits) & 0xFF); }
  }
  return new Uint8Array(out);
}

function parseTotpSecret(raw) {
  if (!raw) return null;
  if (raw.startsWith('otpauth://')) {
    try { return new URL(raw).searchParams.get('secret') || null; } catch { return null; }
  }
  return raw.trim() || null;
}

async function generateTOTP(secret) {
  const key = base32Decode(secret);
  if (!key.length) throw new Error('invalid secret');
  const counter = Math.floor(Date.now() / 1000 / 30);
  const buf = new ArrayBuffer(8);
  new DataView(buf).setUint32(4, counter, false);
  const ck = await crypto.subtle.importKey('raw', key, { name: 'HMAC', hash: 'SHA-1' }, false, ['sign']);
  const sig = new Uint8Array(await crypto.subtle.sign('HMAC', ck, buf));
  const off = sig[19] & 0xf;
  const code = (
    ((sig[off]     & 0x7f) << 24) |
    ((sig[off + 1] & 0xff) << 16) |
    ((sig[off + 2] & 0xff) <<  8) |
    ( sig[off + 3] & 0xff)
  ) % 1_000_000;
  return String(code).padStart(6, '0');
}

function totpSecondsLeft() { return 30 - (Math.floor(Date.now() / 1000) % 30); }

// Active TOTP timers for options vault list
let _optTotpTimers = []; // [{secret, codeEl, ringEl, secsEl, lastCode}]
let _optTotpInterval = null;

function _startOptTotp() {
  clearInterval(_optTotpInterval); // stop any existing interval without clearing the timer list
  _optTotpInterval = null;
  if (!_optTotpTimers.length) return;
  const R = 12; // svg circle radius — must match buildOptVaultItem
  const CIRC = 2 * Math.PI * R;
  async function tick() {
    const secs = totpSecondsLeft();
    for (const t of _optTotpTimers) {
      // Refresh code at period boundary or on first tick
      if (secs === 30 || !t.lastCode) {
        try { t.lastCode = await generateTOTP(t.secret); } catch { t.lastCode = '------'; }
      }
      if (t.codeEl && t.lastCode) {
        const c = t.lastCode;
        t.codeEl.textContent = c.slice(0, 3) + ' ' + c.slice(3);
      }
      if (t.ringEl) {
        const frac = secs / 30;
        t.ringEl.style.strokeDashoffset = CIRC * (1 - frac);
        t.ringEl.style.stroke = secs <= 5 ? 'var(--danger)' : secs <= 10 ? '#F59E0B' : 'var(--accent)';
      }
      if (t.secsEl) { t.secsEl.textContent = secs; t.secsEl.style.color = secs <= 5 ? 'var(--danger)' : secs <= 10 ? '#F59E0B' : ''; }
    }
  }
  tick();
  _optTotpInterval = setInterval(tick, 1000);
}

function _stopOptTotp() {
  clearInterval(_optTotpInterval);
  _optTotpInterval = null;
  _optTotpTimers = [];
}

// =========================================================================
//  VAULT MANAGEMENT
// =========================================================================

function optSend(msg) {
  return new Promise((res, rej) =>
    chrome.runtime.sendMessage(msg, (r) => {
      if (chrome.runtime.lastError) return rej(new Error(chrome.runtime.lastError.message));
      if (r?.error) return rej(new Error(r.error));
      res(r);
    })
  );
}

// ── Icon helpers ──────────────────────────────────────────────────────────

const OPT_GRAD = [
  ['#407cca','#5a9be0'], ['#EC4899','#8B5CF6'], ['#F59E0B','#FB923C'],
  ['#14B8A6','#06B6D4'], ['#5a9be0','#6366F1'], ['#F97316','#EF4444'],
  ['#22D3EE','#5a9be0'], ['#84CC16','#14B8A6'],
];
function optGrad(label) {
  const [a, b] = OPT_GRAD[(label?.charCodeAt(0) ?? 0) % OPT_GRAD.length];
  return `linear-gradient(135deg, ${a}, ${b})`;
}
function buildOptIcon(s) {
  const d = document.createElement('div');
  d.className = 'opt-item-icon';
  const sz = 22;
  if (s.iconUri && !['creditcardlogo.png','addresslogo.png'].includes(s.iconUri)) {
    const img = document.createElement('img');
    img.src = s.iconUri; img.alt = '';
    img.onerror = () => { img.remove(); d.style.background = optGrad(s.label); d.textContent = (s.label?.[0] ?? '?').toUpperCase(); };
    d.appendChild(img);
  } else if (s.type === 'CreditCard') {
    d.style.background = 'linear-gradient(135deg,#F59E0B,#D97706)';
    d.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="${sz}" height="${sz}"><rect x="1" y="4" width="22" height="16" rx="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>`;
  } else if (s.type === 'Authenticator') {
    d.style.background = 'linear-gradient(135deg,#22D3EE,#5a9be0)';
    d.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="${sz}" height="${sz}"><rect x="5" y="2" width="14" height="20" rx="2"/><path d="M9 7h6"/><path d="M9 11h4"/></svg>`;
  } else {
    d.style.background = optGrad(s.label);
    d.textContent = (s.label?.[0] ?? '?').toUpperCase();
  }
  return d;
}

// ── Item row builder ──────────────────────────────────────────────────────

function buildOptVaultItem(s) {
  const row = document.createElement('div');
  row.className = 'opt-vault-item';
  if (s.isBreached) row.classList.add('is-breach');
  if (s.type === 'Authenticator') row.classList.add('has-totp');

  // Normalise display label — fall back to hostname or username when blank
  const displayLabel = s.label || (() => {
    try { return new URL(s.url || '').hostname || s.url || s.username || '(no label)'; }
    catch { return s.url || s.username || '(no label)'; }
  })();
  // Use a copy with normalised label so icon gradient and text are consistent
  const sDisplay = { ...s, label: displayLabel };

  row.appendChild(buildOptIcon(sDisplay));

const body = document.createElement('div');
  body.className = 'opt-item-body';

  const labelRow = document.createElement('div');
  labelRow.className = 'opt-item-label';
  labelRow.textContent = displayLabel;
  if (s.isFavorite) { const star = document.createElement('span'); star.className = 'opt-item-fav'; star.textContent = '★'; labelRow.appendChild(star); }
  if (s.isBreached) { const b = document.createElement('span'); b.className = 'opt-item-breach-badge'; b.textContent = 'BREACH'; labelRow.appendChild(b); }
  body.appendChild(labelRow);

  const sub = document.createElement('div');
  sub.className = 'opt-item-sub';
  sub.textContent = s.username || s.url || '';
  body.appendChild(sub);

  // ── Inline live TOTP for Authenticator items ──────────────────────────
  if (s.type === 'Authenticator') {
    const rawSecret = s.totpSecret;
    const secret = parseTotpSecret(rawSecret);
    if (secret) {
      const R = 12, CIRC = +(2 * Math.PI * R).toFixed(2);
      // compute correct initial offset immediately so ring is visible before first tick
      const initSecs = totpSecondsLeft();
      const initOffset = +(CIRC * (1 - initSecs / 30)).toFixed(2);
      const initColor = initSecs <= 5 ? 'var(--danger)' : initSecs <= 10 ? '#F59E0B' : 'var(--accent)';
      const totpWrap = document.createElement('div');
      totpWrap.className = 'opt-totp-inline';

      const codeEl = document.createElement('span');
      codeEl.className = 'opt-totp-code';
      codeEl.textContent = '··· ···';

      const timerWrap = document.createElement('div');
      timerWrap.className = 'opt-totp-timer';
      timerWrap.innerHTML = `
        <div class="opt-totp-ring-wrap">
          <svg viewBox="0 0 30 30" width="30" height="30">
            <circle class="opt-totp-ring-bg" cx="15" cy="15" r="${R}"/>
            <circle class="opt-totp-ring-fill" cx="15" cy="15" r="${R}"
              stroke-dasharray="${CIRC}" stroke-dashoffset="${initOffset}" style="stroke:${initColor}"/>
          </svg>
        </div>
        <span class="opt-totp-secs" style="color:${initSecs<=5?'var(--danger)':initSecs<=10?'#F59E0B':''}">${initSecs}</span>`;

      const ringEl = timerWrap.querySelector('.opt-totp-ring-fill');
      const secsEl = timerWrap.querySelector('.opt-totp-secs');

      const copyBtn = document.createElement('button');
      copyBtn.className = 'opt-totp-copy-btn';
      copyBtn.textContent = 'Copy';
      copyBtn.addEventListener('click', async (e) => {
        e.stopPropagation();
        try {
          const code = await generateTOTP(secret);
          await navigator.clipboard.writeText(code);
          copyBtn.textContent = 'Copied!';
          setTimeout(() => { copyBtn.textContent = 'Copy'; }, 1500);
          toast('Code copied', 'success');
        } catch { toast('Copy failed', 'error'); }
      });

      totpWrap.appendChild(codeEl);
      totpWrap.appendChild(timerWrap);
      totpWrap.appendChild(copyBtn);
      body.appendChild(totpWrap);

      // Register with the TOTP timer manager
      _optTotpTimers.push({ secret, codeEl, ringEl, secsEl, lastCode: null });
    } else {
      // Secret missing — show a set-up hint
      const hint = document.createElement('div');
      hint.className = 'opt-totp-no-secret';
      hint.textContent = 'No secret key — edit to add one';
      body.appendChild(hint);
    }
  }

  row.appendChild(body);

  const badge = document.createElement('span');
  badge.className = 'opt-item-type-badge';
  badge.textContent = s.type === 'CreditCard' ? 'Card' : s.type === 'Authenticator' ? '2FA' : 'Login';
  row.appendChild(badge);

  const actions = document.createElement('div');
  actions.className = 'opt-item-actions';

  const editBtn = document.createElement('button');
  editBtn.className = 'opt-action-micro'; editBtn.title = 'Edit';
  editBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>`;
  editBtn.addEventListener('click', (e) => { e.stopPropagation(); openOptEditItem(s); });

  const delBtn = document.createElement('button');
  delBtn.className = 'opt-action-micro opt-action-micro-danger'; delBtn.title = 'Delete';
  delBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4h6v2"/></svg>`;
  delBtn.addEventListener('click', async (e) => {
    e.stopPropagation();
    if (!confirm(`Delete "${s.label}"? This cannot be undone.`)) return;
    try {
      await optSend({ type: 'DELETE_ITEM', credentialId: s.id });
      _optAllItems = _optAllItems.filter(x => x.id !== s.id);
      optApplyFilter();
      toast('Deleted', 'success');
    } catch (ex) { toast(ex.message, 'error'); }
  });

  actions.appendChild(editBtn);
  actions.appendChild(delBtn);
  row.appendChild(actions);
  row.addEventListener('click', () => openOptEditItem(s));
  return row;
}

// ── List rendering ────────────────────────────────────────────────────────

function renderOptVaultList(items) {
  const container = $('opt-vault-list');
  if (!container) return;
  _stopOptTotp(); // clear any existing timers before rebuilding
  container.innerHTML = '';
  if (!items || !items.length) {
    container.innerHTML = `<div class="opt-list-empty"><div class="opt-list-empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg></div><span>No items found</span></div>`;
    return;
  }
  items.forEach((s, i) => {
    const el = buildOptVaultItem(s);
    el.style.animationDelay = `${Math.min(i * 28, 300)}ms`;
    container.appendChild(el);
  });
  _startOptTotp(); // kick off live TOTP updates for any Authenticator cards
}

// ── State & filter ────────────────────────────────────────────────────────

let _optAllItems     = [];
let _optActiveFilter = 'all';
let _optActiveSort   = 'az';
let _optEditingItem  = null;
let _optAddType      = 'Login';

function optGetFiltered() {
  const q = ($('vault-search')?.value ?? '').toLowerCase().trim();
  let items = [..._optAllItems];
  if (_optActiveFilter === 'Favorites') { items = items.filter(s => s.isFavorite); }
  else if (_optActiveFilter !== 'all') { items = items.filter(s => s.type === _optActiveFilter); }
  if (q) { items = items.filter(s => (s.label ?? '').toLowerCase().includes(q) || (s.username ?? '').toLowerCase().includes(q) || (s.url ?? '').toLowerCase().includes(q)); }
  if (_optActiveSort === 'az') items.sort((a, b) => (a.label ?? '').localeCompare(b.label ?? ''));
  else if (_optActiveSort === 'za') items.sort((a, b) => (b.label ?? '').localeCompare(a.label ?? ''));
  return items;
}

function optApplyFilter() {
  const filtered = optGetFiltered();
  const countEl = $('vault-item-count');
  if (countEl) countEl.textContent = `${filtered.length} item${filtered.length !== 1 ? 's' : ''}`;
  renderOptVaultList(filtered);
}

async function loadVault() {
  const container = $('opt-vault-list');
  if (!container) return;
  container.innerHTML = `<div class="opt-list-loading"><div class="opt-spinner"></div></div>`;
  try {
    const r = await optSend({ type: 'GET_ALL_ITEMS' });
    _optAllItems = r?.suggestions ?? [];
    optApplyFilter();
  } catch (e) {
    container.innerHTML = `<div class="opt-list-empty"><div class="opt-list-empty-icon opt-list-empty-icon--error"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><circle cx="12" cy="12" r="10"/><line x1="12" y1="8" x2="12" y2="12"/><line x1="12" y1="16" x2="12.01" y2="16"/></svg></div><span>${e.message}</span></div>`;
  }
}

// ── Page switching ────────────────────────────────────────────────────────

function showOptPage(pageId) {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  const pg = $(`page-${pageId}`);
  if (pg) pg.classList.add('active');
  if (pageId !== 'vault') _stopOptTotp();
}

function returnToVault() {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.getElementById('page-vault').classList.add('active');
  // Restore the active nav button (last vault sub-page or default to logins)
  const targetPage = _lastVaultNavPage || 'logins';
  document.querySelectorAll('.snav-btn').forEach(b => {
    b.classList.toggle('active', b.dataset.page === targetPage);
  });
  // Sync active filter chip  
  const btn = document.querySelector(`.snav-btn[data-page="${targetPage}"]`);
  if (btn?.dataset.vaultFilter) {
    _optActiveFilter = btn.dataset.vaultFilter;
    document.querySelectorAll('#page-vault .opt-chip[data-filter]').forEach(c => {
      c.classList.toggle('active', c.dataset.filter === _optActiveFilter);
    });
  }
  loadVault();
}

// ── Add / Edit ────────────────────────────────────────────────────────────

const OAI_FIELDS = ['oai-label','oai-url','oai-username','oai-password','oai-totp','oai-notes',
  'oai-card-label','oai-card-name','oai-card-number','oai-card-expiry','oai-card-cvv','oai-card-notes',
  'oai-auth-issuer','oai-auth-username','oai-auth-secret'];

function selectOptType(type) {
  _optAddType = type;
  document.querySelectorAll('.opt-type-btn').forEach(b => b.classList.toggle('active', b.dataset.itype === type));
  ['Login','CreditCard','Authenticator'].forEach(t => {
    const el = $(`oai-fields-${t}`);
    if (el) el.hidden = (t !== type);
  });
  const labelMap = { Login:'Save Login', CreditCard:'Save Card', Authenticator:'Save 2FA' };
  const lbl = $('btn-opt-save-label');
  if (lbl && !_optEditingItem) lbl.textContent = labelMap[type] ?? 'Save Item';
}

function openOptAddItem() {
  _optEditingItem = null;
  OAI_FIELDS.forEach(id => { const el = $(id); if (el) el.value = ''; });
  const fav = $('oai-favorite'); if (fav) fav.checked = false;
  const favA = $('oai-auth-favorite'); if (favA) favA.checked = false;
  const err = $('oai-err'); if (err) err.hidden = true;
  const sw = $('oai-strength-wrap'); if (sw) sw.hidden = true;
  const picker = $('opt-type-picker'); if (picker) picker.classList.remove('picker-hidden');
  selectOptType('Login');
  $('add-item-page-title').textContent = 'New Item';
  $('btn-opt-save-label').textContent  = 'Save Login';
  showOptPage('add-item');
}

function openOptEditItem(s) {
  _optEditingItem = s;
  OAI_FIELDS.forEach(id => { const el = $(id); if (el) el.value = ''; });
  const err = $('oai-err'); if (err) err.hidden = true;
  const sw = $('oai-strength-wrap'); if (sw) sw.hidden = true;
  const picker = $('opt-type-picker'); if (picker) picker.classList.add('picker-hidden');
  const titleMap = { Login:'Edit Login', CreditCard:'Edit Card', Authenticator:'Edit 2FA' };
  const saveMap  = { Login:'Update Login', CreditCard:'Update Card', Authenticator:'Update 2FA' };
  $('add-item-page-title').textContent = titleMap[s.type] ?? 'Edit Item';
  selectOptType(s.type);
  $('btn-opt-save-label').textContent = saveMap[s.type] ?? 'Update';

  if (s.type === 'Login') {
    [['oai-label',s.label],['oai-url',s.url],['oai-username',s.username],['oai-totp',s.totpSecret],['oai-notes',s.notes]].forEach(([id,val]) => { const el=$(id); if(el) el.value=val??''; });
    const fav=$('oai-favorite'); if(fav) fav.checked=s.isFavorite??false;
  } else if (s.type === 'CreditCard') {
    [['oai-card-label',s.label],['oai-card-name',s.cardholderName],['oai-card-number',s.cardNumber],['oai-card-expiry',s.cardExpiry]].forEach(([id,val]) => { const el=$(id); if(el) el.value=val??''; });
  } else if (s.type === 'Authenticator') {
    [['oai-auth-issuer',s.label],['oai-auth-username',s.username]].forEach(([id,val]) => { const el=$(id); if(el) el.value=val??''; });
    const t=$('oai-auth-type'); if(t) t.value=s.authType??'TOTP';
    const d=$('oai-auth-digits'); if(d) d.value=String(s.digits??6);
    const p=$('oai-auth-period'); if(p) p.value=String(s.period??30);
    const a=$('oai-auth-algorithm'); if(a) a.value=s.algorithm??'SHA1';
    const f=$('oai-auth-favorite'); if(f) f.checked=s.isFavorite??false;
  }
  showOptPage('add-item');
}

// ── Password strength ─────────────────────────────────────────────────────

function optScorePassword(pw) {
  if (!pw) return { score:0, label:'None', color:'#64748b' };
  let s = 0, len = pw.length;
  s += len<6?0:len<8?8:len<10?16:len<12?24:len<14?32:len<20?38:40;
  if(/[A-Z]/.test(pw)) s+=10; if(/[a-z]/.test(pw)) s+=10;
  if(/[0-9]/.test(pw)) s+=10; if(/[^A-Za-z0-9]/.test(pw)) s+=10;
  s += Math.round(new Set(pw).size/len*20);
  s = Math.max(0,Math.min(100,s));
  const label = s<20?'Very Weak':s<40?'Weak':s<60?'Fair':s<80?'Strong':'Very Strong';
  const color = s<20?'#EF4444':s<40?'#F97316':s<60?'#F59E0B':s<80?'#10B981':'#8B5CF6';
  return {score:s,label,color};
}

// ── Save item ─────────────────────────────────────────────────────────────

async function saveOptItem() {
  const errEl = $('oai-err');
  if (errEl) errEl.hidden = true;
  const btn = $('btn-opt-save-item');
  if (btn) btn.disabled = true;
  try {
    if (_optEditingItem) {
      const id = _optEditingItem.id;
      if (_optAddType === 'Login') {
        await optSend({ type:'UPDATE_LOGIN', id, label:$('oai-label')?.value.trim(), url:$('oai-url')?.value.trim(), username:$('oai-username')?.value.trim(), password:$('oai-password')?.value||undefined, totpSecret:$('oai-totp')?.value.trim()||undefined, notes:$('oai-notes')?.value, isFavorite:$('oai-favorite')?.checked??false });
      } else if (_optAddType === 'CreditCard') {
        await optSend({ type:'UPDATE_CARD', id, label:$('oai-card-label')?.value.trim(), cardholderName:$('oai-card-name')?.value.trim(), number:$('oai-card-number')?.value.trim(), expiry:$('oai-card-expiry')?.value.trim(), cvv:$('oai-card-cvv')?.value||undefined });
      } else if (_optAddType === 'Authenticator') {
        const issuer = $('oai-auth-issuer')?.value.trim(); if (!issuer) throw new Error('Issuer is required.');
        await optSend({ type:'UPDATE_AUTHENTICATOR', id, label:issuer, username:$('oai-auth-username')?.value.trim(), totpSecret:$('oai-auth-secret')?.value.trim()||undefined, authType:$('oai-auth-type')?.value, digits:+($('oai-auth-digits')?.value??6), period:+($('oai-auth-period')?.value??30), algorithm:$('oai-auth-algorithm')?.value, isFavorite:$('oai-auth-favorite')?.checked??false });
      }
      toast('Updated ✓', 'success');
    } else {
      if (_optAddType === 'Login') {
        const url=$('oai-url')?.value.trim(), password=$('oai-password')?.value;
        if (!url || !password) throw new Error('URL and password are required.');
        await optSend({ type:'SAVE_LOGIN', url, label:$('oai-label')?.value.trim(), username:$('oai-username')?.value.trim(), password, totpSecret:$('oai-totp')?.value.trim()||undefined, notes:$('oai-notes')?.value, isFavorite:$('oai-favorite')?.checked??false });
      } else if (_optAddType === 'CreditCard') {
        const number=$('oai-card-number')?.value.trim(); if (!number) throw new Error('Card number is required.');
        await optSend({ type:'SAVE_CARD', label:$('oai-card-label')?.value.trim(), cardholderName:$('oai-card-name')?.value.trim(), number, expiry:$('oai-card-expiry')?.value.trim(), cvv:$('oai-card-cvv')?.value });
      } else if (_optAddType === 'Authenticator') {
        const issuer=$('oai-auth-issuer')?.value.trim(), secret=$('oai-auth-secret')?.value.trim();
        if (!issuer) throw new Error('Issuer is required.');
        if (!secret) throw new Error('Secret key is required.');
        await optSend({ type:'SAVE_AUTHENTICATOR', label:issuer, username:$('oai-auth-username')?.value.trim(), totpSecret:secret, authType:$('oai-auth-type')?.value, digits:+($('oai-auth-digits')?.value??6), period:+($('oai-auth-period')?.value??30), algorithm:$('oai-auth-algorithm')?.value, isFavorite:$('oai-auth-favorite')?.checked??false });
      }
      toast('Saved ✓', 'success');
    }
    returnToVault();
    loadVault();
  } catch (e) {
    // Show inline error on the form AND a toast so it's visible from any entry point
    if (errEl) { errEl.textContent = e.message; errEl.hidden = false; }
    toast(e.message, 'error');
  } finally {
    if (btn) btn.disabled = false;
  }
}

// ── Event wiring ──────────────────────────────────────────────────────────

// Vault nav → load items (handled by main nav handler above)

// Search
$('vault-search')?.addEventListener('input', optApplyFilter);

// Sort
$('vault-sort-opt')?.addEventListener('change', (e) => { _optActiveSort = e.target.value; optApplyFilter(); });

// Filter chips (vault page only)
document.querySelectorAll('#page-vault .opt-chip[data-filter]').forEach(chip => {
  chip.addEventListener('click', () => {
    document.querySelectorAll('#page-vault .opt-chip[data-filter]').forEach(c => c.classList.remove('active'));
    chip.classList.add('active');
    _optActiveFilter = chip.dataset.filter;
    optApplyFilter();
  });
});

// Add item button
$('btn-vault-add')?.addEventListener('click', openOptAddItem);

// Type picker
document.querySelectorAll('.opt-type-btn').forEach(btn => {
  btn.addEventListener('click', () => selectOptType(btn.dataset.itype));
});

// Back / Cancel from add-item page
$('btn-add-item-back')?.addEventListener('click', returnToVault);
$('btn-opt-cancel-item')?.addEventListener('click', returnToVault);

// Save
$('btn-opt-save-item')?.addEventListener('click', saveOptItem);

// Eye buttons
document.querySelectorAll('.opt-eye-btn').forEach(btn => {
  btn.addEventListener('click', () => {
    const el = $(btn.dataset.for);
    if (el) el.type = el.type === 'password' ? 'text' : 'password';
  });
});

// Password strength
$('oai-password')?.addEventListener('input', (e) => {
  const pw = e.target.value;
  const fill = $('oai-strength-fill'), lbl = $('oai-strength-label'), wrap = $('oai-strength-wrap');
  if (!pw) { if (wrap) wrap.hidden = true; return; }
  const { score, label, color } = optScorePassword(pw);
  if (fill) { fill.style.width = score + '%'; fill.style.background = color; }
  if (lbl)  { lbl.textContent = label; lbl.style.color = color; }
  if (wrap)  wrap.hidden = false;
});

// =========================================================================
//  DASHBOARD
// =========================================================================

async function loadDashboard() {
  try {
    const [vaultResp, siResp] = await Promise.all([
      optSend({ type: 'GET_ALL_ITEMS' }).catch(() => ({ suggestions: [] })),
      optSend({ type: 'GET_SECURE_ITEMS' }).catch(() => ({ items: [] })),
    ]);
    const vault = vaultResp.suggestions ?? [];
    const si    = siResp.items ?? [];

    const logins  = vault.filter(i => i.type === 'Login');
    const auths   = vault.filter(i => i.type === 'Authenticator');
    const cards   = vault.filter(i => i.type === 'CreditCard');
    const breach  = vault.filter(i => i.isBreached);
    const weak    = logins.filter(i => i.passwordStrengthScore != null && i.passwordStrengthScore < 40);
    const with2fa = logins.filter(i => i.totpSecret);
    const total   = vault.length + si.length;

    const set = (id, val) => { const el = $(id); if (el) el.textContent = val; };
    set('dash-stat-logins',       logins.length);
    set('dash-stat-auths',        auths.length);
    set('dash-stat-cards',        cards.length);
    set('dash-stat-secure',       si.length);
    set('dash-health-breach-val', breach.length);
    set('dash-health-weak-val',   weak.length);
    set('dash-health-2fa-val',    with2fa.length);
    set('dash-health-total-val',  total);

    // Colour breach count red if any
    const bv = $('dash-health-breach-val');
    if (bv) bv.style.color = breach.length > 0 ? 'var(--danger)' : '';
    const wv = $('dash-health-weak-val');
    if (wv) wv.style.color = weak.length > 0 ? '#F59E0B' : '';

    // Cache for AI insights
    _dashVaultCache    = vault;
  } catch { /* vault may be locked */ }
}

// Quick-add buttons on dashboard
$('dash-add-login')?.addEventListener('click',  () => { openOptAddItem(); selectOptType('Login'); });
$('dash-add-auth')?.addEventListener('click',   () => { openOptAddItem(); selectOptType('Authenticator'); });
$('dash-add-card')?.addEventListener('click',   () => { openOptAddItem(); selectOptType('CreditCard'); });
$('dash-add-secure')?.addEventListener('click', openSiAddItem);

// Stat card click → navigate to that section
$('dash-card-logins')?.addEventListener('click', () => {
  document.querySelector('.snav-btn[data-vault-filter="Login"]')?.click();
});
$('dash-card-auths')?.addEventListener('click', () => {
  document.querySelector('.snav-btn[data-vault-filter="Authenticator"]')?.click();
});
$('dash-card-cards')?.addEventListener('click', () => {
  document.querySelector('.snav-btn[data-vault-filter="CreditCard"]')?.click();
});
$('dash-card-secure')?.addEventListener('click', () => {
  document.querySelector('.snav-btn[data-page="secure-items"]')?.click();
});

// keyboard support for stat cards
document.querySelectorAll('.dash-stat-card').forEach(card => {
  card.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); card.click(); } });
});

// =========================================================================
//  AI SECURITY INSIGHTS
// =========================================================================

let _dashVaultCache = [];

function optScorePasswordFast(pw) {
  if (!pw || pw.length < 4)  return 10;
  if (pw.length < 8)         return 30;
  if (pw.length < 12)        return 50;
  let s = pw.length >= 16 ? 70 : 60;
  if (/[A-Z]/.test(pw)) s += 8;
  if (/[a-z]/.test(pw)) s += 8;
  if (/[0-9]/.test(pw)) s += 8;
  if (/[^A-Za-z0-9]/.test(pw)) s += 8;
  return Math.min(s, 100);
}

function aiMakeRow(label, sub, iconHtml) {
  const row = document.createElement('div');
  row.className = 'ai-result-row';
  const icon = document.createElement('div');
  icon.className = 'opt-item-icon';
  icon.style.background = 'var(--accent-glow)';
  icon.style.color = 'var(--accent)';
  icon.innerHTML = iconHtml;
  row.appendChild(icon);
  const info = document.createElement('div');
  info.innerHTML = `<div class="ai-result-label">${label}</div><div class="ai-result-sub">${sub}</div>`;
  row.appendChild(info);
  return row;
}

async function runAiScan() {
  const btn = $('btn-ai-scan');
  if (btn) { btn.disabled = true; btn.textContent = 'Scanning…'; }

  try {
    const r = await optSend({ type: 'GET_ALL_ITEMS' }).catch(() => ({ suggestions: [] }));
    const vault = r.suggestions ?? [];
    _dashVaultCache = vault;

    const logins = vault.filter(i => i.type === 'Login');

    // Breached
    const breached = vault.filter(i => i.isBreached);
    const breachList = $('ai-breach-list');
    const breachCount = $('ai-breach-count');
    if (breachList) {
      breachList.innerHTML = '';
      if (!breached.length) {
        breachList.innerHTML = '<div class="ai-empty ai-empty-ok">&#10003; No breached accounts found!</div>';
      } else {
        breached.forEach(i => breachList.appendChild(
          aiMakeRow(i.label || i.url, i.username || '', '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="16" height="16"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>')
        ));
      }
    }
    if (breachCount) breachCount.textContent = breached.length;

    // Weak passwords
    const lockIcon = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="16" height="16"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>';
    const weakItems = logins.filter(i => {
      const score = i.passwordStrengthScore != null ? i.passwordStrengthScore : optScorePasswordFast(i.password || '');
      return score < 40;
    });
    const weakList = $('ai-weak-list');
    const weakCount = $('ai-weak-count');
    if (weakList) {
      weakList.innerHTML = '';
      if (!weakItems.length) {
        weakList.innerHTML = '<div class="ai-empty ai-empty-ok">&#10003; No weak passwords found!</div>';
      } else {
        weakItems.forEach(i => weakList.appendChild(aiMakeRow(i.label || i.url, i.username || 'Weak or short password', lockIcon)));
      }
    }
    if (weakCount) weakCount.textContent = weakItems.length;

    // Reused passwords
    const pwMap = {};
    logins.forEach(i => {
      if (i.password) { (pwMap[i.password] = pwMap[i.password] || []).push(i); }
    });
    const reused = Object.values(pwMap).filter(arr => arr.length > 1).flat();
    const reusedList = $('ai-reused-list');
    const reusedCount = $('ai-reused-count');
    if (reusedList) {
      reusedList.innerHTML = '';
      if (!reused.length) {
        reusedList.innerHTML = '<div class="ai-empty ai-empty-ok">&#10003; No reused passwords found!</div>';
      } else {
        reused.forEach(i => reusedList.appendChild(aiMakeRow(i.label || i.url, i.username || 'Password used on multiple sites', lockIcon)));
      }
    }
    if (reusedCount) reusedCount.textContent = reused.length;

    // Logins without 2FA
    const no2fa = logins.filter(i => !i.totpSecret);
    const no2faList = $('ai-no2fa-list');
    const no2faCount = $('ai-no2fa-count');
    if (no2faList) {
      no2faList.innerHTML = '';
      if (!no2fa.length) {
        no2faList.innerHTML = '<div class="ai-empty ai-empty-ok">&#10003; All logins have 2FA!</div>';
      } else {
        no2fa.forEach(i => no2faList.appendChild(aiMakeRow(i.label || i.url, i.username || '', lockIcon)));
      }
    }
    if (no2faCount) no2faCount.textContent = no2fa.length;

    const resultsEl = $('ai-insights-results');
    if (resultsEl) resultsEl.hidden = false;
  } catch (e) {
    toast('Scan failed: ' + e.message, 'error');
  } finally {
    if (btn) { btn.disabled = false; btn.innerHTML = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="14" height="14" style="margin-right:6px;"><circle cx="11" cy="11" r="8"/><line x1="21" y1="21" x2="16.65" y2="16.65"/></svg> Run Security Scan'; }
  }
}

$('btn-ai-scan')?.addEventListener('click', runAiScan);

// =========================================================================
//  INIT
// =========================================================================

loadTheme();
loadSettings();
checkStatus();
loadDashboard();

// =========================================================================
//  SECURE ITEMS
// =========================================================================

const SI_DOCUMENT_TYPES = new Set(['IdCard','Passport','DriversLicense','SocialSecurity','TaxNumber']);

const SI_TYPE_LABELS = {
  Identity: 'Identity', IdCard: 'ID Card', Passport: 'Passport',
  DriversLicense: "Driver's License", SocialSecurity: 'Social Security',
  TaxNumber: 'Tax Number', WiFi: 'Wi-Fi', Ssh: 'SSH', SecureNote: 'Secure Note',
};

function siSection(type) {
  if (type === 'Identity')   return 'Identity';
  if (type === 'WiFi')       return 'WiFi';
  if (type === 'Ssh')        return 'Ssh';
  if (type === 'SecureNote') return 'SecureNote';
  return 'Document'; // all document types
}

function siSummary(s) {
  switch (s.itemType) {
    case 'WiFi':          return s.ssid || '';
    case 'Ssh':           return s.username && s.host ? `${s.username}@${s.host}` : s.host || '';
    case 'SocialSecurity': return '••• - •• - ••••';
    case 'TaxNumber':     return 'Tax ID stored';
    case 'Identity':      return `${s.firstName || ''} ${s.lastName || ''}`.trim();
    case 'SecureNote':    return (s.noteContent || '').slice(0, 50) + ((s.noteContent||'').length > 50 ? '…' : '');
    default:              return s.fullName || '';
  }
}

function buildSiIcon(s) {
  const el = document.createElement('div');
  el.className = 'opt-item-icon';
  let cls = '';
  const svg = (path) => `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="20" height="20">${path}</svg>`;
  if (s.itemType === 'Identity') {
    cls = 'opt-si-icon-identity';
    el.innerHTML = svg('<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>');
  } else if (s.itemType === 'WiFi') {
    cls = 'opt-si-icon-wifi';
    el.innerHTML = svg('<path d="M5 12.55a11 11 0 0 1 14.08 0"/><path d="M1.42 9a16 16 0 0 1 21.16 0"/><path d="M8.53 16.11a6 6 0 0 1 6.95 0"/><line x1="12" y1="20" x2="12.01" y2="20"/>');
  } else if (s.itemType === 'Ssh') {
    cls = 'opt-si-icon-ssh';
    el.innerHTML = svg('<polyline points="4 17 10 11 4 5"/><line x1="12" y1="19" x2="20" y2="19"/>');
  } else if (s.itemType === 'SecureNote') {
    cls = 'opt-si-icon-note';
    el.innerHTML = svg('<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/>');
  } else {
    cls = 'opt-si-icon-document';
    el.innerHTML = svg('<rect x="3" y="4" width="18" height="16" rx="2"/><line x1="7" y1="9" x2="17" y2="9"/><line x1="7" y1="13" x2="13" y2="13"/>');
  }
  el.classList.add(cls);
  return el;
}

let _siAllItems   = [];
let _siFilter     = 'all';
let _siSort       = 'az';
let _siEditingItem = null;
let _siAddType    = 'Identity';

function siGetFiltered() {
  const q = ($('si-search')?.value ?? '').toLowerCase().trim();
  let items = _siAllItems.filter(s => {
    if (_siFilter === 'Favorites') return s.isFavorite;
    if (_siFilter === 'Documents') return SI_DOCUMENT_TYPES.has(s.itemType);
    if (_siFilter !== 'all')       return s.itemType === _siFilter;
    return true;
  });
  if (q) {
    items = items.filter(s =>
      (s.label || '').toLowerCase().includes(q) ||
      siSummary(s).toLowerCase().includes(q)
    );
  }
  items.sort((a, b) => _siSort === 'za'
    ? (b.label || '').localeCompare(a.label || '')
    : (a.label || '').localeCompare(b.label || '')
  );
  return items;
}

function siApplyFilter() {
  const list   = $('opt-si-list');
  const count  = $('si-item-count');
  const items  = siGetFiltered();
  if (count) count.textContent = `${items.length} item${items.length !== 1 ? 's' : ''}`;
  if (!list) return;
  list.innerHTML = '';
  if (!items.length) {
    const empty = document.createElement('div');
    empty.className = 'opt-vault-empty';
    empty.textContent = 'No secure items found.';
    list.appendChild(empty);
    return;
  }
  items.forEach(s => list.appendChild(buildOptSecureItemRow(s)));
}

async function loadSecureItems() {
  const list = $('opt-si-list');
  if (list) list.innerHTML = '<div class="opt-list-loading"><div class="opt-spinner"></div></div>';
  try {
    const r = await optSend({ type: 'GET_SECURE_ITEMS' });
    _siAllItems = r.items ?? [];
    siApplyFilter();
  } catch (e) {
    if (list) list.innerHTML = `<div class="opt-vault-empty" style="color:var(--danger)">Failed to load: ${e.message}</div>`;
  }
}

function buildOptSecureItemRow(s) {
  const row = document.createElement('div');
  row.className = 'opt-vault-item';
  row.appendChild(buildSiIcon(s));

  const body = document.createElement('div');
  body.className = 'opt-item-body';

  const labelRow = document.createElement('div');
  labelRow.className = 'opt-item-label';
  labelRow.textContent = s.label;
  if (s.isFavorite) {
    const star = document.createElement('span');
    star.className = 'opt-item-fav';
    star.textContent = '★';
    labelRow.appendChild(star);
  }
  body.appendChild(labelRow);

  const sub = document.createElement('div');
  sub.className = 'opt-item-sub';
  sub.textContent = siSummary(s);
  body.appendChild(sub);

  row.appendChild(body);

  const badge = document.createElement('span');
  badge.className = 'opt-item-type-badge';
  badge.textContent = SI_TYPE_LABELS[s.itemType] ?? s.itemType;
  row.appendChild(badge);

  const actions = document.createElement('div');
  actions.className = 'opt-item-actions';

  const editBtn = document.createElement('button');
  editBtn.className = 'opt-action-micro'; editBtn.title = 'Edit';
  editBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/></svg>`;
  editBtn.addEventListener('click', (e) => { e.stopPropagation(); openSiEditItem(s); });
  actions.appendChild(editBtn);

  const delBtn = document.createElement('button');
  delBtn.className = 'opt-action-micro opt-action-micro-danger'; delBtn.title = 'Delete';
  delBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4h6v2"/></svg>`;
  delBtn.addEventListener('click', async (e) => {
    e.stopPropagation();
    if (!confirm(`Delete "${s.label}"? This cannot be undone.`)) return;
    try {
      await optSend({ type: 'DELETE_SECURE_ITEM', id: s.id });
      await loadSecureItems();
      toast('Deleted', 'success');
    } catch (err) {
      toast('Delete failed: ' + err.message, 'error');
    }
  });
  actions.appendChild(delBtn);

  row.appendChild(actions);
  row.addEventListener('click', () => openSiEditItem(s));
  return row;
}

const SI_ALL_FIELDS = [
  'si-id-label','si-id-first','si-id-last','si-id-email','si-id-phone','si-id-company',
  'si-id-addr1','si-id-addr2','si-id-city','si-id-state','si-id-country','si-id-postal',
  'si-doc-label','si-doc-fullname','si-doc-number','si-doc-dob','si-doc-nationality',
  'si-doc-issuing','si-doc-issued','si-doc-expiry',
  'si-wifi-label','si-wifi-ssid','si-wifi-password',
  'si-ssh-label','si-ssh-host','si-ssh-port','si-ssh-username','si-ssh-password',
  'si-ssh-privatekey','si-ssh-fingerprint',
  'si-note-label','si-note-content',
];

const SI_SECTIONS = ['Identity','Document','WiFi','Ssh','SecureNote'];

function selectSiType(type) {
  _siAddType = type;
  document.querySelectorAll('#opt-si-type-picker .opt-type-btn').forEach(b =>
    b.classList.toggle('active', b.dataset.sitype === type)
  );
  const section = siSection(type);
  SI_SECTIONS.forEach(s => {
    const el = $(`si-fields-${s}`);
    if (el) el.hidden = (s !== section);
  });
  const labelMap = {
    Identity: 'Save Identity', IdCard: 'Save ID Card', Passport: 'Save Passport',
    DriversLicense: "Save License", SocialSecurity: 'Save SSN', TaxNumber: 'Save Tax Number',
    WiFi: 'Save Wi-Fi', Ssh: 'Save SSH', SecureNote: 'Save Note',
  };
  const lbl = $('btn-si-save-label');
  if (lbl && !_siEditingItem) lbl.textContent = labelMap[type] ?? 'Save Item';
}

function returnToSecureItems() {
  showOptPage('secure-items');
  document.querySelectorAll('.snav-btn').forEach(b =>
    b.classList.toggle('active', b.dataset.page === 'secure-items')
  );
}

function openSiAddItem() {
  _siEditingItem = null;
  SI_ALL_FIELDS.forEach(id => { const el = $(id); if (el) { el.value !== undefined ? el.value = '' : null; } });
  // Reset checkboxes
  ['si-id-favorite','si-doc-favorite','si-wifi-favorite','si-ssh-favorite','si-note-favorite']
    .forEach(id => { const el = $(id); if (el) el.checked = false; });
  const err = $('si-err'); if (err) err.hidden = true;
  const picker = $('opt-si-type-picker'); if (picker) picker.classList.remove('picker-hidden');
  // Reset SSH port default
  const port = $('si-ssh-port'); if (port) port.value = '22';
  selectSiType('Identity');
  $('add-si-page-title').textContent = 'New Secure Item';
  $('btn-si-save-label').textContent  = 'Save Identity';
  showOptPage('add-secure-item');
}

function openSiEditItem(s) {
  _siEditingItem = s;
  SI_ALL_FIELDS.forEach(id => { const el = $(id); if (el) el.value = ''; });
  const err = $('si-err'); if (err) err.hidden = true;
  const picker = $('opt-si-type-picker'); if (picker) picker.classList.add('picker-hidden');

  const titleMap = {
    Identity: 'Edit Identity', IdCard: 'Edit ID Card', Passport: 'Edit Passport',
    DriversLicense: 'Edit License', SocialSecurity: 'Edit SSN', TaxNumber: 'Edit Tax Number',
    WiFi: 'Edit Wi-Fi', Ssh: 'Edit SSH', SecureNote: 'Edit Note',
  };
  const saveMap = {
    Identity: 'Update Identity', IdCard: 'Update ID Card', Passport: 'Update Passport',
    DriversLicense: 'Update License', SocialSecurity: 'Update SSN', TaxNumber: 'Update Tax Number',
    WiFi: 'Update Wi-Fi', Ssh: 'Update SSH', SecureNote: 'Update Note',
  };
  $('add-si-page-title').textContent  = titleMap[s.itemType] ?? 'Edit Item';
  $('btn-si-save-label').textContent  = saveMap[s.itemType]  ?? 'Update';
  selectSiType(s.itemType);

  if (s.itemType === 'Identity') {
    [['si-id-label',s.label],['si-id-first',s.firstName],['si-id-last',s.lastName],
     ['si-id-email',s.email],['si-id-phone',s.phone],['si-id-company',s.company],
     ['si-id-addr1',s.addressLine1],['si-id-addr2',s.addressLine2],
     ['si-id-city',s.city],['si-id-state',s.state],
     ['si-id-country',s.country],['si-id-postal',s.postalCode]].forEach(([id,v]) => { const el=$(id);if(el)el.value=v??''; });
    const fav=$('si-id-favorite'); if(fav) fav.checked=s.isFavorite??false;
  } else if (SI_DOCUMENT_TYPES.has(s.itemType)) {
    [['si-doc-label',s.label],['si-doc-fullname',s.fullName],['si-doc-number',s.number],
     ['si-doc-dob',s.dateOfBirth],['si-doc-nationality',s.nationality],
     ['si-doc-issuing',s.issuingCountry],['si-doc-issued',s.issuedDate],['si-doc-expiry',s.expiryDate]].forEach(([id,v]) => { const el=$(id);if(el)el.value=v??''; });
    const fav=$('si-doc-favorite'); if(fav) fav.checked=s.isFavorite??false;
  } else if (s.itemType === 'WiFi') {
    [['si-wifi-label',s.label],['si-wifi-ssid',s.ssid],['si-wifi-password',s.password]].forEach(([id,v]) => { const el=$(id);if(el)el.value=v??''; });
    const sec=$('si-wifi-security'); if(sec) sec.value=s.wifiSecurity??'WPA2';
    const fav=$('si-wifi-favorite'); if(fav) fav.checked=s.isFavorite??false;
  } else if (s.itemType === 'Ssh') {
    [['si-ssh-label',s.label],['si-ssh-host',s.host],['si-ssh-port',s.port||'22'],
     ['si-ssh-username',s.username],['si-ssh-password',s.sshPassword],
     ['si-ssh-privatekey',s.privateKey],['si-ssh-fingerprint',s.keyFingerprint]].forEach(([id,v]) => { const el=$(id);if(el)el.value=v??''; });
    const fav=$('si-ssh-favorite'); if(fav) fav.checked=s.isFavorite??false;
  } else if (s.itemType === 'SecureNote') {
    [['si-note-label',s.label],['si-note-content',s.noteContent]].forEach(([id,v]) => { const el=$(id);if(el)el.value=v??''; });
    const fav=$('si-note-favorite'); if(fav) fav.checked=s.isFavorite??false;
  }
  showOptPage('add-secure-item');
}

async function saveSiItem() {
  const errEl = $('si-err');
  if (errEl) errEl.hidden = true;
  const btn = $('btn-si-save');
  if (btn) btn.disabled = true;
  try {
    const type = _siAddType;
    const id = _siEditingItem?.id;
    let payload = { itemType: type };

    if (type === 'Identity') {
      const label = $('si-id-label')?.value.trim();
      if (!label) throw new Error('Label is required.');
      payload = { ...payload, label,
        firstName: $('si-id-first')?.value.trim(), lastName: $('si-id-last')?.value.trim(),
        email: $('si-id-email')?.value.trim(), phone: $('si-id-phone')?.value.trim(),
        company: $('si-id-company')?.value.trim(),
        addressLine1: $('si-id-addr1')?.value.trim(), addressLine2: $('si-id-addr2')?.value.trim(),
        city: $('si-id-city')?.value.trim(), state: $('si-id-state')?.value.trim(),
        country: $('si-id-country')?.value.trim(), postalCode: $('si-id-postal')?.value.trim(),
        isFavorite: $('si-id-favorite')?.checked ?? false,
      };
    } else if (SI_DOCUMENT_TYPES.has(type)) {
      const label = $('si-doc-label')?.value.trim();
      if (!label) throw new Error('Label is required.');
      payload = { ...payload, label,
        fullName: $('si-doc-fullname')?.value.trim(),
        number: $('si-doc-number')?.value,
        dateOfBirth: $('si-doc-dob')?.value,
        nationality: $('si-doc-nationality')?.value.trim(),
        issuingCountry: $('si-doc-issuing')?.value.trim(),
        issuedDate: $('si-doc-issued')?.value,
        expiryDate: $('si-doc-expiry')?.value,
        isFavorite: $('si-doc-favorite')?.checked ?? false,
      };
    } else if (type === 'WiFi') {
      const label = $('si-wifi-label')?.value.trim();
      const ssid  = $('si-wifi-ssid')?.value.trim();
      if (!label) throw new Error('Label is required.');
      if (!ssid)  throw new Error('Network name (SSID) is required.');
      payload = { ...payload, label, ssid,
        wifiSecurity: $('si-wifi-security')?.value,
        password: $('si-wifi-password')?.value,
        isFavorite: $('si-wifi-favorite')?.checked ?? false,
      };
    } else if (type === 'Ssh') {
      const label = $('si-ssh-label')?.value.trim();
      const host  = $('si-ssh-host')?.value.trim();
      if (!label) throw new Error('Label is required.');
      if (!host)  throw new Error('Host is required.');
      payload = { ...payload, label, host,
        port: $('si-ssh-port')?.value || '22',
        username: $('si-ssh-username')?.value.trim(),
        sshPassword: $('si-ssh-password')?.value,
        privateKey: $('si-ssh-privatekey')?.value,
        keyFingerprint: $('si-ssh-fingerprint')?.value.trim(),
        isFavorite: $('si-ssh-favorite')?.checked ?? false,
      };
    } else if (type === 'SecureNote') {
      const label   = $('si-note-label')?.value.trim();
      const content = $('si-note-content')?.value;
      if (!label) throw new Error('Title is required.');
      payload = { ...payload, label, noteContent: content,
        isFavorite: $('si-note-favorite')?.checked ?? false,
      };
    }

    if (_siEditingItem) {
      await optSend({ type: 'UPDATE_SECURE_ITEM', id, ...payload });
      toast('Updated ✓', 'success');
    } else {
      await optSend({ type: 'SAVE_SECURE_ITEM', ...payload });
      toast('Saved ✓', 'success');
    }
    returnToSecureItems();
    loadSecureItems();
  } catch (e) {
    if (errEl) { errEl.textContent = e.message; errEl.hidden = false; }
    toast(e.message, 'error');
  } finally {
    if (btn) btn.disabled = false;
  }
}

// ── Secure Items event wiring ──────────────────────────────────────────────

// Nav → load items (handled by main nav handler; back/cancel buttons below)

// Search
$('si-search')?.addEventListener('input', siApplyFilter);

// Sort
$('si-sort-opt')?.addEventListener('change', (e) => { _siSort = e.target.value; siApplyFilter(); });

// Filter chips
document.querySelectorAll('[data-si-filter]').forEach(chip => {
  chip.addEventListener('click', () => {
    _siFilter = chip.dataset.siFilter;
    document.querySelectorAll('[data-si-filter]').forEach(c => c.classList.toggle('active', c === chip));
    siApplyFilter();
  });
});

// Add button
$('btn-si-add')?.addEventListener('click', openSiAddItem);

// Type picker
document.querySelectorAll('#opt-si-type-picker .opt-type-btn').forEach(btn => {
  btn.addEventListener('click', () => selectSiType(btn.dataset.sitype));
});
