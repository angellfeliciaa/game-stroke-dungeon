# Pulse Dungeon — integrasi AI tangan

Branch kerja pribadi: **`dev/vis`**. Untuk perubahan milik Jarvis, gunakan `git push origin dev/vis`; jangan push ke `main`.

## Menjalankan di Windows

Gunakan Python 3.10–3.12 64-bit dan Unity versi yang tercatat di `ProjectSettings/ProjectVersion.txt` (6000.4.7f1).

Dari folder project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File AI/setup.ps1
```

Setup membuat `AI/.venv`, memasang dependency, dan memeriksa model serta MediaPipe tanpa menyalakan kamera. Jika `python` bukan interpreter yang benar, tambahkan `-Python 'C:\lokasi\python.exe'`.

1. Buka `Assets/Scenes/SampleScene.unity` di Unity, lalu tekan Play.
2. Tekan **Start Journey**. Unity membuka Python otomatis; jangan menjalankan salinan Python kedua.
3. Tampilkan kepalan yang jelas dan stabil selama 3 detik. Preview kamera tampil di popup kalibrasi.
4. Setelah masuk Prologue, preview kamera kecil tetap terlihat di kanan atas. Lanjutkan dialog dengan klik. Untuk setiap pintu/chest, buka telapak terlebih dahulu, lalu tahan kepalan selama 2 detik.
5. Jika kamera berhenti, input lama tidak dapat melanjutkan aksi. Tombol **Try again** menjalankan ulang AI.
6. Setelah Prologue selesai, game memuat `Level1` yang sudah tercantum di Build Settings.
7. Di Level 1, preview kamera kecil tetap tampil di kanan atas HUD. Status di bawahnya menunjukkan pose serta confidence (contoh `FIST 82%` atau `FIST UNCLEAR`). Tahan telapak terbuka sejenak, lalu kepalkan sekali untuk setiap serangan atau untuk melanjutkan dari tempat istirahat. Beberapa frame yang kurang jelas saat tangan berubah pose tidak membatalkan squeeze; menahan kepalan tetap tidak mengulangi aksi. Buka telapak lagi sebelum squeeze berikutnya, dan tunggu `ATTACK IN PROGRESS` selesai. Jika `Level1` dibuka langsung di Editor tanpa receiver kamera dari menu, klik mouse hanya berfungsi sebagai fallback pengujian. Preview bisa disembunyikan lewat `Hide Hand Camera` pada komponen `Level1Hud` (atau `Hide Hand Camera Preview` pada `PrologManager`).

Untuk memilih kamera lain, ubah `Camera Index` pada komponen `SequenceManager` di objek `CalibrationManager`.

## macOS / Linux

```bash
bash AI/setup.sh
```

Gunakan Python 3.10–3.12. Untuk memilih interpreter: `PYTHON=python3.12 bash AI/setup.sh`. Jalankan Unity dan AI pada OS yang sama. Setup ini ditujukan untuk Unity Editor; distribusi build memerlukan penyertaan folder AI, model, dan environment Python untuk OS tujuan. Menjalankan script setup di WSL tidak membuat environment Python Windows untuk Unity Windows.

## Kontrak input

AI memakai MediaPipe untuk 21 landmark, lalu Random Forest dari `AI/model_rehab.pkl` untuk klasifikasi `palm`/`fist`. Model memerlukan 63 fitur relatif terhadap pergelangan tangan. scikit-learn dipasang pada versi 1.6.1 sesuai metadata model. Model tidak dilatih ulang.

JSON dikirim ke loopback UDP `5052`: `prediction`, `confidence`, dan `landmarks` (21 titik `{x,y,z}`, atau array kosong saat `prediction` adalah `none`). Unity menolak label, confidence, dan landmark tidak valid. Paket lebih tua dari 0,5 detik tidak bisa menjalankan aksi. Kalibrasi hanya berjalan sesudah Start Journey, dengan confidence minimal 0,75 dan toleransi perubahan 0,1. Paket hilang, telapak terbuka, atau confidence rendah mengulang durasi kalibrasi.

**Confidence adalah keyakinan klasifikasi, bukan pengukuran kekuatan fisik genggaman.** Kalibrasi ini menyimpan baseline confidence gesture; tidak mengukur gaya, ROM, atau parameter klinis.

Preview dikirim sebagai satu JPEG 320×240 per datagram pada UDP `5053`, maksimum 60.000 byte, hingga 15 fps. Unity menampilkan dan menghapus preview yang basi. Gambar tidak disimpan atau dikirim ke layanan eksternal.

`UDPReceiver` dan `SequenceManager` berada pada root `CalibrationManager` yang persisten saat berpindah scene. Prologue dan Level 1 menggunakan receiver dan preview lokal yang sama. Level 1 hanya menerima satu aksi per transisi telapak-terbuka ke kepalan setelah kalibrasi; input yang basi, tidak terkalibrasi, atau terputus tidak memicu aksi. Confidence model bukan ukuran kekuatan fisik genggaman; kalibrasi wajib selesai, tetapi baseline confidence kalibrasi tidak dipakai sebagai ambang tambahan saat gameplay. Python, socket, dan thread dibersihkan saat sesi ditutup atau kembali ke menu.

## Verifikasi

```powershell
# Model + inference pada data sintetis + MediaPipe + JPEG, tanpa kamera
AI/.venv/Scripts/python.exe AI/main.py --self-test

# Model, format paket, UDP loopback, JPEG, dan resource cleanup
AI/.venv/Scripts/python.exe -m unittest discover -s Tests -p 'test_*.py' -v

# Logika C# kalibrasi, invalid input, timeout, dan reconnect tanpa Unity
powershell -NoProfile -ExecutionPolicy Bypass -File Tests/run-gesture-tests.ps1

# Opsional: kamera asli selama 30 frame, lalu berhenti (tutup Unity Play dahulu)
AI/.venv/Scripts/python.exe AI/main.py --no-window --max-frames 30
```

Tes tanpa Unity tidak membuktikan kompilasi project, referensi Inspector saat Play Mode, atau akurasi deteksi tangan pengguna. Uji manual di Unity harus mencakup kalibrasi, preview, pintu masuk, chest/pedang sekali, pintu keluar, satu squeeze per serangan Level 1, melanjutkan dari kristal istirahat, putus kamera saat menggenggam, Try again, kembali ke menu, dan keluar Play Mode tanpa proses Python tersisa.

## Troubleshooting

- **Camera input is not ready:** jalankan setup dan lihat Console Unity untuk lokasi interpreter yang diharapkan.
- **Camera did not connect:** periksa izin kamera Windows, pilihan `Camera Index`, dan tutup aplikasi lain yang menggunakan webcam.
- **UDP startup failed:** tutup sesi game/Python lama yang memakai port 5052/5053, lalu mulai ulang Play Mode.
- **Prologue dibuka langsung:** kembali ke menu; koneksi dan kalibrasi dimulai dari SampleScene.
- **Model gagal dimuat:** jalankan setup lagi dan `--self-test`; jangan memperbarui scikit-learn secara terpisah dari versi model.
