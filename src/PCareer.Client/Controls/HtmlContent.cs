namespace PCareer.Client;

internal static class HtmlContent
{
    public const string Template = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<title>Virtual Pilot Network</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
:root{
  --bg:#0b0e12;--surface:#11161c;--surface2:#141920;
  --text:#f1f5f9;--text2:#cbd5e1;--text3:#94a3b8;--text4:#64748b;--text5:#475569;
  --indigo:#818cf8;--indigo2:#a5b4fc;--indigo3:#c7d2fe;
  --emerald:#34d399;--amber:#fbbf24;--red:#f87171;
  --border:rgba(255,255,255,0.065);--border2:rgba(255,255,255,0.08);
  --radius:14px;--radius-xs:8px;
}
html,body{height:100%;background:var(--bg);color:var(--text);
  font-family:'Inter','Segoe UI',system-ui,sans-serif;font-size:14px;line-height:1.5}
body{padding:24px 28px 20px;display:flex;flex-direction:column;gap:16px;overflow-y:auto;
  scrollbar-width:thin;scrollbar-color:#303844 transparent}

/* ── header ── */
.header{display:flex;align-items:center;gap:14px;padding:4px 0 6px}
.logo{width:48px;height:48px;position:relative;flex-shrink:0;display:grid;place-items:center}
.logo img{width:48px;height:48px;object-fit:contain;display:block}
.header-text h1{font-size:20px;font-weight:600;color:var(--text);letter-spacing:-0.01em;line-height:1.2}
.header-text p{font-size:10px;font-weight:600;text-transform:uppercase;letter-spacing:0.18em;
  color:var(--text5);margin-top:2px}

/* ── panels ── */
.panel{border:1px solid var(--border);border-radius:var(--radius);background:var(--surface);
  padding:20px 22px;transition:border-color .15s ease}
.panel:hover{border-color:rgba(255,255,255,0.09)}
.row{display:grid;grid-template-columns:1fr 1fr;gap:14px}

/* ── featured card (contract) ── */
.featured{position:relative;overflow:hidden;
  border:1px solid rgba(99,102,241,0.18);border-radius:var(--radius);
  background:
    radial-gradient(circle at 85% 0%,rgba(99,102,241,0.18),transparent 40%),
    linear-gradient(135deg,#141b29 0%,#10151d 55%,#111722 100%);
  padding:22px 24px;transition:border-color .15s ease}
.featured:hover{border-color:rgba(99,102,241,0.28)}
.featured::before{content:'';position:absolute;inset:0;pointer-events:none;
  background-image:
    linear-gradient(rgba(255,255,255,0.018) 1px,transparent 1px),
    linear-gradient(90deg,rgba(255,255,255,0.018) 1px,transparent 1px);
  background-size:32px 32px;
  mask-image:linear-gradient(to right,transparent,black 40%);
  opacity:0.7}
.featured-inner{position:relative;z-index:1}

/* ── field blocks ── */
.field-label{font-size:10px;font-weight:700;text-transform:uppercase;letter-spacing:0.13em;
  color:var(--text4);display:flex;align-items:center;gap:7px}
.field-value{font-size:14px;color:var(--text2);margin-top:4px;word-break:break-word;line-height:1.5}
.field-value.large{font-size:18px;font-weight:600;color:var(--text)}

/* ── dot ── */
.dot{width:7px;height:7px;border-radius:50%;display:inline-block;flex-shrink:0}
.dot.idle{background:var(--text5)}.dot.info{background:#60a5fa}
.dot.ok{background:var(--emerald)}.dot.warn{background:var(--amber)}
.dot.error{background:var(--red)}

/* ── buttons ── */
.btn-row{display:grid;grid-template-columns:1fr 1fr;gap:10px}
.btn{border:none;border-radius:var(--radius-xs);padding:12px 0;font-size:13px;
  font-weight:600;cursor:pointer;transition:all .15s ease;font-family:inherit;color:var(--text)}
.btn:active{transform:scale(0.98)}
.btn.primary{background:var(--indigo);color:#fff;
  box-shadow:0 4px 14px rgba(99,102,241,0.2)}
.btn.primary:hover{background:#6366f1;
  box-shadow:0 6px 20px rgba(99,102,241,0.28)}
.btn.primary:disabled{background:rgba(99,102,241,0.12);color:rgba(165,180,252,0.4);
  cursor:not-allowed;box-shadow:none}
.load-row{display:grid;grid-template-columns:1fr 1fr;gap:10px}
.btn.secondary{background:rgba(255,255,255,0.045);border:1px solid var(--border2);color:var(--text2)}
.btn.secondary:hover{background:rgba(255,255,255,0.075);border-color:rgba(255,255,255,0.13)}
.btn.secondary:disabled{background:rgba(255,255,255,0.025);border-color:var(--border);
  color:var(--text5);cursor:not-allowed}

/* ── mini button (refresh) ── */
.btn-mini{display:inline-flex;align-items:center;gap:5px;border-radius:var(--radius-xs);
  padding:6px 12px;font-size:11px;font-weight:600;cursor:pointer;font-family:inherit;
  background:rgba(255,255,255,0.04);border:1px solid var(--border2);color:var(--text3);
  transition:all .15s ease}
.btn-mini:hover{background:rgba(255,255,255,0.07);border-color:rgba(255,255,255,0.12);color:var(--text2)}

/* ── footer ── */
.footer{text-align:center;font-size:11px;color:var(--text5);padding-top:4px}
</style>
</head>
<body>

<div class="header">
  <div class="logo"><img src="{{BRAND_LOGO_DATA_URI}}" alt="Virtual Pilot Network logo" /></div>
  <div class="header-text">
    <h1>Virtual Pilot Network</h1>
    <p>Flight companion</p>
  </div>
</div>

<!-- identity row -->
<div class="row">
  <div class="panel">
    <div class="field-label">Account</div>
    <div class="field-value" id="user">--</div>
  </div>
  <div class="panel">
    <div class="field-label">Simulator <span class="dot idle" id="connDot"></span></div>
    <div class="field-value" id="connText">Checking simulator...</div>
  </div>
</div>

<!-- contract (featured) -->
<div class="featured">
  <div class="featured-inner">
    <div style="display:flex;align-items:start;justify-content:space-between;gap:12px">
      <div>
        <div class="field-label" style="color:rgba(165,180,252,0.7)">Active flight assignment</div>
        <div class="field-value" id="contract" style="margin-top:6px">Loading active contract...</div>
      </div>
      <button class="btn-mini" onclick="post({action:'refreshContract'})">&#8635; Refresh</button>
    </div>
  </div>
</div>

<!-- aircraft -->
<div class="panel">
  <div class="field-label">Current aircraft</div>
  <div class="field-value large" id="aircraft">--</div>
</div>

<!-- status row -->
<div class="row">
  <div class="panel">
    <div class="field-label">Flight state <span class="dot idle" id="stateDot"></span></div>
    <div class="field-value" id="stateText">Ready</div>
  </div>
  <div class="panel">
    <div class="field-label">Readiness <span class="dot idle" id="readyDot"></span></div>
    <div class="field-value" id="readyText">Waiting for simulator telemetry.</div>
  </div>
</div>

<!-- actions -->
<div class="load-row">
  <button class="btn secondary" id="payloadBtn" disabled onclick="post({action:'loadPayload'})">Load payload</button>
  <button class="btn secondary" id="fuelBtn" disabled onclick="post({action:'loadFuel'})">Load fuel</button>
</div>
<div class="btn-row">
  <button class="btn primary" id="startBtn" disabled onclick="post({action:'startFlight'})">Start flight</button>
  <button class="btn primary" id="finishBtn" disabled onclick="post({action:'finishFlight'})">Finish flight</button>
</div>

<div class="footer">Virtual Pilot Network</div>

<script>
function post(msg){window.chrome?.webview?.postMessage(msg)}

function dotClass(s){
  if(s==='ok')return'ok';if(s==='warn')return'warn';
  if(s==='error')return'error';if(s==='info')return'info';return'idle';
}

window.chrome?.webview?.addEventListener('message',function(e){
  var s=e.data;if(!s)return;

  setText('user',s.user||'--');
  setText('connText',s.connText||'Checking simulator...');
  setDot('connDot',s.connDot);
  setText('contract',s.contract||'None');
  setText('aircraft',s.aircraft||'--');
  setText('stateText',s.stateText||'Ready');
  setDot('stateDot',s.stateDot);
  setText('readyText',s.readyText||'');
  setDot('readyDot',s.readyDot);

  var sb=document.getElementById('startBtn');
  var fb=document.getElementById('finishBtn');
  var pb=document.getElementById('payloadBtn');
  var fuel=document.getElementById('fuelBtn');
  sb.disabled=!s.startEnabled;
  fb.disabled=!s.finishEnabled;
  pb.disabled=!s.loadPayloadEnabled;
  fuel.disabled=!s.loadFuelEnabled;
  pb.textContent=s.payloadButtonText||'Load payload';
  fuel.textContent=s.fuelButtonText||'Load fuel';
});

function setText(id,t){var e=document.getElementById(id);if(e)e.textContent=t}
function setDot(id,c){var e=document.getElementById(id);if(e){e.className='dot';e.classList.add(dotClass(c))}}
</script>
</body>
</html>
""";
}
