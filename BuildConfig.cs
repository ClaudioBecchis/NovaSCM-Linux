namespace NovaSCM;

/// <summary>Edizione dell'app — controlla quali tab/funzionalità sono visibili</summary>
public enum AppEdition { Lite, Medium, Full }

public static class BuildConfig
{
#if EDITION_LITE
    public const AppEdition Edition = AppEdition.Lite;
#elif EDITION_MEDIUM
    public const AppEdition Edition = AppEdition.Medium;
#else
    public const AppEdition Edition = AppEdition.Full;
#endif

    // Feature flags derivati dall'edizione
    public static bool HasNetwork    => true;                              // sempre
    public static bool HasPcFleet    => Edition >= AppEdition.Medium;
    public static bool HasProxmox    => Edition >= AppEdition.Medium;
    public static bool HasScripts    => Edition >= AppEdition.Medium;
    public static bool HasWol        => Edition >= AppEdition.Medium;
    public static bool HasSsh        => Edition >= AppEdition.Medium;
    public static bool HasTraceroute => Edition >= AppEdition.Medium;
    public static bool HasSpeedTest  => Edition >= AppEdition.Medium;
    public static bool HasMdns       => Edition >= AppEdition.Medium;
    public static bool HasDeploy     => Edition == AppEdition.Full;
    public static bool HasSccm       => Edition == AppEdition.Full;
    public static bool HasOpsi       => Edition == AppEdition.Full;
    public static bool HasWorkflow   => Edition == AppEdition.Full;
    public static bool HasCerts      => Edition == AppEdition.Full;
    public static bool HasChangeReq  => Edition == AppEdition.Full;
}
