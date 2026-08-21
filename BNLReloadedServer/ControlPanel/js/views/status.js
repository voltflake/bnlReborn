/* ---------- status ---------- */

async function refreshStatus() {
  try {
    const res = await fetch('/api/status');
    if (!res.ok) return;
    applyStatus(await res.json());
  } catch { /* ignore */ }
}

let statusStartedAt = null;

function applyStatus(data) {
  setFigure('figOnline', data.player_count);
  document.getElementById('onlineCount').textContent = data.player_count + ' online';
  statusStartedAt = data.uptime_seconds != null ? Date.now() - data.uptime_seconds * 1000 : null;
  document.getElementById('status').textContent = statusStartedAt != null
    ? formatUptime((Date.now() - statusStartedAt) / 1000)
    : (data.uptime || '—');
}

/* Uptime is derived from the last server event locally. This timer changes presentation only;
   it does not contact the server or participate in state-change detection. */
setInterval(() => {
  if (statusStartedAt != null)
    document.getElementById('status').textContent = formatUptime((Date.now() - statusStartedAt) / 1000);
}, 30000);

/* A zero is greyed rather than drawn as loudly as a real count — the row is read at a
   glance and five bold zeroes look like five facts. */
function setFigure(id, value) {
  const el = document.getElementById(id);
  el.textContent = value;
  el.classList.toggle('zero', value === 0);
}

/* The mode a figure counts, by card id. A mode that is absent from the response has
   nobody in it, which is a 0, not a blank. */
const FIG_MODES = {
  figCasual: 'game_mode_friendly',
  figRanked: 'game_mode_ranked',
  figCustom: 'game_mode_custom'
};

async function refreshActivity() {
  try {
    const res = await fetch('/api/activity');
    if (!res.ok) return;
    applyActivity(await res.json());
  } catch { /* ignore */ }
}

function applyActivity(a) {
  const byMode = Object.fromEntries((a.by_mode || []).map(m => [m.mode_id, m.players]));
  setFigure('figIdle', a.in_menu);
  for (const [id, modeId] of Object.entries(FIG_MODES)) setFigure(id, byMode[modeId] || 0);
}

/* ---------- queues ---------- */

/* /api/queues is one region's matchmaker — this process's — while player_count above is
   summed across every region on the master. That is why the queue total sits on this
   card and not in the figures row beside "Players online". */
let queues = null;                 // null until the first response
let queuesStale = false;

/* A failed poll must not leave the last good snapshot on screen: the waits go on ticking
   up off a 1s timer, so a frozen card reads as a queue nobody is being matched out of
   rather than as a panel that has lost contact. */
async function pollQueues() {
  try {
    const res = await fetch('/api/queues');
    if (!res.ok) { queuesStale = true; renderQueues(); return; }
    applyQueues(await res.json());
  } catch {
    queuesStale = true;
    renderQueues();
  }
}

function applyQueues(data) {
  queues = data.queues || [];
  queuesStale = false;
  renderQueues();
}

function fmtWait(ms) {
  const s = Math.max(0, Math.floor(ms / 1000));
  return Math.floor(s / 60) + ':' + String(s % 60).padStart(2, '0');
}

/* Absolute join times and an absolute confirm deadline come over the wire, so both the
   waits and the countdown move on this 1s tick rather than freezing between polls. */
function renderQueues() {
  const box = document.getElementById('queuesBody');
  if (queuesStale) {
    box.innerHTML = '<div class="queue-col"><p class="queue-empty error-row">' +
      'Cannot read the matchmaker right now.</p></div>';
    return;
  }
  if (!queues) {
    box.innerHTML = '<div class="queue-col"><p class="queue-empty">Loading…</p></div>';
    return;
  }
  if (!queues.length) {
    box.innerHTML = '<div class="queue-col"><p class="queue-empty">' +
      'No matchmaking modes in the catalogue.</p></div>';
    return;
  }

  const now = Date.now();
  box.innerHTML = queues.map(q => {
    const players = (q.players || []).slice().sort((a, b) => a.join_time - b.join_time);
    return '<div class="queue-col">' +
      '<div class="queue-col-head">' +
        '<span>' + esc(q.mode_name || q.mode_id) + '</span>' +
        queueState(q, now) +
        '<span class="queue-col-count">' + q.player_count + '</span>' +
      '</div>' +
      (players.length
        ? players.map(p =>
            '<div class="qp' + (p.confirming ? ' qp-confirming' : '') + '">' +
            playerLink(p.nickname || '#' + p.player_id, p.player_id, 'qp-name') +
            '<span class="qp-wait">' + fmtWait(now - p.join_time) + '</span></div>').join('')
        : '<p class="queue-empty">No one waiting</p>') +
      '</div>';
  }).join('');
}

/* Only says anything when the queue is doing something other than waiting. A pop can
   resolve between two polls, so the countdown floors at 0 rather than going negative. */
function queueState(q, now) {
  if (q.state === 'waiting') return '';
  const left = q.confirm_deadline != null
    ? ' ' + Math.max(0, Math.round((Number(q.confirm_deadline) - now) / 1000)) + 's'
    : '';
  const label = { confirming: 'confirming', backfilling: 'backfilling',
                  pop_failed: 'pop failed', unavailable: 'unavailable' }[q.state] || q.state;
  return '<span class="queue-state queue-state-' + esc(q.state) + '">' +
    esc(label) + left + '</span>';
}

/* The server sends uptime as a raw TimeSpan string ("4.02:17:41") as well, which reads
   like a time of day. Two units spelled out, most significant first, and a zero second
   unit is dropped rather than printed ("1 day", not "1 day, 0 hours"). Seconds never
   appear — they are noise on a figure that refreshes every 5s. */
function formatUptime(totalSeconds) {
  const s = Math.max(0, Math.floor(totalSeconds));
  const d = Math.floor(s / 86400);
  const h = Math.floor((s % 86400) / 3600);
  const m = Math.floor((s % 3600) / 60);
  const u = (n, word) => n + ' ' + word + (n === 1 ? '' : 's');
  if (d) return h ? u(d, 'day') + ', ' + u(h, 'hour') : u(d, 'day');
  if (h) return m ? u(h, 'hour') + ', ' + u(m, 'minute') : u(h, 'hour');
  if (m) return u(m, 'minute');
  return 'less than a minute';
}

let statusRenderTimer = null;

registerView('status', {
  enter: () => {
    refreshActivity();
    pollQueues();
    statusRenderTimer = setInterval(() => { if (queues) renderQueues(); }, 1000);
  },
  leave: () => {
    clearInterval(statusRenderTimer);
    statusRenderTimer = null;
  }
});
