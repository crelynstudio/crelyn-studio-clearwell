# Clearwell 0.3.0-preview.1

**Unsigned Windows x64 development preview — not a stable release.**

Download **Clearwell-0.3.0-preview.1-win-x64.zip** and its **.zip.sha256** from Assets. Close the old app, extract into a new folder and open Clearwell.exe. .NET is included; keep the accompanying license/notice files. Do not disable Windows security.

### Changes

- Individual cleanup preview, filtering, file exclusions and CSV export.
- 1/7/30-day retention (default 7), skip reasons and extra confirmation for partial scans.
- Cancellable WinGet queues, process-tree termination where possible, exact package verification and uninstall-target review.
- Preferences repair independent of recovery, legacy history migration, undone-record archive and interrupted-action reconciliation.
- Local bounded activity/support export and optional current-user Store/MSIX inventory.
- Windows CI and verified ZIP/SHA-256 build tooling; all 112 catalog entries resolved in read-only source checks on 11 October 2026.

### Upgrade and limitations

Back up %LOCALAPPDATA%\CrelynStudio\Clearwell first. New history uses schema 1; do not downgrade to 0.2 with an updated profile. Cleanup is permanent: no quarantine or undo. Detached/elevated installers may continue after cancellation. Inventory verification can fail when sources are unavailable. Windows 10 VM/live mutation, real screen-reader, physical mixed-DPI/high-contrast tests and code signing remain pending. ARM64/x86 packages are not supplied.

Verify with `Get-FileHash .\Clearwell-0.3.0-preview.1-win-x64.zip -Algorithm SHA256` and compare with .zip.sha256. A hash is not a digital signature. The provenance JSON is an unsigned build record. GitHub's Source code archives are source, not portable application packages.

[Source and instructions](https://github.com/crelynstudio/crelyn-studio-clearwell) · [Changes](https://github.com/crelynstudio/crelyn-studio-clearwell/blob/main/CHANGELOG.md) · [Audit status](https://github.com/crelynstudio/crelyn-studio-clearwell/blob/main/docs/AUDIT-2026-10.md)

---

## Türkçe

**İmzasız Windows x64 geliştirme önizlemesi — kararlı sürüm değildir.**

Assets'ten **Clearwell-0.3.0-preview.1-win-x64.zip** ve **.zip.sha256** indirin. Eski uygulamayı kapatın, yeni klasöre açın ve Clearwell.exe çalıştırın. .NET dahildir; tüm lisans/bildirim dosyalarını koruyun. Windows güvenliğini kapatmayın.

- Tekil temizlik önizlemesi, filtre, dosya hariç tutma ve CSV aktarımı.
- 1/7/30 gün koruma (varsayılan 7), atlama nedenleri ve kısmi taramada ek onay.
- İptal edilebilir WinGet sırası, mümkün olan süreç ağacını durdurma, tam paket doğrulama ve kaldırma hedefi inceleme.
- Kurtarmadan bağımsız tercih onarımı, eski geçmiş geçişi, geri alınmış kayıt arşivi ve kesilen işlemi uzlaştırma.
- Sınırlı yerel etkinlik/destek raporu, isteğe bağlı geçerli kullanıcı Store/MSIX envanteri.
- Windows CI ve doğrulanan ZIP/hash araçları. 112 katalog kaydı 11 Ekim 2026'da salt okunur kaynak sorgusunda bulundu.

Önce %LOCALAPPDATA%\CrelynStudio\Clearwell klasörünü yedekleyin. Yeni geçmiş şema 1 kullanır; güncellenmiş profille 0.2'ye dönmeyin. Silme kalıcıdır; karantina/geri alma yoktur. Ayrılmış/yükseltilmiş kurucu iptalden sonra devam edebilir. Kaynak erişilemezse envanter doğrulanamayabilir. Windows 10 VM/canlı değişiklik, gerçek ekran okuyucu/fiziksel karma DPI/yüksek kontrast testleri ve kod imzası bekler. ARM64/x86 paketi yoktur.

Yukarıdaki Get-FileHash komutuyla ZIP'i .zip.sha256 ile karşılaştırın. Hash dijital imza değildir; provenance imzasız derleme kaydıdır. GitHub Source code arşivleri uygulama paketi değildir.

Crelyn Studio tarafından geliştirildi / Developed by Crelyn Studio · https://crelynstudio.com
