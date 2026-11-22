using System;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MiniSIEM
{
    public partial class Form1 : Form
    {
        // Windows API: Metin kutularına silik ipucu (placeholder) eklemek için
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
        private const int EM_SETCUEBANNER = 0x1501;

        // Veritabanı bağlantı adresi
        private readonly string baglantiAdresi = BaglantiAdresiniYukle();

        // Yerel db_config.txt dosyasından bağlantı adresini okur, yoksa varsayılan SQL Express adresini kullanır
        private static string BaglantiAdresiniYukle()
        {
            string[] olasiYollar = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_config.txt"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\db_config.txt")
            };

            foreach (string yol in olasiYollar)
            {
                if (File.Exists(yol))
                {
                    string icerik = File.ReadAllText(yol).Trim();
                    if (!string.IsNullOrWhiteSpace(icerik))
                        return icerik;
                }
            }

            return @"Server=.\SQLEXPRESS; Database=CyberSecurityDB; Integrated Security=True;";
        }

        // Ekranda şu an hangi listenin açık olduğunu takip eden bayrak (true: Kritik, false: Tümü)
        private bool kritikGorunumuAktif = true;

        // Silme işlemini takip eden sayaç
        private int sonMetinUzunlugu = 0;

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Kutulara placeholder (silik ipucu yazısı) tanımla
            SendMessage(textBox1.Handle, EM_SETCUEBANNER, 0, "IP Adresi");
            SendMessage(textBox2.Handle, EM_SETCUEBANNER, 0, "Açıklama / Tehdit Türü");
            SendMessage(textBox3.Handle, EM_SETCUEBANNER, 0, "IP Ara...");

            // Tablo yenilenirken ekran titremesini önlemek için DoubleBuffered aktif et
            typeof(DataGridView).InvokeMember("DoubleBuffered", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty, 
                null, dataGridView1, new object[] { true });

            // Program açıldığında kritik alarmları otomatik listele
            KritikAlarmlariGetir();
        }

        // --- IP DOĞRULAMA VE OTOMATİK NOKTA KOYMA ---

        private bool GecerliIpMi(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return false;

            string[] parcalar = ip.Trim().Split('.');
            if (parcalar.Length != 4) return false;

            foreach (string parca in parcalar)
            {
                if (!byte.TryParse(parca, out _))
                    return false;
            }
            return true;
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsControl(e.KeyChar)) return;

            if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.')
            {
                e.Handled = true;
                return;
            }

            string metin = textBox1.Text;

            if (e.KeyChar == '.')
            {
                if (string.IsNullOrEmpty(metin) || metin.EndsWith(".") || metin.Split('.').Length > 3)
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            string metin = textBox1.Text;

            bool siliniyor = metin.Length < sonMetinUzunlugu;
            sonMetinUzunlugu = metin.Length;

            if (siliniyor || string.IsNullOrEmpty(metin))
                return;

            string[] parcalar = metin.Split('.');

            if (parcalar.Length <= 3)
            {
                string sonParca = parcalar[parcalar.Length - 1];
                if (sonParca.Length == 3 && !metin.EndsWith("."))
                {
                    textBox1.Text = metin + ".";
                    textBox1.SelectionStart = textBox1.Text.Length;
                    sonMetinUzunlugu = textBox1.Text.Length;
                }
            }
        }

        // Tabloyu güncellerken kullanıcının kaydırma (scroll) konumunu ve seçtiği satırı korur
        private void TabloVerisiniGuncelle(DataTable yeniTablo)
        {
            int scrollIndex = dataGridView1.FirstDisplayedScrollingRowIndex;
            int seciliIndex = (dataGridView1.CurrentRow != null) ? dataGridView1.CurrentRow.Index : -1;

            dataGridView1.DataSource = yeniTablo;
            TabloyuRenklendir();

            if (scrollIndex >= 0 && scrollIndex < dataGridView1.RowCount)
            {
                dataGridView1.FirstDisplayedScrollingRowIndex = scrollIndex;
            }

            if (seciliIndex >= 0 && seciliIndex < dataGridView1.RowCount)
            {
                dataGridView1.Rows[seciliIndex].Selected = true;
                if (dataGridView1.Columns.Count > 0)
                {
                    dataGridView1.CurrentCell = dataGridView1.Rows[seciliIndex].Cells[0];
                }
            }
        }

        // Satırları tehdit seviyesine göre renklendiren metot
        private void TabloyuRenklendir()
        {
            // IsKritik sütununu kullanıcıya gösterme (sadece arka planda renklendirme için kullanıyoruz)
            if (dataGridView1.Columns.Contains("IsKritik"))
            {
                dataGridView1.Columns["IsKritik"].Visible = false;
            }

            foreach (DataGridViewRow satir in dataGridView1.Rows)
            {
                // 1. Durum: Kritik Alarmlar sekmesindeysek hepsi kırmızı
                if (kritikGorunumuAktif)
                {
                    satir.DefaultCellStyle.BackColor = Color.DarkRed;
                    satir.DefaultCellStyle.ForeColor = Color.White;
                    continue;
                }

                // 2. Durum: Tüm Loglar veya Arama sekmesindeysek:
                // Bu kayıt KritikAlarmlar tablosunda varsa (IsKritik = 1) veya içinde kritik kelime geçiyorsa boya
                bool kritikMi = false;

                if (satir.Cells["IsKritik"] != null && satir.Cells["IsKritik"].Value != DBNull.Value)
                {
                    if (Convert.ToInt32(satir.Cells["IsKritik"].Value) == 1)
                    {
                        kritikMi = true;
                    }
                }

                if (!kritikMi && satir.Cells["TehditTuru"] != null && satir.Cells["TehditTuru"].Value != null)
                {
                    string tehdit = satir.Cells["TehditTuru"].Value.ToString().ToUpper();
                    if (tehdit.Contains("KRİTİK") || tehdit.Contains("KRITIK") || 
                        tehdit.Contains("SQL") || tehdit.Contains("DDOS") || 
                        tehdit.Contains("BRUTE") || tehdit.Contains("EXPLOIT") ||
                        tehdit.Contains("ACİL") || tehdit.Contains("ACIL") ||
                        tehdit.Contains("ZARARLI") || tehdit.Contains("MALWARE"))
                    {
                        kritikMi = true;
                    }
                }

                if (kritikMi)
                {
                    satir.DefaultCellStyle.BackColor = Color.DarkRed;
                    satir.DefaultCellStyle.ForeColor = Color.White;
                }
            }
        }

        // Sadece KritikAlarmlar tablosundaki verileri çeker
        private void KritikAlarmlariGetir()
        {
            kritikGorunumuAktif = true;
            try
            {
                using (SqlConnection baglanti = new SqlConnection(baglantiAdresi))
                {
                    baglanti.Open();
                    string sorgu = "SELECT AlarmID, LogID, IP_Adresi, TehditTuru, AlarmZamani FROM KritikAlarmlar ORDER BY AlarmZamani DESC";

                    using (SqlCommand komut = new SqlCommand(sorgu, baglanti))
                    using (SqlDataAdapter adaptor = new SqlDataAdapter(komut))
                    {
                        DataTable tablo = new DataTable();
                        adaptor.Fill(tablo);

                        TabloVerisiniGuncelle(tablo);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kritik veriler çekilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Tüm logları (TehditLoglari tablosunu) ve kritik durumlarını çeker
        private void TumLoglariGetir()
        {
            kritikGorunumuAktif = false;
            try
            {
                using (SqlConnection baglanti = new SqlConnection(baglantiAdresi))
                {
                    baglanti.Open();
                    // KritikAlarmlar ile LEFT JOIN yaparak hangi logun kritik alarm olduğunu (IsKritik) tespit ediyoruz
                    string sorgu = @"
                        SELECT 
                            t.LogID, 
                            t.IP_Adresi, 
                            t.TehditTuru, 
                            t.KayitTarihi,
                            CASE WHEN k.AlarmID IS NOT NULL THEN 1 ELSE 0 END AS IsKritik
                        FROM TehditLoglari t
                        LEFT JOIN (SELECT DISTINCT LogID, AlarmID FROM KritikAlarmlar) k ON t.LogID = k.LogID
                        ORDER BY t.KayitTarihi DESC";

                    using (SqlCommand komut = new SqlCommand(sorgu, baglanti))
                    using (SqlDataAdapter adaptor = new SqlDataAdapter(komut))
                    {
                        DataTable tablo = new DataTable();
                        adaptor.Fill(tablo);

                        TabloVerisiniGuncelle(tablo);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tüm loglar çekilirken hata oluştu: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Kritik Alarmlar Butonu
        private void button1_Click(object sender, EventArgs e)
        {
            KritikAlarmlariGetir();
        }

        // Tüm Logları Getir Butonu
        private void button4_Click(object sender, EventArgs e)
        {
            TumLoglariGetir();
        }

        // Stored procedure kullanarak yeni tehdit kaydı ekler
        private void button2_Click(object sender, EventArgs e)
        {
            string ip = textBox1.Text.Trim();
            string tehdit = textBox2.Text.Trim();

            if (!GecerliIpMi(ip))
            {
                MessageBox.Show("Lütfen geçerli bir IPv4 adresi giriniz!\n(Örnek: 192.168.1.50 - Her blok 0 ile 255 arasında olmalıdır)", "Geçersiz IP Formatı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(tehdit))
            {
                MessageBox.Show("Lütfen tehdit türü / açıklama alanını doldurunuz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (SqlConnection baglanti = new SqlConnection(baglantiAdresi))
                {
                    baglanti.Open();
                    using (SqlCommand komut = new SqlCommand("sp_YeniTehditEkle", baglanti))
                    {
                        komut.CommandType = CommandType.StoredProcedure;

                        komut.Parameters.AddWithValue("@GelenIp", ip);
                        komut.Parameters.AddWithValue("@GelenTehdit", tehdit);

                        komut.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Tehdit kaydı başarıyla eklendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                textBox1.Clear();
                textBox2.Clear();

                if (kritikGorunumuAktif)
                    KritikAlarmlariGetir();
                else
                    TumLoglariGetir();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Kayıt ekleme hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Acil durum kaydı girer
        private void button3_Click(object sender, EventArgs e)
        {
            string ip = textBox1.Text.Trim();
            string tehdit = textBox2.Text.Trim();

            if (!GecerliIpMi(ip))
            {
                MessageBox.Show("Lütfen geçerli bir IPv4 adresi giriniz!\n(Örnek: 192.168.1.50 - Her blok 0 ile 255 arasında olmalıdır)", "Geçersiz IP Formatı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(tehdit))
            {
                MessageBox.Show("Lütfen tehdit türü / açıklama alanını doldurunuz.", "Eksik Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using (SqlConnection baglanti = new SqlConnection(baglantiAdresi))
                {
                    baglanti.Open();
                    string sorgu = "INSERT INTO TehditLoglari (IP_Adresi, TehditTuru) VALUES (@GelenIP, @GelenTehdit);";

                    using (SqlCommand komut = new SqlCommand(sorgu, baglanti))
                    {
                        string tehditMetni = tehdit;

                        if (!tehditMetni.ToUpper().Contains("KRİTİK") && !tehditMetni.ToUpper().Contains("KRITIK"))
                        {
                            tehditMetni = "[KRİTİK] " + tehditMetni;
                        }

                        komut.Parameters.AddWithValue("@GelenIP", ip);
                        komut.Parameters.AddWithValue("@GelenTehdit", tehditMetni);
                        komut.ExecuteNonQuery();
                    }
                }

                MessageBox.Show("Acil durum alarmı oluşturuldu ve loglandı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                textBox1.Clear();
                textBox2.Clear();

                KritikAlarmlariGetir();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Alarm ekleme hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // IP adresine göre canlı filtreleme
        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox3.Text))
            {
                if (kritikGorunumuAktif)
                    KritikAlarmlariGetir();
                else
                    TumLoglariGetir();
                return;
            }

            try
            {
                using (SqlConnection baglanti = new SqlConnection(baglantiAdresi))
                {
                    baglanti.Open();
                    string sorgu = @"
                        SELECT 
                            t.LogID, 
                            t.IP_Adresi, 
                            t.TehditTuru, 
                            t.KayitTarihi,
                            CASE WHEN k.AlarmID IS NOT NULL THEN 1 ELSE 0 END AS IsKritik
                        FROM TehditLoglari t
                        LEFT JOIN (SELECT DISTINCT LogID, AlarmID FROM KritikAlarmlar) k ON t.LogID = k.LogID
                        WHERE t.IP_Adresi LIKE @ArananIP
                        ORDER BY t.KayitTarihi DESC";

                    using (SqlCommand komut = new SqlCommand(sorgu, baglanti))
                    {
                        komut.Parameters.AddWithValue("@ArananIP", "%" + textBox3.Text.Trim() + "%");

                        using (SqlDataAdapter adaptor = new SqlDataAdapter(komut))
                        {
                            DataTable tablo = new DataTable();
                            adaptor.Fill(tablo);

                            dataGridView1.DataSource = tablo;
                            TabloyuRenklendir();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Filtreleme hatası: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Canlı izleme
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox3.Text))
            {
                if (kritikGorunumuAktif)
                    KritikAlarmlariGetir();
                else
                    TumLoglariGetir();
            }
        }
    }
}

