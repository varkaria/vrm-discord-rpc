"use strict";
const repository = "https://vpm.varkaria.works/index.json";
const list = document.getElementById("packages");
const status = document.getElementById("load-status");
let packages = [];

function element(tag, className, text) {
  const node = document.createElement(tag);
  node.className = className;
  node.textContent = text;
  return node;
}
function safeLink(label, url) {
  try {
    const parsed = new URL(url);
    if (parsed.protocol !== "https:") return null;
    const link = element("a", "", label);
    link.href = parsed.href;
    link.rel = "noopener noreferrer";
    return link;
  } catch { return null; }
}
function render() {
  const query = document.getElementById("search").value.trim().toLowerCase();
  list.replaceChildren();
  const filtered = packages.filter(p => [p.name, p.displayName, p.description].join(" ").toLowerCase().includes(query));
  document.getElementById("count").textContent = String(packages.length);
  status.hidden = filtered.length > 0;
  status.textContent = packages.length ? "No packages match your search." : "Packages will appear here when the first release is published.";
  for (const p of filtered) {
    const card = element("article", "package", "");
    const top = element("div", "package-top", "");
    top.append(element("h3", "", p.displayName || p.name), element("span", "version", "v" + p.version));
    const meta = element("div", "metadata", "");
    meta.append(element("span", "", "Unity " + (p.unity || "—") + "+"), element("span", "", p.name));
    const links = element("div", "package-links", "");
    for (const [label, url] of [["Documentation ↗", p.documentationUrl], ["Changelog ↗", p.changelogUrl], ["Download ZIP ↓", p.url]]) {
      const link = safeLink(label, url);
      if (link) links.append(link);
    }
    card.append(top, element("p", "description", p.description || ""), meta, links);
    list.append(card);
  }
}
document.getElementById("search").addEventListener("input", render);
document.getElementById("copy-repository").addEventListener("click", async () => {
  const feedback = document.getElementById("copy-status");
  try { await navigator.clipboard.writeText(repository); feedback.textContent = "Repository URL copied."; }
  catch { feedback.textContent = "Copy this URL: " + repository; }
});
async function load() {
  try {
    const response = await fetch("index.json", { cache: "no-cache" });
    if (!response.ok) throw new Error("HTTP " + response.status);
    const listing = await response.json();
    packages = Object.values(listing.packages).map(p => Object.values(p.versions)
      .filter(v => /^\d+\.\d+\.\d+$/.test(v.version))
      .sort((a, b) => b.version.localeCompare(a.version, "en", { numeric: true }))[0])
      .filter(Boolean).sort((a, b) => (a.displayName || a.name).localeCompare(b.displayName || b.name));
    render();
  } catch {
    status.hidden = false;
    status.textContent = "Could not load the package list. Refresh to retry, or use the repository URL below.";
  }
}
load();
