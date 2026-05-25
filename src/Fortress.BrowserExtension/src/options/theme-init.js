// theme-init.js — loaded as the first script in options.html
// Applies the saved theme before the page renders to avoid a flash of wrong theme.
(function () {
  chrome.storage.sync.get({ theme: 'system' }, function (s) {
    var d = s.theme === 'dark' ||
      (s.theme === 'system' && window.matchMedia('(prefers-color-scheme:dark)').matches);
    document.documentElement.setAttribute('data-theme', d ? 'dark' : 'light');
  });
})();
