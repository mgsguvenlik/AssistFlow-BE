# Şifre politikası doğrulaması

Bu araç WebAPI'yi başlatmaz; SQLite bellekte çalışır. Gerçek DB, SMTP veya gerçek kullanıcı hesabı kullanılmaz.

AssistFlow-BE dizininde:

```powershell
dotnet restore tools/PasswordPolicy.Tests/PasswordPolicy.Tests.csproj
dotnet build tools/PasswordPolicy.Tests/PasswordPolicy.Tests.csproj --no-restore -m:1 -p:OutputPath=bin/password-policy-build/
dotnet tools/PasswordPolicy.Tests/bin/password-policy-build/PasswordPolicy.Tests.dll
```

Kapsam: eski kullanıcıların başlangıç tarihi, tekrar çalıştırılabilir seed, yeni kullanıcının ilk girişi, admin talebi, açık oturumların engellenmesi, adminin doğrudan şifre belirlemesi, geçerlilik süresi sınırı, parametre doğrulaması, imzalı/süreli/tek kullanımlık değişiklik tokenları ve eski oturumların geçersiz kalması. SQL Server migration betiği ve model snapshot uyumu bağlantı açılmadan doğrulanır.

## Yayına alma

- `20261007200000_AddPasswordPolicy` migration'ı `Users` tablosuna `MustChangePassword` (false), `PasswordChangedAt` (nullable datetimeoffset) ve `PasswordVersion` (0) ekler.
- Mevcut uygulama başlangıç akışı önce migration'ları, ardından seed'leri çalıştırır. `PasswordPolicySeed` yalnızca boş şifre tarihlerini çalıştırıldığı anın UTC tarihiyle doldurur. Mevcut kullanıcılar başlangıçta değişime zorlanmaz.
- `PasswordValidityDays` parametresi yoksa 180 olarak oluşturulur. Birim gün, izin verilen aralık 1–36500. Tekrar çalıştırma mevcut değerleri, tarihleri veya bekleyen talepleri değiştirmez.
- FE ve BE birlikte yayımlanmalıdır. Backend zorunlu değişiklik sırasında normal API erişimini HTTP 428 ile engeller; frontend yalnızca şifre değiştirme ekranını açar. SignalR bildirimi açık oturumları yönlendirir; bağlantı kesilmesine karşı 10 saniyelik kontrol de bulunur.
- Yeni admin talebi mail göndermez. Mevcut “Şifremi unuttum” akışı ayrı olarak devam eder.
- Mevcut adminin doğrudan şifre belirleme işlemi kullanıcıya kendi şifresini belirleme zorunluluğu bırakır. Kullanıcının şifre değişikliği eski erişim ve değişiklik tokenlarını iptal eder.
- Yeni şifreler en az 8 karakter, büyük harf, küçük harf ve rakam içermelidir. Oluşturma ve değiştirme formları aynı kuralları uygular; mevcut şifreler ilk girişte bu kurala göre yeniden değerlendirilmez.
- Admin liste ve detay ekranları bekleyen talepleri 10 saniyede bir yeniler; kullanıcı şifresini değiştirdiğinde buton tekrar açılır. Arka plan oturum kontrolündeki geçici ağ hatası kullanıcıyı oturumdan çıkarmaz.
- Talep API'si admin rolünü güncel DB atamasındaki `ADMIN` kodundan doğrular; eski tokenlarda yalnızca `SuperAdmin` rol adının bulunması yetkili kullanıcıyı engellemez. Rol kaldırılmışsa eski token erişim sağlayamaz. Kullanıcı menüsü düzenleme yetkisi ayrıca zorunludur.

Canlı DB incelemesi (07.10.2026): 188 kullanıcı; son migration `20261005192319_AddMgsSheets`; yeni alanlar ve parametre henüz bulunmuyordu. İnceleme yalnızca SELECT sorgularıyla yapıldı; canlı migration/seed bu inceleme sırasında uygulanmadı.

## Manuel kontrol

1. Mevcut kullanıcı normal giriş yapabilmeli; başlangıç tarihi rollout gününden başlamalı.
2. Admin liste/detay butonundan talep gönderdiğinde buton pasif olmalı, açık kullanıcı oturumu değişiklik ekranına geçmeli.
3. Kullanıcı mevcut şifreyi tekrar kullanamamalı; yeni şifreyle giriş yaptıktan sonra admin butonu tekrar aktif olmalı.
4. Adminin yeni oluşturduğu kullanıcı ilk girişte kendi şifresini belirlemeli.
5. Parametreyi değiştirerek süresi dolmuş kullanıcı girişinin engellendiği kontrol edilmeli.
6. Eski erişim tokenı, değiştirme tokenı ve sayfa yenilemesiyle zorunluluk aşılamamalı.
