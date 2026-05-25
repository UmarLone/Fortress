// -----------------------------------------------------------------------------
// content.js  --  Fortress content script  v1.1
// * Autofill button injected next to password / username fields
// * Inline suggestion panel
// * Save-prompt banner on form submission with new credentials
// -----------------------------------------------------------------------------

(function () {
  'use strict';
  if (window.__fortressInjected) return;
  window.__fortressInjected = true;

  // -- Icons ------------------------------------------------------------------

  const LOCK_SVG = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" width="16" height="16"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>';
  const COPY_ICON_SVG  = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><rect x="9" y="9" width="13" height="13" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/></svg>';
  const CHECK_ICON_SVG = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><polyline points="20 6 9 17 4 12"/></svg>';
  const MORE_ICON_SVG  = '<svg viewBox="0 0 24 24" fill="currentColor" width="14" height="14"><circle cx="12" cy="5" r="1.8"/><circle cx="12" cy="12" r="1.8"/><circle cx="12" cy="19" r="1.8"/></svg>';

  // -- Helpers ----------------------------------------------------------------

  function isPasswordField(el) {
 return el.tagName === 'INPUT' && el.type === 'password';
  }

  function isUsernameField(el) {
    if (el.tagName !== 'INPUT') return false;
    const t  = (el.type         || '').toLowerCase();
    const n  = (el.name         || '').toLowerCase();
    const id = (el.id       || '').toLowerCase();
    const ac = (el.autocomplete || '').toLowerCase();
    if (['email', 'tel'].includes(t)) return true;
    if (ac.includes('username') || ac.includes('email')) return true;
    const kw = ['user', 'email', 'login', 'account', 'mail'];
    return kw.some(k => n.includes(k) || id.includes(k));
  }

  function isCreditCardField(el) {
    if (el.tagName !== 'INPUT') return false;
    const n  = (el.name || '').toLowerCase();
    const id = (el.id   || '').toLowerCase();
    const ac = (el.autocomplete || '').toLowerCase();
    if (ac.includes('cc-number') || ac.includes('cardnumber')) return true;
    return ['cardnum', 'ccnum', 'creditcard', 'cardno'].some(k => n.includes(k) || id.includes(k));
  }

  function getPageUrl() {
    return window.location.origin + window.location.pathname;
  }

  function escHtml(str) {
    return (str ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
  }

  // -- TOTP helpers for inline ring ------------------------------------------
  function _b32Decode(s) {
    const alpha = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
    s = s.replace(/=+$/, '').toUpperCase();
    let bits = 0, val = 0;
    const out = [];
    for (const c of s) {
      const idx = alpha.indexOf(c);
      if (idx < 0) continue;
      val = (val << 5) | idx; bits += 5;
      if (bits >= 8) { out.push((val >>> (bits - 8)) & 0xff); bits -= 8; }
    }
    return new Uint8Array(out);
  }
  async function _genTotp(raw) {
    const secret = (raw || '').startsWith('otpauth://')
      ? (() => { try { return new URL(raw).searchParams.get('secret') || raw; } catch { return raw; } })()
      : raw;
    const key = _b32Decode(secret.trim());
    if (!key.length) return '------';
    const counter = Math.floor(Date.now() / 1000 / 30);
    const buf = new ArrayBuffer(8);
    new DataView(buf).setUint32(4, counter, false);
    const ck = await crypto.subtle.importKey('raw', key, { name: 'HMAC', hash: 'SHA-1' }, false, ['sign']);
    const sig = new Uint8Array(await crypto.subtle.sign('HMAC', ck, buf));
    const off = sig[19] & 0xf;
    const code = (((sig[off] & 0x7f) << 24) | ((sig[off+1] & 0xff) << 16) | ((sig[off+2] & 0xff) << 8) | (sig[off+3] & 0xff)) % 1_000_000;
    return String(code).padStart(6, '0');
  }
  function _totpSecs() { return 30 - (Math.floor(Date.now() / 1000) % 30); }

  function setVal(el, v) {
    if (!el || !v) return;
const proto = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, 'value');
    proto?.set?.call(el, v);
    el.dispatchEvent(new Event('input',  { bubbles: true }));
    el.dispatchEvent(new Event('change', { bubbles: true }));
  }

  // -- Inject CSS once --------------------------------------------------------

  function injectStyles() {
    if (document.getElementById('fortress-styles')) return;
    const style = document.createElement('style');
    style.id = 'fortress-styles';
    style.textContent = [
      '@keyframes fortress-spin { to { transform: rotate(360deg); } }',
      '@keyframes fortress-slide-down { from { opacity:0; transform:translateY(-12px); } to { opacity:1; transform:translateY(0); } }',
      '@keyframes fortress-slide-up   { from { opacity:0; transform:translateY(12px);  } to { opacity:1; transform:translateY(0); } }',
    ].join('\n');
    document.head.appendChild(style);
  }
  injectStyles();

  // ===========================================================================
  //  AUTOFILL BUTTON
  // ===========================================================================

  const injectedFields = new WeakSet();
  // Map from input element → { btn, cleanup }
  const _fieldBtnMap = new WeakMap();

  function _positionBtn(btn, inputEl) {
    const rect = inputEl.getBoundingClientRect();
    const scrollY = window.scrollY;
    const scrollX = window.scrollX;
    Object.assign(btn.style, {
      top:  `${rect.top  + scrollY + rect.height / 2 - 12}px`,
      left: `${rect.left + scrollX + rect.width  - 32}px`,
    });
  }

  function injectButton(inputEl) {
    if (injectedFields.has(inputEl)) return;
    injectedFields.add(inputEl);

    const btn = document.createElement('button');
    btn.type  = 'button';
    btn.title = 'Fill with Fortress';
    btn.setAttribute('data-fortress-btn', '1');
    btn.innerHTML = `<img src="${chrome.runtime.getURL('assets/icons/icon32.png')}" width="16" height="16" style="display:block;border-radius:3px;" alt="">`;

    Object.assign(btn.style, {
      position:       'absolute',
      width:          '24px',
      height:         '24px',
      background:     'linear-gradient(135deg, rgba(64,124,202,0.18), rgba(90,155,224,0.18))',
      border:         '1px solid rgba(64,124,202,0.3)',
      borderRadius:   '6px',
      cursor:         'pointer',
      padding:        '3px',
      display:        'flex',
      alignItems:     'center',
      justifyContent: 'center',
      color:          '#407cca',
      opacity:        '0.85',
      transition:     'opacity 0.15s, transform 0.15s, box-shadow 0.15s',
      zIndex:         '2147483647',
      backdropFilter: 'blur(4px)',
      pointerEvents:  'auto',
    });

    _positionBtn(btn, inputEl);
    document.body.appendChild(btn);

    btn.addEventListener('mouseenter', () => {
      btn.style.opacity   = '1';
      btn.style.boxShadow = '0 0 12px rgba(64,124,202,0.4)';
      btn.style.transform = 'scale(1.1)';
    });
    btn.addEventListener('mouseleave', () => {
      btn.style.opacity   = '0.85';
      btn.style.boxShadow = 'none';
      btn.style.transform = 'scale(1)';
    });
    btn.addEventListener('click', (e) => {
      e.preventDefault(); e.stopPropagation();
      openInlinePanel(inputEl);
    });

    // Keep button aligned when page scrolls or resizes
    const onMove = () => _positionBtn(btn, inputEl);
    window.addEventListener('scroll', onMove, { passive: true });
    window.addEventListener('resize', onMove, { passive: true });

    // Hide button when input loses focus and isn't hovered
    let _focusTimer = null;
    const onBlur = () => {
      _focusTimer = setTimeout(() => {
        if (!btn.matches(':hover')) btn.style.opacity = '0';
      }, 150);
    };
    const onFocus = () => {
      clearTimeout(_focusTimer);
      btn.style.opacity = '0.85';
    };
    inputEl.addEventListener('focus', onFocus);
    inputEl.addEventListener('blur',  onBlur);
    btn.addEventListener('mouseenter', () => clearTimeout(_focusTimer));

    // Initial visibility based on focus state
    if (document.activeElement !== inputEl) btn.style.opacity = '0';

    _fieldBtnMap.set(inputEl, {
      btn,
      cleanup: () => {
        window.removeEventListener('scroll', onMove);
        window.removeEventListener('resize', onMove);
        btn.remove();
      }
    });
  }

  // ===========================================================================
  //  INLINE SUGGESTION PANEL
  // ===========================================================================

  let _panel = null;
  let _panelDark = false; // tracks detected theme for render helpers
  let _ringInterval = null;

  function closePanel() {
    if (_ringInterval) { clearInterval(_ringInterval); _ringInterval = null; }
    if (_panel) { _panel.remove(); _panel = null; }
  }

  function openInlinePanel(triggerInput) {
    closePanel();
    const _fieldType = isCreditCardField(triggerInput) ? 'card' : 'login';

    _panel = document.createElement('div');
    _panel.setAttribute('data-fortress-panel', '1');

    const rect    = triggerInput.getBoundingClientRect();
    const scrollY = window.scrollY;
    const scrollX = window.scrollX;

    const _isDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    _panelDark = _isDark;
    const panelBg          = _isDark ? '#1E293B' : '#FFFFFF';
    const panelBorder      = _isDark ? 'rgba(90,155,224,0.22)' : 'rgba(64,124,202,0.18)';
    const panelShadow      = _isDark
      ? '0 20px 60px rgba(0,0,0,0.55), 0 0 0 1px rgba(64,124,202,0.12)'
      : '0 8px 32px rgba(30,41,59,0.12), 0 2px 8px rgba(0,0,0,0.06), 0 0 0 1px rgba(64,124,202,0.10)';
    const panelColor       = _isDark ? '#F8FAFC' : '#1A1D26';
    const headerBorder     = _isDark ? 'rgba(255,255,255,0.07)' : 'rgba(64,124,202,0.10)';
    const headerBg         = _isDark ? 'rgba(64,124,202,0.10)' : 'rgba(64,124,202,0.06)';
    const closeBg          = _isDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.05)';
    const closeBorder      = _isDark ? 'rgba(255,255,255,0.10)' : 'rgba(0,0,0,0.08)';
    const closeColor       = _isDark ? '#94a3b8' : '#64748b';
    const subColor         = _isDark ? '#64748b' : '#94a3b8';
    const tabInactiveColor = _isDark ? '#64748b' : '#94a3b8';

    Object.assign(_panel.style, {
      position:   'absolute',
      top:        `${rect.bottom + scrollY + 6}px`,
      left:       `${rect.left + scrollX}px`,
      minWidth:   `${Math.max(rect.width, 300)}px`,
      maxWidth:   '340px',
      zIndex:     '2147483646',
      background: panelBg,
      border:     `1px solid ${panelBorder}`,
      borderRadius: '16px',
      boxShadow:  panelShadow,
      overflow:   'hidden',
      fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
      fontSize:   '13px',
      color:      panelColor,
      animation:  'fortress-slide-down 0.18s cubic-bezier(0.34,1.1,0.64,1)',
    });

    // \u2715 = close x
    const _logoUrl = chrome.runtime.getURL('assets/icons/icon32.png');
    _panel.innerHTML =
      `<div style="padding:10px 13px 9px;border-bottom:1px solid ${headerBorder};display:flex;align-items:center;gap:8px;background:${headerBg};">` +
        `<img src="${_logoUrl}" width="18" height="18" style="border-radius:4px;flex-shrink:0;" alt="">` +
        '<span style="font-weight:800;font-size:11px;color:#407cca;letter-spacing:0.10em;text-transform:uppercase;">FORTRESS</span>' +
        `<span style="flex:1;font-size:11px;color:${subColor};">Autofill</span>` +
        `<button data-close style="background:${closeBg};border:1px solid ${closeBorder};cursor:pointer;color:${closeColor};font-size:12px;line-height:1;padding:2px 7px;border-radius:5px;transition:all 0.12s;" title="Close">\u2715</button>` +
      '</div>' +
      // Tab row
      `<div style="display:flex;border-bottom:1px solid ${headerBorder};background:${headerBg};">` +
        `<button data-tab="matches" style="flex:1;padding:7px 4px;background:transparent;border:none;border-bottom:2px solid #407cca;cursor:pointer;font-size:11px;font-weight:700;color:#407cca;transition:all 0.12s;">Matches</button>` +
        `<button data-tab="all" style="flex:1;padding:7px 4px;background:transparent;border:none;border-bottom:2px solid transparent;cursor:pointer;font-size:11px;font-weight:600;color:${tabInactiveColor};transition:all 0.12s;">All</button>` +
      '</div>' +
      '<div data-fortress-list style="max-height:240px;overflow-y:auto;padding:5px 5px;">' +
        `<div style="padding:22px;text-align:center;color:${subColor};"><div style="display:inline-block;width:18px;height:18px;border:2px solid rgba(64,124,202,0.22);border-top-color:#407cca;border-radius:50%;animation:fortress-spin 0.7s linear infinite;"></div></div>` +
      '</div>';

    document.body.appendChild(_panel);
    _panel.querySelector('[data-close]').addEventListener('click', closePanel);

    let _matchedItems  = null;
    let _allPanelItems = null;
    let _activeTab     = 'matches';
    let _vaultLocked   = false;

    function attachItemListeners() {
      if (!_panel) return;
      const list = _panel.querySelector('[data-fortress-list]');

      // Close any open dropdown when clicking elsewhere
      function closeDropdowns() {
        list.querySelectorAll('[data-fortress-dropdown]').forEach(d => d.remove());
      }

      list.querySelectorAll('[data-fill-id]').forEach(row => {
        // Row click = fill
        row.addEventListener('click', async (e) => {
          if (e.target.closest('[data-copy-id],[data-more-id]')) return;
          const id = row.dataset.fillId;
          const totpSecret = row.dataset.totpSecret || null;
          closePanel();
          const fill = await chrome.runtime.sendMessage({ type: 'FILL_CONFIRMED', credentialId: id });
          if (fill?.success) {
            fillFields(triggerInput, fill);
            // Copy TOTP to clipboard — prefer server value, fall back to client gen
            const totpVal = fill.totp || (totpSecret ? await _genTotp(totpSecret).catch(() => null) : null);
            if (totpVal) navigator.clipboard.writeText(totpVal).catch(() => {});
          }
        });
      });

      list.querySelectorAll('[data-copy-id]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
          e.stopPropagation();
          const id = btn.dataset.copyId;
          const fill = await chrome.runtime.sendMessage({ type: 'FILL_CONFIRMED', credentialId: id });
          if (fill?.password) {
            await navigator.clipboard.writeText(fill.password).catch(() => {});
            btn.innerHTML = CHECK_ICON_SVG;
            btn.style.color = '#10B981';
            setTimeout(() => { if (btn.isConnected) { btn.innerHTML = COPY_ICON_SVG; btn.style.color = ''; } }, 1500);
          }
        });
      });

      list.querySelectorAll('[data-more-id]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
          e.stopPropagation();
          closeDropdowns();
          const id        = btn.dataset.moreId;
          const totpSec   = btn.dataset.totpSecret || null;
          const row       = btn.closest('[data-fill-id]');
          const dkBg      = _panelDark ? '#1E293B' : '#FFFFFF';
          const dkBorder  = _panelDark ? 'rgba(255,255,255,0.10)' : 'rgba(64,124,202,0.16)';
          const dkShadow  = _panelDark ? '0 8px 24px rgba(0,0,0,0.5)' : '0 8px 24px rgba(30,41,59,0.14)';
          const dkClr     = _panelDark ? '#e2e8f0' : '#1A1D26';
          const dkSub     = _panelDark ? '#64748b' : '#94a3b8';
          const dkHover   = _panelDark ? 'rgba(64,124,202,0.14)' : 'rgba(64,124,202,0.08)';

          const menu = document.createElement('div');
          menu.setAttribute('data-fortress-dropdown', '1');
          Object.assign(menu.style, {
            position: 'absolute', right: '8px', zIndex: '9999',
            background: dkBg, border: `1px solid ${dkBorder}`, borderRadius: '10px',
            boxShadow: dkShadow, padding: '4px', minWidth: '160px',
            fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
            fontSize: '12px', color: dkClr,
          });

          const mkMenuItem = (label, danger) => {
            const item = document.createElement('div');
            Object.assign(item.style, {
              padding: '7px 10px', borderRadius: '7px', cursor: 'pointer',
              color: danger ? '#EF4444' : dkClr, transition: 'background 0.1s',
              display: 'flex', alignItems: 'center', gap: '8px',
            });
            item.innerHTML = label;
            item.addEventListener('mouseenter', () => item.style.background = danger ? 'rgba(239,68,68,0.09)' : dkHover);
            item.addEventListener('mouseleave', () => item.style.background = 'transparent');
            return item;
          };

          // Fill
          const fillItem = mkMenuItem(
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4z"/></svg> Fill'
          );
          fillItem.addEventListener('click', async (ev) => {
            ev.stopPropagation(); menu.remove();
            const fill = await chrome.runtime.sendMessage({ type: 'FILL_CONFIRMED', credentialId: id });
            if (fill?.success) {
              fillFields(triggerInput, fill);
              const totpVal = fill.totp || (totpSec ? await _genTotp(totpSec).catch(() => null) : null);
              if (totpVal) navigator.clipboard.writeText(totpVal).catch(() => {});
            }
            closePanel();
          });
          menu.appendChild(fillItem);

          // Separator
          const sep = () => { const d = document.createElement('div'); Object.assign(d.style, { borderTop: `1px solid ${dkBorder}`, margin: '3px 0' }); return d; };
          menu.appendChild(sep());

          // Copy Username
          const copyUser = mkMenuItem(
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg> Copy Username'
          );
          copyUser.addEventListener('click', async (ev) => {
            ev.stopPropagation(); menu.remove();
            const fill = await chrome.runtime.sendMessage({ type: 'FILL_CONFIRMED', credentialId: id });
            if (fill?.username) navigator.clipboard.writeText(fill.username).catch(() => {});
          });
          menu.appendChild(copyUser);

          // Copy Password
          const copyPw = mkMenuItem(
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg> Copy Password'
          );
          copyPw.addEventListener('click', async (ev) => {
            ev.stopPropagation(); menu.remove();
            const fill = await chrome.runtime.sendMessage({ type: 'FILL_CONFIRMED', credentialId: id });
            if (fill?.password) navigator.clipboard.writeText(fill.password).catch(() => {});
          });
          menu.appendChild(copyPw);

          // Copy OTP (only if totpSecret present)
          if (totpSec) {
            const copyOtp = mkMenuItem(
              '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><rect x="5" y="2" width="14" height="20" rx="2"/><line x1="12" y1="18" x2="12.01" y2="18"/></svg> Copy OTP Code'
            );
            copyOtp.addEventListener('click', async (ev) => {
              ev.stopPropagation(); menu.remove();
              const code = await _genTotp(totpSec).catch(() => null);
              if (code) navigator.clipboard.writeText(code).catch(() => {});
            });
            menu.appendChild(copyOtp);
          }

          menu.appendChild(sep());

          // Disable (hide this site's suggestion)
          const disableItem = mkMenuItem(
            '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" width="13" height="13"><circle cx="12" cy="12" r="10"/><line x1="4.93" y1="4.93" x2="19.07" y2="19.07"/></svg> Disable for this site',
            true
          );
          disableItem.addEventListener('click', (ev) => {
            ev.stopPropagation(); menu.remove();
            if (row) { row.style.opacity = '0.35'; row.style.pointerEvents = 'none'; }
          });
          menu.appendChild(disableItem);

          // Position below the button
          const btnRect = btn.getBoundingClientRect();
          const listRect = list.getBoundingClientRect();
          menu.style.top = `${btnRect.bottom - listRect.top + list.scrollTop + 4}px`;
          list.style.position = 'relative';
          list.appendChild(menu);

          setTimeout(() => document.addEventListener('click', function closeMenu(ev) {
            if (!menu.contains(ev.target)) { menu.remove(); document.removeEventListener('click', closeMenu); }
          }), 0);
        });
      });
    }

    function renderActiveTab() {
      if (!_panel) return;
      const list = _panel.querySelector('[data-fortress-list]');
      if (_vaultLocked) { list.innerHTML = renderLocked(); return; }
      const items = _activeTab === 'matches' ? _matchedItems : _allPanelItems;
      if (items === null) {
        list.innerHTML = `<div style="padding:22px;text-align:center;color:${subColor};"><div style="display:inline-block;width:18px;height:18px;border:2px solid rgba(64,124,202,0.22);border-top-color:#407cca;border-radius:50%;animation:fortress-spin 0.7s linear infinite;"></div></div>`;
        return;
      }
      if (!items.length) { list.innerHTML = renderEmpty(); return; }
      list.innerHTML = items.map(s => renderSuggestionItem(s)).join('');
      attachItemListeners();
      startTotpRings(list);
    }

    function updateTabStyles() {
      if (!_panel) return;
      _panel.querySelectorAll('[data-tab]').forEach(btn => {
        const active = btn.dataset.tab === _activeTab;
        btn.style.color       = active ? '#407cca' : tabInactiveColor;
        btn.style.borderBottom = active ? '2px solid #407cca' : '2px solid transparent';
        btn.style.fontWeight  = active ? '700' : '600';
      });
    }

    _panel.querySelectorAll('[data-tab]').forEach(btn => {
      btn.addEventListener('click', () => {
        _activeTab = btn.dataset.tab;
        updateTabStyles();
        renderActiveTab();
      });
    });

    // Load site-matched credentials
    chrome.runtime.sendMessage({ type: 'GET_CREDENTIALS', url: getPageUrl() }, (resp) => {
      if (!_panel) return;
      if (!resp || resp.isVaultLocked) { _vaultLocked = true; renderActiveTab(); return; }
      const all = resp.suggestions ?? [];
      _matchedItems = all.filter(s => _fieldType === 'card' ? s.type === 'CreditCard' : s.type === 'Login');
      const matchTab = _panel.querySelector('[data-tab="matches"]');
      if (matchTab) matchTab.textContent = `Matches (${_matchedItems.length})`;
      if (_activeTab === 'matches') renderActiveTab();
    });

    // Load all vault credentials in parallel
    chrome.runtime.sendMessage({ type: 'GET_ALL_ITEMS' }, (resp) => {
      if (!_panel) return;
      const all = resp?.suggestions ?? [];
      _allPanelItems = all.filter(s => _fieldType === 'card' ? s.type === 'CreditCard' : s.type === 'Login');
      const allTab = _panel.querySelector('[data-tab="all"]');
      if (allTab) allTab.textContent = `All (${_allPanelItems.length})`;
      if (_activeTab === 'all') renderActiveTab();
    });

    setTimeout(() => {
      document.addEventListener('click', outsideClose, { once: true });
    }, 50);
  }

  function outsideClose(e) {
    if (_panel && !_panel.contains(e.target)) closePanel();
    else if (_panel) document.addEventListener('click', outsideClose, { once: true });
  }

  // -- Suggestion item HTML ---------------------------------------------------

  function renderSuggestionItem(s) {
    const initial = escHtml((s.label?.[0] ?? '?').toUpperCase());
    const fallbackStyle = 'width:32px;height:32px;border-radius:8px;background:linear-gradient(135deg,#407cca,#5a9be0);display:flex;align-items:center;justify-content:center;font-size:13px;font-weight:700;color:#fff;flex-shrink:0;box-shadow:0 2px 8px rgba(64,124,202,0.25);';
    const fallbackDiv   = '<div style=\'' + fallbackStyle + '\'>' + initial + '</div>';
    const icon = s.iconUri && !['creditcardlogo.png', 'addresslogo.png'].includes(s.iconUri)
      ? '<img src="' + escHtml(s.iconUri) + '" width="32" height="32" style="border-radius:8px;object-fit:cover;flex-shrink:0;box-shadow:0 2px 6px rgba(0,0,0,0.12);" onerror="this.parentNode.innerHTML=\'' + fallbackDiv.replace(/\\/g, '\\\\').replace(/'/g, "\\'") + '\'">'
      : '<div style="' + fallbackStyle + '">' + initial + '</div>';

    const labelColor = _panelDark ? '#F1F5F9' : '#1A1D26';
    const subClr     = _panelDark ? '#64748b'  : '#6B7280';
    const hoverBg    = _panelDark ? 'rgba(64,124,202,0.12)' : 'rgba(64,124,202,0.08)';
    const btnBg      = _panelDark ? 'rgba(255,255,255,0.07)' : 'rgba(64,124,202,0.07)';
    const btnBorder  = _panelDark ? 'rgba(255,255,255,0.10)' : 'rgba(64,124,202,0.20)';
    const btnColor   = _panelDark ? '#94a3b8' : '#407cca';

    const CIRC       = 75.40; // 2π * 12
    const initSecs   = _totpSecs();
    const initOffset = +(CIRC * (1 - initSecs / 30)).toFixed(2);
    const arcColor   = initSecs <= 5 ? '#EF4444' : initSecs <= 10 ? '#F59E0B' : '#407cca';

    const totpHtml = s.totpSecret
      ? '<div data-fortress-totp-secret="' + escHtml(s.totpSecret) + '" style="display:flex;align-items:center;gap:5px;flex-shrink:0;">' +
          '<div style="position:relative;width:28px;height:28px;flex-shrink:0;">' +
            '<svg viewBox="0 0 30 30" width="28" height="28" style="transform:rotate(-90deg);overflow:visible;display:block;">' +
              '<circle cx="15" cy="15" r="12" fill="none" stroke="rgba(128,128,128,0.22)" stroke-width="3"/>' +
              '<circle data-fortress-arc cx="15" cy="15" r="12" fill="none" stroke="' + arcColor + '" stroke-width="3" stroke-linecap="round" stroke-dasharray="' + CIRC + '" stroke-dashoffset="' + initOffset + '" style="transition:stroke-dashoffset 0.9s linear,stroke 0.4s;"/>' +
            '</svg>' +
            '<div data-fortress-secs style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;font-size:8px;font-weight:800;color:' + arcColor + ';">' + initSecs + '</div>' +
          '</div>' +
          '<span data-fortress-code style="font-size:11px;font-weight:700;font-variant-numeric:tabular-nums;letter-spacing:0.06em;color:' + arcColor + ';min-width:44px;">\u00B7\u00B7\u00B7 \u00B7\u00B7\u00B7</span>' +
        '</div>'
      : '';

    return (
      '<div data-fill-id="' + s.id + '" data-totp-secret="' + escHtml(s.totpSecret || '') + '" style="display:flex;align-items:center;gap:10px;padding:9px 8px;border-radius:10px;cursor:pointer;transition:background 0.12s;margin-bottom:2px;"' +
      ' onmouseover="this.style.background=\'' + hoverBg + '\'"' +
      ' onmouseout="this.style.background=\'transparent\'">' +
        icon +
        '<div style="flex:1;min-width:0;">' +
          '<div style="font-weight:600;font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;color:' + labelColor + ';">' + escHtml(s.label) + '</div>' +
          '<div style="font-size:11px;color:' + subClr + ';white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">' + escHtml(s.username) + '</div>' +
        '</div>' +
        totpHtml +
        '<div style="display:flex;gap:4px;flex-shrink:0;">' +
          '<button data-copy-id="' + s.id + '" title="Copy password"' +
          ' style="background:' + btnBg + ';border:1px solid ' + btnBorder + ';border-radius:6px;padding:4px 5px;cursor:pointer;color:' + btnColor + ';display:flex;align-items:center;justify-content:center;line-height:0;transition:all 0.12s;"' +
          ' onmouseover="this.style.background=\'rgba(64,124,202,0.18)\';this.style.color=\'#5a9be0\'"' +
          ' onmouseout="this.style.background=\'' + btnBg + '\';this.style.color=\'' + btnColor + '\'">' +
          COPY_ICON_SVG + '</button>' +
          '<button data-more-id="' + s.id + '" data-totp-secret="' + escHtml(s.totpSecret || '') + '" title="More options"' +
          ' style="background:' + btnBg + ';border:1px solid ' + btnBorder + ';border-radius:6px;padding:4px 5px;cursor:pointer;color:' + btnColor + ';display:flex;align-items:center;justify-content:center;line-height:0;transition:all 0.12s;"' +
          ' onmouseover="this.style.background=\'rgba(64,124,202,0.18)\';this.style.color=\'#5a9be0\'"' +
          ' onmouseout="this.style.background=\'' + btnBg + '\';this.style.color=\'' + btnColor + '\'">' +
          MORE_ICON_SVG + '</button>' +
        '</div>' +
      '</div>'
    );
  }

  function renderLocked() {
    const color = _panelDark ? '#64748b' : '#94a3b8';
    const subColor = _panelDark ? '#94a3b8' : '#5E6578';
    return (
      `<div style="padding:22px;text-align:center;">` +
      `<div style="font-size:26px;margin-bottom:8px;">&#128274;</div>` +
      `<div style="font-weight:600;font-size:12px;color:${subColor};">Vault is locked</div>` +
      `<div style="font-size:11px;margin-top:4px;color:${color};">Open Fortress to unlock</div>` +
      '</div>'
    );
  }

  function renderEmpty() {
    const color = _panelDark ? '#64748b' : '#94a3b8';
    const subColor = _panelDark ? '#94a3b8' : '#5E6578';
    return (
      `<div style="padding:22px;text-align:center;">` +
      `<div style="font-size:22px;margin-bottom:8px;">&#128269;</div>` +
      `<div style="font-size:12px;font-weight:600;color:${subColor};">No matching credentials</div>` +
      `<div style="font-size:11px;margin-top:3px;color:${color};">Try from the Fortress popup</div>` +
      '</div>'
    );
  }

  function startTotpRings(list) {
    if (_ringInterval) { clearInterval(_ringInterval); _ringInterval = null; }
    const wrappers = list.querySelectorAll('[data-fortress-totp-secret]');
    if (!wrappers.length) return;
    async function tick() {
      const secs   = _totpSecs();
      const CIRC   = 75.40;
      const offset = +(CIRC * (1 - secs / 30)).toFixed(2);
      const color  = secs <= 5 ? '#EF4444' : secs <= 10 ? '#F59E0B' : '#407cca';
      wrappers.forEach(async (wrap) => {
        if (!wrap.isConnected) return;
        const arc    = wrap.querySelector('[data-fortress-arc]');
        const secsEl = wrap.querySelector('[data-fortress-secs]');
        const codeEl = wrap.querySelector('[data-fortress-code]');
        if (arc)    { arc.setAttribute('stroke-dashoffset', offset); arc.style.stroke = color; }
        if (secsEl) { secsEl.textContent = secs; secsEl.style.color = color; }
        if (codeEl) {
          try {
            const c = await _genTotp(wrap.dataset.fortressTotpSecret);
            codeEl.textContent = c.slice(0, 3) + ' ' + c.slice(3);
            codeEl.style.color = color;
          } catch {}
        }
      });
    }
    tick();
    _ringInterval = setInterval(tick, 1000);
  }

  // -- Fill fields ------------------------------------------------------------

  function fillFields(triggerInput, fill) {
    const container = triggerInput.closest('form') ?? document;

    if (isPasswordField(triggerInput)) {
      setVal(triggerInput, fill.password ?? '');
    const usernameEl = findUsernameField(container, triggerInput);
      setVal(usernameEl, fill.username ?? '');
    } else {
      setVal(triggerInput, fill.username ?? '');
      setVal(findPasswordField(container), fill.password ?? '');
    }

    if (fill.cardNumber) {
      const cardEl = container.querySelector('input[autocomplete*="cc-number"], input[name*="card"]');
      setVal(cardEl, fill.cardNumber);
    }

    if (fill.totp) navigator.clipboard.writeText(fill.totp).catch(() => {});
  }

  function findPasswordField(container) {
    return container.querySelector('input[type="password"]');
  }

  function findUsernameField(container, skip) {
    return [...container.querySelectorAll('input')].find(i => i !== skip && isUsernameField(i)) ?? null;
  }

  // ===========================================================================
  //  SAVE PROMPT BANNER
  // ===========================================================================

  let _capturedCredential = null;
  let _savePromptBanner   = null;

  function captureFormCredentials(form) {
    const pwEl = form?.querySelector('input[type="password"]') ?? document.querySelector('input[type="password"]');
    const unEl = findUsernameField(form ?? document, pwEl);
    if (!pwEl?.value) return null;
  return {
      url:      window.location.origin,
      username: unEl?.value ?? '',
      password: pwEl.value,
    };
  }

  function showSavePrompt(cred) {
    if (_savePromptBanner) _savePromptBanner.remove();
    _savePromptBanner = document.createElement('div');

    const _bDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    const bannerBg     = _bDark ? 'linear-gradient(160deg, #1E293B 0%, #0F172A 100%)' : '#FFFFFF';
    const bannerBorder = _bDark ? 'rgba(64,124,202,0.40)' : 'rgba(64,124,202,0.22)';
    const bannerShadow = _bDark
      ? '0 20px 60px rgba(0,0,0,0.55), 0 0 0 1px rgba(64,124,202,0.08)'
      : '0 8px 32px rgba(30,41,59,0.13), 0 0 0 1px rgba(64,124,202,0.06)';
    const bannerColor  = _bDark ? '#e2e8f0' : '#1A1D26';
    const subTextColor = _bDark ? '#94a3b8' : '#64748b';
    const dismissBg    = _bDark ? 'rgba(255,255,255,0.06)' : 'rgba(0,0,0,0.04)';
    const dismissBrd   = _bDark ? 'rgba(255,255,255,0.08)' : 'rgba(0,0,0,0.10)';
    const updateBg     = _bDark ? 'rgba(255,255,255,0.06)' : 'rgba(64,124,202,0.07)';
    const updateBrd    = 'rgba(64,124,202,0.25)';
    const neverBrd     = _bDark ? 'rgba(255,255,255,0.10)' : 'rgba(0,0,0,0.10)';

    Object.assign(_savePromptBanner.style, {
      position:       'fixed',
      top:            '20px',
      right:          '20px',
      zIndex:         '2147483647',
      background:     bannerBg,
      border:         `1px solid ${bannerBorder}`,
      borderRadius:   '18px',
      padding:        '0',
      boxShadow:      bannerShadow,
      fontFamily:     '-apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif',
      fontSize:       '13px',
      color:          bannerColor,
      width:          '340px',
      overflow:       'hidden',
      backdropFilter: 'blur(32px) saturate(1.6)',
      animation:      'fortress-slide-down 0.26s cubic-bezier(0.34,1.1,0.64,1)',
    });

    const hostname = escHtml(new URL(cred.url).hostname);
    const username = escHtml(cred.username || '');

    const accentLine = `linear-gradient(90deg, #407cca 0%, #7ab3f0 100%)`;

    // \u00B7 = middle dot  \u2715 = x close
    _savePromptBanner.innerHTML =
      `<div style="height:3px;background:${accentLine};border-radius:18px 18px 0 0;"></div>` +
      `<div style="padding:16px 16px 14px;">` +
        `<div style="display:flex;align-items:flex-start;gap:12px;margin-bottom:14px;">` +
          `<div style="width:44px;height:44px;border-radius:12px;overflow:hidden;flex-shrink:0;box-shadow:0 4px 12px rgba(64,124,202,0.25);">` +
            `<img src="${chrome.runtime.getURL('assets/icons/icon48.png')}" width="44" height="44" style="display:block;width:44px;height:44px;">` +
          `</div>` +
          `<div style="flex:1;min-width:0;padding-top:2px;">` +
            `<div style="font-weight:700;font-size:14px;color:${bannerColor};line-height:1.3;letter-spacing:-0.01em;">Save to Fortress?</div>` +
            `<div style="font-size:12px;color:${subTextColor};margin-top:2px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${username ? `<span style="font-weight:500;">${username}</span> <span style="opacity:0.55;">\u00B7</span> ` : ''}${hostname}</div>` +
          `</div>` +
          `<button data-dismiss style="width:26px;height:26px;background:${dismissBg};border:1px solid ${dismissBrd};border-radius:8px;cursor:pointer;color:${subTextColor};font-size:14px;line-height:1;display:flex;align-items:center;justify-content:center;flex-shrink:0;transition:background 0.15s;" title="Dismiss">\u2715</button>` +
        `</div>` +
        `<div style="display:flex;gap:8px;">` +
          `<button data-save style="flex:1.4;padding:9px 8px;background:linear-gradient(135deg,#3d78c9,#5a9be0);color:#fff;border:none;border-radius:10px;font-size:12.5px;font-weight:600;cursor:pointer;letter-spacing:0.02em;box-shadow:0 4px 16px rgba(64,124,202,0.36);transition:filter 0.15s;">Save Login</button>` +
          `<button data-update style="flex:1;padding:9px 8px;background:${updateBg};color:#5a9be0;border:1px solid ${updateBrd};border-radius:10px;font-size:12.5px;font-weight:600;cursor:pointer;transition:background 0.15s;">Update</button>` +
          `<button data-never style="padding:9px 11px;background:transparent;color:${subTextColor};border:1px solid ${neverBrd};border-radius:10px;font-size:12.5px;cursor:pointer;transition:background 0.15s;">Never</button>` +
        `</div>` +
      `</div>`;

  document.body.appendChild(_savePromptBanner);

    _savePromptBanner.querySelector('[data-dismiss]').addEventListener('click', dismissSavePrompt);
    _savePromptBanner.querySelector('[data-never]').addEventListener('click', dismissSavePrompt);

    // \u2713 = check mark
    _savePromptBanner.querySelector('[data-save]').addEventListener('click', async () => {
      const r = await chrome.runtime.sendMessage({ type: 'SAVE_LOGIN', ...cred }).catch(() => ({ error: 'Extension error' }));
      showToastOverlay(r?.error ? 'Vault is locked \u2014 open Fortress first' : 'Saved to Fortress \u2713');
      dismissSavePrompt();
    });

    _savePromptBanner.querySelector('[data-update]').addEventListener('click', async () => {
      const r = await chrome.runtime.sendMessage({ type: 'SAVE_LOGIN', ...cred }).catch(() => ({ error: 'Extension error' }));
      showToastOverlay(r?.error ? 'Vault is locked \u2014 open Fortress first' : 'Updated in Fortress \u2713');
      dismissSavePrompt();
    });

    // Auto-dismiss after 15 s
    setTimeout(dismissSavePrompt, 15000);
  }

  function dismissSavePrompt() {
    if (_savePromptBanner) {
      _savePromptBanner.style.opacity    = '0';
      _savePromptBanner.style.transition = 'opacity 0.2s';
      setTimeout(() => { _savePromptBanner?.remove(); _savePromptBanner = null; }, 220);
    }
  }

  function showToastOverlay(msg) {
    const t = document.createElement('div');
    Object.assign(t.style, {
      position:       'fixed',
      bottom:         '24px',
      right:      '16px',
      zIndex:         '2147483647',
      background:     'rgba(16,185,129,0.15)',
      border:   '1px solid rgba(16,185,129,0.4)',
      borderRadius:   '10px',
    padding:   '8px 14px',
      fontFamily:     '-apple-system, BlinkMacSystemFont, sans-serif',
      fontSize:     '12px',
      fontWeight: '600',
 color:          '#10B981',
 backdropFilter: 'blur(12px)',
      animation:      'fortress-slide-up 0.18s ease',
    });
    t.textContent = msg;
    document.body.appendChild(t);
setTimeout(() => t.remove(), 2400);
  }

  // -- Form submit detection --------------------------------------------------

  function onFormSubmit(e) {
    const form = e.target.tagName === 'FORM' ? e.target : null;
    const cred = captureFormCredentials(form);
    if (!cred?.password) return;
    _capturedCredential = cred;
    showSavePrompt(cred);
  }

  document.addEventListener('submit', onFormSubmit, true);

  // Also catch button clicks that look like login submissions
  document.addEventListener('click', (e) => {
    const btn = e.target.closest('button, input[type="submit"], [type="button"]');
    if (!btn) return;
    const text = (btn.textContent ?? '').toLowerCase();
    const isLogin = ['login', 'sign in', 'log in', 'signin', 'continue', 'submit'].some(k => text.includes(k));
    if (!isLogin) return;
    const form = btn.closest('form');
    const cred = captureFormCredentials(form);
    if (cred?.password) {
      _capturedCredential = cred;
      setTimeout(() => showSavePrompt(cred), 600);
    }
  }, true);

  // ===========================================================================
  //  DOM SCANNING
  // ===========================================================================

  function scanAndInject() {
    document.querySelectorAll('input').forEach(el => {
      if (isPasswordField(el) || isUsernameField(el)) injectButton(el);
    });
  }

  scanAndInject();

  const obs = new MutationObserver(() => scanAndInject());
  obs.observe(document.body, { childList: true, subtree: true });

  // -- Message listener -------------------------------------------------------

chrome.runtime.onMessage.addListener((msg) => {
    if (msg.type === 'TRIGGER_AUTOFILL') {
      const pw = document.querySelector('input[type="password"]');
      const un = document.querySelector('input[type="email"],input[autocomplete*="username"]');
      openInlinePanel(pw ?? un ?? document.activeElement);
    }
  if (msg.type === 'TRIGGER_SAVE_PROMPT' && _capturedCredential) {
      showSavePrompt(_capturedCredential);
    }
});

})();
