# Signing the build

The release workflow signs the published files and the installer **when a
certificate is configured**, and skips the step otherwise. Nothing else has to
change: an unsigned tag still produces a working, downloadable package.

## What signing does, and what it does not

| | |
|---|---|
| **It proves the file was not altered after the build.** | Anyone can check the signature against the certificate. This is the part that matters most for a download. |
| **It builds reputation with SmartScreen over time.** | SmartScreen still decides by download volume as much as by publisher; a brand-new certificate is unknown at first. |
| **It does *not* by itself satisfy Smart App Control.** | Smart App Control judges by reputation as well. A freshly signed build from an unknown publisher can still be blocked. |

So: signing makes the release verifiable and lets the warning fade. It is not
a switch that turns the warning off on the day it is added.

For **local development** it is the wrong tool anyway — every rebuild produces a
new hash, which Smart App Control has never seen. Either build into a folder
Smart App Control tolerates, or turn the feature off:

```powershell
# State of the feature (1 = on)
Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy' |
    Select-Object VerifiedAndReputablePolicyState
```

Turning Smart App Control off is permanent until Windows is reinstalled. That is
a decision for the machine's owner, not for this repository.

## What to obtain

Since June 2023 the CA/Browser Forum rules require the private key of an
OV or EV code-signing certificate to live in hardware. Most authorities
therefore no longer hand out a `.pfx` at all. That leaves three realistic
routes:

1. **Azure Trusted Signing** — Microsoft's cloud signing, roughly 10 USD per
   month, available to individuals. The key never leaves the service, and it
   works from GitHub Actions with a federated credential. This is the cheapest
   route that produces a publicly trusted signature.
2. **An open-source programme** — [SignPath](https://signpath.io/) signs open
   source projects for free; the private key stays with them.
3. **A certificate you already own**, exported as `.pfx` (test certificates from
   a lab, or an older OV certificate issued before the rule changed).

A **self-signed** certificate is possible here and is useful for testing the
signing step itself — it will not remove any warning on a machine that does not
already trust it.

## Wiring it up (`.pfx` route)

Add two repository secrets under *Settings → Secrets and variables → Actions*:

| Secret | Content |
|---|---|
| `SIGN_PFX_BASE64` | the `.pfx` file, base64-encoded (below) |
| `SIGN_PFX_PASSWORD` | its password |

```powershell
[Convert]::ToBase64String([IO.File]::ReadAllBytes('.\zertifikat.pfx')) |
    Set-Clipboard
```

The workflow then signs `SyncTClient.exe`, `synctexplorer.dll` and, after
packaging, the installer itself — the checksum is computed **after** signing, so
the published value always matches the downloadable file.

## Wiring it up (Azure Trusted Signing)

Not implemented yet. It needs a service principal, the signing account and
certificate profile, and a step that calls `azure/trusted-signing-action`
instead of `signtool`. The packaging order stays the same: sign the published
files, then package, then sign the package, then hash.

## Checking a download

```powershell
Get-FileHash .\SyncTClient-*-setup.exe -Algorithm SHA256
```

Compare with the `SyncTClient-*.exe.sha256` attached to the release. Once
signatures are in place, the same file can be checked with

```powershell
Get-AuthenticodeSignature .\SyncTClient-*-setup.exe
```
