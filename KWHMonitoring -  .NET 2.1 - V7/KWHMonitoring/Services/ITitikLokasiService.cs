using System.Collections.Generic;
using System.Threading.Tasks;
using KWHMonitoring.Models;

namespace KWHMonitoring.Services
{
    /// <summary>
    /// Pembaca data master titik lokasi dari database ERP (WWMERP2019.dbo.TitikLokasi).
    /// Dipakai untuk melengkapi tooltip header kartu panel monitoring
    /// (Kode Lokasi, Alamat, Kota) berdasarkan DeviceKey masing-masing device.
    /// </summary>
    public interface ITitikLokasiService
    {
        /// <summary>
        /// Ambil data titik lokasi untuk daftar DeviceKey (DeviceKey = TitikLokasiID).
        /// Key dictionary = DeviceKey yang diminta; device tanpa data tidak dimasukkan.
        /// Tidak pernah melempar exception: bila database ERP tidak tersedia,
        /// hasilnya dictionary kosong/parsial sehingga halaman monitoring tetap normal.
        /// </summary>
        Task<Dictionary<string, TitikLokasi>> GetByDeviceKeysAsync(IEnumerable<string> deviceKeys);
    }
}
