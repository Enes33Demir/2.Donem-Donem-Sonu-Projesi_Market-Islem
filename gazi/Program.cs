using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Data.SqlClient;
using Terminal.Gui;

namespace MarketTui
{
    
    //Urun Sınıfının Tanımlandığı Kısım
    class Urun
    {
        public string Id { get; set; } = "";
        public string Isim { get; set; } = "";
        public string Markasi { get; set; } = "";
        public string Kategorisi { get; set; } = "";
        public decimal AlisFiyati { get; set; }
        public decimal SatisFiyati { get; set; }
        public int StokMiktari { get; set; }
        public int StokUyariSiniri { get; set; } = 10; 
        public int SimSatisSayisi { get; set; } = 0; // Şuanda Yok ama DB de var uyumluluk için

        public decimal KarYuzdesi()
        {
            if (AlisFiyati > 0)
            {
                return Math.Round((SatisFiyati - AlisFiyati) / AlisFiyati * 100, 1);
            }
            else
            {
                return 0;
            }
        }
        public string EkrandaGorunecekStokDurumu()
        {
            if ( StokMiktari == 0)
            {
                return "BITTI";
            }

            if (StokMiktari <= StokUyariSiniri)
            {
                return "AZ";
            }
            return "OK";
                
        }
    }

    //Veri Tabanı İçin Gereken Fonksiyonları Tutan Class
    static class VeritabaniIslemleri
    {
        //SQL Tablosuna Bağlanmamız İçin Gereken String
        const string BaglantiCumlesi = "Server=localhost,1433;Database=MarketDB;User Id=sa;Password=Gazi_123!Market;TrustServerCertificate=True;Encrypt=False;";
        //SQL Güvenli Bağtantı İçin Fonksiyon
        public static SqlConnection BaglantiyiAc() { var b = new SqlConnection(BaglantiCumlesi); b.Open(); return b; }
        //SQL Tablosuna Sorgusu İçin Fonksiyonlar
        static void SorguyuCalistir(SqlConnection b, string s) { using var k = new SqlCommand(s, b); k.ExecuteNonQuery(); }

        // Tablo Yoksa Yapmak İçin
        public static void TablolariOlusturYoksa()
        {
            using var baglanti = BaglantiyiAc();
            string sqlMetni = @"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Kategoriler') CREATE TABLE Kategoriler (Id INT IDENTITY PRIMARY KEY, Adi NVARCHAR(100) UNIQUE);
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Markalar') CREATE TABLE Markalar (Id INT IDENTITY PRIMARY KEY, Adi NVARCHAR(100) UNIQUE);
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Urunler') CREATE TABLE Urunler (
                    Id NVARCHAR(20) PRIMARY KEY, Adi NVARCHAR(200), Marka NVARCHAR(100), Kategori NVARCHAR(100),
                    GelisFiyati DECIMAL(18,2), SatisFiyati DECIMAL(18,2), Stok INT, MinStokAlarm INT, SimSatisSayisi INT DEFAULT 0);";
            SorguyuCalistir(baglanti, sqlMetni);
        }

        //SQLdeki Ürenleri List<Urun> Olarak Çıkartır
        public static List<Urun> UrunleriGetir()
        {
            var liste = new List<Urun>();
            using var baglanti = BaglantiyiAc();
            using var k = new SqlCommand("SELECT Id,Adi,Marka,Kategori,GelisFiyati,SatisFiyati,Stok,MinStokAlarm,ISNULL(SimSatisSayisi,0) FROM Urunler", baglanti);
            using var o = k.ExecuteReader();
            while (o.Read()) liste.Add(new Urun {
                Id = o.GetString(0), Isim = o.GetString(1), Markasi = o.GetString(2), Kategorisi = o.GetString(3),
                AlisFiyati = o.GetDecimal(4), SatisFiyati = o.GetDecimal(5), StokMiktari = o.GetInt32(6),
                StokUyariSiniri = o.GetInt32(7), SimSatisSayisi = o.GetInt32(8)
            });
            return liste;
        }

        //SQL Urun EKlemek ve Güncellemek İçin
        public static void UrunEkleYadaGuncelle(Urun u, bool yeniMi)
        {
            using var b = BaglantiyiAc();
            string sql = yeniMi ? "INSERT INTO Urunler VALUES(@Id,@Adi,@Marka,@Kat,@GP,@SP,@Stok,@Alarm,@Sim)" : 
                                  "UPDATE Urunler SET Adi=@Adi, Marka=@Marka, Kategori=@Kat, GelisFiyati=@GP, SatisFiyati=@SP, Stok=@Stok, MinStokAlarm=@Alarm, SimSatisSayisi=@Sim WHERE Id=@Id";
            using var k = new SqlCommand(sql, b);
            k.Parameters.AddWithValue("@Id", u.Id); k.Parameters.AddWithValue("@Adi", u.Isim); k.Parameters.AddWithValue("@Marka", u.Markasi);
            k.Parameters.AddWithValue("@Kat", u.Kategorisi); k.Parameters.AddWithValue("@GP", u.AlisFiyati); k.Parameters.AddWithValue("@SP", u.SatisFiyati);
            k.Parameters.AddWithValue("@Stok", u.StokMiktari); k.Parameters.AddWithValue("@Alarm", u.StokUyariSiniri); k.Parameters.AddWithValue("@Sim", u.SimSatisSayisi);
            k.ExecuteNonQuery();
        }

        // İstenilen Ürünü Sİlmek İçin
        public static void UrunSil(string id) { using var b = BaglantiyiAc(); using var k = new SqlCommand("DELETE FROM Urunler WHERE Id=@Id", b); k.Parameters.AddWithValue("@Id", id); k.ExecuteNonQuery(); }
        public static List<string> ListeGetir(string tablo) {
            var l = new List<string>(); using var b = BaglantiyiAc(); using var k = new SqlCommand("SELECT Adi FROM " + tablo + " ORDER BY Adi", b);
            using var o = k.ExecuteReader(); while (o.Read()) l.Add(o.GetString(0)); return l;
        }
        public static void TanimEkle(string tablo, string adi) { try { using var b = BaglantiyiAc(); using var k = new SqlCommand("IF NOT EXISTS (SELECT 1 FROM "+tablo+" WHERE Adi=@A) INSERT INTO "+tablo+"(Adi) VALUES(@A)", b); k.Parameters.AddWithValue("@A", adi); k.ExecuteNonQuery(); } catch {} }
    }

    class Program
    {
        //Temel Değişkenler
        static List<Urun> hafizadakiTumUrunler = new();
        static List<string> hafizadakiKategoriler = new();
        static List<string> hafizadakiMarkalar = new();
        static DataTable tabloVerileri = null!;
        static TableView anaEkrandaGorunenTablo = null!;
        static Label altTarafBilgiMesaji = null!, sagAlttakiOzetMesaji = null!;

        static void Main()
        {
            try { VeritabaniIslemleri.TablolariOlusturYoksa(); } catch (Exception hata) { Console.WriteLine("BAĞLANTI HATASI:\n" + hata.Message); return; }
            VerileriYukle(); 
            Application.Init();

            // Renk Paleti Tasarım İçin
            Colors.Base.Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black);
            Colors.Base.Focus = Application.Driver.MakeAttribute(Color.Black, Color.BrightGreen);
            Colors.Dialog.Normal = Application.Driver.MakeAttribute(Color.White, Color.DarkGray);
            Colors.Dialog.Focus = Application.Driver.MakeAttribute(Color.Black, Color.Cyan);

            //TUI Tasarımı İçin Ust Bar
            var ustMenuCubugu = new MenuBar(new MenuBarItem[] {
                new("_Islemler", new MenuItem[] {
                    new("_Satis Yap", "^S", EkrandaSatisYap),
                    new("_Yeni Ekle", "^N", EkrandaYeniUrunEkle),
                    new("_Duzenle", "^E", EkrandaUrunuDuzenle),
                    new("_Sil", "^D", EkrandaUrunuSil),
                    new("_Cikis", "^Q", () => Application.RequestStop())
                }),
                new("_Raporlar", new MenuItem[] { new("_Alarmlar", "^A", BitenUrunlerinAlarminiGoster), new("_Kar Analizi", "^R", KarRaporunuGoster), new("_Ozet", "", OzetIstatistikleriGoster) }),
                new("_Tanimlar", new MenuItem[] { new("_Kategori Ekle", "", () => TanimEkle("Kategoriler")), new("_Marka Ekle", "", () => TanimEkle("Markalar")) })
            });

            //Ana Çerçeve İçin 
            var anaPencere = new Window("A101 Market Otomasyonu") { X = 0, Y = 1, Width = Dim.Fill(), Height = Dim.Fill() - 2 };
            var aramaKutusu = new TextField("") { X = 9, Y = 0, Width = 28 };
            aramaKutusu.TextChanged += _ => TablonunIciniDoldur(aramaKutusu.Text.ToString() ?? "");

            tabloVerileri = new DataTable();
            string[] sutunlar = { "ID", "Urun Adi", "Marka", "Kategori", "Gelis TL", "Satis TL", "Kar %", "Stok", "Durum" };
            foreach (var s in sutunlar) tabloVerileri.Columns.Add(s);
            
            anaEkrandaGorunenTablo = new TableView(tabloVerileri) { X = 0, Y = 2, Width = Dim.Fill(), Height = Dim.Fill() - 3, FullRowSelect = true };
            
            // SAĞ TIK MENÜSÜ
            anaEkrandaGorunenTablo.MouseClick += (args) => {
                if (args.MouseEvent.Flags.HasFlag(MouseFlags.Button3Clicked)) {
                    var contextMenu = new ContextMenu();
                    contextMenu.Position = new Point(args.MouseEvent.X + 2, args.MouseEvent.Y + 2);
                    contextMenu.MenuItems = new MenuBarItem(new MenuItem[] {
                        new MenuItem("_Satis Yap", "", EkrandaSatisYap),
                        new MenuItem("_Duzenle", "", EkrandaUrunuDuzenle),
                        new MenuItem("_Stok Guncelle", "", EkrandaStokDegistir),
                        new MenuItem("_Sil", "", EkrandaUrunuSil)
                    });
                    contextMenu.Show();
                }
            };

            altTarafBilgiMesaji = new Label("Hazir.") { X = 0, Y = Pos.Bottom(anaEkrandaGorunenTablo) + 1, Width = Dim.Fill() - 40 };
            sagAlttakiOzetMesaji = new Label("") { X = Pos.Right(altTarafBilgiMesaji), Y = Pos.Bottom(anaEkrandaGorunenTablo) + 1, Width = 40 };

            // Boş kaldığı için hata almaması için
            TablonunIciniDoldur("");

            anaPencere.Add(new Label("Filtre:") { X = 1, Y = 0 }, aramaKutusu, anaEkrandaGorunenTablo, altTarafBilgiMesaji, sagAlttakiOzetMesaji);
            Application.Top.Add(ustMenuCubugu, anaPencere);
            Application.Run(); 
            Application.Shutdown();
        }
        //SQL deki verileri Çekme
        static void VerileriYukle() {
            hafizadakiTumUrunler = VeritabaniIslemleri.UrunleriGetir(); 
            hafizadakiKategoriler = VeritabaniIslemleri.ListeGetir("Kategoriler"); 
            hafizadakiMarkalar = VeritabaniIslemleri.ListeGetir("Markalar"); 
        }

        //Grafik Arayüzü Tablomuz İçin Verileri Yerleştirme
        static void TablonunIciniDoldur(string arama)
        {
            if (tabloVerileri == null) return;
            tabloVerileri.Rows.Clear();
            var filtreli = hafizadakiTumUrunler.Where(u => string.IsNullOrEmpty(arama) || u.Isim.ToLower().Contains(arama.ToLower()) || u.Markasi.ToLower().Contains(arama.ToLower()));
            foreach (var u in filtreli) tabloVerileri.Rows.Add(u.Id, u.Isim, u.Markasi, u.Kategorisi, string.Format("{0:N2}", u.AlisFiyati), string.Format("{0:N2}", u.SatisFiyati), "%" + u.KarYuzdesi, u.StokMiktari, u.EkrandaGorunecekStokDurumu);
            
            if (anaEkrandaGorunenTablo != null) anaEkrandaGorunenTablo.Update();
            
            if (sagAlttakiOzetMesaji != null) {
                decimal toplamGuncelKar = hafizadakiTumUrunler.Sum(x => (x.SatisFiyati - x.AlisFiyati) * x.StokMiktari);
                sagAlttakiOzetMesaji.Text = string.Format("Urun:{0} Kar:{1:N0}TL", hafizadakiTumUrunler.Count, toplamGuncelKar);
            }
        }

        //Satır Seçmek İçin
        static Urun? TablodanSecilenUrun() { 
            int s = anaEkrandaGorunenTablo.SelectedRow; 
            if (s < 0 || s >= tabloVerileri.Rows.Count) return null;
            string id = tabloVerileri.Rows[s][0].ToString();
            return hafizadakiTumUrunler.FirstOrDefault(u => u.Id == id);
        }
        
        //Bildirim Özelliği
        static void MesajGoster(string m) => altTarafBilgiMesaji.Text = m;
        //Bildirim Paneli TUI
        static TextField MetinKutusuOlustur(Dialog p, string yazi, string varsayilan, int y) { p.Add(new Label(yazi) { X=1, Y=y }); var k = new TextField(varsayilan) { X=20, Y=y, Width=25 }; p.Add(k); return k; }

        //Satışla Ürünleri Azaltmak İçin
        static void EkrandaSatisYap() {
            var u = TablodanSecilenUrun(); if (u == null) return;
            var p = new Dialog("Satis: " + u.Isim, 40, 8);
            var k = MetinKutusuOlustur(p, "Adet:", "1", 2);
            var btnSatis = new Button("Satis Yap");
            btnSatis.Clicked += () => {
                if (int.TryParse(k.Text.ToString(), out int miktar) && miktar > 0 && miktar <= u.StokMiktari) {
                    u.StokMiktari -= miktar;
                    VeritabaniIslemleri.UrunEkleYadaGuncelle(u, false);
                    TablonunIciniDoldur(""); 
                    MesajGoster("Satis Basarili!"); 
                    Application.RequestStop();
                } else MessageBox.ErrorQuery("Hata", "Gecersiz miktar veya yetersiz stok!", "Tamam");
            };
            p.AddButton(btnSatis); Application.Run(p);
        }
        
        //Urun Ekleme
        static void EkrandaYeniUrunEkle() { UrunPenceresi(new Urun { Id = (hafizadakiTumUrunler.Count == 0 ? "1001" : (hafizadakiTumUrunler.Max(x => int.Parse(x.Id)) + 1).ToString()) }, true); }
        //Urun Düzenleme
        static void EkrandaUrunuDuzenle() { var u = TablodanSecilenUrun(); if (u != null) UrunPenceresi(u, false); }

        //Urun Özel Sayfası
        static void UrunPenceresi(Urun urun, bool yeniMi)
        {
            var pencere = new Dialog(yeniMi ? "Yeni Urun" : "Duzenle", 50, 14);
            var idKutu = MetinKutusuOlustur(pencere, "ID:", urun.Id, 1); idKutu.ReadOnly = true;
            var isimKutu = MetinKutusuOlustur(pencere, "Urun Adi:", urun.Isim, 2);
            var markaKutu = MetinKutusuOlustur(pencere, "Marka:", urun.Markasi, 3);
            var katKutu = MetinKutusuOlustur(pencere, "Kategori:", urun.Kategorisi, 4);
            var alisKutu = MetinKutusuOlustur(pencere, "Alis TL:", urun.AlisFiyati.ToString(), 5);
            var satisKutu = MetinKutusuOlustur(pencere, "Satis TL:", urun.SatisFiyati.ToString(), 6);
            var stokKutu = MetinKutusuOlustur(pencere, "Stok:", urun.StokMiktari.ToString(), 7);

            var kaydet = new Button("Kaydet");
            kaydet.Clicked += () => {
                try {
                    urun.Isim = isimKutu.Text.ToString(); urun.Markasi = markaKutu.Text.ToString(); urun.Kategorisi = katKutu.Text.ToString();
                    urun.AlisFiyati = decimal.Parse(alisKutu.Text.ToString()); urun.SatisFiyati = decimal.Parse(satisKutu.Text.ToString());
                    urun.StokMiktari = int.Parse(stokKutu.Text.ToString());
                    VeritabaniIslemleri.UrunEkleYadaGuncelle(urun, yeniMi);
                    if (yeniMi) hafizadakiTumUrunler.Add(urun);
                    TablonunIciniDoldur(""); Application.RequestStop();
                } catch { MessageBox.ErrorQuery("Hata", "Degerleri kontrol edin!", "Tamam"); }
            };
            pencere.AddButton(kaydet); Application.Run(pencere);
        }

        //Ürünü Kaldırma
        static void EkrandaUrunuSil() {
            var u = TablodanSecilenUrun(); if (u == null) return;
            if (MessageBox.Query("Sil", u.Isim + " silinsin mi?", "Evet", "Hayir") == 0) {
                VeritabaniIslemleri.UrunSil(u.Id); 
                hafizadakiTumUrunler.Remove(u); 
                TablonunIciniDoldur("");
            }
        }

        // Stoklarla Oynama
        static void EkrandaStokDegistir() {
            var u = TablodanSecilenUrun(); if (u == null) return;
            var p = new Dialog("Stok Guncelle", 40, 7);
            var k = MetinKutusuOlustur(p, "Eklenecek:", "0", 2);
            var btn = new Button("Guncelle");
            btn.Clicked += () => {
                if (int.TryParse(k.Text.ToString(), out int fark)) {
                    u.StokMiktari += fark; 
                    VeritabaniIslemleri.UrunEkleYadaGuncelle(u, false);
                    TablonunIciniDoldur(""); 
                    Application.RequestStop();
                }
            };
            p.AddButton(btn); Application.Run(p);
        }

        //Biten Ürünler İçin Uyarı
        static void BitenUrunlerinAlarminiGoster() {
            string m = string.Join("\n", hafizadakiTumUrunler.Where(x => x.StokMiktari <= x.StokUyariSiniri).Select(x => x.Isim + ": " + x.StokMiktari));
            MessageBox.Query("Alarmlar", string.IsNullOrEmpty(m) ? "Stoklar iyi." : m, "Tamam");
        }
        
        //Kar Raporunu Göster
        static void KarRaporunuGoster() {
            string y = string.Join("\n", hafizadakiTumUrunler.Select(u => $"{u.Isim}: %{u.KarYuzdesi} Kar"));
            MessageBox.Query("Kar Analizi", string.IsNullOrEmpty(y) ? "Urun yok." : y, "Kapat");
        }

        static void OzetIstatistikleriGoster() {
            string m = $"Urun: {hafizadakiTumUrunler.Count}\nStok: {hafizadakiTumUrunler.Sum(x => x.StokMiktari)}\nBiten: {hafizadakiTumUrunler.Count(x => x.StokMiktari == 0)}";
            MessageBox.Query("Ozet", m, "Kapat");
        }

        static void TanimEkle(string tablo) {
            var p = new Dialog("Yeni " + tablo, 40, 7);
            var k = MetinKutusuOlustur(p, "Adi:", "", 2);
            var btn = new Button("Ekle");
            btn.Clicked += () => {
                if (!string.IsNullOrWhiteSpace(k.Text.ToString())) {
                    VeritabaniIslemleri.TanimEkle(tablo, k.Text.ToString());
                    VerileriYukle(); 
                    TablonunIciniDoldur("");
                    Application.RequestStop();
                }
            };
            p.AddButton(btn); Application.Run(p);
        }
    }
}
