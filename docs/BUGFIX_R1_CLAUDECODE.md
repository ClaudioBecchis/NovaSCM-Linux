# NovaSCM-Linux — Round 1 · Code Review v1.7.2

**Commit analizzato:** `d724f69` (v1.7.2)  
**Repository:** https://github.com/ClaudioBecchis/NovaSCM-Linux  
**Stack:** C# .NET 9 · Avalonia UI · SQLite (Microsoft.Data.Sqlite)  
**Piattaforme:** Linux + Windows (cross-platform)  
**File esaminati:** `DeployBuilders.cs` (393 righe), `Views/MainWindow.axaml.cs` (1108 righe), `Database.cs` (624 righe), `Models.cs`, `BuildConfig.cs`, `Platform/LinuxPlatform.cs`, `Platform/WindowsPlatform.cs`

---

## Riepilogo

| Severità | N° | Titolo |
|---|---|---|
| 🔴 CRITICAL | 3 | XML injection · PowerShell injection · `Invoke-Expression` senza verifica integrità |
| 🟡 MEDIUM   | 4 | PveLog leaka credenziali · AdminPass plaintext · USB senza confirm · postinstall senza API key |
| 🔵 INFO     | 3 | Colonna `note` inesistente · AppVersion sbagliata · SSH key hardcoded |

---

## 🔴 C-1 · `DeployBuilders.cs` — XML Injection in `autounattend.xml`

**Tutti i valori utente interpolati nell'XML senza escape.**

```csharp
// ATTUALE — nessun escape
$"<Value>{cfg.AdminPassword}</Value><PlainText>true</PlainText>"
$"<n>{cfg.UserName}</n>"
$"<Key>{cfg.ProductKey.Trim().ToUpper()}</Key>"
$"<ComputerName>{pcName}</ComputerName>"
```

Se `AdminPassword` = `P@ss</Value><FakeNode>` oppure contiene `&`, `<`, `>`, il documento XML generato sarà invalido o conterrà nodi iniettati. Windows Setup potrebbe leggere valori errati o l'XML potrebbe non parsare.

Il server NovaSCM (`api.py`) usa correttamente `_xe()` (xml.sax.saxutils.escape) per ogni valore — qui manca l'equivalente.

**Fix — aggiungere `XmlEscape()` helper:**
```csharp
private static string Xe(string? s) =>
    System.Security.SecurityElement.Escape(s ?? "") ?? "";

// Poi usarlo su ogni valore interpolato nel XML:
$"<Value>{Xe(cfg.AdminPassword)}</Value>"
$"<n>{Xe(cfg.UserName)}</n>"
$"<ComputerName>{Xe(pcName)}</ComputerName>"
$"<Key>{Xe(cfg.ProductKey.Trim().ToUpper())}</Key>"
```

`System.Security.SecurityElement.Escape` è disponibile in .NET 9 su tutte le piattaforme e sostituisce `&`, `<`, `>`, `"`, `'` con le entità XML corrette.

---

## 🔴 C-2 · `DeployBuilders.cs` — PowerShell Injection nella specialize pass

**`DomainPassword` e `DomainUser` interpolati in una stringa di comando PowerShell.**

```csharp
// ATTUALE — DomainPassword potrebbe contenere '
$"...Add-Computer -DomainName '{cfg.DomainName}' -Credential " +
$"(New-Object PSCredential('{cfg.DomainName}\\{cfg.DomainUser}'," +
$"(ConvertTo-SecureString '{cfg.DomainPassword}' -AsPlainText -Force)))..."
```

Se `DomainPassword` = `pass'word` oppure `pass'; Remove-Item -Recurse C:\`, il comando PowerShell incorporato nell'autounattend si rompe o viene iniettato. L'autounattend specialize viene eseguito con privilegi SYSTEM durante il setup.

**Fix — usare variabili d'ambiente invece di interpolazione diretta:**
```csharp
// In RunSynchronousCommand, impostare le credenziali via reg temporaneo
// e leggerle nel comando PowerShell senza interpolarle nella stringa
$@"powershell.exe -NonInteractive -Command ""
$domain = '{Xe(cfg.DomainName)}';
$user   = '{Xe(cfg.DomainUser)}';
$pass   = (Get-ItemProperty 'HKLM:\SOFTWARE\NovaSCM\Deploy' -ErrorAction SilentlyContinue).JoinPass;
if ($pass) {{
  Add-Computer -DomainName $domain -Credential (New-Object PSCredential(
    ""$domain\$user"", (ConvertTo-SecureString $pass -AsPlainText -Force)
  )) -Force -ErrorAction SilentlyContinue
}}
"""
```

Oppure, più semplicemente, usare il metodo già presente nel server NovaSCM (ODJ Blob via `djoin.exe`) che non richiede di passare le credenziali nell'XML.

---

## 🔴 C-3 · `DeployBuilders.cs` riga ~305 — `Invoke-Expression` senza verifica integrità

**L'agente viene scaricato e immediatamente eseguito senza verifica SHA-256.**

```powershell
# Generato in BuildPostInstallScript:
$installerScript = (Invoke-WebRequest -Uri '$wfAgentInstaller' -UseBasicParsing).Content
Invoke-Expression $installerScript   # ← download + eval diretto
```

Chiunque riesca a fare un attacco MITM (rete non TLS, certificato non verificato, DNS spoofing) può fare eseguire codice arbitrario con privilegi SYSTEM durante il post-install. Il server NovaSCM ha già un endpoint `/api/download/agent-install.ps1` che genera uno script che a sua volta verifica SHA-256 — ma lo script figlio chiama `Invoke-Expression` sul contenuto scaricato direttamente.

**Fix — scaricare, verificare, poi eseguire:**
```powershell
$tmpScript = Join-Path $env:TEMP "novascm-agent-install-$([System.IO.Path]::GetRandomFileName()).ps1"
try {
    $Headers = @{ 'X-Api-Key' = '$apiKey' }
    Invoke-WebRequest -Uri '$wfAgentInstaller' -OutFile $tmpScript -UseBasicParsing -Headers $Headers
    # Verifica SHA-256 contro endpoint /api/download/agent-install.ps1.sha256 (da aggiungere al server)
    powershell.exe -NonInteractive -ExecutionPolicy Bypass -File $tmpScript
} finally {
    if (Test-Path $tmpScript) { Remove-Item -Force $tmpScript }
}
```

---

## 🟡 M-1 · `Views/MainWindow.axaml.cs` riga ~308 — PveLog leaka credenziali in `/tmp`

**Tutti i dettagli di autenticazione Proxmox vengono scritti in `/tmp/novascm_pve.log`.**

```csharp
private static void PveLog(string tag, string msg) {
    File.AppendAllText("/tmp/novascm_pve.log",
        $"[{DateTime.Now:HH:mm:ss}] [{tag}] {msg}\n");
}
// Chiamate:
PveLog("AUTH", $"password mode user={user} passLen={pass.Length}");
PveLog("LOGIN", $"status={loginResp.StatusCode} body={loginJson[..200]}");  // contiene il ticket!
PveLog("TICKET", "ok, getting nodes");
```

Il log include: nome utente, corpo della risposta `/access/ticket` (che contiene il PVEAuthCookie completo), token API. Su Linux `/tmp` è world-readable per default — qualsiasi processo locale può leggere il file.

**Fix:**
1. Rimuovere il log in produzione o limitarlo a `--debug` mode
2. Se necessario mantenere il log, usare la directory dell'app con permessi 600:
```csharp
private static readonly string _pveLogPath =
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                 "NovaSCM", "logs", "proxmox.log");

private static void PveLog(string tag, string msg) {
    // Non loggare mai dati di autenticazione
    if (tag is "LOGIN" or "TICKET") return;
    try { File.AppendAllText(_pveLogPath, $"[{DateTime.Now:HH:mm:ss}] [{tag}] {msg}\n"); }
    catch { }
}
```

---

## 🟡 M-2 · `Views/MainWindow.axaml.cs` — `AdminPass`, `UnifiPass`, `AdminUser` salvati in plaintext

**`config.json` contiene le credenziali admin in chiaro.**

```csharp
// SaveConfig():
_config.AdminPass = TxtAdminPass.Text ?? "";  // password admin Windows
_config.UnifiPass = TxtUnifiPass.Text ?? "";  // password UniFi

// Scritto in:
File.WriteAllText(ConfigPath,
    JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
// ConfigPath = %APPDATA%/NovaSCM/config.json
```

`AdminPass` è la stessa password usata nell'`autounattend.xml` generato — se il file viene letto da un processo non privilegiato o sincronizzato accidentalmente su cloud, espone le credenziali di tutti i PC deployati.

**Fix — cifrare i campi sensibili con DPAPI (Windows) o SecretService (Linux):**
```csharp
// Windows: System.Security.Cryptography.ProtectedData.Protect()
// Linux: libsecret via PInvoke o semplicemente avvertire l'utente
// Soluzione minima cross-platform: Base64 + chiave derivata dal MachineName (deterrente, non sicurezza forte)
```

O almeno impostare `%APPDATA%/NovaSCM/config.json` con permessi 600 alla creazione.

---

## 🟡 M-3 · `Views/MainWindow.axaml.cs` riga ~950 — USB: copia sulla prima USB senza conferma

**`BtnDeployUsb_Click` usa sempre `usbPaths[0]` senza chiedere quale USB usare.**

```csharp
var drive = usbPaths[0];  // ← prima USB trovata, nessuna conferma
File.Copy(..., Path.Combine(drive, "autounattend.xml"), overwrite: true);
File.Copy(..., Path.Combine(drive, "postinstall.ps1"),  overwrite: true);
```

Se l'utente ha due chiavette inserite (es. una con dati importanti), i file vengono copiati sulla prima senza preavviso. Potrebbe sovrascrivere `autounattend.xml` su una chiavetta sbagliata.

**Fix — mostrare un dialog di selezione se ci sono più USB:**
```csharp
string drive;
if (usbPaths.Count == 1) {
    drive = usbPaths[0];
} else {
    // Mostrare MessageBox con lista USB per selezione
    var choice = await MessageBoxHelper.ShowChoice(
        $"Trovate {usbPaths.Count} USB. Scegli su quale copiare:", usbPaths, this);
    if (choice == null) return;
    drive = choice;
}
```

---

## 🟡 M-4 · `DeployBuilders.cs` — `Report-Step` in `postinstall.ps1` senza X-Api-Key

**Le chiamate all'API NovaSCM dal PC in installazione non includono l'header di autenticazione.**

```csharp
// Generato in BuildPostInstallScript:
var reportStepFn = ... $@"
function Report-Step {{
    ...
    $r = [Net.WebRequest]::Create(""$_crApi/by-name/$_hostname/step"")
    $r.Method = 'POST'; $r.ContentType = 'application/json'
    # ← nessun header X-Api-Key
    ...
}}"
```

Questo funziona solo perché `POST /api/cr/by-name/<pc_name>/step` manca di `@require_auth` nel server (bug C-1 del Round 8 NovaSCM). Se quel bug viene corretto (cosa raccomandata), tutti i `postinstall.ps1` già generati smettono di riportare gli step.

**Fix — includere la API key nella funzione generata:**
```csharp
var reportStepFn = !string.IsNullOrEmpty(cfg.NovaSCMCrApiUrl) ? $@"
$_crApi    = '{cfg.NovaSCMCrApiUrl}'
$_apiKey   = '{cfg.NovaSCMApiKey}'   # ← nuovo campo in DeployConfig
$_hostname = $env:COMPUTERNAME
function Report-Step {{
    ...
    $r.Headers['X-Api-Key'] = $_apiKey
    ...
}}" : ...
```

Aggiungere `NovaSCMApiKey` a `DeployConfig` e all'UI (campo nella tab Deploy accanto all'URL API).

---

## 🔵 I-1 · `Database.cs` riga 558 — Colonna `note` inesistente nello schema

**`SaveDeviceNote` e `SaveDeviceTag` fanno UPDATE su una colonna `note` non dichiarata.**

```csharp
// Schema in Initialize():
CREATE TABLE IF NOT EXISTS devices (
    ip TEXT PRIMARY KEY, mac TEXT, ..., last_seen TEXT
    -- ← nessuna colonna 'note'
)

// In SaveDeviceNote():
cmd2.CommandText = "UPDATE devices SET note=$note WHERE ip=$ip;";
try { cmd2.ExecuteNonQuery(); } catch { }  // ← eccezione silenziosa, nota non salvata
```

Ogni chiamata a `SaveDeviceNote` o `SaveDeviceTag` fallisce silenziosamente. Le note sui device non vengono mai persistite.

**Fix — aggiungere la colonna allo schema e alla migration:**
```csharp
// In Initialize(), aggiungere alla CREATE TABLE devices:
notes TEXT NOT NULL DEFAULT ''

// Oppure come migration:
try { conn.Execute("ALTER TABLE devices ADD COLUMN notes TEXT NOT NULL DEFAULT ''"); }
catch (SqliteException e) when (e.Message.Contains("duplicate column")) { }
```

---

## 🔵 I-2 · `Views/MainWindow.axaml.cs` riga 18 — `AppVersion` sbagliata

**La costante `AppVersion` è `"1.1.0"` ma il repo è alla v1.7.2 (da `git log`).**

```csharp
private const string AppVersion = "1.1.0";  // ← obsoleta
```

Mostrata nell'UI (tab About, status bar) e potenzialmente usata per confronti di versione futuri.

**Fix — allineare con il tag git o leggere da assembly:**
```csharp
private static readonly string AppVersion =
    System.Reflection.Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString(3) ?? "1.7.2";
```

Oppure impostare la versione in `NovaSCM.csproj`:
```xml
<Version>1.7.2</Version>
```
e leggerla da assembly — così la versione è definita in un solo posto.

---

## 🔵 I-3 · `Views/MainWindow.axaml.cs` riga ~1010 — SSH key hardcoded per SCP/PXE upload

**`BtnDeployPxe_Click` usa sempre `~/.ssh/id_ed25519` senza fallback.**

```csharp
var keyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_ed25519");
// ...
scpInfo.ArgumentList.Add("-i");
scpInfo.ArgumentList.Add(keyPath);
```

Se l'utente usa `id_rsa`, `id_ecdsa`, o una chiave con path personalizzato, l'upload SCP fallisce con un messaggio di errore non chiaro.

**Fix — usare `IPlatform.GetDefaultSshKeyPath()` (già presente) e aggiungere un campo nell'UI Settings:**
```csharp
// IPlatform.GetDefaultSshKeyPath() è già definito in LinuxPlatform/WindowsPlatform
// Aggiungere in AppConfig:
public string SshKeyPath { get; set; } = "";

// In BtnDeployPxe_Click:
var keyPath = !string.IsNullOrEmpty(_config.SshKeyPath)
    ? _config.SshKeyPath
    : App.Platform.GetDefaultSshKeyPath();
```

---

## Nota di compatibilità: round 8 NovaSCM → NovaSCM-Linux

Il fix C-1/C-2 del Round 8 NovaSCM (aggiunta `@require_auth` a `report_step`) **romperà** i `postinstall.ps1` generati da NovaSCM-Linux se non viene applicato prima il fix M-4 di questo report. Ordine consigliato:

1. Applicare M-4 (aggiungere `NovaSCMApiKey` a `DeployConfig` + UI)
2. Applicare C-1/C-2 Round 8 NovaSCM (aggiungere `@require_auth`)
3. Applicare C-1/C-2/C-3 di questo report (XML/PS injection + Invoke-Expression)
