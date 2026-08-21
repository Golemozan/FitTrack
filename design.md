# FitTrack arayüz sistemi

FitTrack, koyu temalı kişisel bir performans günlüğüdür. Arayüzün ana işi günün
beslenme, antrenman ve toparlanma durumunu birkaç saniyede okutmak; ikinci işi ise
ayrıntılı kayda hızlı geçiş sağlamaktır. Portfolyoda güçlü görünmeli, günlük kullanımda
ise dekor uğruna bilgi saklamamalıdır.

- **Kitle:** Her gün verisini giren tek kullanıcı + ürün kararlarını inceleyen portfolyo ziyaretçisi.
- **Tür:** modern-minimal, koyu ve teknik-atletik varyant.
- **Macrostructure:** Performance Dashboard — sol araç rayı → günlük komuta alanı → asimetrik özet → Koç.
- **Eksenler:** dark / system-sans / multi-signal / low-motion.

## Yön

- Tek tema koyudur. Zemin saf siyah değil; yüzeyler gölgeyle değil açıklık farkıyla yükselir.
- Masaüstünde kalıcı sol araç rayı; mobilde kısa üst başlık ve beşli alt navigasyon vardır.
- Sayfanın bir baskın yüzeyi bulunur: günlük beslenme halkaları. Diğer parçalar aynı kartın
  kopyası gibi görünmez; kompakt, geniş veya şerit yoğunluğunda davranır.
- Tipografi sıkı ve sayısaldır. Büyük metriklerde tabular rakam, açıklamalarda kısa Türkçe kullanılır.
- Kart içinde kart, dekoratif ikon kutusu, gradyan, cam efekti ve renkli gölge yoktur.
- Yatay taşma kabul edilmez; metrik satırları `minmax(0, 1fr)` mantığıyla daralır.

## Günlük komuta alanı

Halkalar dıştan içe **Carb → Protein → Fat** sırasındadır. Üçünün de gerçek hedefi API'den
gelir. Günün kalori toplamı ve kalori hedefi halkaların hemen altında tek satırda görünür.
Set ilerlemesi bu alana girmez; yalnız Antrenman kartında tamamlanan/toplam set olarak okunur.

Makro legend'ı tekrar bir “Makrolar” kartında gösterilmez. Beslenme özeti, kaç öğün kaydı
olduğunu ve son kaydı gösteren eylem odaklı bir günlük geçididir.

## Renk sahipliği

Mevcut palet korunur; renk sayısı artırılmaz. Aksanlar yalnız anlam taşıdığında görünür:

| Token | Sahibi |
|---|---|
| `--color-carb` | Carb halkası ve karbonhidrat değeri |
| `--color-protein` | Protein halkası ve protein değeri |
| `--color-fat` | Fat halkası ve yağ değeri |
| `--color-lift` | Antrenman ve set ilerlemesi |
| `--color-flow` | Kilo ve fiziksel ilerleme |
| `--color-lav` | Check-in / ruh hâli |
| `--color-accent` | Navigasyon odağı ve ekranın tek birincil eylemi |

Aksan zemin dolgusu değildir. Gradyan yoktur; iki aksan birbirine karıştırılmaz.

## Yüzey ve boşluk

- `hero`: günün baskın özeti, 24px yarıçap ve geniş iç boşluk.
- `card`: normal bilgi yüzeyi, 18px yarıçap.
- `compact`: hızlı bakış kartı, daha sıkı dikey ritim.
- `strip`: hedef gibi ikincil geçitler, yatay ve düşük yükseklik.
- Aynı grid sırasındaki kartlar aynı yüksekliğe zorlanabilir; tüm sayfa eşit kart ızgarası olmaz.

Boşluk 4px tabanlıdır. Ana panel araları 16–20px, metrik kümeleri 8–12px, bölüm araları
32–48px kullanır. Yüzey sınırları token'dan gelir; ham beyaz alfa değeri yeni kodda eklenmez.

## Hareket

| Ad | Ne yapar | Süre / easing |
|---|---|---|
| `ring-draw` | Halkalar açılışta ve değer değişince dolar | 700ms · `--ease-out` |
| `press` | Desteklenen işaretçiyle 2px kalkar, basılınca 1px çöker | 140ms / 70ms |
| `pop` | Yalnız yeni tamamlanan hedef halkasında bir kez | 240ms |

Scroll reveal, spring, overshoot, hover scale ve `transition-all` yoktur.
`prefers-reduced-motion` etkinse mekânsal hareket tamamen düşer.

## Etkileşim ve durumlar

- Minimum dokunma alanı 44px'tir; ikon düğmelerin erişilebilir adı vardır.
- Detaylar mobilde alt sayfa, masaüstünde geniş çalışma paneli olarak açılır.
- Overlay odağı hapseder, `Escape` ile kapanır ve kapanınca önceki odağa döner.
- Her dashboard modülü kendi `ErrorBoundary`'sinde kalır.
- Boş durum gerçek veriyi söyler; sahte ilerleme veya örnek sayı kullanılmaz.
- Yükleme, hata ve disabled durumları aynı yüzey sistemi içinde görünür.

## Kaçınılacaklar

Eşit boyutlu kart duvarı, kart içinde kart, her başlıkta eyebrow, ikon kutulu feature-card,
dev landing-page tipografisi, monospace arayüz dili, gradyan, glow, glassmorphism,
dekoratif grafik, gereksiz rozet, `hover:scale-*`, kutlama toast'ı ve hover-only eylem.

## Uygulama eşlemeleri

- `tokens.css`: renk, yüzey, yarıçap, gölge, hareket ve z-index token'ları.
- `fittrack-client/src/index.css`: global tipografi, erişilebilirlik ve hareket primitifleri.
- `fittrack-client/src/App.tsx`: Performance Dashboard macrostructure.
- `fittrack-client/src/components/TodayRings.tsx`: C/P/F + kalori komuta alanı.
- `fittrack-client/src/components/ui.tsx`: ortak yüzey ve kontrol dili.
- `fittrack-client/src/components/Overlay.tsx`: mobil/masaüstü detay çalışma alanı.

## Format notları

- **Web uygulaması:** Bu dosyadaki token ve durum kuralları doğrudan uygulanır.
- **Statik portfolyo görseli:** 1440×900 masaüstü ve 390×844 mobil yakalama kullanılır;
  sahte veri eklenmez, canlı yerel API verisi gösterilir.
- **Dokümantasyon:** Renkler hex kopyalarıyla çoğaltılmaz; token adlarıyla anlatılır.
