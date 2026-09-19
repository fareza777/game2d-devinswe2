# Oathfire — catatan project

Game RPG 2D isometrik untuk Android (portrait). Ini project utama yang sedang dikerjakan.
Aturan umum folder induk ada di `..\AGENTS.md` dan berlaku penuh di sini.

## Bentuk project

- **Scene dibuat oleh kode, bukan tangan.** Jangan mengedit file `.unity` secara manual:
  jalankan `Oathfire/Build Scenes` dan `Oathfire/Build Rennfall Scene` (lihat
  `Assets/Oathfire/Editor/`). Editan manual akan tertimpa saat scene dibangun ulang.
- **Konten ditulis di `Docs/`, bukan langsung di Unity.** File `Docs/*.json` (dialog, item, quest,
  cinematic, kontrak, event dunia) berisi dua bahasa sekaligus. Jalankan `python Tools/build_content.py` untuk
  mengubahnya menjadi data runtime di `Assets/Oathfire/Resources/` beserta tabel lokalisasi.
- **Aset AI** dibuat lewat `Tools/` (Recraft untuk ilustrasi cukil kayu, Replicate/FLUX untuk
  potret dan musik, ElevenLabs untuk suara). Hasilnya di-cache, jadi menjalankan ulang tidak
  menghabiskan kredit dua kali.

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
python Tools/build_content.py          # dialog, item, quest, kontrak, event -> Resources
python Tools/compose_rennfall.py       # menyusun peta desa + pratinjau PNG
python Tools/build_maps.py             # peta jalan dagang (dipotong dari peta contoh)
Unity -batchmode -executeMethod Oathfire.EditorTools.OathfireBuilder.BuildScenes -quit
Unity -batchmode -executeMethod Oathfire.EditorTools.RennfallSceneBuilder.Build -quit
Unity -batchmode -executeMethod Oathfire.EditorTools.TradeRoadSceneBuilder.Build -quit
Unity -batchmode -buildTarget Android -executeMethod Oathfire.EditorTools.OathfireBuilder.BuildAndroid
```

`BuildAndroid` sudah memanggil kompresi tekstur ASTC lebih dulu. Tanpa itu, aplikasi dimatikan
Android saat loading karena tekstur memakan sekitar 3,3 GB RAM.

## Rahasia

`Tools/.env` berisi API key. Jangan ditampilkan, jangan di-commit (sudah ada di `.gitignore`).
