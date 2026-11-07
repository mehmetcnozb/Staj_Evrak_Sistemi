# Staj Evrak Dogrulama ve Yonetim Sistemi

Bu uygulamanin amaci:
ogrencilerin staj basvurusu icin hazirladigi Ek-16 belgesinin kontrolunu ve takibini kolaylastirmaktir. sistemdeki bilgilerle ogrencinin bilgileri karsilastirilir. hatali olanlar ogrenciye bildirilir. ogrenci hatasini duzeltir. yoneticiler de basvurulari tek yerden kontrol eder.
---------------------------------------------------------------------------------------------
Mevcut ozellikler:
*kullanici kaydi ve girisi - ogrenciler kendi sifrelerini belirleyerek hesap olusturur. sifreler hashlenerek saklanir
*kimlik kontrolu           - yeni kayitlarda T.C. kimlik numarasinin 10. ve 11. haneleri kontrol ediliyor. yabanci ogrencilerde 11 hane ve 99 ile baslama kurali kullaniliyor. yeni veri seti aktarimlarinda da bu kontroller yapiliyor.
*format kontrolleri        - ogrenci numarasi, e-posta ve telefon Regex ile kontrol ediliyor. basvuruda adres, telefon ve e-posta zorunlu. faks, kurum IBAN ve vergi numarasi girilmisse kontrol edilip kaydediliyor.
*hesap kitleme             - 5 kez hatali girilince hesap kitleniyor. 15dk sonra tekrar aciliyor. basarisiz girisler kaydediliyor.
*yetkilendirme             - ogrenci ve admin rolleri bulunuyor. ogrenci sadece kendi belge ve basvurusuna erisebiliyor.
*oturum yonetimi           - birden fazla kullanici icin oturum olusturma, sure kontrolu, cikis islrmleri
*veri seti aktarimi        - admin, CSV ve JSON dosyalarindan ogrenci bilgilerini iceri aktarabiliyor. hatali satirlar raporlaniyor.Tekrarlanan kayitlarin yeniden eklenmesini onler.
*basvuru yonetimi          - basvuru olusturma, goruntuleme, guncelleme, silme, islemleri bulunur. admin basvurulari duruma gore listeleyebilir.
*ek-16 yukleme             - Word, PDF, JPEG ve PNG dosyalari kontrol edilip kaydedilebiliyor.
*bilgi karsilastirma       - onceden okunup servise verilen ogrenci numarasi, kimlik numarasi, ad soyad, bolum ve ogretim yili karsilastirilabiliyor.
*muhendis ve onay kontrolu - muhendis sayisi ve sorumlu muhendis bilgileri kontrol ediliyor. adminin son belge icin kaydettigi onay, imza ve kase incelemesi onay akisinda denetleniyor.
*durum takibi              - degisiklik gecmisi tutuyor. tamamlanan basvurularda ogrencinin degistirmesi engelleniyor. admin degistiriyor.
*tarih kontrolleri         - ogretim yili bicimi, takvimle yil eslesmesi, 30 gun kurali ve ders donemi cakismasi kontrol edilebiliyor. bu kontroller henuz basvuru akisina baglanmadi.
*staj gunu hesabi          - secilen calisma gunleri ve 2026-2027 resmi tatil takvimine gore net staj gunu hesaplaniyor. basvuru olusturulurken ve guncellenirken sonuc kaydediliyor. minimum gun sayisi ayarlanmissa kontrol ediliyor. Ek-16 da belirtilen gun sayisiyla eslesme metodu da bulunuyor.
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
            PassportNumberValidator.cs (Turkiye umuma mahsus pasaport numarasinin U harfi ve sekiz rakamdan olusan bicimini kontrol eder.)
            FormatValidator.cs (ogrenci numarasi, e-posta, telefon, adres, faks, kurum IBAN ve vergi numarasini kontrol eder. basvurudaki format hatalarini liste olarak dondurur.)
            EngineerApprovalValidator.cs (muhendis sayisini, sorumlu muhendis bilgilerini ve belgedeki onay alanlarinin kurallara uygunlugunu kontrol eder.)
            Permission.cs (Sistemde yapilabilecek yetkili islemleri-fonksiyonlari tanimlar.)
            RolePermissions.cs (Permission.cs deki islemleri, hangi rollerin yapabilecegini kisitlar)
            UserRole.cs (Aktif olan kullanici rollerini tanimlar.)
            InternshipDateValidator.cs (ogretim yilinin dogru yazilip yazilmadigini ve takvimle ayni yilda olup olmadigini kontrol eder. 30 gun kuralini, son teslim tarihini ve ders donemi cakismasini da kontrol eder.)
            InternshipDayCalculator.cs (secilen calisma gunleri ve verilen tatil listesiyle net staj gununu hesaplar. minimum gun sartini ve Ek-16 da belirtilen gun sayisiyla eslesmeyi kontrol eder.)

        
        Data/
            DatabaseManager.cs (uygulamanin SQLite veritabanina baglanmasini ve gerekli tablolarin hazirlanmasini saglar.)
            AcademicCalendars.cs (kocaeli universitesinin 2026-2027 guz ve bahar ders baslangic ve bitis tarihlerini saklar.)
            TurkishPublicHolidays.cs (2026 ve 2027 resmi tatillerini saklar. verilen tarih araligindaki tam ve yarim gun tatilleri dondurur. ayni gune gelen bayramlari tek sayar.)
        
        Models/
            User.cs (butun kullanicilarin ortak temel sinifi. kimlik, yabanci kullanici bilgisi, ad soyad, e-posta, telefon vb... kimlik kontrolunu IsForeign bilgisine gore secer. e-posta ve girilmisse telefon bicimini kontrol eder.)
            Student.cs (ogrenciyi temsil eder. user dan turetilir. ogrenci yetkilerini tanimlar. yabanci ogrenci icin isForeign bilgisi verilebilir.)
            Admin.cs (admini temsil eder. user dan turetilmistir. admin yetkilerini tanimlar)
            BaseDocument.cs (Belgelerin ortak temel sinifidir. dosya yolu, yuklenme zamani, onay/ret bilgileri ve ortak dosya kontrollerini icerir. docx dosyasinda Word paket girdileri de kontrol edilir.)
            Ek16Document.cs (Ek-16 belgesini temsil eder. uzanti, boyut ve dosya icerigini kontrol eder. onay alanlarinin inceleme sonucunu, inceleyen admini ve zamani da tasir.)
            DocumentApprovalFields.cs (ogrenci ve danisman onay alanlariyla isveren bolumu, kase ve imza inceleme sonuclarini tasir. null henuz incelenmedi demek.)
            InternshipApplication.cs (ogrencinin staj basvurusunu temsil eder. tarihler, isyeri, kurum IBAN ve vergi numarasi, bolum, iletisim bilgileri, calisma gunleri, hesaplanan net staj gunu, muhendis sayisi, sorumlu muhendisin adi, unvani, oda sicil numarasi, mezun oldugu bolum ve basvuru durumunu icerir.)
            OfficialStudentRecord.cs (Adminin yukledigi referans veri setindeki bir ogrenci kaydini temsil eder. ogrenci, kimlik, uyruk, ogrenim ve iletisim bilgilerini iserir.InternshipApplication.cs ile karsilastirilirken kullanilir. )
            Ek16ComparisonResult.cs (Veri setiyle yapilan karsilastirmanin sonucunu tasir. Eslesen kayit ve alanlar arasindaki uyusmazliklari icerir.)
            ScannedDocumentComparisonResult.cs (ogrencinin girdigi bilgilerle belgeden gelen alanlarin karsilastirma sonucunu tasir. Ayni dosyada, okunmus belge alanlarini tasiyan ScannedDocumentFields modeli de bulunur.)
            StatusTransitionLog.cs (Basvurunun durum degisikligini temsil eder. onceki/yeni durum, islemi yapan kullanici, tarih ve gerekceyi icerir.)
            FailedLoginLog.cs (Basarisiz giris denemesini temsil eder. Kullanici bilgisi, kullanilan giris bilgisi, basarisizlik nedeni ve zamani icerir.)
            AcademicCalendar.cs (ogretim yilini ve guz/bahar ders tarihlerini tasir. takvim olusturulurken ogretim yili bicimini ve tarih araliklarini kontrol eder.)
        
        Services/
            AuthService.cs	(Kullanici kaydi, giris, hesap kilitleme ve basarisiz giris kayitlarini yonetir. kayitta kimlik kontrolu yapar. yabanci kullanici bilgisini veritabanina kaydeder ve giriste geri yukler.)
            AuthorizationService.cs	(Oturum, rol ve basvuru sahipligine gore kullanicinin islem yapma yetkisini kontrol eder.)
            AuthorizationResult.cs	(Yetki kontrolunun sonucunu tasir: izin verildi, giris gerekli veya yetkisiz erisim.)
            AuthorizationExceptions.cs	(Giris yapilmamasi ve yetkisiz erisim durumlarinda kullanilacak hata siniflarini tanimlar.)
            DatasetImportService.cs	(Adminin CSV/JSON veri setini okur, kimlik ve ogrenci numarasini kontrol eder. girilmis e-posta, telefon ve adresin bicimini de kontrol eder. hatali satirlar raporlanir, gecerli kayitlar veritabanina aktarilir.)
            DatasetImportResult.cs	(Veri seti aktariminin sonuclarini tasir: eklenen, tekrarlanan ve hatali kayitlar gibi.)
            DocumentService.cs	(Ek-16 yukleme, listeleme ve silme islemlerini yapar. adminin son belge icin onay alani incelemesini kaydeder. belge yuklenince veya silinince eski karsilastirma sonucu sifirlanir.)
            DocumentValidationException.cs	(Dosya kabul edilmediginde, ornegin formati veya boyutu uygun degilse, hata nedenlerini tasir.)
            Ek16MatcherService.cs	(ogrenci bilgilerini, belgeden gelen alanlari ve adminin veri setini karsilastirip uyusmazliklari belirler.)
            InternshipService.cs	(Staj basvurusu olusturma, goruntuleme, guncelleme, silme ve listeleme islemlerini yapar. format hataliysa kaydi engeller. kurum IBAN, vergi numarasi ve sorumlu muhendis bilgilerini kaydeder. guncellemede eski karsilastirma sonucu ve belge onayi sifirlanir. net staj gununu hesaplar ve minimum gun sayisi verilmisse kontrol eder.)
            LoginResult.cs	(Giris isleminin sonucunu tasir: basarili giris, yanlis bilgiler veya kilitli hesap.)
            PasswordHasher.cs	(sifreleri rastgele salt kullanarak hash ler; giriste verilen sifreyi kayitli hash ile dogrular.)
            SessionManager.cs	(Kullanici oturumlarini olusturur, token ve sure kontrolu yapar, cikista oturumu kapatir.)
            UserSession.cs	(Bir oturumun kullanici numarasi, rolu, token i, baslangic ve bitis zamanini tasir.)
            StatusWorkflowEngine.cs	(Basvuru durum gecislerini, izinleri ve degisiklik gecmisini yonetir. onaylanirken ve tamamlanirken veri seti dogrulamasi, muhendis bilgileri, son belge ve onay alanlari yeniden kontrol edilir.)
            UserGuard.cs	(islem yapan kullanicinin veritabaninda bulundugunu ve rolunun kayitli rolle eslestigini kontrol eder.)
            
        
        Program.cs(	Uygulamanin baslangic noktasi)
        
        StajEvrakSistemi.csproj (.NET surumu ve kullanilan paketleri tanimlar.)
---------------------------------------------------------------------------------------------
Gelistirme durumu

Temel veritabani, kullanici islemleri, yetkilendirme, basvuru yonetimi, veri seti aktarimi ve belge yukleme servisleri gelistirilmistir. Ogretim yili bicimi, takvimle yil eslesmesi, 30 gun kurali ve ders donemi cakisma kontrolleri de eklendi. tarih kontrolleri henuz basvuru akisina baglanmadi. Net staj gunu hesabi, minimum gun ve belgedeki gun sayisiyla eslesme kontrolleri de eklendi. 2026 ve 2027 resmi tatil takvimi hazir. basvuru olusturulurken ve guncellenirken net gun yeniden hesaplaniyor ve kaydediliyor. minimum gun sayisi ayarlanmissa kayit sirasinda kontrol ediliyor. Yeni kullanici kaydi ve veri seti aktariminda T.C. kontrol haneleri ve yabanci kimlik kurallari denetleniyor. Turkiye umuma mahsus pasaport icin ayri bicim kontrolu eklendi. Format kontrolleri kullanici kaydina, veri seti aktarimina ve basvuru kaydina baglandi. kurum IBAN ve vergi numarasi alanlari da eklendi. Muhendis ve belge onay kontrolleri onay akisina baglandi. admin incelemesi son belgeye kaydediliyor, inceleyen kisi ve zaman tutuluyor. Web arayuzuyle entegrasyon henuz yapilmamistir.
---------------------------------------------------------------------------------------------
Yapilacak isler

Yariyil bilgisinin ve staj tarihlerinin secilen akademik takvimle uyumunun kontrol edilmesi.

Hazirlanan tarih kontrollerinin basvuru akisina baglanmasi.

Resmi tatil takviminin diger yillar icin genisletilmesi.

Minimum staj gunu sartinin netlestirilmesi ve Ek-16 dan okunan gun sayisinin karsilastirmaya baglanmasi.

Kimlik kontrollerinin belgeden okunan alanlara ve zorunlu onay akisina baglanmasi.

Pasaport bilgisinin gerekli form alanlarina baglanmasi. diger pasaport turleri ve ulkeler icin kurallarin netlestirilmesi.

Format hatalarinin ogrenci ve admin ekranlarinda alan bazinda gosterilmesi.

Muhendis ve onay alani kontrollerinin web arayuzune ve belgeden okunan alanlara baglanmasi.

Ogrenci ve admin web arayuzlerinin hazirlanmasi.

PDF/Word belgelerinden otomatik bilgi okuma ve OCR entegrasyonu.

Google Form'a otomatik bilgi aktarimi ve Ek-17 olusturulmasi.

Raporlama ekranlarinin, diyagramlarin ve otomatik testlerin hazirlanmasi.

Bilinen sorunlar

Belgeden okunan alanlarin ogrencinin girdigi bilgilerle karsilastirilmasi henuz zorunlu onay akisina baglanmamistir. veri setiyle dogrulama tek basina butun alanlarin dogru oldugunu gostermiyor.

Belgedeki ogrencinin basvurunun sahibiyle eslesmesi her karsilastirma yolunda zorunlu tutulmamaktadir.

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
Staj gunu hesabi
Baslangic ve bitis gunleri dahil, ogrencinin sectigi haftalik calisma gunleri sayiliyor.
Pazar gunleri sayilmiyor. cumartesi sadece calisma gunu olarak secilmisse sayiliyor.
2026 ve 2027 resmi tatilleri tarih araligina gore otomatik aliniyor. tam gun tatil 1, yarim gun tatil 0.5 olarak tutuluyor.
Cuma aksam biten tatilin ertesi cumartesisi de tatil sayiliyor. ayni tarihe iki bayram denk gelirse bir kez dusuluyor.
Tatil zaten calisilmayan bir gune denk gelirse tekrar dusulmuyor.
Servis olusturulurken minimum gun sayisi verilmisse, bu sinirin altindaki basvuru kaydedilmiyor. sabit bir minimum belirlenmedi.
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
Mevcut hesaplar korunuyor. checksum kurali yeni kayitlarda ve yeni veri seti aktarimlarinda uygulanir.
Pasaport icin Turkiye umuma mahsus numara bicimi kontrol edilebiliyor: U harfi ve ardindan 8 rakam. ornek: U12345678.
Pasaport kontrolu ayri bir metot. hesap kaydinda T.C. veya yabanci kimlik numarasinin yerine gecmiyor. diger pasaport turleri ve ulkeler bu metodun kapsaminda degil.
Bu kontroller yalnizca bicim ve matematiksel kontrol yapiyor. numaranin gercek bir kisiye ait oldugu resmi bir servisten sorgulanmiyor.
Pasaport bicimi icin ornek kaynak: https://smarteritaly.agid.gov.it/wp-content/uploads/2024/04/IQ-Structures_presentazione.pdf
---------------------------------------------------------------------------------------------
Format kontrolleri
Ogrenci numarasinda mevcut harf ve rakam kurali korunuyor. bosluk ve ozel karakter kabul edilmiyor. sabit bir hane sayisi belirtilmedigi icin uzunluk zorunlulugu konulmadi.
E-posta kullanici@alanadi.uzanti biciminde olmali. telefon +90 xxx xxx xx xx biciminde yazilmali.
Hesap kaydinda e-posta zorunlu. telefon girilmisse bicimi kontrol ediliyor.
Staj basvurusunda adres, telefon ve e-posta zorunlu. faks bos kalabilir, girilmisse telefonla ayni bicimde olmali.
Adres sirasi mahalle, cadde/sokak, bina no, daire no, ilce/il olarak kontrol ediliyor. bu bir adres bicimi kontrolu, resmi adres sorgusu degil.
Adres ornegi: Merkez Mah. Cumhuriyet Cad. No:10 Daire:2 Izmit/Kocaeli
Kurum IBAN ve vergi numarasi bos kalabilir. girilmisse kontrol edilip veritabanina kaydediliyor.
Turkiye IBAN bilgisi TR ile baslayan 26 karakterlik yapiya uygun olmali. rezerv alani sifir olmali ve MOD-97 kontrolunden gecmeli. bosluklu girilen IBAN kaydedilirken birlestirilip buyuk harfe cevriliyor.
Vergi numarasinda 10 rakamli VKN bicimi kabul ediliyor. sahis firmasi icin 11 haneli T.C. numarasi verilirse kontrol haneleri de denetleniyor. 10 haneli VKN icin yalnizca bicim kontrolu yapiliyor.
Kullanici kaydinda ve yeni veri seti aktariminda ayni Regex kurallari kullaniliyor. veri setindeki istege bagli iletisim alanlari girilmisse denetleniyor.
Eski basvurular okunabiliyor. guncelleme yapilacaksa yeni zorunlu alanlar da doldurulmali.
Belgede uzanti, boyut ve baslangic baytlari kontrol ediliyor. docx icin [Content_Types].xml ve word/document.xml girdilerinin dolu olmasi gerekiyor. herhangi bir ZIP dosyasinin uzantisini degistirmek yeterli degil.
IBAN bicimi ve MOD-97 kontrolu bir banka hesabinin gercekte var oldugunu veya kime ait oldugunu sorgulamiyor.
IBAN kaynagi: https://www.tcmb.gov.tr/wps/wcm/connect/EN/TCMB%2BEN/Bottom%2BMenu/IBAN/Communique
---------------------------------------------------------------------------------------------
Muhendis ve belge onay kontrolleri
Isyerinde en az 1 muhendis olmali. sorumlu muhendisin adi soyadi, unvani ve oda sicil numarasi girilmeli.
Unvanin muhendis veya muhendisi ifadesiyle bitmesi kontrol ediliyor. oda sicil numarasinda bos olmama kontrolu var, resmi bir sicil sorgusu yapilmiyor.
Muhendis sayisi 1 ile 4 arasindaysa sorumlu muhendisin mezun oldugu bolum de zorunlu. 5 ve uzerindeyse bu ek alan zorunlu degil.
Eksik muhendis bilgisiyle taslak basvuru kaydedilebiliyor. onay asamasinda eksikler tamamlanmissa ilerleniyor.
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
