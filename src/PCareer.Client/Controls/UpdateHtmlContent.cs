namespace PCareer.Client;

internal static class UpdateHtmlContent
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
  --border:rgba(255,255,255,0.065);
  --radius:14px;--radius-xs:8px;
}
html,body{height:100%;background:var(--bg);color:var(--text);
  font-family:'Inter','Segoe UI',system-ui,sans-serif;font-size:14px;line-height:1.5}
body{display:flex;align-items:center;justify-content:center;padding:24px}

.card{width:100%;max-width:420px;border:1px solid var(--border);border-radius:var(--radius);
  background:var(--surface);padding:36px 32px;text-align:center}

/* logo */
.logo{width:52px;height:52px;border-radius:14px;margin:0 auto 20px;
  background:radial-gradient(circle at 50% 50%,rgba(99,102,241,0.15),rgba(99,102,241,0.04));
  border:1px solid rgba(99,102,241,0.18);display:grid;place-items:center}
.logo-inner{font-size:17px;font-weight:700;color:var(--indigo2);letter-spacing:0.02em}

/* states */
.title{font-size:18px;font-weight:600;color:var(--text);margin-bottom:6px}
.subtitle{font-size:13px;color:var(--text3);line-height:1.6}

/* spinner */
.spinner{width:36px;height:36px;border:3px solid rgba(99,102,241,0.15);
  border-top-color:var(--indigo);border-radius:50%;margin:24px auto 0;
  animation:spin .8s linear infinite}
@keyframes spin{to{transform:rotate(360deg)}}

/* version badge */
.ver-badge{display:inline-flex;align-items:center;gap:6px;
  background:rgba(99,102,241,0.08);border:1px solid rgba(99,102,241,0.15);
  border-radius:6px;padding:6px 14px;margin:16px 0;font-size:12px;font-weight:600;
  color:var(--indigo2);font-family:ui-monospace,SFMono-Regular,Menlo,monospace;
  letter-spacing:0.04em}
.ver-badge .arrow{color:var(--text4);font-size:14px}

/* progress */
.progress-wrap{margin:20px 0 4px}
.progress-bar{width:100%;height:6px;background:rgba(255,255,255,0.06);border-radius:3px;overflow:hidden}
.progress-fill{height:100%;background:var(--indigo);border-radius:3px;width:0%;transition:width .3s ease}
.progress-text{font-size:12px;color:var(--text4);margin-top:8px}

/* buttons */
.btn-row{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-top:24px}
.btn{border:none;border-radius:var(--radius-xs);padding:12px 0;font-size:13px;
  font-weight:600;cursor:pointer;transition:all .15s ease;font-family:inherit;color:var(--text)}
.btn:active{transform:scale(0.98)}
.btn.primary{background:var(--indigo);color:#fff;box-shadow:0 4px 14px rgba(99,102,241,0.2)}
.btn.primary:hover{background:#6366f1;box-shadow:0 6px 20px rgba(99,102,241,0.28)}
.btn.primary:disabled{background:rgba(99,102,241,0.12);color:rgba(165,180,252,0.4);
  cursor:not-allowed;box-shadow:none}
.btn.ghost{background:rgba(255,255,255,0.04);border:1px solid rgba(255,255,255,0.08);color:var(--text3)}
.btn.ghost:hover{background:rgba(255,255,255,0.07);border-color:rgba(255,255,255,0.12);color:var(--text2)}

/* success check */
.check{width:48px;height:48px;border-radius:50%;background:rgba(52,211,153,0.1);
  border:2px solid rgba(52,211,153,0.3);margin:20px auto 0;display:grid;place-items:center}
.check svg{width:24px;height:24px;stroke:var(--emerald);stroke-width:2.5;fill:none}

.hidden{display:none}
</style>
</head>
<body>

<div class="card">
  <div class="logo"><span class="logo-inner">VP</span></div>

  <!-- checking state -->
  <div id="stateChecking">
    <div class="title">Checking for updates</div>
    <div class="subtitle">Verifying you have the latest version...</div>
    <div class="spinner"></div>
  </div>

  <!-- update available -->
  <div id="stateUpdate" class="hidden">
    <div class="title">Update available</div>
    <div class="subtitle">A new version of Virtual Pilot Network is ready.</div>
    <div class="ver-badge">
      <span id="verCurrent">-</span>
      <span class="arrow">&rarr;</span>
      <span id="verNew">-</span>
    </div>
    <div class="progress-wrap" id="progressWrap" class="hidden">
      <div class="progress-bar"><div class="progress-fill" id="progressFill"></div></div>
      <div class="progress-text" id="progressText">Downloading...</div>
    </div>
    <div class="btn-row" id="btnRow">
      <button class="btn primary" id="updateBtn" onclick="post({action:'update'})">Update &amp; restart</button>
      <button class="btn ghost" onclick="post({action:'quit'})">Quit</button>
    </div>
  </div>

  <!-- up to date -->
  <div id="stateCurrent" class="hidden">
    <div class="title">You're up to date</div>
    <div class="subtitle">Virtual Pilot Network is running the latest version.</div>
    <div class="check">
      <svg viewBox="0 0 24 24"><polyline points="20 6 9 17 4 12"/></svg>
    </div>
  </div>

  <!-- error -->
  <div id="stateError" class="hidden">
    <div class="title">Update check failed</div>
    <div class="subtitle" id="errorText">Could not check for updates.</div>
    <div class="btn-row">
      <button class="btn primary" onclick="post({action:'retry'})">Retry</button>
      <button class="btn ghost" onclick="post({action:'continue'})">Continue offline</button>
    </div>
  </div>
</div>

<script>
function post(msg){window.chrome?.webview?.postMessage(msg)}

function show(id){
  ['stateChecking','stateUpdate','stateCurrent','stateError'].forEach(function(s){
    var el=document.getElementById(s);if(el)el.classList.toggle('hidden',s!==id);
  });
}

window.chrome?.webview?.addEventListener('message',function(e){
  var s=e.data;if(!s)return;

  if(s.show){
    show(s.show);
    if(s.verCurrent)document.getElementById('verCurrent').textContent=s.verCurrent;
    if(s.verNew)document.getElementById('verNew').textContent=s.verNew;
  }

  if(s.progress!==undefined){
    var pct=Math.max(0,Math.min(100,s.progress));
    document.getElementById('progressFill').style.width=pct+'%';
    document.getElementById('progressText').textContent=pct>=100?'Installing...':'Downloading... '+pct+'%';
  }

  if(s.error){
    document.getElementById('errorText').textContent=s.error;
  }

  if(s.disableUpdate){
    var btn=document.getElementById('updateBtn');
    if(btn)btn.disabled=true;
  }
});
</script>
</body>
</html>
""";
}
