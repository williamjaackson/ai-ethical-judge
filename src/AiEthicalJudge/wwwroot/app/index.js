const button = document.querySelector('#new-judge');

button.addEventListener('click', async () => {
  const sessionId = crypto.randomUUID();
  button.disabled = true;

  try {
    const response = await fetch(`/api/app/${sessionId}/reset`, { method: 'POST' });
    if (!response.ok) throw new Error('Unable to start a new judge');

    sessionStorage.setItem('judgeSessionId', sessionId);
    location.assign('configure.html');
  } catch (error) {
    console.error(error);
    button.disabled = false;
  }
});
