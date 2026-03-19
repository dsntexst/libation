/* ========================================
   Libation SPA - Main Application
   ======================================== */

'use strict';

// ---- State ----
let allBooks = [];
let filteredBooks = [];
let currentSearch = '';
let currentFilter = '';
let signalR = null;

// ---- SignalR Connection ----
function initSignalR() {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/progress')
    .withAutomaticReconnect()
    .build();

  conn.on('BookBegin', (id, title) => {
    showToast(`Downloading: ${title}`, 'info');
    updateDownloadProgress(id, 0);
  });

  conn.on('Progress', (received, total) => {
    if (total > 0) {
      const pct = Math.round((received / total) * 100);
      document.querySelectorAll('.progress-bar').forEach(el => {
        el.style.width = pct + '%';
      });
    }
  });

  conn.on('BookCompleted', (id) => {
    showToast('Download complete!', 'success');
    refreshBook(id);
  });

  conn.on('BookError', (id, msg) => {
    showToast(`Error: ${msg}`, 'error');
  });

  conn.on('PdfBegin', (id, title) => {
    showToast(`Downloading PDF: ${title}`, 'info');
  });

  conn.on('PdfCompleted', (id) => {
    showToast('PDF downloaded!', 'success');
  });

  conn.on('QueueCompleted', () => {
    showToast('All downloads complete', 'success');
    loadLibrary();
  });

  conn.on('StatusUpdate', (msg) => console.log('[Status]', msg));

  conn.start().catch(err => console.warn('SignalR connection failed:', err));
  return conn;
}

function updateDownloadProgress(id, pct) {
  const card = document.querySelector(`[data-id="${id}"]`);
  if (!card) return;
  let wrap = card.querySelector('.progress-bar-wrap');
  if (!wrap) {
    wrap = document.createElement('div');
    wrap.className = 'progress-bar-wrap';
    wrap.innerHTML = '<div class="progress-bar" style="width:0%"></div>';
    card.querySelector('.book-info').appendChild(wrap);
  }
  wrap.querySelector('.progress-bar').style.width = pct + '%';
}

// ---- API Helpers ----
async function api(method, path, body) {
  const opts = { method, headers: { 'Content-Type': 'application/json' } };
  if (body !== undefined) opts.body = JSON.stringify(body);
  const res = await fetch('/api' + path, opts);
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || res.statusText);
  }
  if (res.status === 204) return null;
  return res.json();
}

const GET = (path) => api('GET', path);
const POST = (path, body) => api('POST', path, body);
const PATCH = (path, body) => api('PATCH', path, body);
const DELETE = (path) => api('DELETE', path);

// ---- Library ----
async function loadLibrary() {
  try {
    const [books, stats] = await Promise.all([GET('/library'), GET('/library/stats')]);
    allBooks = books;
    renderStats(stats);
    applyFilters();
  } catch (err) {
    document.getElementById('book-grid').innerHTML =
      `<div class="empty-state">Failed to load library: ${err.message}</div>`;
  }
}

function applyFilters() {
  let result = allBooks;

  if (currentSearch) {
    const q = currentSearch.toLowerCase();
    result = result.filter(b =>
      b.title.toLowerCase().includes(q) ||
      b.authors.toLowerCase().includes(q) ||
      b.narrators.toLowerCase().includes(q) ||
      (b.series && b.series.toLowerCase().includes(q)) ||
      b.tags.toLowerCase().includes(q)
    );
  }

  if (currentFilter) {
    result = result.filter(b => b.bookStatus === currentFilter);
  }

  filteredBooks = result;
  renderBookGrid(filteredBooks);
}

function renderStats(stats) {
  const bar = document.getElementById('stats-bar');
  bar.innerHTML = `
    <span class="stat-chip"><strong>${stats.total}</strong> total</span>
    <span class="stat-chip"><strong>${stats.fullyBackedUp}</strong> downloaded</span>
    <span class="stat-chip"><strong>${stats.noProgress + stats.downloadedOnly}</strong> pending</span>
    ${stats.error > 0 ? `<span class="stat-chip"><strong>${stats.error}</strong> errors</span>` : ''}
    ${stats.pdfsNotDownloaded > 0 ? `<span class="stat-chip"><strong>${stats.pdfsNotDownloaded}</strong> PDFs pending</span>` : ''}
  `;
}

function renderBookGrid(books) {
  const grid = document.getElementById('book-grid');
  if (books.length === 0) {
    grid.innerHTML = '<div class="empty-state">No books found</div>';
    return;
  }
  grid.innerHTML = books.map(renderBookCard).join('');

  grid.querySelectorAll('.book-card').forEach(card => {
    card.addEventListener('click', () => openBookModal(card.dataset.id));
  });

  grid.querySelectorAll('.btn-download').forEach(btn => {
    btn.addEventListener('click', async (e) => {
      e.stopPropagation();
      const id = btn.dataset.id;
      try {
        await POST(`/backup/books/${id}`);
        showToast('Download queued', 'info');
      } catch (err) {
        showToast(`Error: ${err.message}`, 'error');
      }
    });
  });
}

function formatDuration(mins) {
  if (!mins) return '';
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

function renderBookCard(book) {
  const cover = book.coverUrl
    ? `<div class="book-cover"><img src="${escHtml(book.coverUrl)}" alt="" loading="lazy" onerror="this.parentElement.textContent='📚'" /></div>`
    : `<div class="book-cover">📚</div>`;

  const series = book.series
    ? `<div class="book-series">${escHtml(book.series)}${book.seriesOrder ? ' #' + escHtml(book.seriesOrder) : ''}</div>`
    : '';

  const downloadBtn = book.bookStatus !== 'Liberated'
    ? `<button class="btn btn-sm btn-primary btn-download" data-id="${escHtml(book.productId)}">↓</button>`
    : '';

  return `
    <div class="book-card" data-id="${escHtml(book.productId)}">
      ${cover}
      <div class="book-info">
        <div class="book-title">${escHtml(book.title)}</div>
        <div class="book-author">${escHtml(book.authors)}</div>
        ${series}
        <div class="book-status">
          <span class="status-badge status-${escHtml(book.bookStatus)}">${formatStatus(book.bookStatus)}</span>
          <span class="book-duration">${formatDuration(book.lengthInMinutes)}</span>
          ${downloadBtn}
        </div>
      </div>
    </div>
  `;
}

function formatStatus(status) {
  const map = {
    Liberated: '✓ Downloaded',
    NotLiberated: '⬇ Pending',
    Error: '✗ Error',
    PartialDownload: '… Partial',
  };
  return map[status] || status;
}

async function refreshBook(id) {
  try {
    const book = await GET(`/library/${id}`);
    const idx = allBooks.findIndex(b => b.productId === id);
    if (idx >= 0) allBooks[idx] = book;
    applyFilters();
  } catch (err) { /* ignore */ }
}

// ---- Book Modal ----
async function openBookModal(productId) {
  const book = allBooks.find(b => b.productId === productId);
  if (!book) return;

  const modal = document.getElementById('book-modal');
  const body = document.getElementById('modal-body');

  const cover = book.coverUrl
    ? `<div class="modal-cover"><img src="${escHtml(book.coverUrl)}" alt="" onerror="this.parentElement.textContent='📚'" /></div>`
    : `<div class="modal-cover">📚</div>`;

  const seriesInfo = book.series
    ? `<div class="modal-series">${escHtml(book.series)}${book.seriesOrder ? ' #' + book.seriesOrder : ''}</div>`
    : '';

  const pubDate = book.datePublished ? new Date(book.datePublished).getFullYear() : '';
  const duration = formatDuration(book.lengthInMinutes);
  const abridged = book.isAbridged ? ' · Abridged' : '';

  body.innerHTML = `
    ${cover}
    <div class="modal-details">
      <div class="modal-title">${escHtml(book.title)}${book.subtitle ? ': ' + escHtml(book.subtitle) : ''}</div>
      <div class="modal-author">by ${escHtml(book.authors)}</div>
      ${seriesInfo}
      <div class="modal-meta">
        ${[book.narrators ? 'Narrated by ' + book.narrators : '', pubDate, duration + abridged, book.language]
          .filter(Boolean).join(' · ')}
      </div>
      <div class="modal-meta">
        Rating: ${book.overallRating > 0 ? book.overallRating.toFixed(1) + ' ★' : 'N/A'}
        &nbsp;|&nbsp; Status: <strong>${formatStatus(book.bookStatus)}</strong>
        ${book.pdfStatus ? `&nbsp;|&nbsp; PDF: ${book.pdfStatus}` : ''}
      </div>
      <div style="margin-top:0.75rem">
        <label style="font-size:0.8rem;color:var(--text-muted);display:block;margin-bottom:0.25rem">Tags</label>
        <input class="tags-input" id="modal-tags" type="text" value="${escHtml(book.tags)}" placeholder="tag1, tag2..." />
      </div>
      <div class="modal-actions">
        ${book.bookStatus !== 'Liberated' ? `<button class="btn btn-primary btn-sm" id="modal-download">Download</button>` : ''}
        <button class="btn btn-secondary btn-sm" id="modal-save-tags">Save Tags</button>
        ${!book.isDeleted
          ? `<button class="btn btn-secondary btn-sm" id="modal-remove">Remove</button>`
          : `<button class="btn btn-secondary btn-sm" id="modal-restore">Restore</button>`
        }
        <label style="display:flex;align-items:center;gap:0.4rem;font-size:0.82rem;cursor:pointer">
          <input type="checkbox" id="modal-finished" ${book.isFinished ? 'checked' : ''} />
          Finished
        </label>
      </div>
    </div>
  `;

  modal.classList.remove('hidden');

  document.getElementById('modal-save-tags')?.addEventListener('click', async () => {
    const tags = document.getElementById('modal-tags').value;
    const isFinished = document.getElementById('modal-finished').checked;
    try {
      await PATCH(`/library/${productId}`, { tags, isFinished });
      showToast('Saved', 'success');
      await refreshBook(productId);
    } catch (err) {
      showToast(`Error: ${err.message}`, 'error');
    }
  });

  document.getElementById('modal-download')?.addEventListener('click', async () => {
    try {
      await POST(`/backup/books/${productId}`);
      showToast('Download queued', 'info');
      modal.classList.add('hidden');
    } catch (err) {
      showToast(`Error: ${err.message}`, 'error');
    }
  });

  document.getElementById('modal-remove')?.addEventListener('click', async () => {
    if (!confirm('Remove this book from your library?')) return;
    try {
      await DELETE(`/library/${productId}`);
      allBooks = allBooks.filter(b => b.productId !== productId);
      applyFilters();
      modal.classList.add('hidden');
      showToast('Book removed', 'info');
    } catch (err) {
      showToast(`Error: ${err.message}`, 'error');
    }
  });

  document.getElementById('modal-restore')?.addEventListener('click', async () => {
    try {
      await POST(`/library/${productId}/restore`);
      await refreshBook(productId);
      modal.classList.add('hidden');
      showToast('Book restored', 'success');
    } catch (err) {
      showToast(`Error: ${err.message}`, 'error');
    }
  });
}

// ---- Import View ----
async function loadAccounts() {
  const list = document.getElementById('accounts-list');
  try {
    const accounts = await GET('/accounts');
    if (accounts.length === 0) {
      list.innerHTML = '<div class="empty-state">No accounts configured. Add accounts in the desktop app.</div>';
      return;
    }
    list.innerHTML = accounts.map(a => `
      <div class="account-row">
        <div class="account-info">
          <div class="account-name">${escHtml(a.accountName)}</div>
          <div class="account-locale">${escHtml(a.accountId)} · ${escHtml(a.localeName)}</div>
        </div>
        <button class="btn btn-secondary btn-sm" data-account="${escHtml(a.accountId)}" data-locale="${escHtml(a.localeName)}">
          Scan
        </button>
      </div>
    `).join('');

    list.querySelectorAll('button[data-account]').forEach(btn => {
      btn.addEventListener('click', () => scanAccount(btn.dataset.account, btn.dataset.locale));
    });
  } catch (err) {
    list.innerHTML = `<div class="empty-state">Failed to load accounts: ${err.message}</div>`;
  }
}

async function scanAccount(accountId, locale) {
  await runImport(() => POST(`/import/scan/${accountId}?locale=${encodeURIComponent(locale)}`));
}

async function scanAllAccounts() {
  await runImport(() => POST('/import/scan'));
}

async function runImport(importFn) {
  const log = document.getElementById('import-log');
  const btn = document.getElementById('btn-scan-all');

  log.classList.remove('hidden');
  log.textContent = 'Starting scan...\n';
  btn.disabled = true;
  document.getElementById('scan-status').classList.remove('hidden');

  try {
    const result = await importFn();
    log.textContent += `Scan complete. Found ${result.totalCount} books, ${result.newCount} new.\n`;
    showToast(`Import complete: ${result.newCount} new books`, 'success');
    if (result.newCount > 0) await loadLibrary();
  } catch (err) {
    log.textContent += `Error: ${err.message}\n`;
    showToast(`Import failed: ${err.message}`, 'error');
  } finally {
    btn.disabled = false;
    document.getElementById('scan-status').classList.add('hidden');
  }
}

// ---- Settings View ----
async function loadSettings() {
  const form = document.getElementById('settings-form');
  try {
    const settings = await GET('/settings');
    form.innerHTML = renderSettingsForm(settings);
  } catch (err) {
    form.innerHTML = `<div class="empty-state">Failed to load settings: ${err.message}</div>`;
  }
}

function renderSettingsForm(s) {
  const boolField = (key, label, desc, value) => `
    <div class="setting-row">
      <div>
        <div class="setting-label">${label}</div>
        ${desc ? `<div class="setting-description">${desc}</div>` : ''}
      </div>
      <label class="toggle">
        <input type="checkbox" name="${key}" ${value ? 'checked' : ''} />
        <span class="toggle-slider"></span>
      </label>
    </div>
  `;

  const readonlyField = (label, value) => `
    <div class="setting-row">
      <div class="setting-label">${label}</div>
      <span style="font-size:0.8rem;color:var(--text-muted);word-break:break-all;max-width:300px;text-align:right">
        ${escHtml(value || 'Not set')}
      </span>
    </div>
  `;

  return `
    ${readonlyField('Books Directory', s.booksDirectory)}
    ${readonlyField('Config Directory', s.libationFilesPath)}
    ${boolField('autoScan', 'Auto Scan', 'Automatically scan accounts on startup', s.autoScan)}
    ${boolField('downloadEpisodes', 'Download Episodes', 'Include podcast/episode content', s.downloadEpisodes)}
    ${boolField('splitFilesByChapter', 'Split by Chapter', 'Create separate files per chapter', s.splitFilesByChapter)}
    ${boolField('retainAaxFile', 'Retain AAX File', 'Keep original encrypted file after decryption', s.retainAaxFile)}
    ${boolField('allowLibationFixup', 'Allow Libation Fixup', 'Apply audio corrections during decryption', s.allowLibationFixup)}
    ${boolField('betaOptIn', 'Beta Updates', 'Receive beta version updates', s.betaOptIn)}
  `;
}

async function saveSettings() {
  const form = document.getElementById('settings-form');
  const updates = {};
  form.querySelectorAll('input[type="checkbox"]').forEach(input => {
    updates[input.name] = input.checked;
  });
  try {
    await PATCH('/settings', updates);
    showToast('Settings saved', 'success');
  } catch (err) {
    showToast(`Error: ${err.message}`, 'error');
  }
}

// ---- Toast ----
function showToast(message, type = 'info') {
  const container = document.getElementById('toast-container');
  const toast = document.createElement('div');
  toast.className = `toast toast-${type}`;
  toast.textContent = message;
  container.appendChild(toast);
  setTimeout(() => toast.remove(), 4000);
}

// ---- Helpers ----
function escHtml(str) {
  if (str == null) return '';
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

// ---- Navigation ----
function showView(name) {
  document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
  document.querySelectorAll('.nav-btn').forEach(b => b.classList.remove('active'));

  document.getElementById(`view-${name}`)?.classList.add('active');
  document.querySelector(`.nav-btn[data-view="${name}"]`)?.classList.add('active');

  if (name === 'import') loadAccounts();
  if (name === 'settings') loadSettings();
}

// ---- Init ----
document.addEventListener('DOMContentLoaded', () => {
  // Navigation
  document.querySelectorAll('.nav-btn').forEach(btn => {
    btn.addEventListener('click', () => showView(btn.dataset.view));
  });

  // Search
  const searchInput = document.getElementById('search-input');
  let searchTimeout;
  searchInput.addEventListener('input', () => {
    clearTimeout(searchTimeout);
    searchTimeout = setTimeout(async () => {
      currentSearch = searchInput.value.trim();
      if (currentSearch.length > 1) {
        try {
          const books = await GET(`/library/search?q=${encodeURIComponent(currentSearch)}`);
          filteredBooks = books;
          renderBookGrid(filteredBooks);
          return;
        } catch { /* fallback to local filter */ }
      }
      applyFilters();
    }, 300);
  });

  // Status filter
  document.getElementById('filter-status').addEventListener('change', (e) => {
    currentFilter = e.target.value;
    applyFilters();
  });

  // Download all
  document.getElementById('btn-backup-all').addEventListener('click', async () => {
    if (!confirm('Queue all undownloaded books for download?')) return;
    try {
      const result = await POST('/backup/books');
      showToast(`Queued ${result.queued} books for download`, 'info');
    } catch (err) {
      showToast(`Error: ${err.message}`, 'error');
    }
  });

  // Import
  document.getElementById('btn-scan-all').addEventListener('click', scanAllAccounts);

  // Settings
  document.getElementById('btn-save-settings').addEventListener('click', saveSettings);

  // Modal close
  document.getElementById('book-modal').addEventListener('click', (e) => {
    if (e.target.classList.contains('modal-backdrop') || e.target.classList.contains('modal-close')) {
      document.getElementById('book-modal').classList.add('hidden');
    }
  });

  // SignalR
  signalR = initSignalR();

  // Load initial data
  loadLibrary();
});
