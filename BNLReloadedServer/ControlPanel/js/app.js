/* ---------- shared ---------- */

async function postPlayer(id, body, successMsg) {
  try {
    const res = await fetch('/api/players/' + id, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    const data = await res.json();
    if (!res.ok) { showToast('error', data.error || 'Update failed'); return false; }
    if (successMsg) showToast('success', successMsg);
    return true;
  } catch (e) {
    showToast('error', e.message);
    return false;
  }
}

function showToast(type, msg) {
  const toast = document.getElementById('toast');
  document.getElementById('toastMsg').textContent = msg;
  toast.className = 'toast ' + type;
  toast.style.display = 'block';
  clearTimeout(showToast._t);
  showToast._t = setTimeout(() => { toast.style.display = 'none'; }, 3000);
}

function esc(s) { const d = document.createElement('div'); d.textContent = s; return d.innerHTML; }

/* 24-hour regardless of locale: the console beside it is 24-hour, and one panel should
   not run two clocks. */
function fmtUntil(ms) {
  return new Date(Number(ms)).toLocaleString(undefined,
    { month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false });
}

function fmtRemaining(ms) {
  const m = Math.max(0, Math.round((Number(ms) - Date.now()) / 60000));
  if (m < 60) return 'in ' + m + ' min';
  const h = Math.floor(m / 60);
  if (h < 24) return 'in ' + h + 'h ' + String(m % 60).padStart(2, '0') + 'm';
  return 'in ' + Math.floor(h / 24) + 'd ' + (h % 24) + 'h';
}

/* Wherever a live nickname appears it opens that player's editor, so a ban you are
   already looking at doesn't send you to the Players pane to retype the name. */
function playerLink(name, id, cls) {
  const c = 'player-link' + (cls ? ' ' + cls : '');
  return id == null
    ? '<span' + (cls ? ' class="' + cls + '"' : '') + '>' + esc(name) + '</span>'
    : '<button class="' + c + '" onclick="showPlayerEdit(' + id + ')">' + esc(name) + '</button>';
}

document.addEventListener('keydown', e => {
  if (e.key === 'Escape' && currentPlayerId != null) closePlayerEdit();
});

/* ---------- boot ---------- */

let initialized = false;
function init() {
  if (initialized) return;
  initialized = true;
  renderBanForm();
  refreshStatus();
  pollLogs();
  loadPlayers();
  setInterval(refreshStatus, 5000);
  setInterval(pollLogs, 1000);
  setInterval(loadPlayers, 15000);
  showPane(paneFromHash(), false);
}

(async function start() {
  try {
    const res = await _origFetch('/api/status');
    if (res.status === 401) {
      showLoginGate();
      return;
    }
    init();
  } catch { /* ignore */ }
})();
