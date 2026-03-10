using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace NovaSCM;

// ── Modelli condivisi (Database + UI) ─────────────────────────────────────────

public class DeviceRow : INotifyPropertyChanged
{
    private string _status         = "🔍 Scansione...";
    private string _mac            = "—";
    private string _vendor         = "—";
    private string _name           = "—";
    private string _icon           = "❓";
    private string _deviceType     = "—";
    private string _connectionType = "❓";

    public string Ip         { get; set; } = "";
    public string CertStatus { get; set; } = "⬜ No";
    public bool   WasOnline  { get; set; } = false;

    public string Mac            { get => _mac;            set { _mac            = value; OnPC(); } }
    public string Vendor         { get => _vendor;         set { _vendor         = value; OnPC(); } }
    public string Name           { get => _name;           set { _name           = value; OnPC(); } }
    public string Status         { get => _status;         set { _status         = value; OnPC(); } }
    public string Icon           { get => _icon;           set { _icon           = value; OnPC(); } }
    public string DeviceType     { get => _deviceType;     set { _deviceType     = value; OnPC(); } }
    public string ConnectionType { get => _connectionType; set { _connectionType = value; OnPC(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPC([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public void DetectType(IEnumerable<int> openPorts)
    {
        var ports  = new HashSet<int>(openPorts);
        var vendor = Vendor.ToLowerInvariant();
        var name   = Name.ToLowerInvariant();

        if (ports.Contains(8006))                          { Icon = "🔷"; DeviceType = "Proxmox"; }
        else if (ports.Contains(8123))                     { Icon = "🏠"; DeviceType = "Home Assistant"; }
        else if (ports.Contains(3389))                     { Icon = "🖥️"; DeviceType = "Windows PC"; }
        else if (ports.Contains(22) && !ports.Contains(3389) &&
                 (ports.Contains(80) || ports.Contains(443) || ports.Contains(9000) || ports.Contains(9090)))
                                                           { Icon = "🐧"; DeviceType = "Linux Server"; }
        else if (ports.Contains(22))                       { Icon = "🐧"; DeviceType = "Linux"; }
        else if (vendor.Contains("ubiquiti") || vendor.Contains("tp-link") ||
                 name.Contains("router") || name.Contains("gateway") || name.Contains("ucg"))
                                                           { Icon = "🌐"; DeviceType = "Router / AP"; }
        else if (vendor.Contains("apple"))                 { Icon = "🍎"; DeviceType = "Apple"; }
        else if (vendor.Contains("oneplus") || vendor.Contains("samsung") ||
                 vendor.Contains("xiaomi") || vendor.Contains("oppo"))
                                                           { Icon = "📱"; DeviceType = "Mobile"; }
        else if (vendor.Contains("raspberry"))             { Icon = "🍓"; DeviceType = "Raspberry Pi"; }
        else if (ports.Contains(9100) || ports.Contains(515) || ports.Contains(631))
                                                           { Icon = "🖨️"; DeviceType = "Stampante"; }
        else if (ports.Contains(1883))                     { Icon = "🔌"; DeviceType = "IoT / MQTT"; }
        else if (ports.Count == 0)                         { Icon = "📡"; DeviceType = "IoT / Smart"; }
        else                                               { Icon = "❔"; DeviceType = "Sconosciuto"; }
    }
}

public record OuiRecord(
    [property: JsonPropertyName("macPrefix")]  string MacPrefix,
    [property: JsonPropertyName("vendorName")] string VendorName,
    [property: JsonPropertyName("private")]    bool   Private);

public record CertRow(string Icon, string Name, string Mac, string Created, string Expires, string Status);
public record AppQueueRow(string Pc, string Ip, string Mac, string Apps, string Status);
public record AppCatRow(string Category, string Items);
public record OpsiRow(string Name, string Version, string Status, string Updated);
public record PcRow(string Icon, string Name, string Ip, string Os, string Cpu, string Ram, string Status, string Agent);

public class WfRow : INotifyPropertyChanged
{
    private string _nome = "";
    private int    _stepCount;
    public int    Id          { get; set; }
    public string Nome        { get => _nome;      set { _nome      = value; OnPC(); } }
    public string Descrizione { get; set; } = "";
    public int    Versione    { get; set; }
    public int    StepCount   { get => _stepCount; set { _stepCount = value; OnPC(); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPC([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public class WfStepRow
{
    public int    Id         { get; set; }
    public int    WorkflowId { get; set; }
    public int    Ordine     { get; set; }
    public string Nome       { get; set; } = "";
    public string Tipo       { get; set; } = "";
    public string Parametri  { get; set; } = "{}";
    public string Platform   { get; set; } = "all";
    public string SuErrore   { get; set; } = "stop";
}

public class WfAssignRow : INotifyPropertyChanged
{
    private string _status   = "";
    private int    _progress;
    public int    Id           { get; set; }
    public string PcName       { get; set; } = "";
    public string WorkflowNome { get; set; } = "";
    public int    WorkflowId   { get; set; }
    public string Status       { get => _status;   set { _status   = value; OnPC(); } }
    public int    Progress     { get => _progress; set { _progress = value; OnPC(); } }
    public string ProgressText => $"{Progress}%";
    public string AssignedAt   { get; set; } = "";
    public string LastSeen     { get; set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPC([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public class CrRow
{
    public int    Id           { get; set; }
    public string PcName       { get; set; } = "";
    public string Domain       { get; set; } = "";
    public string Ou           { get; set; } = "";
    public string AssignedUser { get; set; } = "";
    public string Status       { get; set; } = "";
    public string CreatedAt    { get; set; } = "";
    public string Notes        { get; set; } = "";
    public string LastSeen     { get; set; } = "";
}

// Proxmox VM/CT row
public class PveRow : INotifyPropertyChanged
{
    private string _status = "—";
    public string Type     { get; set; } = "vm";
    public int    VmId     { get; set; }
    public string Name     { get; set; } = "—";
    public string Status   { get => _status; set { _status = value; OnPC(); } }
    public string CpuPct   { get; set; } = "—";
    public string MemInfo  { get; set; } = "—";
    public string DiskInfo { get; set; } = "—";
    public string Uptime   { get; set; } = "—";
    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPC([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

// Script row
public class ScriptRow
{
    public string Name    { get; set; } = "";
    public string Content { get; set; } = "";
    public string Target  { get; set; } = "";
    public override string ToString() => Name;
}

// App config
public class AppConfig
{
    public string CertportalUrl { get; set; } = "";
    public string UnifiUrl      { get; set; } = "";
    public string UnifiUser     { get; set; } = "admin";
    public string UnifiPass     { get; set; } = "";
    public string Ssid          { get; set; } = "NovaSCM-Secure";
    public string RadiusIp      { get; set; } = "";
    public string CertDays      { get; set; } = "3650";
    public string OrgName       { get; set; } = "";
    public string Domain        { get; set; } = "";
    public string ScanNetwork   { get; set; } = "192.168.1.0";
    public string ScanSubnet    { get; set; } = "24";
    public string AdminUser     { get; set; } = "";
    public string AdminPass     { get; set; } = "";
    public string ScanNetworks  { get; set; } = "192.168.1.0/24";
    public string NovaSCMApiUrl { get; set; } = "";
    public string NovaSCMApiKey { get; set; } = "";
    public string SshKeyPath    { get; set; } = "";
}

// Deploy config
public class DeployConfig
{
    public string WinEdition      { get; set; } = "Windows 11 Pro";
    public string WinEditionId    { get; set; } = "Professional";
    public string PcNameTemplate  { get; set; } = "PC-{MAC6}";
    public string Locale          { get; set; } = "it-IT";
    public string TimeZone        { get; set; } = "W. Europe Standard Time";
    public string AdminPassword   { get; set; } = "";
    public string UserName        { get; set; } = "";
    public string UserPassword    { get; set; } = "";
    public List<string> WingetPackages { get; set; } = [];
    public string ProductKey      { get; set; } = "";
    public bool   IncludeAgent    { get; set; } = true;
    public string ServerUrl       { get; set; } = "";
    public string NovaSCMCrApiUrl { get; set; } = "";
    public string NovaSCMApiKey   { get; set; } = "";
    public string PxeServerIp     { get; set; } = "";
    public string PxeServerPath   { get; set; } = "/srv/netboot/novascm/";
    public bool   UseMicrosoftAccount { get; set; } = false;
    public string DomainJoin      { get; set; } = "Workgroup";
    public string DomainName      { get; set; } = "";
    public string DomainUser      { get; set; } = "";
    public string DomainPassword  { get; set; } = "";
    public string DomainControllerIp { get; set; } = "";
    public string AzureTenantId   { get; set; } = "";
}
