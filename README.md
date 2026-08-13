# CloudPad 1.0.0

CloudPad turns one Android phone into one low-latency Xbox 360-compatible controller for Windows. The phone connects directly to the IP you enter; there is no account, telemetry, cloud backend, UPnP, or port forwarding. LAN and Tailscale—including `100.64.0.0/10` addresses—work identically.

## 1. CloudPad nedir?

CloudPad consists of a native Kotlin/Jetpack Compose Android app and a .NET 8 WPF receiver. The receiver creates a real virtual Xbox 360/XInput controller through ViGEmBus; it does **not** emulate keyboard keys.

## 2. Nasıl çalışır?

The phone pairs over TCP using the six-digit PIN shown by the receiver. A random in-memory session ID and 256-bit token then authenticate fixed-size binary UDP controller packets. Sequence numbers discard late packets. TCP heartbeat reports ping; if input stops for the configured 500 ms timeout, every button, stick, and trigger is immediately reset. The newest sampled state is sent directly at 30/60/120 Hz, so no stale packet queue grows.

## 3. Gereksinimler

- Windows 10/11 x64 (physical PC or Azure Windows 11 VM)
- Android 6.0/API 23 or newer
- [Tailscale](https://tailscale.com/download) on both devices for remote use
- [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases) on Windows
- To build: Visual Studio 2022/.NET 8 SDK; Android Studio with JDK 17 and Android SDK 35

ViGEmBus is an archived project, but its signed driver remains the backend supported by the current Nefarius client package. Install only its official signed release. The driver/installer is deliberately not bundled here.

## 4. Windows Receiver kurulumu

For a ready self-contained build, open PowerShell in the repository and run:

```powershell
.\scripts\publish-windows.ps1
```

This restores NuGet dependencies and creates `dist\windows\CloudPadReceiver.exe`; the target PC does not need a separate .NET runtime. Alternatively open `windows-receiver\CloudPad.slnx` in Visual Studio and build Release/x64. Start the EXE, keep the generated PIN visible, and leave **Auto start server** enabled. Settings/logs live under `%LocalAppData%\CloudPad`, never Program Files.

## 5. Sanal Xbox controller driver kurulumu

Download the latest official ViGEmBus installer from the [Nefarius ViGEmBus releases page](https://github.com/nefarius/ViGEmBus/releases), verify that the publisher is Nefarius Software Solutions, install it as administrator, then restart Windows if requested. Receiver shows “Xbox virtual controller driver could not be found” and a **Setup Instructions** button when unavailable.

Third-party dependency: `Nefarius.ViGEm.Client` (NuGet, MIT license) talks to ViGEmBus; ViGEmBus is BSD-3-Clause. Sources are the official [client](https://github.com/nefarius/ViGEmClient) and [driver](https://github.com/nefarius/ViGEmBus) repositories. Android dependencies are AndroidX/Compose (Apache-2.0), Kotlin/coroutines (Apache-2.0), and JUnit (EPL-1.0).

## 6. Windows Firewall ayarı

Run PowerShell as administrator:

```powershell
.\scripts\Add-FirewallRule.ps1 -Port 26760
```

This adds only inbound TCP and UDP rules for port 26760 on the Private profile. Do **not** disable Windows Defender Firewall. If your Tailscale adapter is classified Public, scope an equivalent rule to the Tailscale interface/address after reviewing your tailnet ACLs; never expose this port through a router.

## 7. Tailscale ile kullanım

Install and sign in to the same tailnet on phone and Windows. In Receiver, find **Tailscale IPv4** (`100.x.x.x`). Test the VM from another tailnet device with `tailscale ping 100.x.x.x`. CloudPad needs TCP and UDP 26760 allowed by Windows Firewall and Tailscale ACL/grants. LAN discovery is not used or required.

## 8. Android uygulamasını kurma

Open `android` in Android Studio, let Gradle sync, connect a phone, and Run. From a shell in `android`:

```bash
./gradlew assembleDebug
```

APK: `android/app/build/outputs/apk/debug/app-debug.apk`. For a signed production APK/AAB, create a keystore in Android Studio, configure signing locally (do not commit secrets), then run `./gradlew assembleRelease` or `bundleRelease`. Unsigned release output is under `app/build/outputs`.

## 9. Telefonu VM’ye bağlama

Open Receiver on the VM. In CloudPad enter the Receiver’s displayed Tailscale IPv4, port `26760`, and current six-digit PIN; press **CONNECT**. The landscape gamepad opens. IP/port persist; PIN deliberately does not. Auto reconnect backs off 1, 2, 3, then 5 seconds. Android clears state when backgrounded and keeps the screen awake only on the gamepad page.

Primary Azure example: phone `100.80.10.5`, VM `100.70.20.10`; enter `100.70.20.10` in **Server IP**. Mobile data versus Wi-Fi does not matter as long as Tailscale can reach the VM.

## 10. joy.cpl ile test

Press Win+R, type `joy.cpl`, press Enter. Expect **Xbox 360 Controller for Windows**. Open Properties and move/press controls; Receiver’s built-in tester also displays axes, triggers, and button state. Without Android, run `dotnet run --project windows-receiver/CloudPad.TestClient -- 127.0.0.1 PIN 26760`.

## 11. Steam’de test

Open Steam Settings → Controller and verify the Xbox controller appears. Start Steam after Receiver/virtual pad when a game only scans devices at launch. Avoid enabling mappings that duplicate XInput. RDP can hide controllers from some games; run the game and Receiver in the same interactive Windows session.

## 12. Oyun görmüyorsa troubleshooting

- Confirm the device exists in `joy.cpl`; if not, reinstall ViGEmBus and reboot.
- Restart the game after connecting CloudPad.
- Stop controller remappers that may hide or duplicate devices.
- Confirm game supports XInput and its controller input is enabled.
- In Azure, do not depend on an RDP-redirected controller: CloudPad creates the device inside the VM session.

## 13. Connection timeout

The default 500 ms timeout is intentionally conservative: after no valid UDP packet, Receiver submits a neutral report. Increase it in Settings only on very unstable links. “Poor connection” means RTT above 150 ms. Check Tailscale path (`tailscale status` may show relay/DERP), Wi-Fi quality, VM region, and firewall. TCP handshake working does not prove UDP is allowed.

## 14. Driver troubleshooting

Use only the official release link above. Check Apps & Features for ViGEm Bus Driver and Device Manager → System devices. Reboot after install/update. Logs at `%LocalAppData%\CloudPad\receiver.log` contain technical errors but never PINs/tokens. Generate a new PIN to invalidate convenient reuse; restarting Receiver invalidates every session.

CloudPad does not install a custom kernel driver and does not use vJoy. An old **Virtual Xbox 360 Controller** showing Code 28 is not created by the current receiver; uninstall that stale device from Device Manager (select **Delete the driver software** only if it belongs to the abandoned custom implementation), then rescan hardware. Do not remove **Nefarius Virtual Gamepad Emulation Bus** / ViGEmBus under System devices, because that is the signed backend used by this receiver.

## 15. Azure VM kullanım notları

Use Windows 11 with an interactive logged-in session, install Tailscale and ViGEmBus, then add the scoped firewall rule. No Azure public IP inbound/NAT rule is required for CloudPad. Do not expose 26760 in an Azure NSG. Minimizing closes the window to the system tray while the receiver continues; an RDP disconnect normally leaves the logged-in session alive. A sign-out or VM deallocation stops it. **Start minimized** and **Start CloudPad with Windows** are reversible per-user settings.

## Repository layout

- `android/` — native Compose app, networking, settings, gamepad UI, tests
- `windows-receiver/CloudPad.Receiver/` — WPF receiver/tray/Xbox backend
- `windows-receiver/CloudPad.Protocol/` — binary protocol and session validation
- `windows-receiver/CloudPad.TestClient/` — real packet simulator
- `windows-receiver/CloudPad.Tests/` — protocol/axis/sequence/timeout/session tests
- `docs/protocol.md` — exact protocol v1 specification
- `scripts/` — firewall and self-contained publish scripts

Security note: the PIN prevents unsolicited control but the control channel is not encrypted. CloudPad is designed for a trusted LAN or private Tailscale tailnet, whose WireGuard encryption protects traffic in transit. Do not forward its port to the public Internet.
