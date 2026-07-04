// Local food database — macros per 100 g. Fully offline; no API.
// Search filters this list client-side (instant). `fav` foods surface in the
// quick-pick grid. Values are typical per-100g figures for the raw/cooked form
// noted in the name (rounded, good enough for daily tracking).

export interface Food {
  emoji: string;
  name: string;
  category: FoodCategory;
  fav?: boolean;
  per100: { calories: number; protein: number; carbs: number; fat: number };
}

export type FoodCategory =
  | "Protein"
  | "Süt Ürünü"
  | "Tahıl & Karb"
  | "Baklagil"
  | "Sebze"
  | "Meyve"
  | "Kuruyemiş & Yağ"
  | "Yemek"
  | "Atıştırma & Takviye";

export const FOODS: Food[] = [
  // ── Ozan's usuals (favorites) ──────────────────────────────────────────
  { emoji: "🥚", name: "Yumurta", category: "Protein", fav: true, per100: { calories: 155, protein: 13, carbs: 1.1, fat: 11 } },
  { emoji: "🫒", name: "Zeytinyağı", category: "Kuruyemiş & Yağ", fav: true, per100: { calories: 884, protein: 0, carbs: 0, fat: 100 } },
  { emoji: "🧀", name: "Beyaz Peynir", category: "Süt Ürünü", fav: true, per100: { calories: 264, protein: 17, carbs: 1.5, fat: 21 } },
  { emoji: "🌾", name: "Yulaf", category: "Tahıl & Karb", fav: true, per100: { calories: 389, protein: 17, carbs: 66, fat: 7 } },
  { emoji: "🥛", name: "Süt (Tam Yağlı)", category: "Süt Ürünü", fav: true, per100: { calories: 61, protein: 3.2, carbs: 4.8, fat: 3.3 } },
  { emoji: "🥜", name: "Fitnut Fıstık Ezmesi", category: "Kuruyemiş & Yağ", fav: true, per100: { calories: 600, protein: 28, carbs: 18, fat: 47 } },
  { emoji: "🍚", name: "Pilav (Pişmiş)", category: "Tahıl & Karb", fav: true, per100: { calories: 130, protein: 2.7, carbs: 28, fat: 0.3 } },
  { emoji: "🍞", name: "Uno Tam Tahıllı Ekmek", category: "Tahıl & Karb", fav: true, per100: { calories: 245, protein: 9, carbs: 45, fat: 3.5 } },
  { emoji: "🍗", name: "Tavuk Göğsü", category: "Protein", fav: true, per100: { calories: 165, protein: 31, carbs: 0, fat: 3.6 } },
  { emoji: "🥔", name: "Fırında Patates", category: "Sebze", fav: true, per100: { calories: 93, protein: 2, carbs: 21, fat: 0.1 } },
  { emoji: "🍖", name: "Fırında Köfte", category: "Yemek", fav: true, per100: { calories: 215, protein: 18, carbs: 5, fat: 13 } },

  // ── Protein (et, tavuk, balık, yumurta) ────────────────────────────────
  { emoji: "🍗", name: "Tavuk But", category: "Protein", per100: { calories: 209, protein: 26, carbs: 0, fat: 11 } },
  { emoji: "🦃", name: "Hindi Göğsü", category: "Protein", per100: { calories: 135, protein: 30, carbs: 0, fat: 1 } },
  { emoji: "🥩", name: "Dana Biftek", category: "Protein", per100: { calories: 250, protein: 26, carbs: 0, fat: 15 } },
  { emoji: "🥩", name: "Dana Kıyma (Yağsız)", category: "Protein", per100: { calories: 187, protein: 21, carbs: 0, fat: 11 } },
  { emoji: "🥩", name: "Dana Kıyma (Yağlı)", category: "Protein", per100: { calories: 250, protein: 26, carbs: 0, fat: 15 } },
  { emoji: "🍖", name: "Kuzu Pirzola", category: "Protein", per100: { calories: 294, protein: 25, carbs: 0, fat: 21 } },
  { emoji: "🥓", name: "Pastırma", category: "Protein", per100: { calories: 240, protein: 35, carbs: 2, fat: 10 } },
  { emoji: "🌭", name: "Hindi Sucuk", category: "Protein", per100: { calories: 260, protein: 20, carbs: 2, fat: 19 } },
  { emoji: "🐟", name: "Somon", category: "Protein", per100: { calories: 208, protein: 20, carbs: 0, fat: 13 } },
  { emoji: "🐟", name: "Ton Balığı (Konserve)", category: "Protein", per100: { calories: 116, protein: 26, carbs: 0, fat: 1 } },
  { emoji: "🐟", name: "Levrek", category: "Protein", per100: { calories: 97, protein: 18, carbs: 0, fat: 2.5 } },
  { emoji: "🐟", name: "Hamsi", category: "Protein", per100: { calories: 131, protein: 20, carbs: 0, fat: 5 } },
  { emoji: "🍤", name: "Karides", category: "Protein", per100: { calories: 99, protein: 24, carbs: 0.2, fat: 0.3 } },
  { emoji: "🥚", name: "Yumurta Beyazı", category: "Protein", per100: { calories: 52, protein: 11, carbs: 0.7, fat: 0.2 } },
  { emoji: "🥚", name: "Haşlanmış Yumurta", category: "Protein", per100: { calories: 155, protein: 13, carbs: 1.1, fat: 11 } },

  // ── Süt ürünleri ───────────────────────────────────────────────────────
  { emoji: "🥛", name: "Süt (Yarım Yağlı)", category: "Süt Ürünü", per100: { calories: 50, protein: 3.4, carbs: 4.9, fat: 1.8 } },
  { emoji: "🥛", name: "Süt (Yağsız)", category: "Süt Ürünü", per100: { calories: 35, protein: 3.4, carbs: 5, fat: 0.1 } },
  { emoji: "🍶", name: "Yoğurt (Tam Yağlı)", category: "Süt Ürünü", per100: { calories: 61, protein: 3.5, carbs: 4.7, fat: 3.3 } },
  { emoji: "🍶", name: "Yoğurt (Light)", category: "Süt Ürünü", per100: { calories: 42, protein: 4, carbs: 6, fat: 0.2 } },
  { emoji: "🥣", name: "Süzme Yoğurt", category: "Süt Ürünü", per100: { calories: 96, protein: 9, carbs: 3.6, fat: 5 } },
  { emoji: "🥣", name: "Yunan Yoğurdu", category: "Süt Ürünü", per100: { calories: 97, protein: 9, carbs: 3.9, fat: 5 } },
  { emoji: "🧀", name: "Lor Peyniri", category: "Süt Ürünü", per100: { calories: 98, protein: 11, carbs: 3.4, fat: 4.3 } },
  { emoji: "🧀", name: "Kaşar Peyniri", category: "Süt Ürünü", per100: { calories: 375, protein: 25, carbs: 2, fat: 30 } },
  { emoji: "🧀", name: "Çökelek", category: "Süt Ürünü", per100: { calories: 162, protein: 30, carbs: 5, fat: 2.5 } },
  { emoji: "🧀", name: "Labne", category: "Süt Ürünü", per100: { calories: 235, protein: 6, carbs: 4, fat: 22 } },
  { emoji: "🧈", name: "Tereyağı", category: "Kuruyemiş & Yağ", per100: { calories: 717, protein: 0.9, carbs: 0.1, fat: 81 } },
  { emoji: "🥤", name: "Kefir", category: "Süt Ürünü", per100: { calories: 55, protein: 3.3, carbs: 4.5, fat: 2.5 } },
  { emoji: "🥤", name: "Ayran", category: "Süt Ürünü", per100: { calories: 38, protein: 1.7, carbs: 2.9, fat: 2 } },

  // ── Tahıl & Karbonhidrat ──────────────────────────────────────────────
  { emoji: "🍚", name: "Bulgur Pilavı (Pişmiş)", category: "Tahıl & Karb", per100: { calories: 83, protein: 3, carbs: 19, fat: 0.2 } },
  { emoji: "🍚", name: "Pirinç (Çiğ)", category: "Tahıl & Karb", per100: { calories: 360, protein: 7, carbs: 79, fat: 0.6 } },
  { emoji: "🍚", name: "Esmer Pirinç (Pişmiş)", category: "Tahıl & Karb", per100: { calories: 111, protein: 2.6, carbs: 23, fat: 0.9 } },
  { emoji: "🍝", name: "Makarna (Pişmiş)", category: "Tahıl & Karb", per100: { calories: 131, protein: 5, carbs: 25, fat: 1.1 } },
  { emoji: "🍝", name: "Tam Buğday Makarna (Pişmiş)", category: "Tahıl & Karb", per100: { calories: 124, protein: 5, carbs: 26, fat: 0.5 } },
  { emoji: "🌾", name: "Kinoa (Pişmiş)", category: "Tahıl & Karb", per100: { calories: 120, protein: 4.4, carbs: 21, fat: 1.9 } },
  { emoji: "🍞", name: "Beyaz Ekmek", category: "Tahıl & Karb", per100: { calories: 265, protein: 9, carbs: 49, fat: 3.2 } },
  { emoji: "🍞", name: "Tam Buğday Ekmek", category: "Tahıl & Karb", per100: { calories: 247, protein: 13, carbs: 41, fat: 3.4 } },
  { emoji: "🥖", name: "Ekmek (Somun)", category: "Tahıl & Karb", per100: { calories: 265, protein: 9, carbs: 51, fat: 2 } },
  { emoji: "🫓", name: "Lavaş", category: "Tahıl & Karb", per100: { calories: 275, protein: 8, carbs: 55, fat: 1.5 } },
  { emoji: "🥐", name: "Yufka", category: "Tahıl & Karb", per100: { calories: 306, protein: 9, carbs: 63, fat: 1.5 } },
  { emoji: "🌽", name: "Mısır (Haşlanmış)", category: "Tahıl & Karb", per100: { calories: 96, protein: 3.4, carbs: 21, fat: 1.5 } },
  { emoji: "🥣", name: "Mısır Gevreği", category: "Tahıl & Karb", per100: { calories: 378, protein: 7, carbs: 84, fat: 0.9 } },
  { emoji: "🥣", name: "Granola", category: "Tahıl & Karb", per100: { calories: 471, protein: 10, carbs: 64, fat: 20 } },
  { emoji: "🌾", name: "Karabuğday (Pişmiş)", category: "Tahıl & Karb", per100: { calories: 92, protein: 3.4, carbs: 20, fat: 0.6 } },
  { emoji: "🥔", name: "Patates (Haşlanmış)", category: "Sebze", per100: { calories: 87, protein: 1.9, carbs: 20, fat: 0.1 } },
  { emoji: "🍠", name: "Tatlı Patates", category: "Sebze", per100: { calories: 86, protein: 1.6, carbs: 20, fat: 0.1 } },

  // ── Baklagil ──────────────────────────────────────────────────────────
  { emoji: "🫘", name: "Mercimek (Pişmiş)", category: "Baklagil", per100: { calories: 116, protein: 9, carbs: 20, fat: 0.4 } },
  { emoji: "🫘", name: "Nohut (Pişmiş)", category: "Baklagil", per100: { calories: 164, protein: 9, carbs: 27, fat: 2.6 } },
  { emoji: "🫘", name: "Kuru Fasulye (Pişmiş)", category: "Baklagil", per100: { calories: 127, protein: 9, carbs: 23, fat: 0.5 } },
  { emoji: "🫘", name: "Barbunya (Pişmiş)", category: "Baklagil", per100: { calories: 127, protein: 9, carbs: 22, fat: 0.5 } },
  { emoji: "🫘", name: "Yeşil Mercimek (Pişmiş)", category: "Baklagil", per100: { calories: 116, protein: 9, carbs: 20, fat: 0.4 } },
  { emoji: "🥣", name: "Humus", category: "Baklagil", per100: { calories: 166, protein: 8, carbs: 14, fat: 10 } },
  { emoji: "🫛", name: "Bezelye", category: "Baklagil", per100: { calories: 81, protein: 5, carbs: 14, fat: 0.4 } },
  { emoji: "🫛", name: "Edamame", category: "Baklagil", per100: { calories: 121, protein: 12, carbs: 9, fat: 5 } },

  // ── Sebze ─────────────────────────────────────────────────────────────
  { emoji: "🥦", name: "Brokoli", category: "Sebze", per100: { calories: 34, protein: 2.8, carbs: 7, fat: 0.4 } },
  { emoji: "🥬", name: "Ispanak", category: "Sebze", per100: { calories: 23, protein: 2.9, carbs: 3.6, fat: 0.4 } },
  { emoji: "🥗", name: "Marul", category: "Sebze", per100: { calories: 15, protein: 1.4, carbs: 2.9, fat: 0.2 } },
  { emoji: "🍅", name: "Domates", category: "Sebze", per100: { calories: 18, protein: 0.9, carbs: 3.9, fat: 0.2 } },
  { emoji: "🥒", name: "Salatalık", category: "Sebze", per100: { calories: 15, protein: 0.7, carbs: 3.6, fat: 0.1 } },
  { emoji: "🫑", name: "Biber (Yeşil)", category: "Sebze", per100: { calories: 20, protein: 0.9, carbs: 4.6, fat: 0.2 } },
  { emoji: "🧅", name: "Soğan", category: "Sebze", per100: { calories: 40, protein: 1.1, carbs: 9, fat: 0.1 } },
  { emoji: "🥕", name: "Havuç", category: "Sebze", per100: { calories: 41, protein: 0.9, carbs: 10, fat: 0.2 } },
  { emoji: "🍆", name: "Patlıcan", category: "Sebze", per100: { calories: 25, protein: 1, carbs: 6, fat: 0.2 } },
  { emoji: "🥒", name: "Kabak", category: "Sebze", per100: { calories: 17, protein: 1.2, carbs: 3.1, fat: 0.3 } },
  { emoji: "🍄", name: "Mantar", category: "Sebze", per100: { calories: 22, protein: 3.1, carbs: 3.3, fat: 0.3 } },
  { emoji: "🥑", name: "Avokado", category: "Kuruyemiş & Yağ", per100: { calories: 160, protein: 2, carbs: 9, fat: 15 } },

  // ── Meyve ─────────────────────────────────────────────────────────────
  { emoji: "🍌", name: "Muz", category: "Meyve", per100: { calories: 89, protein: 1.1, carbs: 23, fat: 0.3 } },
  { emoji: "🍎", name: "Elma", category: "Meyve", per100: { calories: 52, protein: 0.3, carbs: 14, fat: 0.2 } },
  { emoji: "🍊", name: "Portakal", category: "Meyve", per100: { calories: 47, protein: 0.9, carbs: 12, fat: 0.1 } },
  { emoji: "🍓", name: "Çilek", category: "Meyve", per100: { calories: 32, protein: 0.7, carbs: 8, fat: 0.3 } },
  { emoji: "🫐", name: "Yaban Mersini", category: "Meyve", per100: { calories: 57, protein: 0.7, carbs: 14, fat: 0.3 } },
  { emoji: "🍇", name: "Üzüm", category: "Meyve", per100: { calories: 69, protein: 0.7, carbs: 18, fat: 0.2 } },
  { emoji: "🍉", name: "Karpuz", category: "Meyve", per100: { calories: 30, protein: 0.6, carbs: 8, fat: 0.2 } },
  { emoji: "🍈", name: "Kavun", category: "Meyve", per100: { calories: 34, protein: 0.8, carbs: 8, fat: 0.2 } },
  { emoji: "🥝", name: "Kivi", category: "Meyve", per100: { calories: 61, protein: 1.1, carbs: 15, fat: 0.5 } },
  { emoji: "🍑", name: "Şeftali", category: "Meyve", per100: { calories: 39, protein: 0.9, carbs: 10, fat: 0.3 } },
  { emoji: "🍐", name: "Armut", category: "Meyve", per100: { calories: 57, protein: 0.4, carbs: 15, fat: 0.1 } },
  { emoji: "🍍", name: "Ananas", category: "Meyve", per100: { calories: 50, protein: 0.5, carbs: 13, fat: 0.1 } },
  { emoji: "🥭", name: "Mango", category: "Meyve", per100: { calories: 60, protein: 0.8, carbs: 15, fat: 0.4 } },
  { emoji: "🌰", name: "Hurma", category: "Meyve", per100: { calories: 282, protein: 2.5, carbs: 75, fat: 0.4 } },
  { emoji: "🍇", name: "Kuru Üzüm", category: "Meyve", per100: { calories: 299, protein: 3.1, carbs: 79, fat: 0.5 } },

  // ── Kuruyemiş & Yağ ───────────────────────────────────────────────────
  { emoji: "🥜", name: "Yer Fıstığı", category: "Kuruyemiş & Yağ", per100: { calories: 567, protein: 26, carbs: 16, fat: 49 } },
  { emoji: "🌰", name: "Badem", category: "Kuruyemiş & Yağ", per100: { calories: 579, protein: 21, carbs: 22, fat: 50 } },
  { emoji: "🌰", name: "Ceviz", category: "Kuruyemiş & Yağ", per100: { calories: 654, protein: 15, carbs: 14, fat: 65 } },
  { emoji: "🌰", name: "Fındık", category: "Kuruyemiş & Yağ", per100: { calories: 628, protein: 15, carbs: 17, fat: 61 } },
  { emoji: "🌰", name: "Antep Fıstığı", category: "Kuruyemiş & Yağ", per100: { calories: 560, protein: 20, carbs: 28, fat: 45 } },
  { emoji: "🌰", name: "Kaju", category: "Kuruyemiş & Yağ", per100: { calories: 553, protein: 18, carbs: 30, fat: 44 } },
  { emoji: "🎃", name: "Kabak Çekirdeği", category: "Kuruyemiş & Yağ", per100: { calories: 559, protein: 30, carbs: 11, fat: 49 } },
  { emoji: "🌻", name: "Ay Çekirdeği", category: "Kuruyemiş & Yağ", per100: { calories: 584, protein: 21, carbs: 20, fat: 51 } },
  { emoji: "🥥", name: "Hindistan Cevizi Yağı", category: "Kuruyemiş & Yağ", per100: { calories: 892, protein: 0, carbs: 0, fat: 99 } },
  { emoji: "🌰", name: "Tahin", category: "Kuruyemiş & Yağ", per100: { calories: 595, protein: 17, carbs: 21, fat: 54 } },
  { emoji: "🍫", name: "Tahin Helva", category: "Atıştırma & Takviye", per100: { calories: 520, protein: 12, carbs: 50, fat: 30 } },

  // ── Yemekler (Türk mutfağı) ───────────────────────────────────────────
  { emoji: "🍲", name: "Mercimek Çorbası", category: "Yemek", per100: { calories: 60, protein: 3, carbs: 9, fat: 1.5 } },
  { emoji: "🥘", name: "Tavuk Sote", category: "Yemek", per100: { calories: 150, protein: 18, carbs: 5, fat: 6 } },
  { emoji: "🍖", name: "Izgara Köfte", category: "Yemek", per100: { calories: 240, protein: 18, carbs: 4, fat: 17 } },
  { emoji: "🥙", name: "Tavuk Döner", category: "Yemek", per100: { calories: 190, protein: 21, carbs: 3, fat: 11 } },
  { emoji: "🥙", name: "Et Döner", category: "Yemek", per100: { calories: 230, protein: 19, carbs: 3, fat: 16 } },
  { emoji: "🍲", name: "Kuru Fasulye Yemeği", category: "Yemek", per100: { calories: 140, protein: 7, carbs: 18, fat: 4 } },
  { emoji: "🍅", name: "Menemen", category: "Yemek", per100: { calories: 118, protein: 6, carbs: 5, fat: 8 } },
  { emoji: "🥗", name: "Çoban Salata", category: "Yemek", per100: { calories: 40, protein: 1, carbs: 4, fat: 2.5 } },
  { emoji: "🍚", name: "Sarma (Yaprak)", category: "Yemek", per100: { calories: 170, protein: 3, carbs: 22, fat: 8 } },
  { emoji: "🥟", name: "Mantı", category: "Yemek", per100: { calories: 210, protein: 9, carbs: 30, fat: 6 } },
  { emoji: "🍕", name: "Lahmacun", category: "Yemek", per100: { calories: 230, protein: 10, carbs: 33, fat: 6 } },
  { emoji: "🍕", name: "Pide (Kıymalı)", category: "Yemek", per100: { calories: 260, protein: 11, carbs: 34, fat: 9 } },
  { emoji: "🍲", name: "Güveç (Etli)", category: "Yemek", per100: { calories: 130, protein: 10, carbs: 8, fat: 6 } },
  { emoji: "🍲", name: "Yayla Çorbası", category: "Yemek", per100: { calories: 55, protein: 2.5, carbs: 7, fat: 2 } },

  // ── Atıştırma & Takviye ───────────────────────────────────────────────
  { emoji: "💪", name: "Whey Protein (Toz)", category: "Atıştırma & Takviye", per100: { calories: 400, protein: 80, carbs: 8, fat: 6 } },
  { emoji: "🥤", name: "Whey Protein (1 Ölçek ~30g)", category: "Atıştırma & Takviye", per100: { calories: 400, protein: 80, carbs: 8, fat: 6 } },
  { emoji: "🍫", name: "Protein Bar", category: "Atıştırma & Takviye", per100: { calories: 350, protein: 30, carbs: 40, fat: 10 } },
  { emoji: "🍫", name: "Bitter Çikolata (%70)", category: "Atıştırma & Takviye", per100: { calories: 546, protein: 8, carbs: 46, fat: 31 } },
  { emoji: "🍫", name: "Sütlü Çikolata", category: "Atıştırma & Takviye", per100: { calories: 535, protein: 8, carbs: 59, fat: 30 } },
  { emoji: "🍯", name: "Bal", category: "Atıştırma & Takviye", per100: { calories: 304, protein: 0.3, carbs: 82, fat: 0 } },
  { emoji: "🫙", name: "Reçel", category: "Atıştırma & Takviye", per100: { calories: 278, protein: 0.4, carbs: 69, fat: 0.1 } },
  { emoji: "🧊", name: "Jelibon", category: "Atıştırma & Takviye", per100: { calories: 335, protein: 6, carbs: 78, fat: 0 } },
  { emoji: "🥨", name: "Kraker", category: "Atıştırma & Takviye", per100: { calories: 430, protein: 9, carbs: 68, fat: 13 } },
  { emoji: "🍟", name: "Cips", category: "Atıştırma & Takviye", per100: { calories: 536, protein: 7, carbs: 53, fat: 34 } },
  { emoji: "🥤", name: "Kola", category: "Atıştırma & Takviye", per100: { calories: 42, protein: 0, carbs: 11, fat: 0 } },
  { emoji: "🧋", name: "Meyve Suyu (Portakal)", category: "Atıştırma & Takviye", per100: { calories: 45, protein: 0.7, carbs: 10, fat: 0.2 } },
];

// Curated quick-pick grid: Ozan's usuals first, kept short for the sheet.
export const FREQUENT_FOODS: Food[] = FOODS.filter((f) => f.fav);

// Case/diacritic-insensitive local search over the whole database.
export function searchFoods(query: string, limit = 30): Food[] {
  const q = normalize(query.trim());
  if (!q) return [];
  const scored = FOODS.map((f) => {
    const name = normalize(f.name);
    let score = -1;
    if (name === q) score = 100;
    else if (name.startsWith(q)) score = 80;
    else if (name.includes(q)) score = 60;
    else if (normalize(f.category).includes(q)) score = 30;
    return { f, score };
  }).filter((x) => x.score >= 0);
  scored.sort((a, b) => b.score - a.score || a.f.name.localeCompare(b.f.name, "tr"));
  return scored.slice(0, limit).map((x) => x.f);
}

function normalize(s: string): string {
  return s
    .toLocaleLowerCase("tr")
    .replaceAll("ı", "i")
    .replaceAll("ğ", "g")
    .replaceAll("ü", "u")
    .replaceAll("ş", "s")
    .replaceAll("ö", "o")
    .replaceAll("ç", "c");
}
