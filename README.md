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
*bilgi karsilastirma       - son belgenin ogrenci no, kimlik no, ad soyad, bolum ve ogretim yili bilgileri basvuru sahibi ve veri setiyle birlikte kontrol ediliyor. sonuc belgeye kaydediliyor. tek basina veri seti eslesmesi onay vermiyor.
*muhendis ve onay kontrolu - en az 1 muhendis olmasi kontrol ediliyor. sayi 5'in altindaysa mezuniyet bolumu icin uyari bilgisi hazirlaniyor. adminin son belge icin kaydettigi onay, imza ve kase incelemesi denetleniyor.
*durum takibi              - degisiklik gecmisi tutuyor. tamamlanan basvurularda ogrencinin degistirmesi engelleniyor. admin degistiriyor.
*tarih kontrolleri         - ogretim yili, yariyil ve staj tarih araligi kayit ve guncellemede kontrol ediliyor. belge tesliminde en az 30 takvim gunu araniyor. onay ve tamamlanmada belge teslim tarihi tekrar denetleniyor.
*staj gunu hesabi          - secilen haftalik gunler ve 2026-2027 resmi tatillerine gore net staj gunu hesaplaniyor. kayit ve guncellemede sonuc saklaniyor. Ek-16 da belirtilen gun sayisiyla eslesme metodu da bulunuyor.
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
            IdentityNumberValidator.cs (T.C. kimlik numarasinin bicimini ve 10./11. kontrol hanelerini denetler. yabanci kimlik numarasinda 11 hane ve 99 onekini kontrol eder.)
            FormatValidator.cs (ogrenci numarasi, e-posta, telefon, adres ve faksi kontrol eder. basvurudaki format hatalarini liste olarak dondurur.)
            EngineerApprovalValidator.cs (muhendis sayisini ve belge onay alanlarini kontrol eder. muhendis sayisi 5'in altindaysa bolum bilgisi icin uyari dondurur.)
            Permission.cs (Sistemde yapilabilecek yetkili islemleri-fonksiyonlari tanimlar.)
            RolePermissions.cs (Permission.cs deki islemleri, hangi rollerin yapabilecegini kisitlar)
            UserRole.cs (Aktif olan kullanici rollerini tanimlar.)
            InternshipDateValidator.cs (ogretim yili, yariyil ve staj tarih araligini kontrol eder. belgenin teslim tarihi icin 30 gun kuralini ve son teslim tarihini hesaplar. UTC zamanini Turkiye tarihine cevirir.)
            InternshipDayCalculator.cs (secilen haftalik gunler ve tatil listesiyle net staj gununu hesaplar. Ek-16 da belirtilen gun sayisiyla eslesmeyi kontrol eder.)

        
        Data/
            DatabaseManager.cs (uygulamanin SQLite veritabanina baglanmasini ve gerekli tablolarin hazirlanmasini saglar.)
            AcademicCalendars.cs (secilen ogretim yilinin tarih sinirlarini hazirlar. 2026-2027 icin onaylanan yil araligini saklar.)
            TurkishPublicHolidays.cs (2026 ve 2027 resmi tatillerini saklar. verilen tarih araligindaki tam ve yarim gun tatilleri dondurur. ayni gune gelen bayramlari tek sayar.)
        
        Models/
            User.cs (butun kullanicilarin ortak temel sinifi. kimlik, yabanci kullanici bilgisi, ad soyad, e-posta, telefon vb... kimlik kontrolunu IsForeign bilgisine gore secer. e-posta ve girilmisse telefon bicimini kontrol eder.)
            Student.cs (ogrenciyi temsil eder. user dan turetilir. ogrenci yetkilerini tanimlar. yabanci ogrenci icin isForeign bilgisi verilebilir.)
            Admin.cs (admini temsil eder. user dan turetilmistir. admin yetkilerini tanimlar)
            BaseDocument.cs (Belgelerin ortak temel sinifidir. dosya yolu, yuklenme zamani, onay/ret bilgileri ve ortak dosya kontrollerini icerir. docx dosyasinda Word paket girdileri de kontrol edilir.)
            Ek16Document.cs (Ek-16 belgesini temsil eder. dosya ve onay incelemesi bilgilerini tasir. ogrenci alanlarinin bu belge icin dogrulanip dogrulanmadigini ve karsilastirma zamanini da saklar.)
            DocumentApprovalFields.cs (ogrenci ve danisman onay alanlariyla isveren bolumu, kase ve imza inceleme sonuclarini tasir. null henuz incelenmedi demek.)
            InternshipApplication.cs (ogrencinin staj basvurusunu temsil eder. tarihler, yariyil, isyeri, bolum, iletisim bilgileri, calisma gunleri, net staj gunu, muhendis sayisi, mezuniyet bolumu ve basvuru durumunu tasir.)
            OfficialStudentRecord.cs (Adminin yukledigi referans veri setindeki bir ogrenci kaydini temsil eder. ogrenci, kimlik, uyruk, ogrenim ve iletisim bilgilerini iserir.InternshipApplication.cs ile karsilastirilirken kullanilir. )
            Ek16ComparisonResult.cs (veri seti karsilastirmasinin sonucunu tasir. belge dogrulamasinda ilgili belge numarasini da icerir. kimlik, uyruk ve alan uyusmazliklarini listeler.)
            ScannedDocumentComparisonResult.cs (belgedeki bes ogrenci alaninin form ile karsilastirma sonucunu tasir. IsMatched yalniz bu alanlar icindir, belgenin tum kurallari gectigini soylemez. ScannedDocumentFields modeli de bu dosyadadir.)
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
            Ek16MatcherService.cs	(kayitli kullanicinin yetkisini ve basvuru sahipligini kontrol eder. referansi basvuru sahibine gore bulur. son belgenin alanlari form ve veri setiyle uyusursa belgeye bagli dogrulama kaydeder.)
            InternshipService.cs	(Basvuru olusturma, guncelleme, silme ve listeleme islemlerini yapar. format, ogretim yili ve tarih araligini kontrol eder. net staj gununu hesaplar ve kaydeder. guncellemede eski karsilastirma sonucu, belgeye ait ogrenci dogrulamasi ve belge onayi sifirlanir.)
            LoginResult.cs	(Giris isleminin sonucunu tasir: basarili giris, yanlis bilgiler veya kilitli hesap.)
            PasswordHasher.cs	(sifreleri rastgele salt kullanarak hash ler; giriste verilen sifreyi kayitli hash ile dogrular.)
            SessionManager.cs	(Kullanici oturumlarini olusturur, token ve sure kontrolu yapar, cikista oturumu kapatir.)
            UserSession.cs	(Bir oturumun kullanici numarasi, rolu, token i, baslangic ve bitis zamanini tasir.)
            StatusWorkflowEngine.cs	(Basvuru durum gecislerini, izinleri ve degisiklik gecmisini yonetir. onaylanirken ve tamamlanirken veri seti dogrulamasi, muhendis bilgileri, son belge, belgeye kaydedilen ogrenci dogrulamasi, onay alanlari ve kayitli tarihler yeniden kontrol edilir.)
            UserGuard.cs	(islem yapan kullanicinin veritabaninda bulundugunu ve rolunun kayitli rolle eslestigini kontrol eder.)
            
        
        Program.cs(	Uygulamanin baslangic noktasi)
        
        StajEvrakSistemi.csproj (.NET surumu ve kullanilan paketleri tanimlar.)
---------------------------------------------------------------------------------------------
Gelistirme durumu

Temel veritabani, kullanici islemleri, yetkilendirme, basvuru yonetimi, veri seti aktarimi ve belge yukleme servisleri gelistirildi. Ogretim yili, yariyil ve staj tarih araligi kayit ve guncellemede denetleniyor. belge teslimi icin en az 30 takvim gunu araniyor. onay ve tamamlanmada son belgenin teslim tarihi kontrol ediliyor.
Net staj gunu secilen haftalik gunler ve resmi tatillerle hesaplaniyor. kimlik ve format kontrolleri de eklendi. muhendis sayisi 5'in altindaysa bolum bilgisi icin uyari hazirlaniyor. belge onay alanlari admin incelemesiyle kaydediliyor. Son belgedeki bes ogrenci alani basvuru sahibi ve veri setiyle birlikte kontrol ediliyor. karsilastirma belgenin numarasina ve zamana bagli kaydediliyor. Web arayuzu ve otomatik belge okuma henuz hazir degil.
---------------------------------------------------------------------------------------------
Yapilacak isler

Resmi tatil takviminin diger yillar icin genisletilmesi.

Ek-16 dan okunan staj tarihleri ve gun bilgilerinin hesaplanan degerlerle karsilastirmaya baglanmasi.

Belgeden okunan diger ogrenci ve iletisim alanlarinin da karsilastirmaya ve ortak onay akisina baglanmasi.

Format hatalarinin ogrenci ve admin ekranlarinda alan bazinda gosterilmesi.

Muhendis sayisi 5'in altindayken bolum bilgisini soran uyari kutusunun ve onay kontrollerinin ekranlara baglanmasi.

Ogrenci ve admin web arayuzlerinin hazirlanmasi.

PDF/Word belgelerinden otomatik alan okuma ve manuel bilgilerle karsilastirma.

Final asamasi icin OCR/HTR ve bos alan tespiti entegrasyonu.

Google Form'a otomatik bilgi aktarimi ve Ek-17 olusturulmasi.

Raporlama ekranlarinin, diyagramlarin ve otomatik testlerin hazirlanmasi.

Bilinen sorunlar

Staj tarihleri, gun bilgileri ve bes alan disindaki ogrenci bilgilerinin belge-form-veri seti karsilastirmasi henuz tamamlanmadi.

Eski basvurularin onay akisinda adres, telefon ve e-posta dahil tum zorunlu alanlarin yeniden kontrolu tamamlanmali.

Veri setindeki butun sutunlarin varlik kontrolu ve atlanan satirlardan sonraki mukerrer kontrolu tamamlanmalidir.

---------------------------------------------------------------------------------------------
Tarih kontrolleri
Ogretim yili 2026-2027 gibi ardisik iki yildan olusmali. yariyil bilgisi Guz veya Bahar olarak girilmeli.
Yariyil teslim edilen belgenin donem bilgisi. buyuk/kucuk harf ve bastaki sondaki bosluklar duzenleniyor.
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
Tarih hatalarinin web ekranlarinda gosterilmesi henuz yapilmadi.
---------------------------------------------------------------------------------------------
Staj gunu hesabi
Baslangic ve bitis gunleri dahil, ogrencinin sectigi haftalik calisma gunleri sayiliyor.
Cumartesi ve pazar da dahil, yalnizca calisma gunu olarak secilen haftalik gunler sayiliyor.
2026 ve 2027 resmi tatilleri tarih araligina gore otomatik aliniyor. tam gun tatil 1, yarim gun tatil 0.5 olarak tutuluyor.
Cuma aksam biten tatilin ertesi cumartesisi de tatil sayiliyor. ayni tarihe iki bayram denk gelirse bir kez dusuluyor.
Tatil zaten calisilmayan bir gune denk gelirse tekrar dusulmuyor.
Hesaplanan sonuc ile Ek-16 da belirtilen gun sayisi karsilastirilabiliyor. gun sayisini su an metoda vermek gerekiyor, belgeden otomatik okunmuyor.
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
CompareFormWithScannedDocument bes ogrenci alanini on kontrol icin karsilastiriyor. sonuc kaydedilmiyor ve tek basina onaya izin vermiyor.
CompareScannedDocumentWithDataset(kullanici, basvuruId, belgeId, okunmusAlanlar) son yuklenen belgeyi, basvuru sahibini ve referans kaydi birlikte karsilastiriyor.
Belgedeki bilgilerle veri setinde baska bir ogrenci aranmiyor. referans kayit kayitli basvuru sahibine gore seciliyor.
Sonuc basariliysa basvuruya Ek16Verified, belgeye StudentFieldsVerified ve StudentFieldsVerifiedAt kaydediliyor. uygulama iki kaydi ayni islemde guncelliyor.
Onay ve tamamlanma icin son belgenin bu dogrulamayi gecmesi de gerekiyor. eski bir belgenin sonucu yeni belge icin kullanilamiyor.
Uyusmazlik varsa eski dogrulama ve belge onayi temizleniyor. basvuru guncellemesi ve yeni belge yukleme de yeniden karsilastirma gerektiriyor.
Eski veritabanlari ilk acilista yeni sutunlari aliyor. onceki veri seti eslesmeleri yeni belge kontrolu yerine sayilmiyor ve yeniden karsilastirma gerekiyor. kullanici, basvuru ve belge kayitlari silinmiyor.
Bu asamada bes alanin okunmus degerleri metoda veriliyor. gercek belge okuyucunun bu degerleri ilgili dosyadan alip dogru belge numarasiyla gondermesi gerekiyor. otomatik okuma henuz hazir degil.
---------------------------------------------------------------------------------------------
Format kontrolleri
Ogrenci numarasinda mevcut harf ve rakam kurali korunuyor. bosluk ve ozel karakter kabul edilmiyor. sabit bir hane sayisi belirtilmedigi icin uzunluk zorunlulugu konulmadi.
E-posta kullanici@alanadi.uzanti biciminde olmali. telefon +90 xxx xxx xx xx biciminde yazilmali.
Hesap kaydinda e-posta zorunlu. telefon girilmisse bicimi kontrol ediliyor.
Staj basvurusunda adres, telefon ve e-posta zorunlu. faks bos kalabilir, girilmisse telefonla ayni bicimde olmali.
Adres sirasi mahalle, cadde/sokak, bina no, daire no, ilce/il olarak kontrol ediliyor. bu bir adres bicimi kontrolu, resmi adres sorgusu degil.
Adres ornegi: Merkez Mah. Cumhuriyet Cad. No:10 Daire:2 Izmit/Kocaeli
Bilesenler virgulle ayrilsa da ayni sira korunuyorsa kabul ediliyor.
Kullanici kaydinda ve yeni veri seti aktariminda ayni Regex kurallari kullaniliyor. veri setindeki istege bagli iletisim alanlari girilmisse denetleniyor.
Eski basvurular okunabiliyor. guncelleme yapilacaksa yeni zorunlu alanlar da doldurulmali.
Belgede uzanti, boyut ve baslangic baytlari kontrol ediliyor. docx icin [Content_Types].xml ve word/document.xml girdilerinin dolu olmasi gerekiyor. herhangi bir ZIP dosyasinin uzantisini degistirmek yeterli degil.
---------------------------------------------------------------------------------------------
Muhendis ve belge onay kontrolleri
Isyerinde en az 1 muhendis olmali. bu sart onay ve tamamlanmada kontrol ediliyor.
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
