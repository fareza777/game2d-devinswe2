# Oathfire — catatan project

Game RPG 2D isometrik untuk Android (portrait). Ini project utama yang sedang dikerjakan.
Aturan umum folder induk ada di `..\AGENTS.md` dan berlaku penuh di sini.

## Bentuk project

- **Scene dibuat oleh kode, bukan tangan.** Jangan mengedit file `.unity` secara manual:
  jalankan `Oathfire/Build Scenes` dan `Oathfire/Build Rennfall Scene` (lihat
  `Assets/Oathfire/Editor/`). Editan manual akan tertimpa saat scene dibangun ulang.
- **Konten ditulis di `Docs/`, bukan langsung di Unity.** File `Docs/*.json` (dialog, item, quest,
  cinematic, kontrak, event dunia) berisi dua bahasa sekaligus. Jalankan `python Tools/build_content.py`
  untuk mengubahnya menjadi data runtime di `Assets/Oathfire/Resources/` beserta tabel lokalisasi.
- **Semua akibat berbentuk flag dan counter di save.** Quest, kontrak papan kerja, dan event dunia
  membaca counter yang sama (`kills.*`, `built.shelter`, `nights.survived`, `contracts.completed`).
  Jangan membuat pelacakan khusus untuk satu fitur: tambahkan objektif yang membaca counter yang sudah ada,
  atau tulis counter baru dari tempat kejadiannya.
- **Peta dibuat oleh kode, dan dinilai sebelum masuk Unity.** `Tools/compose_rennfall.py` menyusun desa dari
  kit bangunan utuh (`Docs/tileset_stamps.json`, hasil `extract_tileset_stamps.py` yang membongkar scene
  contoh aset). `Tools/tileset_preview.py` merender peta jadi PNG di luar Unity memakai sprite, pivot, dan
  urutan gambar yang sama dengan engine — satu siklus lihat-perbaiki jadi hitungan detik, bukan 5–10 menit.
  **Nilai tampilan lewat perender ini dulu**, baru bangun scene.
  Dua pelajaran yang jangan diulang: (1) memotong persegi panjang dari peta contoh memotong bangunan
  separuh dan hasilnya terlihat seperti tempelan aset; (2) menabur variasi tile per petak membuat permukaan
  berbintik — tanah, batu, dan rumput harus dihampar sebagai bidang utuh.
  Kota besar (`Tools/compose_greymarch.py`) berbeda: distrik bertembok di peta contoh adalah **satu struktur
  yang tersambung**, jadi ditempatkan utuh sebagai satu keping, bukan dipotong.
- **Lokasi:** Rennfall (desa) → TradeRoad (jalan dagang, penyergapan + random battle) → Greymarch (kota).
  Semua scene berbagi bagian yang sama lewat `Editor/MapScene.cs`.
- **Aset AI** dibuat lewat `Tools/` (Recraft untuk ilustrasi cukil kayu, Replicate/FLUX untuk
  potret dan musik, ElevenLabs untuk suara). Hasilnya di-cache, jadi menjalankan ulang tidak
  menghabiskan kredit dua kali. VO dialog: `Tools/make_dialogue_vo.py <script>` — sebuah baris jadi
  bersuara cukup dengan adanya file `Resources/Voice/dlg_<script>_<node>.mp3`.
- **Suara:** `Tools/make_audio.py [sfx|music|all]` membuat SFX + ambience (ElevenLabs) dan musik lokasi
  (MusicGen) ke `Resources/{Sfx,Ambience,Music}`. Kode memutar lewat nama: `Audio.PlaySfx("hit")`.
  `Audio/SoundDirector.cs` memilih musik/ambience dari scene, malam, dan musuh di dekat pemain.
- **Branding:** `Tools/make_brand_art.py` (ikon aplikasi + lambang splash, hasil di `Art/Brand`), `OathfireBuilder.ApplyAppIcon`.
- **Panah quest:** `Docs/quest_guides.json` memetakan kunci objektif → target (npc, hearth, board, gather,
  build, exit) + rute antar scene. Objektif tanpa tempat pasti sengaja tidak diberi panah. Bisa dimatikan di Options.

## Jebakan yang sudah pernah terjadi (jangan diulang)

- **`GenericPlayer.prefab` rusak dari paketnya**: sprite dan animator controller-nya tidak ada. Tubuh pemain
  dipasang dari `Characters/NPC3` (rig berpakaian, 26 parameter animator sama). `player1` adalah tubuh dasar
  character creator yang **telanjang** — jangan dipakai.
- **Tokoh paket ada di sorting order 1, atap peta di 2/11/15.** Semua tokoh dinaikkan lewat
  `World/ActorSorting.Raise` (order 20, skala 1.5). Tokoh baru yang di-spawn wajib melewati fungsi itu.
- **`GUID 0000…f000…` bukan referensi mati** — itu sumber daya bawaan Unity.
- **TMP: pilih font dulu, baru outline.** Menyetel `outlineWidth` membuat salinan material; kalau dibuat
  sebelum font diganti, label menggambar huruf dari atlas font lama — huruf nyata yang tidak mengeja apa pun.
- **Jangan pakai `Mask` dengan Image alpha nyaris 0 untuk guliran** — isinya bisa ikut hilang. Pakai `RectMask2D`.
  Dan set `sizeDelta = Vector2.zero` pada konten yang di-stretch, atau terpotong 50px di tiap sisi.
- **UI jangan diukur piksel tetap.** Pakai anchor proporsional + layout group; jendela tes 3:4 lebih pendek dari HP.
- **Tidak ada AudioListener di scene mana pun** — satu-satunya ada di objek layanan persisten (`AudioService`).
  Jangan tambahkan ke kamera; tanpa listener sama sekali, game sunyi total walau VO dan musik ada.
- **Hero macet di spawn:** controller paket berhenti total saat collider menyentuh apa pun. Collider hero kini
  lingkaran kecil di **kaki** (bukan dada), `worldMask` controller = 0 supaya physics yang menahan tembok (hero
  meluncur), dan `World/BodyUnstuck` melepas hero yang tertanam. Cek dengan menu `Oathfire/Diagnostics/Player Spawn Clearance`.
- **`NPC/TraderTemplete.prefab` tidak punya renderer sama sekali.** Setiap NPC/pengunjung wajib diberi tubuh lewat
  `MapScene.GiveNpcBody(actor, "NPC1"|"NPC2", tint)` (atau field `visitorBody` di WorldEventDirector), kalau tidak ia tak terlihat.
- **Tembok ditahan oleh `World/WallSlideMover`**, bukan physics: MovePosition pada body Dynamic menembus collider statis.
  Probe `walls stop the hero` di smoke test membuktikannya.
- **Tombol UI butuh `raycastTarget = true` pada Image-nya**, kalau tidak sentuhan jatuh ke belakang (pilihan dialog pernah macet karena ini).
- **Pintu:** ambang pintu paket adalah lengkungan kosong (`Wall C6_*`, `Wall D6_*`). `Tools/doors.py` memasang `Door A1_<arah>`
  di tiap lengkungan tunggal (lengkungan berderet = arkade, dilewati). Bangunan tanpa lengkungan: `EXTRA_DOORS`.
- **Ke-18 prefab `Destructible tiles/*` adalah EFEK HANCUR** (`destroyOnSpawn: 1`): di frame pertama mereka hancur dan
  mematikan semua collider. Jangan dipakai sebagai pohon/batu/papan. Pakai `MapScene.BuildProp(nama, tile, ...)`.
- **Karakter di sorting order 1** (di bawah atap, seperti rancangan paket). `World/RoofFader` memudarkan atap rumah yang
  dimasuki (data `houses` di map JSON) atau yang menutupi hero. NPC ditempatkan di luar sel yang tertutup atap.
- **Rumah bisa dimasuki:** `Tools/doors.py` memasang pintu di lengkungan tunggal dan membuka interior; `World/HouseDoor`.
- **Collider hantu:** contoh kota penuh collider di jalan kosong. `Tools/walkability.py` menghapusnya (Greymarch: 268).
- **`OathfireBuilder.BuildScenes` dulu menimpa daftar scene build** (peta hilang dari build). Sekarang mempertahankan peta.
- **Atlas font hanya berisi ASCII + `·—–‘’“”…×` dan huruf beraksen.** Karakter lain (mis. `•`, `✓`) tidak tergambar.
- **Tanpa unsur keagamaan juga berlaku untuk nama/gelar:** "Sister Maren" sudah diganti "Inspector Maren".
- **Musuh dimunculkan lewat `World/SpawnPlanner`** (tanah terbuka + jalur jelas), dijaga `EnemyReachGuard`.
  Hearth berdiri di alun-alun pasar: jalur dicek sampai `NightDirector.SquareReach`, bukan ke titik api.
- **Variasi musuh = `World/ActorLook`** (rig, tint, skala) di spawner. Rig NPC1/NPC2 cocok untuk AI musuh
  (26 parameter animator identik). Tint disalin ke warna tersimpan `EnemyHealth2D` agar kedip hit tidak memutihkan.
- **Kesulitan berjenjang (keputusan user):** awal game hanya musuh dasar, lalu bertambah jumlah dan keanehannya.
  Malam 1: 8 prajurit skeleton polos, HP 60%, damage 6. Malam 2 +penyihir, 3 +knight, 4+ elite. Daftar `ActorLook`
  diurutkan dari polos ke aneh dan dibuka bertahap (`PickUnlocked`). Jalan: bandit polos dulu, ragam setelah ambush.
- **Damage pedang = `PlayerState.Damage`** lewat `World/PlayerDamageSync` (paket memakai base 12 tetap, stat tak terpakai).
- **Siang/malam = `World/DayNightLighting`** pada Global Light + lampu obor + vinyet. (Kunang-kunang/partikel udara pernah dicoba dan ditolak: terlihat aneh.)
- **Build Windows berhenti saat jendela tak fokus** (`runInBackground: 0`); mode `-smoketest` memaksanya jalan.
- **Jangan gabungkan collider tile isometrik dengan `CompositeCollider2D`** — hasilnya 0 path, hero tembus tembok.
- **Progres pemain:** XP dari setiap musuh yang tumbang (`Progress/KillRewards`, elite ×2, bos ×3), bar XP + level di HUD,
  naik level memulihkan luka dan menaikkan HP maks (`World/PlayerVitalsSync`; paket memakai maxHealth 100 tetap).
  Skill area terbuka di level 3 (`MobileControls.AreaSkillLevel`).
- **Mati bukan akhir permainan:** paket tidak punya alur bangkit (panel respawn-nya tidak dibangun), jadi hero dulu
  tergeletak selamanya. `World/DefeatRecovery` memudarkan layar dan membangkitkan hero di dekat api/titik datang, -10% koin.
- **Tiba lewat jalan yang dipakai:** `SceneFlow.PreviousScene` + `World/PlayerArrival` menaruh hero di dekat `SceneExit`
  menuju scene asal. Muat save tetap memakai titik awal peta.
- **Wilayah yang dipotong dari contoh peta bisa terbuka ke ruang kosong.** `build_maps.py` (gaya `hollow`) memasang
  collider tepi (`Colliders(edge)`) di sel kosong yang bersebelahan dengan tanah yang bisa dipijak.
- **Babak II dimulai:** `q_hollow` (Ceruk Duri Hitam) setelah `act2.summons_done`: Dace → jalan setapak (muncul setelah
  `act2.dace_briefed`) → kamp + celah (`kills.hollow` 7) → bos Rook Varr (`World/BossEncounter`: dialog dulu, lalu tarung)
  → Mira Holt → Vesna. Petunjuk panah baru: target `spot` (`World/QuestSpot`). Petunjuk berikutnya: benteng besi tua
  (`act2.keep_rumoured`) di aula batu contoh peta (region sekitar x -158..-135, y -110..-66).
- **`QuestService.Evaluate` dulu hanya memajukan satu tahap per panggilan**; dialog yang memenuhi beberapa tahap sekaligus
  membuat quest tertahan. Sekarang diulang sampai tahap aktif belum terpenuhi.
- **Lorong sempit:** pencarian cincin `SpawnPlanner.FindOpenPoint` jatuh ke titik tengah dan musuh bertumpuk.
  `RoadAmbush` memakai `FindOpenPointApart` (grid, jaga jarak antar musuh) bila itu terjadi.
- **Bar HP musuh bawaan paket tak terbaca** (lebar 0,25 unit, menyusut dari dua sisi, hilang 1,5 detik setelah kena).
  Diganti `World/EnemyVitals`: bar di atas kepala yang menyusut dari kiri + angka damage melayang.
- **Dialog bersuara dulu menunggu rekaman selesai** sebelum ketukan diterima; sekarang ketukan selalu melanjutkan
  dan menghentikan suaranya.
- **Titik gather bisa terkurung.** `terrain.reachable` membatasi anchor ke sel yang benar-benar tersambung ke
  alun-alun (dulu 1 quarry + 2 tumpukan kayu ada di balik hutan).
- **Gerbang depan kota tertutup properti contoh peta** (peti/tiang berkolider). `terrain.open_route` +
  `terrain.clear_lane` membuka jalan utama; tembok tidak pernah disentuh.
- **Tile yang namanya salah = lubang di peta** (paket punya `Ground F4_S`, tidak punya `_W`). `terrain.check_tiles`
  memeriksa setiap nama, `terrain.drop_unknown_tiles` membuang sel `?guid` dari peta contoh.
- **Tanah ngarai (Ceruk) harus polos** — keputusan user: tile transisi bertepi membuatnya tampak bersekat-sekat.
  `build_maps.plain_earth` meratakannya. Ubin kota Greymarch **jangan diubah**, user menyukai yang asli.
- **Side quest ditawarkan orang, bukan muncul sendiri.** Quest punya `giver` (speaker key); `QuestService.IsAvailable`
  menuntut flag `quest.<id>.taken`; `UI/QuestOfferPanel` + `World/QuestOffers` (tanda di atas kepala NPC).
  Dulu jurnal penuh side quest sejak awal tanpa ada yang meminta.
- **Kurva level (keputusan user: jangan terlalu cepat):** `200 + (level-1)*180`. Musuh ikut naik lewat
  `EnemyScaling.LevelScale` (maks 1,6x HP, 1,35x damage) supaya pertarungan tetap menantang.
- **Hanya satu Unity batchmode boleh jalan di satu waktu** (lisensi). Instance project lain membuat build Oathfire
  keluar dengan kode 1 tanpa pesan jelas: tunggu sampai selesai, jangan matikan milik orang.
- **Rennfall harus rapi dan simetris (keputusan user):** ubin rata tanpa varian acak, alun-alun berbingkai, hiasan
  dipasang berpasangan (`dress_symmetrically`: tungku api di 4 sudut, panji di mulut jalan, pagar tanaman sejajar).
  Jangan menambah sebaran acak di desa.
- **Fokus quest:** `SaveData.trackedQuest`; ketuk quest di jurnal untuk diikuti panah & HUD; side quest yang baru
  diterima otomatis difokuskan. `QuestRuntime.FocusedQuest()` dipakai HUD dan `QuestGuide`.
- **Equipment 10 slot** (enum ditambah di akhir: Neck, Hands, Legs, Feet, Ring — jangan disisipkan, save menyimpan
  angka). Ikon cincin/kalung/sarung tangan/sepatu dibuat `Tools/make_gear_icons.py` → `Art/Icons` (paket tak punya).
- **Skill:** `Docs/skills.json` → `Progress/SkillBook` (ability paket, terbuka level 3–10), 2 slot di kontrol +
  dash tetap, tab Skill di buku hero. Pilihan slot di `SaveData.skillSlot1/2`.
- **Ekonomi (keputusan user: jangan melimpah):** koin quest ±45%, kontrak ±45%, hadiah dialog dipangkas; hadiah
  side quest berupa perlengkapan. Konter perlengkapan di toko Sabel sebagai tempat membelanjakan koin.
- **Panel dialog maksimal 6 pilihan** (dulu 4 — pilihan ke-5 toko Sabel tak pernah muncul).
- **Urutan gambar tilemap harus `TopRight`** (sama dengan paket). Dengan `TopLeft` sisi tebal tiap tile tergambar di
  atas tile di depannya: tanah tampak bertangga/bersekat (dilaporkan user di jalan dagang dan Greymarch).
- **Malam bisa dipanggil ulang** dari api setelah malam pertama (`NightDirector.CanCallAnotherNight`, jeda 30 dtk).
  Dulu tak ada cara memulai malam ke-2, sehingga 6 side quest (penyihir/knight/elite/beberapa malam) mustahil.
- **Side quest diserahkan kembali ke pemberi:** tahap terakhir menunggu (`quest.<id>.ready`), barang pesanan diambil
  saat diserahkan (`QuestService.HandIn`, kartu di `QuestOfferPanel.ShowHandIn`). Quest punya `giverScene` untuk panah.
- **`Tools/check_quests.py` wajib 0 masalah**: tiap tujuan harus punya sumber (dialog/hadiah/toko/titik pencarian/kode),
  penunjuk panah, dan pemberi yang ada di scene-nya. Tes asap juga menyelesaikan setiap side quest.
- **Mekanik side quest:** `World/SearchSpot` (cari benda, bisa membangkitkan penjaga), percakapan dengan
  `requiresItem`, node dialog `takeItems`, pilihan dialog `action` ("shop:sabel" membuka `UI/ShopPanel`).
- **Toko (keputusan user: jangan jual barang terlalu bagus):** `Docs/shops.json`, hanya Common + sedikit Fine, jual
  kembali 35% dari nilai. Barang Rare hanya dari quest.
- **Tes lolos ≠ tampilan benar.** Tes otomatis memotret layar (`Builds/Windows/smoke_*.png`); periksa gambarnya.

## Gaya visual (terkunci)

Cetak cukil kayu bergaya kodeks kuno: tinta hitam, kertas tulang, hijau lumut, dan satu aksen
amber yang **hanya** untuk api. Panel cinematic dibuat tanpa amber, lalu cahaya api ditambahkan
di Unity sebagai lapisan animasi. Potret melewati `Tools/oathfire_tools/print_style.py` supaya
semuanya terlihat satu set.

## Aturan konten

- Tanpa unsur keagamaan. Faksi "Barisan Abu" adalah badan negara urusan wabah, bukan gereja.
- Bahasa: voice over Inggris, teks Indonesia dan Inggris.

## Build

```
python Tools/build_content.py        # dialog, item, quest, kontrak, event -> Resources
python Tools/assign_item_icons.py    # ikon LootIcons untuk tiap item
python Tools/compose_rennfall.py     # desa
python Tools/build_maps.py           # jalan dagang + Ceruk Duri Hitam (hollow)
python Tools/compose_greymarch.py    # kota
Unity -batchmode -executeMethod Oathfire.EditorTools.OathfireBuilder.BuildScenes -quit
Unity -batchmode -executeMethod Oathfire.EditorTools.RennfallSceneBuilder.Build -quit
Unity -batchmode -executeMethod Oathfire.EditorTools.TradeRoadSceneBuilder.Build -quit
Unity -batchmode -executeMethod Oathfire.EditorTools.GreymarchSceneBuilder.Build -quit
Unity -batchmode -executeMethod Oathfire.EditorTools.HollowSceneBuilder.Build -quit
Unity -batchmode -executeMethod Oathfire.EditorTools.OathfireBuilder.BuildWindows -quit
Builds/Windows/Oathfire.exe -smoketest     # tes penuh + tangkapan layar; hasil di smoke_result.txt
Unity -batchmode -buildTarget Android -executeMethod Oathfire.EditorTools.OathfireBuilder.BuildAndroid
```

`BuildAndroid` sudah memanggil kompresi tekstur ASTC lebih dulu. Tanpa itu, aplikasi dimatikan
Android saat loading karena tekstur memakan sekitar 3,3 GB RAM.

## Rahasia

`Tools/.env` berisi API key. Jangan ditampilkan, jangan di-commit (sudah ada di `.gitignore`).
