using System;

namespace KWHMonitoring.Models
{
    /// <summary>
    /// Data master titik lokasi dari database ERP (WWMERP2019.dbo.TitikLokasi).
    /// Hanya kolom yang dipakai tooltip panel monitoring yang dipetakan:
    /// TitikLokasiID (dipasangkan dengan DeviceKey), KodeLokasi, Address, City, Province.
    /// TitikLokasiID dibaca sebagai string agar aman baik untuk kolom numerik maupun teks.
    /// </summary>
    public class TitikLokasi
    {
        public string TitikLokasiID { get; set; } = string.Empty;
        public string KodeLokasi { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;

        /// <summary>Kota = City + Province (tanpa pengulangan bila salah satu kosong atau sama).</summary>
        public string KotaProvinsi => Compose(City, Province);

        private static string Compose(string city, string province)
        {
            var c = (city ?? string.Empty).Trim();
            var p = (province ?? string.Empty).Trim();

            if (c.Length == 0) return p;
            if (p.Length == 0) return c;
            if (string.Equals(c, p, StringComparison.OrdinalIgnoreCase)) return c;

            return c + ", " + p;
        }
    }
}
