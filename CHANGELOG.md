# Changes / Değişiklikler

## 0.3.0-preview.1 — 2026-10-11

Development preview. No stable-release claim; real Windows mutation and accessibility validation remains pending.

### Added

- Individual cleanup review, file exclusions, filtering and CSV export.
- 1/7/30-day retention (default 7), explicit partial-scan confirmation and skip-reason counters.
- WinGet cancellation/tree termination, exact inventory verification and uninstall-target review.
- Optional current-user Appx/MSIX inventory; absolute PATH fallback for WinGet.
- Versioned recovery history accepting legacy arrays; independent preferences repair, undone-record archive and paged history.
- Latest-related-record undo guard and interrupted/already-restored action reconciliation.
- Local 200-operation activity and privacy-limited support export.
- Windows CI, read-only catalog health, and verified ZIP/checksum/provenance build tooling.
- Native high-contrast color fallback and status live-region metadata.

### Fixed

- Uncancellable installation UI and parent-only timeout termination.
- Preferences corruption disabling valid recovery data; arbitrary 10,000-record lockout.
- Nonstandard registry types silently converted during new changes.
- Inconsistent version strings: build, caption, About and package names now share Directory.Build.props.

### Upgrade

Back up the local profile first. New recovery writes use schema 1; do not downgrade to 0.2 with this updated profile. Deletion remains permanent, without quarantine or undo. Detached/elevated installers may survive cancellation. ZIP/EXE remain unsigned.

### Türkçe

Geliştirme önizlemesi; kararlı sürüm değildir. Gerçek Windows değişikliği ve erişilebilirlik doğrulaması bekler.

- Tekil dosya inceleme/hariç tutma/filtre/CSV; 1/7/30 gün koruma (varsayılan 7), kısmi taramada ek onay ve atlama nedenleri.
- WinGet iptal/süreç ağacı durdurma, tam envanter doğrulama ve kaldırma hedefi inceleme; geçerli kullanıcı Appx/MSIX ve mutlak PATH keşfi.
- Eski dizileri okuyabilen sürümlü kurtarma geçmişi, bağımsız tercih onarımı, geri alınmış kayıt arşivi ve sayfalama.
- İlgili son kaydı önce geri alma koruması, kesilen/zaten dönmüş işlemi uzlaştırma, 200 işlemli yerel etkinlik/destek aktarımı.
- Windows CI, salt okunur katalog kontrolü, doğrulanan ZIP/hash/provenance; yüksek kontrast renkleri ve durum erişilebilirlik bilgisi.
- Tercih bozulmasıyla kurtarma kilitlenmesi, 10.000 kayıt engeli ve yeni değişiklikte standart dışı kayıt türü dönüştürmesi düzeltildi. Sürüm tek kaynaktan okunur.

Önce yerel profili yedekleyin. Yeni geçmiş şema 1 kullanır; aynı profille 0.2'ye dönmeyin. Silme kalıcıdır; karantina/geri alma yoktur. Ayrılmış/yükseltilmiş kurucu iptal sonrası devam edebilir. ZIP/EXE imzasızdır.

## 0.2.0-preview.1

Earlier preview: native overview, bilingual appearances, app catalog/search, reviewed cleanup, Windows preferences, power plans and local recovery. The historic public tag v0.2.0 is a preview despite its abbreviated name; this file does not rewrite published history.

Önceki önizleme: yerel sistem özeti, iki dil/tema, uygulama kataloğu/arama, incelenebilir temizlik, Windows tercihleri, güç planları ve yerel kurtarma. Eski v0.2.0 etiketi kısa adına rağmen önizlemedir; yayımlanmış geçmiş yeniden yazılmaz.
