const form = document.getElementById('chat-form');
const questionInput = document.getElementById('question');
const askButton = document.getElementById('ask-button');
const stopButton = document.getElementById('stop-button');
const thread = document.getElementById('thread');
const emptyEl = document.getElementById('empty');
const clearButton = document.getElementById('clear-history');
const statusEl = document.getElementById('index-status');
const statusText = document.getElementById('index-status-text');
const toastEl = document.getElementById('toast');
const deepToggle = document.getElementById('deep-toggle');

const DEEP_KEY = 'docrag.deep.v1';
try { deepToggle.checked = localStorage.getItem(DEEP_KEY) === '1'; } catch { /* Speicher nicht verfügbar */ }
deepToggle.addEventListener('change', () => {
  try { localStorage.setItem(DEEP_KEY, deepToggle.checked ? '1' : '0'); } catch { /* ignorieren */ }
});

const STORE_KEY = 'docrag.history.v1';
const MAX_HISTORY = 30;

let history = loadHistory();
let controller = null;
let turnCounter = 0;
let statusTimer = null;

/* ---------- Verlauf ---------- */

function loadHistory() {
  try {
    const raw = localStorage.getItem(STORE_KEY);
    const parsed = raw ? JSON.parse(raw) : [];
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function saveHistory() {
  try {
    localStorage.setItem(STORE_KEY, JSON.stringify(history.slice(-MAX_HISTORY)));
  } catch { /* Speicher nicht verfügbar: Verlauf gilt nur für diese Seite */ }
}

function refreshChrome() {
  emptyEl.classList.toggle('hidden', thread.querySelector('.turn') !== null);
  clearButton.classList.toggle('hidden', history.length === 0);
}

clearButton.addEventListener('click', () => {
  history = [];
  saveHistory();
  thread.querySelectorAll('.turn').forEach(t => t.remove());
  refreshChrome();
  questionInput.focus();
});

/* ---------- Eingabe ---------- */

function autosize() {
  questionInput.style.height = 'auto';
  questionInput.style.height = Math.min(questionInput.scrollHeight, 200) + 'px';
}

questionInput.addEventListener('input', autosize);

questionInput.addEventListener('keydown', (e) => {
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
    e.preventDefault();
    form.requestSubmit();
  }
});

stopButton.addEventListener('click', () => controller?.abort());

form.addEventListener('submit', (event) => {
  event.preventDefault();
  const question = questionInput.value.trim();
  if (!question || controller) return;
  questionInput.value = '';
  autosize();
  ask(question);
});

function setBusy(busy) {
  askButton.classList.toggle('hidden', busy);
  stopButton.classList.toggle('hidden', !busy);
  questionInput.disabled = busy;
  if (!busy) questionInput.focus();
}

async function ask(question, existingTurn) {
  const turn = existingTurn ?? createTurn(question);
  const body = turn.querySelector('.turn-body');
  const deep = deepToggle.checked;
  body.replaceChildren(pendingView(deep));
  refreshChrome();
  scrollToTurn(turn);

  controller = new AbortController();
  setBusy(true);

  try {
    const response = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question, deep }),
      signal: controller.signal
    });

    if (!response.ok) {
      throw new Error(`Der Server antwortete mit Status ${response.status}.`);
    }

    const data = await response.json();
    renderAnswer(turn, question, data);
    history.push({ question, data });
    saveHistory();
  } catch (err) {
    if (err.name === 'AbortError') {
      body.replaceChildren(errorView('Anfrage abgebrochen', 'Die Frage wurde nicht beantwortet.', () => ask(question, turn)));
    } else {
      const network = err instanceof TypeError;
      body.replaceChildren(errorView(
        network ? 'Server nicht erreichbar' : 'Antwort fehlgeschlagen',
        network ? 'Prüfe, ob der docRAG-Server läuft, und versuche es erneut.' : err.message,
        () => ask(question, turn)
      ));
    }
  } finally {
    controller = null;
    setBusy(false);
    refreshChrome();
  }
}

function createTurn(question) {
  const turn = document.createElement('article');
  turn.className = 'turn';
  turn.dataset.id = String(++turnCounter);

  const q = document.createElement('p');
  q.className = 'question';
  q.textContent = question;

  const body = document.createElement('div');
  body.className = 'turn-body';

  turn.append(q, body);
  thread.appendChild(turn);
  return turn;
}

function scrollToTurn(turn) {
  turn.scrollIntoView({ behavior: prefersReducedMotion() ? 'auto' : 'smooth', block: 'start' });
}

function prefersReducedMotion() {
  return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
}

/* ---------- Zustände ---------- */

function pendingView(deep) {
  const wrap = document.createElement('div');
  wrap.setAttribute('aria-busy', 'true');
  wrap.innerHTML = '<div class="pending"><div class="bars"><i></i><i></i><i></i></div></div>' +
    '<p class="pending-label"></p>';
  wrap.querySelector('.pending-label').textContent = deep
    ? 'Deep Research läuft: plane Suchanfragen, lese mehrere Stellen und werte sie aus. Das kann einige Minuten dauern …'
    : 'Durchsuche die Dokumente …';
  return wrap;
}

function errorView(title, message, onRetry) {
  const box = document.createElement('div');
  box.className = 'error-box';
  box.setAttribute('role', 'alert');

  const strong = document.createElement('strong');
  strong.textContent = title;
  const p = document.createElement('p');
  p.textContent = message;
  const retry = document.createElement('button');
  retry.type = 'button';
  retry.className = 'ghost';
  retry.textContent = 'Erneut fragen';
  retry.addEventListener('click', onRetry);

  box.append(strong, p, retry);
  return box;
}

/* ---------- Antwort ---------- */

function renderAnswer(turn, question, data) {
  const body = turn.querySelector('.turn-body');
  const sources = Array.isArray(data.sources) ? data.sources : [];
  const turnId = turn.dataset.id;
  const answer = (data.answer || '').trim();

  const answerEl = document.createElement('div');
  answerEl.className = 'answer-body';
  answerEl.appendChild(renderMarkdown(answer || 'Keine Antwort erhalten.', sources.length, turnId));

  const actions = document.createElement('div');
  actions.className = 'answer-actions';
  actions.append(
    ghostButton('Antwort kopieren', () => copyText(answer, 'Antwort kopiert')),
    ghostButton('Neu fragen', () => ask(question, turn))
  );

  const sourcesEl = document.createElement('section');
  sourcesEl.className = 'sources';
  sourcesEl.setAttribute('aria-label', 'Quellen');

  const title = document.createElement('h2');
  title.className = 'sources-title';
  title.textContent = sources.length === 1 ? '1 Quelle' : `${sources.length} Quellen`;
  sourcesEl.appendChild(title);

  if (sources.length === 0) {
    title.textContent = 'Keine Quellen';
    const none = document.createElement('p');
    none.className = 'no-sources';
    none.textContent = 'Zu dieser Frage wurde keine passende Stelle gefunden. Formuliere sie anders oder prüfe, ob die Dokumente indexiert sind.';
    sourcesEl.appendChild(none);
  } else {
    const terms = queryTerms(question);
    sources.forEach((s, i) => sourcesEl.appendChild(sourceCard(s, i + 1, turnId, terms, i === 0)));
  }

  const parts = [answerEl, actions, sourcesEl];
  if (data.deep) parts.unshift(researchNote(data));
  body.replaceChildren(...parts);
}

function researchNote(data) {
  const queries = Array.isArray(data.queries) ? data.queries : [];
  const secs = typeof data.elapsedSeconds === 'number' ? ` in ${Math.round(data.elapsedSeconds)} s` : '';
  const details = document.createElement('details');
  details.className = 'research-note';

  const summary = document.createElement('summary');
  summary.textContent = `Deep Research${secs} · ${queries.length} ${queries.length === 1 ? 'Suchanfrage' : 'Suchanfragen'}`;
  details.appendChild(summary);

  const list = document.createElement('ul');
  queries.forEach((q) => {
    const li = document.createElement('li');
    li.textContent = q;
    list.appendChild(li);
  });
  details.appendChild(list);
  return details;
}

function ghostButton(label, onClick) {
  const b = document.createElement('button');
  b.type = 'button';
  b.className = 'ghost';
  b.textContent = label;
  b.addEventListener('click', onClick);
  return b;
}

const CHEVRON = '<svg class="chev" viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M5 8l5 5 5-5"/></svg>';

function sourceCard(source, index, turnId, terms, open) {
  const card = document.createElement('div');
  card.className = 'source-card';
  card.id = `src-${turnId}-${index}`;
  card.dataset.open = String(open);

  const head = document.createElement('button');
  head.type = 'button';
  head.className = 'source-head';
  head.setAttribute('aria-expanded', String(open));

  const num = document.createElement('span');
  num.className = 'source-num';
  num.textContent = index;

  const name = document.createElement('span');
  name.className = 'source-name';
  const b = document.createElement('b');
  b.textContent = source.fileName || 'Unbenannte Datei';
  name.appendChild(b);
  if (source.location) {
    const small = document.createElement('small');
    small.textContent = source.location;
    name.appendChild(small);
  }

  head.append(num, name);

  if (typeof source.score === 'number' && source.score > 0) {
    const pct = Math.max(0, Math.min(1, source.score));
    const score = document.createElement('span');
    score.className = 'score';
    score.title = 'Ähnlichkeit zur Frage';
    score.innerHTML = '<span class="score-bar"><i></i></span><span></span>';
    score.querySelector('i').style.width = `${Math.round(pct * 100)}%`;
    score.lastElementChild.textContent = `${Math.round(pct * 100)} %`;
    head.appendChild(score);
  }

  head.insertAdjacentHTML('beforeend', CHEVRON);

  const bodyEl = document.createElement('div');
  bodyEl.className = 'source-body';
  bodyEl.hidden = !open;

  const snippet = document.createElement('p');
  snippet.className = 'snippet';
  appendHighlighted(snippet, source.snippet || '', terms);
  bodyEl.appendChild(snippet);

  const foot = document.createElement('div');
  foot.className = 'source-foot';
  if (source.filePath) {
    const path = document.createElement('code');
    path.textContent = source.filePath;
    foot.appendChild(path);
    foot.appendChild(ghostButton('Pfad kopieren', () => copyText(source.filePath, 'Pfad kopiert')));
    if (/\.pdf$/i.test(source.filePath)) foot.appendChild(pdfLink(source));
  }
  if (foot.childElementCount) bodyEl.appendChild(foot);

  head.addEventListener('click', () => setOpen(card, card.dataset.open !== 'true'));

  card.append(head, bodyEl);
  return card;
}

// Öffnet die ganze PDF im Browser-Viewer (inline, ohne Download) und springt zur Fundstelle.
function pdfLink(source) {
  const m = /Seite\s+(\d+)/i.exec(source.location || '');
  const page = m ? Number(m[1]) : 1;
  const label = m ? `PDF öffnen (Seite ${page})` : 'PDF öffnen';
  const b = ghostButton(label, async () => {
    // Tab synchron im Klick öffnen (Popup-Blocker), Ziel setzen sobald der Blob da ist.
    const tab = window.open('', '_blank');
    try {
      const url = await pdfBlobUrl(source.filePath);
      if (tab) tab.location.href = `${url}#page=${page}`;
      else window.location.href = `${url}#page=${page}`;
    } catch (err) {
      if (tab) tab.close();
      toast('PDF konnte nicht geladen werden');
    }
  });
  return b;
}

const pdfBlobCache = new Map();

function pdfBlobUrl(filePath) {
  if (!pdfBlobCache.has(filePath)) {
    pdfBlobCache.set(filePath, fetch(`/api/file?path=${encodeURIComponent(filePath)}`)
      .then((r) => {
        if (!r.ok) throw new Error(`HTTP ${r.status}`);
        return r.blob();
      })
      .then((blob) => URL.createObjectURL(blob))
      .catch((err) => { pdfBlobCache.delete(filePath); throw err; }));
  }
  return pdfBlobCache.get(filePath);
}

function setOpen(card, open) {
  card.dataset.open = String(open);
  card.querySelector('.source-head').setAttribute('aria-expanded', String(open));
  card.querySelector('.source-body').hidden = !open;
}

/* ---------- Textmarker ---------- */

function queryTerms(question) {
  const words = question.toLowerCase().match(/[\p{L}\p{N}]{4,}/gu) || [];
  return [...new Set(words)];
}

function appendHighlighted(el, text, terms) {
  if (!terms.length) { el.textContent = text; return; }
  const pattern = new RegExp(`(${terms.map(escapeRegex).join('|')})`, 'giu');
  let last = 0;
  for (const m of text.matchAll(pattern)) {
    if (m.index > last) el.append(text.slice(last, m.index));
    const mark = document.createElement('mark');
    mark.textContent = m[0];
    el.appendChild(mark);
    last = m.index + m[0].length;
  }
  if (last < text.length) el.append(text.slice(last));
}

function escapeRegex(s) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/* ---------- Mini-Markdown (DOM-basiert, kein innerHTML) ---------- */

function renderMarkdown(text, sourceCount, turnId) {
  const frag = document.createDocumentFragment();
  const lines = text.replace(/\r\n/g, '\n').split('\n');
  let i = 0;

  while (i < lines.length) {
    const line = lines[i];

    if (/^\s*$/.test(line)) { i++; continue; }

    if (/^```/.test(line)) {
      const buf = [];
      i++;
      while (i < lines.length && !/^```/.test(lines[i])) buf.push(lines[i++]);
      i++;
      const pre = document.createElement('pre');
      const code = document.createElement('code');
      code.textContent = buf.join('\n');
      pre.appendChild(code);
      frag.appendChild(pre);
      continue;
    }

    const listMatch = line.match(/^\s*([-*]|\d+[.)])\s+/);
    if (listMatch) {
      const ordered = /\d/.test(listMatch[1]);
      const list = document.createElement(ordered ? 'ol' : 'ul');
      while (i < lines.length) {
        const m = lines[i].match(/^\s*([-*]|\d+[.)])\s+(.*)$/);
        if (!m || /\d/.test(m[1]) !== ordered) break;
        const li = document.createElement('li');
        appendInline(li, m[2], sourceCount, turnId);
        list.appendChild(li);
        i++;
      }
      frag.appendChild(list);
      continue;
    }

    const buf = [];
    while (i < lines.length && !/^\s*$/.test(lines[i]) && !/^```/.test(lines[i]) && !/^\s*([-*]|\d+[.)])\s+/.test(lines[i])) {
      buf.push(lines[i++]);
    }
    const p = document.createElement('p');
    appendInline(p, buf.join('\n'), sourceCount, turnId);
    frag.appendChild(p);
  }

  return frag;
}

function appendInline(el, text, sourceCount, turnId) {
  const token = /(\*\*[^*]+\*\*|`[^`]+`|\[(\d{1,2})\])/g;
  let last = 0;
  for (const m of text.matchAll(token)) {
    if (m.index > last) el.append(text.slice(last, m.index));
    const raw = m[0];
    if (raw.startsWith('**')) {
      const strong = document.createElement('strong');
      strong.textContent = raw.slice(2, -2);
      el.appendChild(strong);
    } else if (raw.startsWith('`')) {
      const code = document.createElement('code');
      code.textContent = raw.slice(1, -1);
      el.appendChild(code);
    } else {
      const n = Number(m[2]);
      if (n >= 1 && n <= sourceCount) {
        const a = document.createElement('a');
        a.className = 'cite';
        a.href = `#src-${turnId}-${n}`;
        a.textContent = n;
        a.setAttribute('aria-label', `Quelle ${n}`);
        a.addEventListener('click', (e) => { e.preventDefault(); jumpToSource(`src-${turnId}-${n}`); });
        el.appendChild(a);
      } else {
        el.append(raw);
      }
    }
    last = m.index + raw.length;
  }
  if (last < text.length) el.append(text.slice(last));
}

function jumpToSource(id) {
  const card = document.getElementById(id);
  if (!card) return;
  setOpen(card, true);
  card.scrollIntoView({ behavior: prefersReducedMotion() ? 'auto' : 'smooth', block: 'center' });
  card.classList.add('flash');
  setTimeout(() => card.classList.remove('flash'), 1400);
  card.querySelector('.source-head').focus({ preventScroll: true });
}

/* ---------- Hinweis & Zwischenablage ---------- */

let toastTimer = null;
function toast(message) {
  toastEl.textContent = message;
  toastEl.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toastEl.classList.remove('show'), 1600);
}

async function copyText(text, message) {
  try {
    await navigator.clipboard.writeText(text);
    toast(message);
  } catch {
    toast('Kopieren nicht möglich');
  }
}

/* ---------- Index-Status ---------- */

async function refreshStatus() {
  clearTimeout(statusTimer);
  let delay = 30000;
  try {
    const res = await fetch('/api/ingest/status');
    if (!res.ok) throw new Error();
    const s = await res.json();

    if (s.isRunning) {
      const done = (s.processedFiles ?? 0) + (s.skippedFiles ?? 0) + (s.failedFiles ?? 0);
      setStatus('busy', `Indexiert … ${done}/${s.totalFiles ?? '?'} Dateien`);
      delay = 2000;
    } else if ((s.totalChunksIndexed ?? 0) > 0 || (s.processedFiles ?? 0) > 0) {
      const failed = s.failedFiles ? `, ${s.failedFiles} fehlgeschlagen` : '';
      setStatus('ready', `${s.totalChunksIndexed} Abschnitte indexiert${failed}`);
    } else {
      setStatus('empty', 'Kein Indexlauf seit Serverstart');
    }
  } catch {
    setStatus('offline', 'Server nicht erreichbar');
    delay = 10000;
  }
  statusTimer = setTimeout(refreshStatus, delay);
}

function setStatus(state, text) {
  statusEl.dataset.state = state;
  statusText.textContent = text;
}

/* ---------- Start ---------- */

for (const entry of history) {
  const turn = createTurn(entry.question);
  renderAnswer(turn, entry.question, entry.data);
}
refreshChrome();
refreshStatus();
questionInput.focus();
if (history.length) window.scrollTo({ top: document.body.scrollHeight });
