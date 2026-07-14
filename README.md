# NovaSCM Linux

> Port **cross-platform (Linux/Windows)** del client NovaSCM, costruito con **Avalonia UI** e .NET.

NovaSCM è un fleet & deployment manager open source ispirato a Microsoft SCCM: scansione rete, gestione PC, deploy Windows zero-touch e certificati WiFi enterprise. Questo repository contiene il client desktop per Linux.

## Download

**[↓ Ultima release (linux-x64)](https://github.com/ClaudioBecchis/NovaSCM-Linux/releases/latest)**

```bash
unzip NovaSCM-v*-linux-x64.zip -d novascm && cd novascm
chmod +x NovaSCM && ./NovaSCM
```

Richiede glibc recente (Debian 12+/Ubuntu 22.04+) e `fontconfig` (`sudo apt install fontconfig` su installazioni minimali). Nessuna dipendenza .NET: il binario è self-contained.

📖 **[Guida completa all'installazione su Linux](https://novascm.polariscore.it/docs/installazione-linux.html)**

## Funzionalità

- 📡 Scansione rete multi-subnet con rilevamento vendor
- 🖥️ Gestione PC (inventario, WoL, RDP/SSH)
- 💿 Generazione `autounattend.xml` + `postinstall.ps1` per deploy Windows
- 🔐 Certificati WiFi EAP-TLS
- 🔗 Si collega allo stesso [server NovaSCM](https://github.com/ClaudioBecchis/NovaSCM/wiki/Installazione-Server) del client Windows

## Compilare da sorgente

```bash
git clone https://github.com/ClaudioBecchis/NovaSCM-Linux
cd NovaSCM-Linux
dotnet publish -c Release -r linux-x64 --self-contained true
```

## Progetto principale

Client Windows (WPF), server Flask, wiki e documentazione completa: **[ClaudioBecchis/NovaSCM](https://github.com/ClaudioBecchis/NovaSCM)** · [Docs](https://novascm.polariscore.it/docs/)

## License

MIT — © 2026 Claudio Becchis
