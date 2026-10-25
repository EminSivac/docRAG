const form = document.getElementById('chat-form');
const questionInput = document.getElementById('question');
const askButton = document.getElementById('ask-button');
const resultEl = document.getElementById('result');
const answerText = document.getElementById('answer-text');
const sourcesList = document.getElementById('sources-list');
const errorEl = document.getElementById('error');

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  const question = questionInput.value.trim();
  if (!question) return;

  askButton.disabled = true;
  askButton.textContent = 'Suche...';
  errorEl.classList.add('hidden');
  resultEl.classList.add('hidden');

  try {
    const response = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ question })
    });

    if (!response.ok) {
      throw new Error(`Server antwortete mit Status ${response.status}`);
    }

    const data = await response.json();
    renderResult(data);
  } catch (err) {
    errorEl.textContent = `Fehler: ${err.message}`;
    errorEl.classList.remove('hidden');
  } finally {
    askButton.disabled = false;
    askButton.textContent = 'Fragen';
  }
});

function renderResult(data) {
  answerText.textContent = data.answer || '(keine Antwort)';
  sourcesList.innerHTML = '';

  if (!data.sources || data.sources.length === 0) {
    sourcesList.innerHTML = '<p class="source-snippet">Keine Quellen gefunden.</p>';
  } else {
    for (const source of data.sources) {
      const item = document.createElement('div');
      item.className = 'source-item';

      const meta = document.createElement('div');
      meta.className = 'source-meta';
      meta.textContent = `${source.fileName}${source.location ? ' — ' + source.location : ''}`;

      const snippet = document.createElement('div');
      snippet.className = 'source-snippet';
      snippet.textContent = source.snippet;

      item.appendChild(meta);
      item.appendChild(snippet);
      sourcesList.appendChild(item);
    }
  }

  resultEl.classList.remove('hidden');
}
