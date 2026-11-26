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
const SOURCES = { phone: '휴대폰', tablet: '아이패드', web: '웹', winforms: 'PC', gesture: '제스처', auto: '자동', test: '테스트' };

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

new ResizeObserver(() => drawChart()).observe($('#chart'));
initCamera();
matchMedia('(prefers-color-scheme: dark)').addEventListener('change', drawChart);
init();
