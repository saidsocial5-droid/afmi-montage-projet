# دليل النشر والتحديث عبر GitHub

**القرار المعماري:** ADR 26 في `docs/DECISIONS.md`

## كيف يعمل النظام

1. ترفع الشيفرة إلى مستودع GitHub، ثم ترسل وسماً (tag) برقم الإصدار، مثل `v1.9.20`.
2. يتولى GitHub Actions الباقي تلقائياً عبر الملف `.github/workflows/release.yml`:
   - يتحقق من تطابق أرقام الإصدار.
   - يشغّل الاختبارات.
   - يبني البرنامج.
   - ينشئ ملف التثبيت `AFMIMontageProjet_Setup_v1.9.20.exe`.
   - يحسب بصمته SHA-256.
   - ينشره في صفحة **Releases** بالمستودع.
3. عند تشغيل البرنامج على أي حاسوب مثبَّت، يجد الإصدار الجديد ويعرض نافذة «تحديث متاح». بعد الموافقة:
   - يحمّل ملف التثبيت.
   - **يتحقق من بصمته** قبل تشغيله. إن لم تطابق البصمة المنشورة يُلغى التحديث.
   - يغلق البرنامج، ثم يثبّت الإصدار الجديد، ثم يعيد فتحه.
   - المشاريع والإعدادات لا تُمس.

## الإعداد (مرة واحدة)

1. أنشئ حساباً على https://github.com، يفضَّل أن يكون للجمعية، ثم أنشئ مستودعاً، مثلاً `afmi-montage-projet`.
   - **مستودع عام (Public):** يجعل التحديثات تعمل دون أي مفتاح.
   - **مستودع خاص (Private):** يتطلب مفتاح وصول، وهذا غير مدعوم حالياً.
2. افتح PowerShell في مجلد المشروع وارفع الشيفرة. Git مثبَّت على جهازك:
   ```powershell
   git init
   git add .
   git commit -m "AFMI Montage Projet 1.9.20"
   git branch -M main
   git remote add origin https://github.com/<الحساب>/afmi-montage-projet.git
   git push -u origin main
   ```
   الملف `.gitignore` يستبعد مجلدات البناء والشهادات والملفات الثنائية الكبيرة.
3. انشر الإصدار الأول:
   ```powershell
   git tag v1.9.20
   git push origin v1.9.20
   ```
   تابع التنفيذ في تبويب **Actions**. بعد نحو 10 دقائق يظهر الإصدار في **Releases**.

## نشر إصدار جديد لاحقاً

1. غيّر رقم الإصدار في مكانين **بالرقم نفسه**:
   - `AFMI Montage projet 2.csproj` ← `<Version>1.9.21</Version>`
   - `setup.iss` ← `#define MyAppVersion "1.9.21"`
2. نفّذ:
   ```powershell
   git commit -am "Version 1.9.21"
   git push
   git tag v1.9.21
   git push origin v1.9.21
   ```

## ملاحظات

- **الحواسيب المثبَّتة قبل ربط GitHub:** يجب أن يُدخل المدير اسم المستودع (`الحساب/afmi-montage-projet`) في **Paramètres › Apparence › Mises à jour**.
  - النسخ المبنية عبر GitHub Actions تعرف مستودعها تلقائياً.
  - أو ثبّت مرة واحدة إصداراً مبنياً من GitHub.
- **البناء المحلي:** يمكنك البناء على جهازك بالأمر:
  ```powershell
  .\build_and_sign.ps1 -GitHubRepository الحساب/afmi-montage-projet
  ```
  ثم ترفع يدوياً الملفين `.exe` و`.exe.sha256` من مجلد `Installer` إلى Release جديد. **ملف البصمة إلزامي**، وبدونه يرفض البرنامج التحديث.
- **ملف التثبيت غير موقَّع رقمياً.** لذلك قد يظهر تحذير Windows SmartScreen عند أول تثبيت: «Informations complémentaires» ثم «Exécuter quand même». لإزالة التحذير تحتاج شهادة توقيع شيفرة (Code Signing)، تُمرَّر إلى `build_and_sign.ps1`.
- **عدد مرات الفحص:** يفحص البرنامج التحديثات مرة واحدة عند كل تشغيل. يمكن الفحص يدوياً من **Paramètres › Mises à jour › Rechercher une mise à jour**.
