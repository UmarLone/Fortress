// ─────────────────────────────────────────────────────────────────────────────
// native.js  �  Fortress typed API wrapper  v1.1
// Every call routes through the background service worker.
// ─────────────────────────────────────────────────────────────────────────────
export function sendToBackground(msg) {
  return new Promise((resolve, reject) => {
    chrome.runtime.sendMessage(msg, (response) => {
      if (chrome.runtime.lastError) return reject(new Error(chrome.runtime.lastError.message));
      if (response?.error) return reject(new Error(response.error));
      resolve(response);
    });
  });
}

// ── Typed API ─────────────────────────────────────────────────────────────────
export const FortressAPI = {
  // Session
  getStatus()         { return sendToBackground({ type: 'GET_STATUS' }); },
  unlock(password) { return sendToBackground({ type: 'UNLOCK', password }); },
  unlockPin(pin)           { return sendToBackground({ type: 'UNLOCK_PIN', pin }); },
  lock()  { return sendToBackground({ type: 'LOCK' }); },
  rescheduleAutoLock(seconds)     { return sendToBackground({ type: 'RESCHEDULE_AUTOLOCK', seconds }); },

  // Vault queries
  getCredentials(url)          { return sendToBackground({ type: 'GET_CREDENTIALS', url }); },
  getAllItems()          { return sendToBackground({ type: 'GET_ALL_ITEMS' }); },
  searchVault(query)            { return sendToBackground({ type: 'SEARCH_VAULT', query }); },

  // Fill
  fillConfirmed(credentialId)     { return sendToBackground({ type: 'FILL_CONFIRMED', credentialId }); },

  // Save
  saveLogin(url, username, pass)  { return sendToBackground({ type: 'SAVE_LOGIN', url, username, password: pass }); },
  saveCard(data)            { return sendToBackground({ type: 'SAVE_CARD', ...data }); },
  saveIdentity(data)  { return sendToBackground({ type: 'SAVE_IDENTITY', ...data }); },
  saveNote(label, content)        { return sendToBackground({ type: 'SAVE_NOTE', label, content }); },

  // Manage
deleteItem(credentialId)      { return sendToBackground({ type: 'DELETE_ITEM', credentialId }); },
  toggleFavorite(credentialId, value) { return sendToBackground({ type: 'TOGGLE_FAVORITE', credentialId, value }); },

  // Generator
  generatePassword(opts)          { return sendToBackground({ type: 'GENERATE_PASSWORD', opts }); },

  // Import / Export
  exportEncrypted()   { return sendToBackground({ type: 'EXPORT_VAULT_ENCRYPTED' }); },
  exportPlain(){ return sendToBackground({ type: 'EXPORT_VAULT_PLAIN' }); },
  importVault(format, data)       { return sendToBackground({ type: 'IMPORT_VAULT', format, data }); },
};
