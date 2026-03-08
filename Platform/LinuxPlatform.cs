using System.Diagnostics;

namespace NovaSCM.Platform;

public class LinuxPlatform : IPlatform
{
    public string Name => "Linux";
    public bool IsWindows => false;
    public string ShellName => "bash";
    // Passa il file direttamente a bash (nessun -c, nessun rischio di injection)
    public string ShellArgs(string scriptFile) => scriptFile;
    public string GetDefaultSshKeyPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_ed25519");

    public string GetArpTable()
    {
        if (File.Exists("/proc/net/arp"))
            return File.ReadAllText("/proc/net/arp");
        var p = Process.Start(new ProcessStartInfo("arp", "-n")
            { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
        var out_ = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return out_;
    }

    public string GetMacFromArp(string ip)
    {
        var arp = GetArpTable();
        foreach (var line in arp.Split('\n'))
        {
            // Usa regex per evitare match parziali (es. "192.168.1.1" dentro "192.168.1.10")
            if (!System.Text.RegularExpressions.Regex.IsMatch(line,
                $@"(?<![0-9.]){System.Text.RegularExpressions.Regex.Escape(ip)}(?![0-9.])")) continue;
            var m = System.Text.RegularExpressions.Regex.Match(line,
                @"([0-9a-f]{2}[:\-]){5}[0-9a-f]{2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success) return m.Value.ToUpper();
        }
        return "—";
    }

    public void OpenRdp(string ip)
    {
        // Prova xfreerdp, poi remmina
        try { Process.Start(new ProcessStartInfo("xfreerdp3", $"/v:{ip} /dynamic-resolution") { UseShellExecute = true }); }
        catch { Process.Start(new ProcessStartInfo("remmina", $"-c rdp://{ip}") { UseShellExecute = true }); }
    }

    public void OpenSsh(string ip, string user)
    {
        var cmd = $"ssh {user}@{ip}";
        // Prova terminali comuni
        foreach (var term in new[] { "gnome-terminal", "xterm", "konsole", "xfce4-terminal" })
        {
            try
            {
                var args = term == "gnome-terminal" ? $"-- bash -c \"{cmd}; read\"" : $"-e \"{cmd}\"";
                Process.Start(new ProcessStartInfo(term, args) { UseShellExecute = true });
                return;
            }
            catch { }
        }
    }
}
