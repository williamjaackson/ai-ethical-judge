const form = document.querySelector('#configuration');

document.querySelectorAll('[data-add]').forEach(button => {
  button.addEventListener('click', () => addInput(button.dataset.add));
});

form.addEventListener('submit', event => {
  event.preventDefault();
  saveConfiguration();
});

async function saveConfiguration() {
  const configuration = Object.fromEntries(
    ['speech', 'looks'].map(name => [
      name,
      [...form.querySelectorAll(`[name="${name}"]`)]
        .map(input => input.value.trim())
        .filter(Boolean)
    ]));

  const sessionId = sessionStorage.getItem('judgeSessionId');
  if (!sessionId) {
    location.replace('./');
    return;
  }

  const nextButton = form.querySelector('[type="submit"]');
  nextButton.disabled = true;

  try {
    const response = await fetch(`/api/app/${sessionId}/criteria`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(configuration)
    });
    if (!response.ok) throw new Error('Unable to save judge criteria');

    location.assign('judge.html');
  } catch (error) {
    console.error(error);
    nextButton.disabled = false;
  }
}

function addInput(containerId) {
  const container = document.querySelector(`#${containerId}`);
  const input = document.createElement('input');
  const name = containerId.startsWith('speech') ? 'speech' : 'looks';

  input.name = name;
  input.type = 'text';
  input.ariaLabel = `${name} instruction ${container.children.length + 1}`;
  container.append(input);
  input.focus();
}
