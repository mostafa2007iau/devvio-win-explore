using System.Collections.Generic;
using System.Globalization;

namespace Devvio.Archiver.Core
{
    /// <summary>
    /// Bilingual UI strings. Persian is used when the Windows UI language is fa-*,
    /// otherwise English. RTL is enabled for rtl languages.
    /// </summary>
    public static class Strings
    {
        public static readonly bool IsRtl;
        public static readonly bool IsFa;

        static Strings()
        {
            string two;
            try
            {
                two = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            }
            catch
            {
                two = "en";
            }
            IsFa = two == "fa";
            IsRtl = two == "fa" || two == "ar" || two == "he" || two == "ur" || two == "ps" || two == "ckb";
        }

        public static string S(string key)
        {
            string value;
            if (IsFa && Fa.TryGetValue(key, out value))
            {
                return value;
            }
            if (En.TryGetValue(key, out value))
            {
                return value;
            }
            return key;
        }

        public static string S(string key, params object[] args)
        {
            string value = S(key);
            if (args == null || args.Length == 0)
            {
                return value;
            }
            try
            {
                return string.Format(CultureInfo.CurrentCulture, value, args);
            }
            catch
            {
                return value;
            }
        }

        // English ----------------------------------------------------------------

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            { "AppName", "Devvio Archiver" },

            // Context menu
            { "MenuExtractHere", "Extract Here" },
            { "MenuExtractTo", "Extract to \"{0}\\\"" },
            { "MenuExtractDialog", "Extract..." },
            { "MenuTestArchive", "Test archive" },
            { "MenuOpenBrowser", "Open in Devvio Archive Browser" },
            { "MenuAddToArchive", "Add to archive..." },
            { "MenuCompressTo", "Compress to \"{0}\"" },
            { "MenuCompressAll", "Compress all items in this folder..." },
            { "MenuCompressAllTo", "Compress all to \"{0}\"" },
            { "MenuTitle", "Devvio Archiver" },

            // Common
            { "Ok", "OK" },
            { "Cancel", "Cancel" },
            { "Close", "Close" },
            { "Browse", "Browse..." },
            { "Password", "Password" },
            { "Destination", "Destination" },
            { "Error", "Error" },
            { "Done", "Done" },
            { "Failed", "Failed" },
            { "Cancelled", "Cancelled" },

            // Extract dialog
            { "ExtractTitle", "Extract archive" },
            { "ExtractTo", "Extract to:" },
            { "OverwritePolicy", "When files exist:" },
            { "Overwrite", "Overwrite" },
            { "AutoRename", "Keep both (rename new)" },
            { "Skip", "Skip existing" },
            { "UnwrapTar", "Also unpack the inner .tar of .tar.gz/.tar.bz2/.tar.xz archives" },
            { "OpenFolderWhenDone", "Open the destination folder when finished" },
            { "Start", "Extract" },

            // Compress dialog
            { "CompressTitle", "Add to archive" },
            { "ArchiveName", "Archive:" },
            { "Format", "Format" },
            { "Level", "Compression level" },
            { "Method", "Method" },
            { "Dictionary", "Dictionary size" },
            { "LevelStore", "Store (no compression)" },
            { "LevelFastest", "Fastest" },
            { "LevelFast", "Fast" },
            { "LevelNormal", "Normal" },
            { "LevelMaximum", "Maximum" },
            { "LevelUltra", "Ultra" },
            { "SolidArchive", "Solid archive" },
            { "EncryptNames", "Encrypt file names" },
            { "Password2", "Repeat password" },
            { "ShowPassword", "Show password" },
            { "Volume", "Split to volumes" },
            { "VolumeNone", "None" },
            { "VolumeCustom", "Custom..." },
            { "CreateArchive", "Create" },
            { "PasswordMismatch", "The passwords do not match." },
            { "ItemsToCompress", "Items: {0}" },
            { "ArchiveExists", "The archive \"{0}\" already exists.\nYes = create it from scratch (delete the old one)\nNo = update the existing archive" },
            { "ManagedEngineNotice", "The bundled 7-Zip engine was not found - the format list is reduced. Reinstall the app to restore all formats." },
            { "SingleFileFormatsOnly", "This format stores a single file only." },

            // Progress
            { "ProgressTitleCompress", "Compressing..." },
            { "ProgressTitleExtract", "Extracting..." },
            { "ProgressTitleTest", "Testing archive..." },
            { "StopButton", "Cancel" },
            { "OpenDestination", "Open destination folder" },
            { "OperationDone", "Operation completed successfully." },
            { "TestOk", "The archive is OK." },

            // Browser
            { "BrowserTitle", "Archive Browser - {0}" },
            { "BrowserExtractAll", "Extract all..." },
            { "BrowserExtractSelected", "Extract selected" },
            { "BrowserTest", "Test" },
            { "BrowserRefresh", "Refresh" },
            { "BrowserUp", "Up" },
            { "BrowserSearch", "Search..." },
            { "ColName", "Name" },
            { "ColPath", "Path" },
            { "ColSize", "Size" },
            { "ColPacked", "Packed" },
            { "ColModified", "Modified" },
            { "ColType", "Type" },
            { "StatusEntries", "{0} files, {1} folders   |   {2} (packed {3})" },
            { "EncryptedArchive", "This archive is encrypted." },
            { "LoadingArchive", "Loading archive..." },
            { "NothingSelected", "Select at least one item first." },

            // Welcome
            { "WelcomeTitle", "Devvio Archiver" },
            { "WelcomeText", "Archive support for Windows Explorer.\n\nRight-click any file or folder to compress it, and right-click an archive (7z, RAR, tar, gz, ...) to extract it - directly from Windows Explorer.\n\nUse the button below to open and browse an archive." },
            { "WelcomeOpenArchive", "Open an archive..." },

            // Misc
            { "ArchiveFilter", "Archives" },
            { "AllFiles", "All files" },
            { "NoEngineForFormat", "No available engine supports this format. Install 7-Zip or reinstall Devvio Archiver." },
            { "PasswordPrompt", "This archive is encrypted. Enter its password:" },
            { "PasswordPromptTitle", "Password required" },
            { "PasswordWrong", "The password was not accepted. Try again:" }
        };

        // Persian ----------------------------------------------------------------

        private static readonly Dictionary<string, string> Fa = new Dictionary<string, string>
        {
            { "AppName", "دِویو آرشیور" },

            // Context menu
            { "MenuExtractHere", "استخراج اینجا" },
            { "MenuExtractTo", "استخراج در «{0}\\»" },
            { "MenuExtractDialog", "استخراج..." },
            { "MenuTestArchive", "تست صحت آرشیو" },
            { "MenuOpenBrowser", "نمایش در مرورگر آرشیو دِویو" },
            { "MenuAddToArchive", "افزودن به آرشیو..." },
            { "MenuCompressTo", "فشرده‌سازی در «{0}»" },
            { "MenuCompressAll", "فشرده‌سازی همهٔ اقلام این پوشه..." },
            { "MenuCompressAllTo", "فشرده‌سازی همه در «{0}»" },
            { "MenuTitle", "دِویو آرشیور" },

            // Common
            { "Ok", "تأیید" },
            { "Cancel", "انصراف" },
            { "Close", "بستن" },
            { "Browse", "مرور..." },
            { "Password", "رمز عبور" },
            { "Destination", "مقصد" },
            { "Error", "خطا" },
            { "Done", "انجام شد" },
            { "Failed", "ناموفق" },
            { "Cancelled", "لغو شد" },

            // Extract dialog
            { "ExtractTitle", "استخراج آرشیو" },
            { "ExtractTo", "استخراج در:" },
            { "OverwritePolicy", "در صورت وجود فایل تکراری:" },
            { "Overwrite", "رونویسی شود" },
            { "AutoRename", "هر دو حفظ شوند (نام جدید)" },
            { "Skip", "از فایل موجود رد شود" },
            { "UnwrapTar", "باز کردن خودکار tar داخلیِ آرشیوهای tar.gz/tar.bz2/tar.xz" },
            { "OpenFolderWhenDone", "پس از پایان، پوشهٔ مقصد باز شود" },
            { "Start", "استخراج" },

            // Compress dialog
            { "CompressTitle", "افزودن به آرشیو" },
            { "ArchiveName", "آرشیو:" },
            { "Format", "قالب" },
            { "Level", "سطح فشرده‌سازی" },
            { "Method", "روش" },
            { "Dictionary", "اندازهٔ دیکشنری" },
            { "LevelStore", "بدون فشرده‌سازی" },
            { "LevelFastest", "سریع‌ترین" },
            { "LevelFast", "سریع" },
            { "LevelNormal", "معمولی" },
            { "LevelMaximum", "حداکثر" },
            { "LevelUltra", "فوق‌العاده" },
            { "SolidArchive", "آرشیو یکپارچه (Solid)" },
            { "EncryptNames", "رمزنگاری نام فایل‌ها" },
            { "Password2", "تکرار رمز عبور" },
            { "ShowPassword", "نمایش رمز" },
            { "Volume", "افزایش به حجم‌های" },
            { "VolumeNone", "بدون تقسیم" },
            { "VolumeCustom", "سفارشی..." },
            { "CreateArchive", "ایجاد" },
            { "PasswordMismatch", "رمزهای واردشده یکسان نیستند." },
            { "ItemsToCompress", "تعداد اقلام: {0}" },
            { "ArchiveExists", "آرشیو «{0}» از قبل وجود دارد.\nبله = از نو ساخته شود (قبلی حذف شود)\nخیر = به آرشیو موجود اضافه شود" },
            { "ManagedEngineNotice", "موتور 7-Zip همراه برنامه پیدا نشد و فهرست قالب‌ها محدود شده است. برای بازگرداندن همهٔ قالب‌ها برنامه را دوباره نصب کنید." },
            { "SingleFileFormatsOnly", "این قالب فقط یک فایل را نگه می‌دارد." },

            // Progress
            { "ProgressTitleCompress", "در حال فشرده‌سازی..." },
            { "ProgressTitleExtract", "در حال استخراج..." },
            { "ProgressTitleTest", "در حال تست آرشیو..." },
            { "StopButton", "لغو" },
            { "OpenDestination", "باز کردن پوشهٔ مقصد" },
            { "OperationDone", "عملیات با موفقیت انجام شد." },
            { "TestOk", "آرشیو سالم است." },

            // Browser
            { "BrowserTitle", "مرورگر آرشیو - {0}" },
            { "BrowserExtractAll", "استخراج همه..." },
            { "BrowserExtractSelected", "استخراج انتخاب‌شده‌ها" },
            { "BrowserTest", "تست" },
            { "BrowserRefresh", "به‌روزرسانی" },
            { "BrowserUp", "بالا" },
            { "BrowserSearch", "جستجو..." },
            { "ColName", "نام" },
            { "ColPath", "مسیر" },
            { "ColSize", "حجم" },
            { "ColPacked", "فشرده" },
            { "ColModified", "تاریخ" },
            { "ColType", "نوع" },
            { "StatusEntries", "{0} فایل، {1} پوشه   |   {2} (فشرده {3})" },
            { "EncryptedArchive", "این آرشیو رمزنگاری شده است." },
            { "LoadingArchive", "در حال خواندن آرشیو..." },
            { "NothingSelected", "ابتدا حداقل یک مورد را انتخاب کنید." },

            // Welcome
            { "WelcomeTitle", "دِویو آرشیور" },
            { "WelcomeText", "پشتیبانی کامل از آرشیوها در ویندوز اکسپلورر.\n\nروی هر فایل یا پوشه راست‌کلیک کنید تا فشرده شود، و روی هر آرشیو (7z، RAR، tar، gz و ...) راست‌کلیک کنید تا استخراج شود — مستقیماً در خود اکسپلورر ویندوز.\n\nبا دکمهٔ زیر می‌توانید یک آرشیو را باز و مرور کنید." },
            { "WelcomeOpenArchive", "باز کردن یک آرشیو..." },

            // Misc
            { "ArchiveFilter", "آرشیوها" },
            { "AllFiles", "همهٔ فایل‌ها" },
            { "NoEngineForFormat", "هیچ موتورِ در دسترسی از این قالب پشتیبانی نمی‌کند. 7-Zip را نصب کنید یا برنامه را دوباره نصب کنید." },
            { "PasswordPrompt", "این آرشیو رمزنگاری شده است. رمز آن را وارد کنید:" },
            { "PasswordPromptTitle", "رمز لازم است" },
            { "PasswordWrong", "رمز پذیرفته نشد. دوباره تلاش کنید:" }
        };
    }

    /// <summary>Misc formatting helpers.</summary>
    public static class Fmt
    {
        public static string Size(long bytes)
        {
            if (bytes < 0)
            {
                return bytes.ToString();
            }
            if (bytes < 1024)
            {
                return bytes + " B";
            }
            double value = bytes;
            string[] units = { "KB", "MB", "GB", "TB" };
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return value.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + " " + units[unit];
        }
    }
}
