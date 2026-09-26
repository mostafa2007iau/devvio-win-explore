# Devvio Archiver — پشتیبانی کامل از آرشیوها در ویندوز اکسپلورر

**Devvio Archiver** یک اپلیکیشن رایگان و متن‌باز ویندوز (۱۰ و ۱۱) است که مثل پشتیبانی داخلی ZIP، امکان **فشرده‌سازی و استخراج** فرمت‌های دیگر آرشیو را مستقیماً به **ویندوز اکسپلورر** اضافه می‌کند.

> ویندوز به‌صورت داخلی فقط ZIP را می‌شناسد. با نصب این برنامه، راست‌کلیک روی `7z`، `RAR`، `tar.gz`، `cab`، `iso` و ده‌ها فرمت دیگر هم مستقیماً در خودِ اکسپلورر کار می‌کند — هم برای فشرده‌سازی فایل‌ها و پوشه‌ها و هم برای استخراج.

---

## ✨ امکانات

- **منوی راست‌کلیک کامل در اکسپلورر** (مثل 7-Zip) برای همهٔ فایل‌ها، پوشه‌ها، پس‌زمینهٔ پوشه‌ها و درایوها:
  - روی آرشیوها: **استخراج اینجا**، **استخراج در «پوشه‌ای به نام فایل»**، **استخراج…** (با گزینه‌ها)، **تست صحت آرشیو**، **نمایش در مرورگر آرشیو**
  - روی فایل/پوشه‌ها: **افزودن به آرشیو…** (دیالوگ کامل)، **فشرده‌سازی سریع به 7z/zip**
  - روی پس‌زمینهٔ پوشه: فشرده‌سازی همهٔ اقلام داخل پوشه
- **مرورگر آرشیو** داخلی: لیست فایل‌ها با ناوبری پوشه‌ای، جستجو، مرتب‌سازی، دابل‌کلیک برای باز کردن فایل داخل آرشیو (حتی آرشیو تودرتو)، استخراج انتخابی
- **دیالوگ فشرده‌سازی حرفه‌ای**: انتخاب قالب (7z / zip / tar / tar.gz / tar.bz2 / tar.xz / gzip / bzip2 / xz / wim)، سطح فشرده‌سازی (Store تا Ultra)، الگوریتم (LZMA2، LZMA، PPMd، BZip2، Deflate64 و…)، اندازهٔ دیکشنری، **رمزنگاری AES-256** (و رمزنگاری نام فایل‌ها در 7z)، آرشیو Solid و **تقسیم به حجم‌های چندگانه**
- **رمز عبور**: اگر آرشیو رمزدار باشد، موقع استخراج در همان لحظه از شما پرسیده می‌شود (فایل‌های 7z با نام رمزنگاری‌شده هم پشتیبانی می‌شوند)
- **دو موتور آرشیو**:
  1. **موتور 7-Zip** (فایل‌های `7z.exe`/`7z.dll` که همراه اینستالر نصب می‌شوند) — پشتیبانی کامل از همهٔ کدک‌ها
  2. **موتور Managed (SharpCompress)** — موتور جایگزینِ همیشه‌در دسترس که بدون هیچ فایل خارجی کار می‌کند
- **رابط کاربری دوزبانه** فارسی/انگلیس (بر اساس زبان ویندوز، با پشتیبانی RTL)

## 📦 فرمت‌های پشتیبانی‌شده

| کار | فرمت‌ها |
|---|---|
| **استخراج** (موتور 7-Zip) | 7z، ZIP، RAR (شامل RAR5)، tar، tar.gz، tar.bz2، tar.xz، gzip، bzip2، xz، cab، iso، wim، lzh، arj، cpio، deb، rpm، jar/apk/war/ear، آرشیوهای چندبخشی (.001 و…) و SFX |
| **استخراج** (موتور Managed، وقتی 7-Zip موجود نباشد) | 7z، ZIP، RAR، tar، tar.gz، tar.bz2، tar.xz، gzip، bzip2، xz، arj |
| **فشرده‌سازی** | 7z، ZIP، tar، tar.gz، tar.bz2، tar.xz، gzip، bzip2، xz، wim + رمزنگاری AES در 7z/zip + تقسیم به حجم |

> ⚠️ **ساخت فایل RAR ممکن نیست** — قالب RAR اختصاصی و انحصاری شرکت RARLAB است و هیچ نرم‌افزار دیگری (حتی 7-Zip) اجازهٔ ساخت RAR ندارد. برای آرشیو رمزدار و پرحجم، 7z انتخاب بهتری است.

## 🖥️ نصب

1. فایل `DevvioArchiver-Setup-1.0.0.exe` را اجرا کنید (نیاز به دسترسی Administrator).
2. بعد از نصب، اکسپلورر یک‌بار ری‌استارت می‌شود و منوها فعال می‌شوند.
3. روی هر فایل یا پوشه راست‌کلیک کنید!

**نکتهٔ ویندوز ۱۱:** منوهای اپ‌های کلاسیک در ویندوز ۱۱ زیر گزینهٔ **«Show more options»** (یا `Shift + F10`) نمایش داده می‌شوند؛ این رفتار پیش‌فرض ویندوز برای همهٔ اپ‌های مشابه (از جمله خود 7-Zip و WinRAR) است.

### پیش‌نیازها
- ویندوز ۱۰ (نسخهٔ 1809 به بعد) یا ویندوز ۱۱
- .NET Framework 4.8 (روی ویندوزهای به‌روز از قبل نصب است)

## 🚀 دانلود بیلد آماده

**ساده‌ترین راه — صفحهٔ Releases:**

👉 **https://github.com/mostafa2007iau/devvio-win-explore/releases**

- `DevvioArchiver-Setup-1.0.0.exe` — اینستالر کامل (پیشنهادی)
- `DevvioArchiver-1.0.0-portable.zip` — نسخهٔ بدون نصب (فایل‌ها را در یک پوشه بریزید و `scripts\register.cmd` را با دسترسی Administrator اجرا کنید)

هر push موفق روی مخزن هم GitHub Actions را اجرا می‌کند و همین خروجی‌ها را در **Actions → آخرین run → Artifacts** قرار می‌دهد (نیاز به لاگین گیت‌هاب دارد و artifacts بعد از مدتی منقضی می‌شوند؛ Releases دائمی است).

## 🛠️ بیلد از سورس

### پیش‌نیازهای بیلد
- **Visual Studio 2022** (نسخهٔ 17.8 به بعد) با **workload «‎.NET desktop development‎»** — شامل MSBuild و targeting pack مربوط به ‎.NET Framework 4.8‎ است و کافی است.
- یا اگر ترجیح می‌دهید از خط فرمان بیلد بگیرید: هر ‎.NET SDK‎ نسخهٔ 8 به بعد (`dotnet build`)
- برای ساخت اینستالر: **Inno Setup 6** (`choco install innosetup`)
- برای همراه‌کردن موتور 7-Zip در بیلد: **7-Zip نصب‌شده** (`choco install 7zip`) — در غیاب آن، بیلد ادامه می‌یابد ولی موتور کامل همراه نمی‌شود

> 📌 نکتهٔ سازگاری: پروژه عمداً از **SharpCompress 0.42.1** (آخرین نسخهٔ با API کلاسیک) استفاده می‌کند تا با کامپایلر C#‏ 12/13 داخل VS 2022 هم بدون مشکل کامپایل شود و نیازی به VS 2026 یا SDK جدیدتر نباشد.
>
> ⚠️ نکتهٔ امنیتی: برای SharpCompress ≤ 0.47.4 هشدار NU1902 (GHSA-6c8g-7p36-r338 — عبور از مسیر در `WriteToDirectory`) وجود دارد. این اپ **هرگز** از متد آسیب‌پذیر `WriteToDirectory` استفاده نمی‌کند؛ مسیر هر entry قبل از استخراج توسط sanitizer داخلی (`BuildTargetPath`) پاک‌سازی می‌شود و موتور اصلی (7z.exe) هم اصلاً درگیر SharpCompress نیست. با این حال هشدار NuGet در بیلد نمایش داده می‌شود و آگاهانه پذیرفته شده است.

### مراحل
```powershell
git clone <repo>
cd devvio-win-explore
./scripts/build.ps1          # خروجی در stage\ و dist\
```

### ثبت دستی برای توسعه (بدون اینستالر)
```cmd
scripts\register.cmd                 # ثبت افزونه از پوشهٔ stage
scripts\unregister.cmd               # حذف ثبت
scripts\restart-explorer.cmd         # ری‌استارت اکسپلورر
```

## 🏗️ معماری

```
DevvioArchiver.sln
├── src/DevvioArchiver.Core          ← موتور آرشیو (بدون UI)
│   ├── ArchiveFormats.cs            ← شناسایی قالب (magic bytes + پسوند)
│   ├── ArchiveModels.cs             ← مدل‌ها: ArchiveInfo, CreateOptions, ...
│   ├── SevenZipLocator.cs           ← یافتن 7z.exe (همراه برنامه → رجیستری → PATH)
│   └── Engines/
│       ├── IArchiveEngine.cs        ← قرارداد موتور + انتخاب‌گر موتور
│       ├── SevenZipCliEngine.cs     ← موتور اصلی: اجرای 7z.exe با parse پروگرس
│       └── SharpCompressEngine.cs   ← موتور Managed جایگزین (SharpCompress)
├── src/DevvioArchiver.Shell         ← افزونهٔ COM راست‌کلیک اکسپلورر (SharpShell)
│   └── DevvioContextMenu.cs         ← ساخت منو + اجرای اپ کمکی
├── src/DevvioArchiver.App           ← اپ کمکی WinForms (دیالوگ‌ها + مرورگر)
│   └── Forms/                       ← Compress, Extract, Progress, Password, Browser, Welcome
├── installer/DevvioArchiver.iss     ← اسکریپت Inno Setup
└── scripts/                         ← build, register/unregister, ...
```

**چرا این معماری؟**
- افزونهٔ Shell فقط **منو می‌سازد و اپ کمکی را اجرا می‌کند** — هیچ عملیات سنگینی داخل پروسهٔ Explorer انجام نمی‌شود (اگر موتور کرش کند، Explorer سالم می‌ماند).
- ثبت افزونه به سبک خود 7-Zip است: یک handler برای همه (`HKCR\*\ShellEx\...` + `Directory` + `Drive`) و منطق نمایش منو داخل خود افزونه تصمیم می‌گیرد.
- موتور 7-Zip با **list file** و `WorkingDirectory` کار می‌کند تا ساختار نسبی پوشه‌ها دقیقاً مثل 7-Zip در آرشیو حفظ شود.
- پارس خروجی `‑bsp1` ابزار 7z (توکن‌های درصد + نام فایل جاری) برای نمایش پروگرس زنده انجام می‌شود و لغو عملیات با `Kill` پروسه پاسخ می‌دهد.

## 🔍 عیب‌یابی

| مشکل | راه‌حل |
|---|---|
| منوها دیده نمی‌شوند | `scripts\restart-explorer.cmd` را اجرا کنید؛ اگر نصب از سورس است، `scripts\register.cmd` را با دسترسی Administrator اجرا کنید |
| پیام «موتور 7-Zip پیدا نشد» | 7-Zip را نصب کنید یا برنامه را دوباره با اینستالر نصب کنید (موتور Managed با تعداد کمتری فرمت جایگزین می‌شود) |
| آرشیو رمزدار استخراج نمی‌شود | رمز در زمان درخواست درست وارد شده؟ برای آرشیوهای tar.gz رمزدار، ابتدا لایهٔ gzip با رمز باز می‌شود |
| فایل `.tar.gz` بعد از استخراج، یک فایل `.tar` داده | گزینهٔ «باز کردن خودکار tar داخلی» در دیالوگ استخراج را روشن نگه دارید |

## ⚖️ مجوزها

- کد این پروژه: **MIT**
- فایل‌های `7z.exe` و `7z.dll` (به‌همراه `7z\7-ZIP-LICENSE.txt` و `7z\lgpl-3.0.txt` نصب می‌شوند): مجوز **GNU LGPL** + محدودیت unRAR — سورس کامل در [7-zip.org](https://www.7-zip.org) و [github.com/ip7z/7zip](https://github.com/ip7z/7zip)
- کتابخانه‌ها: [SharpShell](https://github.com/dwmkerr/sharpshell) (MIT) و [SharpCompress](https://github.com/adamhathcock/sharpcompress) (MIT)

---

**English summary:** Devvio Archiver adds full archive support (7z, RAR, tar/gz/bz2/xz, cab, iso, wim, lzh, arj, cpio, deb, rpm, split volumes …) to Windows 10/11 Explorer — context menus for compressing and extracting any format, an archive browser, AES encryption, split volumes and a live progress window. It registers like 7-Zip does (all files/folders/drives) and delegates all heavy work to a helper app so Explorer stays safe. On Windows 11 the classic entries appear under "Show more options" (`Shift+F10`) by default.
