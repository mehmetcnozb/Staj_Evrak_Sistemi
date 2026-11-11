# Staj Evrak Dogrulama ve Yonetim Sistemi

Bu uygulamanin amaci:
ogrencilerin staj basvurusu icin hazirladigi Ek-16 belgesinin kontrolunu ve takibini kolaylastirmaktir. sistemdeki bilgilerle ogrencinin bilgileri karsilastirilir. hatali olanlar ogrenciye bildirilir. ogrenci hatasini duzeltir. yoneticiler de basvurulari tek yerden kontrol eder.
---------------------------------------------------------------------------------------------
Mevcut ozellikler:
*kullanici kaydi ve girisi - ogrenciler kendi sifrelerini belirleyerek hesap olusturur. sifreler hashlenerek saklanir
*kimlik kontrolu           - yeni kayitlarda T.C. kimlik numarasinin 10. ve 11. haneleri kontrol ediliyor. yabanci ogrencilerde 11 hane ve 99 ile baslama kurali kullaniliyor. yeni veri seti aktarimlarinda da bu kontroller yapiliyor.
*format kontrolleri        - ogrenci numarasi, e-posta, telefon ve adres kontrol ediliyor. basvuruda adres, telefon ve e-posta zorunlu. faks bos birakilabilir, girilmisse bicimi kontrol ediliyor.
*hesap kitleme             - 5 kez hatali girilince hesap kitleniyor. 15dk sonra tekrar aciliyor. basarisiz girisler kaydediliyor.
*yetkilendirme             - ogrenci ve admin rolleri bulunuyor. ogrenci sadece kendi belge ve basvurusuna erisebiliyor.
*oturum yonetimi           - birden fazla kullanici icin oturum olusturma, sure kontrolu, cikis islrmleri
*veri seti aktarimi        - admin, CSV ve JSON dosyalarindan ogrenci bilgilerini iceri aktarabiliyor. hatali satirlar raporlaniyor.Tekrarlanan kayitlarin yeniden eklenmesini onler.
*basvuru yonetimi          - basvuru olusturma, goruntuleme, guncelleme, silme, islemleri bulunur. admin basvurulari duruma gore listeleyebilir.
*ek-16 yukleme             - Word, PDF, JPEG ve PNG dosyalari kontrol edilip kaydedilebiliyor.
*bilgi karsilastirma       - son belgenin ogrenci bilgileri, staj tarihleri, iletisim alanlari, yariyili ve isyeri adi form ile karsilastiriliyor. veri setindeki ogrenci bilgileri ve mevcut iletisim degerleri de kontrol ediliyor. sonuc belgeye kaydediliyor. tek basina veri seti eslesmesi onay vermiyor.
*muhendis ve onay kontrolu - en az 1 muhendis olmasi kontrol ediliyor. sayi 5'in altindaysa mezuniyet bolumu icin uyari bilgisi hazirlaniyor. belgede ayri muhendis sayisi okunmussa form ile de karsilastiriliyor. adminin son belge icin kaydettigi onay, imza ve kase incelemesi denetleniyor.
*durum takibi              - degisiklik gecmisi tutuyor. tamamlanan basvurularda ogrencinin degistirmesi engelleniyor. admin degistiriyor.
*tarih kontrolleri         - ogretim yili, yariyil ve staj tarih araligi kayit ve guncellemede kontrol ediliyor. belge tesliminde en az 30 takvim gunu araniyor. onay ve tamamlanmada belge teslim tarihi tekrar denetleniyor.
*staj gunu hesabi          - secilen haftalik gunler ve 2026-2027 resmi tatillerine gore net staj gunu hesaplaniyor. belgedeki baslangic ve bitis form ile karsilastiriliyor. belgede ayri gun sayisi veya calisma gunleri varsa onlar da kontrol ediliyor.
---------------------------------------------------------------------------------------------
Projede kullanilan teknolojiler:
C#                     - uygulamanin yazildigi temel programlama dili
.NET 8                 - C# kodlarini derlemek ve calistirmak icin kullanilan platform
SQLite                 - kullanici, basvuru ve belge bilgilerini saklayan veri tabani
Microsoft.Data.Sqlite  - C# uygulamasinin SQLite veritabani ile  iletisim kurmasini saglayan paket.
---------------------------------------------------------------------------------------------
Kurulum ve calistirma

.NET 8 SDK ile depo kökünden ilk yönetici hesabını oluşturun.

```sh
dotnet run --project StajEvrakSistemi -- --create-admin
```

Ardından uygulamayı başlatın ve terminaldeki yerel adresi açın.

```sh
dotnet run --project StajEvrakSistemi -- --urls http://localhost:5080
```

Yönetici, panelden kurgu öğrenci kayıtlarını içeren örnek CSV'yi indirip içe aktarabilir.
Öğrenci hesabı giriş ekranından açılır. Veri seti eşleştirmesini denemek için CSV'deki
bir öğrencinin kimlik, öğrenci numarası ve iletişim bilgileriyle bir test hesabı açın.

Öğrenci staj tarihlerini ve haftalık günlerini seçer, başvurusunu kaydeder ve Ek-16 yükler.
Yönetici belgeyi indirip incelemeyi başlatır. Ek-16 alanlarını ve imza/kaşe durumunu
elle girdikten sonra karşılaştırma sonucu öğrenciye de görünür. Hata varsa yönetici
düzeltme ister. Kontrolleri geçen başvuru onaylandığında Tamamlandı sekmesine geçer.

Google Form alanı HTTPS yanıt bağlantısının biçimini denetler. Vize akışında belge
incelemesi manuel yapılır.

SQLite dosyası ve yüklenen belgeler varsayılan olarak proje klasöründe saklanır.
Farklı bir yerel veri klasörü için uygulamayı ve yönetici kurulumunu çalıştırmadan önce
`StajDataDirectory` ortam değişkenini ayarlayın.
---------------------------------------------------------------------------------------------
Klasor yapisi:
Staj_Evrak_Sistemi/
    
    README.md (proje hakkinda bilgilerin oldugu dosya)
    
    StajEvrakSistemi.sln (Hangi C# projelerinin birlikte acilip derlenecegini tanimlar)
   
    StajEvrakSistemi/
        
        Core/
            ApplicationStatus.cs (Basvurunun alabilecegi durumlari tanimlar. yuklendi, inceleniyor, hatali, Onaylandi ve Tamamlandi)
            DocumentType.cs (Sistemin isledigi belge turlerini tanimlar. su anda yalnizca Ek-16 bulunuyor.)
            IdentityNumberValidator.cs (T.C. kimlik numarasinin bicimini ve 10./11. kontrol hanelerini denetler. yabanci kimlik numarasinda 11 hane ve 99 onekini kontrol eder.)
            FormatValidator.cs (ogrenci numarasi, e-posta, telefon, adres ve faksi kontrol eder. basvurudaki format hatalarini liste olarak dondurur.)
            ContactDocumentValidator.cs (formdaki adres, telefon, e-posta ve faksi belgeden okunan degerlerle karsilastirir. alanlarin bicimini de kontrol eder. veri setinde bulunan iletisim bilgileri icin de karsilastirma yapar.)
            EngineerApprovalValidator.cs (muhendis sayisini ve belge onay alanlarini kontrol eder. muhendis sayisi 5'in altindaysa bolum bilgisi icin uyari dondurur.)
            ApplicationDocumentValidator.cs (belgedeki yariyil ve isyeri adini form ile karsilastirir. muhendis sayisi belgeden ayrica okunmussa onu da kontrol eder.)
            Permission.cs (Sistemde yapilabilecek yetkili islemleri-fonksiyonlari tanimlar.)
            RolePermissions.cs (Permission.cs deki islemleri, hangi rollerin yapabilecegini kisitlar)
            UserRole.cs (Aktif olan kullanici rollerini tanimlar.)
            InternshipDateValidator.cs (ogretim yili, yariyil ve staj tarih araligini kontrol eder. belgenin teslim tarihi icin 30 gun kuralini ve son teslim tarihini hesaplar. UTC zamanini Turkiye tarihine cevirir.)
            InternshipDayCalculator.cs (secilen haftalik gunler ve tatil listesiyle net staj gununu hesaplar. Ek-16 da belirtilen gun sayisiyla eslesmeyi kontrol eder.)
            InternshipDocumentValidator.cs (belgedeki staj tarihlerini form ile karsilastirir. iki tarih araligindan net gunu yeniden hesaplar. belgede ayri gun sayisi veya haftalik gun listesi varsa onlari da kontrol eder.)

        
        Data/
            DatabaseManager.cs (uygulamanin SQLite veritabanina baglanmasini ve gerekli tablolarin hazirlanmasini saglar.)
            AcademicCalendars.cs (secilen ogretim yilinin tarih sinirlarini hazirlar. 2026-2027 icin onaylanan yil araligini saklar.)
            TurkishPublicHolidays.cs (2026 ve 2027 resmi tatillerini saklar. verilen tarih araligindaki tam ve yarim gun tatilleri dondurur. ayni gune gelen bayramlari tek sayar.)
        
        Models/
            User.cs (butun kullanicilarin ortak temel sinifi. kimlik, yabanci kullanici bilgisi, ad soyad, e-posta, telefon vb... kimlik kontrolunu IsForeign bilgisine gore secer. e-posta ve girilmisse telefon bicimini kontrol eder.)
            Student.cs (ogrenciyi temsil eder. user dan turetilir. ogrenci yetkilerini tanimlar. yabanci ogrenci icin isForeign bilgisi verilebilir.)
            Admin.cs (admini temsil eder. user dan turetilmistir. admin yetkilerini tanimlar)
            BaseDocument.cs (Belgelerin ortak temel sinifidir. dosya yolu, yuklenme zamani, onay/ret bilgileri ve ortak dosya kontrollerini icerir. docx dosyasinda Word paket girdileri de kontrol edilir.)
            Ek16Document.cs (Ek-16 belgesini temsil eder. dosya ve onay incelemesi bilgilerini tasir. ogrenci, staj, iletisim, yariyil ve isyeri alanlarinin bu belge icin dogrulanip dogrulanmadigini ve karsilastirma zamanlarini da saklar.)
            DocumentApprovalFields.cs (ogrenci ve danisman onay alanlariyla isveren bolumu, kase ve imza inceleme sonuclarini tasir. null henuz incelenmedi demek.)
            InternshipApplication.cs (ogrencinin staj basvurusunu temsil eder. tarihler, yariyil, isyeri, bolum, iletisim bilgileri, calisma gunleri, net staj gunu, muhendis sayisi, mezuniyet bolumu ve basvuru durumunu tasir.)
            OfficialStudentRecord.cs (Adminin yukledigi referans veri setindeki bir ogrenci kaydini temsil eder. ogrenci, kimlik, uyruk, ogrenim ve iletisim bilgilerini iserir.InternshipApplication.cs ile karsilastirilirken kullanilir. )
            Ek16ComparisonResult.cs (veri seti karsilastirmasinin sonucunu tasir. belge dogrulamasinda ilgili belge numarasini da icerir. kimlik, uyruk, tarih, gun, iletisim, yariyil, isyeri ve muhendis sayisi uyusmazliklarini listeler. belge karsilastirmasinda hesaplanan net gunleri de tasir.)
            ScannedDocumentComparisonResult.cs (belgeden okunan alanlarin form ile karsilastirma sonucunu tasir. IsMatched belgenin tum kurallari gectigini soylemez. ScannedDocumentFields modeli de bu dosyadadir.)
            InternshipComparisonResult.cs (staj tarih ve gun karsilastirmasinin hatalarini, formdan ve belgeden hesaplanan net gunleri tasir.)
            StatusTransitionLog.cs (Basvurunun durum degisikligini temsil eder. onceki/yeni durum, islemi yapan kullanici, tarih ve gerekceyi icerir.)
            FailedLoginLog.cs (Basarisiz giris denemesini temsil eder. Kullanici bilgisi, kullanilan giris bilgisi, basarisizlik nedeni ve zamani icerir.)
            AcademicCalendar.cs (ogretim yilini ve tarih sinirlarini tasir. sinirlar verilmezse uygulama tercihi olarak 1 Eylul - 31 Agustos araligi kullanilir.)
        
        Services/
            AuthService.cs	(Kullanici kaydi, giris, hesap kilitleme ve basarisiz giris kayitlarini yonetir. kayitta kimlik kontrolu yapar. yabanci kullanici bilgisini veritabanina kaydeder ve giriste geri yukler.)
            AuthorizationService.cs	(Oturum, rol ve basvuru sahipligine gore kullanicinin islem yapma yetkisini kontrol eder.)
            AuthorizationResult.cs	(Yetki kontrolunun sonucunu tasir: izin verildi, giris gerekli veya yetkisiz erisim.)
            AuthorizationExceptions.cs	(Giris yapilmamasi ve yetkisiz erisim durumlarinda kullanilacak hata siniflarini tanimlar.)
            DatasetImportService.cs	(Adminin CSV/JSON veri setini okur, kimlik ve ogrenci numarasini kontrol eder. girilmis e-posta, telefon ve adresin bicimini de kontrol eder. hatali satirlar raporlanir, gecerli kayitlar veritabanina aktarilir.)
            DatasetImportResult.cs	(Veri seti aktariminin sonuclarini tasir: eklenen, tekrarlanan ve hatali kayitlar gibi.)
            DocumentService.cs	(Ek-16 yukleme, listeleme ve silme islemlerini yapar. her yuklemede staj tarihlerini ve 30 gun suresini kontrol eder, teslim zamanini sistem saatinden kaydeder. adminin son belge incelemesini de saklar. belge yuklenince veya silinince eski karsilastirma sonucu sifirlanir.)
            DocumentValidationException.cs	(Dosya kabul edilmediginde, ornegin formati veya boyutu uygun degilse, hata nedenlerini tasir.)
            Ek16MatcherService.cs	(kayitli kullanicinin yetkisini ve basvuru sahipligini kontrol eder. referansi basvuru sahibine gore bulur. son belgenin ogrenci alanlarini form ve veri setiyle karsilastirir. staj tarihlerini, gun bilgilerini, iletisim alanlarini, yariyili ve isyeri adini da kontrol eder. okunmussa muhendis sayisini da karsilastirir. tumu uyusursa belgeye bagli dogrulama kaydeder ve hesaplanan net gunu gunceller.)
            InternshipService.cs	(Basvuru olusturma, guncelleme, silme ve listeleme islemlerini yapar. format, ogretim yili ve tarih araligini kontrol eder. net staj gununu hesaplar ve kaydeder. guncellemede eski karsilastirma sonucu, belgeye ait tum karsilastirma dogrulamalari ile belge onayi sifirlanir.)
            LoginResult.cs	(Giris isleminin sonucunu tasir: basarili giris, yanlis bilgiler veya kilitli hesap.)
            PasswordHasher.cs	(sifreleri rastgele salt kullanarak hash ler; giriste verilen sifreyi kayitli hash ile dogrular.)
            SessionManager.cs	(Kullanici oturumlarini olusturur, token ve sure kontrolu yapar, cikista oturumu kapatir.)
            UserSession.cs	(Bir oturumun kullanici numarasi, rolu, token i, baslangic ve bitis zamanini tasir.)
            StatusWorkflowEngine.cs	(Basvuru durum gecislerini, izinleri ve degisiklik gecmisini yonetir. onaylanirken ve tamamlanirken veri seti dogrulamasi, muhendis bilgileri, son belge, belgeye kaydedilen tum karsilastirma dogrulamalari, onay alanlari, zorunlu form alanlari ve kayitli tarihler yeniden kontrol edilir.)
            UserGuard.cs	(islem yapan kullanicinin veritabaninda bulundugunu ve rolunun kayitli rolle eslestigini kontrol eder.)
            
        
        Program.cs(	Uygulamanin baslangic noktasi)
        
        StajEvrakSistemi.csproj (.NET surumu ve kullanilan paketleri tanimlar.)
---------------------------------------------------------------------------------------------
Gelistirme durumu

Temel veritabani, kullanici islemleri, yetkilendirme, basvuru yonetimi, veri seti aktarimi ve belge yukleme servisleri gelistirildi. Ogretim yili, yariyil ve staj tarih araligi kayit ve guncellemede denetleniyor. belge teslimi icin en az 30 takvim gunu araniyor. onay ve tamamlanmada son belgenin teslim tarihi kontrol ediliyor.
Net staj gunu secilen haftalik gunler ve resmi tatillerle hesaplaniyor. kimlik ve format kontrolleri de eklendi. muhendis sayisi 5'in altindaysa bolum bilgisi icin uyari hazirlaniyor. belge onay alanlari admin incelemesiyle kaydediliyor. Son belgedeki bes ogrenci alani basvuru sahibi ve veri setiyle birlikte kontrol ediliyor. belgedeki staj tarihleri, gun bilgileri, iletisim alanlari, yariyil ve isyeri adi da karsilastirmaya baglandi. karsilastirma belgenin numarasina ve zamana bagli kaydediliyor. Vize akışı öğrenci ve yönetici web panellerinden yürütülebilir.
---------------------------------------------------------------------------------------------
Yapilacak isler

Resmi tatil takviminin diger yillar icin genisletilmesi.

Belgeden okunan diger ogrenci alanlarinin da karsilastirmaya ve ortak onay akisina baglanmasi.

PDF/Word belgelerinden otomatik alan okuma ve manuel bilgilerle karsilastirma.

Final asamasi icin OCR/HTR ve bos alan tespiti entegrasyonu.

Google Form'a otomatik bilgi aktarimi ve Ek-17 olusturulmasi.

Diyagramlarin ve otomatik testlerin hazirlanmasi.

Bilinen sorunlar

Dogum tarihi, onceki staj gun sayisi ve saglik yardimi gibi diger ogrenci alanlarinin karsilastirmasi henuz tamamlanmadi.

Veri setindeki butun sutunlarin varlik kontrolu ve atlanan satirlardan sonraki mukerrer kontrolu tamamlanmalidir.

---------------------------------------------------------------------------------------------
Tarih kontrolleri
Ogretim yili 2026-2027 gibi ardisik iki yildan olusmali. yariyil bilgisi Guz veya Bahar olarak girilmeli.
Yariyil teslim edilen belgenin donem bilgisi. buyuk/kucuk harf ve bastaki sondaki bosluklar duzenleniyor.
Belgeden okunan yariyil form ile ayni olmali. okunamayan, gecersiz veya farkli yariyil uyusmazlik olarak donuyor. staj tarihinden yariyil tahmin edilmiyor.
Stajin baslangic ve bitisi secilen ogretim yili icinde olmali. bitis baslangictan once olamaz.
2026-2027 icin 1 Eylul 2026 - 31 Agustos 2027 araligi secildi. diger ardisik ogretim yillari da ayni yil araligi tercihiyle hazirlanabiliyor. resmi tatil verisi henuz 2026 ve 2027 ile sinirli. iki sinir gunu de dahil. bu proje icin onaylanan uygulama tercihi, PDF'nin verdigi kesin bir tarih siniri degil.
Ogretim yili ve tarih araligi kayit, guncelleme, belge yukleme, onay ve tamamlanmada denetleniyor.
Belge staj baslangicindan en az 30 takvim gunu once yuklenmeli. tam 30 gun kabul ediliyor. sure yetersizse son teslim tarihi hata mesajinda veriliyor.
30 gun hesabi dosyanin sistemdeki teslim zamaniyla yapiliyor. form kaydetme ve guncelleme islemlerine ek bir 30 gun sarti uygulanmiyor.
Teslim zamani UTC olarak kaydediliyor. takvim gunu hesabi icin Turkiye tarihine cevriliyor. ayni gun icindeki saat ve dakika farklari sayilmiyor.
Her yeni dosya yuklemesinde teslim tarihi yeniden kontrol ediliyor. reddedilen yukleme onceki belgeyi, durum gecmisini ve dogrulamayi degistirmiyor.
Onay ve tamamlanmada son belgenin kayitli teslim tarihi kullaniliyor. zamaninda yuklenen belge daha sonra da onaylanip tamamlanabiliyor.
Belgenin teslim zamani eksikse veya okunamiyorsa onay engelleniyor.
Eski kayitlar okunabiliyor. yariyil eksikse belge yukleme veya onaydan once tamamlanmali.
---------------------------------------------------------------------------------------------
Staj gunu hesabi
Baslangic ve bitis gunleri dahil, ogrencinin sectigi haftalik calisma gunleri sayiliyor.
Cumartesi ve pazar da dahil, yalnizca calisma gunu olarak secilen haftalik gunler sayiliyor.
2026 ve 2027 resmi tatilleri tarih araligina gore otomatik aliniyor. tam gun tatil 1, yarim gun tatil 0.5 olarak tutuluyor.
Cuma aksam biten tatilin ertesi cumartesisi de tatil sayiliyor. ayni tarihe iki bayram denk gelirse bir kez dusuluyor.
Tatil zaten calisilmayan bir gune denk gelirse tekrar dusulmuyor.
Belgedeki baslangic ve bitis tarihleri form ile karsilastiriliyor. iki tarih araligi icin net gun yeniden hesaplaniyor. toplam gun ayni olsa bile farkli tarihler uyusmazlik olarak donuyor.
Baslangic veya bitis okunamamissa dogrulama verilmiyor. belgede ayri gun sayisi varsa hesaplanan sonuc ile karsilastiriliyor. ayri haftalik gun listesi varsa formdaki secimle ayni olmali.
Belgede ayri gun sayisi veya haftalik gun listesi yoksa bunlar ek zorunlu alan sayilmiyor. belge tarihleri icin formda secilen haftalik gunler kullaniliyor.
Sonuc formdan ve belgeden hesaplanan gunleri, hangi alanin uyusmadigini birlikte tasiyor. on kontrol sonuc kaydetmiyor.
Son belge icin tum karsilastirmalar birlikte basariliysa dogrulama kaydediliyor. daha once saklanmis net gun yerine yeniden hesaplanan sonuc kullaniliyor. net gun kaydi da bu sonuc ile guncelleniyor.
Net gun hesabi basvuru olusturma ve guncelleme islemlerine baglandi. tarih veya calisma gunu degisince yeniden hesaplaniyor. yarim gunler de veritabaninda saklaniyor.
Tatil takvimi su an sadece 2026 ve 2027 icin hazir. baska yil girilirse uyari veriliyor.
Kaynaklar:
2026 resmi tatiller: https://vakithesaplama.diyanet.gov.tr/icerik.php?icerik=158
2027 resmi tatiller: https://vakithesaplama.diyanet.gov.tr/icerik.php?icerik=159
Tatil kurallari: https://vakithesaplama.diyanet.gov.tr/2429_kanun.php
---------------------------------------------------------------------------------------------
Kimlik kontrolleri
T.C. kimlik numarasi 11 rakamdan olusmali ve ilk rakami sifir olmamali.
10. ve 11. haneler hesaplanarak numaradaki degerlerle karsilastiriliyor.
Yeni kullanici kaydinda kontrol haneleri hataliysa hesap acilmiyor. yeni CSV/JSON aktariminda ise hatali satir raporlaniyor.
Yabanci ogrenci icin isForeign bilgisi true verilmeli. varsayilan deger false, yani T.C. kullanicisi.
Yabanci kimlikte proje kuralina gore 11 rakam ve 99 ile baslama kontrolu yapiliyor. T.C. kontrol hanesi hesabi yabanci kimlige uygulanmiyor.
IsForeign bilgisi veritabaninda saklaniyor ve giris yapildiginda geri yukleniyor.
Mevcut hesaplar korunuyor. checksum kurali yeni kayitlarda, yeni veri seti aktariminda ve belge karsilastirmasinda uygulanir.
Bu kontroller yalnizca bicim ve matematiksel kontrol yapiyor. numaranin gercek bir kisiye ait oldugu resmi bir servisten sorgulanmiyor.
Belgedeki kimlik numarasi kayitli ogrencinin IsForeign secimine gore kontrol ediliyor. veri setindeki T.C./yabanci uyruk secimiyle de uyumlu olmali.
Karsilastirma metotlari islem yapan kayitli kullaniciyi aliyor. ogrenci kendi basvurusuna, admin tum basvurulara erisebiliyor. toplu kontrol yalnizca admin icin.
CompareWithEk16 ve CompareAllApplicationsWithEk16 yalnizca basvuru-veri seti kontrolu yapiyor. basarili olmalarinin kendisi belge dogrulamasi vermiyor. uyusmazlik varsa eski dogrulama temizleniyor.
CompareFormWithScannedDocument okunmus belge alanlarini form ile on kontrol icin karsilastiriyor. sonuc kaydedilmiyor ve tek basina onaya izin vermiyor.
CompareScannedDocumentWithDataset(kullanici, basvuruId, belgeId, okunmusAlanlar) son yuklenen belgeyi, basvuru sahibini ve referans kaydi birlikte karsilastiriyor.
Belgedeki bilgilerle veri setinde baska bir ogrenci aranmiyor. referans kayit kayitli basvuru sahibine gore seciliyor.
Tum karsilastirmalar birlikte basariliysa basvuruya Ek16Verified kaydediliyor. belgeye StudentFieldsVerified, InternshipFieldsVerified, ContactFieldsVerified, ApplicationFieldsVerified ve bu kontrollerin zamanlari kaydediliyor. uygulama iki kaydi ayni islemde guncelliyor.
Onay ve tamamlanma icin son belgenin tum karsilastirma dogrulamalarini gecmesi ve zamanlarinin kayitli olmasi gerekiyor. eski bir belgenin sonucu yeni belge icin kullanilamiyor.
Uyusmazlik varsa eski dogrulama ve belge onayi temizleniyor. basvuru guncellemesi ve yeni belge yukleme de yeniden karsilastirma gerektiriyor.
Eski veritabanlari ilk acilista yeni sutunlari aliyor. onceki karsilastirmalar yeni yariyil ve isyeri kontrolu yerine sayilmiyor. yeniden karsilastirma gerekiyor. kullanici, basvuru ve belge kayitlari silinmiyor.
Bu asamada kontrol edilen belge alanlarinin okunmus degerleri metoda veriliyor. gercek belge okuyucunun bu degerleri ilgili dosyadan alip dogru belge numarasiyla gondermesi gerekiyor. otomatik okuma henuz hazir degil.
---------------------------------------------------------------------------------------------
Format kontrolleri
Ogrenci numarasinda mevcut harf ve rakam kurali korunuyor. bosluk ve ozel karakter kabul edilmiyor. sabit bir hane sayisi belirtilmedigi icin uzunluk zorunlulugu konulmadi.
E-posta kullanici@alanadi.uzanti biciminde olmali. telefon +90 xxx xxx xx xx biciminde yazilmali.
Hesap kaydinda e-posta zorunlu. telefon girilmisse bicimi kontrol ediliyor.
Staj basvurusunda adres, telefon ve e-posta zorunlu. faks bos kalabilir, girilmisse telefonla ayni bicimde olmali.
Adres sirasi mahalle, cadde/sokak, bina no, daire no, ilce/il olarak kontrol ediliyor. bu bir adres bicimi kontrolu, resmi adres sorgusu degil.
Adres ornegi: Merkez Mah. Cumhuriyet Cad. No:10 Daire:2 Izmit/Kocaeli
Bilesenler virgulle ayrilsa da ayni sira korunuyorsa kabul ediliyor.
Formdaki adres, telefon, e-posta ve faks belgeden okunan bilgilerle karsilastiriliyor. adres, telefon ve e-posta belgeden okunamazsa dogrulama verilmiyor. faks iki tarafta da bos kalabilir.
Adreste buyuk/kucuk harf, fazla bosluk ve ayirici virgul farklari eslesmeyi bozmuyor. e-postada buyuk/kucuk harf farki aranmiyor. farkli numara veya adres bilgisi uyusmazlik olarak donuyor.
Form ve belgede ayni hatali deger olsa bile bicim kontrolunu gecemiyor.
Veri setinde adres, telefon veya e-posta varsa form ile de karsilastiriliyor. eski veri setinde olmayan degerler uretilmiyor. tum veri seti sutunlarinin zorunlu varlik kontrolu halen yapilacak isler arasinda.
Tam karsilastirma basariliysa son belgeye ContactFieldsVerified ve ContactFieldsVerifiedAt kaydediliyor. basvuru degisince veya uyusmazlik cikinca eski sonuc temizleniyor. yeni dosya eski dosyanin sonucunu kullanamiyor.
Onay ve tamamlanmada kayitli formun adres, telefon, e-posta, faks ve isyeri adi bicim kontrolleri tekrar yapiliyor. eski dogrulama isaretleri eksik bilgiyi kabul ettirmiyor.
Kullanici kaydinda ve yeni veri seti aktariminda ayni Regex kurallari kullaniliyor. veri setindeki istege bagli iletisim alanlari girilmisse denetleniyor.
Eski basvurular okunabiliyor. guncelleme yapilacaksa yeni zorunlu alanlar da doldurulmali.
Belgede uzanti, boyut ve baslangic baytlari kontrol ediliyor. docx icin [Content_Types].xml ve word/document.xml girdilerinin dolu olmasi gerekiyor. herhangi bir ZIP dosyasinin uzantisini degistirmek yeterli degil.
---------------------------------------------------------------------------------------------
Muhendis ve belge onay kontrolleri
Isyerinde en az 1 muhendis olmali. bu sart karsilastirma, onay ve tamamlanmada kontrol ediliyor.
Belgedeki isyeri adi form ile karsilastiriliyor. buyuk/kucuk harf ve fazla bosluk farklari kabul ediliyor. bos veya farkli isyeri adi dogrulamayi engelliyor.
Muhendis sayisi belgeden ayrica okunmussa pozitif ve formdaki sayiyla ayni olmali. belgede ayri sayi alani yoksa ek zorunlu alan sayilmiyor. formdaki en az 1 muhendis sarti yine araniyor.
Bu kontroller son belgeye ApplicationFieldsVerified ve ApplicationFieldsVerifiedAt olarak kaydediliyor. yeni belge, form guncellemesi veya uyusmazlik yeniden karsilastirma gerektiriyor.
Muhendis sayisi 1 ile 4 arasindaysa mezun oldugu bolumu soran uyari bilgisi hazirlaniyor. bu uyari onayi engellemiyor.
EngineerGraduationDepartment alani bolum bilgisini saklayabiliyor. uyari kutusunun ekranda gosterilmesi henuz yapilmadi.
Ogrenci ve staj danismani onay alanlari bos olmali. isveren bolumu eksiksiz, kase ve imza dolu olmali.
Bu alanlarin inceleme sonucu DocumentApprovalFields ile tutuluyor. null henuz incelenmedi, false yok, true var demek. isveren bolumunde true eksiksiz dolduruldu demek.
Admin son yuklenen belge icin DocumentService.RecordApprovalFields metoduyla inceleme sonucunu kaydedebiliyor. inceleyen admin ve inceleme zamani da saklaniyor. eksik veya hatali sonuc kaydedilir ama onaya izin vermez.
Bu kisim henuz imza veya kaseyi gorselden otomatik bulmuyor. adminin inceleme sonucunu kurallara gore kontrol ediyor. ekran ve OCR baglantisi daha sonra yapilacak.
Onaylandi ve Tamamlandi durumlarina geciste muhendis bilgileri, veri seti dogrulamasi ve son belgenin incelemesi yeniden kontrol ediliyor. dosya bulunamiyorsa veya dosya kurallara uymuyorsa gecis engelleniyor.
Yeni belge yuklenince veya belge silinince onceki veri seti dogrulamasi sifirlaniyor. yeni belge eski belgenin inceleme sonucunu kullanamiyor.
Basvuru guncellenince veri seti dogrulamasi ve belge onayi sifirlaniyor. tamamlanmadan once yeniden dogrulama gerekiyor.
Eski belgeler otomatik incelenmis sayilmiyor. yeni inceleme alanlari baslangicta null tutuluyor.
Tamamlanan basvuruyu ogrenci degistiremez veya silemez. admin bu islemleri yapabilir.
---------------------------------------------------------------------------------------------
Eski veritabanlari mevcut kayitlar silinmeden kullanilabiliyor. onceki surumlerden kalan kullanilmayan sutunlar yeni islemlerde okunup yazilmiyor. yeni tablolarda yalnizca kullanilan alanlar olusturuluyor.

---------------------------------------------------------------------------------------------
Kartlarin mevcut durumu
ALG-1, ALG-2, VAL-1, VAL-2 ve VAL-3 icin algoritma ve servis kontrolleri hazir.
Bu durum ekran ve otomatik belge okuma baglantilarinin bittigi anlamina gelmiyor. hata gosterimi, muhendis bolumu uyari kutusu ve gercek dosyadan alan okuma henuz hazir degil.
Diger ogrenci alanlari ve veri seti sorunlari yukaridaki yapilacak isler arasinda kalmaya devam ediyor.
