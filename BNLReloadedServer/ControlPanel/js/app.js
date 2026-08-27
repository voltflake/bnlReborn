/* ---------- shared ---------- */

async function postPlayer(id, body, successMsg) {
  if (!window.controlPanelAdmin) return false;
  try {
    const res = await fetch("/api/players/" + id, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    const data = await res.json();
    if (!res.ok) {
      showToast("error", data.error || "Update failed");
      return false;
    }
    if (successMsg) showToast("success", successMsg);
    return true;
  } catch (e) {
    showToast("error", e.message);
    return false;
  }
}

function showToast(type, msg) {
  const toast = document.getElementById("toast");
  document.getElementById("toastMsg").textContent = msg;
  toast.className = "toast " + type;
  toast.style.display = "block";
  clearTimeout(showToast._t);
  showToast._t = setTimeout(() => {
    toast.style.display = "none";
  }, 3000);
}

function esc(s) {
  const d = document.createElement("div");
  d.textContent = s;
  return d.innerHTML;
}

/* 24-hour regardless of locale: the console beside it is 24-hour, and one panel should
   not run two clocks. */
function fmtUntil(ms) {
  return new Date(Number(ms)).toLocaleString(undefined, {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hour12: false,
  });
}

function fmtRemaining(ms) {
  const m = Math.max(0, Math.round((Number(ms) - Date.now()) / 60000));
  if (m < 60) return "in " + m + " min";
  const h = Math.floor(m / 60);
  if (h < 24) return "in " + h + "h " + String(m % 60).padStart(2, "0") + "m";
  return "in " + Math.floor(h / 24) + "d " + (h % 24) + "h";
}

/* Wherever a live nickname appears it opens that player's editor, so a ban you are
   already looking at doesn't send you to the Players pane to retype the name. */
function playerLink(name, id, cls) {
  const c = "player-link" + (cls ? " " + cls : "");
  return id == null || !window.controlPanelAdmin
    ? "<span" +
        (cls ? ' class="' + cls + '"' : "") +
        ">" +
        esc(name) +
        "</span>"
    : '<button class="' +
        c +
        '" onclick="showPlayerEdit(' +
        id +
        ')">' +
        esc(name) +
        "</button>";
}

document.addEventListener("keydown", (e) => {
  if (e.key === "Escape" && currentPlayerId != null) closePlayerEdit();
});

/* ---------- boot ---------- */

let initialized = false;
let eventRetry = 1000;
let eventSocket = null;
let lastEventAt = 0;
let eventReconnectTimer = null;
let eventRecoveryEnabled = false;
let eventSnapshotReceived = false;

function setEventConnection(state, message) {
  const indicator = document.getElementById("eventConnection");
  if (!indicator) return;
  indicator.hidden = false;
  indicator.className = "event-connection " + state;
  indicator.textContent = message;
}

function stopEventRecovery(message, expired) {
  eventRecoveryEnabled = false;
  if (eventReconnectTimer != null) {
    clearTimeout(eventReconnectTimer);
    eventReconnectTimer = null;
  }
  setEventConnection("stale", message);
  if (expired) showLoginGate("Your session expired. Sign in again.");
}

function scheduleEventReconnect() {
  if (!eventRecoveryEnabled || eventReconnectTimer != null) return;
  const delay = eventRetry;
  setEventConnection(
    "reconnecting",
    "Live updates reconnecting in " +
      Math.ceil(delay / 1000) +
      "s; displayed data may be stale.",
  );
  eventReconnectTimer = setTimeout(() => {
    eventReconnectTimer = null;
    connectEvents();
  }, delay);
  eventRetry = Math.min(eventRetry * 2, 30000);
}

async function eventSessionStillValid() {
  try {
    // A WebSocket handshake failure is intentionally opaque to browser JavaScript. Probe an
    // authenticated route only when the stream died before its initial snapshot, which is the
    // usual signature of a restarted server having forgotten its in-memory session.
    const res = await _origFetch(
      "/api/logs?since=" + encodeURIComponent(logCursor),
      { cache: "no-store" },
    );
    return res.status !== 401 && res.status !== 403;
  } catch {
    // A transport failure is recoverable; the normal backoff remains responsible for it.
    return true;
  }
}

function connectEvents() {
  if (
    !eventRecoveryEnabled ||
    eventSocket?.readyState === WebSocket.OPEN ||
    eventSocket?.readyState === WebSocket.CONNECTING
  )
    return;
  const scheme = location.protocol === "https:" ? "wss:" : "ws:";
  const endpoint = window.controlPanelAdmin
    ? "/api/events?logs_since=" + encodeURIComponent(logCursor)
    : "/api/public/events";
  const socket = new WebSocket(scheme + "//" + location.host + endpoint);
  eventSocket = socket;
  eventSnapshotReceived = false;
  setEventConnection("reconnecting", "Connecting live updates…");

  socket.onopen = () => {
    eventRetry = 1000;
    lastEventAt = Date.now();
  };
  socket.onmessage = (event) => {
    lastEventAt = Date.now();
    let message;
    try {
      message = JSON.parse(event.data);
    } catch {
      return;
    }
    if (!eventSnapshotReceived) {
      eventSnapshotReceived = true;
      setEventConnection("live", "Live updates connected.");
    }
    const data = message.data || {};
    if (message.type === "status") applyStatus(data);
    else if (message.type === "activity") applyActivity(data);
    else if (message.type === "queues") applyQueues(data);
    else if (message.type === "players") applyPlayers(data);
    else if (message.type === "logs") applyLogBatch(data);
  };
  socket.onclose = async (event) => {
    if (eventSocket === socket) eventSocket = null;
    if (!eventRecoveryEnabled) return;
    if (event.code === 1008) {
      stopEventRecovery(
        "Live updates stopped because the session expired.",
        true,
      );
      return;
    }
    if (
      window.controlPanelAdmin &&
      !eventSnapshotReceived &&
      !(await eventSessionStillValid())
    ) {
      stopEventRecovery(
        "Live updates stopped because the session is no longer valid.",
        true,
      );
      return;
    }
    scheduleEventReconnect();
  };
}

/* A WebSocket can be silently dropped by a proxy, leaving the browser with an open
   object and the last received queue snapshot. The server emits heartbeats; if they
   stop, close this socket so its normal reconnect path takes a fresh event snapshot. */
setInterval(() => {
  if (
    eventSocket?.readyState === WebSocket.OPEN &&
    Date.now() - lastEventAt > 45000
  )
    eventSocket.close(4000, "Event stream timed out");
}, 15000);

function init() {
  if (initialized) return;
  initialized = true;
  if (window.controlPanelAdmin) {
    document.getElementById("loginBtn").hidden = true;
    renderBanForm();
  }
  // The stream sends its own initial snapshots; there is no REST bootstrap to race it.
  eventRecoveryEnabled = true;
  connectEvents();
  showPane(paneFromHash(), false);
}

init();
