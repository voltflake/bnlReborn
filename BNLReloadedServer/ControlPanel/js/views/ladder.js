/* ---------- ladder ---------- */

let mmrEditing = false;
let mmrRows = []; // working copy
let mmrSnapshot = []; // what Cancel restores

registerView("ladder", {
  enter: () => {
    if (!mmrEditing) {
      buildMmrRows();
      renderMmr();
    }
  },
});

function buildMmrRows() {
  mmrRows = allPlayers
    .map((p) => ({
      id: p.id,
      nickname: p.nickname,
      steam_id: p.steam_id,
      dev: p.rating_deviation ?? 0,
      mmr: p.rating_mean ?? 0,
      was: null,
      wasDev: null,
    }))
    .sort((a, b) => b.mmr - a.mmr || a.id - b.id);
  mmrSnapshot = mmrRows.map((r) => ({ ...r }));
}

/* Ratings are stored as TrueSkill means (~25), but the panel edits their hundredths as
   an integer (2500 means 25.00). */
const fmtMmr = (v) => String(Math.round(Number(v) * 100));
const fmtMmrDeviation = (v) => Number(v).toFixed(2);

/* The ladder is ordered by rating, so a move is only meaningful if the rating follows
   it. Moving a player up past someone means they must now rate at least as high as the
   best player they overtook; moving down, at most as low as the worst that overtook
   them. Clamping against the new neighbours does both, because the list was sorted
   before the move. */
function clampAt(i) {
  const row = mmrRows[i],
    above = mmrRows[i - 1],
    below = mmrRows[i + 1];
  let v = row.mmr;
  if (below && v < below.mmr) v = below.mmr;
  if (above && v > above.mmr) v = above.mmr;
  if (v !== row.mmr) {
    if (row.was === null) row.was = row.mmr;
    row.mmr = v;
    if (row.was === row.mmr) row.was = null; // dragged back to where it started
  }
}

function mmrPending() {
  return mmrRows.filter((r) => r.was !== null || r.wasDev !== null).length;
}

/* Typing a rating is the other half of dragging: set the number and the row moves to
   wherever that number belongs, rather than the position dictating the number. */
function setMmrValue(i, raw) {
  const row = mmrRows[i];
  const v = parseInt(raw, 10) / 100;
  if (!isFinite(v)) {
    renderMmr();
    return;
  }
  if (row.was === null) row.was = row.mmr;
  row.mmr = v;
  if (row.was === row.mmr) row.was = null;
  mmrRows.sort((a, b) => b.mmr - a.mmr || a.id - b.id);
  renderMmr();
}

function setMmrDeviation(i, raw) {
  const row = mmrRows[i];
  const v = parseFloat(raw);
  if (!isFinite(v)) {
    renderMmr();
    return;
  }
  if (row.wasDev === null) row.wasDev = row.dev;
  row.dev = v;
  if (row.wasDev === row.dev) row.wasDev = null;
  renderMmr();
}

function renderMmr() {
  document.getElementById("mmrBody").innerHTML = mmrRows.length
    ? mmrRows
        .map(
          (r, i) =>
            '<tr data-i="' +
            i +
            '"' +
            (mmrEditing ? ' draggable="true"' : "") +
            ">" +
            '<td class="mmr-position">' +
            (i + 1) +
            "</td>" +
            '<td><a class="steam-btn compact" href="https://steamcommunity.com/profiles/' +
            encodeURIComponent(r.steam_id) +
            '" target="_blank" rel="noopener noreferrer"' +
            ' title="Open Steam profile">' +
            esc(r.nickname) +
            "</a></td>" +
            "<td>" +
            '<input class="mmr-input" type="number" step="1" value="' +
            fmtMmr(r.mmr) +
            '"' +
            (mmrEditing ? "" : ' readonly tabindex="-1"') +
            ' onchange="setMmrValue(' +
            i +
            ', this.value)">' +
            '<span class="mmr-was">' +
            (r.was !== null ? fmtMmr(r.was) : "") +
            "</span>" +
            "</td>" +
            "<td>" +
            (mmrEditing
              ? '<input class="mmr-input" type="number" step="0.01" value="' +
                fmtMmrDeviation(r.dev) +
                '" onchange="setMmrDeviation(' +
                i +
                ', this.value)">'
              : fmtMmrDeviation(r.dev)) +
            "</td>" +
            "<td>" +
            r.id +
            "</td>" +
            '<td class="grip-col"><span class="grip" title="Drag to move">&#9776;</span></td>' +
            "</tr>",
        )
        .join("")
    : '<tr class="empty-row"><td colspan="6">No players yet.</td></tr>';
  updateMmrTools();
}

/* Nothing here is shown in a state where it can't do anything: no Apply until there is
   a change to apply, and no hint when the pane is read-only. */
function updateMmrTools() {
  if (!window.controlPanelAdmin) return;
  const n = mmrPending();
  const apply = document.getElementById("mmrApplyBtn");
  document.getElementById("mmrEditBtn").textContent = mmrEditing
    ? "Cancel"
    : "Edit ladder";
  apply.hidden = !mmrEditing || n === 0;
  apply.textContent = "Apply " + n + " change" + (n === 1 ? "" : "s");
  const hint = document.getElementById("mmrHint");
  hint.textContent = mmrEditing
    ? "Drag by the handle or type a rating. Nothing saves until you apply."
    : "";
  hint.title = hint.textContent; // it truncates on a narrow window
  document.body.classList.toggle("mmr-editing", mmrEditing);
}

function toggleMmrEdit() {
  if (
    mmrEditing &&
    mmrPending() &&
    !confirm("Discard " + mmrPending() + " unapplied change(s)?")
  )
    return;
  mmrEditing = !mmrEditing;
  if (mmrEditing) mmrSnapshot = mmrRows.map((r) => ({ ...r }));
  else mmrRows = mmrSnapshot.map((r) => ({ ...r }));
  renderMmr();
}

async function applyMmrEdits() {
  const changed = mmrRows.filter((r) => r.was !== null);
  if (!changed.length) return;
  const apply = document.getElementById("mmrApplyBtn");
  apply.disabled = true;
  let saved = 0;
  for (const r of changed) {
    if (
      await postPlayer(
        r.id,
        { rating_mean: r.mmr, rating_deviation: r.dev },
        null,
      )
    ) {
      r.was = null;
      r.wasDev = null;
      saved++;
    }
  }
  apply.disabled = false;
  mmrEditing = false;
  showToast(
    saved === changed.length ? "success" : "error",
    saved +
      " of " +
      changed.length +
      " rating" +
      (changed.length === 1 ? "" : "s") +
      " updated.",
  );
  await loadPlayers(); // rebuilds the rows from what the server now holds
}

/* Reordering is native HTML5 drag-and-drop with its drag image suppressed, and the row
   moved by transform instead. Native DnD is kept underneath rather than rebuilt on
   pointer events because it brings edge auto-scroll, Esc-to-cancel and the correct
   cursor for free. */
(function initMmrDrag() {
  const body = document.getElementById("mmrBody");
  let from = null; // index the row started at
  let to = null; // index it would land on right now
  let rowH = 0;
  let startY = 0;
  let scroller = null; // the ancestor the browser auto-scrolls during the drag
  let startScroll = 0;

  /* The list is taller than its viewport, so the drag will scroll — and the rows move
     under a cursor whose clientY hasn't changed. Every offset below is therefore in
     layout space, which means knowing which ancestor did the scrolling. */
  const scrollerFor = (el) => {
    for (let p = el.parentElement; p; p = p.parentElement) {
      if (
        /(auto|scroll)/.test(getComputedStyle(p).overflowY) &&
        p.scrollHeight > p.clientHeight
      )
        return p;
    }
    return document.scrollingElement;
  };

  /* One transparent pixel. Assigning it as the drag image is what stops the browser
     drawing the row under the cursor in two dimensions. */
  const BLANK = new Image();
  BLANK.src =
    "data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7";

  const clamp = (v, lo, hi) => (v < lo ? lo : v > hi ? hi : v);

  /* Everything between the row's old index and its current one slides one row out of
     the way; everything outside that span sits still. */
  const layout = () => {
    const rows = body.rows;
    for (let i = 0; i < rows.length; i++) {
      let shift = 0;
      if (i === from)
        shift = null; // handled below
      else if (from < to && i > from && i <= to) shift = -rowH;
      else if (to < from && i >= to && i < from) shift = rowH;
      if (shift !== null)
        rows[i].style.transform = shift ? "translateY(" + shift + "px)" : "";
    }
  };

  const reset = () => {
    for (const r of body.rows) {
      r.style.transform = "";
      r.classList.remove("dragging");
    }
    from = to = null;
  };

  /* draggable stays on the <tr> so the whole row is the drag source, but the drag is
     refused unless the pointer went down on the grip — otherwise the row is just text. */
  let grabbed = false;
  body.addEventListener("pointerdown", (e) => {
    grabbed = !!e.target.closest(".grip");
  });
  document.addEventListener("pointerup", () => {
    grabbed = false;
  });

  body.addEventListener("dragstart", (e) => {
    const tr = e.target.closest("tr");
    if (!tr || !mmrEditing || !grabbed) {
      e.preventDefault();
      return;
    }
    from = to = +tr.dataset.i;
    /* The pitch between two rows, not one row's own height: the last row's box rounds
       to half a pixel short, and reading the height off whichever row you happened to
       grab would put that error into every shift. */
    rowH =
      body.rows.length > 1
        ? body.rows[1].offsetTop - body.rows[0].offsetTop
        : tr.getBoundingClientRect().height;
    startY = e.clientY;
    scroller = scrollerFor(body);
    startScroll = scroller.scrollTop;
    tr.classList.add("dragging");
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setDragImage(BLANK, 0, 0);
    e.dataTransfer.setData("text/plain", String(from)); // Firefox needs a payload
  });

  /* On the document, not the tbody: the pointer is allowed to wander off the table, the
     row simply stops following it at the end of the list. */
  document.addEventListener("dragover", (e) => {
    if (from === null) return;
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
    const n = body.rows.length;
    /* clientY is a viewport coordinate and the rows are not: while the browser
       auto-scrolls at the edge of the list the pointer can sit perfectly still and
       still be over a different row. Adding the scroll delta puts this back in the
       same space as the transforms. */
    const moved = e.clientY - startY + (scroller.scrollTop - startScroll);
    const dy = clamp(moved, -from * rowH, (n - 1 - from) * rowH);
    to = clamp(from + Math.round(dy / rowH), 0, n - 1);
    layout();
    body.rows[from].style.transform = "translateY(" + dy + "px)";
  });

  document.addEventListener("drop", (e) => {
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
  body.addEventListener("dragend", () => {
    grabbed = false;
    if (from === null) return;
    reset();
    renderMmr();
  });
})();
