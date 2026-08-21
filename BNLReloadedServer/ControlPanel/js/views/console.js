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

registerView('console', {
  enter: () => {
    /* A hidden console has no height, so establish the tail after it becomes visible. */
    const out = document.getElementById('consoleOutput');
    out.scrollTop = out.scrollHeight;
    unreadErrors = 0;
    updateErrorBadge();
    updateConsoleCount();
  }
});

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
