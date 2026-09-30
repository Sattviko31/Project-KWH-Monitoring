(function () {
    // Translate user-facing text only. Keep each node's original text so changing
    // languages never translates a previous translation or changes application data.
    var phrases = {
        'Dashboard': ['Dasbor', 'Dashboard'], 'History': ['Riwayat', 'History'],
        'Settings': ['Pengaturan', 'Settings'], 'Charts': ['Grafik', 'Charts'],
        'Usage': ['Penggunaan', 'Usage'], 'Usage Statistics': ['Statistik Penggunaan', 'Usage Statistics'],
        'Anomaly': ['Anomali', 'Anomaly'], 'Anomaly Logs': ['Log Anomali', 'Anomaly Logs'], 'Details': ['Detail', 'Details'],
        'Login': ['Masuk', 'Login'], 'Logout': ['Keluar', 'Log out'], 'Register': ['Daftar', 'Register'],
        'Cancel': ['Batal', 'Cancel'], 'Save': ['Simpan', 'Save'], 'Delete': ['Hapus', 'Delete'],
        'Edit': ['Ubah', 'Edit'], 'Search': ['Cari', 'Search'], 'Filter': ['Filter', 'Filter'], 'Refresh': ['Muat Ulang', 'Refresh'],
        'Export': ['Ekspor', 'Export'], 'Download': ['Unduh', 'Download'], 'Submit': ['Kirim', 'Submit'],
        'Back': ['Kembali', 'Back'], 'Confirm': ['Konfirmasi', 'Confirm'],
        'Status': ['Status', 'Status'], 'Active': ['Aktif', 'Active'], 'Inactive': ['Tidak Aktif', 'Inactive'],
        'Loading data...': ['Memuat data...', 'Loading data...'],
        'No data available': ['Data tidak tersedia.', 'No data available.'],
        'No data found': ['Data tidak ditemukan.', 'No data found.'],
        'Today': ['Hari Ini', 'Today'], 'Yesterday': ['Kemarin', 'Yesterday'],
        'This week': ['Minggu Ini', 'This Week'], 'This month': ['Bulan Ini', 'This Month'],
        'This year': ['Tahun Ini', 'This Year'], 'Hourly': ['Per Jam', 'Hourly'],
        'Daily': ['Harian', 'Daily'], 'Monthly': ['Bulanan', 'Monthly'], 'Yearly': ['Tahunan', 'Yearly'],
        'Average': ['Rata-rata', 'Average'], 'Current': ['Arus', 'Current'],
        'Voltage': ['Tegangan', 'Voltage'], 'Power': ['Daya', 'Power'],
        'Active Energy': ['Energi Aktif', 'Active Energy'], 'Reactive Energy': ['Energi Reaktif', 'Reactive Energy'],
        'Energy': ['Energi', 'Energy'], 'Consumption': ['Konsumsi', 'Consumption'],
        'Frequency': ['Frekuensi', 'Frequency'], 'Load': ['Beban', 'Load'],
        'Power Factor': ['Faktor Daya', 'Power Factor'], 'Peak Hour': ['Jam Puncak', 'Peak Hour'],
        'Peak Day': ['Hari Puncak', 'Peak Day'], 'Peak Month': ['Bulan Puncak', 'Peak Month'],
        'Financial Analysis': ['Analisis Keuangan', 'Financial Analysis'],
        'Financial Summary': ['Ringkasan Keuangan', 'Financial Summary'],
        'Estimated Cost': ['Estimasi Biaya', 'Estimated Cost'], 'Cost': ['Biaya', 'Cost'],
        'Tariff': ['Tarif', 'Tariff'], 'Budget vs Actual': ['Anggaran dan Realisasi', 'Budget vs. Actual'],
        'Projection': ['Proyeksi', 'Projection'], 'Not configured': ['Belum dikonfigurasi', 'Not configured'],
        'Not available': ['Tidak tersedia', 'Not available'], 'Excellent': ['Sangat Baik', 'Excellent'],
        'Good': ['Baik', 'Good'], 'Poor': ['Buruk', 'Poor'], 'HIGH': ['TINGGI', 'HIGH'],
        'MEDIUM': ['SEDANG', 'MEDIUM'], 'NORMAL': ['NORMAL', 'NORMAL'],
        'Device': ['Perangkat', 'Device'], 'Devices': ['Perangkat', 'Devices'],
        'Per-Device Settings': ['Pengaturan Perangkat', 'Device Settings'],
        'User Management': ['Manajemen Pengguna', 'User Management'],
        'Kelola akun pengguna lainnya. Hanya administrator utama yang dapat mengakses tab ini.': ['Kelola akun pengguna lainnya. Hanya administrator utama yang dapat mengakses tab ini.', 'Manage other user accounts. Only the master administrator can access this tab.'],
        'Alihkan Administrator Utama': ['Alihkan Administrator Utama', 'Transfer Master Administrator'],
        'Pilih akun administrator yang aktif untuk mengalihkan hak administrator utama. Setelah pengalihan, Anda tidak lagi memiliki hak tersebut.': ['Pilih akun administrator yang aktif untuk mengalihkan hak administrator utama. Setelah pengalihan, Anda tidak lagi memiliki hak tersebut.', 'Select an active administrator account to transfer master administrator privileges. You will no longer have those privileges after the transfer.'],
        'Memuat data pengguna...': ['Memuat data pengguna...', 'Loading user data...'],
        'Pilih akun administrator terlebih dahulu.': ['Pilih akun administrator terlebih dahulu.', 'Select an administrator account first.'],
        'Kapasitas Maksimum (W)': ['Kapasitas Maksimum (W)', 'Maximum Capacity (W)'],
        'Awal WBP': ['Awal WBP', 'WBP Start'], 'Akhir WBP': ['Akhir WBP', 'WBP End'],
        'Anggaran Bulanan (Rp)': ['Anggaran Bulanan (Rp)', 'Monthly Budget (IDR)'],
        'Pendapatan per Jam (Rp)': ['Pendapatan per Jam (Rp)', 'Revenue per Hour (IDR)'],
        'Batas Atas (%)': ['Batas Atas (%)', 'Upper Limit (%)'], 'Batas Bawah (%)': ['Batas Bawah (%)', 'Lower Limit (%)'],
        'Mulai': ['Mulai', 'Start'], 'Selesai': ['Selesai', 'End'], 'Mode Kendali': ['Mode Kendali', 'Control Mode'],
        'ON/OFF': ['NYALA/MATI', 'ON/OFF'], 'Pulse': ['Sesaat', 'Momentary'],
        'Konfigurasi spesifik untuk setiap perangkat': ['Pengaturan khusus untuk setiap perangkat.', 'Settings specific to each device.'],
        'Belum ada perangkat yang terdaftar.': ['Belum ada perangkat yang terdaftar.', 'No devices have been registered yet.'],
        'Manage user accounts.': ['Kelola akun pengguna.', 'Manage user accounts.'],
        'Email': ['Email', 'Email'], 'Name': ['Nama', 'Name'], 'Role': ['Peran', 'Role'],
        'Registered': ['Tanggal Pendaftaran', 'Registered'], 'Last Login': ['Terakhir Masuk', 'Last Login'],
        'Actions': ['Tindakan', 'Actions'], 'Action': ['Tindakan', 'Action'],
        'Administrator': ['Administrator', 'Administrator'], 'Operator': ['Operator', 'Operator'],
        'Viewer': ['Pengamat', 'Viewer'], 'System Activity Log': ['Log Aktivitas Sistem', 'System Activity Log'],
        'Notifications': ['Notifikasi', 'Notifications'], 'Menu': ['Menu', 'Menu'], 'Watt': ['Watt', 'Watt'],
        'Avg': ['Rata-rata', 'Avg.'], 'Capacity': ['Kapasitas', 'Capacity'],
        'Per hour': ['Per jam', 'Per hour'], 'Per day': ['Per hari', 'Per day'],
        'Per month': ['Per bulan', 'Per month'], 'Hour': ['Jam', 'Hour'], 'Day': ['Hari', 'Day'],
        'Month': ['Bulan', 'Month'], 'Date': ['Tanggal', 'Date'], 'Year': ['Tahun', 'Year'],
        'Please wait...': ['Mohon tunggu...', 'Please wait...'], 'Are you sure?': ['Apakah Anda yakin?', 'Are you sure?'],
        'This action cannot be undone.': ['Tindakan ini tidak dapat dibatalkan.', 'This action cannot be undone.'],
        'Failed to load user data.': ['Gagal memuat data pengguna.', 'Failed to load user data.'],
        'Try again.': ['Silakan coba lagi.', 'Please try again.'], 'No notifications': ['Tidak ada notifikasi.', 'No notifications.'],
        'System': ['Sistem', 'System'], 'Category': ['Kategori', 'Category'],
        'Maximum Capacity': ['Kapasitas Maksimum', 'Maximum Capacity'], 'Rate': ['Tarif', 'Rate'],
        'Start Time': ['Waktu Mulai', 'Start Time'], 'End Time': ['Waktu Selesai', 'End Time'],
        'Total': ['Total', 'Total'], 'Grand Total': ['Jumlah Keseluruhan', 'Grand Total'],
        'Description': ['Deskripsi', 'Description'], 'Password': ['Kata Sandi', 'Password'],
        'Confirm Password': ['Konfirmasi Kata Sandi', 'Confirm Password'],
        'Forgot Password?': ['Lupa kata sandi?', 'Forgot password?'],
        'Create Account': ['Buat Akun', 'Create Account'], 'Reset Password': ['Atur Ulang Kata Sandi', 'Reset Password'],
        'Verification successful!': ['Verifikasi Berhasil!', 'Verification Successful!'],
        'Your email verification is complete': ['Verifikasi alamat email Anda telah selesai.', 'Your email address has been verified.'],
        'Access Denied': ['Akses Ditolak', 'Access Denied'], 'An error occurred.': ['Terjadi kesalahan.', 'An error occurred.'],
        'Database Error': ['Kesalahan Basis Data', 'Database Error'], 'Cos Phi': ['Cos Phi', 'Cos Phi'],
        'ON ALL': ['NYALAKAN SEMUA', 'TURN ALL ON'], 'POWER': ['DAYA', 'POWER'],
        'Pengaturan Device': ['Pengaturan Perangkat', 'Device Settings'],
        'Konfigurasi spesifik untuk setiap device': ['Pengaturan khusus untuk setiap perangkat.', 'Settings specific to each device.'],
        'Belum ada device yang terdaftar.': ['Belum ada perangkat yang terdaftar.', 'No devices have been registered yet.'],
        'Kelola akun pengguna lain. Hanya master admin yang dapat mengakses tab ini.': ['Kelola akun pengguna lain. Hanya administrator utama yang dapat mengakses tab ini.', 'Manage other user accounts. Only the master administrator can access this tab.'],
        'Pindahkan Master Admin': ['Alihkan Administrator Utama', 'Transfer Master Administrator'],
        'Pilih user Admin aktif untuk memindahkan hak master admin. Setelah dipindahkan, Anda akan kehilangan akses master admin.': ['Pilih administrator aktif untuk mengalihkan hak administrator utama. Setelah pengalihan, Anda tidak lagi memiliki hak tersebut.', 'Select an active administrator to transfer master administrator privileges. You will lose those privileges after the transfer.'],
        'Yakin ingin menghapus akun': ['Apakah Anda yakin ingin menghapus akun', 'Are you sure you want to delete the account'],
        'Memuat data user...': ['Memuat data pengguna...', 'Loading user data...'],
        'Akses Disetujui': ['Akses Disetujui', 'Access Approved'],
        'Persetujuan berhasil diproses': ['Permintaan akses berhasil diproses.', 'The access request was processed successfully.'],
        'Setujui atau tolak permintaan akses pengguna baru': ['Setujui atau tolak permintaan akses dari pengguna baru.', 'Approve or reject the new user access request.'],
        'Akses Ditolak': ['Akses Ditolak', 'Access Denied'],
        'Permintaan akses telah ditolak': ['Permintaan akses telah ditolak.', 'The access request has been rejected.'],
        'Kembali ke Dashboard': ['Kembali ke Dasbor', 'Back to the dashboard'],
        'Last Update:': ['Pembaruan terakhir:', 'Last updated:'],
        'Rescan': ['Pindai Ulang', 'Rescan'], 'Panel': ['Panel', 'Panel'],
        'All': ['Semua', 'All'], 'Single': ['Satu', 'Single'], 'Aggregate': ['Agregat', 'Aggregate'],
        'Aggregation': ['Agregasi', 'Aggregation'], 'Metric': ['Metrik', 'Metric'],
        'Budget': ['Anggaran', 'Budget'], 'Actual': ['Realisasi', 'Actual'],
        'Report': ['Laporan', 'Report'], 'Chart': ['Grafik', 'Chart'], 'Financial': ['Keuangan', 'Financial'],
        'Revenue': ['Pendapatan', 'Revenue'], 'Threshold': ['Ambang Batas', 'Threshold'],
        'Load Factor': ['Faktor Beban', 'Load Factor'], 'Unit Economics': ['Ekonomi per Unit', 'Unit Economics'],
        'Anomaly Cost Impact': ['Dampak Biaya Anomali', 'Anomaly Cost Impact'],
        'Waste': ['Pemborosan', 'Waste'], 'Single Device': ['Satu Perangkat', 'Single Device'],
        'All Devices': ['Semua Perangkat', 'All Devices'], 'Single Panel': ['Satu Panel', 'Single Panel'],
        'All Panels': ['Semua Panel', 'All Panels'],
        'Metode': ['Metode', 'Method'], 'Kegunaan': ['Kegunaan', 'Purpose'],
        'Field': ['Field', 'Field'], 'Rumus': ['Rumus', 'Formula'], 'Komponen': ['Komponen', 'Component'],
        'Cara Baca': ['Cara Membaca', 'How to Read'], 'Kondisi': ['Kondisi', 'Condition'],
        'Indikasi': ['Indikasi', 'What It Indicates'], 'Saran Tindakan': ['Saran Tindakan', 'Recommended Action'],
        'Cara Membaca': ['Cara Membaca', 'How to Read'],
        'Data jam-jaman hari ini.': ['Data per jam untuk hari ini.', "Today's hourly data."],
        'Data harian dalam bulan ini.': ['Data per hari untuk bulan ini.', 'Daily data for this month.'],
        'Data bulanan dalam tahun ini.': ['Data per bulan untuk tahun ini.', 'Monthly data for this year.'],
        'Tarif flat default per kWh (fallback Rp 1.500).': ['Tarif tetap bawaan per kWh (nilai pengganti Rp1.500).', 'Default flat rate per kWh (fallback: IDR 1,500).'],
        'Proyeksi tagihan akhir bulan.': ['Perkiraan tagihan pada akhir bulan.', 'Estimated bill at the end of the month.'],
        'Dampak biaya dari anomali OVERLOAD.': ['Dampak biaya akibat anomali beban berlebih.', 'Cost impact of an OVERLOAD anomaly.'],
        'Konsumsi saat downtime.': ['Konsumsi energi selama waktu henti.', 'Energy consumption during downtime.'],
        'Budget kWh vs aktual.': ['Anggaran energi (kWh) dibandingkan dengan realisasi.', 'Energy budget (kWh) compared with actual usage.'],
        'Tanya Voltra AI Assistant': ['Tanya Asisten AI Voltra', 'Ask Voltra AI'],
        'Voltra AI Assistant': ['Asisten AI Voltra', 'Voltra AI Assistant'],
        'Minimize': ['Perkecil', 'Minimize'], 'Hapus Percakapan': ['Hapus Percakapan', 'Clear Conversation'],
        'Kirim Pesan': ['Kirim Pesan', 'Send Message'],
        'Expand/Collapse': ['Perluas/Ciutkan', 'Expand or collapse'],
        'Toggle': ['Alihkan', 'Toggle'], 'Pulse ON': ['Aktifkan sesaat', 'Pulse on'],
        'Pulse OFF': ['Matikan sesaat', 'Pulse off'], 'Turn ON': ['Nyalakan', 'Turn on'],
        'Turn OFF': ['Matikan', 'Turn off'], 'Relay 1': ['Relai 1', 'Relay 1'], 'Relay 2': ['Relai 2', 'Relay 2'],
        'Toggle All': ['Alihkan semua', 'Toggle all'], 'Toggle filter': ['Tampilkan atau sembunyikan filter', 'Show or hide filters'],
        'Toggle filter perangkat': ['Tampilkan atau sembunyikan filter perangkat', 'Show or hide device filters'],
        'Reset filter': ['Atur ulang filter', 'Reset filter'], 'Export CSV': ['Ekspor CSV', 'Export CSV'],
        'Export Excel': ['Ekspor Excel', 'Export Excel'], 'Close': ['Tutup', 'Close'],
        'Tampilkan password': ['Tampilkan kata sandi', 'Show password'],
        'Masukkan password': ['Masukkan kata sandi', 'Enter your password'],
        'Ulangi password': ['Ulangi kata sandi', 'Re-enter your password'],
        'Ulangi password baru': ['Ulangi kata sandi baru', 'Re-enter the new password'],
        'Minimal 8 karakter': ['Minimal 8 karakter', 'At least 8 characters'],
        'Cari panel...': ['Cari panel...', 'Search panels...'],
        'Hari sebelumnya': ['Hari sebelumnya', 'Previous day'], 'Hari berikutnya': ['Hari berikutnya', 'Next day'],
        'Operator Teraktif': ['Operator Paling Aktif', 'Most Active Operator'],
        'Tambahkan keterangan opsional...': ['Tambahkan catatan (opsional)...', 'Add an optional note...'],
        'Alamat': ['Alamat', 'Address'], 'Kota': ['Kota', 'City'], 'Lokasi': ['Lokasi', 'Location'],
        'Ganti Tema': ['Ganti Tema', 'Change theme'],
        'Buka panduan aplikasi': ['Buka panduan aplikasi', 'Open the application guide'],
        'Panduan dan catatan pembaruan': ['Panduan dan catatan pembaruan', 'Guide and release notes'],
        'Online 💭 KWH Monitoring Support': ['Daring 💭 Bantuan KWH Monitoring', 'Online 💭 KWH Monitoring Support'],
        'Saya Voltra, asisten AI KWH Monitoring. Tanyakan tentang data listrik, panel, tagihan, atau anomali.': ['Saya Voltra, asisten AI KWH Monitoring. Tanyakan tentang data listrik, panel, tagihan, atau anomali.', 'I am Voltra, the KWH Monitoring AI assistant. Ask me about electricity data, panels, bills, or anomalies.'],
        'Panel Boros': ['Panel dengan Konsumsi Tertinggi', 'Highest-Consumption Panel'],
        'Tagihan': ['Estimasi Tagihan', 'Estimated Bill'], 'Daya Total': ['Total Daya', 'Total Power'],
        'Mana panel paling boros?': ['Panel mana yang konsumsinya paling tinggi?', 'Which panel has the highest energy consumption?'],
        'Berapa tagihan bulan ini?': ['Berapa estimasi tagihan bulan ini?', "What is this month's estimated bill?"],
        'Berapa daya total sekarang?': ['Berapa total daya saat ini?', 'What is the total power now?'],
        'Ada anomali apa saja?': ['Anomali apa saja yang terdeteksi?', 'What anomalies have been detected?'],
        'Selamat Datang!': ['Selamat datang!', 'Welcome!'],
        'Voltra sedang mengetik...': ['Voltra sedang menyiapkan jawaban...', 'Voltra is preparing a response...'],
        'Ketik pertanyaan Anda...': ['Ketik pertanyaan Anda...', 'Type your question...'],
        'Anda tidak memiliki izin untuk mengakses halaman ini': ['Anda tidak memiliki izin untuk mengakses halaman ini.', 'You are not authorized to access this page.'],
        'Lupa Kata Sandi?': ['Lupa Kata Sandi?', 'Forgot your password?'],
        'Masukkan alamat email Anda. Kami akan mengirimkan tautan untuk mengatur ulang kata sandi.': ['Masukkan alamat email Anda. Kami akan mengirimkan tautan untuk mengatur ulang kata sandi.', 'Enter your email address. We will send you a password reset link.'],
        'Ingat kata sandi Anda?': ['Ingat kata sandi Anda?', 'Remember your password?'],
        'Kembali ke halaman masuk': ['Kembali ke halaman masuk', 'Back to sign in'],
        'Periksa Email Anda': ['Periksa Email Anda', 'Check your email'],
        'Link reset password telah dikirim': ['Tautan pengaturan ulang kata sandi telah dikirim.', 'A password reset link has been sent.'],
        'Selamat Datang': ['Selamat Datang', 'Welcome'],
        'Masuk ke akun KWH Monitoring Anda': ['Masuk ke akun KWH Monitoring Anda.', 'Sign in to your KWH Monitoring account.'],
        'Ingat saya': ['Ingat saya', 'Remember me'],
        'Belum punya akun?': ['Belum memiliki akun?', "Don't have an account?"],
        'Daftar sekarang': ['Daftar sekarang', 'Register now'], 'Buat Akun': ['Buat Akun', 'Create an account'],
        'Daftar gratis untuk mulai memantau konsumsi energi': ['Daftar secara gratis untuk mulai memantau konsumsi energi.', 'Register for free to start monitoring energy use.'],
        'Nama Lengkap': ['Nama Lengkap', 'Full Name'], 'Konfirmasi Kata Sandi': ['Konfirmasi Kata Sandi', 'Confirm Password'],
        'Sudah memiliki akun?': ['Sudah memiliki akun?', 'Already have an account?'],
        'Masuk ke akun Anda': ['Masuk ke akun Anda', 'Sign in to your account'],
        'Buat password baru yang kuat untuk melindungi akun Anda': ['Buat kata sandi baru yang kuat untuk melindungi akun Anda.', 'Create a strong new password to protect your account.'],
        'Kata Sandi Baru': ['Kata Sandi Baru', 'New Password'], 'Konfirmasi Kata Sandi Baru': ['Konfirmasi Kata Sandi Baru', 'Confirm New Password'],
        'Tinggal satu langkah lagi': ['Tinggal satu langkah lagi.', 'Just one more step.'],
        'Akun Anda kini lebih aman': ['Akun Anda kini lebih aman.', 'Your account is now more secure.'],
        'Role:': ['Peran:', 'Role:'], 'Tanggal Daftar': ['Tanggal Pendaftaran', 'Registration Date'],
        'Pilih Role': ['Pilih Peran', 'Select a Role'], 'Pilih role...': ['Pilih peran...', 'Select a role...'],
        'Viewer (Hanya melihat)': ['Pengamat (hanya dapat melihat)', 'Viewer (read-only)'],
        'Operator (Dapat mengontrol device)': ['Operator (dapat mengendalikan perangkat)', 'Operator (can control devices)'],
        'Full Name': ['Nama Lengkap', 'Full Name'],
        'Energy (kWh)': ['Energi (kWh)', 'Energy (kWh)'], 'Power (W)': ['Daya (W)', 'Power (W)'],
        'Upper Threshold': ['Ambang Batas Atas', 'Upper Threshold'], 'Lower Threshold': ['Ambang Batas Bawah', 'Lower Threshold'],
        'Max Capacity': ['Kapasitas Maksimum', 'Maximum Capacity'], 'Actual (kWh)': ['Realisasi (kWh)', 'Actual (kWh)'],
        'Projected (kWh)': ['Proyeksi (kWh)', 'Projected (kWh)'], 'Cumulative Actual': ['Realisasi Kumulatif', 'Cumulative Actual'],
        'Cumulative Projection': ['Proyeksi Kumulatif', 'Cumulative Projection'],
        'Budget Pace': ['Laju Anggaran', 'Budget Pace'], 'Data Point': ['Titik Data', 'Data Point'],
        'Tautan Pengaturan Ulang Kata Sandi Telah Dikirim': ['Tautan Pengaturan Ulang Kata Sandi Telah Dikirim', 'Password Reset Link Sent'],
        'Konfirmasi Terkirim': ['Konfirmasi Terkirim', 'Confirmation Sent'],
        'Petunjuk pengaturan ulang kata sandi telah dikirim ke alamat email Anda.': ['Petunjuk pengaturan ulang kata sandi telah dikirim ke alamat email Anda.', 'Instructions to reset your password have been sent to your email address.'],
        'Buka tautan dalam email untuk membuat kata sandi baru.': ['Buka tautan dalam email untuk membuat kata sandi baru.', 'Open the link in the email to create a new password.'],
        'Jika email tidak diterima, periksa folder spam atau ajukan permintaan kembali.': ['Jika email tidak diterima, periksa folder spam atau ajukan permintaan kembali.', 'If you do not receive the email, check your spam folder or submit another request.'],
        'Tautan pengaturan ulang kata sandi telah dikirim.': ['Tautan pengaturan ulang kata sandi telah dikirim.', 'A password reset link has been sent.'],
        'Jika alamat email Anda terdaftar, petunjuk pengaturan ulang kata sandi telah kami kirim ke kotak masuk Anda.': ['Jika alamat email Anda terdaftar, petunjuk pengaturan ulang kata sandi telah kami kirim ke kotak masuk Anda.', 'If your email address is registered, we have sent password reset instructions to your inbox.'],
        'Tidak menerima email? Periksa folder spam atau ajukan permintaan kembali dalam beberapa menit.': ['Tidak menerima email? Periksa folder spam atau ajukan permintaan kembali dalam beberapa menit.', 'Did not receive the email? Check your spam folder or submit another request in a few minutes.'],
        'Kata Sandi Berhasil Diatur Ulang': ['Kata Sandi Berhasil Diatur Ulang', 'Password Reset Successful'],
        'Kata Sandi Berhasil Diatur Ulang!': ['Kata Sandi Berhasil Diatur Ulang!', 'Password Reset Successful!'],
        'Kata sandi akun Anda telah diperbarui.': ['Kata sandi akun Anda telah diperbarui.', 'Your account password has been updated.'],
        'Gunakan kata sandi baru untuk masuk.': ['Gunakan kata sandi baru untuk masuk.', 'Use your new password to sign in.'],
        'Silakan masuk menggunakan kata sandi baru Anda.': ['Silakan masuk menggunakan kata sandi baru Anda.', 'Please sign in with your new password.'],
        'Tautan pengaturan ulang sebelumnya tidak dapat digunakan kembali.': ['Tautan pengaturan ulang sebelumnya tidak dapat digunakan kembali.', 'The previous reset link can no longer be used.'],
        'Pendaftaran Berhasil': ['Pendaftaran Berhasil', 'Registration Successful'],
        'Pendaftaran akun telah diterima oleh sistem.': ['Pendaftaran akun telah diterima oleh sistem.', 'Your account registration has been received.'],
        'Langkah berikutnya, periksa email untuk membuka tautan verifikasi yang berlaku selama 24 jam.': ['Langkah berikutnya, periksa email untuk membuka tautan verifikasi yang berlaku selama 24 jam.', 'Next, check your email and open the verification link, which is valid for 24 hours.'],
        'Setelah verifikasi, tunggu persetujuan administrator utama sebelum masuk.': ['Setelah verifikasi, tunggu persetujuan administrator utama sebelum masuk.', 'After verification, wait for approval from the primary administrator before signing in.'],
        'Periksa kotak masuk Anda, lalu buka tautan verifikasi untuk melanjutkan.': ['Periksa kotak masuk Anda, lalu buka tautan verifikasi untuk melanjutkan.', 'Check your inbox and open the verification link to continue.'],
        'Setelah verifikasi, administrator utama akan meninjau permintaan akses Anda.': ['Setelah verifikasi, administrator utama akan meninjau permintaan akses Anda.', 'After verification, the primary administrator will review your access request.'],
        'Alamat email telah dikonfirmasi dan permintaan akses dikirim kepada administrator utama.': ['Alamat email telah dikonfirmasi dan permintaan akses dikirim kepada administrator utama.', 'Your email address has been confirmed and your access request has been sent to the primary administrator.'],
        'Langkah berikutnya, tunggu persetujuan administrator yang akan disampaikan melalui email.': ['Langkah berikutnya, tunggu persetujuan administrator yang akan disampaikan melalui email.', 'Next, wait for the administrator’s decision, which will be sent by email.'],
        'Setelah disetujui, masuk sesuai peran yang diberikan (Pengamat atau Operator).': ['Setelah disetujui, masuk sesuai peran yang diberikan (Pengamat atau Operator).', 'Once approved, sign in with your assigned role (Viewer or Operator).'],
        'Permintaan akses Anda telah dikirim kepada administrator utama untuk diproses.': ['Permintaan akses Anda telah dikirim kepada administrator utama untuk diproses.', 'Your access request has been sent to the primary administrator for review.'],
        'Anda akan menerima email konfirmasi setelah administrator menyetujui akses Anda.': ['Anda akan menerima email konfirmasi setelah administrator menyetujui akses Anda.', 'You will receive a confirmation email after the administrator approves your access.'],
        'Ke Halaman Masuk': ['Ke Halaman Masuk', 'Go to Sign In'],
        'Masuk ke sistem pemantauan menggunakan alamat email dan kata sandi akun Anda.': ['Masuk ke sistem pemantauan menggunakan alamat email dan kata sandi akun Anda.', 'Sign in to the monitoring system with your account email address and password.'],
        'Pengguna baru perlu mendaftar, memverifikasi alamat email, lalu menunggu persetujuan administrator utama.': ['Pengguna baru perlu mendaftar, memverifikasi alamat email, lalu menunggu persetujuan administrator utama.', 'New users must register, verify their email address, and wait for approval from the primary administrator.'],
        'Sesi berlaku selama 30 menit dan diperpanjang secara otomatis.': ['Sesi berlaku selama 30 menit dan diperpanjang secara otomatis.', 'Sessions last 30 minutes and are extended automatically.'],
        'Masukkan kata sandi': ['Masukkan kata sandi', 'Enter your password'],
        'Tampilkan kata sandi': ['Tampilkan kata sandi', 'Show password'],
        'Ulangi kata sandi': ['Ulangi kata sandi', 'Re-enter your password'],
        'Ulangi kata sandi baru': ['Ulangi kata sandi baru', 'Re-enter your new password'],
        'Buat kata sandi baru yang kuat untuk melindungi akun Anda.': ['Buat kata sandi baru yang kuat untuk melindungi akun Anda.', 'Create a strong new password to protect your account.'],
        'Buat akun untuk mengakses sistem pemantauan.': ['Buat akun untuk mengakses sistem pemantauan.', 'Create an account to access the monitoring system.'],
        'Alur pendaftaran: isi data, verifikasi alamat email, lalu tunggu persetujuan administrator utama.': ['Alur pendaftaran: isi data, verifikasi alamat email, lalu tunggu persetujuan administrator utama.', 'Registration steps: provide your details, verify your email address, and wait for approval from the primary administrator.'],
        'Kata sandi disimpan dalam bentuk hash (PBKDF2-HMACSHA256, 100.000 iterasi) dan tidak disimpan sebagai teks biasa.': ['Kata sandi disimpan dalam bentuk hash (PBKDF2-HMACSHA256, 100.000 iterasi) dan tidak disimpan sebagai teks biasa.', 'Passwords are stored as hashes (PBKDF2-HMACSHA256, 100,000 iterations) and are never stored as plain text.'],
        'Kirim tautan untuk mengatur ulang kata sandi ke alamat email terdaftar.': ['Kirim tautan untuk mengatur ulang kata sandi ke alamat email terdaftar.', 'Send a password reset link to the registered email address.'],
        'Tautan bersifat rahasia, memiliki masa berlaku terbatas, dan hanya dapat digunakan satu kali.': ['Tautan bersifat rahasia, memiliki masa berlaku terbatas, dan hanya dapat digunakan satu kali.', 'The link is confidential, expires after a limited time, and can only be used once.'],
        'Email pemberitahuan tidak akan dikirim jika alamat tersebut tidak terdaftar di sistem.': ['Email pemberitahuan tidak akan dikirim jika alamat tersebut tidak terdaftar di sistem.', 'A notification email will not be sent if the address is not registered in the system.'],
        'Anda tidak memiliki izin untuk membuka halaman atau menggunakan fitur ini.': ['Anda tidak memiliki izin untuk membuka halaman atau menggunakan fitur ini.', 'You do not have permission to open this page or use this feature.'],
        'Peran akun Anda tidak mencakup izin yang diminta.': ['Peran akun Anda tidak mencakup izin yang diminta.', 'Your account role does not include the requested permission.'],
        'Hubungi administrator utama jika Anda memerlukan izin tersebut.': ['Hubungi administrator utama jika Anda memerlukan izin tersebut.', 'Contact the primary administrator if you need this permission.'],
        'Anda tidak memiliki izin untuk mengelola akun. Hanya administrator utama yang dapat mengakses fitur ini.': ['Anda tidak memiliki izin untuk mengelola akun. Hanya administrator utama yang dapat mengakses fitur ini.', 'You do not have permission to manage accounts. Only the primary administrator can access this feature.'],
        'Gagal memuat data pengguna.': ['Gagal memuat data pengguna.', 'Failed to load user data.'],
        'Gagal menghapus akun pengguna.': ['Gagal menghapus akun pengguna.', 'Failed to delete the user account.'],
        'PERINGATAN: Anda akan mengalihkan hak administrator utama kepada': ['PERINGATAN: Anda akan mengalihkan hak administrator utama kepada', 'WARNING: You are about to transfer primary administrator privileges to'],
        'Setelah pengalihan, Anda tidak lagi memiliki hak tersebut. Hak administrator utama hanya dapat dipulihkan oleh administrator utama yang baru.': ['Setelah pengalihan, Anda tidak lagi memiliki hak tersebut. Hak administrator utama hanya dapat dipulihkan oleh administrator utama yang baru.', 'After the transfer, you will no longer have these privileges. Only the new primary administrator can restore them.'],
        'Gagal mengalihkan hak administrator utama.': ['Gagal mengalihkan hak administrator utama.', 'Failed to transfer primary administrator privileges.'],
        'Alamat email administrator yang menerima permintaan verifikasi pendaftaran pengguna baru. Pengaturan ini hanya dapat diubah oleh administrator utama melalui Manajemen Pengguna.': ['Alamat email administrator yang menerima permintaan verifikasi pendaftaran pengguna baru. Pengaturan ini hanya dapat diubah oleh administrator utama melalui Manajemen Pengguna.', 'The administrator email address that receives new user registration verification requests. Only the primary administrator can change this setting through User Management.'],
        'Kirim Tautan Pengaturan Ulang': ['Kirim Tautan Pengaturan Ulang', 'Send Reset Link'],
        'Menolak permintaan akses dari pengguna yang baru mendaftar.': ['Menolak permintaan akses dari pengguna yang baru mendaftar.', 'Rejects an access request from a newly registered user.'],
        'Akun akan dinonaktifkan dan tidak dapat digunakan untuk masuk.': ['Akun akan dinonaktifkan dan tidak dapat digunakan untuk masuk.', 'The account will be deactivated and will no longer be able to sign in.'],
        'Halaman ini dilindungi oleh token dari email dan token anti-pemalsuan permintaan.': ['Halaman ini dilindungi oleh token dari email dan token anti-pemalsuan permintaan.', 'This page is protected by an email token and an anti-forgery token.'],
        'Halaman khusus administrator utama untuk menyetujui pendaftaran pengguna baru.': ['Halaman khusus administrator utama untuk menyetujui pendaftaran pengguna baru.', 'This page is for the primary administrator to approve new user registrations.'],
        'Pilih peran (Pengamat atau Operator) bagi pengguna yang disetujui.': ['Pilih peran (Pengamat atau Operator) bagi pengguna yang disetujui.', 'Select a role (Viewer or Operator) for the approved user.'],
        'Halaman ini dilindungi oleh token dari email dan token anti-pemalsuan permintaan; akun administrator tidak dapat disetujui melalui halaman ini.': ['Halaman ini dilindungi oleh token dari email dan token anti-pemalsuan permintaan; akun administrator tidak dapat disetujui melalui halaman ini.', 'This page is protected by an email token and an anti-forgery token; administrator accounts cannot be approved here.'],
        'Pengguna': ['Pengguna', 'User'], 'Perangkat': ['Perangkat', 'Device'],
        'Tautan': ['Tautan', 'Link'], 'Administrator Utama': ['Administrator Utama', 'Primary Administrator'],
        'Gangguan Perangkat': ['Gangguan Perangkat', 'Device Failure'], 'Kehilangan Pendapatan': ['Kehilangan Pendapatan', 'Revenue Loss'],
        'Tingkat Keparahan': ['Tingkat Keparahan', 'Severity'], 'Kritis': ['Kritis', 'Critical'],
        'Tinggi': ['Tinggi', 'High'], 'Rendah': ['Rendah', 'Low'], 'Terbuka': ['Terbuka', 'Open'],
        'Selesaikan': ['Selesaikan', 'Resolve'], 'Sudah Diselesaikan': ['Sudah Diselesaikan', 'Resolved'],
        'Konfirmasi Penanganan': ['Konfirmasi Penanganan', 'Acknowledge'],
        'Sangat Baik': ['Sangat Baik', 'Excellent'], 'Pemborosan': ['Pemborosan', 'Waste'],
        'Faktor Beban': ['Faktor Beban', 'Load Factor'], 'Ringkasan Keuangan': ['Ringkasan Keuangan', 'Financial Summary'],
        'Biaya per Unit': ['Biaya per Unit', 'Unit Cost'], 'Melebihi Anggaran': ['Melebihi Anggaran', 'Over Budget'],
        'Peringatan Seketika': ['Peringatan Seketika', 'Instant Alert'],
        'Laporan Per Jam': ['Laporan Per Jam', 'Hourly Report'], 'Laporan Harian': ['Laporan Harian', 'Daily Report'],
        'Laporan Bulanan': ['Laporan Bulanan', 'Monthly Report'], 'Tampilkan Garis EMA': ['Tampilkan Garis EMA', 'Show EMA Line'],
        'Tampilkan Garis Ambang Batas': ['Tampilkan Garis Ambang Batas', 'Show Threshold Lines'],
        '100 Titik Data Awal': ['100 Titik Data Awal', '100 Initial Points'],
        'Derau': ['Derau', 'Noise'], 'Nilai Acuan': ['Nilai Acuan', 'Baseline'],
        'Waktu Henti': ['Waktu Henti', 'Downtime'], 'Persentase Beban': ['Persentase Beban', 'Load Percentage'],
        'Generate': ['Buat', 'Generate'], 'Generate Report': ['Buat Laporan', 'Generate Report'],
        'Anomaly Center': ['Pusat Anomali', 'Anomaly Center'], 'Auto Refresh': ['Muat Ulang Otomatis', 'Auto Refresh'],
        'Clear All': ['Hapus Semua', 'Clear All'], 'Resolve': ['Selesaikan', 'Resolve'],
        'Acknowledge': ['Konfirmasi Penanganan', 'Acknowledge'], 'Acknowledged': ['Telah Ditangani', 'Acknowledged'],
        'Resolved': ['Selesai', 'Resolved'], 'Unresolved': ['Belum Diselesaikan', 'Unresolved'],
        'All Charts': ['Semua Grafik', 'All Charts'], 'Chart Display Settings': ['Pengaturan Tampilan Grafik', 'Chart Display Settings'],
        'Show Grid': ['Tampilkan Kisi', 'Show Grid'], 'System Settings': ['Pengaturan Sistem', 'System Settings'],
        'Generated by': ['Dibuat oleh', 'Generated by'], 'No recurring causes were recorded.': ['Tidak ada penyebab berulang yang tercatat.', 'No recurring causes were recorded.'],
        'Peringatan Langsung': ['Peringatan Langsung', 'Instant Alert'],
        'Gagal menyelesaikan anomali.': ['Gagal menyelesaikan anomali.', 'Failed to resolve the anomaly.'],
        'Gagal menyelesaikan anomali:': ['Gagal menyelesaikan anomali:', 'Failed to resolve the anomaly:'],
        'Gagal mengonfirmasi penanganan anomali.': ['Gagal mengonfirmasi penanganan anomali.', 'Failed to acknowledge anomaly handling.'],
        'Gagal mengonfirmasi penanganan anomali:': ['Gagal mengonfirmasi penanganan anomali:', 'Failed to acknowledge anomaly handling:'],
        'Kesalahan tidak diketahui.': ['Kesalahan tidak diketahui.', 'An unknown error occurred.'],
        'Gagal membuat laporan.': ['Gagal membuat laporan.', 'Failed to generate the report.'],
        'Laporan gagal dibuat. Silakan muat ulang periode ini dan coba kembali.': ['Laporan gagal dibuat. Silakan muat ulang periode ini dan coba kembali.', 'The report could not be generated. Reload this period and try again.'],
        'Gagal memuat laporan': ['Gagal memuat laporan.', 'Failed to load the report.'],
        'Tidak ada penyebab berulang yang tercatat.': ['Tidak ada penyebab berulang yang tercatat.', 'No recurring causes were recorded.'],
        'Dokumentasi Perhitungan Statistik Penggunaan': ['Dokumentasi Perhitungan Statistik Penggunaan', 'Usage Statistics Calculation Guide'],
        'Kembali ke Statistik Penggunaan': ['Kembali ke Statistik Penggunaan', 'Back to Usage Statistics'],
        'Titik Akhir': ['Titik Akhir', 'Endpoint'], 'Properti': ['Properti', 'Property'],
        'A. Titik Akhir API Utama': ['A. Titik Akhir API Utama', 'A. Main API Endpoints'],
        'B. Parameter Request': ['B. Parameter Permintaan', 'B. Request Parameters'],
        'C. Properti Utama yang Dikembalikan API': ['C. Properti Utama yang Dikembalikan API', 'C. Main Properties Returned by the API'],
        '1. Gambaran Umum': ['1. Gambaran Umum', '1. Overview'], '2. Sumber Data': ['2. Sumber Data', '2. Data Sources'],
        '3. Pengolahan Data': ['3. Pengolahan Data', '3. Data Processing'],
        '4. Daftar Perhitungan Lengkap': ['4. Daftar Perhitungan Lengkap', '4. Complete Calculation Reference'],
        '4.2 Total Biaya': ['4.2 Total Biaya', '4.2 Total Cost'], '4.3 Tarif Rata-Rata': ['4.3 Tarif Rata-Rata', '4.3 Average Tariff'],
        '4.4 Pembagian WBP dan LWBP': ['4.4 Pembagian WBP dan LWBP', '4.4 WBP and LWBP Breakdown'],
        '4.5 Anggaran dan Realisasi': ['4.5 Anggaran dan Realisasi', '4.5 Budget and Actual Usage'],
        '4.6 Perbandingan Periode (Bulanan dan Tahunan)': ['4.6 Perbandingan Periode (Bulanan dan Tahunan)', '4.6 Period Comparison (Monthly and Yearly)'],
        '4.7 Proyeksi Tagihan': ['4.7 Proyeksi Tagihan', '4.7 Bill Projection'],
        '4.8 Proyeksi Biaya Bulanan': ['4.8 Proyeksi Biaya Bulanan', '4.8 Monthly Cost Projection'],
        '4.9 Proyeksi Biaya Tahunan': ['4.9 Proyeksi Biaya Tahunan', '4.9 Yearly Cost Projection'],
        '4.10 Proyeksi Energi Bulanan': ['4.10 Proyeksi Energi Bulanan', '4.10 Monthly Energy Projection'],
        '4.11 Proyeksi Energi Tahunan': ['4.11 Proyeksi Energi Tahunan', '4.11 Yearly Energy Projection'],
        '4.12 Faktor Beban': ['4.12 Faktor Beban', '4.12 Load Factor'], '4.13 Biaya per Unit': ['4.13 Biaya per Unit', '4.13 Unit Cost'],
        '4.14 Dampak Biaya Anomali': ['4.14 Dampak Biaya Anomali', '4.14 Anomaly Cost Impact'],
        '4.15 Pemborosan Energi': ['4.15 Pemborosan Energi', '4.15 Energy Waste'],
        '4.16 Laporan Harian, Bulanan, dan Tahunan': ['4.16 Laporan Harian, Bulanan, dan Tahunan', '4.16 Daily, Monthly, and Yearly Reports'],
        '5. Cara Membaca Hasil': ['5. Cara Membaca Hasil', '5. How to Read the Results'],
        '6. Matriks Keputusan dan Saran Tindakan': ['6. Matriks Keputusan dan Saran Tindakan', '6. Decision Matrix and Recommended Actions'],
        '7. Catatan Validasi dan Keterbatasan': ['7. Catatan Validasi dan Keterbatasan', '7. Validation Notes and Limitations'],
        'Perangkat representatif': ['Perangkat representatif', 'Representative device'],
        'Data per jam untuk hari ini.': ['Data per jam untuk hari ini.', "Today's hourly data."],
        'Data per hari untuk bulan ini.': ['Data per hari untuk bulan ini.', 'Daily data for this month.'],
        'Data per bulan untuk tahun ini.': ['Data per bulan untuk tahun ini.', 'Monthly data for this year.'],
        'Konsumsi energi selama waktu henti.': ['Konsumsi energi selama waktu henti.', 'Energy consumption during downtime.'],
        'Perbandingan anggaran energi dengan realisasi.': ['Perbandingan anggaran energi dengan realisasi.', 'Energy budget compared with actual usage.'],
        'Dampak biaya akibat anomali beban berlebih.': ['Dampak biaya akibat anomali beban berlebih.', 'Cost impact of an excessive-load anomaly.'],
        'Faktor beban berdasarkan kapasitas maksimum.': ['Faktor beban berdasarkan kapasitas maksimum.', 'Load factor based on maximum capacity.'],
        'Data jam berjalan jika memilih hari ini.': ['Data jam berjalan jika memilih hari ini.', 'Current-hour data when today is selected.'],
        'Tarif tetap bawaan per kWh. Jika tarif belum dikonfigurasi, aplikasi menggunakan Rp1.500.': ['Tarif tetap bawaan per kWh. Jika tarif belum dikonfigurasi, aplikasi menggunakan Rp1.500.', 'Default flat rate per kWh. The application uses IDR 1,500 if no rate has been configured.'],
        'Biaya aktual dihitung dengan rumus': ['Biaya aktual dihitung dengan rumus', 'Actual cost is calculated using'],
        'Jika tidak ada konsumsi, digunakan rata-rata dari kedua tarif.': ['Jika tidak ada konsumsi, digunakan rata-rata dari kedua tarif.', 'If there is no consumption, the average of both rates is used.'],
        'Hasil dibatasi maksimum 100%. Klasifikasi:': ['Hasil dibatasi maksimum 100%. Klasifikasi:', 'The result is capped at 100%. Categories:'],
        'Pemanfaatan rendah': ['Pemanfaatan rendah', 'Underutilized'], 'Risiko tinggi': ['Risiko tinggi', 'High risk'],
        'Tarif rata-rata agregat merupakan rata-rata aritmetika antarperangkat, tanpa pembobotan berdasarkan konsumsi kWh.': ['Tarif rata-rata agregat merupakan rata-rata aritmetika antarperangkat, tanpa pembobotan berdasarkan konsumsi kWh.', 'The aggregate average tariff is the arithmetic mean across devices, without weighting by kWh consumption.'],
        'Jenis Tindakan Pemeliharaan': ['Jenis Tindakan Pemeliharaan', 'Maintenance Action'],
        'Mulai ulang perangkat': ['Mulai ulang perangkat', 'Restart device'], 'Ganti sekring atau MCB': ['Ganti sekring atau MCB', 'Replace fuse or circuit breaker'],
        'Periksa relai atau sistem kendali': ['Periksa relai atau sistem kendali', 'Inspect relay or control system'],
        'Periksa tegangan suplai': ['Periksa tegangan suplai', 'Check supply voltage'],
        'Atur ulang pemutus arus': ['Atur ulang pemutus arus', 'Reset circuit breaker'],
        'Periksa kabel pentanahan': ['Periksa kabel pentanahan', 'Inspect grounding cable'],
        'Perangkat:': ['Perangkat:', 'Device:'], 'Jenis:': ['Jenis:', 'Type:'],
        'Perkiraan kehilangan pendapatan selama perangkat mengalami gangguan.': ['Perkiraan kehilangan pendapatan selama perangkat mengalami gangguan.', 'Estimated revenue loss while the device is unavailable.'],
        'Perkiraan kehilangan pendapatan selama perangkat mengalami gangguan. Perhitungan berlanjut hingga anomali diselesaikan.': ['Perkiraan kehilangan pendapatan selama perangkat mengalami gangguan. Perhitungan berlanjut hingga anomali diselesaikan.', 'Estimated revenue loss while the device is unavailable. The estimate continues until the anomaly is resolved.'],
        'Pendapatan per jam belum dikonfigurasi untuk perangkat ini.': ['Pendapatan per jam belum dikonfigurasi untuk perangkat ini.', 'Revenue per hour has not been configured for this device.'],
        'Dampak keuangan untuk jenis anomali ini tidak dapat diperkirakan.': ['Dampak keuangan untuk jenis anomali ini tidak dapat diperkirakan.', 'The financial impact of this anomaly type cannot be estimated.'],
        'Dampak biaya energi akibat beban berlebih, dihitung dari selisih daya × 0,25 jam × tarif.': ['Dampak biaya energi akibat beban berlebih, dihitung dari selisih daya × 0,25 jam × tarif.', 'The energy cost impact of excessive load is calculated as the power difference × 0.25 hours × the tariff.'],
        'Atur Ulang Grafik': ['Atur Ulang Grafik', 'Reset Chart'],
        'Hapus seluruh data grafik untuk menghitung ulang nilai SMA': ['Hapus seluruh data grafik untuk menghitung ulang nilai SMA', 'Delete all chart data to recalculate the SMA'],
        'Lupa Kata Sandi': ['Lupa Kata Sandi', 'Forgot Password'],
        'Atur Ulang Kata Sandi': ['Atur Ulang Kata Sandi', 'Reset Password'],
        'Email Terverifikasi': ['Email Terverifikasi', 'Email Verified'],
        'Statistik Penggunaan:': ['Statistik Penggunaan:', 'Usage Statistics:'],
        'Gagal mengubah peran akun.': ['Gagal mengubah peran akun.', 'Failed to change the account role.'],
        'Gagal mengubah status akun.': ['Gagal mengubah status akun.', 'Failed to change the account status.'],
        'Tidak dapat menghubungi server.': ['Tidak dapat menghubungi server.', 'Unable to contact the server.'],
        'Belum ada data konsumsi pada periode ini.': ['Belum ada data konsumsi pada periode ini.', 'No consumption data is available for this period.'],
        'Peringkat gagal dimuat.': ['Peringkat gagal dimuat.', 'The ranking could not be loaded.'],
        'Pembaruan gagal, menampilkan data terakhir': ['Pembaruan gagal, menampilkan data terakhir', 'Update failed. Showing the most recent data.'],
        'Memuat peringkat...': ['Memuat peringkat...', 'Loading rankings...'],
        'Pilih baris untuk membuka detail perangkat. Estimasi biaya dihitung menggunakan tarif untuk setiap perangkat.': ['Pilih baris untuk membuka detail perangkat. Estimasi biaya dihitung menggunakan tarif untuk setiap perangkat.', 'Select a row to view device details. Cost estimates use the tariff configured for each device.'],
        'Gagal menyimpan kategori perangkat.': ['Gagal menyimpan kategori perangkat.', 'Failed to save the device category.'],
        'Gagal memeriksa status.': ['Gagal memeriksa status.', 'Failed to check the status.'],
        'Cari operator, perangkat, atau tindakan…': ['Cari operator, perangkat, atau tindakan…', 'Search operators, devices, or actions…'],
        'Admin': ['Administrator', 'Admin'],
        '(opsional)': ['(opsional)', '(optional)'],
        'Kata Sandi Aplikasi': ['Kata Sandi Aplikasi', 'App Password'],
        '0 = tarif tetap': ['0 = tarif tetap', '0 = flat rate'],
        '0 = tidak tersedia': ['0 = tidak tersedia', '0 = not available'],
        'Token dari dasbor Wablas': ['Token dari dasbor Wablas', 'Token from the Wablas dashboard'],
        'Kunci rahasia dari dasbor Wablas': ['Kunci rahasia dari dasbor Wablas', 'Secret key from the Wablas dashboard'],
        'Masukkan kunci API baru sebagai pengganti kunci saat ini': ['Masukkan kunci API baru sebagai pengganti kunci saat ini', 'Enter a new API key to replace the current key'],
        'Masukkan nama model khusus': ['Masukkan nama model khusus', 'Enter a custom model name'],
        'Pendaftaran Berhasil!': ['Pendaftaran Berhasil!', 'Registration Successful!'],
        'Satu langkah lagi untuk mengaktifkan akun Anda.': ['Satu langkah lagi untuk mengaktifkan akun Anda.', 'One more step to activate your account.'],
        'Simpan Kata Sandi Baru': ['Simpan Kata Sandi Baru', 'Save New Password'],
        'Auto-refresh ON': ['Pembaruan otomatis aktif', 'Auto-refresh ON'],
        'Auto-refresh OFF': ['Pembaruan otomatis nonaktif', 'Auto-refresh OFF'],
        'Data copied to the clipboard.': ['Data berhasil disalin ke papan klip.', 'Data copied to the clipboard.'],
        'Data copied to clipboard': ['Data berhasil disalin ke papan klip.', 'Data copied to clipboard'],
        'Per-Device': ['Per Perangkat', 'Per-Device'], 'AI Chatbot': ['Asisten AI', 'AI Assistant'],
        'MQTT Connection': ['Koneksi MQTT', 'MQTT Connection'], 'Test': ['Uji', 'Test'],
        'No': ['Tidak', 'No'], 'Yes': ['Ya', 'Yes'],
        'Broker Host / IP': ['Host / Alamat IP Broker', 'Broker Host / IP'],
        'IP address atau hostname broker MQTT': ['Alamat IP atau nama host broker MQTT', 'MQTT broker IP address or host name'],
        'Default Mosquitto: 1883': ['Port bawaan Mosquitto: 1883', 'Default Mosquitto port: 1883'],
        'Avg Power Factor': ['Rata-rata Faktor Daya', 'Average Power Factor'],
        'Est. Biaya': ['Estimasi Biaya', 'Estimated Cost'],
        'Device Terdampak Finansial': ['Perangkat Terdampak secara Finansial', 'Devices with Financial Impact'],
        'Anomali per Device': ['Anomali per Perangkat', 'Anomalies by Device'],
        'Revenue Loss Berlangsung': ['Perkiraan Kehilangan Pendapatan yang Berlangsung', 'Ongoing Revenue Loss'],
        'Device Drop': ['Gangguan Perangkat', 'Device Drop'],
        'Device Terdampak': ['Perangkat Terdampak', 'Affected Devices'],
        'Severity Kritis': ['Tingkat Keparahan Kritis', 'Critical Severity'],
        'High Risk': ['Risiko Tinggi', 'High Risk'], 'Under-utilized': ['Kurang Dimanfaatkan', 'Underutilized'],
        'Optimal': ['Optimal', 'Optimal'],
        'Semua device menggunakan tarif flat': ['Semua perangkat menggunakan tarif tetap', 'All devices use the flat rate'],
        'device flat': ['perangkat bertarif tetap', 'flat-rate devices'],
        'Cek Device': ['Periksa Perangkat', 'Check Device'],
        'Device not connected.': ['Perangkat tidak terhubung.', 'Device not connected.'],
        'Kembali ke Login': ['Kembali ke Halaman Masuk', 'Back to Login'],
        'telah diberikan role': ['telah diberi peran', 'has been assigned the role']
        , 'History ID': ['Nomor Riwayat', 'History ID']
        , 'Received Time': ['Waktu Penerimaan', 'Received Time']
        , 'Terminal Time': ['Waktu pada Perangkat', 'Terminal Time']
        , 'Total W': ['Energi Total', 'Total Energy']
        , 'Freq (Hz)': ['Frekuensi (Hz)', 'Frequency (Hz)']
        , 'Aktif Power': ['Daya Aktif', 'Active Power']
        , 'Volt R': ['Tegangan R', 'Voltage R'], 'Volt S': ['Tegangan S', 'Voltage S'], 'Volt T': ['Tegangan T', 'Voltage T']
        , 'Amp R': ['Arus R', 'Current R'], 'Amp S': ['Arus S', 'Current S'], 'Amp T': ['Arus T', 'Current T']
        , 'HIGH LOAD': ['BEBAN TINGGI', 'HIGH LOAD'], 'MEDIUM LOAD': ['BEBAN SEDANG', 'MEDIUM LOAD']
        , 'Pembaruan Otomatis': ['Pembaruan Otomatis', 'Automatic Refresh']
        , 'Belum Diselesaikan': ['Belum Diselesaikan', 'Unresolved']
        , 'Anomali per Perangkat': ['Anomali per Perangkat', 'Anomalies by Device']
        , 'Estimasi Kehilangan Pendapatan': ['Estimasi Kehilangan Pendapatan', 'Estimated Revenue Loss']
        , 'Kehilangan Pendapatan yang Sedang Berlangsung': ['Kehilangan Pendapatan yang Sedang Berlangsung', 'Ongoing Revenue Loss']
        , 'Perangkat Terdampak secara Finansial': ['Perangkat Terdampak secara Finansial', 'Devices with Financial Impact']
        , 'Beban Berlebih': ['Beban Berlebih', 'Overload']
        , 'Perangkat Terdampak': ['Perangkat Terdampak', 'Affected Devices']
        , 'Tingkat Keparahan Kritis': ['Tingkat Keparahan Kritis', 'Critical Severity']
        , 'Ambang Batas': ['Ambang Batas', 'Threshold']
        , 'Waktu Respons': ['Waktu Respons', 'Response Time'], 'Penyebab Utama': ['Penyebab Utama', 'Root Cause']
        , 'Tindakan yang Disarankan': ['Tindakan yang Disarankan', 'Recommended Action']
        , 'Dampak Biaya (Beban Berlebih)': ['Dampak Biaya (Beban Berlebih)', 'Cost Impact (Overload)']
        , 'Estimasi Kehilangan Pendapatan (Gangguan)': ['Estimasi Kehilangan Pendapatan (Gangguan)', 'Estimated Revenue Loss (Failure)']
        , 'Cuplikan Grafik': ['Cuplikan Grafik', 'Chart Snapshot']
        , 'Selesaikan Anomali': ['Selesaikan Anomali', 'Resolve Anomaly']
        , 'Hapus Semua': ['Hapus Semua', 'Clear All'], 'Buat Laporan': ['Buat Laporan', 'Generate Report']
        , 'Koneksi MQTT': ['Koneksi MQTT', 'MQTT Connection']
        , 'Interval Pengambilan Data': ['Interval Pengambilan Data', 'Data Sampling Interval']
        , 'Pembaruan Dasbor': ['Pembaruan Dasbor', 'Dashboard Refresh']
        , 'Pembaruan Grafik': ['Pembaruan Grafik', 'Chart Refresh']
        , 'Halaman Detail': ['Halaman Detail', 'Details Page']
        , 'Pemeriksaan Anomali': ['Pemeriksaan Anomali', 'Anomaly Check']
        , 'Jumlah Titik Data Grafik': ['Jumlah Titik Data Grafik', 'Chart Data Points']
        , 'Pemindaian Otomatis': ['Pemindaian Otomatis', 'Automatic Scan']
        , 'Notifikasi Email': ['Notifikasi Email', 'Email Notifications']
        , 'Notifikasi WhatsApp': ['Notifikasi WhatsApp', 'WhatsApp Notifications']
        , 'Nama Pengguna': ['Nama Pengguna', 'Username']
        , 'Gunakan TLS': ['Gunakan TLS', 'Use TLS']
        , 'Sertifikat CA': ['Sertifikat CA', 'CA Certificate']
        , 'Sertifikat Klien': ['Sertifikat Klien', 'Client Certificate']
        , 'Kata Sandi Sertifikat Klien': ['Kata Sandi Sertifikat Klien', 'Client Certificate Password']
        , 'Lewati Validasi Sertifikat': ['Lewati Validasi Sertifikat', 'Skip Certificate Validation']
        , 'Alamat Email Pengirim': ['Alamat Email Pengirim', 'Sender Email Address']
        , 'Email Administrator Utama': ['Email Administrator Utama', 'Master Administrator Email']
        , 'Jadwal Laporan Per Jam': ['Jadwal Laporan Per Jam', 'Hourly Report Schedule']
        , 'Waktu Pengiriman Laporan Harian': ['Waktu Pengiriman Laporan Harian', 'Daily Report Time']
        , 'Tanggal dan Waktu Pengiriman Laporan Bulanan': ['Tanggal dan Waktu Pengiriman Laporan Bulanan', 'Monthly Report Date and Time']
        , 'Uji Pengiriman Email': ['Uji Pengiriman Email', 'Test Email Delivery']
        , 'Uji Pengiriman': ['Uji Pengiriman', 'Test Delivery']
        , 'Periksa Perangkat': ['Periksa Perangkat', 'Check Device']
        , 'Periode EMA': ['Periode EMA', 'EMA Period']
        , 'Faktor Daya dan Frekuensi': ['Faktor Daya dan Frekuensi', 'Power Factor and Frequency']
        , 'Pengaturan Tampilan Grafik': ['Pengaturan Tampilan Grafik', 'Chart Display Settings']
        , 'Perangkat ini menggunakan tarif tetap': ['Perangkat ini menggunakan tarif tetap', 'This device uses a flat rate']
        , 'Tarif tetap': ['Tarif tetap', 'Flat rate']
        , 'Kapasitas kurang dimanfaatkan': ['Kapasitas kurang dimanfaatkan', 'Underutilized capacity']
        , 'Rincian': ['Rincian', 'Details']
        , 'Host / Alamat IP Broker': ['Host / Alamat IP Broker', 'Broker Host / IP Address']
        , 'Alamat IP atau nama host broker MQTT': ['Alamat IP atau nama host broker MQTT', 'MQTT broker IP address or host name']
        , 'Port bawaan Mosquitto: 1883': ['Port bawaan Mosquitto: 1883', 'Default Mosquitto port: 1883']
        , 'Kata sandi berkas .pfx — isi sebelum mengunggah berkas jika diperlukan.': ['Kata sandi berkas .pfx — isi sebelum mengunggah berkas jika diperlukan.', 'Enter the .pfx file password before uploading if required.']
        , 'Autentikasi klien ke broker — TLS timbal balik (.pfx / .p12)': ['Autentikasi klien ke broker melalui TLS timbal balik (.pfx / .p12)', 'Client authentication to the broker using mutual TLS (.pfx / .p12)']
        , 'Jangka waktu pembaruan data pada setiap halaman pemantauan.': ['Jangka waktu pembaruan data pada setiap halaman pemantauan.', 'How often data is refreshed on each monitoring page.']
        , 'Jangka waktu pemindaian panel baru.': ['Jangka waktu pemindaian panel baru.', 'How often new panels are scanned.']
        , 'Alamat email yang digunakan untuk mengirim notifikasi.': ['Alamat email yang digunakan untuk mengirim notifikasi.', 'Email address used to send notifications.']
        , 'Pemulihan pengaturan bawaan selesai. Klik Simpan untuk menerapkannya.': ['Pemulihan pengaturan bawaan selesai. Klik Simpan untuk menerapkannya.', 'Default settings restored. Click Save to apply them.']
        , 'Pulihkan Pengaturan Bawaan': ['Pulihkan Pengaturan Bawaan', 'Restore Default Settings']
        , 'Simpan Pengaturan': ['Simpan Pengaturan', 'Save Settings']
        , 'Menyimpan...': ['Menyimpan...', 'Saving...']
        , 'Mengirim...': ['Mengirim...', 'Sending...']
        , 'Memeriksa...': ['Memeriksa...', 'Checking...']
        , 'Menguji...': ['Menguji...', 'Testing...']
        , 'Uji Koneksi': ['Uji Koneksi', 'Test Connection']
        , 'Model Khusus...': ['Model Khusus...', 'Custom Model...']
        , 'Nama Model Khusus': ['Nama Model Khusus', 'Custom Model Name']
        , 'Pengaturan Asisten AI': ['Pengaturan Asisten AI', 'AI Assistant Settings']
        , 'Penyedia AI': ['Penyedia AI', 'AI Provider']
        , 'Penyedia Khusus...': ['Penyedia Khusus...', 'Custom Provider...']
        , 'Pilih penyedia AI; URL dan model akan disesuaikan secara otomatis.': ['Pilih penyedia AI; URL dan model akan disesuaikan secara otomatis.', 'Choose an AI provider; the URL and model will be adjusted automatically.']
        , 'URL Endpoint API Khusus': ['URL Endpoint API Khusus', 'Custom API Endpoint URL']
        , 'Pengaturan sistem akan tersimpan di basis data.': ['Pengaturan sistem akan tersimpan di basis data.', 'System settings will be saved to the database.']
        , 'Analisis Keuangan': ['Analisis Keuangan', 'Financial Analysis']
        , 'Rata-rata Faktor Daya': ['Rata-rata Faktor Daya', 'Average Power Factor']
        , 'Estimasi Biaya': ['Estimasi Biaya', 'Estimated Cost']
        , 'Faktor Daya': ['Faktor Daya', 'Power Factor']
        , 'Masuk Terakhir': ['Masuk Terakhir', 'Last Sign-In']
        , 'WAWASAN': ['WAWASAN', 'INSIGHT']
        , 'WAWASAN TAHUNAN': ['WAWASAN TAHUNAN', 'ANNUAL INSIGHTS']
        , 'Perangkat bertarif tetap': ['Perangkat bertarif tetap', 'Flat-rate device']
        , 'Melebihi Anggaran': ['Melebihi Anggaran', 'Over Budget']
        , 'Kapasitas Kurang Dimanfaatkan': ['Kapasitas Kurang Dimanfaatkan', 'Underutilized Capacity']
        , 'Perubahan Bulanan': ['Perubahan Bulanan', 'Month-over-Month Change']
        , 'Perubahan Tahunan': ['Perubahan Tahunan', 'Year-over-Year Change']
        , 'Penanganan Dikonfirmasi': ['Penanganan Dikonfirmasi', 'Handling Confirmed']
        , 'Catatan Dihapus': ['Catatan Dihapus', 'Records Deleted']
        , 'Semua Catatan Dihapus': ['Semua Catatan Dihapus', 'All Records Deleted']
        , 'Hapus Semua Catatan': ['Hapus Semua Catatan', 'Clear All Records']
        , 'Pengaturan berhasil diekspor.': ['Pengaturan berhasil diekspor.', 'Settings exported successfully.']
        , 'Peringatan uji berhasil dikirim.': ['Peringatan uji berhasil dikirim.', 'The test alert was sent successfully.']
        , 'Laporan per jam uji berhasil dikirim.': ['Laporan per jam uji berhasil dikirim.', 'The test hourly report was sent successfully.']
        , 'Laporan harian uji berhasil dikirim.': ['Laporan harian uji berhasil dikirim.', 'The test daily report was sent successfully.']
        , 'Laporan bulanan uji berhasil dikirim.': ['Laporan bulanan uji berhasil dikirim.', 'The test monthly report was sent successfully.']
        , 'Email uji berhasil dikirim. Periksa kotak masuk Anda.': ['Email uji berhasil dikirim. Periksa kotak masuk Anda.', 'The test email was sent successfully. Check your inbox.']
        , 'Koneksi asisten AI berhasil. Balasan AI: ': ['Koneksi asisten AI berhasil. Balasan AI: ', 'AI assistant connection succeeded. AI response: ']
        , 'Koneksi asisten AI gagal: ': ['Koneksi asisten AI gagal: ', 'AI assistant connection failed: ']
        , 'Terjadi kesalahan pada koneksi asisten AI: ': ['Terjadi kesalahan pada koneksi asisten AI: ', 'An error occurred while connecting to the AI assistant: ']
        , 'Semua interval diselaraskan menjadi ': ['Semua interval diselaraskan menjadi ', 'All intervals synchronized to ']
        , 'Pengaturan MQTT berhasil disimpan.': ['Pengaturan MQTT berhasil disimpan.', 'MQTT settings saved successfully.']
        , 'Gagal menyimpan pengaturan MQTT.': ['Gagal menyimpan pengaturan MQTT.', 'Failed to save MQTT settings.']
        , 'Pengaturan interval berhasil disimpan.': ['Pengaturan interval berhasil disimpan.', 'Interval settings saved successfully.']
        , 'Gagal menyimpan pengaturan interval.': ['Gagal menyimpan pengaturan interval.', 'Failed to save interval settings.']
        , 'Pengaturan email dan notifikasi berhasil disimpan.': ['Pengaturan email dan notifikasi berhasil disimpan.', 'Email and notification settings saved successfully.']
        , 'Gagal menyimpan pengaturan email.': ['Gagal menyimpan pengaturan email.', 'Failed to save email settings.']
        , 'Pengaturan WhatsApp berhasil disimpan.': ['Pengaturan WhatsApp berhasil disimpan.', 'WhatsApp settings saved successfully.']
        , 'Gagal menyimpan pengaturan WhatsApp.': ['Gagal menyimpan pengaturan WhatsApp.', 'Failed to save WhatsApp settings.']
        , 'Jadwal dan pengaturan notifikasi berhasil disimpan.': ['Jadwal dan pengaturan notifikasi berhasil disimpan.', 'Schedule and notification settings saved successfully.']
        , 'Gagal menyimpan jadwal laporan.': ['Gagal menyimpan jadwal laporan.', 'Failed to save the report schedule.']
        , 'Pengaturan EMA berhasil disimpan.': ['Pengaturan EMA berhasil disimpan.', 'EMA settings saved successfully.']
        , 'Gagal menyimpan pengaturan EMA.': ['Gagal menyimpan pengaturan EMA.', 'Failed to save EMA settings.']
        , 'Pengaturan notifikasi berhasil disimpan.': ['Pengaturan notifikasi berhasil disimpan.', 'Notification settings saved successfully.']
        , 'Gagal menyimpan pengaturan notifikasi: ': ['Gagal menyimpan pengaturan notifikasi: ', 'Failed to save notification settings: ']
        , 'Gagal mengirim email uji: ': ['Gagal mengirim email uji: ', 'Failed to send the test email: ']
        , 'Pengaturan notifikasi bawaan dipulihkan. Klik Simpan untuk menerapkannya.': ['Pengaturan notifikasi bawaan dipulihkan. Klik Simpan untuk menerapkannya.', 'Default notification settings restored. Click Save to apply them.']
        , 'Pengaturan asisten AI berhasil disimpan.': ['Pengaturan asisten AI berhasil disimpan.', 'AI assistant settings saved successfully.']
        , 'Gagal menyimpan pengaturan asisten AI.': ['Gagal menyimpan pengaturan asisten AI.', 'Failed to save AI assistant settings.']
        , 'Pengaturan asisten AI bawaan dipulihkan. Klik Simpan untuk menerapkannya.': ['Pengaturan asisten AI bawaan dipulihkan. Klik Simpan untuk menerapkannya.', 'Default AI assistant settings restored. Click Save to apply them.']
        , 'Pengaturan EMA bawaan dipulihkan. Klik Simpan untuk menerapkannya.': ['Pengaturan EMA bawaan dipulihkan. Klik Simpan untuk menerapkannya.', 'Default EMA settings restored. Click Save to apply them.']
        , 'Anggaran dan Realisasi': ['Anggaran dan Realisasi', 'Budget and Actual']
        , 'Wawasan Bulanan': ['Wawasan Bulanan', 'Monthly Insights']
        , 'Wawasan Tahunan': ['Wawasan Tahunan', 'Annual Insights']
        , 'Proyeksi dibandingkan dengan anggaran:': ['Proyeksi dibandingkan dengan anggaran:', 'Projection compared with budget:']
        , 'Anggaran bulanan belum dikonfigurasi. Atur anggaran rupiah per bulan pada halaman Pengaturan.': ['Anggaran bulanan belum dikonfigurasi. Atur anggaran rupiah per bulan pada halaman Pengaturan.', 'The monthly budget has not been configured. Set the monthly budget on the Settings page.']
        , 'Konfirmasi Penghapusan': ['Konfirmasi Penghapusan', 'Confirm Deletion']
        , 'Apakah Anda yakin ingin menghapus catatan ini?': ['Apakah Anda yakin ingin menghapus catatan ini?', 'Are you sure you want to delete this record?']
        , 'Apakah Anda yakin ingin menghapus semua catatan anomali?': ['Apakah Anda yakin ingin menghapus semua catatan anomali?', 'Are you sure you want to delete all anomaly records?']
        , 'Kosongkan jika tidak ingin mengubah kunci API.': ['Kosongkan jika tidak ingin mengubah kunci API.', 'Leave blank if you do not want to change the API key.']
    };

    var englishToIndonesian = Object.create(null);
    var indonesianToEnglish = Object.create(null);
    var normalizeIndonesian = Object.create(null);
    var indonesianWord = /\b(ada|agar|aktual|alamat|anda|anomali|atau|belum|buka|boros|berapa|bulan|biaya|cara|cari|dalam|dan|dari|datang|data|default|dengan|daya|energi|ganti|hari|henti|ingin|jam|keterangan|ke|kelola|ketik|konsumsi|kota|khusus|kegunaan|komponen|konfigurasi|kondisi|lokasi|mana|masukkan|minimal|operator|opsional|pembaruan|pengaturan|pengguna|perangkat|permintaan|pertanyaan|pindahkan|paling|pilih|proyeksi|password|rata|realisasi|reset|saat|saran|sedang|selamat|sekarang|sebelumnya|setiap|spesifik|tagihan|tampilkan|tarif|terdaftar|terjadi|tema|tanya|ulangi|untuk|waktu|yang|yakin)\b/i;
    Object.keys(phrases).forEach(function (key) {
        var pair = phrases[key];
        if (key === pair[0] || indonesianWord.test(key)) {
            indonesianToEnglish[key] = pair[1];
            indonesianToEnglish[pair[0]] = pair[1];
            englishToIndonesian[pair[1]] = pair[0];
            normalizeIndonesian[key] = pair[0];
        } else {
            englishToIndonesian[key] = pair[0];
            englishToIndonesian[pair[1]] = pair[0];
            indonesianToEnglish[pair[0]] = pair[1];
        }
    });

    var sourceByNode = new WeakMap();
    var renderedByNode = new WeakMap();
    var sourceByAttribute = new WeakMap();
    var renderedByAttribute = new WeakMap();
    var language = 'id';
    var pageTitleSource = null;

    function trimBounds(value) {
        var match = value.match(/^(\s*)([\s\S]*?)(\s*)$/);
        return { leading: match[1], content: match[2], trailing: match[3] };
    }

    function phraseTranslate(value, lang) {
        var bounds = trimBounds(value);
        var content = bounds.content;
        // Prefer complete phrase matches so translations retain natural sentence structure.
        var dictionary = lang === 'en' ? indonesianToEnglish : englishToIndonesian;
        var exact = dictionary[content] || (lang === 'id' ? normalizeIndonesian[content] : null);
        if (!exact) {
            var exactKey = Object.keys(dictionary).find(function (key) { return key.toLowerCase() === content.toLowerCase(); });
            if (exactKey) exact = dictionary[exactKey];
            else if (lang === 'id') {
                exactKey = Object.keys(normalizeIndonesian).find(function (key) { return key.toLowerCase() === content.toLowerCase(); });
                if (exactKey) exact = normalizeIndonesian[exactKey];
            }
        }
        if (exact) return bounds.leading + exact + bounds.trailing;
        // Keep formatted measurements intact while localizing their label.
        var average = content.match(/^(Avg\.?|Average|Rata-rata)(:?\s+)(.+)$/i);
        if (average) return bounds.leading + (lang === 'id' ? 'Rata-rata' : 'Average') + (average[2].indexOf(':') >= 0 ? ': ' : ' ') + average[3] + bounds.trailing;
        return bounds.leading + content + bounds.trailing;
    }

    function translateHelpSegment(value, lang) {
        var exact = phraseTranslate(value, lang);
        if (exact !== value) return exact;
        var bounds = trimBounds(value);
        var dictionary = lang === 'en' ? indonesianToEnglish : englishToIndonesian;
        var keys = Object.keys(dictionary).sort(function (a, b) { return b.length - a.length; });
        var pattern = new RegExp('(^|[^A-Za-z])(' + keys.map(function (key) {
            return key.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
        }).join('|') + ')(?=$|[^A-Za-z])', 'gi');
        var translated = bounds.content.replace(pattern, function (match, prefix, word) {
            var key = dictionary[word] ? word : keys.find(function (candidate) { return candidate.toLowerCase() === word.toLowerCase(); });
            var replacement = key ? dictionary[key] : word;
            if (word === word.toLowerCase()) replacement = replacement.toLowerCase();
            else if (word === word.toUpperCase()) replacement = replacement.toUpperCase();
            return prefix + replacement;
        });
        return bounds.leading + translated + bounds.trailing;
    }

    function textAllowed(node) {
        return node.parentElement && !/^(SCRIPT|STYLE|TEXTAREA|CODE|PRE)$/.test(node.parentElement.tagName);
    }

    function translateText(node) {
        var current = node.nodeValue;
        var previous = renderedByNode.get(node);
        if (previous !== current || !sourceByNode.has(node)) sourceByNode.set(node, current);
        var output = phraseTranslate(sourceByNode.get(node), language);
        if (output !== current) node.nodeValue = output;
        renderedByNode.set(node, output);
    }

    function translateAttribute(element, name) {
        var values = sourceByAttribute.get(element) || {};
        var rendered = renderedByAttribute.get(element) || {};
        var current = element.getAttribute(name);
        if (rendered[name] !== current || !Object.prototype.hasOwnProperty.call(values, name)) values[name] = current;
        var output = name === 'data-card-help'
            ? values[name].split('|').map(function (part) { return translateHelpSegment(part, language); }).join('|')
            : phraseTranslate(values[name], language);
        if (output !== current) element.setAttribute(name, output);
        rendered[name] = output;
        sourceByAttribute.set(element, values);
        renderedByAttribute.set(element, rendered);
    }

    function translate(root) {
        var localizedElements = [];
        if (root.nodeType === 1 && root.matches('[data-i18n-id][data-i18n-en]')) localizedElements.push(root);
        if (root.querySelectorAll) localizedElements = localizedElements.concat(Array.prototype.slice.call(root.querySelectorAll('[data-i18n-id][data-i18n-en]')));
        localizedElements.forEach(function (element) {
            element.textContent = element.getAttribute(language === 'en' ? 'data-i18n-en' : 'data-i18n-id');
        });
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, null, false);
        var node;
        while ((node = walker.nextNode())) if (textAllowed(node)) translateText(node);
        var elements = [];
        if (root.nodeType === 1 && root.matches('[placeholder],[title],[aria-label],[data-card-help],[data-message]')) elements.push(root);
        if (root.querySelectorAll) elements = elements.concat(Array.prototype.slice.call(root.querySelectorAll('[placeholder],[title],[aria-label],[data-card-help],[data-message]')));
        elements.forEach(function (element) { ['placeholder', 'title', 'aria-label', 'data-card-help', 'data-message'].forEach(function (name) { if (element.hasAttribute(name)) translateAttribute(element, name); }); });
    }

    function translateChart(chart) {
        if (!chart || !chart.data) return;
        (chart.data.datasets || []).forEach(function (dataset) {
            if (typeof dataset.label === 'string') dataset.label = phraseTranslate(dataset.label, language);
        });
        var options = chart.options || {};
        var title = options.plugins && options.plugins.title && options.plugins.title.text;
        if (typeof title === 'string') options.plugins.title.text = phraseTranslate(title, language);
        var scales = options.scales || {};
        Object.keys(scales).forEach(function (key) {
            var axisTitle = scales[key] && scales[key].title && scales[key].title.text;
            if (typeof axisTitle === 'string') scales[key].title.text = phraseTranslate(axisTitle, language);
        });
    }

    function translateCharts() {
        if (!window.Chart || !Chart.instances) return;
        if (!Chart.prototype.__kwhLanguageHooked && Chart.prototype.update) {
            var originalUpdate = Chart.prototype.update;
            Chart.prototype.update = function (mode) {
                translateChart(this);
                return originalUpdate.call(this, mode);
            };
            Chart.prototype.__kwhLanguageHooked = true;
        }
        Object.keys(Chart.instances).forEach(function (id) {
            var chart = Chart.instances[id];
            translateChart(chart);
            if (chart && chart.update) chart.update('none');
        });
    }

    function translatePageTitle() {
        var title = document.querySelector('title');
        if (!title) return;
        if (pageTitleSource === null) pageTitleSource = title.textContent;
        var match = pageTitleSource.match(/^(.*?)\s+-\s+KWH Monitoring$/);
        var source = match ? match[1] : pageTitleSource;
        var suffix = match ? ' - KWH Monitoring' : '';
        title.textContent = phraseTranslate(source, language) + suffix;
    }

    function setLanguage(next) {
        language = next;
        document.documentElement.lang = language;
        try { localStorage.setItem('kwh_language', language); } catch (_) { }
        translate(document.body);
        translatePageTitle();
        translateCharts();
        window.dispatchEvent(new CustomEvent('kwh-language-changed', { detail: { language: language } }));
        var button = document.getElementById('languageToggle');
        if (button) {
            button.textContent = language === 'id' ? 'EN' : 'ID';
            button.title = language === 'id' ? 'Switch to English' : 'Ganti bahasa';
            button.setAttribute('aria-label', button.title);
        }
    }

    function init() {
        try { language = localStorage.getItem('kwh_language') || 'id'; } catch (_) { language = 'id'; }
        if (language !== 'en') language = 'id';
        var button = document.getElementById('languageToggle');
        if (button) button.addEventListener('click', function () { setLanguage(language === 'id' ? 'en' : 'id'); });
        setLanguage(language);
        new MutationObserver(function (changes) {
            changes.forEach(function (change) {
                if (change.type === 'characterData' && textAllowed(change.target)) translateText(change.target);
                Array.prototype.forEach.call(change.addedNodes || [], function (added) {
                    if (added.nodeType === 1) translate(added);
                    else if (added.nodeType === 3 && textAllowed(added)) translateText(added);
                });
                if (change.type === 'attributes') translateAttribute(change.target, change.attributeName);
            });
        }).observe(document.body, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: ['placeholder', 'title', 'aria-label', 'data-card-help', 'data-message'] });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
