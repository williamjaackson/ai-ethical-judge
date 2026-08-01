const status = document.querySelector('#status');
const board = document.querySelector('#board');
const waiting = document.querySelector('#waiting');
const totalLine = document.querySelector('#total');
const summaryLine = document.querySelector('#summary');
const sessionId = sessionStorage.getItem('judgeSessionId');

// How often to re-read the results endpoint when the live feed is unavailable.
// Judging rounds are five seconds apart, so this is comfortably ahead of them.
const PollInterval = 1000;

// The bar, value and comment nodes of every criterion currently on screen,
// keyed by the criterion it belongs to. Reusing the nodes across updates is
// what lets CSS animate each bar from its old score to its new one.
let rows = new Map();

// The criteria the board was last built for, and the snapshot it last drew, so
// repeated polls of an unchanged judgement do not redraw anything.
let builtFor = '';
let drawn = '';

if (!sessionId) location.replace('./');

function keyOf(group, criterion) {
  return `${group} ${criterion}`;
}

function show(snapshot) {
  if (!snapshot) {
    showWaiting('Waiting for the first judgement.');
    return;
  }

  const version = JSON.stringify(snapshot);
  if (version === drawn) return;
  drawn = version;

  const groups = snapshot.groups ?? [];
  const criteria = JSON.stringify(
    groups.flatMap(group => group.scores.map(score => keyOf(group.label, score.criterion))));

  // The criteria only change when a new judge is configured. While they hold
  // steady the existing bars are kept and simply re-pointed at the new scores.
  if (criteria !== builtFor) {
    build(groups);
    builtFor = criteria;
  }

  draw(snapshot);

  waiting.hidden = true;
  totalLine.hidden = false;
  summaryLine.hidden = false;
  totalLine.textContent = `${snapshot.total} out of ${snapshot.maxTotal}`;
  summaryLine.textContent = snapshot.summary;
  status.textContent = `Judged ${new Date(snapshot.producedAt).toLocaleTimeString()}`;
}

function showWaiting(message) {
  builtFor = '';
  drawn = '';
  rows = new Map();
  board.replaceChildren();
  waiting.hidden = false;
  waiting.textContent = message;
  totalLine.hidden = true;
  summaryLine.hidden = true;
}

function build(groups) {
  rows = new Map();
  const fragment = document.createDocumentFragment();

  for (const group of groups) {
    const section = document.createElement('section');
    section.className = 'results-group';

    const heading = document.createElement('h2');
    heading.textContent = group.label;
    section.append(heading);

    for (const score of group.scores) {
      const row = document.createElement('div');
      row.className = 'score';

      const criterion = document.createElement('p');
      criterion.className = 'score-criterion';
      criterion.textContent = score.criterion;

      const track = document.createElement('div');
      track.className = 'track';
      const bar = document.createElement('div');
      bar.className = 'bar';
      track.append(bar);

      const value = document.createElement('span');
      value.className = 'score-value';

      const comment = document.createElement('p');
      comment.className = 'score-comment';

      row.append(criterion, track, value, comment);
      section.append(row);
      rows.set(keyOf(group.label, score.criterion), { bar, value, comment });
    }

    fragment.append(section);
  }

  board.replaceChildren(fragment);

  // Lay the new bars out at their starting width of zero before any score is
  // applied, so the first update transitions up to it rather than snapping.
  void board.offsetHeight;
}

function draw(snapshot) {
  for (const group of snapshot.groups ?? []) {
    for (const score of group.scores) {
      const row = rows.get(keyOf(group.label, score.criterion));
      if (!row) continue;

      row.value.textContent = `${score.score}/${snapshot.maxScore}`;
      row.comment.textContent = score.comment;
      row.bar.style.width = `${(score.score / snapshot.maxScore) * 100}%`;
    }
  }
}

async function fetchResults() {
  const response = await fetch(`/api/app/${sessionId}/results`, { cache: 'no-store' });

  if (response.status === 404) return null;
  if (!response.ok) throw new Error(`The results endpoint returned ${response.status}`);

  return response.json();
}

function poll() {
  fetchResults()
    .then(show)
    .catch(error => {
      console.error(error);
      status.textContent = 'Reconnecting';
    });
}

function openLiveFeed() {
  if (!window.signalR) {
    console.warn('The live results feed is unavailable; polling instead.');
    return;
  }

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/resultsHub')
    .withAutomaticReconnect()
    .build();

  connection.on('ResultsUpdated', show);
  connection.on('ResultsCleared', () => showWaiting('The session was reset.'));
  connection.start().catch(error => console.error('Live results feed failed:', error));
}

poll();
setInterval(poll, PollInterval);
openLiveFeed();
