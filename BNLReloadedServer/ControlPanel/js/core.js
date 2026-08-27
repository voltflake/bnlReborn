/* ============================================================
   BNL Reloaded Control Panel

   Panes: status · players · ladder · bans · tools · console.
   Every number here comes from the API; nothing is invented.
   ============================================================ */

let allPlayers = [];
let currentPlayerId = null;

/* ---------- session ---------- */

function showLoginGate(message) {
  document.getElementById("loginGate").classList.add("active");
  document.getElementById("loginError").textContent = message || "";
}

function hideLoginGate() {
  document.getElementById("loginGate").classList.remove("active");
  document.getElementById("loginError").textContent = "";
}

async function doLogin() {
  const username = document.getElementById("loginUsername").value;
  const input = document.getElementById("loginPassword");
  const password = input.value;
  try {
    const res = await fetch("/api/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username, password }),
    });
    const data = await res.json();
    if (res.ok) {
      document.getElementById("loginUsername").value = "";
      input.value = "";
      // An authenticated reload is what receives the server-included admin controls.
      location.reload();
    } else {
      document.getElementById("loginError").textContent =
        data.error || "Login failed";
    }
  } catch (e) {
    document.getElementById("loginError").textContent = e.message;
  }
}

async function doLogout() {
  try {
    await fetch("/api/logout", { method: "POST" });
  } catch {
    /* ignore */
  }
  location.reload();
}

const _origFetch = window.fetch.bind(window);
window.fetch = async function (input, init) {
  const res = await _origFetch(input, init);
  const url = typeof input === "string" ? input : input.url;
  if (res.status === 401 && !url.includes("/api/login")) {
    showLoginGate();
  }
  return res;
};

/* ---------- chrome and view lifecycle ---------- */

const viewHooks = Object.create(null);
let activePane = null;

function registerView(name, hooks) {
  viewHooks[name] = hooks || {};
}

function paneFromHash() {
  const name = location.hash.replace(/^#\/?/, "");
  return document.getElementById("pane-" + name) ? name : "status";
}

function showPane(name, updateLocation = true) {
  if (!document.getElementById("pane-" + name)) name = "status";
  if (activePane !== name) viewHooks[activePane]?.leave?.();
  document
    .querySelectorAll(".view-pane")
    .forEach((p) => p.classList.toggle("active", p.id === "pane-" + name));
  document
    .querySelectorAll(".rail-item")
    .forEach((b) => b.classList.toggle("active", b.dataset.pane === name));
  document.body.classList.toggle("pane-console", name === "console");
  activePane = name;
  viewHooks[name]?.enter?.();
  if (updateLocation && location.hash !== "#/" + name)
    location.hash = "/" + name;
}

window.addEventListener("hashchange", () => showPane(paneFromHash(), false));
