// ─────────────────────────────────────────────────────────────────────────────
// popup.js  —  Fortress popup controller  v1.1
// ─────────────────────────────────────────────────────────────────────────────
'use strict';

// =========================================================================
// THEME MANAGEMENT
// =========================================================================

function accentColor() {
  return getComputedStyle(document.documentElement).getPropertyValue('--accent').trim() || '#407cca';
}

async function applyTheme(themeVal) {
  const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
  const isDark = themeVal === 'dark' || (themeVal === 'system' && prefersDark);
  document.documentElement.setAttribute('data-theme', isDark ? 'dark' : 'light');

  // Sync header toggle icons
  const sunIcon  = $('icon-sun');
  const moonIcon = $('icon-moon');
  if (sunIcon && moonIcon) {
    sunIcon.hidden  =  isDark;
    moonIcon.hidden = !isDark;
  }

  // Sync theme pills in settings
  document.querySelectorAll('.theme-pill').forEach(p => {
    p.classList.toggle('active', p.dataset.themeVal === themeVal);
  });

  await chrome.storage.sync.set({ theme: themeVal }).catch(() => {});
}

async function loadTheme() {
  const { theme } = await chrome.storage.sync.get({ theme: 'system' }).catch(() => ({ theme: 'system' }));
  await applyTheme(theme);
}


// ── Utility ───────────────────────────────────────────────────────────────────
const $ = (id) => document.getElementById(id);
const show = (el) => { if (el) el.hidden = false; };
const hide = (el) => { if (el) el.hidden = true; };

function send(msg) {
  return new Promise((res, rej) =>
    chrome.runtime.sendMessage(msg, (r) => {
      if (chrome.runtime.lastError) return rej(new Error(chrome.runtime.lastError.message));
      if (r?.error) return rej(new Error(r.error));
      res(r);
    })
  );
}

function toast(msg, type = 'default') {
  const el = $('toast');
  const icon = type === 'success' ? '?' : type === 'error' ? '?' : '•';
  el.innerHTML = `<span style="color:${
    type === 'success' ? 'var(--success)' :
    type === 'error'   ? 'var(--danger)'  : 'var(--accent-light)'}">${icon}</span> ${msg}`;
  el.style.borderColor = type === 'success' ? 'rgba(16,185,129,0.4)'
    : type === 'error' ? 'rgba(239,68,68,0.4)'
    : 'rgba(64,124,202,0.3)';
  show(el);
  clearTimeout(el._tid);
  el._tid = setTimeout(() => hide(el), 2400);
}

function showScreen(id) {
  if (id !== 'screen-detail') {
    clearInterval(_totpInterval); _totpInterval = null;
  }
  ['screen-connecting','screen-setup','screen-lock','screen-main','screen-add','screen-detail','screen-settings'].forEach(s => {
    const el = $(s);
    if (el) el.hidden = (s !== id);
  });
}

function setLoading(btn, isLoading) {
  btn.disabled = isLoading;
  const lbl = btn.querySelector('span');
  const spin = btn.querySelector('.btn-spinner');
  if (lbl)  lbl.style.opacity = isLoading ? '0' : '1';
  if (spin) spin.hidden = !isLoading;
}

// ── TOTP helpers ──────────────────────────────────────────────────────────────
let _totpInterval = null;
let _rowTotpTimers = [];

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

// ── Favicon / icon helpers ────────────────────────────────────────────────────
const GRADIENT_COLORS = [
  ['#407cca','#5a9be0'],   // Fortress blue
  ['#EC4899','#8B5CF6'],   // pink-purple
  ['#F59E0B','#FB923C'],   // amber-orange
  ['#14B8A6','#06B6D4'],   // teal-cyan (Maui NotesTileIcon)
  ['#5a9be0','#6366F1'],   // blue-indigo
  ['#F97316','#EF4444'],   // orange-red
  ['#22D3EE','#5a9be0'],   // cyan-blue (Maui TwoFAStatIcon)
  ['#84CC16','#14B8A6'],   // lime-teal
];

function avatarGradient(label) {
  const i = (label?.charCodeAt(0) ?? 0) % GRADIENT_COLORS.length;
  const [a, b] = GRADIENT_COLORS[i];
  return `linear-gradient(135deg, ${a}, ${b})`;
}

function buildItemIcon(s, size = 34) {
  const div = document.createElement('div');
  div.className = 'item-icon';
  div.style.width  = `${size}px`;
  div.style.height = `${size}px`;
  if (s.iconUri && !['creditcardlogo.png','addresslogo.png'].includes(s.iconUri)) {
    const img = document.createElement('img');
    img.src = s.iconUri; img.alt = '';
    img.onerror = () => {
   img.remove();
      div.style.background = avatarGradient(s.label);
      div.textContent = (s.label?.[0] ?? '?').toUpperCase();
    };
    div.appendChild(img);
  } else if (s.type === 'CreditCard') {
    /* Maui CardsTileIcon amber */
    div.style.background = 'linear-gradient(135deg, #F59E0B, #D97706)';
    div.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="${size*0.5}" height="${size*0.5}"><rect x="1" y="4" width="22" height="16" rx="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>`;
  } else if (s.type === 'Identity') {
    /* Maui IdTileIcon purple */
    div.style.background = 'linear-gradient(135deg, #8B5CF6, #7C3AED)';
    div.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="${size*0.5}" height="${size*0.5}"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>`;
  } else if (s.type === 'Note') {
    /* Maui NotesTileIcon teal */
    div.style.background = 'linear-gradient(135deg, #14B8A6, #0F766E)';
    div.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="${size*0.5}" height="${size*0.5}"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><polyline points="14 2 14 8 20 8"/><line x1="16" y1="13" x2="8" y2="13"/><line x1="16" y1="17" x2="8" y2="17"/></svg>`;
  } else if (s.type === 'Authenticator') {
    /* Maui 2FA / TwoFAStatIcon cyan-blue */
    div.style.background = 'linear-gradient(135deg, #22D3EE, #5a9be0)';
    div.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="${size*0.5}" height="${size*0.5}"><rect x="5" y="2" width="14" height="20" rx="2"/><path d="M9 7h6"/><path d="M9 11h4"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>`;
  } else {
    div.style.background = avatarGradient(s.label);
    div.textContent = (s.label?.[0] ?? '?').toUpperCase();
  }
  return div;
}

// ── Item rendering ────────────────────────────────────────────────────────────
function buildVaultItem(s, { onFill, onCopy, onClick } = {}) {
  const row = document.createElement('div');
  row.className = 'vault-item';
  if (s.isBreached) row.classList.add('is-breach');

  row.appendChild(buildItemIcon(s));

const body = document.createElement('div');
  body.className = 'item-body';

  const labelRow = document.createElement('div');
  labelRow.className = 'item-label';
  labelRow.textContent = s.label;
  if (s.isFavorite) {
    const star = document.createElement('span');
    star.className = 'item-fav';
    star.textContent = '?';
    labelRow.appendChild(star);
  }
  if (s.isBreached) {
 const badge = document.createElement('span');
    badge.className = 'item-breach-badge';
    badge.textContent = 'BREACH';
    labelRow.appendChild(badge);
  }
  body.appendChild(labelRow);

  const sub = document.createElement('div');
  sub.className = 'item-sub';
  sub.textContent = s.username || s.type || '';
  body.appendChild(sub);

  // ??? TOTP inline display ???
  const hasTOTP = !!(s.totpSecret);
  const totpParsed = hasTOTP ? parseTotpSecret(s.totpSecret) : null;

  if (hasTOTP && totpParsed) {
    const totpRow = document.createElement('div');
    totpRow.className = 'item-totp-row';
    const digitsEl = document.createElement('div');
    digitsEl.className = 'item-totp-digits';
    digitsEl.textContent = '\u00B7\u00B7\u00B7 \u00B7\u00B7\u00B7';
    totpRow.appendChild(digitsEl);
    body.appendChild(totpRow);

    // Circular ring + dots menu (right side)
    const CIRC_R = 14, circ = +(2 * Math.PI * CIRC_R).toFixed(2);
    const initSecs = totpSecondsLeft();
    const initOffset = +(circ * (1 - initSecs / 30)).toFixed(2);
    const initColor = initSecs <= 5 ? 'danger' : initSecs <= 10 ? 'warn' : '';
    const totpRight = document.createElement('div');
    totpRight.className = 'item-totp-right';

    const ringWrap = document.createElement('div');
    ringWrap.className = 'totp-ring-wrap';
    ringWrap.innerHTML = `
      <svg viewBox="0 0 36 36" width="36" height="36" style="transform:rotate(-90deg)">
        <circle class="totp-ring-bg-circle" cx="18" cy="18" r="14"/>
        <circle class="totp-ring-arc${initColor ? ' totp-arc-' + initColor : ''}" cx="18" cy="18" r="14"
          stroke-dasharray="${circ}" stroke-dashoffset="${initOffset}"
          stroke-linecap="round"/>
      </svg>
      <div class="totp-ring-secs${initColor ? ' totp-secs-' + initColor : ''}">${initSecs}</div>`;
    const arc = ringWrap.querySelector('.totp-ring-arc');
    const secsEl = ringWrap.querySelector('.totp-ring-secs');

    const dotsBtn = document.createElement('button');
    dotsBtn.className = 'item-dots-btn';
    dotsBtn.title = 'More options';
    dotsBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="currentColor" width="14" height="14"><circle cx="12" cy="5" r="1.8"/><circle cx="12" cy="12" r="1.8"/><circle cx="12" cy="19" r="1.8"/></svg>`;

    totpRight.appendChild(ringWrap);
    totpRight.appendChild(dotsBtn);

    // Dots menu
    dotsBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      document.querySelectorAll('.item-dots-menu').forEach(m => m.remove());
      const menu = document.createElement('div');
      menu.className = 'item-dots-menu';

      const mkItem = (label, svgPath, danger = false) => {
        const it = document.createElement('div');
        it.className = 'item-dots-menu-item' + (danger ? ' danger' : '');
        it.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13">${svgPath}</svg>${label}`;
        return it;
      };

      const copyIt = mkItem('Copy Code', '<rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>');
      copyIt.addEventListener('click', async (ev) => {
        ev.stopPropagation(); menu.remove();
        try { const c = await generateTOTP(totpParsed); await navigator.clipboard.writeText(c); toast('Code copied', 'success'); }
        catch { toast('Copy failed', 'error'); }
      });

      const editIt = mkItem('Edit', '<path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>');
      editIt.addEventListener('click', (ev) => { ev.stopPropagation(); menu.remove(); openEditScreen(s); });

      const delIt = mkItem('Delete', '<polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4h6v2"/>', true);
      delIt.addEventListener('click', async (ev) => {
        ev.stopPropagation(); menu.remove();
        if (!confirm(`Delete "${s.label}"? This cannot be undone.`)) return;
        try { await send({ type: 'DELETE_ITEM', credentialId: s.id }); _allItems = _allItems.filter(x => x.id !== s.id); applyFilter(); updateVaultHero(); toast('Deleted', 'success'); }
        catch (ex) { toast(ex.message, 'error'); }
      });

      menu.appendChild(copyIt); menu.appendChild(editIt); menu.appendChild(delIt);

      // Append to body with fixed positioning so overflow:hidden on vault-item doesn't clip it
      document.body.appendChild(menu);
      const rect = dotsBtn.getBoundingClientRect();
      menu.style.position = 'fixed';
      menu.style.right = `${window.innerWidth - rect.right}px`;
      menu.style.top = `${rect.bottom + 5}px`;

      setTimeout(() => document.addEventListener('click', function closeMenu(ev) {
        if (!menu.contains(ev.target)) { menu.remove(); document.removeEventListener('click', closeMenu); }
      }), 0);
    });

    // Live refresh
    const refreshRow = async () => {
      const secs = totpSecondsLeft();
      arc.setAttribute('stroke-dashoffset', (circ * (1 - secs / 30)).toFixed(2));
      const isDanger = secs <= 5, isWarn = secs <= 10;
      arc.classList.toggle('totp-arc-danger', isDanger);
      arc.classList.toggle('totp-arc-warn', !isDanger && isWarn);
      arc.classList.toggle('totp-arc-ok', !isDanger && !isWarn);
      secsEl.textContent = secs;
      secsEl.classList.toggle('totp-secs-danger', isDanger);
      secsEl.classList.toggle('totp-secs-warn', !isDanger && isWarn);
      try {
        const c = await generateTOTP(totpParsed);
        digitsEl.textContent = c.slice(0, 3) + '\u2007' + c.slice(3);
      } catch { digitsEl.textContent = 'Error'; }
    };
    refreshRow();
    const timer = setInterval(refreshRow, 1000);
    _rowTotpTimers.push(timer);

    row.appendChild(body);
    row.appendChild(totpRight);
  } else {
    // Non-TOTP item: badge + hover action buttons
    const badge = document.createElement('span');
    badge.className = 'item-type-badge';
    badge.textContent = s.type === 'CreditCard' ? 'Card'
      : s.type === 'Identity' ? 'Identity'
      : s.type === 'Note'     ? 'Note'
      : s.type === 'Authenticator' ? '2FA' : 'Login';

    const actions = document.createElement('div');
    actions.className = 'item-actions';

    if (s.type !== 'Identity' && s.type !== 'Note' && s.type !== 'Authenticator' && onCopy) {
      const copyBtn = document.createElement('button');
      copyBtn.className = 'action-micro'; copyBtn.title = 'Copy password';
      copyBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="12" height="12"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;
      copyBtn.addEventListener('click', (e) => { e.stopPropagation(); onCopy(s); });
      actions.appendChild(copyBtn);
    }
    if (onFill && s.type === 'Login') {
      const fillBtn = document.createElement('button');
      fillBtn.className = 'action-micro'; fillBtn.title = 'Autofill';
      fillBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="12" height="12"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4z"/></svg>`;
      fillBtn.addEventListener('click', (e) => { e.stopPropagation(); onFill(s); });
      actions.appendChild(fillBtn);
    }
    const editMicro = document.createElement('button');
    editMicro.className = 'action-micro'; editMicro.title = 'Edit';
    editMicro.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="12" height="12"><path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"/>`;
    editMicro.addEventListener('click', (e) => { e.stopPropagation(); openEditScreen(s); });
    actions.appendChild(editMicro);

    const delMicro = document.createElement('button');
    delMicro.className = 'action-micro action-micro-danger'; delMicro.title = 'Delete';
    delMicro.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="12" height="12"><polyline points="3 6 5 6 21 6"/><path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6"/><path d="M14 11v6"/><path d="M9 6V4h6v2"/></svg>`;
    delMicro.addEventListener('click', async (e) => {
      e.stopPropagation();
      if (!confirm(`Delete "${s.label}"? This cannot be undone.`)) return;
      try { await send({ type: 'DELETE_ITEM', credentialId: s.id }); _allItems = _allItems.filter(x => x.id !== s.id); applyFilter(); updateVaultHero(); toast('Deleted', 'success'); }
      catch (ex) { toast(ex.message, 'error'); }
    });
    actions.appendChild(delMicro);

    row.appendChild(body);
    row.appendChild(badge);
    row.appendChild(actions);
  }

  row.addEventListener('click', () => {
    if (onClick) onClick(s);
    else if (onFill) onFill(s);
  });

  return row;
}

function renderList(container, items, handlers = {}) {
  _rowTotpTimers.forEach(t => clearInterval(t));
  _rowTotpTimers = [];
  container.innerHTML = '';
  if (!items || items.length === 0) {
    container.innerHTML = `<div class="list-empty"><span class="list-empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg></span><span>No items found</span></div>`;
    return;
  }
  items.forEach((s, i) => {
    const el = buildVaultItem(s, handlers);
    el.style.animationDelay = `${Math.min(i * 38, 320)}ms`;
    container.appendChild(el);
  });
}

// ── Password strength ─────────────────────────────────────────────────────────
function scorePassword(pw) {
  if (!pw) return { score: 0, label: 'None', color: '#64748b' };
  let s = 0;
  const len = pw.length;
  s += len < 6 ? 0 : len < 8 ? 8 : len < 10 ? 16 : len < 12 ? 24 : len < 14 ? 32 : len < 20 ? 38 : 40;
  if (/[A-Z]/.test(pw)) s += 10;
  if (/[a-z]/.test(pw)) s += 10;
  if (/[0-9]/.test(pw)) s += 10;
  if (/[^A-Za-z0-9]/.test(pw)) s += 10;
  const dist = new Set(pw).size / len;
  s += Math.round(dist * 20);
  s = Math.max(0, Math.min(100, s));
  const label = s < 20 ? 'Very Weak' : s < 40 ? 'Weak' : s < 60 ? 'Fair' : s < 80 ? 'Strong' : 'Very Strong';
  const color = s < 20 ? '#EF4444' : s < 40 ? '#F97316' : s < 60 ? '#F59E0B' : s < 80 ? '#10B981' : '#8B5CF6';
  return { score: s, label, color };
}

function updateStrength(pw, fillEl, labelEl, wrapEl) {
  if (pw.length === 0) { if (wrapEl) hide(wrapEl); return; }
  const { score, label, color } = scorePassword(pw);
  fillEl.style.width = score + '%';
  fillEl.style.background = color;
  labelEl.textContent = label;
  labelEl.style.color = color;
  if (wrapEl) show(wrapEl);
}

// ── Password generator ────────────────────────────────────────────────────────
const WORDLIST = [
  'amber','brave','cedar','delta','eagle','flame','grace','haven','ivory','jewel',
  'karma','lunar','maple','noble','ocean','pearl','quiet','raven','solar','titan',
  'ultra','vapor','witty','xenon','yacht','zonal','agent','bloom','chase','drift',
  'ember','forge','glide','haste','inlet','joust','knack','lance','mirth','nexus',
  'orbit','plush','quirk','ridge','stark','trove','unity','vivid','windy','axiom',
];

function generatePassword(opts = {}) {
  const { length = 20, upper = true, lower = true, digits = true, symbols = true, excludeAmbiguous = false } = opts;
  let chars = '';
  if (upper)   chars += excludeAmbiguous ? 'ABCDEFGHJKLMNPQRSTUVWXYZ' : 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
  if (lower)   chars += excludeAmbiguous ? 'abcdefghjkmnpqrstuvwxyz'  : 'abcdefghijklmnopqrstuvwxyz';
  if (digits)  chars += excludeAmbiguous ? '23456789' : '0123456789';
  if (symbols) chars += '!@#$%^&*()-_=+[]{}|;:,.<>?';
  if (!chars)  chars = 'abcdefghijklmnopqrstuvwxyz';
  const arr = new Uint32Array(length);
  crypto.getRandomValues(arr);
  return Array.from(arr, (v) => chars[v % chars.length]).join('');
}

function generatePassphrase(wordCount = 4, separator = '-', capitalize = true, includeNum = false) {
  const arr = new Uint32Array(wordCount);
  crypto.getRandomValues(arr);
  let words = Array.from(arr, (v) => {
    const w = WORDLIST[v % WORDLIST.length];
    return capitalize ? w[0].toUpperCase() + w.slice(1) : w;
  });
  if (includeNum) {
    const n = new Uint32Array(1);
    crypto.getRandomValues(n);
    words.push(String(n[0] % 100));
  }
  return words.join(separator);
}

function generatePin(length = 6) {
  const arr = new Uint32Array(length);
  crypto.getRandomValues(arr);
  return Array.from(arr, v => v % 10).join('');
}

function getGenOpts() {
  return {
    length: +$('gen-length').value,
    upper:   $('gen-upper').checked,
    lower:   $('gen-lower').checked,
    digits:  $('gen-digits').checked,
    symbols: $('gen-symbols').checked,
    excludeAmbiguous: $('gen-ambiguous').checked,
  };
}

let _genType = 'password';

function refreshGenerator() {
  let pw = '';
  if (_genType === 'password') {
    pw = generatePassword(getGenOpts());
  } else if (_genType === 'passphrase') {
    const wordCount = +$('gen-words').value;
    const sep = $('gen-sep').value;
    const cap = $('gen-pp-cap').checked;
    const num = $('gen-pp-num').checked;
 pw = generatePassphrase(wordCount, sep, cap, num);
  } else {
    pw = generatePin(+$('gen-pin-len').value);
  }
  $('gen-output').value = pw;
  const { score, label, color } = scorePassword(pw);
  $('gen-strength-fill').style.width = score + '%';
  $('gen-strength-fill').style.background = color;
  $('gen-strength-label').textContent = label;
  $('gen-strength-label').style.color = color;
}

// ── Main state ────────────────────────────────────────────────────────────────
let _allItems     = [];
let _activeFilter = 'all';
let _activeSort   = 'az';
let _activeView   = 'vault';
let _settings     = { autoLockSeconds: 300, inlineBtn: true, savePrompt: true, breachAlerts: true, genLength: 20 };
let _currentDetailItem = null;
let _editingItem = null;

// ── Fill + copy handlers ──────────────────────────────────────────────────────
async function handleFill(s) {
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab?.id) { toast('No active tab', 'error'); return; }
    const fill = await send({ type: 'FILL_CONFIRMED', credentialId: s.id });
    if (!fill?.success) { toast(fill?.errorMessage ?? 'Fill failed', 'error'); return; }
    await chrome.scripting.executeScript({
      target: { tabId: tab.id },
      func: (data) => {
      function setVal(el, v) {
 if (!el || !v) return;
          const desc = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
          desc?.set?.call(el, v);
          el.dispatchEvent(new Event('input',  { bubbles: true }));
     el.dispatchEvent(new Event('change', { bubbles: true }));
        }
  const pw = document.querySelector('input[type="password"]');
        const un = document.querySelector('input[type="email"],input[autocomplete*="username"],input[name*="user"],input[name*="email"],input[id*="user"],input[id*="email"]');
        setVal(un, data.username);
        setVal(pw, data.password);
        if (data.totp) navigator.clipboard.writeText(data.totp).catch(() => {});
      },
      args: [fill],
    });
    toast('Filled ?', 'success');
  } catch (e) {
    toast(e.message, 'error');
  }
}

async function handleCopy(s) {
  try {
    const fill = await send({ type: 'FILL_CONFIRMED', credentialId: s.id });
    if (!fill?.success) { toast('Copy failed', 'error'); return; }
    const text = fill.password ?? fill.cardNumber ?? '';
    await navigator.clipboard.writeText(text);
    toast('Copied to clipboard ?', 'success');
  } catch (e) {
    toast(e.message, 'error');
  }
}

// ── View switcher ─────────────────────────────────────────────────────────────
function showView(name) {
  _activeView = name;
  document.querySelectorAll('.view').forEach(v => { v.hidden = true; });
  const view = $(`view-${name}`);
  if (view) view.hidden = false;
  document.querySelectorAll('.nav-btn').forEach(b => {
    b.classList.toggle('active', b.dataset.view === name);
  });
  if (name === 'health') loadHealth();
  if (name === 'secure') loadSecureItemsPopup();
  if (name === 'autofill') loadAutofillSuggestions();
}

// ── Sort + filter ─────────────────────────────────────────────────────────────
function getFilteredSorted() {
  const q = ($('global-search-input')?.value ?? '').toLowerCase().trim();
  let items = [..._allItems];

  if (_activeFilter === 'Favorites') {
    items = items.filter(s => s.isFavorite);
  } else if (_activeFilter !== 'all') {
    items = items.filter(s => s.type === _activeFilter);
  }

  if (q) {
    items = items.filter(s =>
      (s.label ?? '').toLowerCase().includes(q) ||
      (s.username ?? '').toLowerCase().includes(q)
    );
  }

  if (_activeSort === 'az') {
    items.sort((a, b) => (a.label ?? '').localeCompare(b.label ?? ''));
  } else if (_activeSort === 'za') {
    items.sort((a, b) => (b.label ?? '').localeCompare(a.label ?? ''));
  } else if (_activeSort === 'recent') {
    items.sort((a, b) => (b.matchScore ?? 0) - (a.matchScore ?? 0));
  }

  return items;
}

function applyFilter() {
  const filtered = getFilteredSorted();
  const count = $('vault-count');
  if (count) count.textContent = `${filtered.length} item${filtered.length !== 1 ? 's' : ''}`;
  renderList($('vault-list'), filtered, {
    onFill: handleFill,
    onCopy: handleCopy,
    onClick: openDetail,
  });
}

// ── Edit screen ───────────────────────────────────────────────────────────────
function openEditScreen(s) {
  _editingItem = s;
  // Reset form fields without clearing _editingItem
  ['add-label','add-url','add-username','add-password','add-totp','add-notes',
   'add-card-label','add-card-name','add-card-number','add-card-expiry','add-card-cvv','add-card-notes',
   'add-auth-issuer','add-auth-username','add-auth-secret'].forEach(id => { const el = $(id); if (el) el.value = ''; });
  hide($('add-strength-wrap')); hide($('err-save'));

  selectAddType(s.type);

  // Hide type picker - can't change type of an existing item
  const picker = document.querySelector('.type-picker');
  if (picker) picker.classList.add('picker-hidden');

  // Update titles
  const titleMap = { Login:'Edit Login', CreditCard:'Edit Card', Authenticator:'Edit 2FA' };
  const saveMap  = { Login:'Update Login', CreditCard:'Update Card', Authenticator:'Update 2FA' };
  $('add-screen-title').textContent = titleMap[s.type] ?? 'Edit Item';
  const lbl = $('btn-save-item-label');
  if (lbl) lbl.textContent = saveMap[s.type] ?? 'Update';

  // Pre-fill fields
  if (s.type === 'Login') {
    $('add-label').value      = s.label    ?? '';
    $('add-url').value        = s.url      ?? '';
    $('add-username').value   = s.username ?? '';
    $('add-totp').value       = s.totpSecret ?? '';
    $('add-notes').value      = s.notes    ?? '';
    $('add-favorite').checked = s.isFavorite ?? false;
  } else if (s.type === 'CreditCard') {
    $('add-card-label').value  = s.label          ?? '';
    $('add-card-name').value   = s.cardholderName ?? '';
    $('add-card-number').value = s.cardNumber     ?? '';
    $('add-card-expiry').value = s.cardExpiry     ?? '';
  } else if (s.type === 'Authenticator') {
    $('add-auth-issuer').value   = s.label    ?? '';
    $('add-auth-username').value = s.username ?? '';
    const typeEl = $('add-auth-type');      if (typeEl) typeEl.value  = s.authType  ?? 'TOTP';
    const digEl  = $('add-auth-digits');    if (digEl)  digEl.value   = String(s.digits ?? 6);
    const perEl  = $('add-auth-period');    if (perEl)  perEl.value   = String(s.period ?? 30);
    const algEl  = $('add-auth-algorithm'); if (algEl)  algEl.value   = s.algorithm ?? 'SHA1';
    const favEl  = $('add-auth-favorite');  if (favEl)  favEl.checked = s.isFavorite ?? false;
  }

  showScreen('screen-add');
}

// ── Item Detail ───────────────────────────────────────────────────────────────
function openDetail(s) {
  _currentDetailItem = s;
  $('detail-title').textContent = s.label;

  // Favourite icon
  const favIcon = $('detail-fav-icon');
  favIcon.style.fill = s.isFavorite ? '#FBBF24' : 'none';
  favIcon.style.stroke = s.isFavorite ? '#FBBF24' : 'currentColor';

  // Hide fill/copy for non-fillable types
  const fillBtn = $('btn-detail-fill');
  const copyBtn = $('btn-detail-copy-pw');
  fillBtn.hidden = (s.type === 'Identity' || s.type === 'Note' || s.type === 'Authenticator');
  copyBtn.hidden = (s.type === 'Identity' || s.type === 'Note' || s.type === 'Authenticator');

  renderDetailBody(s);
  showScreen('screen-detail');
}

function renderDetailBody(s) {
  const body = $('detail-body');
  body.innerHTML = '';

  const copyBtn = (value, title = 'Copy') => {
    const btn = document.createElement('button');
    btn.className = 'detail-copy-btn';
    btn.title = title;
    btn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;
    btn.addEventListener('click', async () => {
      await navigator.clipboard.writeText(value || '');
      toast('Copied ?', 'success');
 });
    return btn;
  };

  function field(label, value, opts = {}) {
    if (!value) return null;
    const wrap = document.createElement('div');
    wrap.className = 'detail-field';

    const iconWrap = buildItemIcon(s, 28);
    if (label !== 'Site') iconWrap.remove?.(); // only show on first field

  const bdy = document.createElement('div');
    bdy.className = 'detail-field-body';
    const lbl = document.createElement('div');
lbl.className = 'detail-field-label';
    lbl.textContent = label;
    const val = document.createElement('div');
    val.className = 'detail-field-value' + (opts.mono ? ' mono' : '') + (opts.blur ? ' blurred' : '');
    val.textContent = opts.blur ? value.replace(/./g, '•') : value;
    if (opts.blur) {
 val.title = 'Click to reveal';
      val.addEventListener('click', () => {
     if (val.classList.contains('blurred')) {
          val.textContent = value;
val.classList.remove('blurred');
    } else {
       val.textContent = value.replace(/./g, '•');
          val.classList.add('blurred');
     }
      });
    }
    bdy.appendChild(lbl);
    bdy.appendChild(val);
    wrap.appendChild(bdy);
    if (!opts.noClipboard) wrap.appendChild(copyBtn(value));
    body.appendChild(wrap);
    return wrap;
  }

  if (s.type === 'Login') {
    field('Label',    s.label);
    field('URL',      s.url);
    field('Username', s.username);
    field('Password', s._rawPassword ?? '(load to view)', { mono: true, blur: true });

    // TOTP row — flat code display, no circular ring
    const secret = parseTotpSecret(s.totpSecret);
    if (secret) {
      const tot = document.createElement('div');
      tot.className = 'detail-totp';

      const hdr = document.createElement('div');
      hdr.className = 'detail-totp-header';

      const tlbl = document.createElement('div');
      tlbl.className = 'detail-totp-label';
      tlbl.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="11" height="11" style="vertical-align:-1px;margin-right:4px"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>2FA Code`;

      const cpyBtn = document.createElement('button');
      cpyBtn.className = 'detail-copy-btn';
      cpyBtn.title = 'Copy code';
      cpyBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;

      hdr.appendChild(tlbl); hdr.appendChild(cpyBtn);

      const codeEl = document.createElement('div');
      codeEl.className = 'detail-totp-code';
      codeEl.textContent = '\u00B7\u00B7\u00B7  \u00B7\u00B7\u00B7';

      // Circular ring timer
      const R = 14, CIRC = +(2 * Math.PI * R).toFixed(2);
      const ringWrap = document.createElement('div');
      ringWrap.className = 'detail-totp-ring-wrap';
      ringWrap.innerHTML = `
        <svg class="detail-totp-ring-svg" viewBox="0 0 36 36" width="52" height="52">
          <circle class="dtr-bg" cx="18" cy="18" r="${R}"/>
          <circle class="dtr-fill" cx="18" cy="18" r="${R}"
            stroke-dasharray="${CIRC}" stroke-dashoffset="${CIRC}"/>
        </svg>
        <div class="dtr-secs-center" id="dtr-secs-center-login">30</div>`;
      const ringEl = ringWrap.querySelector('.dtr-fill');
      const secsEl = ringWrap.querySelector('.dtr-secs-center');

      tot.appendChild(hdr);
      const codeRow = document.createElement('div');
      codeRow.className = 'detail-totp-code-row';
      codeRow.appendChild(codeEl);
      codeRow.appendChild(ringWrap);
      tot.appendChild(codeRow);
      body.appendChild(tot);

      const updateBar = () => {
        const s = totpSecondsLeft();
        const frac = s / 30;
        ringEl.style.strokeDashoffset = CIRC * (1 - frac);
        const color = s <= 5 ? 'var(--danger)' : s <= 10 ? 'var(--warning)' : 'var(--accent)';
        ringEl.style.stroke = color;
        secsEl.textContent = s;
        secsEl.style.color = s <= 5 ? 'var(--danger)' : s <= 10 ? 'var(--warning)' : 'var(--text-muted)';
      };

      const refreshCode = async () => {
        try {
          const c = await generateTOTP(secret);
          codeEl.textContent = c.slice(0, 3) + '\u2003' + c.slice(3);
        } catch { codeEl.textContent = 'Invalid secret'; }
      };

      cpyBtn.addEventListener('click', async () => {
        try {
          const c = await generateTOTP(secret);
          await navigator.clipboard.writeText(c);
          toast('Code copied', 'success');
        } catch { toast('Failed to copy', 'error'); }
      });

      refreshCode();
      updateBar();
      clearInterval(_totpInterval);
      let _lastPeriod = Math.floor(Date.now() / 1000 / 30);
      _totpInterval = setInterval(() => {
        updateBar();
        const period = Math.floor(Date.now() / 1000 / 30);
        if (period !== _lastPeriod) { _lastPeriod = period; refreshCode(); }
      }, 1000);
    }
  } else if (s.type === 'CreditCard') {
 field('Label',         s.label);
    field('Cardholder',      s.cardholderName ?? s.username);
 field('Card Number',     s.cardNumber ?? '•••• •••• •••• ••••', { mono: true, blur: true });
    field('Expiry',        s.cardExpiry);
  } else if (s.type === 'Identity') {
    field('Label',   s.label);
    field('Name',    s.username);
    field('Email',   s.email ?? s.username);
  } else if (s.type === 'Note') {
    field('Title',   s.label);
    field('Content', s.notes ?? '(secure note)', { blur: true });
  } else if (s.type === 'Authenticator') {
    field('Issuer',   s.label);
    field('Account',  s.username);
    if (s.authType)  field('Type',      s.authType);
    if (s.algorithm) field('Algorithm', s.algorithm);
    if (s.digits)    field('Digits',    String(s.digits));
    if (s.period)    field('Period',    `${s.period}s`);

    // Live TOTP code display (same as Login)
    const authSecret = parseTotpSecret(s.totpSecret);
    if (authSecret) {
      const tot = document.createElement('div');
      tot.className = 'detail-totp';
      const hdr = document.createElement('div');
      hdr.className = 'detail-totp-header';
      const tlbl = document.createElement('div');
      tlbl.className = 'detail-totp-label';
      tlbl.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="11" height="11" style="vertical-align:-1px;margin-right:4px"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/></svg>One-Time Code`;
      const cpyBtn = document.createElement('button');
      cpyBtn.className = 'detail-copy-btn';
      cpyBtn.title = 'Copy code';
      cpyBtn.innerHTML = `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>`;
      hdr.appendChild(tlbl); hdr.appendChild(cpyBtn);
      const codeEl = document.createElement('div');
      codeEl.className = 'detail-totp-code';
      codeEl.textContent = '\u00B7\u00B7\u00B7  \u00B7\u00B7\u00B7';
      // Circular ring timer
      const periodSecs = s.period ?? 30;
      const Ra = 14, CIRCa = +(2 * Math.PI * Ra).toFixed(2);
      const ringWrap = document.createElement('div');
      ringWrap.className = 'detail-totp-ring-wrap';
      ringWrap.innerHTML = `
        <svg class="detail-totp-ring-svg" viewBox="0 0 36 36" width="52" height="52">
          <circle class="dtr-bg" cx="18" cy="18" r="${Ra}"/>
          <circle class="dtr-fill" cx="18" cy="18" r="${Ra}"
            stroke-dasharray="${CIRCa}" stroke-dashoffset="${CIRCa}"/>
        </svg>
        <div class="dtr-secs-center">30</div>`;
      const ringEl = ringWrap.querySelector('.dtr-fill');
      const secsEl = ringWrap.querySelector('.dtr-secs-center');

      tot.appendChild(hdr);
      const codeRow = document.createElement('div');
      codeRow.className = 'detail-totp-code-row';
      codeRow.appendChild(codeEl);
      codeRow.appendChild(ringWrap);
      tot.appendChild(codeRow);
      body.appendChild(tot);
      const updateBar = () => {
        const left = periodSecs - (Math.floor(Date.now() / 1000) % periodSecs);
        const frac = left / periodSecs;
        ringEl.style.strokeDashoffset = CIRCa * (1 - frac);
        const color = left <= 5 ? 'var(--danger)' : left <= 10 ? 'var(--warning)' : 'var(--accent)';
        ringEl.style.stroke = color;
        secsEl.textContent = left;
        secsEl.style.color = left <= 5 ? 'var(--danger)' : left <= 10 ? 'var(--warning)' : 'var(--text-muted)';
      };
      const refreshCode = async () => {
        try {
          const c = await generateTOTP(authSecret);
          codeEl.textContent = c.slice(0, 3) + '\u2003' + c.slice(3);
        } catch { codeEl.textContent = 'Invalid secret'; }
      };
      cpyBtn.addEventListener('click', async () => {
        try {
          const c = await generateTOTP(authSecret);
          await navigator.clipboard.writeText(c);
          toast('Code copied', 'success');
        } catch { toast('Failed to copy', 'error'); }
      });
      refreshCode(); updateBar();
      clearInterval(_totpInterval);
      let _lastPeriod = Math.floor(Date.now() / 1000 / periodSecs);
      _totpInterval = setInterval(() => {
        updateBar();
        const period = Math.floor(Date.now() / 1000 / periodSecs);
        if (period !== _lastPeriod) { _lastPeriod = period; refreshCode(); }
      }, 1000);
    }
  }
}

// ── Vault Hero Stats ──────────────────────────────────────────────────────────
function updateVaultHero() {
  const hero    = $('vault-hero');
  const statsRow = $('vault-stats-row');
  if (!hero || !statsRow) return;

  const logins  = _allItems.filter(s => s.type === 'Login');
  const cards   = _allItems.filter(s => s.type === 'CreditCard');
  const twofa   = _allItems.filter(s => s.type === 'Authenticator' || (s.type === 'Login' && s.totpSecret));

  const weak    = logins.filter(s => (s.matchScore ?? 100) < 40);
  const breached = _allItems.filter(s => s.isBreached);

  const total   = logins.length || 1;
  const badCount = weak.length + breached.length;
  const score   = Math.max(0, Math.round(100 - (badCount / total) * 100));

  // Score number
  $('hero-score').textContent = score;

  // Chips
  const chips = $('hero-chips');
  chips.innerHTML = '';
  const makeChip = (icon, label) => {
    const c = document.createElement('div');
    c.className = 'vault-hero-chip';
    c.innerHTML = `${icon} ${label}`;
    return c;
  };
  if (weak.length === 0 && breached.length === 0) {
    chips.appendChild(makeChip(
      `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" width="11" height="11"><polyline points="20 6 9 17 4 12"/></svg>`,
      'All Clear'
    ));
  } else {
    if (weak.length)    chips.appendChild(makeChip(`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" width="11" height="11"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg>`, `${weak.length} Weak`));
    if (breached.length) chips.appendChild(makeChip(`<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="11" height="11"><circle cx="12" cy="8" r="5"/><path d="M9 17v1a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1v-1"/><path d="M9 13v4"/><path d="M15 13v4"/><path d="M12 13v4"/></svg>`, `${breached.length} Breached`));
  }

  // Stat pills
  statsRow.innerHTML = '';
  const makePill = (icon, bg, count, label, filterId) => {
    const p = document.createElement('div');
    p.className = 'vault-stat-pill';
    p.innerHTML = `
      <div class="vault-stat-icon" style="background:${bg};">${icon}</div>
      <div class="vault-stat-count">${count}</div>
      <div class="vault-stat-label">${label}</div>`;
    p.addEventListener('click', () => {
      document.querySelectorAll('.chip').forEach(c => c.classList.toggle('active', c.dataset.filter === filterId));
      _activeFilter = filterId;
      applyFilter();
    });
    return p;
  };

  statsRow.appendChild(makePill(
    `<svg viewBox="0 0 24 24" fill="none" stroke="#407cca" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="18" height="18"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>`,
    'rgba(64,124,202,0.12)', logins.length, 'Logins', 'Login'
  ));
  statsRow.appendChild(makePill(
    `<svg viewBox="0 0 24 24" fill="none" stroke="#14B8A6" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="18" height="18"><rect x="5" y="2" width="14" height="20" rx="2"/><line x1="12" y1="18" x2="12.01" y2="18"/></svg>`,
    'rgba(20,184,166,0.12)', twofa.length, '2FA Codes', 'Login'
  ));
  statsRow.appendChild(makePill(
    `<svg viewBox="0 0 24 24" fill="none" stroke="#F59E0B" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="18" height="18"><rect x="1" y="4" width="22" height="16" rx="2"/><line x1="1" y1="10" x2="23" y2="10"/></svg>`,
    'rgba(245,158,11,0.12)', cards.length, 'Cards', 'CreditCard'
  ));

  // Show both sections
  hero.hidden = false;
  statsRow.hidden = false;

  // Wire hero "View Details" btn
  const hBtn = $('hero-health-btn');
  if (hBtn) {
    hBtn.onclick = () => showView('health');
  }
}

// ── Load vault items ──────────────────────────────────────────────────────────
async function loadVault() {
  const list = $('vault-list');
list.innerHTML = `<div class="list-loading"><div class="spinner"></div></div>`;
  try {
    const resp = await send({ type: 'GET_ALL_ITEMS' });
    _allItems = (resp?.suggestions ?? []).map(s => ({
      ...s,
      isFavorite: s.isFavorite ?? false,
      isBreached: s.isBreached ?? false,
    }));
    applyFilter();
    updateVaultHero();
  } catch (e) {
    list.innerHTML = `<div class="list-empty"><span class="list-empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg></span><span>${e.message}</span></div>`;
  }
}

async function loadAutofillSuggestions() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  const url = tab?.url ?? '';
  const badge = $('autofill-url');
  try {
 badge.textContent = url ? new URL(url).hostname : '(no page)';
  } catch { badge.textContent = url; }

  const list = $('autofill-list');
  list.innerHTML = `<div class="list-loading"><div class="spinner"></div></div>`;
  try {
    const resp = await send({ type: 'GET_CREDENTIALS', url });
    if (resp?.isVaultLocked) {
 list.innerHTML = `<div class="list-empty"><span class="list-empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg></span><span>Vault is locked</span></div>`;
      return;
    }
    renderList(list, resp?.suggestions ?? [], { onFill: handleFill, onCopy: handleCopy, onClick: openDetail });
  } catch (e) {
    list.innerHTML = `<div class="list-empty"><span class="list-empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg></span><span>${e.message}</span></div>`;
  }
}

// ── Secure items (popup) ──────────────────────────────────────────────────────

let _secureItems = [];

async function loadSecureItemsPopup() {
  const list = $('secure-list');
  if (!list) return;
  list.innerHTML = `<div class="list-loading"><div class="spinner"></div></div>`;
  try {
  const r = await send({ type: 'GET_SECURE_ITEMS' });
    _secureItems = r?.items ?? [];
    applySecureFilter(document.querySelector('.secure-chip.active')?.dataset.sf ?? 'all');
  } catch (e) {
    list.innerHTML = `<div class="list-empty"><span class="list-empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/></svg></span><span>${e.message}</span></div>`;
  }
}

function applySecureFilter(filter) {
  const q = ($('secure-search')?.value ?? '').toLowerCase().trim();
  let items = [..._secureItems];
  if (filter === 'Favorites')  items = items.filter(i => i.isFavorite);
  else if (filter === 'Documents') items = items.filter(i => ['IdCard','Passport','DriversLicense'].includes(i.itemType));
  else if (filter !== 'all')   items = items.filter(i => i.itemType === filter);
  if (q) items = items.filter(i => (i.label ?? '').toLowerCase().includes(q));
  const list = $('secure-list');
  if (!list) return;
  const count = $('secure-item-count');
  if (count) count.textContent = `${items.length} item${items.length !== 1 ? 's' : ''}`;
  if (!items.length) { list.innerHTML = `<div class="list-empty"><span class="list-empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg></span><span>No items</span></div>`; return; }
  list.innerHTML = '';
  items.forEach(s => {
    const row = document.createElement('div');
    row.className = 'vault-item';
  row.appendChild(buildItemIcon({ ...s, type: s.itemType, label: s.label }));
    const body = document.createElement('div');
    body.className = 'item-body';
    const lbl = document.createElement('div');
    lbl.className = 'item-label';
    lbl.textContent = s.label;
  body.appendChild(lbl);
    const sub = document.createElement('div');
    sub.className = 'item-sub';
    sub.textContent = s.itemType;
    body.appendChild(sub);
    row.appendChild(body);
    list.appendChild(row);
  });
}

// ── Health ────────────────────────────────────────────────────────────────────

async function loadHealth() {
  const list = $('health-issues-list');
  if (!list) return;
  const logins = _allItems.filter(s => s.type === 'Login');
  const weak   = logins.filter(s => (s.matchScore ?? 100) < 40);
  const reused = [];
  const pwMap  = {};
  logins.forEach(s => { if (s.username) { pwMap[s.username] = (pwMap[s.username] || 0) + 1; } });
  const breached = _allItems.filter(s => s.isBreached);
  const total  = logins.length || 1;
  const bad    = weak.length + breached.length;
  const score  = Math.max(0, Math.round(100 - (bad / total) * 100));
  const ring   = $('health-ring-fill');
  const circumference = 213.6;
  if (ring) ring.style.strokeDashoffset = String(circumference - (score / 100) * circumference);
  const num = $('health-score-num'); if (num) num.textContent = String(score);
  const lbl = $('health-score-label'); if (lbl) lbl.textContent = score >= 80 ? 'Great' : score >= 60 ? 'Good' : score >= 40 ? 'Fair' : 'Needs attention';
  const wc  = $('health-weak-count');   if (wc)  wc.textContent  = String(weak.length);
  const rc  = $('health-reused-count'); if (rc)  rc.textContent  = String(reused.length);
  const bc  = $('health-breach-count'); if (bc)  bc.textContent  = String(breached.length);
  const issues = [...weak.map(s => ({ ...s, issue: 'Weak password' })), ...breached.map(s => ({ ...s, issue: 'Breached' }))];
  if (!issues.length) { list.innerHTML = `<div class="list-empty"><span class="list-empty-icon"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" width="40" height="40"><path d="M9 12l2 2 4-4"/><circle cx="12" cy="12" r="9"/></svg></span><span>No issues found</span></div>`; return; }
  list.innerHTML = '';
  issues.forEach(s => {
    const row = buildVaultItem(s, {});
    const badge = document.createElement('span');
    badge.className = 'item-breach-badge';
    badge.textContent = s.issue;
    row.querySelector('.item-label')?.appendChild(badge);
    list.appendChild(row);
  });
}

// ── Startup ───────────────────────────────────────────────────────────────────

function showSetupPage(pageId) {
  document.querySelectorAll('.setup-page').forEach(p => { p.hidden = true; });
  const pg = document.getElementById(`setup-page-${pageId}`);
  if (pg) pg.hidden = false;
}

function applyLockOrMain(status) {
  if (!status?.isUnlocked) {
    showScreen('screen-lock');
    const pinTab    = $('tab-pin');
    const pwTab     = $('tab-password');
    const switchBtn = $('btn-switch-mode');
    const footerSep = document.getElementById('lock-footer-sep');
    if (status?.isPinEnabled) {
      if (pinTab)    show(pinTab);
      if (pwTab)     hide(pwTab);
      if (switchBtn) { switchBtn.textContent = 'Use master password'; show(switchBtn); }
      if (footerSep) show(footerSep);
    } else {
      if (pinTab)    hide(pinTab);
      if (pwTab)     show(pwTab);
      if (switchBtn) hide(switchBtn);
      if (footerSep) hide(footerSep);
    }
    return;
  }
  showScreen('screen-main');
  loadVault();
  loadAutofillSuggestions();
}

async function init() {
  await loadTheme();

  const { fortress_mode } = await chrome.storage.local.get('fortress_mode');

  if (fortress_mode === 'standalone') {
    const status = await send({ type: 'GET_STATUS' }).catch(() => null);
    if (!status) { const el = $('conn-attempt'); if (el) el.textContent = 'Extension error. Please reload.'; return; }
    if (!status.isSetupComplete) { showScreen('screen-setup'); showSetupPage('password'); return; }
    applyLockOrMain(status);
    return;
  }

  // Try native service
  let status;
  try {
    status = await send({ type: 'GET_STATUS' });
  } catch (e) {
    // No service — go straight to standalone setup wizard
    showScreen('screen-setup');
    showSetupPage('password');
    return;
  }

  if (!status?.ok && status?.serviceDisconnected) {
    showScreen('screen-setup');
    showSetupPage('password');
    return;
  }

  if (!status?.isSetupComplete) {
    showScreen('screen-setup');
    showSetupPage('desktop');
    return;
  }

  applyLockOrMain(status);
}

// ── Setup wizard (standalone mode) ────────────────────────────────────────
function setupWizard() {
  let _wData = { provider: null, gdriveToken: null, password: null };

  // Welcome page choices
  $('btn-setup-have-desktop')?.addEventListener('click', () => {
    chrome.storage.local.set({ fortress_mode: 'service' });
    showSetupPage('desktop');
  });
  $('btn-setup-standalone')?.addEventListener('click', () => showSetupPage('sync'));

  // Desktop page: retry connects to service; back goes to welcome
  $('btn-setup-retry')?.addEventListener('click', () => init().catch(console.error));
  $('btn-desktop-back')?.addEventListener('click', () => {
    chrome.storage.local.remove('fortress_mode');
    showSetupPage('welcome');
  });
  // Escape hatch for users who picked "I have desktop" but don't actually have
  // it running — let them continue with the standalone wizard inline.
  $('btn-setup-use-extension-only')?.addEventListener('click', async () => {
    await chrome.storage.local.remove('fortress_mode');
    showSetupPage('sync');
  });

  // Back buttons (data-target = page to navigate to)
  document.querySelectorAll('.setup-back-btn[data-target]').forEach(btn => {
    btn.addEventListener('click', () => showSetupPage(btn.dataset.target));
  });

  // ── Sync provider selection ─────────────────────────────────────────────
  document.querySelectorAll('.sync-provider-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const provider = btn.dataset.provider;
      if (provider === 'dropbox' || provider === 'onedrive') return; // coming soon
      document.querySelectorAll('.sync-provider-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      _wData.provider = provider;
      const gdr = $('gdrive-connect-row');
      if (gdr) gdr.hidden = (provider !== 'gdrive');
      const nxt = $('btn-sync-next');
      if (nxt) nxt.disabled = (provider === 'gdrive' && !_wData.gdriveToken);
      else if (nxt) nxt.disabled = false;
      if (provider !== 'gdrive') { const n = $('btn-sync-next'); if (n) n.disabled = false; }
    });
  });

  $('btn-gdrive-auth')?.addEventListener('click', async () => {
    const btn  = $('btn-gdrive-auth');
    const stat = $('gdrive-status-text');
    setLoading(btn, true);
    try {
      const r = await send({ type: 'GDRIVE_AUTH' });
      _wData.gdriveToken = r.token;
      if (stat) { stat.textContent = 'Connected to Google Drive ✓'; stat.style.color = 'var(--success)'; }
      const nxt = $('btn-sync-next'); if (nxt) nxt.disabled = false;
    } catch (e) {
      if (stat) { stat.textContent = 'Failed: ' + e.message; stat.style.color = 'var(--danger)'; }
    } finally { setLoading(btn, false); }
  });

  $('btn-sync-next')?.addEventListener('click', () => {
    if (!_wData.provider) return;
    if (_wData.provider === 'gdrive' && !_wData.gdriveToken) return;
    showSetupPage('password');
  });

  // ── Master password ─────────────────────────────────────────────────────
  const pwInp  = $('inp-setup-pw');
  const pwInp2 = $('inp-setup-pw2');

  function pwStrength(pw) {
    let s = 0;
    if (pw.length >= 8)           s++;
    if (pw.length >= 12)          s++;
    if (/[A-Z]/.test(pw))         s++;
    if (/[0-9]/.test(pw))         s++;
    if (/[^A-Za-z0-9]/.test(pw))  s++;
    const labels = ['', 'Weak', 'Fair', 'Good', 'Strong', 'Very strong'];
    const colors = ['', '#ef4444', '#f59e0b', '#22c55e', '#22c55e', '#22c55e'];
    return { s, label: labels[Math.min(s, 5)], color: colors[Math.min(s, 5)] || colors[1], pct: Math.min(s * 20, 100) + '%' };
  }

  function updatePwSetupUI() {
    const pw  = pwInp?.value  ?? '';
    const pw2 = pwInp2?.value ?? '';
    const { label, color, pct } = pwStrength(pw);
    const sfill = $('setup-strength-fill');
    const slabel = $('setup-strength-label');
    if (sfill)  { sfill.style.width = pct; sfill.style.background = pw ? color : 'var(--border)'; }
    if (slabel) { slabel.textContent = pw ? label : '—'; slabel.style.color = pw ? color : ''; }
    $('req-len')?.classList.toggle('ok', pw.length >= 8);
    $('req-upper')?.classList.toggle('ok', /[A-Z]/.test(pw));
    $('req-num')?.classList.toggle('ok', /[0-9]/.test(pw));
    const nxt = $('btn-pw-next');
    if (nxt) nxt.disabled = !(pw.length >= 8 && pw === pw2);
  }

  pwInp?.addEventListener('input', updatePwSetupUI);
  pwInp2?.addEventListener('input', updatePwSetupUI);

  $('btn-pw-next')?.addEventListener('click', () => {
    const pw  = pwInp?.value  ?? '';
    const pw2 = pwInp2?.value ?? '';
    const err = $('err-setup-pw');
    hide(err);
    if (pw.length < 8) { err.textContent = 'Password must be at least 8 characters'; show(err); return; }
    if (pw !== pw2)    { err.textContent = 'Passwords do not match'; show(err); return; }
    _wData.password = pw;
    showSetupPage('pin');
  });

  // ── PIN setup ─────────────────────────────────────────────────────────────
  let _setupPin = '';
  function updateSetupPinDots() {
    document.querySelectorAll('#setup-pin-display .dot').forEach((d, i) => {
      d.classList.toggle('filled', i < _setupPin.length);
    });
    const cb = $('btn-setup-pin-confirm');
    if (cb) cb.disabled = _setupPin.length < 4;
  }

  document.querySelectorAll('#setup-page-pin .pin-key').forEach(k => {
    k.addEventListener('click', () => {
      const v = k.dataset.v;
      if (v === 'clear')   { _setupPin = _setupPin.slice(0, -1); updateSetupPinDots(); return; }
      if (v === 'confirm') { if (_setupPin.length >= 4) _doCompleteSetup(true); return; }
      if (_setupPin.length < 4) { _setupPin += v; updateSetupPinDots(); }
    });
  });

  $('btn-pin-skip')?.addEventListener('click', () => _doCompleteSetup(false));

  async function _doCompleteSetup(withPin) {
    showSetupPage('complete');
    const openBtn = $('btn-setup-open-vault');
    if (openBtn) openBtn.disabled = true;
    try {
      await send({
        type:        'SETUP',
        password:    _wData.password,
        pin:         withPin ? _setupPin : null,
        // Standalone-only fields — ignored by the service-mode branch in background.js
        syncProvider: _wData.provider ?? 'local',
        gdriveToken: _wData.gdriveToken,
      });
      const names = { local: 'locally in this browser', gdrive: 'in Google Drive' };
      const subEl = $('setup-complete-sub');
      if (subEl) subEl.textContent = `Encrypted and stored ${names[_wData.provider] ?? 'locally'}.`;
      if (withPin) {
        const si = $('setup-complete-sync-info');
        if (si) { si.hidden = false; si.textContent = '🔒 PIN quick-unlock is active'; }
      }
    } catch (e) {
      showSetupPage('password');
      const err = $('err-setup-pw');
      err.textContent = 'Setup failed: ' + e.message; show(err);
    } finally {
      if (openBtn) openBtn.disabled = false;
    }
  }

  $('btn-setup-open-vault')?.addEventListener('click', async () => {
    showScreen('screen-main');
    await loadVault();
    await loadAutofillSuggestions();
  });
}

document.addEventListener('DOMContentLoaded', () => {
  init().catch(console.error);
  setupWizard();

  // ── PIN keypad (lock screen only — scoped to avoid clash with setup wizard) ─
  let _pin = '';
  const PIN_LENGTH = 4;

  function updatePinDots() {
    document.querySelectorAll('#pin-display .dot').forEach((dot, i) => {
      dot.classList.toggle('filled', i < _pin.length);
    });
  }

  async function submitPin() {
    if (_pin.length < PIN_LENGTH) return;
    const btn = document.querySelector('#screen-lock .pin-submit');
    if (btn) btn.disabled = true;
    try {
      const r = await send({ type: 'UNLOCK_PIN', pin: _pin });
    if (r?.ok) {
        _pin = ''; updatePinDots();
    showScreen('screen-main');
        await loadVault();
  await loadAutofillSuggestions();
      } else {
    const err = $('err-pin');
        if (err) { err.textContent = 'Incorrect PIN'; show(err); setTimeout(() => hide(err), 2000); }
        _pin = ''; updatePinDots();
      }
    } catch (e) {
      const err = $('err-pin');
      if (err) { err.textContent = e.message; show(err); setTimeout(() => hide(err), 2500); }
      _pin = ''; updatePinDots();
    } finally {
      const btn2 = document.querySelector('#screen-lock .pin-submit');
      if (btn2) btn2.disabled = false;
    }
  }

  document.querySelectorAll('#screen-lock .pin-key').forEach(key => {
    key.addEventListener('click', async () => {
      const v = key.dataset.v;
      if (v === 'clear')       { _pin = _pin.slice(0, -1); updatePinDots(); }
    else if (v === 'submit') { await submitPin(); }
      else if (_pin.length < PIN_LENGTH) { _pin += v; updatePinDots(); if (_pin.length === PIN_LENGTH) await submitPin(); }
    });
  });

  // ── Master password unlock ───────────────────────────────────────────────
  $('btn-unlock')?.addEventListener('click', async () => {
    const btn  = $('btn-unlock');
    const pw   = $('inp-password')?.value ?? '';
    const err  = $('err-password');
    if (!pw) { if (err) { err.textContent = 'Enter your master password'; show(err); } return; }
    setLoading(btn, true);
    try {
      const r = await send({ type: 'UNLOCK', password: pw });
      if (r?.ok) {
        showScreen('screen-main');
        await loadVault();
 await loadAutofillSuggestions();
      } else {
        if (err) { err.textContent = 'Incorrect password'; show(err); }
      }
    } catch (e) {
   if (err) { err.textContent = e.message; show(err); }
    } finally {
      setLoading(btn, false);
    }
  });

  $('inp-password')?.addEventListener('keydown', e => { if (e.key === 'Enter') $('btn-unlock')?.click(); });

  // ── Switch between PIN / password mode ───────────────────────────────────
  $('btn-switch-mode')?.addEventListener('click', () => {
    const pinTab = $('tab-pin');
    const pwTab  = $('tab-password');
    const btn    = $('btn-switch-mode');
    if (pwTab?.hidden) {
      hide(pinTab); show(pwTab);
      if (btn) btn.textContent = 'Use PIN';
      $('inp-password')?.focus();
  } else {
      show(pinTab); hide(pwTab);
      if (btn) btn.textContent = 'Use master password';
    }
  });

  // ── Lock button ──────────────────────────────────────────────────────────
  $('btn-lock')?.addEventListener('click', async () => {
    await send({ type: 'LOCK' }).catch(() => {});
    showScreen('screen-lock');
    _pin = ''; updatePinDots();
  });

  $('btn-lock-from-settings')?.addEventListener('click', async () => {
    await send({ type: 'LOCK' }).catch(() => {});
    showScreen('screen-lock');
    _pin = ''; updatePinDots();
  });

  // ── Nav tabs ─────────────────────────────────────────────────────────────
  document.querySelectorAll('.nav-btn').forEach(btn => {
    btn.addEventListener('click', () => showView(btn.dataset.view));
  });

  // ── Add item ─────────────────────────────────────────────────────────────
  $('btn-add')?.addEventListener('click', () => {
    _editingItem = null;
    $('add-screen-title').textContent = 'New Item';
    const lbl = $('btn-save-item-label'); if (lbl) lbl.textContent = 'Save Login';
    const picker = document.querySelector('.type-picker'); if (picker) picker.classList.remove('picker-hidden');
    selectAddType('Login');
    showScreen('screen-add');
  });

  $('btn-add-back')?.addEventListener('click', () => showScreen('screen-main'));

  // ── Type picker ──────────────────────────────────────────────────────────
  document.querySelectorAll('.type-btn').forEach(btn => {
    btn.addEventListener('click', () => selectAddType(btn.dataset.itype));
  });

  function selectAddType(type) {
 document.querySelectorAll('.type-btn').forEach(b => b.classList.toggle('active', b.dataset.itype === type));
    ['Login','CreditCard','Authenticator'].forEach(t => {
const el = $(`add-fields-${t}`); if (el) el.hidden = (t !== type);
    });
    const lbl = $('btn-save-item-label');
if (lbl) lbl.textContent = type === 'Login' ? 'Save Login' : type === 'CreditCard' ? 'Save Card' : 'Save 2FA';
  }

  // ── Save item ────────────────────────────────────────────────────────────
  $('btn-save-item')?.addEventListener('click', async () => {
    const btn  = $('btn-save-item');
 const err  = $('err-save');
    const type = document.querySelector('.type-btn.active')?.dataset.itype ?? 'Login';
    setLoading(btn, true); if (err) hide(err);

    try {
      if (_editingItem) {
        if (type === 'Login') {
      await send({ type: 'UPDATE_LOGIN', id: _editingItem.id, label: $('add-label').value.trim(), url: $('add-url').value.trim(), username: $('add-username').value.trim(), password: $('add-password').value, totpSecret: $('add-totp').value.trim(), notes: $('add-notes').value.trim(), isFavorite: $('add-favorite').checked });
        } else if (type === 'CreditCard') {
   await send({ type: 'UPDATE_CARD', id: _editingItem.id, label: $('add-card-label').value.trim(), cardholderName: $('add-card-name').value.trim(), number: $('add-card-number').value.trim(), expiry: $('add-card-expiry').value.trim(), cvv: $('add-card-cvv').value });
        } else {
      const issuer = $('add-auth-issuer').value.trim();
        await send({ type: 'UPDATE_AUTHENTICATOR', id: _editingItem.id, label: issuer, username: $('add-auth-username').value.trim(), totpSecret: $('add-auth-secret').value.trim(), authType: $('add-auth-type').value, digits: +$('add-auth-digits').value, period: +$('add-auth-period').value, algorithm: $('add-auth-algorithm').value });
    }
        toast('Updated ✓', 'success');
      } else {
        if (type === 'Login') {
          await send({ type: 'SAVE_LOGIN', label: $('add-label').value.trim(), url: $('add-url').value.trim(), username: $('add-username').value.trim(), password: $('add-password').value, totpSecret: $('add-totp').value.trim(), notes: $('add-notes').value.trim(), isFavorite: $('add-favorite').checked });
        } else if (type === 'CreditCard') {
          await send({ type: 'SAVE_CARD', label: $('add-card-label').value.trim(), cardholderName: $('add-card-name').value.trim(), number: $('add-card-number').value.trim(), expiry: $('add-card-expiry').value.trim(), cvv: $('add-card-cvv').value });
        } else {
  const issuer = $('add-auth-issuer').value.trim();
          await send({ type: 'SAVE_AUTHENTICATOR', label: issuer, username: $('add-auth-username').value.trim(), totpSecret: $('add-auth-secret').value.trim(), authType: $('add-auth-type').value, digits: +$('add-auth-digits').value, period: +$('add-auth-period').value, algorithm: $('add-auth-algorithm').value });
        }
        toast('Saved ✓', 'success');
      }
      _editingItem = null;
 showScreen('screen-main');
      await loadVault();
    } catch (e) {
    if (err) { err.textContent = e.message; show(err); }
    } finally {
  setLoading(btn, false);
    }
  });

  // ── Detail screen buttons ────────────────────────────────────────────────
  $('btn-detail-back')?.addEventListener('click', () => showScreen('screen-main'));

  $('btn-detail-fill')?.addEventListener('click', () => {
if (_currentDetailItem) handleFill(_currentDetailItem);
  });

  $('btn-detail-copy-pw')?.addEventListener('click', () => {
    if (_currentDetailItem) handleCopy(_currentDetailItem);
  });

  $('btn-detail-edit')?.addEventListener('click', () => {
    if (_currentDetailItem) openEditScreen(_currentDetailItem);
  });

  $('btn-detail-favorite')?.addEventListener('click', async () => {
    if (!_currentDetailItem) return;
    const newVal = !_currentDetailItem.isFavorite;
    _currentDetailItem.isFavorite = newVal;
    const idx = _allItems.findIndex(x => x.id === _currentDetailItem.id);
    if (idx >= 0) _allItems[idx].isFavorite = newVal;
    const icon = $('detail-fav-icon');
    if (icon) { icon.style.fill = newVal ? '#FBBF24' : 'none'; icon.style.stroke = newVal ? '#FBBF24' : 'currentColor'; }
    await send({ type: 'TOGGLE_FAVORITE', credentialId: _currentDetailItem.id, value: newVal }).catch(() => {});
    applyFilter();
  });

  $('btn-detail-delete')?.addEventListener('click', async () => {
    if (!_currentDetailItem) return;
    if (!confirm(`Delete "${_currentDetailItem.label}"?`)) return;
    try {
      await send({ type: 'DELETE_ITEM', credentialId: _currentDetailItem.id });
      _allItems = _allItems.filter(x => x.id !== _currentDetailItem.id);
      applyFilter(); updateVaultHero();
      toast('Deleted', 'success');
  showScreen('screen-main');
    } catch (e) { toast(e.message, 'error'); }
  });

  // ── Settings ─────────────────────────────────────────────────────────────
  $('btn-settings-nav')?.addEventListener('click', () => showScreen('screen-settings'));
  $('btn-settings-back')?.addEventListener('click', () => showScreen('screen-main'));
  $('settings-open-options')?.addEventListener('click', () => chrome.runtime.openOptionsPage());

  $('setting-autolock')?.addEventListener('change', async (e) => {
    const secs = +e.target.value;
    _settings.autoLockSeconds = secs;
    await chrome.storage.sync.set({ autoLockSeconds: secs });
    await send({ type: 'RESCHEDULE_AUTOLOCK', seconds: secs }).catch(() => {});
  });

  document.querySelectorAll('.theme-pill').forEach(p => {
    p.addEventListener('click', () => applyTheme(p.dataset.themeVal));
  });

  // ── Global search ─────────────────────────────────────────────────────────
  $('btn-search-toggle')?.addEventListener('click', () => {
    const bar = $('global-search-bar');
    if (bar?.hidden) { show(bar); $('global-search-input')?.focus(); }
    else hide(bar);
  });

  $('global-search-close')?.addEventListener('click', () => {
    hide($('global-search-bar'));
    const inp = $('global-search-input'); if (inp) inp.value = '';
    applyFilter();
  });

  $('global-search-input')?.addEventListener('input', applyFilter);

  // ── Filter chips ──────────────────────────────────────────────────────────
  document.querySelectorAll('.chip[data-filter]').forEach(c => {
    c.addEventListener('click', () => {
      document.querySelectorAll('.chip[data-filter]').forEach(x => x.classList.remove('active'));
      c.classList.add('active');
      _activeFilter = c.dataset.filter;
      applyFilter();
    });
  });

  // ── Sort ──────────────────────────────────────────────────────────────────
  $('vault-sort')?.addEventListener('change', e => { _activeSort = e.target.value; applyFilter(); });

  // ── Password strength on add form ─────────────────────────────────────────
  $('add-password')?.addEventListener('input', e => {
    updateStrength(e.target.value, $('add-strength-fill'), $('add-strength-label'), $('add-strength-wrap'));
  });

  // ── Eye (reveal) buttons ──────────────────────────────────────────────────
  document.querySelectorAll('.eye-btn').forEach(btn => {
    btn.addEventListener('click', () => {
      const inp = $(btn.dataset.for);
      if (inp) inp.type = inp.type === 'password' ? 'text' : 'password';
    });
  });

  // ── Generator ─────────────────────────────────────────────────────────────
  function refreshGen() {
    const type = document.querySelector('.gen-type-btn.active')?.dataset.gentype ?? 'password';
    let pw = '';
    if (type === 'password') pw = generatePassword(getGenOpts());
    else if (type === 'passphrase') pw = generatePassphrase(+$('gen-words').value, $('gen-sep').value, $('gen-pp-cap').checked, $('gen-pp-num').checked);
    else pw = generatePin(+$('gen-pin-len').value);
    const out = $('gen-output'); if (out) out.value = pw;
    updateStrength(pw, $('gen-strength-fill'), $('gen-strength-label'));
  }

  document.querySelectorAll('.gen-type-btn').forEach(btn => {
    btn.addEventListener('click', () => {
    document.querySelectorAll('.gen-type-btn').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      ['password','passphrase','pin'].forEach(t => { const el = $(`gen-opts-${t}`); if (el) el.hidden = t !== btn.dataset.gentype; });
      refreshGen();
    });
  });

  $('gen-refresh')?.addEventListener('click', refreshGen);
$('gen-length')?.addEventListener('input',  e => { const v=$('gen-len-val'); if(v) v.textContent=e.target.value; refreshGen(); });
  $('gen-words')?.addEventListener('input',   e => { const v=$('gen-words-val'); if(v) v.textContent=e.target.value; refreshGen(); });
  $('gen-pin-len')?.addEventListener('input', e => { const v=$('gen-pin-len-val'); if(v) v.textContent=e.target.value; refreshGen(); });
  ['gen-upper','gen-lower','gen-digits','gen-symbols','gen-ambiguous','gen-sep','gen-pp-cap','gen-pp-num'].forEach(id => $(`${id}`)?.addEventListener('change', refreshGen));

  $('gen-copy')?.addEventListener('click', async () => {
    const v = $('gen-output')?.value; if (!v) return;
    await navigator.clipboard.writeText(v).catch(() => {});
    toast('Copied ✓', 'success');
  });

  $('gen-fill-page')?.addEventListener('click', async () => {
    const v = $('gen-output')?.value; if (!v) return;
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    if (!tab?.id) return;
    await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: (pw) => { const el = document.activeElement; if (el && (el.tagName==='INPUT'||el.isContentEditable)) { const d=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value'); d?.set?.call(el,pw); el.dispatchEvent(new Event('input',{bubbles:true})); el.dispatchEvent(new Event('change',{bubbles:true})); } }, args: [v] }).catch(() => {});
    toast('Filled ✓', 'success');
  });

  // ── Secure items filter ────────────────────────────────────────────────────
  document.querySelectorAll('.secure-chip').forEach(c => {
    c.addEventListener('click', () => {
      document.querySelectorAll('.secure-chip').forEach(x => x.classList.remove('active'));
      c.classList.add('active');
      applySecureFilter(c.dataset.sf);
    });
  });

  $('secure-search')?.addEventListener('input', () => applySecureFilter(document.querySelector('.secure-chip.active')?.dataset.sf ?? 'all'));

  $('btn-secure-add')?.addEventListener('click', () => chrome.runtime.openOptionsPage());

  // ── Theme toggle button ────────────────────────────────────────────────────
  $('btn-theme-toggle')?.addEventListener('click', async () => {
 const { theme } = await chrome.storage.sync.get({ theme: 'system' });
    const next = theme === 'light' ? 'dark' : theme === 'dark' ? 'system' : 'light';
    await applyTheme(next);
  });

  // Load settings from storage
  chrome.storage.sync.get({ autoLockSeconds: 300, inlineBtn: true, savePrompt: true, breachAlerts: true, genLength: 20 }, s => {
    _settings = s;
  const al = $('setting-autolock'); if (al) al.value = String(s.autoLockSeconds);
    const ib = $('setting-inline-btn'); if (ib) ib.checked = s.inlineBtn;
    const sp = $('setting-save-prompt'); if (sp) sp.checked = s.savePrompt;
    const ba = $('setting-breach-alerts'); if (ba) ba.checked = s.breachAlerts;
    const gl = $('setting-gen-length'); if (gl) gl.value = String(s.genLength ?? 20);
    const grl = $('gen-length'); if (grl) { grl.value = String(s.genLength ?? 20); const v=$('gen-len-val'); if(v) v.textContent=String(s.genLength ?? 20); }
    refreshGen();
  });
});


