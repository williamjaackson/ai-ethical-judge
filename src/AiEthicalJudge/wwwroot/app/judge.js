const video = document.querySelector('#camera');
const toggle = document.querySelector('#toggle');
const status = document.querySelector('#status');
const transcript = document.querySelector('#transcript');
const sessionId = sessionStorage.getItem('judgeSessionId') || crypto.randomUUID();
const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;

let media;
let recognition;
let audioRecorder;
let transcriptSocket;
let imageTimer;
let running = false;

toggle.addEventListener('click', () => media ? stopJudge() : startJudge());

async function startJudge() {
  try {
    if (!SpeechRecognition) {
      throw new Error('Speech recognition is not supported by this browser');
    }

    toggle.disabled = true;
    status.textContent = 'Requesting camera and microphone';
    media = await navigator.mediaDevices.getUserMedia({
      audio: true,
      video: { facingMode: { ideal: 'environment' } }
    });
    video.srcObject = media;
    startAudioCapture();

    const socket = await openTranscriptStream();
    if (!media) {
      socket.close(1000);
      return;
    }
    transcriptSocket = socket;

    const speech = await createRecognition();
    if (!media) return;
    recognition = speech;
    running = true;
    speech.start();

    imageTimer = setInterval(sendImage, 5000);
    await sendImage();
    toggle.textContent = 'Stop Judge';
  } catch (error) {
    stopJudge();
    showError(error);
  } finally {
    toggle.disabled = false;
  }
}

function startAudioCapture() {
  audioRecorder = new MediaRecorder(new MediaStream(media.getAudioTracks()));
  audioRecorder.ondataavailable = event => {
    if (!event.data.size) return;

    fetch(`/api/app/${sessionId}/audio`, {
      method: 'POST',
      body: event.data,
      headers: { 'Content-Type': event.data.type || 'application/octet-stream' }
    }).then(response => {
      if (!response.ok) throw new Error('Unable to store audio chunk');
    }).catch(showError);
  };
  audioRecorder.start(2000);
}

async function createRecognition() {
  const speech = new SpeechRecognition();
  speech.continuous = true;
  speech.interimResults = true;
  speech.lang = navigator.language;
  await preferLocalRecognition(speech);

  speech.onresult = event => {
    let interim = '';
    for (let i = event.resultIndex; i < event.results.length; i++) {
      const text = event.results[i][0].transcript.trim();
      if (event.results[i].isFinal) {
        transcript.textContent = text;
        if (transcriptSocket?.readyState === WebSocket.OPEN) {
          transcriptSocket.send(text);
        }
      } else {
        interim += text;
      }
    }
    if (interim) transcript.textContent = interim;
  };
  speech.onstart = () => status.textContent = 'Listening';
  speech.onspeechstart = () => status.textContent = 'Speech detected';
  speech.onerror = event => {
    const error = new Error(event.error === 'network'
      ? 'The browser speech service is unreachable and on-device recognition is unavailable'
      : `Speech recognition: ${event.error}`);
    if (event.error === 'network') stopJudge();
    showError(error);
  };
  speech.onend = () => {
    if (running && recognition === speech) speech.start();
  };

  return speech;
}

function openTranscriptStream() {
  return new Promise((resolve, reject) => {
    const protocol = location.protocol === 'https:' ? 'wss' : 'ws';
    const socket = new WebSocket(
      `${protocol}://${location.host}/api/app/${sessionId}/transcript`);

    socket.onopen = () => resolve(socket);
    socket.onerror = () => reject(new Error('Unable to open transcript stream'));
    socket.onclose = () => {
      if (running) showError(new Error('Transcript stream closed'));
    };
  });
}

async function preferLocalRecognition(speech) {
  if (!('processLocally' in speech) ||
      typeof SpeechRecognition.available !== 'function') return;

  const options = { langs: [speech.lang], processLocally: true };
  const availability = await SpeechRecognition.available(options);

  if (availability === 'available') {
    speech.processLocally = true;
    return;
  }

  if (availability !== 'unavailable' &&
      typeof SpeechRecognition.install === 'function') {
    status.textContent = `Installing speech recognition for ${speech.lang}`;
    const installed = await Promise.race([
      SpeechRecognition.install(options),
      timeout(30000, `Installation of the ${speech.lang} speech pack timed out`)
    ]);

    if (!installed) throw new Error(`The ${speech.lang} speech pack could not be installed`);
    speech.processLocally = true;
  }
}

function timeout(milliseconds, message) {
  return new Promise((_, reject) => {
    setTimeout(() => reject(new Error(message)), milliseconds);
  });
}

async function sendImage() {
  if (!video.videoWidth) return;

  const canvas = document.createElement('canvas');
  canvas.width = video.videoWidth;
  canvas.height = video.videoHeight;
  canvas.getContext('2d').drawImage(video, 0, 0);
  const image = await new Promise(resolve => canvas.toBlob(resolve, 'image/jpeg', .75));

  await fetch(`/api/app/${sessionId}/image`, {
    method: 'POST',
    body: image,
    headers: { 'Content-Type': 'image/jpeg' }
  });
}

function stopJudge() {
  running = false;
  clearInterval(imageTimer);
  recognition?.stop();
  if (audioRecorder?.state === 'recording') audioRecorder.stop();
  transcriptSocket?.close(1000);
  media?.getTracks().forEach(track => track.stop());
  media = null;
  recognition = null;
  audioRecorder = null;
  transcriptSocket = null;
  video.srcObject = null;
  toggle.textContent = 'Start Judge';
  status.textContent = 'Stopped';
}

function showError(error) {
  console.error(error);
  status.textContent = error.message || 'Unable to start the judge';
}
