/* Bilgi kartları ("Biliyor muydun?"). Her kartın bir kaynağı var; sayılar kaynaktan, yuvarlanmadan.
   Sağlık etkisi kanıtlanmamış yerde "kanıtlanmadı" denir — kart korkutmak için değil, seçim yaptırmak için.
   Kaynaklar 2026-11-02'de doğrulandı (künye + DOI). Yeni kart eklerken aynı kural: kaynaksız kart yok. */

export type TipTopic = "microplastic" | "nutrition" | "sleep" | "training";

export interface Tip {
  id: string;
  topic: TipTopic;
  title: string;
  body: string;
  /** Somut, uygulanabilir bir adım. */
  action: string;
  /** Kartta büyük gösterilen tek sayı — gövdedeki rakamın aynısı, yeni bilgi değil. */
  stat?: { value: string; label: string };
  source: { label: string; url: string };
}

export const TIP_TOPICS: Record<TipTopic, string> = {
  microplastic: "Mikroplastik",
  nutrition: "Beslenme",
  sleep: "Uyku",
  training: "Antrenman",
};

export const TIPS: Tip[] = [
  {
    id: "mp-bottled-water",
    topic: "microplastic",
    title: "Şişe suyunda nanoplastik",
    body: "Columbia Üniversitesi'nin çalışmasında bir litre şişe suyunda ortalama ~240.000 plastik parçacık bulundu; yaklaşık %90'ı nanoplastikti. Sağlığa etkisi henüz kanıtlanmadı.",
    action: "Evde filtreli musluk suyu ve cam ya da çelik matara kullan.",
    stat: { value: "~240.000", label: "parçacık / litre" },
    source: { label: "Qian vd., PNAS, 2024", url: "https://doi.org/10.1073/pnas.2300582121" },
  },
  {
    id: "mp-microwave",
    topic: "microplastic",
    title: "Plastik kapta mikrodalga",
    body: "Plastik kaplar mikrodalgada 3 dakika ısıtılınca santimetrekare başına milyonlarca mikroplastik ve milyarlarca nanoplastik saldı. Isı, salınımı belirgin artırıyor.",
    action: "Yemeği ısıtmadan önce cam ya da seramik kaba al.",
    stat: { value: "3 dk", label: "ısıtmada milyarlarca nanoplastik" },
    source: { label: "Hussain vd., Environ. Sci. Technol., 2023", url: "https://doi.org/10.1021/acs.est.3c01942" },
  },
  {
    id: "mp-teabag",
    topic: "microplastic",
    title: "Plastik çay poşeti",
    body: "Naylon ya da PET poşet 95°C suda demlenince tek poşetten ~11,6 milyar mikroplastik ve ~3,1 milyar nanoplastik parçacık salındı. Kâğıt poşetler bu çalışmada yoktu.",
    action: "Dökme çay ya da kâğıt poşet tercih et.",
    stat: { value: "11,6 milyar", label: "mikroplastik / poşet" },
    source: { label: "Hernandez vd., Environ. Sci. Technol., 2019", url: "https://doi.org/10.1021/acs.est.9b02540" },
  },
  {
    id: "mp-heat-general",
    topic: "microplastic",
    title: "Sıcak + plastik = daha fazla salınım",
    body: "Hem mikrodalga hem çay poşeti çalışmalarında ortak bulgu: plastik ısındıkça çok daha fazla parçacık bırakıyor. Soğuk kullanımda salınım çok daha düşük.",
    action: "Sıcak içeceği plastik bardak ya da şişeye koyma.",
    source: { label: "Hussain vd., 2023 · Hernandez vd., 2019", url: "https://doi.org/10.1021/acs.est.3c01942" },
  },
  {
    id: "nu-protein",
    topic: "nutrition",
    title: "Kas için ne kadar protein?",
    body: "49 çalışmalık meta-analizde direnç antrenmanıyla birlikte kas kazanımına protein katkısı yaklaşık 1,6 g/kg/gün civarında plato yaptı. Bunun üstü ek fayda göstermedi.",
    action: "80 kg için günde ~130 g; 3-4 öğüne böl.",
    stat: { value: "1,6 g/kg", label: "günlük protein platosu" },
    source: { label: "Morton vd., Br J Sports Med, 2018", url: "https://doi.org/10.1136/bjsports-2017-097608" },
  },
  {
    id: "nu-fiber",
    topic: "nutrition",
    title: "Lif: günde 25-29 g",
    body: "DSÖ için yapılan büyük derlemede günde 25-29 g lif alanlarda kalp hastalığı, tip 2 diyabet ve kolon kanseri riski daha düşüktü; daha fazlası ek fayda gösterdi.",
    action: "Her öğüne sebze, baklagil ya da tam tahıl ekle.",
    stat: { value: "25–29 g", label: "günlük lif" },
    source: { label: "Reynolds vd., The Lancet, 2019", url: "https://doi.org/10.1016/S0140-6736(18)31809-9" },
  },
  {
    id: "nu-sugar",
    topic: "nutrition",
    title: "Serbest şeker sınırı",
    body: "DSÖ, eklenmiş (serbest) şekerin günlük enerjinin %10'unu geçmemesini, mümkünse %5'in altında kalmasını öneriyor. 2.400 kcal için %10 ≈ 60 g.",
    action: "Şekerli içeceği suya ya da şekersiz çaya çevir.",
    stat: { value: "%10", label: "enerjinin üst sınırı" },
    source: { label: "DSÖ, Şeker alımı kılavuzu, 2015", url: "https://www.who.int/publications/i/item/9789241549028" },
  },
  {
    id: "nu-salt",
    topic: "nutrition",
    title: "Tuz: günde 5 g altı",
    body: "DSÖ yetişkinler için günde 5 g'dan (yaklaşık bir çay kaşığı) az tuz öneriyor. Tuzun çoğu sofradan değil, paketli ve hazır yiyeceklerden geliyor.",
    action: "Paket etiketinde 'tuz' satırına bak; 1,5 g/100 g üstü yüksektir.",
    stat: { value: "< 5 g", label: "günlük tuz" },
    source: { label: "DSÖ, Sodyum azaltma bilgi notu", url: "https://www.who.int/news-room/fact-sheets/detail/salt-reduction" },
  },
  {
    id: "nu-upf",
    topic: "nutrition",
    title: "Ultra işlenmiş gıdalar",
    body: "45 havuzlanmış analizi kapsayan şemsiye derlemede ultra işlenmiş gıda tüketimi yüksek olanlarda kalp-damar ölümü, tip 2 diyabet ve kaygı-depresyon riskleri daha yüksek bulundu (gözlemsel kanıt).",
    action: "İçindekiler listesi uzun ve tanımadığın maddelerle doluysa başka seçenek ara.",
    source: { label: "Lane vd., BMJ, 2024", url: "https://doi.org/10.1136/bmj-2023-077310" },
  },
  {
    id: "sl-7h",
    topic: "sleep",
    title: "En az 7 saat",
    body: "Uyku tıbbı derneklerinin ortak önerisi yetişkinler için düzenli olarak gecede 7 saat ve üstü. Daha kısa uyku kilo alımı ve performans düşüşüyle ilişkili.",
    action: "Kalkış saatinden 7,5 saat geriye say, o saatte yatakta ol.",
    stat: { value: "7+ saat", label: "her gece" },
    source: { label: "Watson vd., Sleep (AASM & SRS), 2015", url: "https://doi.org/10.5665/sleep.4716" },
  },
  {
    id: "sl-consistency",
    topic: "sleep",
    title: "Düzen, süre kadar önemli",
    body: "UK Biobank'ta ~61 bin kişinin bilek ölçerle izlendiği çalışmada uyku saatlerinin düzenliliği, ölüm riskini uyku süresinden daha güçlü öngördü (gözlemsel kanıt).",
    action: "Hafta sonu da kalkış saatini 1 saatten fazla kaydırma.",
    stat: { value: "~61 bin", label: "kişilik çalışma" },
    source: { label: "Windred vd., Sleep, 2024", url: "https://doi.org/10.1093/sleep/zsad253" },
  },
  {
    id: "tr-steps",
    topic: "training",
    title: "10 bin adım şart mı?",
    body: "15 kohortluk meta-analizde ölüm riskindeki düşüş 60 yaş üstünde günde 6.000-8.000, altında 8.000-10.000 adımda plato yaptı. Her ek adım o noktaya kadar fayda sağlıyor.",
    action: "Hedefini yaşına göre koy; telefon cebinde dursun.",
    stat: { value: "8–10 bin", label: "adım (60 yaş altı)" },
    source: { label: "Paluch vd., Lancet Public Health, 2022", url: "https://doi.org/10.1016/S2468-2667(21)00302-9" },
  },
  {
    id: "tr-creatine",
    topic: "training",
    title: "Kreatin: en çok çalışılan takviye",
    body: "ISSN'in görüş bildirisine göre kreatin monohidrat güç ve kas kazanımı için en etkili ve en çok araştırılmış takviye; sağlıklı kişilerde önerilen dozlarda güvenli.",
    action: "Günde 3-5 g, her gün aynı saatte; yükleme şart değil.",
    stat: { value: "3–5 g", label: "günde, her gün" },
    source: { label: "Kreider vd., J Int Soc Sports Nutr, 2017", url: "https://doi.org/10.1186/s12970-017-0173-z" },
  },
  {
    id: "tr-protein-timing",
    topic: "training",
    title: "Toplam protein, zamanlamadan önemli",
    body: "Protein zamanlaması meta-analizinde kas kazanımını asıl belirleyen günlük toplam protein çıktı; toplam eşitlendiğinde antrenman sonrası 'anabolik pencere' etkisi kayboldu.",
    action: "Günlük toplamı tuttur; shake'i yetiştirmek için koşma.",
    source: { label: "Schoenfeld vd., J Int Soc Sports Nutr, 2013", url: "https://doi.org/10.1186/1550-2783-10-53" },
  },
];
