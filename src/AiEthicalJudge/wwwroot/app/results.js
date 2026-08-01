const SVG_NS = 'http://www.w3.org/2000/svg';

/* Mark specs, shared by every chart here so they read as one system. */
const TREND_HEIGHT = 240;
const TREND_PADDING = { top: 18, right: 56, bottom: 28, left: 38 };
const END_DOT_RADIUS = 4;
const SPARKLINE = { width: 76, height: 22, points: 12 };

/* The highest mark a single criterion can be given, mirroring Judgement.MaxScore. */
const MAX_CRITERION_SCORE = 5;

const elements = {
  connection: document.querySelector('#connection'),
  connectionLabel: document.querySelector('#connection-label'),
  heroFigure: document.querySelector('.hero-figure'),
  total: document.querySelector('#total'),
  maxTotal: document.querySelector('#max-total'),
  meter: document.querySelector('#meter'),
  meterFill: document.querySelector('#meter-fill'),
  summary: document.querySelector('#summary'),
  producedAt: document.querySelector('#produced-at'),
  trend: document.querySelector('#trend'),
  trendNote: document.querySelector('#trend-note'),
  speechList: document.querySelector('#speech-list'),
  looksList: document.querySelector('#looks-list'),
  roundsHead: document.querySelector('#rounds-head'),
  roundsBody: document.querySelector('#rounds-body'),
  tooltip: document.querySelector('#tooltip')
};

const sessionId = resolveSessionId();

/** The most recent snapshot from the stream, kept so a resize can redraw. */
let snapshot = null;

/** Where the trend chart last put each point, for hit-testing the crosshair. */
let trendMarks = [];

/** The width the trend chart was last drawn at, so a resize only redraws on a change. */
let trendWidth = 0;

connect();
watchForResize();

/**
 * The results page is a second screen: it is opened alongside a judge that is
 * already running, so the session may arrive in the URL rather than in this
 * tab's storage.
 */
function resolveSessionId() {
  const fromQuery = new URLSearchParams(location.search).get('session');
  if (fromQuery) sessionStorage.setItem('judgeSessionId', fromQuery);

  return fromQuery
    || sessionStorage.getItem('judgeSessionId')
    || crypto.randomUUID();
}

function connect() {
  const source = new EventSource(`/api/app/${sessionId}/results/stream`);

  source.onopen = () => setConnection('live', 'Live');
  source.onmessage = event => {
    setConnection('live', 'Live');
    try {
      render(JSON.parse(event.data));
    } catch (error) {
      console.error(error);
    }
  };

  // EventSource reconnects on its own; this only reports that it is doing so.
  source.onerror = () => setConnection('offline', 'Reconnecting');
}

function setConnection(state, label) {
  elements.connection.dataset.state = state;
  elements.connectionLabel.textContent = label;
}

function render(data) {
  snapshot = data;
  renderVerdict(data);
  renderTrend(data);
  renderCriteria(data);
  renderTable(data);
}

/* ---------------------------------------------------------------- verdict */

function renderVerdict({ latest, producedAt, history }) {
  const total = latest?.total;
  const maxTotal = latest?.maxTotal;
  const scored = Number.isFinite(total) && Number.isFinite(maxTotal) && maxTotal > 0;

  // Greyed out until there is a score, so the placeholder does not read as one.
  elements.heroFigure.classList.toggle('is-empty', !scored);
  elements.total.textContent = scored ? total : '–';
  elements.maxTotal.textContent = scored ? maxTotal : '–';
  elements.meterFill.style.width = scored ? `${(total / maxTotal) * 100}%` : '0%';
  elements.meter.ariaLabel = scored
    ? `Running score ${total} out of ${maxTotal}.`
    : 'No score yet.';

  elements.summary.textContent = latest?.summary
    || 'Waiting for the first judgement.';

  const previous = history?.at(-2)?.total;
  const delta = scored && Number.isFinite(previous) ? total - previous : null;
  elements.producedAt.textContent = producedAt
    ? `Updated ${formatTime(producedAt)}${delta === null ? '' : ` · ${formatDelta(delta)} on the round before`}`
    : '';
}

/* ------------------------------------------------------------------ trend */

function renderTrend({ history }) {
  const width = elements.trend.clientWidth;
  if (!width) return;

  trendWidth = width;
  trendMarks = [];
  elements.trend.replaceChildren();

  if (!history?.length) {
    elements.trendNote.textContent = '';
    elements.trend.append(emptyState('The chart starts as soon as the judge does.'));
    return;
  }

  elements.trendNote.textContent = history.length === 1
    ? '1 round'
    : `${history.length} rounds`;

  const maxTotal = Math.max(...history.map(point => point.maxTotal), 1);
  const times = history.map(point => Date.parse(point.producedAt));
  const first = Math.min(...times);
  const last = Math.max(...times);
  const plotWidth = width - TREND_PADDING.left - TREND_PADDING.right;
  const plotHeight = TREND_HEIGHT - TREND_PADDING.top - TREND_PADDING.bottom;

  const toX = time => first === last
    ? TREND_PADDING.left + plotWidth / 2
    : TREND_PADDING.left + ((time - first) / (last - first)) * plotWidth;
  const toY = value =>
    TREND_PADDING.top + plotHeight - (value / maxTotal) * plotHeight;

  const chart = createSvg('svg', {
    class: 'trend-chart',
    width,
    height: TREND_HEIGHT,
    viewBox: `0 0 ${width} ${TREND_HEIGHT}`,
    role: 'img',
    'aria-label': trendDescription(history, maxTotal)
  });

  // Gridlines first, so every mark sits on top of them.
  for (let value = 0; value <= maxTotal; value += tickStep(maxTotal)) {
    chart.append(createSvg('line', {
      class: 'chart-gridline',
      x1: TREND_PADDING.left,
      x2: width - TREND_PADDING.right,
      y1: toY(value),
      y2: toY(value)
    }));
    chart.append(text(TREND_PADDING.left - 10, toY(value) + 4, value, {
      class: 'chart-tick',
      'text-anchor': 'end'
    }));
  }

  // The outermost labels are anchored inwards so they cannot spill past the plot.
  const ticks = timeTicks(times);
  ticks.forEach((time, index) => {
    chart.append(text(toX(time), TREND_HEIGHT - 8, formatTime(time), {
      class: 'chart-tick',
      'text-anchor': ticks.length === 1 ? 'middle'
        : index === 0 ? 'start'
        : index === ticks.length - 1 ? 'end'
        : 'middle'
    }));
  });

  const marks = history.map((point, index) => ({
    point,
    x: toX(times[index]),
    y: toY(point.total)
  }));
  trendMarks = marks;

  if (marks.length > 1) {
    const line = marks.map(mark => `${mark.x},${mark.y}`).join(' L');
    const baseline = toY(0);
    chart.append(createSvg('path', {
      class: 'chart-area',
      d: `M${line} L${marks.at(-1).x},${baseline} L${marks[0].x},${baseline} Z`
    }));
    chart.append(createSvg('path', { class: 'chart-line', d: `M${line}` }));
  }

  const end = marks.at(-1);
  chart.append(createSvg('circle', {
    class: 'chart-end-dot',
    cx: end.x,
    cy: end.y,
    r: END_DOT_RADIUS
  }));
  // The endpoint is the value the page is about, so it is the one direct label.
  chart.append(text(end.x + 12, end.y + 4, end.point.total, { class: 'chart-end-label' }));

  const crosshair = createSvg('line', {
    class: 'chart-crosshair',
    y1: TREND_PADDING.top,
    y2: TREND_PADDING.top + plotHeight
  });
  chart.append(crosshair);

  attachCrosshair(chart, crosshair);
  elements.trend.append(chart);
}

/**
 * Readers aim at a moment in time, never at a 2px line, so the pointer only has
 * to be nearest to a round rather than on top of it.
 */
function attachCrosshair(chart, crosshair) {
  const move = event => {
    const bounds = chart.getBoundingClientRect();
    const x = event.clientX - bounds.left;
    const nearest = trendMarks.reduce((best, mark) =>
      Math.abs(mark.x - x) < Math.abs(best.x - x) ? mark : best);

    crosshair.setAttribute('x1', nearest.x);
    crosshair.setAttribute('x2', nearest.x);
    crosshair.classList.add('is-tracking');

    showTooltip(event.clientX, bounds.top + nearest.y, formatTime(nearest.point.producedAt), [
      { value: `${nearest.point.total} / ${nearest.point.maxTotal}`, label: 'Running score' }
    ]);
  };

  chart.addEventListener('pointermove', move);
  chart.addEventListener('pointerleave', () => {
    crosshair.classList.remove('is-tracking');
    hideTooltip();
  });
}

function trendDescription(history, maxTotal) {
  const totals = history.map(point => point.total);
  return `Running score over ${history.length} rounds, out of ${maxTotal}. `
    + `Starts at ${totals[0]}, ends at ${totals.at(-1)}, `
    + `ranging from ${Math.min(...totals)} to ${Math.max(...totals)}.`;
}

/* --------------------------------------------------------------- criteria */

function renderCriteria(data) {
  renderCriterionList(elements.speechList, 'speech', data);
  renderCriterionList(elements.looksList, 'looks', data);
}

function renderCriterionList(list, group, data) {
  const names = data[group] ?? [];
  const scores = data.latest?.[group] ?? [];
  list.replaceChildren();

  if (!names.length) {
    const empty = document.createElement('li');
    empty.className = 'criterion-empty';
    empty.textContent = `No ${group} criteria were configured for this judge.`;
    list.append(empty);
    return;
  }

  names.forEach((name, index) => {
    const trail = (data.history ?? [])
      .map(point => point[group]?.[index])
      .filter(Number.isFinite);

    list.append(criterionRow(name, scores[index], trail));
  });
}

/**
 * One criterion as a stat tile: the criterion, the mark it currently holds, how
 * that mark has moved, and the judge's reasoning for it.
 */
function criterionRow(name, scored, trail) {
  const row = document.createElement('li');
  row.className = 'criterion';
  row.tabIndex = 0;

  const heading = document.createElement('div');
  heading.className = 'criterion-heading';

  const label = document.createElement('p');
  label.className = 'criterion-name';
  label.textContent = name;

  const value = document.createElement('p');
  value.className = 'criterion-score';
  const score = scored?.score;
  const has = Number.isFinite(score);

  const strong = document.createElement('strong');
  strong.textContent = has ? score : '–';
  value.append(strong, ` / ${MAX_CRITERION_SCORE}`);

  const previous = trail.at(-2);
  if (has && Number.isFinite(previous) && previous !== score) {
    const delta = document.createElement('span');
    delta.className = 'criterion-delta';
    delta.textContent = formatDelta(score - previous);
    value.append(delta);
  }

  heading.append(label, value);

  const plot = document.createElement('div');
  plot.className = 'criterion-plot';

  const track = document.createElement('div');
  track.className = 'criterion-track';
  const steps = document.createElement('div');
  steps.className = 'criterion-steps';
  steps.setAttribute('aria-hidden', 'true');
  const bar = document.createElement('div');
  bar.className = 'criterion-bar';
  bar.style.width = has ? `${(score / MAX_CRITERION_SCORE) * 100}%` : '0';
  track.append(steps, bar);

  plot.append(track, sparkline(trail));

  const comment = document.createElement('p');
  comment.className = 'criterion-comment';
  comment.textContent = scored?.comment || 'Not judged yet.';

  row.append(heading, plot, comment);

  const describe = event => {
    const bounds = row.getBoundingClientRect();
    showTooltip(
      event.clientX ?? bounds.left + bounds.width / 2,
      event.clientY ?? bounds.top,
      name,
      [
        { value: has ? `${score} / ${MAX_CRITERION_SCORE}` : 'Not judged yet', label: 'Current mark' },
        ...(trail.length > 1
          ? [{ value: `${Math.min(...trail)}–${Math.max(...trail)}`, label: `Range over ${trail.length} rounds` }]
          : [])
      ]);
  };

  row.addEventListener('pointermove', describe);
  row.addEventListener('focus', describe);
  row.addEventListener('pointerleave', hideTooltip);
  row.addEventListener('blur', hideTooltip);

  return row;
}

/** The last few rounds for one criterion, as a 12-point trend. */
function sparkline(trail) {
  const recent = trail.slice(-SPARKLINE.points);
  const chart = createSvg('svg', {
    class: 'sparkline',
    width: SPARKLINE.width,
    height: SPARKLINE.height,
    viewBox: `0 0 ${SPARKLINE.width} ${SPARKLINE.height}`,
    'aria-hidden': 'true'
  });

  // One round is not a trend, and a lone dot reads as a stray mark. The box is
  // still reserved, so nothing shifts when the second round lands.
  if (recent.length < 2) return chart;

  const inset = 3;
  const span = SPARKLINE.width - inset * 2;
  const toX = index => inset + (index / (recent.length - 1)) * span;
  // Pinned to the full 1-5 scale, so a flat line means a steady mark rather
  // than an unchanging one drawn across the whole box.
  const toY = score => SPARKLINE.height - inset
    - ((score - 1) / (MAX_CRITERION_SCORE - 1)) * (SPARKLINE.height - inset * 2);

  chart.append(createSvg('path', {
    class: 'sparkline-line',
    d: `M${recent.map((score, index) => `${toX(index)},${toY(score)}`).join(' L')}`
  }));

  chart.append(createSvg('circle', {
    class: 'sparkline-dot',
    cx: toX(recent.length - 1),
    cy: toY(recent.at(-1)),
    r: 2.5
  }));

  return chart;
}

/* ------------------------------------------------------------ table twin */

function renderTable({ speech, looks, history }) {
  const columns = [
    ...(speech ?? []).map(name => ({ name, group: 'speech' })),
    ...(looks ?? []).map(name => ({ name, group: 'looks' }))
  ];

  elements.roundsHead.replaceChildren();
  elements.roundsBody.replaceChildren();

  const head = document.createElement('tr');
  for (const heading of ['Time', 'Total', ...columns.map(column => column.name)]) {
    const cell = document.createElement('th');
    cell.scope = 'col';
    cell.textContent = heading;
    head.append(cell);
  }
  elements.roundsHead.append(head);

  const rounds = [...(history ?? [])].reverse();

  if (!rounds.length) {
    const row = document.createElement('tr');
    const cell = document.createElement('td');
    cell.colSpan = columns.length + 2;
    cell.textContent = 'No rounds judged yet.';
    row.append(cell);
    elements.roundsBody.append(row);
    return;
  }

  // The indexes reset per group, so speech and looks each read from their own list.
  const counters = { speech: 0, looks: 0 };
  const positions = columns.map(column => counters[column.group]++);

  for (const round of rounds) {
    const row = document.createElement('tr');
    const time = document.createElement('th');
    time.scope = 'row';
    time.textContent = formatTime(round.producedAt);
    row.append(time);

    for (const value of [`${round.total} / ${round.maxTotal}`,
      ...columns.map((column, index) => round[column.group]?.[positions[index]] ?? '–')]) {
      const cell = document.createElement('td');
      cell.textContent = value;
      row.append(cell);
    }

    elements.roundsBody.append(row);
  }
}

/* ---------------------------------------------------------------- tooltip */

function showTooltip(clientX, clientY, title, rows) {
  const tooltip = elements.tooltip;
  tooltip.replaceChildren();

  const heading = document.createElement('p');
  heading.className = 'tooltip-title';
  heading.textContent = title;
  tooltip.append(heading);

  for (const row of rows) {
    const line = document.createElement('p');
    line.className = 'tooltip-row';

    const key = document.createElement('span');
    key.className = 'tooltip-key';
    key.setAttribute('aria-hidden', 'true');

    // The value leads: the reader already knows which criterion they are on.
    const value = document.createElement('strong');
    value.textContent = row.value;

    const label = document.createElement('span');
    label.className = 'tooltip-label';
    label.textContent = row.label;

    line.append(key, value, label);
    tooltip.append(line);
  }

  tooltip.hidden = false;

  const bounds = tooltip.getBoundingClientRect();
  const left = Math.min(
    Math.max(8, clientX + 14),
    Math.max(8, innerWidth - bounds.width - 8));
  const top = Math.min(
    Math.max(8, clientY - bounds.height - 12),
    Math.max(8, innerHeight - bounds.height - 8));

  tooltip.style.transform = `translate(${left}px, ${top}px)`;
}

function hideTooltip() {
  elements.tooltip.hidden = true;
}

/* ----------------------------------------------------------------- shared */

function watchForResize() {
  new ResizeObserver(() => {
    if (snapshot && elements.trend.clientWidth !== trendWidth) renderTrend(snapshot);
  }).observe(elements.trend);
}

function createSvg(name, attributes = {}) {
  const node = document.createElementNS(SVG_NS, name);
  for (const [key, value] of Object.entries(attributes)) {
    node.setAttribute(key, value);
  }

  return node;
}

function text(x, y, content, attributes = {}) {
  const node = createSvg('text', { x, y, ...attributes });
  node.textContent = content;

  return node;
}

function emptyState(message) {
  const paragraph = document.createElement('p');
  paragraph.className = 'chart-empty';
  paragraph.textContent = message;

  return paragraph;
}

/** Keeps y-axis ticks on round numbers, whatever the criteria add up to. */
function tickStep(maxTotal) {
  return [5, 10, 20, 25, 50, 100].find(step => maxTotal / step <= 6) ?? 200;
}

/** Up to four evenly spaced moments, so the axis never crowds itself. */
function timeTicks(times) {
  const first = Math.min(...times);
  const last = Math.max(...times);
  if (first === last) return [first];

  const count = Math.min(4, times.length);
  return Array.from({ length: count }, (_, index) =>
    first + ((last - first) * index) / (count - 1));
}

function formatTime(value) {
  return new Date(value).toLocaleTimeString([], {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit'
  });
}

/** Direction rides an arrow rather than a colour, so it never reads as a status. */
function formatDelta(delta) {
  return `${delta > 0 ? '↑' : '↓'}${Math.abs(delta)}`;
}
