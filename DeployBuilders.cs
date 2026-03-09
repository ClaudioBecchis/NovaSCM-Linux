using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NovaSCM;

// Builder statici per autounattend.xml e postinstall.ps1
// Logica identica alla versione WPF (PolarisManager/MainWindow.xaml.cs)
internal static class DeployBuilders
{
    internal static string BuildAutounattendXml(DeployConfig cfg)
    {
        var (inputLocale, _) = cfg.Locale switch
        {
            "en-US" => ("en-US", "0409:00000409"),
            "en-GB" => ("en-GB", "0809:00000809"),
            "fr-FR" => ("fr-FR", "040c:0000040c"),
            "de-DE" => ("de-DE", "0407:00000407"),
            _       => ("it-IT", "0410:00000410"),
        };

        var pcName   = cfg.PcNameTemplate.Contains("{MAC6}") ? "*" : cfg.PcNameTemplate;
        var isServer = cfg.WinEdition.Contains("Server");

        var keySection = string.IsNullOrWhiteSpace(cfg.ProductKey) ? "" :
            $"      <ProductKey><Key>{cfg.ProductKey.Trim().ToUpper()}</Key></ProductKey>\n";

        var userSection = string.IsNullOrEmpty(cfg.UserName) ? "" :
            $@"
          <LocalAccount wcm:action=""add"">
            <Name>{cfg.UserName}</Name>
            <Group>Users</Group>
            <DisplayName>{cfg.UserName}</DisplayName>
            <Password><Value>{cfg.UserPassword}</Value><PlainText>true</PlainText></Password>
          </LocalAccount>";

        string oobeSection;
        if (isServer)
            oobeSection = "      <!-- Server: nessun OOBE interattivo -->";
        else if (cfg.UseMicrosoftAccount)
            oobeSection = @"      <OOBE>
        <HideEULAPage>true</HideEULAPage>
        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>
        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>
        <NetworkLocation>Work</NetworkLocation>
        <ProtectYourPC>3</ProtectYourPC>
      </OOBE>";
        else
            oobeSection = @"      <OOBE>
        <HideEULAPage>true</HideEULAPage>
        <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>
        <HideWirelessSetupInOOBE>true</HideWirelessSetupInOOBE>
        <ProtectYourPC>3</ProtectYourPC>
      </OOBE>";

        var rsSb  = new StringBuilder();
        int rsOrd = 1;
        if (!cfg.UseMicrosoftAccount && cfg.DomainJoin != "AD")
            rsSb.Append($@"
        <RunSynchronousCommand wcm:action=""add"">
          <Order>{rsOrd++}</Order>
          <Path>reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f</Path>
          <Description>Bypass account Microsoft</Description>
        </RunSynchronousCommand>");
        if (cfg.DomainJoin == "AD" && !string.IsNullOrEmpty(cfg.DomainControllerIp))
            rsSb.Append($@"
        <RunSynchronousCommand wcm:action=""add"">
          <Order>{rsOrd++}</Order>
          <Path>powershell.exe -NonInteractive -Command ""for($i=0;$i-lt30;$i++){{$n=Get-NetAdapter|?{{$_.Status-eq'Up'-and$_.HardwareInterface}}|Select -First 1;if($n){{Set-DnsClientServerAddress -InterfaceIndex $n.InterfaceIndex -ServerAddresses '{cfg.DomainControllerIp}';break}};Start-Sleep 2}}""</Path>
          <Description>Attendi rete e imposta DNS DC</Description>
        </RunSynchronousCommand>");
        if (cfg.DomainJoin == "AD" && !string.IsNullOrEmpty(cfg.DomainName))
            rsSb.Append($@"
        <RunSynchronousCommand wcm:action=""add"">
          <Order>{rsOrd++}</Order>
          <Path>powershell.exe -NonInteractive -Command ""Add-Computer -DomainName '{cfg.DomainName}' -Credential (New-Object PSCredential('{cfg.DomainName}\{cfg.DomainUser}',(ConvertTo-SecureString '{cfg.DomainPassword}' -AsPlainText -Force))) -Force -ErrorAction SilentlyContinue""</Path>
          <Description>Join dominio AD</Description>
        </RunSynchronousCommand>");
        var runSyncBlock = rsSb.Length > 0
            ? $@"      <RunSynchronousCommands wcm:action=""add"">{rsSb}
      </RunSynchronousCommands>"
            : "";

        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<unattend xmlns=""urn:schemas-microsoft-com:unattend""
          xmlns:wcm=""http://schemas.microsoft.com/WMIConfig/2002/State""
          xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">

  <!-- ═══ windowsPE: disk + image selection ═══ -->
  <settings pass=""windowsPE"">
    <component name=""Microsoft-Windows-International-Core-WinPE""
               processorArchitecture=""amd64""
               publicKeyToken=""31bf3856ad364e35""
               language=""neutral"" versionScope=""nonSxS"">
      <SetupUILanguage><UILanguage>{cfg.Locale}</UILanguage></SetupUILanguage>
      <InputLocale>{inputLocale}</InputLocale>
      <SystemLocale>{cfg.Locale}</SystemLocale>
      <UILanguage>{cfg.Locale}</UILanguage>
      <UserLocale>{cfg.Locale}</UserLocale>
    </component>

    <component name=""Microsoft-Windows-Setup""
               processorArchitecture=""amd64""
               publicKeyToken=""31bf3856ad364e35""
               language=""neutral"" versionScope=""nonSxS"">
      <DiskConfiguration>
        <WillShowUI>OnError</WillShowUI>
        <Disk wcm:action=""add"">
          <DiskID>0</DiskID>
          <WillWipeDisk>true</WillWipeDisk>
          <CreatePartitions>
            <CreatePartition wcm:action=""add"">
              <Order>1</Order><Type>EFI</Type><Size>100</Size>
            </CreatePartition>
            <CreatePartition wcm:action=""add"">
              <Order>2</Order><Type>MSR</Type><Size>16</Size>
            </CreatePartition>
            <CreatePartition wcm:action=""add"">
              <Order>3</Order><Type>Primary</Type><Extend>true</Extend>
            </CreatePartition>
          </CreatePartitions>
          <ModifyPartitions>
            <ModifyPartition wcm:action=""add"">
              <Order>1</Order><PartitionID>1</PartitionID><Format>FAT32</Format><Label>System</Label>
            </ModifyPartition>
            <ModifyPartition wcm:action=""add"">
              <Order>2</Order><PartitionID>3</PartitionID><Format>NTFS</Format><Label>Windows</Label><Letter>C</Letter>
            </ModifyPartition>
          </ModifyPartitions>
        </Disk>
      </DiskConfiguration>
      <ImageInstall>
        <OSImage>
          <InstallTo><DiskID>0</DiskID><PartitionID>3</PartitionID></InstallTo>
          <InstallFrom>
            <MetaData wcm:action=""add"">
              <Key>/IMAGE/NAME</Key><Value>{cfg.WinEdition}</Value>
            </MetaData>
          </InstallFrom>
          <WillShowUI>OnError</WillShowUI>
        </OSImage>
      </ImageInstall>
      <UserData>
        <AcceptEula>true</AcceptEula>
        <FullName>Utente</FullName>
        <Organization>NovaSCM</Organization>
{keySection}      </UserData>
    </component>
  </settings>

  <!-- ═══ specialize: computer name + timezone ═══ -->
  <settings pass=""specialize"">
    <component name=""Microsoft-Windows-Shell-Setup""
               processorArchitecture=""amd64""
               publicKeyToken=""31bf3856ad364e35""
               language=""neutral"" versionScope=""nonSxS"">
      <ComputerName>{pcName}</ComputerName>
      <TimeZone>{cfg.TimeZone}</TimeZone>
      <RegisteredOrganization>NovaSCM</RegisteredOrganization>
      {runSyncBlock}
    </component>
    <component name=""Microsoft-Windows-International-Core""
               processorArchitecture=""amd64""
               publicKeyToken=""31bf3856ad364e35""
               language=""neutral"" versionScope=""nonSxS"">
      <InputLocale>{inputLocale}</InputLocale>
      <SystemLocale>{cfg.Locale}</SystemLocale>
      <UILanguage>{cfg.Locale}</UILanguage>
      <UserLocale>{cfg.Locale}</UserLocale>
    </component>
  </settings>

  <!-- ═══ oobeSystem: skip OOBE + autologon + postinstall ═══ -->
  <settings pass=""oobeSystem"">
    <component name=""Microsoft-Windows-Shell-Setup""
               processorArchitecture=""amd64""
               publicKeyToken=""31bf3856ad364e35""
               language=""neutral"" versionScope=""nonSxS"">
      {oobeSection}
      <UserAccounts>
        <LocalAccounts>
          <LocalAccount wcm:action=""add"">
            <Name>Administrator</Name>
            <Group>Administrators</Group>
            <Password>
              <Value>{cfg.AdminPassword}</Value>
              <PlainText>true</PlainText>
            </Password>
          </LocalAccount>{userSection}
        </LocalAccounts>
      </UserAccounts>
      <AutoLogon>
        <Password><Value>{cfg.AdminPassword}</Value><PlainText>true</PlainText></Password>
        <Enabled>true</Enabled>
        <LogonCount>1</LogonCount>
        <Username>Administrator</Username>
      </AutoLogon>
      <FirstLogonCommands>
        <SynchronousCommand wcm:action=""add"">
          <Order>1</Order>
          <CommandLine>{(string.IsNullOrEmpty(cfg.ServerUrl)
              ? @"cmd /c for %d in (D E F G H I J K L M N O P Q R S T U V W X Y Z) do if exist %d:\postinstall.ps1 copy /Y %d:\postinstall.ps1 C:\Windows\postinstall.ps1"
              : $@"powershell.exe -NonInteractive -ExecutionPolicy Bypass -Command ""iwr '{cfg.ServerUrl.TrimEnd('/')}/deploy/postinstall.ps1' -OutFile C:\Windows\postinstall.ps1 -UseBasicParsing""")}</CommandLine>
          <Description>NovaSCM: recupera postinstall.ps1</Description>
        </SynchronousCommand>
        <SynchronousCommand wcm:action=""add"">
          <Order>2</Order>
          <CommandLine>powershell.exe -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File C:\Windows\postinstall.ps1</CommandLine>
          <Description>NovaSCM post-install</Description>
        </SynchronousCommand>
      </FirstLogonCommands>
    </component>
  </settings>

</unattend>
<!-- Generato da NovaSCM il {DateTime.Now:yyyy-MM-dd HH:mm} -->
";
    }

    internal static string BuildPostInstallScript(DeployConfig cfg)
    {
        var pkgLines = cfg.WingetPackages.Count == 0 ? "# (nessun pacchetto selezionato)" :
            string.Join("\n", cfg.WingetPackages.Select(p =>
                $"Report-Step 'install_{p}' 'running'\n" +
                $"winget install --id {p} --silent --accept-package-agreements --accept-source-agreements 2>&1 | Write-Output\n" +
                $"Report-Step 'install_{p}' 'done'"));

        var reportStepFn = !string.IsNullOrEmpty(cfg.NovaSCMCrApiUrl) ? $@"
$_crApi    = '{cfg.NovaSCMCrApiUrl}'
$_hostname = $env:COMPUTERNAME
function Report-Step {{
    param([string]$Step, [string]$Status = 'done')
    if (-not $_crApi) {{ return }}
    try {{
        $b = [Text.Encoding]::UTF8.GetBytes(
            (ConvertTo-Json @{{step=$Step;status=$Status;ts=(Get-Date -Format 'o')}} -Compress))
        $r = [Net.WebRequest]::Create(""$_crApi/by-name/$_hostname/step"")
        $r.Method = 'POST'; $r.ContentType = 'application/json'
        $r.ContentLength = $b.Length; $r.Timeout = 5000
        $s = $r.GetRequestStream(); $s.Write($b,0,$b.Length); $s.Close()
        $r.GetResponse().Close()
    }} catch {{}}
}}" : "function Report-Step { param([string]$Step, [string]$Status = 'done') }  # API non configurata";

        var renamePc = cfg.PcNameTemplate.Contains("{MAC6}") ? @"
# Rinomina PC con ultimi 6 hex del MAC del primo adapter fisico
$adapter = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' -and $_.HardwareInterface } |
           Sort-Object InterfaceIndex | Select-Object -First 1
if ($adapter) {
    $mac6 = ($adapter.MacAddress -replace '[:\-]','').Substring(6).ToUpper()
    $newName = '" + cfg.PcNameTemplate.Replace("{MAC6}", "' + $mac6 + '") + @"'
    if ($env:COMPUTERNAME -ne $newName) {
        Rename-Computer -NewName $newName -Force -ErrorAction SilentlyContinue
        Write-Output ""PC rinominato: $newName""
    }
}" : $@"
# Nome PC fisso: {cfg.PcNameTemplate} (impostato dall'autounattend)";

        var domainSection = cfg.DomainJoin switch
        {
            "AzureAD" => $@"
# Azure AD Join
Write-Output 'Unione ad Azure AD in corso...'
try {{
    $dsreg = 'C:\Windows\System32\dsregcmd.exe'
    {(string.IsNullOrEmpty(cfg.AzureTenantId) ? "" : $"$env:AAD_TENANT_ID = '{cfg.AzureTenantId}'")}
    & $dsreg /join 2>&1 | Write-Output
    Write-Output 'Azure AD join avviato — completare il login al primo avvio'
}} catch {{
    Write-Warning ""Azure AD join fallito: $($_.Exception.Message)""
}}",
            "AD" => $"# Unione a {cfg.DomainName} già completata in fase di setup (autounattend specialize)",
            _    => "# Nessun dominio configurato (Workgroup)"
        };

        var checkinSection = !string.IsNullOrEmpty(cfg.NovaSCMCrApiUrl) ? $@"
# Check-in NovaSCM — registra completamento installazione
try {{
    $hostname = $env:COMPUTERNAME
    $body = ConvertTo-Json @{{ hostname=$hostname; event='postinstall_done'; timestamp=(Get-Date -Format 'o') }}
    Invoke-RestMethod -Uri '{cfg.NovaSCMCrApiUrl}/by-name/$hostname/checkin' -Method POST `
        -Body $body -ContentType 'application/json' -UseBasicParsing -ErrorAction Stop
    Write-Output 'Check-in NovaSCM: OK'
}} catch {{
    Write-Warning ""Check-in NovaSCM non riuscito (continua comunque): $($_.Exception.Message)""
}}" : "# NovaSCM API non configurata — skip check-in";

        var agentSection = cfg.IncludeAgent && !string.IsNullOrEmpty(cfg.ServerUrl) ? $@"
# Installa agente NovaSCM (enrollment WiFi EAP-TLS)
try {{
    $agentUrl = '{cfg.ServerUrl.TrimEnd('/')}/agent/install.ps1'
    Write-Output ""Download agente da: $agentUrl""
    Invoke-RestMethod -Uri $agentUrl -UseBasicParsing | Invoke-Expression
    Write-Output 'Agente NovaSCM installato'
}} catch {{
    Write-Warning ""Agente NovaSCM non raggiungibile: $($_.Exception.Message)""
}}" : "# Agente WiFi EAP-TLS non incluso";

        var novaSCMBaseUrl = !string.IsNullOrEmpty(cfg.NovaSCMCrApiUrl)
            ? cfg.NovaSCMCrApiUrl.Replace("/api/cr", "").TrimEnd('/')
            : (!string.IsNullOrEmpty(cfg.ServerUrl) ? cfg.ServerUrl.TrimEnd('/') : "");
        var workflowAgentSection = !string.IsNullOrEmpty(novaSCMBaseUrl) ? $@"
# Installa NovaSCM Workflow Agent (servizio di esecuzione workflow)
Report-Step 'workflow_agent_install' 'running'
try {{
    $wfAgentInstaller = '{novaSCMBaseUrl}/api/download/agent-install.ps1'
    Write-Output ""Download NovaSCM Workflow Agent da: $wfAgentInstaller""
    $installerScript = (Invoke-WebRequest -Uri $wfAgentInstaller -UseBasicParsing).Content
    $installerScript = $installerScript -replace '\$ApiUrl\s*=.*', '$ApiUrl = ""{novaSCMBaseUrl}""'
    Invoke-Expression $installerScript
    Write-Output 'NovaSCM Workflow Agent installato come servizio Windows'
    Report-Step 'workflow_agent_install' 'done'
}} catch {{
    Write-Warning ""NovaSCM Workflow Agent non installato: $($_.Exception.Message)""
    Report-Step 'workflow_agent_install' 'error'
}}" : "# NovaSCM Workflow Agent: API URL non configurato — skip";

        return $@"#Requires -RunAsAdministrator
# ============================================================
# postinstall.ps1 — generato da NovaSCM il {DateTime.Now:yyyy-MM-dd HH:mm}
# Eseguito automaticamente al primo avvio post-installazione
# ============================================================
Set-ExecutionPolicy Bypass -Scope Process -Force
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
{reportStepFn}

$logFile = 'C:\Windows\Temp\novascm_postinstall.log'
Start-Transcript -Path $logFile -Append

Write-Output '=== NovaSCM Post-Install avviato ==='

# Lancia schermata OSD dalla USB (se NovaSCM.exe presente)
$usbDrive = $null
foreach ($d in @('D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','X','Y','Z')) {{
    if (Test-Path ""${{d}}:\NovaSCM.exe"") {{ $usbDrive = ""${{d}}:""; break }}
}}
if ($usbDrive) {{
    $osdExe = 'C:\Windows\Temp\NovaSCM-OSD.exe'
    Copy-Item ""$usbDrive\NovaSCM.exe"" $osdExe -Force -ErrorAction SilentlyContinue
    if (Test-Path $osdExe) {{
        $apiArg = if ($_crApi) {{ ""--osd $env:COMPUTERNAME $_crApi"" }} else {{ ""--osd $env:COMPUTERNAME"" }}
        Start-Process $osdExe $apiArg -WindowStyle Normal -ErrorAction SilentlyContinue
        Start-Sleep 2
    }}
}}

Report-Step 'postinstall_start'
{renamePc}
Report-Step 'rename_pc'
{domainSection}

# Installa winget se mancante (Windows 10 — Win11 ce l'ha già)
if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {{
    Write-Output 'Installazione winget...'
    Report-Step 'winget_install' 'running'
    $wgUrl = 'https://github.com/microsoft/winget-cli/releases/latest/download/Microsoft.DesktopAppInstaller_8wekyb3d8bbwe.msixbundle'
    $wgTmp = ""$env:TEMP\winget.msixbundle""
    Invoke-WebRequest -Uri $wgUrl -OutFile $wgTmp -UseBasicParsing
    Add-AppxPackage -Path $wgTmp -ErrorAction SilentlyContinue
    Report-Step 'winget_install' 'done'
}}

# Installa software
Write-Output 'Installazione software...'
{pkgLines}

Report-Step 'agent_install' 'running'
{agentSection}
Report-Step 'agent_install' 'done'

{workflowAgentSection}

{checkinSection}

Write-Output '=== Post-install completato ==='
Stop-Transcript

# Riavvio finale (dopo 15 secondi)
Start-Sleep -Seconds 5
shutdown /r /t 15 /c ""NovaSCM: configurazione completata. Riavvio in 15 secondi.""
";
    }
}
