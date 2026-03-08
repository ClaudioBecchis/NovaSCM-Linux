namespace NovaSCM.Platform;

/// <summary>Astrazione per operazioni OS-specifiche</summary>
public interface IPlatform
{
    string Name { get; }                        // "Windows" | "Linux" | "macOS"
    bool IsWindows { get; }
    string GetArpTable();                       // output arp -a o /proc/net/arp
    string GetMacFromArp(string ip);            // lookup MAC da ARP
    void OpenRdp(string ip);                    // mstsc o xfreerdp
    void OpenSsh(string ip, string user);       // wt/cmd o gnome-terminal/xterm
    string GetDefaultSshKeyPath();              // ~/.ssh/id_ed25519
    string ShellName { get; }                   // "powershell" | "bash"
    string ShellArgs(string script);            // wrappa script per esecuzione
}
