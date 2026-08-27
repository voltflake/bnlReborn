/* ---------- maps ---------- */
let mapsLoaded = false;
let mapRows = [];
let mapPools = {};
let mapFilter = "";
let mapEditing = null;
let mapSnapshot = [];
let mapDraft = [];
let mapChangesExpanded = false;

async function loadMaps(force) {
  if (mapsLoaded && !force) {
    renderMaps();
    return;
  }
  const res = await fetch("/api/maps");
  if (!res.ok) return;
  const data = await res.json();
  mapRows = data.maps || [];
  mapPools = data.pools || {};
  mapsLoaded = true;
  document.getElementById("railMaps").textContent = mapRows.length;
  renderMaps();
}

function mapLabel(pool) {
  return (
    { friendly: "Casual", ranked: "Ranked", custom: "Customs" }[pool] || pool
  );
}

function mapModeLabel(match) {
  return (
    {
      ShieldRush2: "Shield Rush",
      ShieldCapture: "Shield Capture",
      Tutorial: "Tutorial",
      TimeTrial: "Time Trial",
    }[match] ||
    match ||
    "Unknown"
  );
}

const TIME_TRIAL_HERO_NAMES = {
  unit_hero_abe: "Yeti",
  unit_hero_astro: "Astraella",
  unit_hero_boxer: "Sweet Science",
  unit_hero_cogwheel: "Cogwheel",
  unit_hero_djinn: "Dream Genie",
  unit_hero_doc_eliza: "Doc Eliza",
  unit_hero_engineer: "Tony",
  unit_hero_hunter: "Nigel",
  unit_hero_kira: "Kira",
  unit_hero_kreepy: "Kreepy",
  unit_hero_magnus: "Vander",
  unit_hero_ninja: "Ninja",
  unit_hero_roly: "Roly",
  unit_hero_sarge_stone: "Sarge",
  unit_hero_trondson: "Trondson",
};

function mapObjectiveLabel(map) {
  if (map.match === "TimeTrial")
    return (
      TIME_TRIAL_HERO_NAMES[map.time_trial_hero_key] ||
      map.time_trial_hero ||
      "Time Trial"
    );
  const cubes = (map.cubes || 0) + 1;
  return cubes + (cubes === 1 ? " cube" : " cubes");
}

function mapImageSrc(map) {
  const remote = [map.image, map.large_image].find(
    (value) => typeof value === "string" && /^https?:\/\//i.test(value.trim()),
  );
  return remote
    ? remote.trim()
    : "/assets/maps/" + encodeURIComponent(map.image_asset || map.key + ".jpg");
}

function selectMapPoolFilter(btn) {
  mapFilter = btn.dataset.pool || "";
  document
    .querySelectorAll("#mapPoolChips .chip")
    .forEach((c) => c.classList.toggle("on", c === btn));
  renderMapPoolControls();
  renderMaps();
}

function renderMapPoolControls() {
  const controls = document.getElementById("poolControls");
  if (!controls || !window.controlPanelAdmin) return;
  controls.innerHTML =
    !mapEditing && ["friendly", "ranked", "custom"].includes(mapFilter)
      ? '<button class="save-btn" onclick="beginPoolEdit(\'' +
        mapFilter +
        "')\">Edit " +
        esc(mapLabel(mapFilter)) +
        " map pool</button>"
      : "";
}

function beginPoolEdit(pool) {
  if (!window.controlPanelAdmin) return;
  mapEditing = pool;
  mapSnapshot = (mapPools[pool] || []).slice();
  const selected = new Set(mapSnapshot);
  mapRows.forEach((m) => {
    m._selected = selected.has(m.key);
  });
  mapDraft = mapSnapshot.concat(
    mapRows.map((m) => m.key).filter((k) => !mapSnapshot.includes(k)),
  );
  mapChangesExpanded = false;
  document.getElementById("poolEditBar").classList.add("open");
  document.getElementById("mapToolbar").hidden = true;
  renderMapPoolControls();
  updateMapEditStatus();
  renderMaps();
}

function mapPoolDirty() {
  const selected = mapDraft.filter(
    (k) => mapRows.find((m) => m.key === k)._selected,
  );
  return (
    selected.length !== mapSnapshot.length ||
    selected.some((k, i) => k !== mapSnapshot[i])
  );
}

function selectedMapKeys() {
  return mapDraft.filter((k) => mapRows.find((m) => m.key === k)._selected);
}

function updateMapEditStatus(saved) {
  if (!mapEditing) return;
  const selected = selectedMapKeys();
  const added = selected.filter((k) => !mapSnapshot.includes(k));
  const removed = mapSnapshot.filter((k) => !selected.includes(k));
  const reordered =
    !added.length &&
    !removed.length &&
    selected.some((k, i) => k !== mapSnapshot[i]);
  const count = added.length + removed.length + (reordered ? 1 : 0);
  const status = saved
    ? "Changes submitted"
    : count
      ? count + " pending " + (count === 1 ? "change" : "changes")
      : "No pending changes";
  const details = removed
    .map(
      (key) =>
        '<li class="removed">Removed ' +
        esc(mapRows.find((m) => m.key === key)?.name || key) +
        "</li>",
    )
    .concat(
      added.map(
        (key) =>
          '<li class="added">Added ' +
          esc(mapRows.find((m) => m.key === key)?.name || key) +
          "</li>",
      ),
    );
  if (reordered) details.push('<li class="added">Map order changed</li>');
  document.getElementById("poolEditTitle").innerHTML =
    "<b>Editing " +
    esc(mapLabel(mapEditing)) +
    ' map pool <span class="pool-map-count">&middot; ' +
    selected.length +
    " maps</span></b>" +
    '<div><div class="pool-edit-status ' +
    (saved ? "saved" : count ? "pending" : "") +
    '"><span class="pool-change-summary">' +
    status +
    "</span>" +
    (count
      ? '<button class="pool-change-toggle" onclick="toggleMapChangeDetails()">' +
        (mapChangesExpanded ? "Hide details" : "Show details") +
        "</button>"
      : "") +
    "</div>" +
    (count && mapChangesExpanded
      ? '<ul class="pool-change-list">' + details.join("") + "</ul>"
      : "") +
    "</div>";
  document
    .getElementById("poolEditBar")
    .classList.toggle("details-open", count > 0 && mapChangesExpanded);
  document.getElementById("poolDoneEditBtn").hidden = count > 0;
  document.getElementById("poolCancelBtn").hidden = count === 0;
  document.getElementById("poolSubmitBtn").hidden = count === 0;
}

function toggleMapChangeDetails() {
  mapChangesExpanded = !mapChangesExpanded;
  updateMapEditStatus();
}

function endPoolEdit() {
  if (mapPoolDirty()) return;
  mapEditing = null;
  mapFilter = "";
  document.getElementById("poolEditBar").classList.remove("open");
  document.getElementById("mapToolbar").hidden = false;
  document
    .querySelectorAll("#mapPoolChips .chip")
    .forEach((c) => c.classList.toggle("on", !c.dataset.pool));
  renderMapPoolControls();
  renderMaps();
}

function cancelPoolEdit() {
  const selected = new Set(mapSnapshot);
  mapRows.forEach((m) => {
    m._selected = selected.has(m.key);
  });
  mapDraft = mapSnapshot.concat(
    mapRows.map((m) => m.key).filter((k) => !selected.has(k)),
  );
  mapChangesExpanded = false;
  updateMapEditStatus();
  renderMaps();
}

function toggleMapMembership(key) {
  const map = mapRows.find((m) => m.key === key);
  map._selected = !map._selected;
  mapDraft = mapDraft.filter((k) => k !== key);
  if (map._selected) {
    const lastSelected = mapDraft.findLastIndex(
      (k) => mapRows.find((m) => m.key === k)._selected,
    );
    mapDraft.splice(lastSelected + 1, 0, key);
  } else mapDraft.push(key);
  updateMapEditStatus();
  renderMaps();
}

function moveMap(key, direction) {
  const selected = selectedMapKeys();
  const at = selected.indexOf(key),
    to = at + direction;
  if (to < 0 || to >= selected.length) return;
  const other = selected[to];
  const a = mapDraft.indexOf(key),
    b = mapDraft.indexOf(other);
  [mapDraft[a], mapDraft[b]] = [mapDraft[b], mapDraft[a]];
  updateMapEditStatus();
  renderMaps();
}

async function submitPoolEdit() {
  const maps = selectedMapKeys();
  const button = document.getElementById("poolSubmitBtn");
  button.disabled = true;
  try {
    const res = await fetch("/api/map-pools/" + mapEditing, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ maps }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || "Could not update map pool");
    mapPools[mapEditing] = data.maps.slice();
    mapSnapshot = data.maps.slice();
    mapRows.forEach((m) => {
      m.pools = m.pools.filter((p) => p !== mapEditing);
      if (maps.includes(m.key)) m.pools.push(mapEditing);
    });
    updateMapEditStatus(true);
    renderMaps();
  } catch (e) {
    alert(e.message);
  } finally {
    button.disabled = false;
  }
}

function renderMaps() {
  if (!mapsLoaded) return;
  let rows = mapEditing
    ? mapDraft.map((k) => mapRows.find((m) => m.key === k))
    : mapRows.slice();
  if (!mapEditing && mapFilter === "unused")
    rows = rows.filter((m) => !(m.pools || []).length);
  else if (!mapEditing && mapFilter === "time-trial")
    rows = rows.filter((m) => m.match === "TimeTrial");
  else if (!mapEditing && mapFilter === "tutorial")
    rows = rows.filter((m) => m.match === "Tutorial");
  else if (!mapEditing && mapFilter)
    rows = rows.filter((m) => (m.pools || []).includes(mapFilter));
  rows.sort(
    mapEditing
      ? () => 0
      : (a, b) =>
          a.name.localeCompare(b.name, undefined, { sensitivity: "base" }),
  );
  let dividerState = null;
  document.getElementById("mapGrid").innerHTML = rows
    .map((m) => {
      const selected = !!m._selected;
      let divider = "";
      if (mapEditing && selected !== dividerState) {
        dividerState = selected;
        divider =
          '<div class="map-pool-divider">' +
          (selected
            ? "In " + esc(mapLabel(mapEditing)) + " pool"
            : "Unused in " + esc(mapLabel(mapEditing))) +
          "</div>";
      }
      const selectedKeys = mapEditing ? selectedMapKeys() : [];
      const pos = mapEditing && selected ? selectedKeys.indexOf(m.key) : -1;
      const art = mapImageSrc(m);
      const activePools = (m.pools || []).filter((pool) =>
        ["friendly", "ranked", "custom"].includes(pool),
      );
      const tags = mapEditing
        ? ""
        : '<div class="map-pool-hint">Used in</div><div class="map-tags">' +
          (activePools.length
            ? activePools
                .map(
                  (pool) =>
                    '<span class="map-tag active">' +
                    esc(mapLabel(pool)) +
                    "</span>",
                )
                .join("")
            : '<span class="map-tag none">Nowhere</span>') +
          "</div>";
      const footer = mapEditing
        ? '<div class="map-state-footer ' +
          (selected ? "included" : "excluded") +
          '">' +
          (selected
            ? '<span class="map-order-controls"><button class="map-order-step" onclick="moveMap(\'' +
              esc(m.key) +
              "',-1)\" " +
              (pos === 0 ? "disabled" : "") +
              ">&#9664;</button>" +
              '<span class="map-order-value">' +
              String(pos + 1).padStart(2, "0") +
              "</span>" +
              '<button class="map-order-step" onclick="moveMap(\'' +
              esc(m.key) +
              "',1)\" " +
              (pos === selectedKeys.length - 1 ? "disabled" : "") +
              ">&#9654;</button></span>In pool"
            : "Unused") +
          '<button class="map-membership-action" onclick="toggleMapMembership(\'' +
          esc(m.key) +
          "')\">" +
          (selected ? "Remove" : "Add to pool") +
          "</button></div>"
        : "";
      return (
        divider +
        '<article class="map-tile ' +
        (mapEditing
          ? "map-editing " + (selected ? "pool-included" : "pool-excluded")
          : "") +
        '">' +
        '<img class="map-shot" src="' +
        esc(art) +
        '" alt="" onerror="this.outerHTML=\'<div class=&quot;map-shot none&quot;>No preview</div>\'">' +
        '<div class="map-body"><div class="map-title"><div class="map-name">' +
        esc(m.name) +
        "</div>" +
        '<div class="map-meta">' +
        mapObjectiveLabel(m) +
        " &middot; " +
        esc(mapModeLabel(m.match)) +
        "</div></div>" +
        (tags ? tags : "") +
        (!mapEditing
          ? '<div class="map-downloads"><span class="map-download-hint">Downloads</span>' +
            '<a class="map-download" href="/api/cards/' +
            encodeURIComponent(m.key) +
            '" download="' +
            esc(m.key) +
            '-card.json">Card info</a>' +
            '<a class="map-download" href="/api/maps/' +
            encodeURIComponent(m.key) +
            '/download">BNLBIN</a></div>'
          : "") +
        "</div>" +
        footer +
        "</article>"
      );
    })
    .join("");
}

registerView("maps", { enter: () => loadMaps() });
