## کدام فایل را دانلود کنم؟

فایل مناسب سیستم‌عامل خود را از بخش **Assets** همین صفحه دانلود کنید. اگر مطمئن نیستید، از جدول پایین کمک بگیرید.

### ویندوز ۱۰ و ۱۱ (۶۴-bit)

| فایل | توضیح |
|---|---|
| `Persian.Typo.Fixer.exe` | نسخهٔ پرتابل ویندوز؛ نیازی به نصب ندارد. |

برنامه را اجرا کنید و از سینی سیستم استفاده کنید. اگر Windows SmartScreen هشدار داد، روی **More info** و سپس **Run anyway** بزنید.

### لینوکس (x86_64)

| اگر این توزیع را دارید | فایل مناسب | نصب |
|---|---|---|
| اوبونتو ۲۲.۰۴+، دبیان ۱۲+، مینت ۲۱+، پاپ!_OS، زورین، کالی | `persian-typo-fixer_*_amd64.deb` | دوبار کلیک کنید یا در ترمینال: `sudo apt install ./persian-typo-fixer_*_amd64.deb` |
| فدورا و توزیع‌های جدید RPM با glibc 2.35+ (از جمله openSUSE Tumbleweed) | `persian-typo-fixer-*.x86_64.rpm` | دوبار کلیک کنید یا: `sudo dnf install ./persian-typo-fixer-*.x86_64.rpm` |
| آرچ، مانجارو، EndeavourOS | `persian-typo-fixer-*-x86_64.pkg.tar.zst` | `sudo pacman -U ./persian-typo-fixer-*-x86_64.pkg.tar.zst` |
| هر توزیع دیگری، یا نمی‌دانید کدام را بردارید | `Persian.Typo.Fixer-*-x86_64.AppImage` | `chmod +x Persian.Typo.Fixer-*.AppImage` و بعد `./Persian.Typo.Fixer-*.AppImage` |
| نسخهٔ پرتابل بدون نصب | `Persian.Typo.Fixer-*-linux-x86_64.tar.gz` | استخراج کنید و `./PersianTypoFixer` را اجرا کنید |

- **اگر توزیعتان را نمی‌شناسید:** در ترمینال `cat /etc/os-release` را اجرا کنید؛ مقدار `NAME` یا `ID` معمولاً نام توزیع را نشان می‌دهد. اگر باز هم مطمئن نیستید، **AppImage** امن‌ترین انتخاب است.
- **اگر AppImage اجرا نشد:** در اوبونتو/دبیان `sudo apt install libfuse2` را نصب کنید، یا فایل را با `./Persian.Typo.Fixer-*.AppImage --appimage-extract-and-run` اجرا کنید.
- **در Wayland** برای اصلاح خودکار هنگام تایپ به `ydotool`، سرویس `ydotoold` و دسترسی گروه `input` نیاز دارید. راهنمای کامل در [README](https://github.com/TheGreatAzizi/Persian-Typo-Desktop#لینوکس-x86_64) است.
- **فقط در مرورگر تایپ می‌کنید؟** نیازی به این برنامه ندارید؛ [افزونهٔ مرورگر](https://github.com/TheGreatAzizi/Persian-Typo-Fixer) سبک‌تر است.

### بررسی سلامت فایل (اختیاری)

فایل `SHA256SUMS.txt` در همین صفحه قرار دارد. در لینوکس، در پوشهٔ دانلودها اجرا کنید:

```bash
sha256sum -c SHA256SUMS.txt
```

برای ویندوز می‌توانید هش فایل را با `Get-FileHash .\Persian.Typo.Fixer.exe` مقایسه کنید.

## تغییرات این نسخه

- افزودن بستهٔ نصب مخصوص دبیان/اوبونتو (`.deb`) و فدورا/RPM (`.rpm`).
- افزودن بستهٔ آرچ لینوکس (`.pkg.tar.zst`) و نسخهٔ پرتابل (`.tar.gz`).
- حفظ AppImage برای توزیع‌هایی که بستهٔ اختصاصی ندارند.
- راهنمای انتخاب فایل و نصب، همراه با این صفحه.
- مستندات کامل‌تر و بخش مقایسهٔ نسخهٔ دسکتاپ با افزونهٔ مرورگر.

برای گزارش مشکل یا پیشنهاد، از [Issues](https://github.com/TheGreatAzizi/Persian-Typo-Desktop/issues) استفاده کنید.
