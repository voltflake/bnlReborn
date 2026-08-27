/* ---------- bans ---------- */

registerView("bans", { enter: () => renderModeration() });

/* One row per penalty, not per player: someone can be sitting out a matchmaking ban
   and a graveyard sentence at the same time, and they lift separately. */
function activeBans() {
  const t = Date.now();
  const rows = [];
  for (const p of allPlayers) {
    if (p.matchmaker_ban_end != null && p.matchmaker_ban_end > t)
      rows.push({
        id: p.id,
        name: p.nickname,
        kind: "mm",
        until: p.matchmaker_ban_end,
        permanent: false,
      });
    if (p.graveyard_permanent === true)
      rows.push({
        id: p.id,
        name: p.nickname,
        kind: "gy",
        until: null,
        permanent: true,
      });
    else if (p.graveyard_leave_time != null && p.graveyard_leave_time > t)
      rows.push({
        id: p.id,
        name: p.nickname,
        kind: "gy",
        until: p.graveyard_leave_time,
        permanent: false,
      });
  }
  // soonest to lift first; permanent ones can't lift on their own, so they sit last
  rows.sort(
    (a, b) => a.permanent - b.permanent || a.until - b.until || a.id - b.id,
  );
  return rows;
}

function renderModeration() {
  const rows = activeBans();
  document.getElementById("banBody").innerHTML = rows.length
    ? rows
        .map(
          (r) =>
            "<tr>" +
            "<td>" +
            playerLink(r.name, r.id) +
            "</td>" +
            '<td><span class="pen-badge' +
            (r.kind === "gy" ? " graveyard" : "") +
            '">' +
            (r.kind === "gy" ? "Graveyard" : "Matchmaking") +
            "</span></td>" +
            "<td>" +
            (r.permanent
              ? '<span class="mod-perm">Never &mdash; permanent</span>'
              : '<span class="mod-until">' +
                fmtRemaining(r.until) +
                "</span> " +
                '<span class="mod-when">' +
                fmtUntil(r.until) +
                "</span>") +
            "</td>" +
            "<td>" +
            (window.controlPanelAdmin
              ? '<button class="unban-btn lift-btn" onclick="liftBan(' +
                r.id +
                ", '" +
                r.kind +
                "')\">Lift</button>"
              : "") +
            "</td>" +
            "</tr>",
        )
        .join("")
    : '<tr><td colspan="4" class="empty-row">Nobody is banned.</td></tr>';

  /* A bare count, like the Players badge beside it. */
  document.getElementById("railBans").textContent = rows.length
    ? String(rows.length)
    : "";
}

const UNIT_MS = { minutes: 60000, hours: 3600000, days: 86400000 };

function durationMs(amountId, unitId) {
  const amount = parseFloat(document.getElementById(amountId).value);
  if (!amount || amount <= 0) return null;
  return amount * UNIT_MS[document.getElementById(unitId).value];
}

/* Two duration groups rather than one, so each penalty keeps its own fields. */
function renderBanForm() {
  if (!window.controlPanelAdmin) return;
  const gy = document.getElementById("f-ban-kind").value === "gy";
  for (const [id, show] of [
    ["mmDuration", !gy],
    ["mmUnit", !gy],
    ["gyDuration", gy],
    ["gyUnit", gy],
    ["gyPermRow", gy],
  ])
    document.getElementById(id).hidden = !show;
}

async function imposeBan() {
  const q = document.getElementById("f-ban-player").value.trim();
  const p = allPlayers.find(
    (x) => x.nickname.toLowerCase() === q.toLowerCase() || String(x.id) === q,
  );
  if (!p) {
    showToast(
      "error",
      q ? 'No player matches "' + q + '".' : "Name the player to ban.",
    );
    return;
  }

  const gy = document.getElementById("f-ban-kind").value === "gy";
  let body, msg;
  if (gy) {
    const permanent = document.getElementById("f-gy-permanent").checked;
    const ms = permanent
      ? null
      : durationMs("f-gy-ban-amount", "f-gy-ban-unit");
    if (!permanent && ms == null) {
      showToast("error", "Enter a duration or mark the ban permanent.");
      return;
    }
    body = {
      graveyard_permanent: permanent,
      graveyard_leave_time: permanent ? null : Date.now() + ms,
    };
    msg = permanent
      ? "Sent to the graveyard permanently."
      : "Sent to the graveyard.";
  } else {
    const ms = durationMs("f-mm-ban-amount", "f-mm-ban-unit");
    if (ms == null) {
      showToast("error", "Enter how long the ban should last.");
      return;
    }
    body = { matchmaker_ban_end: Date.now() + ms };
    msg = "Banned from matchmaking.";
  }

  if (!(await postPlayer(p.id, body, msg))) return;
  document.getElementById("f-ban-player").value = "";
  document.getElementById("f-mm-ban-amount").value = "";
  document.getElementById("f-gy-ban-amount").value = "";
  document.getElementById("f-gy-permanent").checked = false;
  await loadPlayers();
  if (currentPlayerId === p.id) loadPlayer(p.id);
}

async function liftBan(id, kind) {
  const body =
    kind === "gy"
      ? { graveyard_permanent: false, graveyard_leave_time: null }
      : { matchmaker_ban_end: null };
  if (
    !(await postPlayer(
      id,
      body,
      kind === "gy" ? "Graveyard ban lifted." : "Matchmaking ban lifted.",
    ))
  )
    return;
  await loadPlayers();
  if (currentPlayerId === id) loadPlayer(id);
}

/* Bans expire on their own, so the rail count has to be able to fall without anyone
   pressing anything. */
setInterval(() => {
  if (document.getElementById("pane-bans").classList.contains("active"))
    renderModeration();
  else
    document.getElementById("railBans").textContent = activeBans().length || "";
}, 15000);

setInterval(updatePresenceLabels, 1000);
