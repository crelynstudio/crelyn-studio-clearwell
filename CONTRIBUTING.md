# Contributing / Katkı

Use Windows x64 and the SDK pinned in global.json. Restore locked dependencies and run the README build/check commands with a new empty fixture directory. Never test deletion against personal files or mutate the host's settings for automated checks. Live integration tests belong in snapshot-backed disposable VMs.

Open an issue describing the problem and a reproducible example, then submit a focused pull request with the change, validation actually run and remaining limits. Preserve both English/Turkish text, keyboard access, file identity checks, local privacy and recovery-before-mutation ordering. Do not add network services, telemetry, credentials or dependencies without a concrete need. Do not upload artifacts/bin/obj, local profiles or screenshots containing private data. Report security problems privately as described in SECURITY.md. Contributions use the project's MIT license; brand rights remain separate.

## Türkçe

Windows x64 ve global.json'daki SDK'yı kullanın. Kilitli bağımlılıkları geri yükleyip README'deki derleme/test komutlarını yeni/boş test klasörüyle çalıştırın. Kişisel dosyalarda silme veya otomatik test için ana cihazda ayar değişikliği yapmayın. Canlı entegrasyon, anlık görüntülü gözden çıkarılabilir VM'de yapılır.

Sorun ve tekrarlanabilir örnekle issue açın; odaklı PR'da değişikliği, gerçekten yapılan testleri ve sınırları açıklayın. İki dili, klavye erişimini, dosya kimliği korumasını, yerel gizliliği ve değişiklikten önce kurtarma kaydını koruyun. Somut ihtiyaç olmadan servis/telemetri/kimlik bilgisi/bağımlılık eklemeyin. artifacts/bin/obj, yerel profil veya özel veri gösteren görüntü yüklemeyin. Güvenlik sorunlarını SECURITY.md'ye göre özel bildirin. Katkılar MIT kapsamındadır; marka hakları ayrıdır.
