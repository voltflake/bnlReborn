/* ---------- tools ---------- */

registerView('tools', { enter: () => loadMatchmakingStatus() });

async function loadMatchmakingStatus() {
  const button = document.getElementById('matchmakingToggle');
  const status = document.getElementById('matchmakingStatus');
  try {
    const res = await fetch('/api/matchmaking');
    if (!res.ok) throw new Error('request failed');
    const data = await res.json();
    button.disabled = false;
    button.textContent = data.enabled ? 'Turn off' : 'Turn on';
    status.textContent = data.enabled
      ? 'Enabled. Players can enter matchmaking.'
      : 'Disabled. Matchmaking buttons are grayed out for players.';
    button.classList.toggle('danger-btn', data.enabled);
  } catch {
    button.disabled = true;
    button.textContent = 'Unavailable';
    status.textContent = 'Could not load matchmaking status.';
  }
}

async function toggleMatchmaking() {
  const button = document.getElementById('matchmakingToggle');
  const enabled = button.textContent === 'Turn on';
  button.disabled = true;
  try {
    const res = await fetch('/api/matchmaking', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ enabled })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || 'Update failed');
    showToast('success', data.enabled ? 'Matchmaking enabled.' : 'Matchmaking disabled.');
    loadMatchmakingStatus();
  } catch (e) {
    showToast('error', e.message);
    loadMatchmakingStatus();
  }
}

async function exec(action) {
  const btn = event.target;
  btn.disabled = true;
  try {
    const res = await fetch('/api/' + action, { method: 'POST' });
    const data = await res.json();
    showToast(res.ok ? 'success' : 'error', data.message || data.error || 'Done');
  } catch (e) {
    showToast('error', e.message);
  }
  setTimeout(() => { btn.disabled = false; }, 3000);
}

async function resetServer() {
  if (!confirm('This shuts down the server process, disconnecting all players. The service is expected to relaunch it automatically. Continue?')) return;
  const btn = event.target;
  btn.disabled = true;
  try {
    const res = await fetch('/api/reset', { method: 'POST' });
    const data = await res.json();
    showToast(res.ok ? 'success' : 'error', data.message || data.error || 'Done');
  } catch (e) {
    showToast('error', e.message);
  }
  setTimeout(() => { btn.disabled = false; }, 3000);
}

let lookedUpCardJson = null;
let lookedUpCardName = 'card';

async function lookupCard() {
  const query = document.getElementById('f-card-query').value.trim();
  const result = document.getElementById('cardResult');
  const actions = document.getElementById('cardResultActions');
  if (!query) { showToast('error', 'Enter a card key or key hash.'); return; }
  lookedUpCardJson = null;
  actions.hidden = true;
  result.textContent = 'Loading…';
  try {
    const res = await fetch('/api/cards/' + encodeURIComponent(query));
    const data = await res.json();
    if (!res.ok) {
      result.textContent = '';
      showToast('error', data.error || 'Card not found');
      return;
    }
    lookedUpCardJson = JSON.stringify(data, null, 2);
    lookedUpCardName = String(data._id || data.id || query).replace(/[^a-z0-9._-]+/gi, '_');
    result.textContent = lookedUpCardJson;
    actions.hidden = false;
  } catch (e) {
    result.textContent = '';
    showToast('error', e.message);
  }
}

function downloadCardResult() {
  if (!lookedUpCardJson) return;
  const blob = new Blob([lookedUpCardJson + '\n'], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = lookedUpCardName + '.json';
  link.click();
  URL.revokeObjectURL(url);
}

async function copyCardResult() {
  if (!lookedUpCardJson) return;
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(lookedUpCardJson);
    } else {
      const area = document.createElement('textarea');
      area.value = lookedUpCardJson;
      document.body.appendChild(area);
      area.select();
      document.execCommand('copy');
      area.remove();
    }
    showToast('success', 'Card JSON copied.');
  } catch (e) {
    showToast('error', 'Could not copy card JSON.');
  }
}
