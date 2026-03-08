using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NovaSCM.Platform;

public class WindowsPlatform : IPlatform
{
    public string Name => "Windows";
    public bool IsWindows => true;
    public string ShellName => "powershell.exe";
    // -File invece di -Command: gestisce correttamente i percorsi senza rischio di injection
    public string ShellArgs(string scriptFile) => $"-ExecutionPolicy Bypass -File \"{scriptFile}\"";
    public string GetDefaultSshKeyPath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh", "id_ed25519");

    public string GetArpTable()
    {
        var p = Process.Start(new ProcessStartInfo("arp", "-a")
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
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
                if (System.Text.RegularExpressions.Regex.IsMatch(p, @"([0-9a-f]{2}[:\-]){5}[0-9a-f]{2}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    return p.Replace('-', ':').ToUpper();
        }
        return "—";
    }

    public void OpenRdp(string ip)
    {
        var startInfo = new ProcessStartInfo("mstsc") { UseShellExecute = true };
        startInfo.ArgumentList.Add("/v");
        startInfo.ArgumentList.Add(ip);
        Process.Start(startInfo);
    }

    public void OpenSsh(string ip, string user)
    {
        try
        {
            var startInfo = new ProcessStartInfo("wt.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add("ssh");
            startInfo.ArgumentList.Add($"{user}@{ip}");
            Process.Start(startInfo);
        }
        catch
        {
            var startInfo = new ProcessStartInfo("cmd.exe") { UseShellExecute = true };
            startInfo.ArgumentList.Add("/k");
            startInfo.ArgumentList.Add("ssh");
            startInfo.ArgumentList.Add($"{user}@{ip}");
            Process.Start(startInfo);
        }
    }
}
