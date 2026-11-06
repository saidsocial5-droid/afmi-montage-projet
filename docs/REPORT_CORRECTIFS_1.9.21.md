# تقرير التصحيحات: الإصدار 1.9.21 (ADR 27)

> التاريخ: 06/11/2026. المصدر: التحليل العميق للشيفرة وسجلات GitHub Actions للإصدار 1.9.20.

## 1. سبب توقف النشر الآلي

لم يكن GitHub Actions معطلاً: سكربت `release.yml` يعمل، لكنه يتوقف في خطوة الاختبارات (260 ناجحاً من 264).

| الاختبار الفاشل | السبب | التصحيح |
|---|---|---|
| `TextAssistTests` (Enrichir وReformuler) و`RiskAiServiceTests.AuditRiskMatrixAsync` | تستدعي Ollama الحقيقي، وهو غير موجود على خادم GitHub، فتصل رسالة الخطأ بدل البديل الاستدلالي | `LocalAiEngine` يرفع استثناء، وتوجيه Ollama في الاختبارات إلى منفذ مغلق |
| `Territory_IsSingleSource…` | الأرقام تُكتب حسب لغة Windows (‎1,200‎ على خادم إنجليزي) | ثقافة تنسيق ثابتة fr-FR |

## 2. التصحيحات

1. **فشل الذكاء الاصطناعي المحلي يُعامل كفشل:**
   - `LocalAiUnavailableException` بدل إرجاع نص الخطأ كإجابة.
   - `RoutingResult.ErrorMessage` جديد، و`ResponseText` يبقى فارغاً عند الفشل.
   - البدائل الاستدلالية (المساعد النصي، المخاطر، ملف الجمعية…) تعمل الآن فعلاً دون Ollama.
   - `AiAssistantService.CallAiAsync` يرفع استثناء عند الفشل، و`StructuredAiPrompter` يتوقف دون محاولات إصلاح عبثية.
2. **ثقافة التنسيق:** `Helpers/AppCulture` (fr-FR) في `App.OnStartup` وفي `TestEnvironment`. لغة الواجهة وخاصية `Language` في WPF لم تتغيرا.
3. **الملاحظات الصوتية:** لا يُرسل الصوت إلى Gemini أو Whisper إلا بموافقة السحابة وتحت السقف الشهري (`AiRouterService.IsCloudUseAllowed`)، ويُسجَّل الإرسال في `AiLog`.
4. **الأمان:**
   - `ProfileSourceAnalysisService` يجلب الصفحات عبر `SafeHttpFetcher`.
   - مفتاح Gemini في الترويسة `x-goog-api-key` بدل عنوان URL (في `AiAssistantService` و`VoiceNoteAssistantService`).
   - قناة التحديث عبر Drive: بصمة SHA-256 إلزامية في `version.json`، تُحسب عند النشر من الإعدادات وتُتحقق قبل التشغيل.
5. **الدَّين التقني:**
   - 52 كتلة `catch { }` فارغة صارت تسجّل في `AppLogger`، و7 استدعاءات `Console.WriteLine` استُبدلت.
   - عزل `settings.json` في الاختبارات (`LocalSettingsService` يتبع `AFMI_DATA_DIR`).
   - العملة الافتراضية في مكان واحد: `Project.DefaultCurrency` و`Project.CurrencyOf`.

## 3. تصحيحات للتوثيق

- **عدد القواعد الحتمية 40 لا 36.** القواعد غير الموثقة:
  - `STRAT-01`: غياب الأساس الاستراتيجي.
  - `STRAT-02`: غياب صياغة المشكلة المركزية.
  - `STRAT-03`: نقص المعطيات الداعمة للمشكلة.
  - `SMART-01`: هدف دون مؤشر رقمي قابل للقياس.
- `NormalizeCurrency` لا يحوّل عملة مجهولة إلى MAD: التعبير النمطي لا يقبل إلا EUR وUSD وMAD/DH، فالسلوك صحيح.

## 4. ما لم يُعالَج (يحتاج قراراً أو عملاً أكبر)

- **منطق الأعمال داخل ViewModels:** 31 ملفاً تفتح قاعدة البيانات مباشرة، و`CadrageViewModel` في 1625 سطراً. إعادة الهيكلة تحتاج خطة مستقلة.
- **نظاما مزامنة متوازيان:** القديم `Services/Drive/SyncEngineService` ما زال مستعملاً في `ProjectListViewModel`، إلى جانب `Services/Collab`.
- **`MessageBox` داخل خدمتين:** `AutoUpdaterService` و`PipelineService`.
- **`OrganizationAiService` و`ProfileSourceAnalysisService` لا يستعملان Ollama:** يشترطان `AiAssistantService.IsConfigured()`، أي مفتاحاً سحابياً، قبل أي استدعاء. مستخدم Ollama وحده يحصل دائماً على البديل الاستدلالي.

## 5. التحقق

- لم تُترجم الشيفرة في بيئة التحليل لعدم توفر .NET فيها. يُشغَّل `dotnet test AFMI_Montage_Projet.Tests` على Windows، أو يُدفع الوسم `v1.9.21` ليتولى GitHub Actions الترجمة والاختبارات.
- **ملاحظة:** أي `version.json` منشور على Drive قبل 1.9.21 يُرفض لعدم احتوائه على بصمة، فيجب إعادة النشر من الإعدادات. قناة GitHub غير معنية.
