/* ============================================================
   BNL Reloaded Control Panel

   Panes: status · players · ladder · bans · tools · console.
   Every number here comes from the API; nothing is invented.
   ============================================================ */

let allPlayers = [];
let currentPlayerId = null;

/* ---------- session ---------- */

function showLoginGate(message) {
  document.getElementById('loginGate').classList.add('active');
  document.getElementById('loginError').textContent = message || '';
}

function hideLoginGate() {
  document.getElementById('loginGate').classList.remove('active');
  document.getElementById('loginError').textContent = '';
}

async function doLogin() {
  const username = document.getElementById('loginUsername').value;
  const input = document.getElementById('loginPassword');
  const password = input.value;
  try {
    const res = await fetch('/api/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    });
    const data = await res.json();
    if (res.ok) {
      document.getElementById('loginUsername').value = '';
      input.value = '';
      hideLoginGate();
      init();
    } else {
      document.getElementById('loginError').textContent = data.error || 'Login failed';
    }
  } catch (e) {
    document.getElementById('loginError').textContent = e.message;
  }
}

async function doLogout() {
  try { await fetch('/api/logout', { method: 'POST' }); } catch { /* ignore */ }
  location.reload();
}

const _origFetch = window.fetch.bind(window);
window.fetch = async function(input, init) {
  const res = await _origFetch(input, init);
  const url = typeof input === 'string' ? input : input.url;
  if (res.status === 401 && !url.includes('/api/login')) {
    showLoginGate();
  }
  return res;
};

/* ---------- chrome ---------- */

function showPane(name) {
  document.querySelectorAll('.view-pane').forEach(p => p.classList.toggle('active', p.id === 'pane-' + name));
  document.querySelectorAll('.rail-item').forEach(b => b.classList.toggle('active', b.dataset.pane === name));
  document.body.classList.toggle('pane-console', name === 'console');
  if (name === 'ladder' && !mmrEditing) { buildMmrRows(); renderMmr(); }
  if (name === 'bans') renderModeration();
  if (name === 'maps') loadMaps();
  if (name === 'console') {
    /* While the pane is hidden it has no height, so the follow-the-tail check can't
       run and the log would otherwise open at the top of the buffer. */
    const out = document.getElementById('consoleOutput');
    out.scrollTop = out.scrollHeight;
    unreadErrors = 0;
    updateErrorBadge();
    updateConsoleCount();
  }
}

/* ---------- maps ---------- */
let mapsLoaded = false;
let mapRows = [];
let mapPools = {};
let mapFilter = '';
let mapEditing = null;
let mapSnapshot = [];
let mapDraft = [];
let mapChangesExpanded = false;

async function loadMaps(force) {
  if (mapsLoaded && !force) { renderMaps(); return; }
  const res = await fetch('/api/maps');
  if (!res.ok) return;
  const data = await res.json();
  mapRows = data.maps || [];
  mapPools = data.pools || {};
  mapsLoaded = true;
  document.getElementById('railMaps').textContent = mapRows.length;
  renderMaps();
}

function mapLabel(pool) {
  return ({ friendly: 'Casual', ranked: 'Ranked', custom: 'Customs' })[pool] || pool;
}

function mapModeLabel(match) {
  return ({ ShieldRush2: 'Shield Rush', ShieldCapture: 'Shield Capture', Tutorial: 'Tutorial', TimeTrial: 'Time Trial' })[match] || match || 'Unknown';
}

const TIME_TRIAL_HERO_NAMES = {
  unit_hero_abe: 'Yeti',
  unit_hero_astro: 'Astraella',
  unit_hero_boxer: 'Sweet Science',
  unit_hero_cogwheel: 'Cogwheel',
  unit_hero_djinn: 'Dream Genie',
  unit_hero_doc_eliza: 'Doc Eliza',
  unit_hero_engineer: 'Tony',
  unit_hero_hunter: 'Nigel',
  unit_hero_kira: 'Kira',
  unit_hero_kreepy: 'Kreepy',
  unit_hero_magnus: 'Vander',
  unit_hero_ninja: 'Ninja',
  unit_hero_roly: 'Roly',
  unit_hero_sarge_stone: 'Sarge',
  unit_hero_trondson: 'Trondson'
};

function mapObjectiveLabel(map) {
  if (map.match === 'TimeTrial') return TIME_TRIAL_HERO_NAMES[map.time_trial_hero_key] || map.time_trial_hero || 'Time Trial';
  const cubes = (map.cubes || 0) + 1;
  return cubes + (cubes === 1 ? ' cube' : ' cubes');
}

function selectMapPoolFilter(btn) {
  mapFilter = btn.dataset.pool || '';
  document.querySelectorAll('#mapPoolChips .chip').forEach(c => c.classList.toggle('on', c === btn));
  renderMapPoolControls();
  renderMaps();
}

function renderMapPoolControls() {
  const controls = document.getElementById('poolControls');
  controls.innerHTML = !mapEditing && ['friendly', 'ranked', 'custom'].includes(mapFilter)
    ? '<button class="save-btn" onclick="beginPoolEdit(\'' + mapFilter + '\')">Edit ' + esc(mapLabel(mapFilter)) + ' map pool</button>'
    : '';
}

function beginPoolEdit(pool) {
  mapEditing = pool;
  mapSnapshot = (mapPools[pool] || []).slice();
  const selected = new Set(mapSnapshot);
  mapRows.forEach(m => { m._selected = selected.has(m.key); });
  mapDraft = mapSnapshot.concat(mapRows.map(m => m.key).filter(k => !mapSnapshot.includes(k)));
  mapChangesExpanded = false;
  document.getElementById('poolEditBar').classList.add('open');
  document.getElementById('mapToolbar').hidden = true;
  renderMapPoolControls();
  updateMapEditStatus();
  renderMaps();
}

function mapPoolDirty() {
  const selected = mapDraft.filter(k => mapRows.find(m => m.key === k)._selected);
  return selected.length !== mapSnapshot.length || selected.some((k, i) => k !== mapSnapshot[i]);
}

function selectedMapKeys() {
  return mapDraft.filter(k => mapRows.find(m => m.key === k)._selected);
}

function updateMapEditStatus(saved) {
  if (!mapEditing) return;
  const selected = selectedMapKeys();
  const added = selected.filter(k => !mapSnapshot.includes(k));
  const removed = mapSnapshot.filter(k => !selected.includes(k));
  const reordered = !added.length && !removed.length && selected.some((k, i) => k !== mapSnapshot[i]);
  const count = added.length + removed.length + (reordered ? 1 : 0);
  const status = saved ? 'Changes submitted' : count ? count + ' pending ' + (count === 1 ? 'change' : 'changes') : 'No pending changes';
  const details = removed.map(key => '<li class="removed">Removed ' + esc(mapRows.find(m => m.key === key)?.name || key) + '</li>')
    .concat(added.map(key => '<li class="added">Added ' + esc(mapRows.find(m => m.key === key)?.name || key) + '</li>'));
  if (reordered) details.push('<li class="added">Map order changed</li>');
  document.getElementById('poolEditTitle').innerHTML = '<b>Editing ' + esc(mapLabel(mapEditing)) +
    ' map pool <span class="pool-map-count">&middot; ' + selected.length + ' maps</span></b>' +
    '<div><div class="pool-edit-status ' + (saved ? 'saved' : count ? 'pending' : '') + '"><span class="pool-change-summary">' + status + '</span>' +
    (count ? '<button class="pool-change-toggle" onclick="toggleMapChangeDetails()">' + (mapChangesExpanded ? 'Hide details' : 'Show details') + '</button>' : '') + '</div>' +
    (count && mapChangesExpanded ? '<ul class="pool-change-list">' + details.join('') + '</ul>' : '') + '</div>';
  document.getElementById('poolEditBar').classList.toggle('details-open', count > 0 && mapChangesExpanded);
  document.getElementById('poolDoneEditBtn').hidden = count > 0;
  document.getElementById('poolCancelBtn').hidden = count === 0;
  document.getElementById('poolSubmitBtn').hidden = count === 0;
}

function toggleMapChangeDetails() {
  mapChangesExpanded = !mapChangesExpanded;
  updateMapEditStatus();
}

function endPoolEdit() {
  if (mapPoolDirty()) return;
  mapEditing = null;
  mapFilter = '';
  document.getElementById('poolEditBar').classList.remove('open');
  document.getElementById('mapToolbar').hidden = false;
  document.querySelectorAll('#mapPoolChips .chip').forEach(c => c.classList.toggle('on', !c.dataset.pool));
  renderMapPoolControls();
  renderMaps();
}

function cancelPoolEdit() {
  const selected = new Set(mapSnapshot);
  mapRows.forEach(m => { m._selected = selected.has(m.key); });
  mapDraft = mapSnapshot.concat(mapRows.map(m => m.key).filter(k => !selected.has(k)));
  mapChangesExpanded = false;
  updateMapEditStatus();
  renderMaps();
}

function toggleMapMembership(key) {
  const map = mapRows.find(m => m.key === key);
  map._selected = !map._selected;
  mapDraft = mapDraft.filter(k => k !== key);
  if (map._selected) {
    const lastSelected = mapDraft.findLastIndex(k => mapRows.find(m => m.key === k)._selected);
    mapDraft.splice(lastSelected + 1, 0, key);
  } else mapDraft.push(key);
  updateMapEditStatus();
  renderMaps();
}

function moveMap(key, direction) {
  const selected = selectedMapKeys();
  const at = selected.indexOf(key), to = at + direction;
  if (to < 0 || to >= selected.length) return;
  const other = selected[to];
  const a = mapDraft.indexOf(key), b = mapDraft.indexOf(other);
  [mapDraft[a], mapDraft[b]] = [mapDraft[b], mapDraft[a]];
  updateMapEditStatus();
  renderMaps();
}

async function submitPoolEdit() {
  const maps = selectedMapKeys();
  const button = document.getElementById('poolSubmitBtn');
  button.disabled = true;
  try {
    const res = await fetch('/api/map-pools/' + mapEditing, {
      method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ maps })
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || 'Could not update map pool');
    mapPools[mapEditing] = data.maps.slice();
    mapSnapshot = data.maps.slice();
    mapRows.forEach(m => {
      m.pools = m.pools.filter(p => p !== mapEditing);
      if (maps.includes(m.key)) m.pools.push(mapEditing);
    });
    updateMapEditStatus(true);
    renderMaps();
  } catch (e) { alert(e.message); }
  finally { button.disabled = false; }
}

function renderMaps() {
  if (!mapsLoaded) return;
  let rows = mapEditing ? mapDraft.map(k => mapRows.find(m => m.key === k)) : mapRows.slice();
  if (!mapEditing && mapFilter === 'unused') rows = rows.filter(m => !(m.pools || []).length);
  else if (!mapEditing && mapFilter === 'time-trial') rows = rows.filter(m => m.match === 'TimeTrial');
  else if (!mapEditing && mapFilter === 'tutorial') rows = rows.filter(m => m.match === 'Tutorial');
  else if (!mapEditing && mapFilter) rows = rows.filter(m => (m.pools || []).includes(mapFilter));
  rows.sort(mapEditing ? () => 0 : (a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }));
  let dividerState = null;
  document.getElementById('mapGrid').innerHTML = rows.map(m => {
    const selected = !!m._selected;
    let divider = '';
    if (mapEditing && selected !== dividerState) {
      dividerState = selected;
      divider = '<div class="map-pool-divider">' + (selected ? 'In ' + esc(mapLabel(mapEditing)) + ' pool' : 'Unused in ' + esc(mapLabel(mapEditing))) + '</div>';
    }
    const selectedKeys = mapEditing ? selectedMapKeys() : [];
    const pos = mapEditing && selected ? selectedKeys.indexOf(m.key) : -1;
    const art = '/assets/maps/' + encodeURIComponent(m.image_asset || (m.key + '.jpg'));
    const activePools = (m.pools || []).filter(pool => ['friendly', 'ranked', 'custom'].includes(pool));
    const tags = mapEditing ? '' :
      '<div class="map-pool-hint">Used in</div><div class="map-tags">' +
      (activePools.length
        ? activePools.map(pool => '<span class="map-tag active">' + esc(mapLabel(pool)) + '</span>').join('')
        : '<span class="map-tag none">Nowhere</span>') +
      '</div>';
    const footer = mapEditing
      ? '<div class="map-state-footer ' + (selected ? 'included' : 'excluded') + '">' +
          (selected ? '<span class="map-order-controls"><button class="map-order-step" onclick="moveMap(\'' + esc(m.key) + '\',-1)" ' + (pos === 0 ? 'disabled' : '') + '>&#9664;</button>' +
            '<span class="map-order-value">' + String(pos + 1).padStart(2, '0') + '</span>' +
            '<button class="map-order-step" onclick="moveMap(\'' + esc(m.key) + '\',1)" ' + (pos === selectedKeys.length - 1 ? 'disabled' : '') + '>&#9654;</button></span>In pool' : 'Unused') +
          '<button class="map-membership-action" onclick="toggleMapMembership(\'' + esc(m.key) + '\')">' + (selected ? 'Remove' : 'Add to pool') + '</button></div>'
      : '';
    return divider + '<article class="map-tile ' + (mapEditing ? 'map-editing ' + (selected ? 'pool-included' : 'pool-excluded') : '') + '">' +
      '<img class="map-shot" src="' + art + '" alt="" onerror="this.outerHTML=\'<div class=&quot;map-shot none&quot;>No preview</div>\'">' +
      '<div class="map-body"><div class="map-title"><div class="map-name">' + esc(m.name) + '</div>' +
      '<div class="map-meta">' + mapObjectiveLabel(m) + ' &middot; ' + esc(mapModeLabel(m.match)) + '</div></div>' +
      (tags ? tags : '') +
      (!mapEditing ? '<div class="map-downloads"><span class="map-download-hint">Downloads</span>' +
        '<a class="map-download" href="/api/cards/' + encodeURIComponent(m.key) + '" download="' + esc(m.key) + '-card.json">Card info</a>' +
        '<a class="map-download" href="/api/maps/' + encodeURIComponent(m.key) + '/download">BNLBIN</a></div>' : '') +
      '</div>' + footer + '</article>';
  }).join('');
}

/* ---------- status ---------- */

async function refreshStatus() {
  try {
    const res = await fetch('/api/status');
    if (!res.ok) return;
    const data = await res.json();
    setFigure('figOnline', data.player_count);
    document.getElementById('onlineCount').textContent = data.player_count + ' online';
    document.getElementById('status').textContent = data.uptime_seconds != null
      ? formatUptime(data.uptime_seconds)
      : (data.uptime || '—');
  } catch { /* ignore */ }
}

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
    const a = await res.json();
    const byMode = Object.fromEntries((a.by_mode || []).map(m => [m.mode_id, m.players]));
    setFigure('figIdle', a.in_menu);
    for (const [id, modeId] of Object.entries(FIG_MODES)) setFigure(id, byMode[modeId] || 0);
  } catch { /* ignore */ }
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
    const data = await res.json();
    queues = data.queues || [];
    queuesStale = false;
    renderQueues();
  } catch {
    queuesStale = true;
    renderQueues();
  }
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

/* ---------- players ---------- */

async function loadPlayers() {
  const body = document.getElementById('playersBody');
  if (!allPlayers.length) body.innerHTML = '<tr class="empty-row"><td colspan="5">Loading…</td></tr>';
  try {
    const res = await fetch('/api/players');
    if (!res.ok) throw new Error('request failed');
    const data = await res.json();
    allPlayers = data.players || [];
    document.getElementById('railPlayers').textContent = allPlayers.length || '';
    document.getElementById('banPlayerNames').innerHTML =
      allPlayers.map(p => '<option value="' + esc(p.nickname) + '"></option>').join('');
    filterPlayers();
    if (!mmrEditing) { buildMmrRows(); renderMmr(); }
    renderModeration();
  } catch {
    body.innerHTML = '<tr class="empty-row error-row"><td colspan="5">Failed to load players.</td></tr>';
  }
}

let sortKey = null, sortDir = -1;      // -1 desc, 1 asc

/* Numeric and ordinal columns open descending — newest ID, highest role, online first —
   because that's the answer you wanted when you clicked. Text opens A-Z. */
const SORT_OPENS_ASC = { nickname: true };

const COMPARE = {
  id:       (a, b) => a.id - b.id,
  nickname: (a, b) => a.nickname.localeCompare(b.nickname, undefined, { sensitivity: 'base', numeric: true }),
  role:     (a, b) => a.role_id - b.role_id,          // by rank, not alphabetically
  status:   (a, b) => (a.online ? 1 : 0) - (b.online ? 1 : 0)
};

function setSort(key) {
  if (sortKey === key) sortDir = -sortDir;
  else { sortKey = key; sortDir = SORT_OPENS_ASC[key] ? 1 : -1; }
  document.querySelectorAll('.th-sort').forEach(b => {
    const active = b.dataset.key === sortKey;
    b.dataset.dir = active ? (sortDir < 0 ? 'desc' : 'asc') : 'none';
    b.closest('th').setAttribute('aria-sort',
      active ? (sortDir < 0 ? 'descending' : 'ascending') : 'none');
  });
  filterPlayers();
}

/* Ties fall back to player ID ascending regardless of direction, so equal roles and the
   whole offline block keep a stable order instead of shuffling between renders — and
   this table re-renders on every poll. */
function sortPlayers(players) {
  if (!sortKey) return players;
  const cmp = COMPARE[sortKey];
  return players.slice().sort((a, b) => {
    const d = cmp(a, b);
    return d !== 0 ? d * sortDir : a.id - b.id;
  });
}

function renderPlayers(list) {
  const players = sortPlayers(list);
  const body = document.getElementById('playersBody');
  if (!players.length) {
    body.innerHTML = '<tr class="empty-row"><td colspan="5">No players match that filter.</td></tr>';
    return;
  }
  body.innerHTML = players.map(p =>
    '<tr>' +
    '<td>' + p.id + '</td>' +
    '<td><a class="steam-btn" href="https://steamcommunity.com/profiles/' +
      encodeURIComponent(p.steam_id) + '" target="_blank" rel="noopener noreferrer"' +
      ' title="Open Steam profile">' + esc(p.nickname) + '</a></td>' +
    '<td><span class="role-badge role-' + esc(p.role) + '">' + esc(p.role) + '</span></td>' +
    '<td><span class="status-dot ' + (p.online ? 'online' : '') + '">' +
      (p.online ? 'Online' : 'Offline') + '</span></td>' +
    '<td class="row-action"><button class="edit-btn" onclick="showPlayerEdit(' + p.id + ')">Edit</button></td>' +
    '</tr>').join('');
}

function filterPlayers() {
  const q = document.getElementById('searchInput').value.toLowerCase();
  const filtered = allPlayers.filter(p =>
    p.nickname.toLowerCase().includes(q) ||
    String(p.id).includes(q) ||
    String(p.steam_id).includes(q) ||
    (p.region || '').toLowerCase().includes(q));
  renderPlayers(filtered);
  // Only says anything while a filter is narrowing things — the total lives in the rail.
  document.getElementById('playerCount').textContent =
    filtered.length === allPlayers.length ? ''
      : filtered.length + ' of ' + allPlayers.length + ' players';
}

/* ---------- drawer ---------- */

let returnFocusTo = null;

function showPlayerEdit(id) {
  currentPlayerId = id;
  returnFocusTo = document.activeElement;      // the Edit button that opened it
  document.getElementById('view-player').classList.add('active');
  document.getElementById('overlay').classList.add('active');
  loadPlayer(id);
  document.getElementById('view-player').focus();
}

function closePlayerEdit() {
  currentPlayerId = null;
  const view = document.getElementById('view-player');
  view.classList.remove('active');
  document.getElementById('overlay').classList.remove('active');
  // blur first: if the opener has gone (or was never focusable) focus would otherwise
  // stay parked on a drawer that is now hidden
  view.blur();
  if (returnFocusTo && document.contains(returnFocusTo)) returnFocusTo.focus();
  returnFocusTo = null;
}

async function loadPlayer(id) {
  document.getElementById('playerTitle').textContent = 'Player #' + id;
  document.getElementById('playerRole').innerHTML = '';
  document.getElementById('playerSub').innerHTML = '';
  try {
    const res = await fetch('/api/players/' + id);
    if (!res.ok) throw new Error('Player not found');
    const p = await res.json();
    if (currentPlayerId !== id) return;           // drawer moved on while we waited

    document.getElementById('playerTitle').textContent = p.nickname;
    document.getElementById('playerRole').innerHTML =
      '<span class="role-badge role-' + esc(p.role) + '">' + esc(p.role) + '</span>';
    document.getElementById('playerSub').innerHTML =
      '<span class="status-dot ' + (p.online ? 'online' : '') + '">' +
        (p.online ? 'Online' : 'Offline') + '</span>';
    document.getElementById('f-id').value = p.id;
    document.getElementById('f-steam').value = p.steam_id;
    document.getElementById('f-nickname').value = p.nickname;
    document.getElementById('f-role').value = p.role_id;
    loadedRoleId = String(p.role_id);
    roleChanged();

    const t = Date.now();
    const mm = document.getElementById('mmBanStatus');
    const mmBanned = p.matchmaker_ban_end != null && p.matchmaker_ban_end > t;
    mm.textContent = mmBanned ? 'Until ' + fmtUntil(p.matchmaker_ban_end) : 'Not banned';
    mm.className = 'ban-status ' + (mmBanned ? 'banned' : 'clear');

    const gy = document.getElementById('gyBanStatus');
    const perm = p.graveyard_permanent === true;
    const gyBanned = perm || (p.graveyard_leave_time != null && p.graveyard_leave_time > t);
    gy.textContent = perm ? 'Permanent'
      : gyBanned ? 'Until ' + fmtUntil(p.graveyard_leave_time)
      : 'Not banned';
    gy.className = 'ban-status ' + (gyBanned ? 'banned' : 'clear');

    renderProfile(p.badges, p.badge_icons);
    renderLoadouts(p.loadouts || []);
  } catch (e) {
    showToast('error', 'Failed to load player: ' + e.message);
  }
}

let currentLoadouts = [];

/* Every sprite the API names, it names by the card's CDB `icon`. sprites.js lists what
   this panel actually ships; anything not in it draws an empty tile rather than a
   broken image, so a catalogue update costs a missing icon and nothing else. */
const SPRITE_INDEX = typeof SPRITES === 'undefined' ? {} : SPRITES;

function sprite(folder, icon) {
  const set = SPRITE_INDEX[folder];
  return icon && set && set.has(icon) ? '/assets/' + folder + '/' + icon + '.png' : null;
}

/* The three profile slots. `badges` arrives keyed by BadgeType with card names in it and
   `badge_icons` maps those names to sprites — the shape /api/players/{id} sends. The
   caps decide how many slots are drawn, so an unfilled one is a dashed box rather than
   a shorter row. */
const BADGE_CAPS = { Title: 1, Border: 1, Icon: 3 };   // global_logic.max_badges_by_type

function renderProfile(badges, badgeIcons) {
  const b = badges || {};
  const title = (b.Title || [])[0];
  const border = (b.Border || [])[0];
  const icons = (b.Icon || []).slice(0, BADGE_CAPS.Icon);
  const artOf = (folder, name) => sprite(folder, name ? (badgeIcons || {})[name] : null);

  const titleEl = document.getElementById('profileTitle');
  titleEl.textContent = title || 'No title selected';
  titleEl.classList.toggle('profile-none', !title);

  /* Four rotated copies of the one corner sprite; CSS puts them in the corners. */
  const art = artOf('borders', border);
  const emblem = document.getElementById('profileBorder');
  emblem.classList.toggle('empty', !art);
  emblem.innerHTML = art
    ? '<img src="' + art + '" alt=""><img src="' + art + '" alt="">' +
      '<img src="' + art + '" alt=""><img src="' + art + '" alt="">'
    : '';
  const borderName = document.getElementById('profileBorderName');
  borderName.textContent = border || 'None selected';
  borderName.classList.toggle('profile-none', !border);

  document.getElementById('profileBadgeCap').textContent =
    icons.length + ' / ' + BADGE_CAPS.Icon;
  document.getElementById('profileBadges').innerHTML =
    Array.from({ length: BADGE_CAPS.Icon }, (_, i) => {
      const src = artOf('badges', icons[i]);
      return '<div class="badge-slot' + (src ? '' : ' empty') + '"' +
        (icons[i] ? ' title="' + esc(icons[i]) + '"' : '') + '>' +
        (src ? '<img src="' + src + '" alt="' + esc(icons[i]) + '">' : '') + '</div>';
    }).join('');
}

function renderLoadouts(loadouts) {
  currentLoadouts = loadouts;
  const select = document.getElementById('f-loadout-select');
  select.innerHTML = loadouts.map((l, i) => '<option value="' + i + '">' + esc(l.hero) + '</option>').join('');
  renderHeroTabs();
  renderSelectedLoadout();
}

/* The visible picker. It writes to the hidden <select> and re-renders, so the selection
   still lives in one place. */
function renderHeroTabs() {
  const sel = document.getElementById('f-loadout-select');
  document.getElementById('loadoutTabs').innerHTML = currentLoadouts.map((l, i) => {
    const face = sprite('portraits', l.hero_icon);
    return '<button class="hero-tab" type="button" role="tab" data-i="' + i + '"' +
      ' aria-selected="' + (String(i) === sel.value) + '" onclick="pickHero(' + i + ')">' +
      (face ? '<img src="' + face + '" alt="">' : '') + esc(l.hero) + '</button>';
  }).join('');
}

function pickHero(i) {
  document.getElementById('f-loadout-select').value = String(i);
  renderHeroTabs();
  renderSelectedLoadout();
}

const BLOCK_SLOTS = 6, PERK_SLOTS = 3;

function renderSelectedLoadout() {
  const detail = document.getElementById('loadoutDetail');
  const l = currentLoadouts[parseInt(document.getElementById('f-loadout-select').value)];
  document.getElementById('loadoutSkin').textContent = l && l.skin ? 'Skin: ' + l.skin : '';
  if (!l) { detail.innerHTML = '<p class="ban-status clear">No loadouts</p>'; return; }

  const icon = (folder, iconName, label, cls) => {
    const src = sprite(folder, iconName);
    return src
      ? '<img class="kit-icon ' + cls + '" src="' + src + '" alt="" title="' + esc(label) + '">'
      : '<span class="kit-icon ' + cls + ' empty"></span>';
  };

  /* Fixed slot count: an unused slot is drawn, not skipped, so every loadout is the
     same shape and slot 4 is slot 4 for everyone. */
  const devices = (l.devices || []).filter(d => d.name && d.name !== '(empty)');
  const blocks = Array.from({ length: BLOCK_SLOTS }, (_, i) => {
    const d = devices.find(x => x.slot === i + 1);
    return '<div class="slot-tile' + (d ? '' : ' empty') + '">' +
      icon('devices', d && d.icon, d && d.name, 'dev') +
      '<span class="slot-name">' + (d
        ? esc(d.name) + (d.variant ? ' <span class="device-variant">' + esc(d.variant) + '</span>' : '')
        : 'Empty slot') + '</span></div>';
  }).join('');

  /* Fixed slot order, not the order the API happens to return them in — the same perk
     type should sit in the same place for every character. */
  const PERK_ORDER = { Defensive: 0, Offensive: 1, Hero: 2 };
  const held = (l.perks || []).slice()
    .sort((a, b) => (PERK_ORDER[a.slot_type] ?? 9) - (PERK_ORDER[b.slot_type] ?? 9));
  const perks = Array.from({ length: PERK_SLOTS }, (_, i) => held[i] || null).map(pk => {
    const empty = !pk || pk.name === '(empty)';
    return '<div class="perk-tile">' +
      icon('perks', empty ? null : pk.icon, empty ? null : pk.name, 'perk') +
      '<div class="perk-body">' +
      (pk && pk.slot_type ? '<span class="perk-badge perk-badge-' + esc(pk.slot_type) + '">' +
        esc(pk.slot_type) + '</span>' : '') +
      '<div class="perk-name' + (empty ? ' perk-empty' : '') + '">' +
        (empty ? 'Empty slot' : esc(pk.name)) + '</div>' +
      (pk && pk.upside ? '<div class="perk-upside">' + esc(pk.upside) + '</div>' : '') +
      (pk && pk.downside ? '<div class="perk-downside">' + esc(pk.downside) + '</div>' : '') +
      '</div></div>';
  }).join('');

  const section = (title, cls, body) =>
    '<div class="loadout-section loadout-section-' + cls + '">' +
    '<div class="loadout-section-title">' + title + '</div>' + body + '</div>';

  detail.innerHTML = '<div class="loadout-cols">' +
    section('Block loadout', 'blocks', '<div class="slot-grid">' + blocks + '</div>') +
    section('Selected perks', 'perks', perks) +
    '</div>';
}

/* ---------- role ---------- */

let loadedRoleId = null;

/* A role change is pending until it is applied — same contract as the ladder, so
   "nothing is saved until you say so" holds everywhere in the panel. */
function roleChanged() {
  const sel = document.getElementById('f-role');
  const dirty = sel.value !== loadedRoleId;
  document.getElementById('rolePending').hidden = !dirty;
  if (dirty) {
    const was = [...sel.options].find(o => o.value === loadedRoleId);
    document.getElementById('roleWas').textContent = was ? was.text : '';
  }
}

function discardRole() {
  document.getElementById('f-role').value = loadedRoleId;
  roleChanged();
}

async function saveRole() {
  const sel = document.getElementById('f-role');
  const id = currentPlayerId;
  const ok = await postPlayer(id, { role_id: parseInt(sel.value) }, 'Role saved.');
  if (!ok) return;
  loadedRoleId = sel.value;
  roleChanged();
  await loadPlayers();
  if (currentPlayerId === id) loadPlayer(id);
}

/* ---------- ladder ---------- */

let mmrEditing = false;
let mmrRows = [];        // working copy
let mmrSnapshot = [];    // what Cancel restores

function buildMmrRows() {
  mmrRows = allPlayers
    .map(p => ({ id: p.id, nickname: p.nickname, steam_id: p.steam_id,
                 dev: p.rating_deviation ?? 0, mmr: p.rating_mean ?? 0, was: null, wasDev: null }))
    .sort((a, b) => b.mmr - a.mmr || a.id - b.id);
  mmrSnapshot = mmrRows.map(r => ({ ...r }));
}

/* Ratings are stored as TrueSkill means (~25), but the panel edits their hundredths as
   an integer (2500 means 25.00). */
const fmtMmr = v => String(Math.round(Number(v) * 100));
const fmtMmrDeviation = v => Number(v).toFixed(2);

/* The ladder is ordered by rating, so a move is only meaningful if the rating follows
   it. Moving a player up past someone means they must now rate at least as high as the
   best player they overtook; moving down, at most as low as the worst that overtook
   them. Clamping against the new neighbours does both, because the list was sorted
   before the move. */
function clampAt(i) {
  const row = mmrRows[i], above = mmrRows[i - 1], below = mmrRows[i + 1];
  let v = row.mmr;
  if (below && v < below.mmr) v = below.mmr;
  if (above && v > above.mmr) v = above.mmr;
  if (v !== row.mmr) {
    if (row.was === null) row.was = row.mmr;
    row.mmr = v;
    if (row.was === row.mmr) row.was = null;   // dragged back to where it started
  }
}

function mmrPending() { return mmrRows.filter(r => r.was !== null || r.wasDev !== null).length; }

/* Typing a rating is the other half of dragging: set the number and the row moves to
   wherever that number belongs, rather than the position dictating the number. */
function setMmrValue(i, raw) {
  const row = mmrRows[i];
  const v = parseInt(raw, 10) / 100;
  if (!isFinite(v)) { renderMmr(); return; }
  if (row.was === null) row.was = row.mmr;
  row.mmr = v;
  if (row.was === row.mmr) row.was = null;
  mmrRows.sort((a, b) => b.mmr - a.mmr || a.id - b.id);
  renderMmr();
}

function setMmrDeviation(i, raw) {
  const row = mmrRows[i];
  const v = parseFloat(raw);
  if (!isFinite(v)) { renderMmr(); return; }
  if (row.wasDev === null) row.wasDev = row.dev;
  row.dev = v;
  if (row.wasDev === row.dev) row.wasDev = null;
  renderMmr();
}

function renderMmr() {
  document.getElementById('mmrBody').innerHTML = mmrRows.length
    ? mmrRows.map((r, i) =>
      '<tr data-i="' + i + '"' + (mmrEditing ? ' draggable="true"' : '') + '>' +
      '<td class="mmr-position">' + (i + 1) + '</td>' +
      '<td><a class="steam-btn compact" href="https://steamcommunity.com/profiles/' +
        encodeURIComponent(r.steam_id) + '" target="_blank" rel="noopener noreferrer"' +
        ' title="Open Steam profile">' + esc(r.nickname) + '</a></td>' +
      '<td>' +
        '<input class="mmr-input" type="number" step="1" value="' + fmtMmr(r.mmr) + '"' +
          (mmrEditing ? '' : ' readonly tabindex="-1"') +
          ' onchange="setMmrValue(' + i + ', this.value)">' +
        '<span class="mmr-was">' + (r.was !== null ? fmtMmr(r.was) : '') + '</span>' +
      '</td>' +
      '<td>' + (mmrEditing
        ? '<input class="mmr-input" type="number" step="0.01" value="' + fmtMmrDeviation(r.dev) + '" onchange="setMmrDeviation(' + i + ', this.value)">'
        : fmtMmrDeviation(r.dev)) + '</td>' +
      '<td>' + r.id + '</td>' +
      '<td class="grip-col"><span class="grip" title="Drag to move">&#9776;</span></td>' +
      '</tr>').join('')
    : '<tr class="empty-row"><td colspan="6">No players yet.</td></tr>';
  updateMmrTools();
}

/* Nothing here is shown in a state where it can't do anything: no Apply until there is
   a change to apply, and no hint when the pane is read-only. */
function updateMmrTools() {
  const n = mmrPending();
  const apply = document.getElementById('mmrApplyBtn');
  document.getElementById('mmrEditBtn').textContent = mmrEditing ? 'Cancel' : 'Edit ladder';
  apply.hidden = !mmrEditing || n === 0;
  apply.textContent = 'Apply ' + n + ' change' + (n === 1 ? '' : 's');
  const hint = document.getElementById('mmrHint');
  hint.textContent = mmrEditing
    ? 'Drag by the handle or type a rating. Nothing saves until you apply.'
    : '';
  hint.title = hint.textContent;  // it truncates on a narrow window
  document.body.classList.toggle('mmr-editing', mmrEditing);
}

function toggleMmrEdit() {
  if (mmrEditing && mmrPending() && !confirm('Discard ' + mmrPending() + ' unapplied change(s)?')) return;
  mmrEditing = !mmrEditing;
  if (mmrEditing) mmrSnapshot = mmrRows.map(r => ({ ...r }));
  else mmrRows = mmrSnapshot.map(r => ({ ...r }));
  renderMmr();
}

async function applyMmrEdits() {
  const changed = mmrRows.filter(r => r.was !== null);
  if (!changed.length) return;
  const apply = document.getElementById('mmrApplyBtn');
  apply.disabled = true;
  let saved = 0;
  for (const r of changed) {
    if (await postPlayer(r.id, { rating_mean: r.mmr, rating_deviation: r.dev }, null)) {
      r.was = null;
      r.wasDev = null;
      saved++;
    }
  }
  apply.disabled = false;
  mmrEditing = false;
  showToast(saved === changed.length ? 'success' : 'error',
    saved + ' of ' + changed.length + ' rating' + (changed.length === 1 ? '' : 's') + ' updated.');
  await loadPlayers();     // rebuilds the rows from what the server now holds
}

/* Reordering is native HTML5 drag-and-drop with its drag image suppressed, and the row
   moved by transform instead. Native DnD is kept underneath rather than rebuilt on
   pointer events because it brings edge auto-scroll, Esc-to-cancel and the correct
   cursor for free. */
(function initMmrDrag() {
  const body = document.getElementById('mmrBody');
  let from = null;      // index the row started at
  let to = null;        // index it would land on right now
  let rowH = 0;
  let startY = 0;
  let scroller = null;  // the ancestor the browser auto-scrolls during the drag
  let startScroll = 0;

  /* The list is taller than its viewport, so the drag will scroll — and the rows move
     under a cursor whose clientY hasn't changed. Every offset below is therefore in
     layout space, which means knowing which ancestor did the scrolling. */
  const scrollerFor = el => {
    for (let p = el.parentElement; p; p = p.parentElement) {
      if (/(auto|scroll)/.test(getComputedStyle(p).overflowY) && p.scrollHeight > p.clientHeight) return p;
    }
    return document.scrollingElement;
  };

  /* One transparent pixel. Assigning it as the drag image is what stops the browser
     drawing the row under the cursor in two dimensions. */
  const BLANK = new Image();
  BLANK.src = 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7';

  const clamp = (v, lo, hi) => v < lo ? lo : v > hi ? hi : v;

  /* Everything between the row's old index and its current one slides one row out of
     the way; everything outside that span sits still. */
  const layout = () => {
    const rows = body.rows;
    for (let i = 0; i < rows.length; i++) {
      let shift = 0;
      if (i === from) shift = null;                                  // handled below
      else if (from < to && i > from && i <= to) shift = -rowH;
      else if (to < from && i >= to && i < from) shift = rowH;
      if (shift !== null) rows[i].style.transform = shift ? 'translateY(' + shift + 'px)' : '';
    }
  };

  const reset = () => {
    for (const r of body.rows) { r.style.transform = ''; r.classList.remove('dragging'); }
    from = to = null;
  };

  /* draggable stays on the <tr> so the whole row is the drag source, but the drag is
     refused unless the pointer went down on the grip — otherwise the row is just text. */
  let grabbed = false;
  body.addEventListener('pointerdown', e => { grabbed = !!e.target.closest('.grip'); });
  document.addEventListener('pointerup', () => { grabbed = false; });

  body.addEventListener('dragstart', e => {
    const tr = e.target.closest('tr');
    if (!tr || !mmrEditing || !grabbed) { e.preventDefault(); return; }
    from = to = +tr.dataset.i;
    /* The pitch between two rows, not one row's own height: the last row's box rounds
       to half a pixel short, and reading the height off whichever row you happened to
       grab would put that error into every shift. */
    rowH = body.rows.length > 1 ? body.rows[1].offsetTop - body.rows[0].offsetTop
                                : tr.getBoundingClientRect().height;
    startY = e.clientY;
    scroller = scrollerFor(body);
    startScroll = scroller.scrollTop;
    tr.classList.add('dragging');
    e.dataTransfer.effectAllowed = 'move';
    e.dataTransfer.setDragImage(BLANK, 0, 0);
    e.dataTransfer.setData('text/plain', String(from));   // Firefox needs a payload
  });

  /* On the document, not the tbody: the pointer is allowed to wander off the table, the
     row simply stops following it at the end of the list. */
  document.addEventListener('dragover', e => {
    if (from === null) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = 'move';
    const n = body.rows.length;
    /* clientY is a viewport coordinate and the rows are not: while the browser
       auto-scrolls at the edge of the list the pointer can sit perfectly still and
       still be over a different row. Adding the scroll delta puts this back in the
       same space as the transforms. */
    const moved = (e.clientY - startY) + (scroller.scrollTop - startScroll);
    const dy = clamp(moved, -from * rowH, (n - 1 - from) * rowH);
    to = clamp(from + Math.round(dy / rowH), 0, n - 1);
    layout();
    body.rows[from].style.transform = 'translateY(' + dy + 'px)';
  });

  document.addEventListener('drop', e => {
    if (from === null) return;
    e.preventDefault();
    if (to !== from) {
      const [row] = mmrRows.splice(from, 1);
      mmrRows.splice(to, 0, row);
      clampAt(to);
    }
    reset();
    renderMmr();
  });

  /* Esc cancels — it fires dragend with no drop, so the row snaps back. After a drop
     `from` is already null and the table has been re-rendered; this is only the
     cancelled path. */
  body.addEventListener('dragend', () => {
    grabbed = false;
    if (from === null) return;
    reset();
    renderMmr();
  });
})();

/* ---------- bans ---------- */

/* One row per penalty, not per player: someone can be sitting out a matchmaking ban
   and a graveyard sentence at the same time, and they lift separately. */
function activeBans() {
  const t = Date.now();
  const rows = [];
  for (const p of allPlayers) {
    if (p.matchmaker_ban_end != null && p.matchmaker_ban_end > t)
      rows.push({ id: p.id, name: p.nickname, kind: 'mm', until: p.matchmaker_ban_end, permanent: false });
    if (p.graveyard_permanent === true)
      rows.push({ id: p.id, name: p.nickname, kind: 'gy', until: null, permanent: true });
    else if (p.graveyard_leave_time != null && p.graveyard_leave_time > t)
      rows.push({ id: p.id, name: p.nickname, kind: 'gy', until: p.graveyard_leave_time, permanent: false });
  }
  // soonest to lift first; permanent ones can't lift on their own, so they sit last
  rows.sort((a, b) => (a.permanent - b.permanent) || (a.until - b.until) || (a.id - b.id));
  return rows;
}

function renderModeration() {
  const rows = activeBans();
  document.getElementById('banBody').innerHTML = rows.length
    ? rows.map(r =>
        '<tr>' +
        '<td>' + playerLink(r.name, r.id) + '</td>' +
        '<td><span class="pen-badge' + (r.kind === 'gy' ? ' graveyard' : '') + '">' +
          (r.kind === 'gy' ? 'Graveyard' : 'Matchmaking') + '</span></td>' +
        '<td>' + (r.permanent
          ? '<span class="mod-perm">Never &mdash; permanent</span>'
          : '<span class="mod-until">' + fmtRemaining(r.until) + '</span> ' +
            '<span class="mod-when">' + fmtUntil(r.until) + '</span>') + '</td>' +
        '<td><button class="unban-btn lift-btn" onclick="liftBan(' + r.id + ', \'' + r.kind + '\')">' +
          'Lift</button></td>' +
        '</tr>').join('')
    : '<tr><td colspan="4" class="empty-row">Nobody is banned.</td></tr>';

  /* A bare count, like the Players badge beside it. */
  document.getElementById('railBans').textContent = rows.length ? String(rows.length) : '';
}

const UNIT_MS = { minutes: 60000, hours: 3600000, days: 86400000 };

function durationMs(amountId, unitId) {
  const amount = parseFloat(document.getElementById(amountId).value);
  if (!amount || amount <= 0) return null;
  return amount * UNIT_MS[document.getElementById(unitId).value];
}

/* Two duration groups rather than one, so each penalty keeps its own fields. */
function renderBanForm() {
  const gy = document.getElementById('f-ban-kind').value === 'gy';
  for (const [id, show] of [['mmDuration', !gy], ['mmUnit', !gy],
                            ['gyDuration', gy], ['gyUnit', gy], ['gyPermRow', gy]])
    document.getElementById(id).hidden = !show;
}

async function imposeBan() {
  const q = document.getElementById('f-ban-player').value.trim();
  const p = allPlayers.find(x => x.nickname.toLowerCase() === q.toLowerCase() || String(x.id) === q);
  if (!p) { showToast('error', q ? 'No player matches "' + q + '".' : 'Name the player to ban.'); return; }

  const gy = document.getElementById('f-ban-kind').value === 'gy';
  let body, msg;
  if (gy) {
    const permanent = document.getElementById('f-gy-permanent').checked;
    const ms = permanent ? null : durationMs('f-gy-ban-amount', 'f-gy-ban-unit');
    if (!permanent && ms == null) { showToast('error', 'Enter a duration or mark the ban permanent.'); return; }
    body = { graveyard_permanent: permanent, graveyard_leave_time: permanent ? null : Date.now() + ms };
    msg = permanent ? 'Sent to the graveyard permanently.' : 'Sent to the graveyard.';
  } else {
    const ms = durationMs('f-mm-ban-amount', 'f-mm-ban-unit');
    if (ms == null) { showToast('error', 'Enter how long the ban should last.'); return; }
    body = { matchmaker_ban_end: Date.now() + ms };
    msg = 'Banned from matchmaking.';
  }

  if (!await postPlayer(p.id, body, msg)) return;
  document.getElementById('f-ban-player').value = '';
  document.getElementById('f-mm-ban-amount').value = '';
  document.getElementById('f-gy-ban-amount').value = '';
  document.getElementById('f-gy-permanent').checked = false;
  await loadPlayers();
  if (currentPlayerId === p.id) loadPlayer(p.id);
}

async function liftBan(id, kind) {
  const body = kind === 'gy'
    ? { graveyard_permanent: false, graveyard_leave_time: null }
    : { matchmaker_ban_end: null };
  if (!await postPlayer(id, body, kind === 'gy' ? 'Graveyard ban lifted.' : 'Matchmaking ban lifted.')) return;
  await loadPlayers();
  if (currentPlayerId === id) loadPlayer(id);
}

/* Bans expire on their own, so the rail count has to be able to fall without anyone
   pressing anything. */
setInterval(() => {
  if (document.getElementById('pane-bans').classList.contains('active')) renderModeration();
  else document.getElementById('railBans').textContent = activeBans().length || '';
}, 15000);

/* ---------- tools ---------- */

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

/* ---------- console ---------- */

/* /api/logs hands back records — sequence number, unix timestamp, level, category,
   message, and a detail string for stack traces. The poll sends the last sequence
   number it saw and gets only what followed, so there is no snapshot to diff and no
   tail-matching to re-find our place. Nodes are only ever appended, which is what keeps
   a text selection alive across a poll. */
const MAX_LINES = 2000;
const LEVELS = ['error', 'warn', 'info', 'debug'];

let logRecords = [];
let logCursor = 0;     // highest sequence number rendered
let logBoot = null;    // server run the cursor belongs to
let unreadErrors = 0;  // errors that arrived while another pane was open

function onConsolePane() { return document.body.classList.contains('pane-console'); }

/* The record carries epoch milliseconds, so the browser renders it in the viewer's own
   timezone — the server's clock no longer decides what the log says the time was. */
function fmtLogTime(ts) {
  const d = new Date(ts);
  const p = n => String(n).padStart(2, '0');
  return p(d.getHours()) + ':' + p(d.getMinutes()) + ':' + p(d.getSeconds()) +
         '.' + String(d.getMilliseconds()).padStart(3, '0');
}

function makeLogNode(rec) {
  const line = document.createElement('div');
  line.className = 'log-line ' + rec.lvl;

  const t = document.createElement('span');
  t.className = 'log-time';
  t.textContent = fmtLogTime(rec.ts);

  const c = document.createElement('span');
  c.className = 'log-cat';
  c.textContent = rec.cat;

  const b = document.createElement('span');
  b.className = 'log-msg';
  b.textContent = rec.msg;     // never innerHTML — log lines carry nicknames

  line.append(t, c, b);
  return line;
}

/* A stack trace is its own node under the line it belongs to: one failure stays one
   line in the stream, and the filter hides both together. */
function makeDetailNode(rec) {
  const d = document.createElement('div');
  d.className = 'log-detail';
  d.textContent = rec.detail;
  return d;
}

function atBottom(el) { return el.scrollHeight - el.scrollTop - el.clientHeight < 24; }
function currentFilter() { return document.getElementById('f-console-filter').value.toLowerCase(); }
function currentCat() { return document.getElementById('f-console-cat').value; }

function currentLevels() {
  const on = new Set();
  for (const chip of document.querySelectorAll('#logChips .chip.on'))
    on.add(chip.getAttribute('data-level'));
  return on;
}

function matchesFilter(rec, q, levels, cat) {
  if (!levels.has(rec.lvl)) return false;
  if (cat && rec.cat !== cat) return false;
  if (!q) return true;
  return (rec.msg + ' ' + rec.cat + ' ' + (rec.detail || '')).toLowerCase().includes(q);
}

function toggleLogChip(el) {
  const on = !el.classList.contains('on');
  el.classList.toggle('on', on);
  el.setAttribute('aria-pressed', String(on));
  renderConsole();
}

/* The category list is built from what the buffer actually contains rather than
   hardcoded, so a category added on the server shows up here without a panel change. */
function updateCatOptions() {
  const sel = document.getElementById('f-console-cat');
  const seen = [...new Set(logRecords.map(r => r.cat))].sort();
  const have = [...sel.options].slice(1).map(o => o.value);
  if (seen.length === have.length && seen.every((c, i) => c === have[i])) return;

  const keep = sel.value;
  sel.replaceChildren(new Option('All sources', ''));
  for (const cat of seen) sel.append(new Option(cat, cat));
  sel.value = seen.includes(keep) ? keep : '';
}

function updateConsoleCount() {
  const tally = { error: 0, warn: 0, info: 0, debug: 0 };
  for (const rec of logRecords) tally[rec.lvl] = (tally[rec.lvl] || 0) + 1;
  for (const chip of document.querySelectorAll('#logChips .chip'))
    chip.querySelector('.chip-n').textContent = String(tally[chip.getAttribute('data-level')] || 0);

  const shown = document.querySelectorAll('#consoleOutput .log-line:not(.hidden)').length;
  document.getElementById('consoleCount').textContent =
    shown === logRecords.length ? logRecords.length.toLocaleString() + ' lines'
                                : shown.toLocaleString() + ' of ' + logRecords.length.toLocaleString();
  document.getElementById('consoleJump').hidden =
    !document.querySelector('#consoleOutput .log-line.error:not(.hidden)');
}

function appendLogRecords(records) {
  if (!records.length) return;
  const out = document.getElementById('consoleOutput');
  const stuck = atBottom(out);
  const q = currentFilter();
  const levels = currentLevels();
  const cat = currentCat();

  const frag = document.createDocumentFragment();
  for (const rec of records) {
    const hide = !matchesFilter(rec, q, levels, cat);
    const node = makeLogNode(rec);
    if (hide) node.classList.add('hidden');
    frag.append(node);
    if (rec.detail) {
      const detail = makeDetailNode(rec);
      if (hide) detail.classList.add('hidden');
      frag.append(detail);
    }
    logRecords.push(rec);
  }
  out.append(frag);

  while (logRecords.length > MAX_LINES) {
    const dropped = logRecords.shift();
    out.firstElementChild?.remove();
    if (dropped.detail) out.firstElementChild?.remove();
  }

  updateCatOptions();
  updateConsoleCount();

  /* Most of the time nobody is on this pane. Count the errors that land while you are
     elsewhere and put the number on the rail; opening the pane clears it. */
  if (!onConsolePane()) {
    unreadErrors += records.filter(r => r.lvl === 'error').length;
    updateErrorBadge();
  }

  if (stuck) out.scrollTop = out.scrollHeight;
}

function updateErrorBadge() {
  document.getElementById('railFaults').textContent =
    unreadErrors ? unreadErrors + (unreadErrors === 1 ? ' error' : ' errors') : '';
}

function resetLog() {
  document.getElementById('consoleOutput').replaceChildren();
  logRecords = [];
  logCursor = 0;
}

async function pollLogs() {
  try {
    const res = await fetch('/api/logs?since=' + logCursor);
    if (!res.ok) return;
    const data = await res.json();

    /* Sequence numbers restart with the server, so a new boot id means the cursor is
       meaningless. Drop what we have and take the buffer from the top. */
    if (logBoot !== null && data.boot !== logBoot) {
      resetLog();
      logBoot = data.boot;
      return pollLogs();
    }
    logBoot = data.boot;

    /* Sliced before rendering, not after: the first poll of a full buffer returns
       10 000 records, and building nodes for all of them only to trim to MAX_LINES
       would cost a stall on every page load. */
    const all = data.records || [];
    if (all.length) logCursor = all[all.length - 1].seq;
    appendLogRecords(all.length > MAX_LINES ? all.slice(-MAX_LINES) : all);
  } catch { /* ignore */ }
}

/* Landing on the tail is no help when the thing you came for scrolled past a thousand
   debug lines ago — go to the newest error instead. */
function jumpToErrors() {
  if (!onConsolePane()) showPane('console');
  const chip = document.querySelector('#logChips .chip[data-level="error"]');
  if (!chip.classList.contains('on')) toggleLogChip(chip);
  setTimeout(() => {
    const out = document.getElementById('consoleOutput');
    const hits = out.querySelectorAll('.log-line.error:not(.hidden)');
    const last = hits[hits.length - 1];
    if (!last) return;
    last.scrollIntoView({ block: 'center' });
    last.classList.add('jumped');
    setTimeout(() => last.classList.remove('jumped'), 1400);
  }, 40);
}

/* Filter changed: retag the nodes that already exist. Still no rebuild, so a selection
   survives typing in the filter box too. */
function renderConsole() {
  const out = document.getElementById('consoleOutput');
  const q = currentFilter();
  const levels = currentLevels();
  const cat = currentCat();

  let node = out.firstElementChild;
  for (const rec of logRecords) {
    const hide = !matchesFilter(rec, q, levels, cat);
    if (node) { node.classList.toggle('hidden', hide); node = node.nextElementSibling; }
    if (rec.detail && node) { node.classList.toggle('hidden', hide); node = node.nextElementSibling; }
  }
  updateConsoleCount();
}

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
  refreshActivity();
  pollQueues();
  pollLogs();
  loadPlayers();
  setInterval(refreshStatus, 5000);
  setInterval(refreshActivity, 5000);
  setInterval(pollQueues, 5000);
  setInterval(pollLogs, 1000);
  setInterval(loadPlayers, 15000);
  /* Redraw between polls so waits and the confirm countdown move, but only while the
     pane is up — this rebuilds the box, and doing that unseen costs a selection for
     nothing. */
  setInterval(() => {
    if (queues && document.getElementById('pane-status').classList.contains('active')) renderQueues();
  }, 1000);
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
