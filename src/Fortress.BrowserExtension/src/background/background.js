// ─────────────────────────────────────────────────────────────────────────────
// background.js  —  Fortress MV3 Service Worker  v1.1
// ─────────────────────────────────────────────────────────────────────────────
// ── DEMO MODE ─────────────────────────────────────────────────────────────────
// Set to true to use hard-coded fake data instead of the native host.
// Flip back to false once the Fortress service is running.
const DEMO_MODE = false;

const DEMO_CREDENTIALS = [
  // === Logins ===
  {
    id: 'demo-1',
    type: 'Login',
    label: 'GitHub',
    username: 'dev@example.com',
    iconUri: 'https://www.google.com/s2/favicons?domain=github.com&sz=64',
    isFavorite: true,
    isBreached: false,
    matchScore: 95,
    url: 'https://github.com',
  },
  {
    id: 'demo-2',
    type: 'Login',
    label: 'Google Account',
    username: 'myaccount@gmail.com',
    iconUri: 'https://www.google.com/s2/favicons?domain=google.com&sz=64',
    isFavorite: false,
    isBreached: false,
    matchScore: 90,
    url: 'https://accounts.google.com',
    totpSecret: 'JBSWY3DPEHPK3PXP',
  },
  {
    id: 'demo-3',
    type: 'Login',
    label: 'Outlook / Microsoft',
    username: 'work@outlook.com',
    iconUri: 'https://www.google.com/s2/favicons?domain=outlook.com&sz=64',
    isFavorite: false,
    isBreached: true,
    matchScore: 82,
    url: 'https://outlook.com',
  },
  {
    id: 'demo-4',
    type: 'Login',
    label: 'Amazon',
    username: 'shopper@amazon.com',
    iconUri: 'https://www.google.com/s2/favicons?domain=amazon.com&sz=64',
    isFavorite: false,
    isBreached: false,
    matchScore: 35,
    url: 'https://amazon.com',
  },
  {
    id: 'demo-5',
    type: 'Login',
    label: 'Twitter / X',
    username: 'myhandle@twitter.com',
    iconUri: 'https://www.google.com/s2/favicons?domain=x.com&sz=64',
    isFavorite: true,
    isBreached: false,
    matchScore: 88,
    url: 'https://x.com',
  },
  {
    id: 'demo-6',
    type: 'Login',
    label: 'Netflix',
    username: 'myaccount@gmail.com',
    iconUri: 'https://www.google.com/s2/favicons?domain=netflix.com&sz=64',
    isFavorite: false,
    isBreached: false,
    matchScore: 25,
    url: 'https://netflix.com',
  },
  {
    id: 'demo-7',
    type: 'Login',
    label: 'Notion',
    username: 'dev@example.com',
    iconUri: 'https://www.google.com/s2/favicons?domain=notion.so&sz=64',
    isFavorite: false,
    isBreached: false,
    matchScore: 78,
    url: 'https://notion.so',
  },
  {
    id: 'demo-8',
    type: 'Login',
    label: 'Figma',
    username: 'designer@example.com',
    iconUri: 'https://www.google.com/s2/favicons?domain=figma.com&sz=64',
    isFavorite: false,
    isBreached: false,
    matchScore: 92,
    url: 'https://figma.com',
  },
  // === Credit Cards ===
  {
    id: 'demo-card-1',
    type: 'CreditCard',
    label: 'Visa Debit',
    username: 'John Doe',
    cardholderName: 'John Doe',
    cardNumber: '4111111111111111',
    cardExpiry: '12/27',
    iconUri: null,
    isFavorite: false,
    isBreached: false,
  },
  {
    id: 'demo-card-2',
    type: 'CreditCard',
    label: 'Mastercard Business',
    username: 'John Doe',
    cardholderName: 'John Doe',
    cardNumber: '5500005555555559',
    cardExpiry: '08/26',
    iconUri: null,
    isFavorite: true,
    isBreached: false,
  },
  {
    id: 'demo-auth-1',
    type: 'Authenticator',
    label: 'GitHub 2FA',
    username: 'dev@example.com',
    totpSecret: 'JBSWY3DPEHPK3PXP',
    authType: 'TOTP',
    digits: 6,
    period: 30,
    algorithm: 'SHA1',
    iconUri: 'https://www.google.com/s2/favicons?domain=github.com&sz=64',
    isFavorite: false,
    isBreached: false,
  },
  {
    id: 'demo-auth-2',
    type: 'Authenticator',
    label: 'Dropbox 2FA',
    username: 'user@dropbox.com',
    totpSecret: 'HXDMVJECJJWSRB3HWIZR4IFUGFTMXBOZ',
    authType: 'TOTP',
    digits: 6,
    period: 30,
    algorithm: 'SHA1',
    iconUri: 'https://www.google.com/s2/favicons?domain=dropbox.com&sz=64',
    isFavorite: false,
    isBreached: false,
  },
];

// ── Demo Secure Items ─────────────────────────────────────────────────────
// Separate collection for the SecureItem system (documents, Wi-Fi, SSH, etc.)
let DEMO_SECURE_ITEMS = [
  // === Identity ===
  {
    id: 'demo-si-1',
    itemType: 'Identity',
    label: 'Personal Profile',
    firstName: 'John', lastName: 'Doe',
    email: 'john.doe@example.com', phone: '+1 555 000 1234',
    company: 'Acme Corp',
    addressLine1: '123 Main St', addressLine2: '',
    city: 'Springfield', state: 'IL', country: 'US', postalCode: '62701',
    isFavorite: false,
  },
  // === ID Card ===
  {
    id: 'demo-si-2',
    itemType: 'IdCard',
    label: 'National ID',
    fullName: 'John Doe',
    dateOfBirth: '1990-01-15',
    nationality: 'US',
    number: '123456789',
    issuingCountry: 'US',
    issuedDate: '2018-03-01',
    expiryDate: '2028-03-01',
    isFavorite: false,
  },
  // === Passport ===
  {
    id: 'demo-si-3',
    itemType: 'Passport',
    label: 'US Passport',
    fullName: 'John Doe',
    dateOfBirth: '1990-01-15',
    nationality: 'US',
    number: 'A12345678',
    issuingCountry: 'US',
    issuedDate: '2020-06-10',
    expiryDate: '2030-06-10',
    isFavorite: true,
  },
  // === Driver's License ===
  {
    id: 'demo-si-4',
    itemType: 'DriversLicense',
    label: "Driver's License",
    fullName: 'John Doe',
    dateOfBirth: '1990-01-15',
    nationality: 'US',
    number: 'D12345678',
    issuingCountry: 'US',
    issuedDate: '2019-05-20',
    expiryDate: '2025-05-20',
    isFavorite: false,
  },
  // === Wi-Fi ===
  {
    id: 'demo-si-5',
    itemType: 'WiFi',
    label: 'Home Wi-Fi',
    ssid: 'MyHomeNetwork',
    wifiSecurity: 'WPA3',
    password: 'SuperSecret123!',
    isFavorite: true,
  },
  // === SSH ===
  {
    id: 'demo-si-6',
    itemType: 'Ssh',
    label: 'Production Server',
    host: 'prod-server-01.example.com',
    port: '22',
    username: 'deploy',
    sshPassword: '',
    privateKey: '-----BEGIN RSA PRIVATE KEY-----\n(demo key)\n-----END RSA PRIVATE KEY-----',
    keyFingerprint: 'SHA256:abc123xyz',
    isFavorite: false,
  },
  // === Secure Note ===
  {
    id: 'demo-si-7',
    itemType: 'SecureNote',
    label: 'Recovery Codes',
    noteContent: 'GitHub: 1a2b-3c4d\nGoogle: 5e6f-7g8h',
    isFavorite: false,
  },
];

async function handleMessageDemo(msg) {
  switch (msg.type) {
    case 'GET_STATUS':
    return { ok: true, isUnlocked: true, deviceId: 'demo-device' };
    case 'UNLOCK':
    case 'UNLOCK_PIN':
      setBadgeUnlocked();
      return { ok: true, token: 'demo-token' };
    case 'LOCK':
      setBadgeLocked();
      return { ok: true };
    case 'GET_CREDENTIALS': {
      const q = (msg.url ?? '').toLowerCase();
      const logins = DEMO_CREDENTIALS.filter(c => c.type === 'Login');
      const matches = q
        ? logins.filter(c => c.label.toLowerCase().includes(q) || (c.username ?? '').toLowerCase().includes(q))
        : logins;
      return { isVaultLocked: false, suggestions: matches };
    }
    case 'FILL_CONFIRMED': {
      const cred = DEMO_CREDENTIALS.find(c => c.id === msg.credentialId);
      return { success: true, username: cred?.username ?? 'demo@example.com', password: 'Demo@P@ssw0rd!' };
    }
    case 'GET_ALL_ITEMS':
    case 'SEARCH_VAULT':
    return { isVaultLocked: false, suggestions: DEMO_CREDENTIALS };
    case 'SAVE_LOGIN':
      console.log('[Fortress DEMO] Save login:', msg);
      return { ok: true };
    case 'SAVE_AUTHENTICATOR':
      console.log('[Fortress DEMO] Save authenticator:', msg);
      return { ok: true };
    case 'UPDATE_LOGIN':
    case 'UPDATE_CARD':
    case 'UPDATE_IDENTITY':
    case 'UPDATE_NOTE':
    case 'UPDATE_AUTHENTICATOR':
      console.log('[Fortress DEMO] Update item:', msg);
      return { ok: true };
    case 'DELETE_ITEM':
      console.log('[Fortress DEMO] Delete item:', msg.credentialId);
      return { ok: true };
    case 'TOGGLE_FAVORITE':
      return { ok: true };
    // ── Secure Items (demo) ──────────────────────────────────────────────────
    case 'GET_SECURE_ITEMS':
      return { ok: true, items: DEMO_SECURE_ITEMS };
    case 'SAVE_SECURE_ITEM':
      console.log('[Fortress DEMO] Save secure item:', msg);
      DEMO_SECURE_ITEMS.push({ id: 'demo-si-' + Date.now(), ...msg });
      return { ok: true };
    case 'UPDATE_SECURE_ITEM':
      console.log('[Fortress DEMO] Update secure item:', msg);
      DEMO_SECURE_ITEMS = DEMO_SECURE_ITEMS.map(i => i.id === msg.id ? { ...i, ...msg } : i);
      return { ok: true };
    case 'DELETE_SECURE_ITEM':
      console.log('[Fortress DEMO] Delete secure item:', msg.id);
      DEMO_SECURE_ITEMS = DEMO_SECURE_ITEMS.filter(i => i.id !== msg.id);
      return { ok: true };
    case 'GENERATE_PASSWORD':
      return { ok: true, password: generatePassword(msg.opts ?? {}) };
    case 'RESCHEDULE_AUTOLOCK':
      return { ok: true };
    default:
      return { ok: true };
  }
}
// ─────────────────────────────────────────────────────────────────────────────
const HOST       = 'com.fortress.vault';
const LOCK_ALARM = 'fortress-auto-lock';

function _getDomain(url) {
  try { return new URL(url.startsWith('http') ? url : `https://${url}`).hostname.replace(/^www\./, ''); }
  catch { return url; }
}

let _port    = null;
let _sessionToken = null;
let _pendingMap   = new Map();
let _msgId        = 0;
let _keepAliveTimer = null;

// Keep the MV3 service worker alive while the native port is open.
// Chrome terminates idle SWs after ~30 s; this ping resets that timer.
function _startKeepAlive() {
  if (_keepAliveTimer) return;
  _keepAliveTimer = setInterval(() => chrome.runtime.getPlatformInfo(() => {}), 20_000);
}
function _stopKeepAlive() {
  clearInterval(_keepAliveTimer);
  _keepAliveTimer = null;
}

// ── Standalone vault (extension-only mode, no native host) ──────────────────
const SA_CONFIG_KEY = 'sa_config';
const SA_VAULT_KEY  = 'sa_vault';

let _saKey = null; // In-memory AES-GCM CryptoKey — cleared on lock

async function _saGetMode() {
  const r = await chrome.storage.local.get('fortress_mode');
  return r.fortress_mode ?? null; // 'standalone' | 'service' | null
}

async function _saGetConfig() {
  const r = await chrome.storage.local.get(SA_CONFIG_KEY);
  return r[SA_CONFIG_KEY] ?? null;
}
async function _saSetConfig(cfg) {
  await chrome.storage.local.set({ [SA_CONFIG_KEY]: cfg });
}

async function _saGetVaultEnc() {
  const r = await chrome.storage.local.get(SA_VAULT_KEY);
  return r[SA_VAULT_KEY] ?? null;
}
async function _saSetVaultEnc(enc) {
  await chrome.storage.local.set({ [SA_VAULT_KEY]: enc });
}

// Derive an AES-GCM key from a text secret + salt (Uint8Array | number[])
async function _saDeriveKey(secret, salt) {
  const raw = await crypto.subtle.importKey(
    'raw', new TextEncoder().encode(secret), 'PBKDF2', false, ['deriveKey']
  );
  return crypto.subtle.deriveKey(
    { name: 'PBKDF2', salt: new Uint8Array(salt), iterations: 310_000, hash: 'SHA-256' },
    raw,
    { name: 'AES-GCM', length: 256 },
    true,          // extractable — so we can re-import after PIN unlock
    ['encrypt', 'decrypt']
  );
}

async function _saEncrypt(key, data) {
  const iv = crypto.getRandomValues(new Uint8Array(12));
  const ct = await crypto.subtle.encrypt(
    { name: 'AES-GCM', iv },
    key,
    new TextEncoder().encode(JSON.stringify(data))
  );
  return { iv: [...iv], ct: [...new Uint8Array(ct)] };
}

async function _saDecrypt(key, enc) {
  const pt = await crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: new Uint8Array(enc.iv) },
    key,
    new Uint8Array(enc.ct)
  );
  return JSON.parse(new TextDecoder().decode(pt));
}

// Load+decrypt vault. Returns array of item objects.
async function _saLoadItems() {
  if (!_saKey) throw new Error('Vault is locked');
  const enc = await _saGetVaultEnc();
  if (!enc) return [];
  return _saDecrypt(_saKey, enc);
}

// Encrypt+save vault items.
async function _saSaveItems(items) {
  if (!_saKey) throw new Error('Vault is locked');
  const enc = await _saEncrypt(_saKey, items);
  await _saSetVaultEnc(enc);
}

// GET_STATUS for standalone mode
async function saGetStatus() {
  const cfg = await _saGetConfig();
  if (!cfg?.setupComplete) {
    return { ok: true, isSetupComplete: false, isUnlocked: false, isPinEnabled: false, mode: 'standalone' };
  }
  return {
    ok: true,
    isSetupComplete: true,
    isUnlocked: !!_saKey,
    isPinEnabled: !!cfg.pinData,
    isPasswordEnabled: true,
    mode: 'standalone',
    syncProvider: cfg.syncProvider ?? 'local',
  };
}

// STANDALONE_SETUP — called once from the setup wizard
async function saSetup({ password, pin, syncProvider }) {
  // 1. Generate a random vault key
  const vaultKeyRaw = crypto.getRandomValues(new Uint8Array(32));
  const vaultKey = await crypto.subtle.importKey(
    'raw', vaultKeyRaw, { name: 'AES-GCM' }, true, ['encrypt', 'decrypt']
  );
  _saKey = vaultKey;

  // 2. Encrypt vault key with master-password-derived key
  const pwSalt = [...crypto.getRandomValues(new Uint8Array(32))];
  const pwKey  = await _saDeriveKey(password, pwSalt);
  const encVaultKeyWithPw = await _saEncrypt(pwKey, [...vaultKeyRaw]);

  // 3. Optionally encrypt vault key with PIN-derived key
  let pinData = null;
  if (pin) {
    const pinSalt               = [...crypto.getRandomValues(new Uint8Array(32))];
    const pinKey                = await _saDeriveKey(pin, pinSalt);
    const encVaultKeyWithPin    = await _saEncrypt(pinKey, [...vaultKeyRaw]);
    pinData = { pinSalt, encVaultKeyWithPin };
  }

  // 4. Initialise an empty encrypted vault
  await _saSaveItems([]);

  // 5. Persist config
  await _saSetConfig({ setupComplete: true, pwSalt, encVaultKeyWithPw, pinData, syncProvider: syncProvider ?? 'local' });
  await chrome.storage.local.set({ fortress_mode: 'standalone' });
  await persistSession('sa-' + Date.now());
  setBadgeUnlocked();
  return { ok: true };
}

// UNLOCK for standalone mode
async function saUnlock(password) {
  const cfg = await _saGetConfig();
  if (!cfg?.setupComplete) throw new Error('Vault not set up');
  try {
    const pwKey      = await _saDeriveKey(password, cfg.pwSalt);
    const rawArr     = await _saDecrypt(pwKey, cfg.encVaultKeyWithPw);
    const vaultKey   = await crypto.subtle.importKey('raw', new Uint8Array(rawArr), { name: 'AES-GCM' }, true, ['encrypt', 'decrypt']);
    // Verify by decrypting vault (throws if wrong key)
    const enc = await _saGetVaultEnc();
    if (enc) await _saDecrypt(vaultKey, enc);
    _saKey = vaultKey;
    await persistSession('sa-' + Date.now());
    setBadgeUnlocked();
    return { ok: true };
  } catch {
    throw new Error('Incorrect master password');
  }
}

// UNLOCK_PIN for standalone mode
async function saUnlockPin(pin) {
  const cfg = await _saGetConfig();
  if (!cfg?.pinData) throw new Error('PIN not configured');
  try {
    const pinKey    = await _saDeriveKey(pin, cfg.pinData.pinSalt);
    const rawArr    = await _saDecrypt(pinKey, cfg.pinData.encVaultKeyWithPin);
    const vaultKey  = await crypto.subtle.importKey('raw', new Uint8Array(rawArr), { name: 'AES-GCM' }, true, ['encrypt', 'decrypt']);
    const enc = await _saGetVaultEnc();
    if (enc) await _saDecrypt(vaultKey, enc);
    _saKey = vaultKey;
    await persistSession('sa-' + Date.now());
    setBadgeUnlocked();
    return { ok: true };
  } catch {
    throw new Error('Incorrect PIN');
  }
}

// GET_ALL_ITEMS for standalone mode (returns same shape as service mode)
async function saGetAllItems() {
  const items = await _saLoadItems();
  return { isVaultLocked: false, suggestions: items };
}

// Generic standalone item mutation helpers
async function saSaveItem(item) {
  const items = await _saLoadItems();
  const newItem = { ...item, id: crypto.randomUUID(), createdAt: new Date().toISOString(), updatedAt: new Date().toISOString() };
  items.push(newItem);
  await _saSaveItems(items);
  return { ok: true };
}

async function saUpdateItem(id, fields) {
  const items = await _saLoadItems();
  const idx = items.findIndex(i => i.id === id);
  if (idx < 0) throw new Error('Item not found');
  items[idx] = { ...items[idx], ...fields, updatedAt: new Date().toISOString() };
  await _saSaveItems(items);
  return { ok: true };
}

async function saDeleteItem(id) {
  const items = await _saLoadItems();
  const filtered = items.filter(i => i.id !== id);
  await _saSaveItems(filtered);
  return { ok: true };
}

async function saToggleFavorite(id, value) {
  return saUpdateItem(id, { isFavorite: value });
}

// LOCK for standalone mode — just clear the in-memory key
async function saLock() {
  _saKey = null;
  await clearSession();
  setBadgeLocked();
  return { ok: true };
}

// ── Native port ───────────────────────────────────────────────────────────────
function getPort() {
  if (_port) return _port;
  try {
    console.log('[Fortress] connectNative →', HOST);
    _port = chrome.runtime.connectNative(HOST);
    _port.onMessage.addListener(onNativeMessage);
    _port.onDisconnect.addListener(() => {
      const err = chrome.runtime.lastError?.message ?? '(no error)';
      console.warn('[Fortress] native port disconnected:', err);
      _port = null;
      _stopKeepAlive();
      for (const [, { reject }] of _pendingMap)
        reject(new Error('Fortress Service disconnected.'));
      _pendingMap.clear();
    });
    _startKeepAlive();
    console.log('[Fortress] native port connected');
  } catch (e) {
    console.error('[Fortress] connectNative threw:', e);
    _port = null;
  }
  return _port;
}

function onNativeMessage(response) {
  console.log('[Fortress] ← native response:', JSON.stringify(response));
  const id = response._reqId;
  const pending = _pendingMap.get(id);
  if (!pending) { console.warn('[Fortress] no pending for _reqId:', id, '| map:', [..._pendingMap.keys()]); return; }
  _pendingMap.delete(id);
  if (response.success) pending.resolve(response);
  else pending.reject(new Error(response.errorMessage ?? 'Unknown error'));
}

function nativeSend(method, payload = {}, _retried = false) {
  return new Promise((resolve, reject) => {
    const port = getPort();
    if (!port) {
      console.error('[Fortress] nativeSend: no port for', method);
      reject(new Error('Fortress Service is not running. Please launch Fortress and ensure the native host is installed.'));
      return;
    }
    const id = ++_msgId;
    _pendingMap.set(id, { resolve, reject });
    const msg = {
      method,
      sessionToken: _sessionToken ?? '',
      payload: JSON.stringify({ ...payload, _reqId: id }),
    };
    console.log('[Fortress] → native send:', method, '| _reqId:', id);
    port.postMessage(msg);
    setTimeout(() => {
      if (_pendingMap.has(id)) {
        _pendingMap.delete(id);
        console.warn('[Fortress] timeout for', method, '_reqId:', id);
        reject(new Error('Request timed out — Fortress Service did not respond.'));
      }
    }, 10_000);
  }).catch((e) => {
    // If the port was recycled by the MV3 SW lifecycle, retry once with a fresh connection.
    if (!_retried && e.message === 'Fortress Service disconnected.') {
      console.warn('[Fortress] port recycled — retrying', method);
      return nativeSend(method, payload, true);
    }
    throw e;
  });
}

// ── Session ───────────────────────────────────────────────────────────────────
async function persistSession(token) {
  _sessionToken = token;
  await chrome.storage.session.set({ sessionToken: token, unlockedAt: Date.now() });
}
async function clearSession() {
  _sessionToken = null;
  await chrome.storage.session.remove(['sessionToken', 'unlockedAt']);
}
async function restoreSession() {
  const { sessionToken } = await chrome.storage.session.get('sessionToken');
  if (sessionToken) _sessionToken = sessionToken;
}

// ── Auto-lock alarm ───────────────────────────────────────────────────────────
async function scheduleAutoLock(seconds) {
  await chrome.alarms.clear(LOCK_ALARM);
  if (seconds > 0)
    await chrome.alarms.create(LOCK_ALARM, { delayInMinutes: seconds / 60 });
}

chrome.alarms.onAlarm.addListener(async (alarm) => {
  if (alarm.name === LOCK_ALARM) {
    await clearSession();
    await nativeSend('Lock', {}).catch(() => {});
    setBadgeLocked();
  }
});

function _drawLockedIcon() {
  try {
    const canvas = new OffscreenCanvas(128, 128);
    const ctx    = canvas.getContext('2d');
    // Rounded rect background — red
    const r = 24;
    ctx.beginPath();
    ctx.moveTo(r, 0); ctx.lineTo(128 - r, 0);
    ctx.quadraticCurveTo(128, 0, 128, r);
    ctx.lineTo(128, 128 - r);
    ctx.quadraticCurveTo(128, 128, 128 - r, 128);
    ctx.lineTo(r, 128);
    ctx.quadraticCurveTo(0, 128, 0, 128 - r);
    ctx.lineTo(0, r);
    ctx.quadraticCurveTo(0, 0, r, 0);
    ctx.closePath();
    ctx.fillStyle = '#C0392B';
    ctx.fill();
    // Lock body
    ctx.fillStyle = '#FFFFFF';
    const bx = 36, by = 60, bw = 56, bh = 46, br = 8;
    ctx.beginPath();
    ctx.moveTo(bx + br, by); ctx.lineTo(bx + bw - br, by);
    ctx.quadraticCurveTo(bx + bw, by, bx + bw, by + br);
    ctx.lineTo(bx + bw, by + bh - br);
    ctx.quadraticCurveTo(bx + bw, by + bh, bx + bw - br, by + bh);
    ctx.lineTo(bx + br, by + bh);
    ctx.quadraticCurveTo(bx, by + bh, bx, by + bh - br);
    ctx.lineTo(bx, by + br);
    ctx.quadraticCurveTo(bx, by, bx + br, by);
    ctx.closePath();
    ctx.fill();
    // Lock shackle
    ctx.strokeStyle = '#FFFFFF';
    ctx.lineWidth   = 11;
    ctx.lineCap     = 'round';
    ctx.beginPath();
    ctx.arc(64, 58, 20, Math.PI, 0);
    ctx.stroke();
    // Keyhole
    ctx.fillStyle = '#C0392B';
    ctx.beginPath();
    ctx.arc(64, 83, 8, 0, Math.PI * 2);
    ctx.fill();
    ctx.fillRect(60, 83, 8, 14);
    return ctx.getImageData(0, 0, 128, 128);
  } catch { return null; }
}

function setBadgeLocked() {
  chrome.action.setBadgeText({ text: '' });
  const img = _drawLockedIcon();
  if (img) {
    chrome.action.setIcon({ imageData: { 128: img } });
  } else {
    // Fallback if OffscreenCanvas unavailable
    chrome.action.setBadgeText({ text: '🔒' });
    chrome.action.setBadgeBackgroundColor({ color: '#C0392B' });
  }
}
function setBadgeUnlocked() {
  chrome.action.setBadgeText({ text: '' });
  // Restore manifest default icons
  chrome.action.setIcon({
    path: {
      '16':  'assets/icons/icon16.png',
      '32':  'assets/icons/icon32.png',
      '48':  'assets/icons/icon48.png',
      '128': 'assets/icons/icon128.png',
    }
  });
}

// ── Context menus ─────────────────────────────────────────────────────────────
chrome.runtime.onInstalled.addListener(() => {
  chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({ id: 'fortress-fill',     title: 'Fill with Fortress',        contexts: ['editable'] });
    chrome.contextMenus.create({ id: 'fortress-generate', title: 'Generate & Fill Password',  contexts: ['editable'] });
    chrome.contextMenus.create({ id: 'fortress-save',     title: 'Save to Fortress Vault',    contexts: ['editable'] });
    chrome.contextMenus.create({ id: 'fortress-open',     title: 'Open Fortress Vault',       contexts: ['page','frame'] });
  });
});

chrome.contextMenus.onClicked.addListener(async (info, tab) => {
  switch (info.menuItemId) {
    case 'fortress-open':
      // chrome.action.openPopup() requires an active focused window (MV3 restriction).
      // Open the options page instead — works regardless of window focus state.
      chrome.runtime.openOptionsPage();
      break;
    case 'fortress-generate': {
      const pw = generatePassword({ length: 20, upper: true, lower: true, digits: true, symbols: true });
      await chrome.scripting.executeScript({
     target: { tabId: tab.id },
   func: (pw) => {
          const el = document.activeElement;
    if (el && (el.tagName === 'INPUT' || el.isContentEditable)) {
            el.value = pw;
       el.dispatchEvent(new Event('input',  { bubbles: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
  }
        },
        args: [pw],
      }).catch(() => {});
      await chrome.scripting.executeScript({
     target: { tabId: tab.id },
        func: (pw) => navigator.clipboard.writeText(pw).catch(() => {}),
        args: [pw],
}).catch(() => {});
      break;
    }
    case 'fortress-fill':
      chrome.tabs.sendMessage(tab.id, { type: 'TRIGGER_AUTOFILL' }).catch(() => {});
      break;
    case 'fortress-save':
      chrome.tabs.sendMessage(tab.id, { type: 'TRIGGER_SAVE_PROMPT' }).catch(() => {});
      break;
  }
});

// ── Message router ────────────────────────────────────────────────────────────
chrome.runtime.onMessage.addListener((msg, _sender, sendResponse) => {
  handleMessage(msg).then(sendResponse).catch((e) => {
    const msg_type = msg.type;
    const isExpected = e.message?.includes('locked') || e.message?.includes('token') || e.message?.includes('Unauthorized');
    if (!isExpected) console.error('[Fortress] handleMessage error for', msg_type, ':', e.message);
    sendResponse({ error: e.message });
  });
  return true;
});

async function handleMessage(msg) {
  if (DEMO_MODE) return handleMessageDemo(msg);

  await restoreSession();

  // Standalone-only messages (always routed here, no service needed)
  if (msg.type === 'STANDALONE_SETUP') return saSetup(msg);

  // Unified setup router — branches by current mode. The setup wizard sends this
  // regardless of mode so callers don't need to know whether the backend is the
  // service or the in-extension vault.
  if (msg.type === 'SETUP') {
    const setupMode = await _saGetMode();
    if (setupMode === 'service') {
      const r = await nativeSend('CompleteSetup', {
        masterPassword: msg.password,
        pin: msg.pin ?? null,
      });
      if (!r.success) throw new Error(r.errorMessage ?? 'Service setup failed');
      const p = JSON.parse(r.payload ?? '{}');
      await persistSession(p.token);
      const { autoLockSeconds = 300 } = await chrome.storage.sync.get({ autoLockSeconds: 300 });
      await scheduleAutoLock(autoLockSeconds);
      setBadgeUnlocked();
      return { ok: true, mode: 'service' };
    }
    // Default: standalone (covers explicit 'standalone' and unset modes).
    return saSetup(msg);
  }

  // Check if user is in standalone (extension-only) mode
  const _mode = await _saGetMode();
  if (_mode === 'standalone') {
    switch (msg.type) {
      case 'GET_STATUS':   return saGetStatus();
      case 'UNLOCK':       return saUnlock(msg.password);
      case 'UNLOCK_PIN':   return saUnlockPin(msg.pin);
      case 'LOCK':         return saLock();
      case 'RESCHEDULE_AUTOLOCK': { await scheduleAutoLock(msg.seconds ?? 300); return { ok: true }; }
      case 'GET_ALL_ITEMS':    return saGetAllItems();
      case 'SEARCH_VAULT': {
        const all = await saGetAllItems();
        const q = (msg.query ?? '').toLowerCase().trim();
        if (!q) return all;
        return { isVaultLocked: false, suggestions: (all.suggestions ?? []).filter(i =>
          (i.label ?? '').toLowerCase().includes(q) ||
          (i.username ?? '').toLowerCase().includes(q) ||
          (i.url ?? '').toLowerCase().includes(q)
        )};
      }
      case 'GET_CREDENTIALS': {
        const all = await saGetAllItems();
        const url = msg.url ?? '';
        const host = url ? new URL(url).hostname.replace(/^www\./, '') : '';
        const matches = (all.suggestions ?? []).filter(i =>
          i.type === 'Login' && i.url && new URL(i.url).hostname.replace(/^www\./, '') === host
        );
        return { credentials: matches };
      }
      case 'SAVE_LOGIN': {
        const iconUri = msg.url ? `https://www.google.com/s2/favicons?domain=${_getDomain(msg.url)}&sz=64` : null;
        return saSaveItem({ type: 'Login', label: msg.label ?? msg.url ?? '', url: msg.url, username: msg.username, password: msg.password, totpSecret: msg.totpSecret ?? null, notes: msg.notes ?? '', isFavorite: false, iconUri, matchScore: 0 });
      }
      case 'SAVE_CARD':
        return saSaveItem({ type: 'CreditCard', label: msg.label, cardholderName: msg.cardholderName, cardNumber: msg.number, cardExpiry: msg.expiry, cvv: msg.cvv, isFavorite: false });
      case 'SAVE_IDENTITY':
        return saSaveItem({ type: 'Identity', label: msg.label, firstName: msg.firstName, lastName: msg.lastName, email: msg.email, phone: msg.phone, company: msg.company, isFavorite: false });
      case 'SAVE_NOTE':
        return saSaveItem({ type: 'SecureNote', label: msg.label, content: msg.content, isFavorite: false });
      case 'SAVE_AUTHENTICATOR': {
        const iconUri2 = msg.label ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(msg.label.toLowerCase().replace(/\s+/g,''))}&sz=64` : null;
        return saSaveItem({ type: 'Authenticator', label: msg.label, username: msg.username, totpSecret: msg.totpSecret, authType: msg.authType, digits: msg.digits, period: msg.period, algorithm: msg.algorithm, isFavorite: false, iconUri: iconUri2, matchScore: 0 });
      }
      case 'UPDATE_LOGIN':
        return saUpdateItem(msg.id, { label: msg.label, url: msg.url, username: msg.username, password: msg.password, totpSecret: msg.totpSecret, notes: msg.notes, isFavorite: msg.isFavorite });
      case 'UPDATE_CARD':
        return saUpdateItem(msg.id, { label: msg.label, cardholderName: msg.cardholderName, cardNumber: msg.number, cardExpiry: msg.expiry, cvv: msg.cvv });
      case 'UPDATE_IDENTITY':
        return saUpdateItem(msg.id, { label: msg.label, firstName: msg.firstName, lastName: msg.lastName, email: msg.email, phone: msg.phone, company: msg.company });
      case 'UPDATE_NOTE':
        return saUpdateItem(msg.id, { label: msg.label, content: msg.content });
      case 'UPDATE_AUTHENTICATOR':
        return saUpdateItem(msg.id, { label: msg.label, username: msg.username, totpSecret: msg.totpSecret, authType: msg.authType, digits: msg.digits, period: msg.period, algorithm: msg.algorithm });
      case 'DELETE_ITEM':
        return saDeleteItem(msg.credentialId ?? msg.id);
      case 'TOGGLE_FAVORITE':
        return saToggleFavorite(msg.credentialId, msg.value);
      case 'GET_SECURE_ITEMS':
        { const all2 = await saGetAllItems(); return { items: (all2.suggestions ?? []).filter(i => i.type === 'SecureItem') }; }
      case 'SAVE_SECURE_ITEM': {
        const { type: _t, ...secureFields } = msg;
        return saSaveItem({ ...secureFields, type: 'SecureItem' });
      }
      case 'UPDATE_SECURE_ITEM':
        return saUpdateItem(msg.id, { ...msg });
      case 'DELETE_SECURE_ITEM':
        return saDeleteItem(msg.id);
      case 'GDRIVE_AUTH': {
        return new Promise((resolve, reject) => {
          chrome.identity.getAuthToken({ interactive: true, scopes: ['https://www.googleapis.com/auth/drive.appdata'] }, (token) => {
            if (chrome.runtime.lastError || !token) {
              reject(new Error(chrome.runtime.lastError?.message ?? 'Google auth failed'));
            } else {
              resolve({ ok: true, token });
            }
          });
        });
      }
      case 'GENERATE_PASSWORD':
        return { ok: true, password: generatePassword(msg.opts ?? {}) };
      case 'COPY_TO_CLIPBOARD':
      case 'FILL_CONFIRMED':
      case 'OPEN_POPUP':
        break; // fall through to service handler (these don't need the service)
      default:
        return { ok: false, error: 'Not supported in extension-only mode' };
    }
  }

  switch (msg.type) {

    case 'GET_STATUS': {
      console.log('[Fortress] GET_STATUS: sending to native host');
 let r;
      try {
        r = await nativeSend('GetStatus', {});
      } catch (e) {
        console.warn('[Fortress] GET_STATUS: service unavailable —', e.message);
        return { ok: false, isUnlocked: false, serviceDisconnected: true };
      }
      console.log('[Fortress] GET_STATUS: raw response:', JSON.stringify(r));
      const p = JSON.parse(r.payload ?? '{}');
      console.log('[Fortress] GET_STATUS: parsed payload:', p);
      return {
        ok: true,
        isUnlocked:         p.isUnlocked    ?? false,
        deviceId:           p.deviceId        ?? null,
        isSetupComplete:    p.isSetupComplete     ?? false,
        isPinEnabled:       p.isPinEnabled ?? false,
        isBiometricEnabled: p.isBiometricEnabled  ?? false,
        isPasswordEnabled:  p.isPasswordEnabled   ?? false,
      };
    }

    // ── Unlock / Lock ───────────────────────────────────────────────────────────
    case 'UNLOCK': {
      const r = await nativeSend('UnlockWithPassword', { credential: msg.password });
      const p = JSON.parse(r.payload ?? '{}');
      if (!r.success) throw new Error(r.errorMessage);
      await persistSession(p.token);
      const { autoLockSeconds = 300 } = await chrome.storage.sync.get({ autoLockSeconds: 300 });
      await scheduleAutoLock(autoLockSeconds);
      setBadgeUnlocked();
      return { ok: true, token: p.token };
    }

    case 'UNLOCK_PIN': {
      const r = await nativeSend('UnlockWithPin', { credential: msg.pin });
      const p = JSON.parse(r.payload ?? '{}');
      if (!r.success) throw new Error(r.errorMessage);
      await persistSession(p.token);
    setBadgeUnlocked();
      return { ok: true };
    }

    case 'LOCK': {
      await nativeSend('Lock', {}).catch(() => {});
      await clearSession();
      setBadgeLocked();
      return { ok: true };
    }

    case 'RESCHEDULE_AUTOLOCK': {
      await scheduleAutoLock(msg.seconds ?? 300);
   return { ok: true };
    }

  // ── Credentials ─────────────────────────────────────────────────────────────
    case 'GET_CREDENTIALS': {
      const r = await nativeSend('GetCredentials', { url: msg.url, sessionToken: _sessionToken ?? '' });
      return JSON.parse(r.payload ?? '{}');
    }

    case 'FILL_CONFIRMED': {
      const r = await nativeSend('FillConfirmed', { credentialId: msg.credentialId, sessionToken: _sessionToken ?? '' });
      return JSON.parse(r.payload ?? '{}');
    }

    case 'GET_ALL_ITEMS': {
      const [loginsR, authsR, cardsR, notesR, idsR] = await Promise.all([
        nativeSend('GetLoginItems',     { sessionToken: _sessionToken ?? '' }),
        nativeSend('GetAuthenticators', { sessionToken: _sessionToken ?? '' }),
        nativeSend('GetCreditCards',    { sessionToken: _sessionToken ?? '' }),
        nativeSend('GetSecureNotes',    { sessionToken: _sessionToken ?? '' }),
        nativeSend('GetIdentities',     { sessionToken: _sessionToken ?? '' }),
      ]);
      const logins = (JSON.parse(loginsR.payload ?? '{}').items ?? []).map(i => ({
        id: i.id, type: 'Login', label: i.label, url: i.url,
        username: i.username,
        iconUri: i.url ? `https://www.google.com/s2/favicons?domain=${_getDomain(i.url)}&sz=64`
               : i.label ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(i.label.toLowerCase().replace(/\s+/g, ''))}&sz=64` : null,
        isFavorite: i.isFavorite ?? false, isBreached: false,
        totpSecret: i.otpSecret ?? null, matchScore: 0,
        passwordStrengthScore: i.passwordStrengthScore ?? null,
      }));
      const auths = (JSON.parse(authsR.payload ?? '{}').items ?? []).map(i => ({
        id: i.id, type: 'Authenticator', label: i.issuer || i.label || '',
        username: i.username,
        iconUri: i.issuer ? `https://www.google.com/s2/favicons?domain=${encodeURIComponent(i.issuer.toLowerCase().replace(/\s+/g, ''))}&sz=64` : null,
        isFavorite: i.isFavorite ?? false, isBreached: false,
        totpSecret: i.secret, authType: i.type, digits: i.digits, period: i.period,
        algorithm: i.algorithm, matchScore: 0,
      }));
      const cards = (JSON.parse(cardsR.payload ?? '{}').items ?? []).map(i => ({
        id: i.id, type: 'CreditCard', label: i.label, username: i.cardholderName,
        iconUri: null, isFavorite: i.isFavorite ?? false, isBreached: false,
        cardNumber: i.number,
        cardExpiry: i.expiryMonth && i.expiryYear ? `${i.expiryMonth}/${i.expiryYear}` : '',
        matchScore: 0,
      }));
      const notes = (JSON.parse(notesR.payload ?? '{}').items ?? []).map(i => ({
        id: i.id, type: 'Note', label: i.label,
        username: '',
        iconUri: null, isFavorite: i.isFavorite ?? false, isBreached: false,
        // Service returns `content`; popup expects `notes`.
        notes: i.content ?? '',
        matchScore: 0,
      }));
      const identities = (JSON.parse(idsR.payload ?? '{}').items ?? []).map(i => ({
        id: i.id, type: 'Identity', label: i.label,
        username: [i.firstName, i.lastName].filter(Boolean).join(' '),
        email: i.email,
        iconUri: null, isFavorite: i.isFavorite ?? false, isBreached: false,
        matchScore: 0,
      }));
      return { isVaultLocked: false, suggestions: [...logins, ...auths, ...cards, ...notes, ...identities] };
    }

    case 'SEARCH_VAULT': {
      // Re-use GET_ALL_ITEMS and filter client-side so the autofill engine
      // (which requires a non-empty URL) is not used for vault search.
      const allR = await handleMessage({ type: 'GET_ALL_ITEMS' });
      const q = (msg.query ?? '').toLowerCase().trim();
      if (!q) return allR;
      const filtered = (allR.suggestions ?? []).filter(i =>
        (i.label   ?? '').toLowerCase().includes(q) ||
        (i.username ?? '').toLowerCase().includes(q) ||
        (i.url     ?? '').toLowerCase().includes(q)
      );
      return { isVaultLocked: false, suggestions: filtered };
    }

    // ── Save items ──────────────────────────────────────────────────────────────
    case 'SAVE_LOGIN': {
      // SaveLoginItemRequest expects { sessionToken, item: { id, label, url, ... }, encryptSensitiveFields }
  // Generate a new GUID on the client so every saved item has a unique ID.
    const newId = crypto.randomUUID();
      await nativeSend('SaveLoginItem', {
        sessionToken: _sessionToken ?? '',
  item: {
          id:         newId,
       label:      msg.label     ?? '',
          url:        msg.url       ?? '',
          username:   msg.username?? '',
   password:   msg.password  ?? '',
          otpSecret:  msg.totpSecret || null,
          notes:      msg.notes     ?? '',
 isFavorite: msg.isFavorite ?? false,
     },
        encryptSensitiveFields: true,
      });
      return { ok: true };
    }

    case 'SAVE_CARD': {
      await nativeSend('SaveCreditCard', {
      label: msg.label,
        cardholderName: msg.cardholderName,
        number:         msg.number,
        expiry:         msg.expiry,
  cvv:      msg.cvv,
        sessionToken:   _sessionToken ?? '',
  });
      return { ok: true };
    }

    case 'SAVE_IDENTITY': {
      await nativeSend('SaveIdentity', {
     label:        msg.label,
        firstName:    msg.firstName,
lastName:     msg.lastName,
        email:        msg.email,
        phone:        msg.phone,
      company:      msg.company,
        sessionToken: _sessionToken ?? '',
      });
  return { ok: true };
    }

    case 'SAVE_NOTE': {
      await nativeSend('SaveSecureNote', {
        label:        msg.label,
        content:      msg.content,
        sessionToken: _sessionToken ?? '',
      });
      return { ok: true };
    }

    case 'SAVE_AUTHENTICATOR': {
      await nativeSend('SaveAuthenticator', {
        label:        msg.label,
        username:     msg.username,
        secret:       msg.totpSecret,
        authType:     msg.authType,
        digits:       msg.digits,
        period:       msg.period,
        algorithm:    msg.algorithm,
        sessionToken: _sessionToken ?? '',
      });
      return { ok: true };
    }

    case 'UPDATE_LOGIN': {
      await nativeSend('UpdateLogin', {
        id:           msg.id,
        label:        msg.label,
        url:          msg.url,
        username:     msg.username,
        password:     msg.password,
        totpSecret:   msg.totpSecret,
        notes:        msg.notes,
        isFavorite:   msg.isFavorite,
        sessionToken: _sessionToken ?? '',
      });
      return { ok: true };
    }

    case 'UPDATE_CARD': {
      await nativeSend('UpdateCard', {
        id:             msg.id,
        label:          msg.label,
        cardholderName: msg.cardholderName,
        number:         msg.number,
        expiry:         msg.expiry,
        cvv:            msg.cvv,
        sessionToken:   _sessionToken ?? '',
      });
      return { ok: true };
    }

    case 'UPDATE_IDENTITY': {
      await nativeSend('UpdateIdentity', {
        id:           msg.id,
        label:        msg.label,
        firstName:    msg.firstName,
        lastName:     msg.lastName,
        email:        msg.email,
        phone:        msg.phone,
        company:      msg.company,
        sessionToken: _sessionToken ?? '',
      });
      return { ok: true };
    }

    case 'UPDATE_NOTE': {
      await nativeSend('UpdateSecureNote', {
        id:           msg.id,
        label:        msg.label,
        content:      msg.content,
        sessionToken: _sessionToken ?? '',
      });
      return { ok: true };
    }

    case 'UPDATE_AUTHENTICATOR': {
      await nativeSend('UpdateAuthenticator', {
        id:           msg.id,
        label:        msg.label,
        username:     msg.username,
        secret:       msg.totpSecret,
        authType:     msg.authType,
        digits:       msg.digits,
        period:       msg.period,
        algorithm:    msg.algorithm,
        sessionToken: _sessionToken ?? '',
      });
      return { ok: true };
    }

    // ── Manage items ────────────────────────────────────────────────────────────
    case 'DELETE_ITEM': {
      await nativeSend('DeleteItem', {
        credentialId: msg.credentialId,
        sessionToken: _sessionToken ?? '',
      });
 return { ok: true };
    }

    case 'TOGGLE_FAVORITE': {
      await nativeSend('ToggleFavorite', {
  credentialId: msg.credentialId,
    value:        msg.value,
   sessionToken: _sessionToken ?? '',
      }).catch(() => {}); // best-effort
      return { ok: true };
    }

    // ── Secure Items ────────────────────────────────────────────────────────
    case 'GET_SECURE_ITEMS': {
      const r = await nativeSend('GetSecureItems', { sessionToken: _sessionToken ?? '' });
      return JSON.parse(r.payload ?? '{}');
    }

    case 'SAVE_SECURE_ITEM': {
      await nativeSend('SaveSecureItem', {
        itemType:      msg.itemType,
        label:         msg.label,
        fullName:      msg.fullName,
        dateOfBirth:   msg.dateOfBirth,
        nationality:   msg.nationality,
        number:        msg.number,
        issuingCountry: msg.issuingCountry,
        issuedDate:    msg.issuedDate,
        expiryDate:    msg.expiryDate,
        firstName:     msg.firstName,
        lastName:      msg.lastName,
        email:         msg.email,
        phone:         msg.phone,
        company:       msg.company,
        addressLine1:  msg.addressLine1,
        addressLine2:  msg.addressLine2,
        city:          msg.city,
        state:         msg.state,
        country:       msg.country,
        postalCode:    msg.postalCode,
        ssid:          msg.ssid,
        wifiSecurity:  msg.wifiSecurity,
        password:      msg.password,
        host:          msg.host,
        port:          msg.port,
        username:      msg.username,
        sshPassword:   msg.sshPassword,
        privateKey:    msg.privateKey,
        keyFingerprint: msg.keyFingerprint,
        noteContent:   msg.noteContent,
        isFavorite:    msg.isFavorite,
        sessionToken:  _sessionToken ?? '',
      });
      return { ok: true };
    }

    case 'UPDATE_SECURE_ITEM': {
      await nativeSend('UpdateSecureItem', {
        id:            msg.id,
        label:         msg.label,
        fullName:      msg.fullName,
        dateOfBirth:   msg.dateOfBirth,
        nationality:   msg.nationality,
        number:        msg.number,
        issuingCountry: msg.issuingCountry,
        issuedDate:    msg.issuedDate,
        expiryDate:    msg.expiryDate,
        firstName:     msg.firstName,
        lastName:      msg.lastName,
        email:         msg.email,
        phone:         msg.phone,
        company:       msg.company,
        addressLine1:  msg.addressLine1,
        addressLine2:  msg.addressLine2,
        city:          msg.city,
        state:         msg.state,
        country:       msg.country,
        postalCode:    msg.postalCode,
        ssid:          msg.ssid,
        wifiSecurity:  msg.wifiSecurity,
        password:      msg.password,
        host:          msg.host,
        port:          msg.port,
        username:      msg.username,
        sshPassword:   msg.sshPassword,
        privateKey:    msg.privateKey,
        keyFingerprint: msg.keyFingerprint,
        noteContent:   msg.noteContent,
        isFavorite:    msg.isFavorite,
        sessionToken:  _sessionToken ?? '',
      });
      return { ok: true };
    }

    case 'DELETE_SECURE_ITEM': {
      await nativeSend('DeleteSecureItem', {
        id:           msg.id,
        sessionToken: _sessionToken ?? '',
      });
      return { ok: true };
    }

    // ── Generator ───────────────────────────────────────────────────────────────
    case 'GENERATE_PASSWORD': {
      return { ok: true, password: generatePassword(msg.opts ?? {}) };
    }

    // ── Open popup ──────────────────────────────────────────────────────────────
    case 'OPEN_POPUP': {
      if (chrome.action?.openPopup) {
        chrome.action.openPopup().catch(() => {
          // openPopup() requires a focused window — fall back to popup tab
          chrome.tabs.create({ url: chrome.runtime.getURL('popup/popup.html') });
        });
      } else {
        chrome.tabs.create({ url: chrome.runtime.getURL('popup/popup.html') });
      }
      return { ok: true };
    }

    // ── Clipboard ───────────────────────────────────────────────────────────────
    case 'COPY_TO_CLIPBOARD': {
 await chrome.scripting.executeScript({
  target: { tabId: msg.tabId },
        func: (text) => navigator.clipboard.writeText(text),
args: [msg.text],
      }).catch(() => {});
      return { ok: true };
    }

    default:
      console.warn('[Fortress] Unhandled message type:', msg.type);
      return { ok: false, error: `Unknown message type: ${msg.type}` };
  }
}

// ── Password generator ────────────────────────────────────────────────────────
function generatePassword({
  length = 20, upper = true, lower = true,
  digits = true, symbols = true, excludeAmbiguous = false
} = {}) {
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

