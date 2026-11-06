# Staj Evrak Dogrulama ve Yonetim Sistemi

Bu uygulamanin amaci:
ogrencilerin staj basvurusu icin hazirladigi Ek-16 belgesinin kontrolunu ve takibini kolaylastirmaktir. sistemdeki bilgilerle ogrencinin bilgileri karsilastirilir. hatali olanlar ogrenciye bildirilir. ogrenci hatasini duzeltir. yoneticiler de basvurulari tek yerden kontrol eder.
---------------------------------------------------------------------------------------------
Mevcut ozellikler:
*kullanici kaydi ve girisi - ogrenciler kendi sifrelerini belirleyerek hesap olusturur. sifreler hashlenerek saklanir
*hesap kitleme             - 5 kez hatali girilince hesap kitleniyor. 15dk sonra tekrar aciliyor. basarisiz girisler kaydediliyor.
*yetkilendirme             - ogrenci ve admin rolleri bulunuyor. ogrenci sadece kendi belge ve basvurusuna erisebiliyor.
*oturum yonetimi           - birden fazla kullanici icin oturum olusturma, sure kontrolu, cikis islrmleri
*veri seti aktarimi        - admin, CSV ve JSON dosyalarindan ogrenci bilgilerini iceri aktarabiliyor. hatali satirlar raporlaniyor.Tekrarlanan kayitlarin yeniden eklenmesini onler.
*basvuru yonetimi          - basvuru olusturma, goruntuleme, guncelleme, silme, islemleri bulunur. admin basvurulari duruma gore listeleyebilir.
*ek-16 yukleme             - Word, PDF, JPEG ve PNG dosyalari kontrol edilip kaydedilebiliyor.
*bilgi karsilastirma       - onceden okunup servise verilen ogrenci numarasi, kimlik numarasi, ad soyad, bolum ve ogretim yili karsilastirilabiliyor.
*durum takibi              - degisiklik gecmisi tutuyor. tamamlanan basvurularda ogrencinin degistirmesi engelleniyor. admin degistiriyor.
*tarih kontrolleri         - ogretim yili bicimi, takvimle yil eslesmesi, 30 gun kurali ve ders donemi cakismasi kontrol edilebiliyor. bu kontroller henuz basvuru akisina baglanmadi.
---------------------------------------------------------------------------------------------
Projede kullanilan teknolojiler:
C#                     - uygulamanin yazildigi temel programlama dili
.NET 8                 - C# kodlarini derlemek ve calistirmak icin kullanilan platform
SQLite                 - kullanici, basvuru ve belge bilgilerini saklayan veri tabani
Microsoft.Data.Sqlite  - C# uygulamasinin SQLite veritabani ile  iletisim kurmasini saglayan paket.
---------------------------------------------------------------------------------------------
Kurulum ve calistirma
henuz kurulmadi.
---------------------------------------------------------------------------------------------
Klasor yapisi:
Staj_Evrak_Sistemi/
    
    README.md (proje hakkinda bilgilerin oldugu dosya)
    
    StajEvrakSistemi.sln (Hangi C# projelerinin birlikte acilip derlenecegini tanimlar)
   
    StajEvrakSistemi/
        
        Core/
            ApplicationStatus.cs (Basvurunun alabilecegi durumlari tanimlar. yuklendi, inceleniyor, hatali, Onaylandi ve Tamamlandi)
            DocumentType.cs (Sistemin isledigi belge turlerini tanimlar. su anda yalnizca Ek-16 bulunuyor.)
            IdentityNumberValidator.cs (Kimlik numarasini kontrol eder. her kimligin kendi algoritmasi vardir)
            Permission.cs (Sistemde yapilabilecek yetkili islemleri-fonksiyonlari tanimlar.)
            RolePermissions.cs (Permission.cs deki islemleri, hangi rollerin yapabilecegini kisitlar)
            UserRole.cs (Aktif olan kullanici rollerini tanimlar.)
            InternshipDateValidator.cs (ogretim yilinin dogru yazilip yazilmadigini ve takvimle ayni yilda olup olmadigini kontrol eder. 30 gun kuralini, son teslim tarihini ve ders donemi cakismasini da kontrol eder.)

        
        Data/
            DatabaseManager.cs (uygulamanin SQLite veritabanina baglanmasini ve gerekli tablolarin hazirlanmasini saglar.)
            AcademicCalendars.cs (kocaeli universitesinin 2026-2027 guz ve bahar ders baslangic ve bitis tarihlerini saklar.)
        
        Models/
            User.cs (butun kullanicilarin ortak temel sinifi.  Kimlik, ad soyad, e-posta, telefon vb...)
            Student.cs (ogrenciyi temsil eder. user dan turetilir. ogrenciyi yetkilerini tanimlar)
            Admin.cs (admini temsil eder. user dan turetilmistir. admin yetkilerini tanimlar)
            BaseDocument.cs (Belgelerin ortak temel sinifidir.dosya yolu, yuklenme zamani, onay/ret bilgileri ve ortak dosya kontrollerini icerir.)
            Ek16Document.cs (Ek-16 belgesini temsil eder.uzantilari, dosya boyutunu ve dosya iceriginin baslangic baytlarini kontrol eder.)
            InternshipApplication.cs (ogrencinin staj basvurusunu temsil eder. Tarihler, isyeri, bolum, iletisim bilgileri, calisma gunleri, muhendis sayisi ve basvuru durumunu icerir.)
            OfficialStudentRecord.cs (Adminin yukledigi referans veri setindeki bir ogrenci kaydini temsil eder. ogrenci, kimlik, uyruk, ogrenim ve iletisim bilgilerini iserir.InternshipApplication.cs ile karsilastirilirken kullanilir. )
            Ek16ComparisonResult.cs (Veri setiyle yapilan karsilastirmanin sonucunu tasir. Eslesen kayit ve alanlar arasindaki uyusmazliklari icerir.)
            ScannedDocumentComparisonResult.cs (ogrencinin girdigi bilgilerle belgeden gelen alanlarin karsilastirma sonucunu tasir. Ayni dosyada, okunmus belge alanlarini tasiyan ScannedDocumentFields modeli de bulunur.)
            StatusTransitionLog.cs (Basvurunun durum degisikligini temsil eder. onceki/yeni durum, islemi yapan kullanici, tarih ve gerekceyi icerir.)
            FailedLoginLog.cs (Basarisiz giris denemesini temsil eder. Kullanici bilgisi, kullanilan giris bilgisi, basarisizlik nedeni ve zamani icerir.)
            AcademicCalendar.cs (ogretim yilini ve guz/bahar ders tarihlerini tasir. takvim olusturulurken ogretim yili bicimini ve tarih araliklarini kontrol eder.)
        
        Services/
            AuthService.cs	(Kullanici kaydi, giris, hesap kilitleme ve basarisiz giris kayitlarini yonetir.)
            AuthorizationService.cs	(Oturum, rol ve basvuru sahipligine gore kullanicinin islem yapma yetkisini kontrol eder.)
            AuthorizationResult.cs	(Yetki kontrolunun sonucunu tasir: izin verildi, giris gerekli veya yetkisiz erisim.)
            AuthorizationExceptions.cs	(Giris yapilmamasi ve yetkisiz erisim durumlarinda kullanilacak hata siniflarini tanimlar.)
            DatasetImportService.cs	(Adminin CSV/JSON veri setini okur, kayitlari kontrol eder ve veritabanina aktarir.)
            DatasetImportResult.cs	(Veri seti aktariminin sonuclarini tasir: eklenen, tekrarlanan ve hatali kayitlar gibi.)
            DocumentService.cs	(Ek-16 yukleme, dosyayi depolama, belge kaydi olusturma, listeleme ve silme islemlerini yapar.)
            DocumentValidationException.cs	(Dosya kabul edilmediginde, ornegin formati veya boyutu uygun degilse, hata nedenlerini tasir.)
            Ek16MatcherService.cs	(ogrenci bilgilerini, belgeden gelen alanlari ve adminin veri setini karsilastirip uyusmazliklari belirler.)
            InternshipService.cs	(Staj basvurusu olusturma, goruntuleme, guncelleme, silme ve duruma gore listeleme islemlerini yapar.)
            LoginResult.cs	(Giris isleminin sonucunu tasir: basarili giris, yanlis bilgiler veya kilitli hesap.)
            PasswordHasher.cs	(sifreleri rastgele salt kullanarak hash ler; giriste verilen sifreyi kayitli hash ile dogrular.)
            SessionManager.cs	(Kullanici oturumlarini olusturur, token ve sure kontrolu yapar, cikista oturumu kapatir.)
            UserSession.cs	(Bir oturumun kullanici numarasi, rolu, token i, baslangic ve bitis zamanini tasir.)
            StatusWorkflowEngine.cs	(Basvuru durum gecislerini, guncelleme/silme izinlerini ve durum degisikligi gecmisini yonetir.)
            UserGuard.cs	(islem yapan kullanicinin veritabaninda bulundugunu ve rolunun kayitli rolle eslestigini kontrol eder.)
            
        
        Program.cs(	Uygulamanin baslangic noktasi)
        
        StajEvrakSistemi.csproj (.NET surumu ve kullanilan paketleri tanimlar.)
---------------------------------------------------------------------------------------------
Gelistirme durumu

Temel veritabani, kullanici islemleri, yetkilendirme, basvuru yonetimi, veri seti aktarimi ve belge yukleme servisleri gelistirilmistir. Ogretim yili bicimi, takvimle yil eslesmesi, 30 gun kurali ve ders donemi cakisma kontrolleri de eklendi. tarih kontrolleri henuz basvuru akisina baglanmadi. Web arayuzuyle entegrasyon henuz yapilmamistir.
---------------------------------------------------------------------------------------------
Yapilacak isler

Yariyil bilgisinin ve staj tarihlerinin secilen akademik takvimle uyumunun kontrol edilmesi.

Hazirlanan tarih kontrollerinin basvuru akisina baglanmasi.

Staj gunlerinin hesaplanmasi ve resmi tatillerin cikarilmasi.

Mevcut kimlik dogrulama kurallarinin basvuru akisina baglanmasi.

Adres, telefon, e-posta ve faks kontrollerinin tamamlanmasi.

Muhendis sayisi ve gerekli onay alanlarinin kontrolu.

Ogrenci ve admin web arayuzlerinin hazirlanmasi.

PDF/Word belgelerinden otomatik bilgi okuma ve OCR entegrasyonu.

Google Form'a otomatik bilgi aktarimi ve Ek-17 olusturulmasi.

Raporlama ekranlarinin, diyagramlarin ve otomatik testlerin hazirlanmasi.

Bilinen sorunlar

Belge, ogrenci bilgileri ve referans veri seti karsilastirmalari tek bir zorunlu onay akisina baglanmamistir. Bazi hatali veya belgesiz basvurular tamamlanabilmektedir.

Belgedeki ogrencinin basvurunun sahibiyle eslesmesi her karsilastirma yolunda zorunlu tutulmamaktadir.

Belge yeniden yuklendiginde veya silindiginde onceki dogrulama sonucu gecerli kalabilmektedir.

Onaylanan basvuru admin tarafindan degistirildikten sonra tamamlanma asamasinda yeniden dogrulama kontrolu eksiktir.
---------------------------------------------------------------------------------------------
Tarih kontrolleri
Ogretim yili 2026-2027 gibi ardisik iki yildan olusmali. 2026-2028 veya 2026/2027 gibi girisler kabul edilmiyor.
Basvurudaki ogretim yili ile secilen takvimin yili karsilastiriliyor. hatali ogretim yiliyla takvim olusturulamiyor.
Basvuru tarihi ile staj baslangici arasinda en az 30 takvim gunu bulunmasi kontrol edilir. Tam 30 gun gecerli kabul edilir.
Staj baslangicina gore en gec teslim tarihi hesaplanir.
Staj tarih araliginin guz veya bahar ders donemiyle cakismasi kontrol edilir. Donemlerin baslangic ve bitis gunleri kontrole dahildir.
Bitis tarihinin baslangic tarihinden once olmasi engellenir.
kocaeli universitesinin 2026-2027 genel onlisans ve lisans ders tarihleri;
donem   -       ders baslangici     -       ders bitisi 
guz     -       21 eylul 2026       -       25 Aralik 2026
bahar   -       8 Subat 2027        -       28 Mayis 2027

Bu cakisma kontrolu sadece ders donemlerini kapsiyor. sinav donemleri ve resmi tatiller dahil degil.
Kontrol metotlari hazir. basvuru akisina ve web arayuzune baglanmasi henuz yapilmadi.
---------------------------------------------------------------------------------------------