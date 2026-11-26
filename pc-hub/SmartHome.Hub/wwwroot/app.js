'use strict';

// 휴대폰/아이패드 대시보드
// - 버튼: POST /api/devices/{device}  →  허브가 ESP32의 ACK를 받으면 응답
// - 실시간: WebSocket /ws  (state, sensor, log)

const $ = (s) => document.querySelector(s);
const NAMES = { fan: '팬', light: '전등', curtain: '커튼', door: '문' };
const UA = navigator.userAgent;
const SOURCE = /iPad/i.test(UA) || (/Macintosh/i.test(UA) && navigator.maxTouchPoints > 1) ? 'tablet'
  : /iPhone|Android/i.test(UA) ? 'phone' : 'web';
const MAX_SAMPLES = 180; // 1초 간격 3분
const samples = [];

// ───────── 화면 그리기 ─────────

function render(st) {
  const esp = st.esp;
  const warn = st.connected && esp && !esp.arduino;
  $('#link').className = 'link ' + (!st.connected ? 'off' : warn ? 'warn' : 'on');
  let text = st.connected ? st.link : 'ESP32 연결 없음';
  if (st.connected && esp) {
    if (esp.rssi != null) text += ` · 신호 ${esp.rssi}dBm`;
    if (esp.simulated) text += ' · ESP32 단독 모드';
    if (!esp.arduino) text += ' · Arduino 응답 없음';
  }
  $('#linkText').textContent = text;
  setDevice('fan', st.fan ? '켜짐' : '꺼짐', st.fan, st.fan ? 'on' : 'off');
  setDevice('light', st.light ? '켜짐' : '꺼짐', st.light, st.light ? 'on' : 'off');
  setDevice('curtain', `${st.curtain}%`, st.curtain > 0, null);
  setDevice('door', st.door ? '열림' : '닫힘', st.door, st.door ? 'open' : 'close');
  const range = $('#curtainRange');
  if (document.activeElement !== range) range.value = st.curtain;
  if (st.sensors) renderSensors(st.sensors);
}

function setDevice(device, text, on, activeAction) {
  const el = $(`#${device}State`);
  el.textContent = text;
  el.classList.toggle('on', !!on);
  document.querySelectorAll(`.card[data-device="${device}"] button`)
    .forEach((b) => b.classList.toggle('active', b.dataset.action === activeAction));
}

function renderSensors(s) {
  $('#temp').textContent = s.temp.toFixed(1);
  $('#hum').textContent = s.hum.toFixed(1);
  $('#lux').textContent = Math.round(s.lux);
  $('#ir').textContent = s.ir ? '감지됨' : '없음';
}

function stateText(device, s) {
  if (device === 'curtain') return `${s}%`;
  if (device === 'door') return s === 'OPEN' ? '열림' : '닫힘';
  return s === 'ON' ? '켜짐' : '꺼짐';
}

// ───────── 명령 보내기 ─────────

async function send(device, action, value) {
  const card = document.querySelector(`.card[data-device="${device}"]`);
  card.classList.add('busy');
  try {
    const res = await fetch(`/api/devices/${device}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-Source': SOURCE },
      body: JSON.stringify(value === undefined ? { action } : { action, value }),
    });
    const r = await res.json();
    if (r.ok) toast(`${NAMES[device]} ${stateText(device, r.state)} · ${r.ms}ms`);
    else toast(r.error || '실패했어요', true);
  } catch {
    toast('허브에 연결할 수 없어요', true);
  } finally {
    card.classList.remove('busy');
  }
}

document.querySelectorAll('.card').forEach((card) => {
  card.addEventListener('click', (e) => {
    const b = e.target.closest('button');
    if (!b) return;
    const device = card.dataset.device;
    if (b.dataset.action === 'set') send(device, 'set', Number($('#curtainRange').value));
    else send(device, b.dataset.action);
  });
});

let toastTimer;
function toast(msg, bad = false) {
  const t = $('#toast');
  t.textContent = msg;
  t.className = 'show' + (bad ? ' bad' : '');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => { t.className = bad ? 'bad' : ''; }, 2200);
}

// ───────── 기록 ─────────

const SHOWN_LOGS = ['CMD', 'FAIL', 'WARN', 'NET', 'EVT', 'ERROR'];
const SOURCES = { phone: '휴대폰', tablet: '아이패드', web: '웹', winforms: 'PC', gesture: '제스처', auto: '자동', test: '테스트', effect: '영역전개' };

// "phone: FAN → ON (42ms)" → "휴대폰 · 팬 켜짐"
function prettyLog(e) {
  const m = e.kind === 'CMD' && /^(\S+): (\w+) → (\S+) \((\d+)ms\)$/.exec(e.text);
  if (!m) return e.text;
  const device = m[2].toLowerCase();
  return `${SOURCES[m[1]] || m[1]} · ${NAMES[device] || device} ${stateText(device, m[3])}`;
}

function hhmmss(iso) {
  const d = new Date(iso);
  return [d.getHours(), d.getMinutes(), d.getSeconds()].map((n) => String(n).padStart(2, '0')).join(':');
}

function addLog(e) {
  if (!SHOWN_LOGS.includes(e.kind)) return;
  const li = document.createElement('li');
  const t = document.createElement('span');
  t.className = 't';
  t.textContent = hhmmss(e.time);
  const m = document.createElement('span');
  m.textContent = prettyLog(e);
  if (['FAIL', 'WARN', 'ERROR'].includes(e.kind)) m.className = 'bad';
  li.append(t, m);
  const ul = $('#log');
  ul.prepend(li);
  while (ul.children.length > 30) ul.lastChild.remove();
}

// ───────── 온습도 그래프 (라이브러리 없이 canvas) ─────────

function addSample(s) {
  samples.push({ t: new Date(s.time).getTime(), temp: s.temp, hum: s.hum });
  while (samples.length > MAX_SAMPLES) samples.shift();
  renderSensors(s);
  drawChart();
}

function drawChart() {
  const c = $('#chart');
  const dpr = window.devicePixelRatio || 1;
  const w = c.clientWidth, h = c.clientHeight;
  if (!w || !h) return;
  if (c.width !== Math.round(w * dpr) || c.height !== Math.round(h * dpr)) {
    c.width = Math.round(w * dpr);
    c.height = Math.round(h * dpr);
  }
  const g = c.getContext('2d');
  g.setTransform(dpr, 0, 0, dpr, 0, 0);
  g.clearRect(0, 0, w, h);
  const css = getComputedStyle(document.documentElement);
  const color = (name) => css.getPropertyValue(name).trim();
  g.font = '11px -apple-system, "Malgun Gothic", sans-serif';

  if (samples.length < 2) {
    g.fillStyle = color('--muted');
    g.fillText('센서값을 기다리는 중', 4, 16);
    return;
  }

  const pad = { l: 4, r: 64, t: 10, b: 20 };
  const plotW = w - pad.l - pad.r, plotH = h - pad.t - pad.b;
  const t0 = samples[0].t, t1 = samples[samples.length - 1].t;

  g.strokeStyle = color('--border');
  g.lineWidth = 1;
  for (let i = 0; i <= 3; i++) {
    const y = Math.round(pad.t + plotH * i / 3) + 0.5;
    g.beginPath(); g.moveTo(pad.l, y); g.lineTo(w - pad.r, y); g.stroke();
  }

  const series = [
    { key: 'temp', color: color('--temp'), unit: '°C', label: '온도' },
    { key: 'hum', color: color('--hum'), unit: '%', label: '습도' },
  ];
  const labels = [];
  for (const s of series) {
    const vals = samples.map((p) => p[s.key]);
    let min = Math.min(...vals), max = Math.max(...vals);
    if (max - min < 2) { const mid = (max + min) / 2; min = mid - 1; max = mid + 1; }
    const x = (p) => pad.l + plotW * ((p.t - t0) / Math.max(1, t1 - t0));
    const y = (v) => pad.t + plotH * (1 - (v - min) / (max - min));
    g.strokeStyle = s.color;
    g.lineWidth = 2;
    g.lineJoin = 'round';
    g.beginPath();
    samples.forEach((p, i) => (i ? g.lineTo(x(p), y(p[s.key])) : g.moveTo(x(p), y(p[s.key]))));
    g.stroke();
    const last = samples[samples.length - 1];
    labels.push({ y: y(last[s.key]), text: `${s.label} ${last[s.key].toFixed(1)}${s.unit}`, color: s.color });
  }
  // 오른쪽 끝 값 표시 (겹치면 살짝 벌리기)
  labels.sort((a, b) => a.y - b.y);
  if (labels.length === 2 && labels[1].y - labels[0].y < 14) {
    const mid = (labels[0].y + labels[1].y) / 2;
    labels[0].y = mid - 7; labels[1].y = mid + 7;
  }
  g.font = '600 11px -apple-system, "Malgun Gothic", sans-serif';
  for (const l of labels) { g.fillStyle = l.color; g.fillText(l.text, w - pad.r + 6, l.y + 4); }

  const span = (t1 - t0) / 1000;
  g.font = '11px -apple-system, "Malgun Gothic", sans-serif';
  g.fillStyle = color('--muted');
  g.textAlign = 'left';
  g.fillText(span >= 90 ? `${Math.round(span / 60)}분 전` : `${Math.round(span)}초 전`, pad.l, h - 4);
  g.textAlign = 'right';
  g.fillText('지금', w - pad.r, h - 4);
  g.textAlign = 'left';
}

// ───────── 실시간 연결 ─────────

function connect() {
  const ws = new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/ws`);
  ws.onopen = () => { $('#ws').textContent = '실시간'; $('#ws').className = 'ws on'; };
  ws.onmessage = (ev) => {
    const m = JSON.parse(ev.data);
    if (m.type === 'state') render(m.data);
    else if (m.type === 'sensor') addSample(m.data);
    else if (m.type === 'log') addLog(m.data);
    else if (m.type === 'effect') playEffect(m.data.name);
  };
  ws.onclose = () => {
    $('#ws').textContent = '실시간 끊김 · 다시 연결 중';
    $('#ws').className = 'ws';
    setTimeout(connect, 2000);
  };
}

async function init() {
  try {
    const [history, logs] = await Promise.all([
      fetch('/api/sensors?minutes=3').then((r) => r.json()),
      fetch('/api/logs').then((r) => r.json()),
    ]);
    history.forEach((s) => samples.push({ t: new Date(s.time).getTime(), temp: s.temp, hum: s.hum }));
    while (samples.length > MAX_SAMPLES) samples.shift();
    logs.forEach(addLog);
  } catch { /* 허브가 아직 안 떠 있으면 WebSocket 재연결이 처리 */ }
  drawChart();
  connect();
}

// ───────── 제스처 카메라 (라즈베리파이 미리보기를 허브가 전달) ─────────

async function initCamera() {
  try {
    const cam = await fetch('/api/camera').then((r) => r.json());
    if (!cam.enabled) return; // 설정에 CameraUrl이 없으면 칸을 숨겨요
  } catch {
    return;
  }
  $('#cameraPanel').hidden = false;
  const img = $('#cameraImg'), msg = $('#cameraMsg'), state = $('#cameraState');
  const start = () => { img.src = `/camera/stream?t=${Date.now()}`; };
  img.onload = () => { msg.hidden = true; state.textContent = '실시간'; };
  img.onerror = () => {
    msg.hidden = false;
    msg.textContent = '카메라 연결 안 됨 · 라즈베리파이 제스처 앱(--preview-port 8080)이 켜져 있는지 확인하세요';
    state.textContent = '다시 연결 중';
    setTimeout(start, 3000);
  };
  start();
}

// ───────── 영역전개 · 무량공처 (코드로 그리는 연출, 이미지/음원 파일 없음) ─────────

const FX_MS = 6500;
let fxRun = 0;
let audioCtx = null;

// 브라우저는 화면을 한 번 눌러야 소리를 낼 수 있어요 (아이패드는 버튼만 눌러도 돼요)
addEventListener('pointerdown', () => {
  try { audioCtx = audioCtx || new (window.AudioContext || window.webkitAudioContext)(); audioCtx.resume(); } catch { /* 소리 없이 */ }
}, { once: false, passive: true });

function fxSound() {
  if (!audioCtx || audioCtx.state !== 'running') return;
  const t0 = audioCtx.currentTime, out = audioCtx.createGain();
  out.gain.setValueAtTime(0.0001, t0);
  out.gain.exponentialRampToValueAtTime(0.35, t0 + 0.8);
  out.gain.exponentialRampToValueAtTime(0.0001, t0 + FX_MS / 1000);
  out.connect(audioCtx.destination);
  // 낮게 울리는 소리 + 위로 올라가는 맑은 소리 (직접 합성)
  [[55, 'sine', 1], [82.5, 'sine', 0.5], [440, 'triangle', 0.08]].forEach(([f, type, g], i) => {
    const o = audioCtx.createOscillator(), og = audioCtx.createGain();
    o.type = type; o.frequency.setValueAtTime(f, t0);
    if (i === 2) o.frequency.exponentialRampToValueAtTime(1760, t0 + 3);
    og.gain.value = g; o.connect(og).connect(out);
    o.start(t0); o.stop(t0 + FX_MS / 1000);
  });
}

function playEffect(name) {
  if (name !== 'infinite_void') return;
  const run = ++fxRun;
  const box = $('#fx'), cv = $('#fxCanvas'), g = cv.getContext('2d');
  box.hidden = false;
  box.className = 'fx';
  requestAnimationFrame(() => { box.className = 'fx on'; });
  setTimeout(() => { if (run === fxRun) box.classList.add('sub'); }, 600);
  setTimeout(() => { if (run === fxRun) box.classList.add('main'); }, 1400);
  fxSound();

  const dpr = Math.min(2, window.devicePixelRatio || 1);
  const stars = Array.from({ length: 420 }, () => ({ a: Math.random() * Math.PI * 2, z: Math.random(), s: 0.25 + Math.random() * 0.75, h: Math.random() }));
  const streams = Array.from({ length: 60 }, () => ({ x: Math.random(), y: Math.random(), v: 0.15 + Math.random() * 0.5, l: 0.05 + Math.random() * 0.2 }));
  const t0 = performance.now();

  function frame(now) {
    if (run !== fxRun) return;
    const t = (now - t0) / 1000;
    const w = cv.clientWidth, h = cv.clientHeight;
    if (cv.width !== Math.round(w * dpr)) { cv.width = Math.round(w * dpr); cv.height = Math.round(h * dpr); }
    g.setTransform(dpr, 0, 0, dpr, 0, 0);
    const cx = w / 2, cy = h * 0.42, m = Math.min(w, h);

    // 깊은 우주 배경
    const bg = g.createRadialGradient(cx, cy, 0, cx, cy, Math.hypot(w, h) * 0.6);
    bg.addColorStop(0, '#0b1030'); bg.addColorStop(0.5, '#05061a'); bg.addColorStop(1, '#000');
    g.fillStyle = bg; g.fillRect(0, 0, w, h);

    // 끝없이 흘러가는 정보 줄기
    g.lineWidth = 1;
    for (const s of streams) {
      const y = ((s.y + t * s.v) % 1.2 - 0.1) * h;
      g.strokeStyle = `rgba(120,170,255,${0.18 + 0.2 * Math.sin(t * 3 + s.x * 9) ** 2})`;
      g.beginPath(); g.moveTo(s.x * w, y); g.lineTo(s.x * w, y - s.l * h); g.stroke();
    }

    // 별빛 줄기 (중심에서 바깥으로)
    const ring = m * (0.08 + 0.1 * Math.min(1, t / 1.2)) * (1 + 0.02 * Math.sin(t * 5));
    const far = Math.hypot(w, h) * 0.7;
    for (const st of stars) {
      const p = (st.z + t * st.s * 0.28) % 1;
      const r1 = ring + p * p * far, r0 = Math.max(ring, r1 - (6 + 70 * p * st.s));
      const ang = st.a + t * 0.08;
      const c = Math.cos(ang), s = Math.sin(ang);
      g.strokeStyle = `hsla(${200 + st.h * 60}, 90%, ${70 + 25 * p}%, ${Math.min(1, p * 2)})`;
      g.lineWidth = 0.6 + 1.6 * p;
      g.beginPath(); g.moveTo(cx + c * r0, cy + s * r0); g.lineTo(cx + c * r1, cy + s * r1); g.stroke();
    }

    // 사건의 지평선: 안은 완전히 검고 테두리는 하얗게 빛나요
    g.save();
    g.shadowColor = 'rgba(170,220,255,0.95)';
    g.shadowBlur = 40;
    g.fillStyle = '#000';
    g.beginPath(); g.arc(cx, cy, ring, 0, Math.PI * 2); g.fill();
    g.lineWidth = 3; g.strokeStyle = '#fff';
    g.beginPath(); g.arc(cx, cy, ring, 0, Math.PI * 2); g.stroke();
    g.lineWidth = 2; g.strokeStyle = 'rgba(160,210,255,0.9)';
    for (let k = 0; k < 3; k++) {
      const a0 = t * (2 + k * 0.6) + k * 2.1;
      g.beginPath(); g.arc(cx, cy, ring + 8 + k * 6, a0, a0 + 1.1); g.stroke();
    }
    g.restore();

    // 끝날 때 서서히 사라지기
    if (t * 1000 > FX_MS - 900) box.classList.remove('on');
    if (t * 1000 < FX_MS) requestAnimationFrame(frame);
    else { box.hidden = true; box.className = 'fx'; }
  }
  requestAnimationFrame(frame);
}

$('#fx').addEventListener('click', () => { fxRun++; $('#fx').hidden = true; $('#fx').className = 'fx'; });
$('#fxTest').addEventListener('click', async () => {
  try { await fetch('/api/effects/infinite_void', { method: 'POST', headers: { 'X-Source': SOURCE } }); }
  catch { toast('허브에 연결할 수 없어요', true); }
});

new ResizeObserver(() => drawChart()).observe($('#chart'));
initCamera();
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', drawChart);
init();
