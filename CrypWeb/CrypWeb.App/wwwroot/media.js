// Devices of the browser for plugins (CrypWeb.App/Engine/MediaBridge.cs): the camera for the Webcam plugin, the
// microphone and the speakers for AudioInput and AudioOutput.
// While a plugin uses the camera, its picture is sent to .NET a few times per second as a JPEG data URL; the
// plugin takes the newest one whenever it captures. Audio travels as 16-bit mono PCM (base64).
window.ct2media = (() => {
  const FRAMES_PER_SECOND = 10;
  let stream = null, video = null, canvas = null, timer = null;

  async function start() {
    if (stream) return;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
    } catch (error) {
      console.warn('Camera not available:', error);
      return;
    }
    video = document.createElement('video');
    video.muted = true;
    video.playsInline = true;
    video.srcObject = stream;
    await video.play();
    canvas = document.createElement('canvas');
    timer = setInterval(sendFrame, 1000 / FRAMES_PER_SECOND);
  }

  function stop() {
    clearInterval(timer);
    timer = null;
    if (stream) stream.getTracks().forEach(track => track.stop());
    stream = null;
    video = null;
  }

  function sendFrame() {
    if (!video || !video.videoWidth) return;
    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    canvas.getContext('2d').drawImage(video, 0, 0);
    DotNet.invokeMethodAsync('CrypWeb.App', 'CameraFrame', canvas.toDataURL('image/jpeg', 0.85));
  }

  // ---- audio: 16-bit mono PCM at the plugin's sample rate (8 kHz) ---------------------------------------------

  let microphone = null;     // { stream, context, source, processor }
  let output = null;         // { context, nextTime }

  function toBase64(bytes) {
    let text = '';
    for (let i = 0; i < bytes.length; i += 0x8000) text += String.fromCharCode.apply(null, bytes.subarray(i, i + 0x8000));
    return btoa(text);
  }

  /** Records the microphone; blocks of samples go to .NET as they are recorded (about every 0.25 s). */
  async function startMicrophone(sampleRate) {
    if (microphone) return;
    let stream;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true, video: false });
    } catch (error) {
      console.warn('Microphone not available:', error);
      return;
    }
    // the audio context resamples to the plugin's rate
    const context = new AudioContext({ sampleRate });
    const source = context.createMediaStreamSource(stream);
    const processor = context.createScriptProcessor(2048, 1, 1);
    processor.onaudioprocess = event => {
      const input = event.inputBuffer.getChannelData(0);
      const pcm = new Int16Array(input.length);
      for (let i = 0; i < input.length; i++) pcm[i] = Math.max(-32768, Math.min(32767, Math.round(input[i] * 32767)));
      DotNet.invokeMethodAsync('CrypWeb.App', 'MicrophoneSamples', toBase64(new Uint8Array(pcm.buffer)));
    };
    source.connect(processor);
    processor.connect(context.destination);      // a script processor only runs when connected
    microphone = { stream, context, source, processor };
  }

  function stopMicrophone() {
    if (!microphone) return;
    microphone.processor.disconnect();
    microphone.source.disconnect();
    microphone.stream.getTracks().forEach(track => track.stop());
    microphone.context.close();
    microphone = null;
  }

  /** Plays samples after the ones already queued (a short gap is closed by starting slightly ahead). */
  function play(base64, sampleRate) {
    if (!output) {
      const context = new AudioContext();
      output = { context, nextTime: 0 };
    }
    const bytes = Uint8Array.from(atob(base64), c => c.charCodeAt(0));
    const pcm = new Int16Array(bytes.buffer, 0, bytes.length >> 1);
    if (!pcm.length) return;
    const buffer = output.context.createBuffer(1, pcm.length, sampleRate);
    const channel = buffer.getChannelData(0);
    for (let i = 0; i < pcm.length; i++) channel[i] = pcm[i] / 32768;
    const node = output.context.createBufferSource();
    node.buffer = buffer;
    node.connect(output.context.destination);
    const now = output.context.currentTime;
    const start = Math.max(output.nextTime, now + 0.05);
    node.start(start);
    output.nextTime = start + buffer.duration;
  }

  return {
    /** Starts (true) or stops (false) the camera. */
    camera(on) { if (on) start(); else stop(); },
    /** Starts (true) or stops (false) recording from the microphone. */
    microphone(on, sampleRate) { if (on) startMicrophone(sampleRate); else stopMicrophone(); },
    play,
  };
})();
