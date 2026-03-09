using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using NovaSCM.Helpers;

namespace NovaSCM.Views;

// ── MainWindow ────────────────────────────────────────────────────────────────
public partial class MainWindow : Window
{
    private const string AppVersion = "1.1.0";

    private static readonly string ConfigPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "NovaSCM", "config.json");

    private static readonly string ScriptsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "NovaSCM", "scripts");

    private AppConfig _config = new();
    private CancellationTokenSource? _scanCts;
    private readonly ObservableCollection<DeviceRow> _netRows     = [];
    private readonly ObservableCollection<DeviceRow> _netFiltered = [];
    private readonly ObservableCollection<PcRow>     _pcRows      = [];
    private readonly ObservableCollection<PveRow>    _pveRows     = [];
    private readonly ObservableCollection<CertRow>   _certRows    = [];
    private readonly ObservableCollection<ScriptRow> _scripts     = [];

    // OUI database
    private static readonly Dictionary<string, string> _ouiDb = [];
    private static bool _ouiLoaded = false;

    // Clock timer
    private readonly DispatcherTimer _clockTimer;

    public MainWindow()
    {
        InitializeComponent();
        Database.Initialize();

        // Bind grids
        NetGrid.ItemsSource    = _netFiltered;
        PcGrid.ItemsSource     = _pcRows;
        PveGrid.ItemsSource    = _pveRows;
        CertGrid.ItemsSource   = _certRows;
        LstScripts.ItemsSource = _scripts;

        // Config
        LoadConfig();
        LoadFromDatabase();
        LoadScripts();

        // About info
        TxtAboutVersion.Text  = $"v{AppVersion}";
        TxtAboutPlatform.Text = App.Platform.Name;
        TxtAboutRuntime.Text  = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        TxtAboutDb.Text       = ConfigPath.Replace("config.json", "novascm.db");
        TxtPlatformLabel.Text = App.Platform.Name;
        TxtVersion.Text       = $"v{AppVersion}";

        // Clock
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => TxtClock.Text = DateTime.Now.ToString("HH:mm:ss");
        _clockTimer.Start();

        // Load OUI in background
        _ = LoadOuiDatabaseAsync();
    }

    // ── Tab navigation ────────────────────────────────────────────────────────
    private void SetActiveTab(string name)
    {
        TabNet.IsVisible      = name == "Net";
        TabPc.IsVisible       = name == "Pc";
        TabProxmox.IsVisible  = name == "Proxmox";
        TabScripts.IsVisible  = name == "Scripts";
        TabCerts.IsVisible    = name == "Certs";
        TabSettings.IsVisible = name == "Settings";
        TabAbout.IsVisible    = name == "About";
        UpdateTabButtons(name);
    }

    private void UpdateTabButtons(string active)
    {
        var buttons = new (Button Btn, string Name)[] {
            (BtnTabNet,      "Net"),
            (BtnTabPc,       "Pc"),
            (BtnTabProxmox,  "Proxmox"),
            (BtnTabScripts,  "Scripts"),
            (BtnTabCerts,    "Certs"),
            (BtnTabSettings, "Settings"),
            (BtnTabAbout,    "About"),
        };
        foreach (var (btn, name) in buttons)
        {
            btn.Classes.Clear();
            btn.Classes.Add(name == active ? "tab-btn-active" : "tab-btn");
        }
    }

    private void BtnTabNet_Click(object? s, RoutedEventArgs e)      => SetActiveTab("Net");
    private void BtnTabPc_Click(object? s, RoutedEventArgs e)       => SetActiveTab("Pc");
    private void BtnTabProxmox_Click(object? s, RoutedEventArgs e)  => SetActiveTab("Proxmox");
    private void BtnTabScripts_Click(object? s, RoutedEventArgs e)  => SetActiveTab("Scripts");
    private void BtnTabCerts_Click(object? s, RoutedEventArgs e)    => SetActiveTab("Certs");
    private void BtnTabSettings_Click(object? s, RoutedEventArgs e) => SetActiveTab("Settings");
    private void BtnTabAbout_Click(object? s, RoutedEventArgs e)    => SetActiveTab("About");

    // ── Config ────────────────────────────────────────────────────────────────
    private void LoadConfig()
    {
        if (File.Exists(ConfigPath))
            try { _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new(); }
            catch { _config = new(); }
        ApplyConfigToUI();
    }

    private void ApplyConfigToUI()
    {
        TxtScanIp.Text               = _config.ScanNetwork;
        TxtScanSubnet.Text           = _config.ScanSubnet;
        TxtScanNetworks.Text         = _config.ScanNetworks;
        TxtSettingsScanIp.Text       = _config.ScanNetwork;
        TxtSettingsScanSubnet.Text   = _config.ScanSubnet;
        TxtSettingsScanNetworks.Text = _config.ScanNetworks;
        TxtNovaSCMApiUrl.Text        = _config.NovaSCMApiUrl;
        TxtUnifiUrl.Text             = _config.UnifiUrl;
        TxtUnifiUser.Text            = _config.UnifiUser;
        TxtUnifiPass.Text            = _config.UnifiPass;
        TxtCertportalUrl.Text        = _config.CertportalUrl;
        TxtSsid.Text                 = _config.Ssid;
        TxtRadiusIp.Text             = _config.RadiusIp;
        TxtCertDays.Text             = _config.CertDays;
        TxtOrgName.Text              = _config.OrgName;
        TxtDomain.Text               = _config.Domain;
        TxtAdminUser.Text            = _config.AdminUser;
        TxtAdminPass.Text            = _config.AdminPass;
    }

    private void SaveConfig()
    {
        _config.ScanNetwork   = TxtSettingsScanIp.Text?.Trim() ?? "";
        _config.ScanSubnet    = TxtSettingsScanSubnet.Text?.Trim() ?? "";
        _config.ScanNetworks  = TxtSettingsScanNetworks.Text ?? "";
        _config.NovaSCMApiUrl = TxtNovaSCMApiUrl.Text?.Trim() ?? "";
        _config.UnifiUrl      = TxtUnifiUrl.Text?.Trim() ?? "";
        _config.UnifiUser     = TxtUnifiUser.Text?.Trim() ?? "";
        _config.UnifiPass     = TxtUnifiPass.Text ?? "";
        _config.CertportalUrl = TxtCertportalUrl.Text?.Trim() ?? "";
        _config.Ssid          = TxtSsid.Text?.Trim() ?? "";
        _config.RadiusIp      = TxtRadiusIp.Text?.Trim() ?? "";
        _config.CertDays      = TxtCertDays.Text?.Trim() ?? "";
        _config.OrgName       = TxtOrgName.Text?.Trim() ?? "";
        _config.Domain        = TxtDomain.Text?.Trim() ?? "";
        _config.AdminUser     = TxtAdminUser.Text?.Trim() ?? "";
        _config.AdminPass     = TxtAdminPass.Text ?? "";
        var _d = Path.GetDirectoryName(ConfigPath)!; if (!Directory.Exists(_d)) Directory.CreateDirectory(_d);
        File.WriteAllText(ConfigPath,
            JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true }));
        TxtScanIp.Text       = _config.ScanNetwork;
        TxtScanSubnet.Text   = _config.ScanSubnet;
        TxtScanNetworks.Text = _config.ScanNetworks;
    }

    private void BtnSettingsSave_Click(object? s, RoutedEventArgs e)
    {
        SaveConfig();
        TxtSettingsStatus.Text = "✅ Configurazione salvata.";
    }

    // ── Database ──────────────────────────────────────────────────────────────
    private void LoadFromDatabase()
    {
        _pcRows.Clear();
        foreach (var p in Database.GetPcs())
            _pcRows.Add(p);
        _certRows.Clear();
        foreach (var c in Database.GetCerts())
            _certRows.Add(c);
    }

    // ── Scansione rete ────────────────────────────────────────────────────────
    private async void BtnScan_Click(object? s, RoutedEventArgs e)
    {
        var ipText     = TxtScanIp.Text?.Trim() ?? "";
        var subnetText = TxtScanSubnet.Text?.Trim() ?? "";
        if (!IPAddress.TryParse(ipText, out var baseIp))
        { SetStatus("⚠️ IP non valido"); return; }
        if (!int.TryParse(subnetText, out int cidr) || cidr < 1 || cidr > 30)
        { SetStatus("⚠️ Subnet non valida (1-30)"); return; }
        List<IPAddress> ips;
        try { ips = GetHostsInSubnet(baseIp, cidr); }
        catch (Exception ex) { SetStatus($"❌ {ex.Message}"); return; }
        await RunScanAsync(ips);
    }

    private async void BtnScanAll_Click(object? s, RoutedEventArgs e)
    {
        var lines = (TxtScanNetworks.Text ?? "").Split('\n',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var allIps = new List<IPAddress>();
        foreach (var net in lines)
        {
            var parts = net.Split('/');
            if (parts.Length != 2) continue;
            if (!IPAddress.TryParse(parts[0].Trim(), out var bip)) continue;
            if (!int.TryParse(parts[1].Trim(), out int cidr) || cidr < 1 || cidr > 30) continue;
            try { allIps.AddRange(GetHostsInSubnet(bip, cidr)); } catch { }
        }
        if (allIps.Count == 0) { SetStatus("⚠️ Nessuna subnet valida configurata"); return; }
        await RunScanAsync(allIps);
    }

    private async Task RunScanAsync(List<IPAddress> ips)
    {
        _netRows.Clear();
        _netFiltered.Clear();
        BtnScan.IsEnabled    = false;
        BtnScanAll.IsEnabled = false;
        BtnStop.IsEnabled    = true;
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        int total = ips.Count, done = 0, found = 0;
        ScanProgress.Maximum = total;
        ScanProgress.Value   = 0;

        var semaphore = new SemaphoreSlim(50);
        try
        {
            await Task.Run(async () =>
            {
                var tasks = ips.Select(async ip =>
                {
                    if (token.IsCancellationRequested) return;
                    await semaphore.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        using var ping = new Ping();
                        PingReply reply;
                        try { reply = await ping.SendPingAsync(ip, 800).ConfigureAwait(false); }
                        catch { Interlocked.Increment(ref done); return; }
                        Interlocked.Increment(ref done);

                        if (reply.Status == IPStatus.Success)
                        {
                            var row = new DeviceRow { Ip = ip.ToString(), Status = "🟢 Online" };
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                _netRows.Add(row);
                                found++;
                                ApplyFilter();
                            });

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var host = await Dns.GetHostEntryAsync(ip.ToString()).ConfigureAwait(false);
                                    row.Name = host.HostName.Split('.')[0];
                                }
                                catch { }
                            });

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    var ipStr = ip.ToString();
                                    var mac = App.Platform.GetMacFromArp(ipStr);
                                    if (mac == "—") mac = GetLocalMacForIp(ipStr);
                                    row.Mac    = mac;
                                    row.Vendor = LookupVendor(mac);
                                    if (row.Vendor == "—" && mac != "—")
                                        row.Vendor = await LookupVendorOnlineAsync(mac);

                                    var sigPorts = new[] { 22, 80, 443, 3389, 8006, 8123, 9000, 9090, 1883 };
                                    var openSig  = new ConcurrentBag<int>();
                                    await Task.WhenAll(sigPorts.Select(async port =>
                                    {
                                        if (await QuickPortOpenAsync(ipStr, port, 400))
                                            openSig.Add(port);
                                    }));
                                    row.DetectType(openSig);
                                }
                                catch { }
                            });
                        }

                        var d2 = done; var f2 = found;
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            ScanProgress.Value = d2;
                            TxtScanStatus.Text = $"Scansione: {d2}/{total} — {f2} device trovati";
                        });
                    }
                    catch (OperationCanceledException) { }
                    catch { }
                    finally { semaphore.Release(); }
                });

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }, CancellationToken.None);
        }
        catch { }

        BtnScan.IsEnabled    = true;
        BtnScanAll.IsEnabled = true;
        BtnStop.IsEnabled    = false;
        ScanProgress.Value   = total;
        var msg = token.IsCancellationRequested
            ? $"⏹ Scansione interrotta — {found} device trovati"
            : $"✅ Completata — {found} device online su {total} host";
        TxtScanStatus.Text = msg;
        SetStatus(msg);
        foreach (var d in _netRows)
            Database.UpsertDevice(d);
    }

    private void BtnStop_Click(object? s, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
        BtnScan.IsEnabled    = true;
        BtnScanAll.IsEnabled = true;
        BtnStop.IsEnabled    = false;
    }

    // ── Filtro ────────────────────────────────────────────────────────────────
    private void TxtFilter_TextChanged(object? s, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var filter = (TxtFilter.Text ?? "").ToLowerInvariant().Trim();
        _netFiltered.Clear();
        foreach (var r in _netRows)
        {
            if (string.IsNullOrEmpty(filter) ||
                r.Ip.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.Vendor.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.Mac.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                r.DeviceType.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                _netFiltered.Add(r);
            }
        }
    }

    // ── Selezione device ──────────────────────────────────────────────────────
    private void NetGrid_SelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (NetGrid.SelectedItem is DeviceRow dev)
        {
            TxtDetailIp.Text     = dev.Ip;
            TxtDetailName.Text   = dev.Name;
            TxtDetailMac.Text    = dev.Mac;
            TxtDetailVendor.Text = dev.Vendor;
            TxtDetailType.Text   = $"{dev.Icon} {dev.DeviceType}";
        }
    }

    // ── Azioni rete ───────────────────────────────────────────────────────────
    private void BtnRdp_Click(object? s, RoutedEventArgs e)
    {
        if (NetGrid.SelectedItem is DeviceRow dev)
            App.Platform.OpenRdp(dev.Ip);
    }

    private void BtnSsh_Click(object? s, RoutedEventArgs e)
    {
        if (NetGrid.SelectedItem is DeviceRow dev)
            App.Platform.OpenSsh(dev.Ip, _config.AdminUser.Length > 0 ? _config.AdminUser : "root");
    }

    private async void BtnWol_Click(object? s, RoutedEventArgs e)
    {
        if (NetGrid.SelectedItem is DeviceRow dev && dev.Mac != "—")
        {
            SendWol(dev.Mac);
            SetStatus($"⚡ WoL inviato a {dev.Mac}");
        }
        else
        {
            await MessageBoxHelper.ShowInfo("Seleziona un device con MAC valido per inviare WoL.", this);
        }
    }

    private static void SendWol(string mac)
    {
        var macBytes = mac.Replace(":", "").Replace("-", "")
            .Chunk(2).Select(c => Convert.ToByte(new string(c), 16)).ToArray();
        var packet = new byte[6 + 16 * 6];
        for (int i = 0; i < 6; i++) packet[i] = 0xFF;
        for (int i = 0; i < 16; i++) Array.Copy(macBytes, 0, packet, 6 + i * 6, 6);
        using var client = new UdpClient();
        client.EnableBroadcast = true;
        client.Send(packet, packet.Length, new IPEndPoint(IPAddress.Broadcast, 9));
    }

    // ── PC Fleet ──────────────────────────────────────────────────────────────
    private void BtnPcRefresh_Click(object? s, RoutedEventArgs e)
    {
        _pcRows.Clear();
        foreach (var p in Database.GetPcs())
            _pcRows.Add(p);
        SetStatus($"↺ {_pcRows.Count} PC caricati dal database.");
    }

    private async void BtnPcAdd_Click(object? s, RoutedEventArgs e)
    {
        await MessageBoxHelper.ShowInfo("Funzionalità: aggiunta PC manuale (in sviluppo).", this);
    }

    private void BtnPcDelete_Click(object? s, RoutedEventArgs e)
    {
        if (PcGrid.SelectedItem is PcRow pc)
        {
            Database.DeletePc(pc.Name);
            _pcRows.Remove(pc);
            SetStatus($"✕ PC '{pc.Name}' rimosso.");
        }
    }

    private void BtnPcSsh_Click(object? s, RoutedEventArgs e)
    {
        if (PcGrid.SelectedItem is PcRow pc && pc.Ip != "—")
            App.Platform.OpenSsh(pc.Ip, _config.AdminUser.Length > 0 ? _config.AdminUser : "root");
    }

    // ── Proxmox ───────────────────────────────────────────────────────────────
    private string _pveTicket = "";
    private string _pveCsrf   = "";
    private string PveBase => $"https://{TxtPveHost.Text?.Trim()}:8006/api2/json";

    private async void BtnPveConnect_Click(object? s, RoutedEventArgs e)
    {
        var host  = TxtPveHost.Text?.Trim() ?? "";
        var token = TxtPveToken.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(host)) { SetStatus("⚠️ Inserisci l'host Proxmox"); return; }
        SetStatus("🔷 Connessione a Proxmox...");
        PveLog("START", $"host={host} token={(!string.IsNullOrEmpty(token) ? "sì" : "no")}");
        try
        {
            // NEW-C: accetta solo self-signed (UntrustedRoot); blocca scaduti, revocati, hostname errati
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, chain, errors) =>
                    errors == System.Net.Security.SslPolicyErrors.None ||
                    (errors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors &&
                     chain?.ChainStatus.All(s => s.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot) == true)
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            HttpResponseMessage resp;
            if (!string.IsNullOrEmpty(token))
            {
                PveLog("AUTH", "token mode");
                http.DefaultRequestHeaders.Add("Authorization", $"PVEAPIToken={token}");
                resp = await http.GetAsync($"{PveBase}/nodes");
            }
            else
            {
                var user = TxtPveUser.Text?.Trim() ?? "";
                var pass = TxtPvePass.Text ?? "";
                PveLog("AUTH", $"password mode user={user} passLen={pass.Length}");
                var form = new StringContent($"username={user}&password={pass}",
                    System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
                var loginResp = await http.PostAsync($"{PveBase}/access/ticket", form);
                var loginJson = await loginResp.Content.ReadAsStringAsync();
                PveLog("LOGIN", $"status={loginResp.StatusCode} body={loginJson[..Math.Min(200,loginJson.Length)]}");
                var loginData = JsonDocument.Parse(loginJson);
                var dataElem  = loginData.RootElement.GetProperty("data");
                if (dataElem.ValueKind == JsonValueKind.Null)
                {
                    var msg = loginData.RootElement.TryGetProperty("message", out var m) ? m.GetString() : "credenziali errate";
                    SetStatus($"❌ Proxmox: {msg}");
                    PveLog("ERROR", $"data=null msg={msg}");
                    return;
                }
                _pveTicket = dataElem.GetProperty("ticket").GetString() ?? "";
                _pveCsrf   = dataElem.GetProperty("CSRFPreventionToken").GetString() ?? "";
                http.DefaultRequestHeaders.Add("Cookie", $"PVEAuthCookie={_pveTicket}");
                http.DefaultRequestHeaders.Add("CSRFPreventionToken", _pveCsrf);
                PveLog("TICKET", "ok, getting nodes");
                resp = await http.GetAsync($"{PveBase}/nodes");
            }
            var nodesJson = await resp.Content.ReadAsStringAsync();
            PveLog("NODES", $"status={resp.StatusCode} body={nodesJson[..Math.Min(200,nodesJson.Length)]}");
            var nodesDoc  = JsonDocument.Parse(nodesJson);
            var node      = nodesDoc.RootElement.GetProperty("data")[0].GetProperty("node").GetString() ?? "pve";
            await Dispatcher.UIThread.InvokeAsync(() => {
                TxtPveNode.Text      = node;
                PveNodeBar.IsVisible = true;
            });
            SetStatus($"✅ Connesso a Proxmox — nodo {node}");
            PveLog("OK", $"node={node}");
            await LoadPveVmsAsync(http, node);
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException?.Message ?? ex.Message;
            SetStatus($"❌ Proxmox: {detail}");
            PveLog("EXCEPTION", ex.ToString());
        }
    }

    private static void PveLog(string tag, string msg)
    {
        try { System.IO.File.AppendAllText("/tmp/novascm_pve.log",
            $"[{DateTime.Now:HH:mm:ss}] [{tag}] {msg}\n"); } catch { }
    }

    private void BtnPveRefresh_Click(object? s, RoutedEventArgs e) => BtnPveConnect_Click(s, e);

    private async Task LoadPveVmsAsync(HttpClient http, string node)
    {
        await Dispatcher.UIThread.InvokeAsync(() => _pveRows.Clear());
        var rows = new List<PveRow>();
        try
        {
            var vmResp = await http.GetAsync($"{PveBase}/nodes/{node}/qemu");
            var vmJson = await vmResp.Content.ReadAsStringAsync();
            PveLog("QEMU", $"status={vmResp.StatusCode} len={vmJson.Length}");
            var vmDoc  = JsonDocument.Parse(vmJson);
            foreach (var vm in vmDoc.RootElement.GetProperty("data").EnumerateArray())
            {
                var row = new PveRow
                {
                    Type   = "VM",
                    VmId   = vm.TryGetProperty("vmid",   out var vid) ? vid.GetInt32()   : 0,
                    Name   = vm.TryGetProperty("name",   out var vn)  ? vn.GetString() ?? "—" : "—",
                    Status = vm.TryGetProperty("status", out var vs)  ? vs.GetString() ?? "—" : "—",
                    CpuPct = vm.TryGetProperty("cpu",    out var cpu) ? $"{cpu.GetDouble()*100:0.0}%" : "—",
                    Uptime = vm.TryGetProperty("uptime", out var up)  ? FormatUptime(up.GetInt64()) : "—",
                };
                if (vm.TryGetProperty("mem", out var mem) && vm.TryGetProperty("maxmem", out var maxm))
                    row.MemInfo = $"{mem.GetInt64()/1024/1024}MB/{maxm.GetInt64()/1024/1024}MB";
                rows.Add(row);
            }
        }
        catch (Exception ex) { PveLog("QEMU_ERR", ex.Message); }
        try
        {
            var ctResp = await http.GetAsync($"{PveBase}/nodes/{node}/lxc");
            var ctJson = await ctResp.Content.ReadAsStringAsync();
            PveLog("LXC", $"status={ctResp.StatusCode} len={ctJson.Length}");
            var ctDoc  = JsonDocument.Parse(ctJson);
            foreach (var ct in ctDoc.RootElement.GetProperty("data").EnumerateArray())
            {
                var row = new PveRow
                {
                    Type   = "CT",
                    VmId   = ct.TryGetProperty("vmid",   out var vid) ? vid.GetInt32()   : 0,
                    Name   = ct.TryGetProperty("name",   out var vn)  ? vn.GetString() ?? "—" : "—",
                    Status = ct.TryGetProperty("status", out var vs)  ? vs.GetString() ?? "—" : "—",
                    CpuPct = ct.TryGetProperty("cpu",    out var cpu) ? $"{cpu.GetDouble()*100:0.0}%" : "—",
                    Uptime = ct.TryGetProperty("uptime", out var up)  ? FormatUptime(up.GetInt64()) : "—",
                };
                if (ct.TryGetProperty("mem", out var mem) && ct.TryGetProperty("maxmem", out var maxm))
                    row.MemInfo = $"{mem.GetInt64()/1024/1024}MB/{maxm.GetInt64()/1024/1024}MB";
                rows.Add(row);
            }
        }
        catch (Exception ex) { PveLog("LXC_ERR", ex.Message); }
        await Dispatcher.UIThread.InvokeAsync(() => {
            foreach (var r in rows) _pveRows.Add(r);
            TxtPveNodeInfo.Text = $"{_pveRows.Count} VM/CT";
        });
        PveLog("LOADED", $"{rows.Count} righe");
    }

    private static string FormatUptime(long seconds)
    {
        if (seconds <= 0) return "—";
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
        if (ts.TotalHours >= 1) return $"{ts.Hours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }

    private void PveGrid_SelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (PveGrid.SelectedItem is PveRow row)
        {
            PveDetailGrid.IsVisible  = true;
            TxtPveDetailName.Text    = row.Name;
            TxtPveVmid.Text          = row.VmId.ToString();
            TxtPveType.Text          = row.Type;
            TxtPveStatus.Text        = row.Status;
            TxtPveCpu.Text           = row.CpuPct;
            TxtPveMem.Text           = row.MemInfo;
            TxtPveUptime.Text        = row.Uptime;
            BtnPveStart.IsEnabled   = row.Status == "stopped";
            BtnPveStop.IsEnabled    = row.Status == "running";
            BtnPveRestart.IsEnabled = row.Status == "running";
        }
    }

    private async void BtnPveStart_Click(object? s, RoutedEventArgs e)
    {
        if (PveGrid.SelectedItem is PveRow row) await PveActionAsync(row, "start");
    }

    private async void BtnPveStop_Click(object? s, RoutedEventArgs e)
    {
        if (PveGrid.SelectedItem is PveRow row) await PveActionAsync(row, "stop");
    }

    private async void BtnPveRestart_Click(object? s, RoutedEventArgs e)
    {
        if (PveGrid.SelectedItem is PveRow row) await PveActionAsync(row, "reboot");
    }

    private async Task PveActionAsync(PveRow row, string action)
    {
        try
        {
            // NEW-C: accetta solo self-signed (UntrustedRoot); blocca scaduti, revocati, hostname errati
            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, chain, errors) =>
                    errors == System.Net.Security.SslPolicyErrors.None ||
                    (errors == System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors &&
                     chain?.ChainStatus.All(s => s.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot) == true)
            };
            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            var token = TxtPveToken.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(token))
                http.DefaultRequestHeaders.Add("Authorization", $"PVEAPIToken={token}");
            else
            {
                http.DefaultRequestHeaders.Add("Cookie", $"PVEAuthCookie={_pveTicket}");
                http.DefaultRequestHeaders.Add("CSRFPreventionToken", _pveCsrf);
            }
            var nodeVal = TxtPveNode.Text ?? "pve";
            var ep = row.Type == "CT"
                ? $"{PveBase}/nodes/{nodeVal}/lxc/{row.VmId}/status/{action}"
                : $"{PveBase}/nodes/{nodeVal}/qemu/{row.VmId}/status/{action}";
            await http.PostAsync(ep, new StringContent(""));
            SetStatus($"✅ Azione '{action}' inviata a {row.Name}");
        }
        catch (Exception ex) { SetStatus($"❌ Errore: {ex.Message}"); }
    }

    // ── Script ────────────────────────────────────────────────────────────────
    private void LoadScripts()
    {
        _scripts.Clear();
        Directory.CreateDirectory(ScriptsPath);
        foreach (var f in Directory.GetFiles(ScriptsPath, "*.sh")
                         .Concat(Directory.GetFiles(ScriptsPath, "*.ps1")))
        {
            _scripts.Add(new ScriptRow
            {
                Name    = Path.GetFileNameWithoutExtension(f),
                Content = File.ReadAllText(f),
            });
        }
    }

    private void LstScripts_SelectionChanged(object? s, SelectionChangedEventArgs e)
    {
        if (LstScripts.SelectedItem is ScriptRow script)
        {
            TxtScriptName.Text   = script.Name;
            TxtScriptEditor.Text = script.Content;
        }
    }

    private void BtnScriptNew_Click(object? s, RoutedEventArgs e)
    {
        TxtScriptName.Text   = "nuovo-script";
        TxtScriptEditor.Text = App.Platform.IsWindows ? "# PowerShell script\n" : "#!/bin/bash\n";
        TxtScriptOutput.Text = "";
    }

    private void BtnScriptSave_Click(object? s, RoutedEventArgs e)
    {
        var name = TxtScriptName.Text?.Trim() ?? "script";
        var ext  = App.Platform.IsWindows ? ".ps1" : ".sh";
        Directory.CreateDirectory(ScriptsPath);
        File.WriteAllText(Path.Combine(ScriptsPath, name + ext), TxtScriptEditor.Text ?? "");
        LoadScripts();
        SetStatus($"💾 Script '{name}' salvato.");
    }

    private void BtnScriptDelete_Click(object? s, RoutedEventArgs e)
    {
        if (LstScripts.SelectedItem is ScriptRow script)
        {
            foreach (var ext in new[] { ".sh", ".ps1" })
            {
                var path = Path.Combine(ScriptsPath, script.Name + ext);
                if (File.Exists(path)) File.Delete(path);
            }
            _scripts.Remove(script);
            SetStatus($"✕ Script '{script.Name}' eliminato.");
        }
    }

    private async void BtnScriptRun_Click(object? s, RoutedEventArgs e)
    {
        var content = TxtScriptEditor.Text ?? "";
        if (string.IsNullOrWhiteSpace(content)) return;
        TxtScriptOutput.Text = "▶ Esecuzione in corso...\n";
        try
        {
            var tempExt  = App.Platform.IsWindows ? ".ps1" : ".sh";
            var tempFile = Path.Combine(Path.GetTempPath(), $"novascm_script{tempExt}");
            File.WriteAllText(tempFile, content);
            if (!App.Platform.IsWindows)
            {
                var chmod = Process.Start(new ProcessStartInfo("chmod", $"+x {tempFile}")
                    { UseShellExecute = false, CreateNoWindow = true });
                chmod?.WaitForExit();
            }
            var psi = new ProcessStartInfo(App.Platform.ShellName, App.Platform.ShellArgs(tempFile))
            {
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };
            var proc = Process.Start(psi)!;
            var output = await proc.StandardOutput.ReadToEndAsync();
            var errors = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            TxtScriptOutput.Text = output + (errors.Length > 0 ? "\n[STDERR]\n" + errors : "");
        }
        catch (Exception ex) { TxtScriptOutput.Text = $"❌ Errore: {ex.Message}"; }
    }

    // ── Certificati ───────────────────────────────────────────────────────────
    private void BtnCertRefresh_Click(object? s, RoutedEventArgs e)
    {
        _certRows.Clear();
        foreach (var c in Database.GetCerts())
            _certRows.Add(c);
        SetStatus($"↺ {_certRows.Count} certificati caricati.");
    }

    private async void BtnCertRevoke_Click(object? s, RoutedEventArgs e)
    {
        if (CertGrid.SelectedItem is CertRow cert)
        {
            Database.DeleteCert(cert.Mac);
            _certRows.Remove(cert);
            SetStatus($"✕ Certificato '{cert.Name}' revocato.");
        }
        else
        {
            await MessageBoxHelper.ShowInfo("Seleziona un certificato da revocare.", this);
        }
    }

    // ── Utility ───────────────────────────────────────────────────────────────
    private void SetStatus(string msg)
        => Dispatcher.UIThread.Post(() => TxtGlobalStatus.Text = msg);

    private static List<IPAddress> GetHostsInSubnet(IPAddress network, int cidr)
    {
        var bytes = network.GetAddressBytes();
        if (bytes.Length != 4) throw new ArgumentException("Solo IPv4 supportato");
        uint baseAddr  = (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
        uint mask      = cidr == 0 ? 0 : 0xFFFFFFFF << (32 - cidr);
        uint netAddr   = baseAddr & mask;
        uint broadcast = netAddr | ~mask;
        var list = new List<IPAddress>();
        for (uint i = netAddr + 1; i < broadcast; i++)
            list.Add(new IPAddress(new byte[] {
                (byte)(i >> 24), (byte)((i >> 16) & 0xFF), (byte)((i >> 8) & 0xFF), (byte)(i & 0xFF)
            }));
        return list;
    }

    private static async Task<bool> QuickPortOpenAsync(string ip, int port, int timeoutMs)
    {
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await tcp.ConnectAsync(ip, port, cts.Token);
            return true;
        }
        catch { return false; }
    }

    private static string GetLocalMacForIp(string targetIp)
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                        ua.Address.ToString() == targetIp)
                    {
                        var phy = ni.GetPhysicalAddress().GetAddressBytes();
                        if (phy.Length == 6)
                            return string.Join(":", phy.Select(b => b.ToString("X2")));
                    }
        }
        catch { }
        return "—";
    }

    // ── OUI / Vendor lookup ───────────────────────────────────────────────────
    private static async Task LoadOuiDatabaseAsync()
    {
        if (_ouiLoaded) return;
        try
        {
            var localPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NovaSCM", "oui.json");
            if (File.Exists(localPath))
            {
                var records = JsonSerializer.Deserialize<List<OuiRecord>>(await File.ReadAllTextAsync(localPath));
                if (records != null)
                {
                    foreach (var r in records)
                        _ouiDb[r.MacPrefix.ToUpper()] = r.VendorName;
                    _ouiLoaded = true;
                }
            }
        }
        catch { }
    }

    private static string LookupVendor(string mac)
    {
        if (mac == "—" || mac.Length < 8) return "—";
        var prefix = mac.Replace(":", "").Replace("-", "").ToUpper();
        foreach (var len in new[] { 10, 9, 8, 7, 6 })
            if (prefix.Length >= len && _ouiDb.TryGetValue(prefix[..len], out var v))
                return v;
        return "—";
    }

    private static async Task<string> LookupVendorOnlineAsync(string mac)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var clean = mac.Replace(":", "").Replace("-", "")[..6];
            var resp  = await http.GetStringAsync($"https://api.maclookup.app/v2/macs/{clean}?apiKey=free");
            var doc   = JsonDocument.Parse(resp);
            if (doc.RootElement.TryGetProperty("company", out var co))
            {
                var name = co.GetString() ?? "";
                if (name.Length > 0 && name != "not found") return name;
            }
        }
        catch { }
        return "—";
    }
}
