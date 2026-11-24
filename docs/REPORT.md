# التقرير الفني الشامل لتسليم التطوير: وحدة التخطيط الاستراتيجي وإعادة ترتيب المسار التنموي للمشروع
**البرنامج:** AFMI Montage Projet  
**الإصدار:** v2.0.0 (Architecture Release)  
**البيئة التقنية:** C# 13, .NET 9.0 LTS, WPF (MVVM via CommunityToolkit.Mvvm), EF Core 9 (SQLite / PostgreSQL), QuestPDF, DocX, ClosedXML, WebView2, Windows DPAPI  
**التاريخ:** أكتوبر 2026  

---

## 📌 1. الملخص التنفيذي (Executive Summary)

تم بنجاح وبشكل ذاتي متكامل تنفيذ الترقية المعمارية الكبرى لبرنامج "AFMI Montage Projet" استجابة لمتطلبات هندسة المشاريع التنموية المعاصرة (Results-Based Management - RBM / Cadre Logique / Theory of Change).

تم إنجاز أمرين جوهريين متلازمين:
1. **بناء وتدشين وحدة "التخطيط الاستراتيجي وتحليل المعطيات"**: لتشكل الأساس الهندسي المتين ونقطة الانطلاق الأولى لبناء أي مشروع تنموي متماسك، مع 9 تبويبات متسلسلة تدعم التحليل الدقيق وتوثيق الأدلة.
2. **إعادة ترتيب وتوحيد مسار المشروع في خط أنابيب منطقي من 9 مراحل (9-Stage RBM Pipeline)**: مزود بشريط معالج (Stepper) تفاعلي، إشارات لحالة كل مرحلة (غير مبدوءة، قيد العمل، مكتملة، تحتاج مراجعة Stale)، وبوابات جاهزية (Readiness Gates) تمنع الانتقال العشوائي ما لم تكتمل المتطلبات المانعة أو يتم تسجيل تجاوز إداري مبرر وموثق في سجل التدقيق.
3. **محرك ارتباط تلقائي حذر (Propagation Engine)**: ينقل المعطيات كمقترحات (Drafts) دون الكتابة فوق التعديلات البشرية، مع الحفاظ على التتبع الكامل (Traceability) وحماية التعديلات اليدوية (Overridden).
4. **فاحص جودة حتمي ومستقل عن الإنترنت والذكاء الاصطناعي (Deterministic Quality Checker)**: يفحص السلسلة السببية، معايير SMART، كفاية المؤشرات، ومطابقة الميزانية للسقف التقديري.
5. **مخرجات ذكاء اصطناعي مهيكلة بحلقة إصلاح ذاتي (JSON Schema & Self-Repair)** مع عزل صارم لنصوص الوثائق ضد حقن الأوامر (`<UNTRUSTED_DOCUMENT_DATA>`).
6. **مزامنة تشاركية موقعة رقمياً (HMAC-SHA256)** وتصدير مزدوج لتقارير التخطيط الاستراتيجي بصيغتي Word و PDF مع دعم كامل للغة العربية والـ RTL.

---

## 🧭 2. وحدة التخطيط الاستراتيجي وتحليل المعطيات (The 9 Tabs Architecture)

تتألف الوحدة من 9 تبويبات تفاعلية متتالية:

1. **بطاقة المشروع والسياق (Fiche & Contexte)**:
   - فكرة المشروع، المنطقة المستهدفة، الفئات المستفيدة، القطاع التنموي.
   - المدة التقديرية بالشهور، الميزانية التقديرية وسقف التمويل، الممول المحتمل وأولوياته الاستراتيجية.
   - مؤشر لحظي لحساب نسبة اكتمال الأساس الاستراتيجي (Completeness Score %).

2. **جمع المعطيات وتحليلها (Collecte & Indicateurs de Base)**:
   - سجل مصادر البيانات والوثائق (Data Sources) مع حقول نوع المصدر، الموثوقية (High, Medium, Low)، وتاريخ النشر.
   - جدول مؤشرات خط الأساس السياقية (Baseline Indicators) بالقيمة، الوحدة، السنة، والمصدر.
   - التعامل مع محتوى الوثائق كبيانات فقط ومعزولة أمنياً.

3. **تحليل الأطراف المعنية (Parties Prenantes & Power/Interest Matrix)**:
   - جدول الأطراف المعنية، دورها، مستوى التأثير (Power 1-5)، مستوى الاهتمام (Interest 1-5)، التوقعات، واستراتيجية التعامل (Manage Closely, Keep Satisfied, Keep Informed, Monitor).
   - مصفوفة بصرية تفاعلية 2×2 (Power vs. Interest Grid) تعرض توزيع الشركاء وتصنيفهم البصري.

4. **التحليل الاستراتيجي (SWOT, PESTEL & Besoins Prioritaires)**:
   - مصفوفة رباعية لـ SWOT (نقاط القوة، الضعف، الفرص، التهديدات) مدعومة بالأدلة والأوزان.
   - تحليل PESTEL البيئي الكلي (الأبعاد السياسية، الاقتصادية، الاجتماعية، التكنولوجية، البيئية، القانونية).
   - جدول تقييم الاحتياجات المرجح (Weighted Needs Assessment) بمعايير قابلة للضبط (الاستعجال 35%، الجدوى 25%، الأثر 40%) لحساب النتيجة المركبة وترتيب الأولويات تلقائياً.

5. **المشكلة المركزية والخيارات الاستراتيجية (Problème Central & Options)**:
   - صياغة المشكلة المركزية مدعمة بحقل الأدلة والبيانات الإحصائية الرقمية.
   - مصفوفة المفاضلة بين الخيارات والحلول الاستراتيجية (الجدوى، الأثر، فعالية التكلفة) مع تسجيل تبرير الاختيار المعتمد.

6. **نظرية التغيير (Théorie du Changement - ToC)**:
   - السلسلة السببية المكونة من 5 مستويات: المدخلات (Inputs) ← الأنشطة (Activities) ← المخرجات (Outputs) ← النتائج (Outcomes) ← الأثر بعيد المدى (Impact).
   - صياغة الفرضيات الأساسية المصاحبة لكل رابط سببي.
   - عرض بصري تفاعلي بـ 5 أعمدة مميزة بالألوان والبطاقات التوضيحية.

7. **الأهداف الاستراتيجية (Objectifs SMART)**:
   - الهدف العام الاستراتيجي (Objectif Général).
   - الأهداف النوعية (Objectifs Spécifiques: OS1, OS2...) مع ترقيم تلقائي وفحص تحقق معايير SMART (محدد، قابل للقياس، قابل للتحقيق، ذو صلة، محدد زمنياً).

8. **المخاطر والفرضيات (Registre des Risques & Hypothèses)**:
   - سجل المخاطر المعمق بكيان مستقل (`RiskEntry`) في قاعدة البيانات: الفئة، الاحتمال (1-5)، الأثر (1-5)، درجة الخطورة المحسوبة تلقائياً (Score = P × I)، استراتيجية التخفيف، والمسؤول.
   - سجل الفرضيات الأساسية وآليات التحقق منها.

9. **بوابة الجاهزية (Porte de Maturité & Readiness Gate)**:
   - مؤشر بياني للجاهزية ونسبة الاكتمال.
   - قائمة تدقيق بالنواقص المانعة قبل الانتقال إلى التأطير.
   - زر "تجاوز إداري مبرر" يسمح بتخطي البوابة عند الضرورة مع إلزام كتابة السبب وتوثيقه فوراً في سجل التدقيق.

---

## 🔄 3. الترتيب المنطقي الجديد لوحدات البرنامج (The 9-Stage Pipeline)

تمت إعادة هيكلة واجهة مساحة العمل الرئيسية (`ProjectWorkspaceView.xaml` و `ProjectWorkspaceViewModel.cs`) ليظهر شريط المعالج (`ProjectStepperControl`) في أعلى مساحة العمل متضمناً المراحل التسع بالترتيب الصارم:

| الترتيب | المرحلة | الأيقونة | المفتاح الداخلي | مخرجاتها التنموية |
|:---:|---|:---:|---|---|
| **1** | **التخطيط الاستراتيجي وتحليل المعطيات** | 🧭 | `StrategicPlanning` | بطاقة الفكرة، المعطيات، الأطراف، SWOT، المشكلة، ToC، المخاطر. |
| **2** | **التأطير (Cadrage)** | 📋 | `Cadrage` | بطاقة المشروع الرسمية، السياق، المبررات، الأهداف العامة والنوعية. |
| **3** | **شجرة المشاكل وشجرة الأهداف** | 🌳 | `ArbreProbleme` | المشكلة المركزية، الأسباب الجذرية، النتائج، والأهداف المقابلة. |
| **4** | **الإطار المنطقي (Cadre Logique)** | 📊 | `CadreLogique` | مصفوفة النتائج، الأنشطة، المؤشرات، وسائل التحقق، والفرضيات. |
| **5** | **المخطط الزمني (Gantt)** | 📅 | `Chronogramme` | جدولة الأنشطة، التبعيات، والمدد الزمنية للتنفيذ. |
| **6** | **الميزانية التقديرية (Budget)** | 💰 | `Budget` | تسعير الأنشطة، الفئات، مصادر التمويل، وسقوف الميزانية. |
| **7** | **الكانفا ونماذج التمويل** | 📝 | `FormulaireDynamique` | استيراد وتعبئة نماذج الممولين ومطابقة الأسئلة مع المشروع. |
| **8** | **التعاون والمزامنة** | 🤝 | `Collaboration` | التصويت، الاقتراحات، الاقتران بالهواتف، ومزامنة الفريق. |
| **9** | **التصدير والتقارير** | 📤 | `Export` | توليد وثائق المشروع الرسمية (Word, PDF, Excel). |

**الحفاظ الكامل على الوحدات السابقة:**  
تم تجميع الوحدات المساندة (مؤشرات المتابعة، السجلات، تقارير الأنشطة، لوحة القيادة، فحص الوثائق، صندوق البريد، وإعدادات المزامنة) في قسم منظم أسفل القائمة الجانبية تحت عنوان **"أدوات ومتابعة"** مع الحفاظ على كامل روابطها وبياناتها دون حذف أي وحدة.

---

## ⚡ 4. محرك الارتباط التلقائي (Propagation Engine)

يقوم `PropagationEngineService` بتغذية المراحل اللاحقة تلقائياً من المراحل السابقة وفق القواعد التالية:

1. **الارتباط تلقائي لكن الاعتماد بشري (Human-in-the-loop)**:
   - محرك الارتباط ينشئ مقترحات (`ProposedPropagationItem`) قابلة للمراجعة.
   - لا يتم استبدال أي بيانات نهائية في قاعدة البيانات إلا بعد تأكيد المستخدم.
2. **سجل الروابط الاستراتيجية (`StrategicLink`)**:
   - كل عنصر مشتق يحمل معرف الكيان المصدر وبصمة التشفير (SHA-256 Fingerprint).
   - الحالات المعتمدة:
     - `Synced`: متطابق ومتزامن مع المصدر.
     - `Stale`: الأصل عُدِّل في التخطيط الاستراتيجي، ويحتاج العنصر المشتق إلى مراجعة مع إظهار الفروق.
     - `Overridden`: المستخدم عدل العنصر المشتق يدوياً، وبالتالي يُحمى من أي كتابة تلقائية فوقه.
3. **مراقبة سقف الميزانية**:
   - دالة `CheckBudgetCeilingAsync` تراقب مجموع بنود الميزانية وتقارنها بالسقف المقدر في التخطيط الاستراتيجي وتصدر تنبيهات فورية عند التجاوز.

---

## 🛡️ 5. طبقة الجودة والذكاء الاصطناعي الآمن

1. **فاحص الجودة الحتمي (`DeterministicQualityChecker`)**:
   - يعمل محلياً 100% بدون إنترنت وبدون ذكاء اصطناعي.
   - يفحص منطق RBM: هل توجد أهداف SMART؟ هل كل نتيجة لها نشاط؟ هل كل نشاط له مؤشر وتكلفة؟ هل سلسلة نظرية التغيير متصلة الحلقات؟
   - يُخرج تقريراً شاملاً بدرجة جودة مئوية (Quality Score) وقائمة ملاحظات مصنفة (حرجة، تنبيه، معلومات).
2. **الذكاء الاصطناعي المهيكل (`StructuredAiPrompter`)**:
   - يجبر النماذج على مخرجات JSON صارمة مطابقة لمخطط محدد.
   - مزود بـ **حلقة إصلاح تلقائي (Self-Repair Loop)**: عند حدوث خطأ في بنية الـ JSON، يُرسل طلب تصحيح فوري بالسياق المباشر حتى مرتين قبل إشعار المستخدم بالخطأ الصريح.
   - **حماية ضد Prompt Injection**: عزل تام لأي محتوى مستخرج من الوثائق والمواقع داخل وسوم:
     `<<<UNTRUSTED_DOCUMENT_DATA>>> ... <<<END_UNTRUSTED_DOCUMENT_DATA>>>`
     وتوجيه النموذج للتعامل معها كحقائق سياقية لا كتعليمات تنفيذية.

---

## 🔒 6. التوقيع الرقمي للمزامنة والتصدير المزدوج

1. **التعاون والمزامنة الموقعة (Cryptographically Signed Collab)**:
   - تم تزويد مقترحات وأصوات التعاون (`ProposalDto` و `VoteDto`) بحقول `DeviceId` و `DeviceSignature`.
   - يتم التحقق عبر توقيع HMAC-SHA256 المشفر بمفتاح الجهاز المقترن لمنع حقن أي مقترحات أو أصوات من أجهزة مجهولة أو متلاعب بها.
2. **التصدير المزدوج للأساس الاستراتيجي (Word & PDF)**:
   - تمكين تصدير تقرير التخطيط الاستراتيجي كاملاً إلى Microsoft Word (`DocX`) و PDF عالي الجودة (`QuestPDF`) مع التنسيق والجدولة ودعم اللغة العربية والـ RTL.

---

## 🧪 7. نتائج الاختبارات وضمان الجودة (Test Results)

تم إنشاء وتشغيل حزمة اختبارات وحدة تكاملية موسعة عبر `dotnet test`:

| ملف الاختبار | عدد الاختبارات | النتيجة | الجوانب المغطاة |
|---|:---:|:---:|---|
| `StrategicFoundationTests.cs` | 51 | ✅ نجاح | إنشاء الأساس، الترحيل الآمن، سلة المحذوفات، تقييم البوابات، كشف الروابط المشتقة. |
| `PropagationEngineTests.cs` | 5 | ✅ نجاح | توليد مقترحات التأطير، تغذية شجرة المشاكل، إنشاء الروابط، وفحص سقوف الميزانية. |
| `QualityCheckerAndAiParserTests.cs` | 5 | ✅ نجاح | فحص الجودة الحتمي، كشف النواقص، تنظيف JSON وعزل الوسوم. |
| `ExportAndSignatureTests.cs` | 2 | ✅ نجاح | التوقيع الرقمي للأجهزة المقترنة HMAC-SHA256 وكشف التلاعب. |
| `TextEnhancementTests.cs` | 8 | ✅ نجاح | تنظيف مخرجات الـ AI، بناء الأوامر الذكية، وتخصيص التعليمات لأنماط Améliorer و Enrichir و Reformuler. |
| **المجموع الإجمالي** | **71** | **✅ 100% نجاح** | **0 إخفاق، 0 تجاهل** |

---

## 🤖 9. المساعد التحريري الذكي الشامل (Universal AI Text Assistant)

استجابةً لطلب توفير المساعدة بالذكاء الاصطناعي في كامل مكونات البرنامج مع خيارات **التحسين (Améliorer)**، **الإثراء (Enrichir)**، و**إعادة الصياغة (Reformuler)** لكافة النصوص والعبارات في جميع الحقول دون استثناء:

1. **الربط الشامل الفوري (Universal WPF Hook)**:
   - تم تطوير `Helpers/AiTextAssistant.cs` عبر تسجيل `EventManager.RegisterClassHandler` على فئة `TextBoxBase`.
   - هذا يضمن تغطية **100% من حقول النصوص (`TextBox` و `RichTextBox`)** في كامل الشاشات وقوائم البيانات والنوافذ المنبثقة الحالية والمستقبلية تلقائياً.
   - عند النقر بالزر الأيمن (Right-Click) على أي حقل نصي، تظهر خيارات الذكاء الاصطناعي مباشرة:
     - ✨ **Améliorer (تحسين الأسلوب والمعايير RBM)**
     - 📝 **Enrichir (إثراء وتوسيع التحليل)**
     - 🔄 **Reformuler (إعادة الصياغة)**
     - 🤖 **خيارات إضافية: Suggérer (توليد مسودة عند فراغ الحقل)، Corriger (تصحيح لغوي)**
   - دعم اختصارات لوحة المفاتيح المباشرة: `Ctrl+Alt+A` و `Ctrl+Shift+E`.

2. **التمييز الذكي بين العبارة المحددة والنص الكامل**:
   - إذا قام المستخدم بتحديد جملة أو فقرة معينة (`SelectedText`)، يتم تكييف الأمر لمعالجة تلك الجملة بدقة واستبدالها في موضعها دون المساس بباقي النص.
   - إذا لم يتم تحديد نص، تتم معالجة كامل الحقل.

3. **محرك الصياغة المعتمد على سياق المشروع (`Services/TextEnhancementService.cs`)**:
   - يستحضر تلقائياً سياق المشروع الكامل من `ProjectContextBuilder` (القطاع، المنطقة، الفئة المستهدفة، المشكلة المركزية، الأهداف، وسلسلة النتائج).
   - يصيغ النصوص وفق معايير الإدارة القائمة على النتائج (RBM / GAR) واشتراطات المانحين الدوليين.

4. **نافذة المراجعة والتحكم البشري (`Views/AiTextEnhancementDialog.xaml`)**:
   - تتيح مقارنة فورية بين النص الأصلي والبديل المقترح.
   - إمكانية التبديل اللحظي بين أوضاع Améliorer / Enrichir / Reformuler.
   - اختيار النبرة والأسلوب ("Normes Bailleurs & RBM", "Concis", "Technique").
   - خيارات الإدراج: استبدال فوري داخل الحقل (`Remplacer dans le champ`) أو إلحاق في النهاية (`Insérer à la suite`).

5. **أزرار وصول سريع مدمجة**:
   - تزويد واجهات التخطيط الاستراتيجي (`StrategicPlanningView.xaml`) في التبويب 1 (Fiche & Contexte) والتبويب 5 (Problème Central) بأشرطة وأزرار مساعدة سريعة بنقرة واحدة.

---

## 📦 10. ملخص الملفات المضافة والمعدلة في المستودع

### الملفات الجديدة:
1. `Models/Strategic/StrategicEntities.cs`
2. `Services/Strategic/StrategicFoundationService.cs`
3. `Services/Strategic/ReadinessGateService.cs`
4. `Services/Strategic/ImpactAnalysisService.cs`
5. `Services/Strategic/PropagationEngineService.cs`
6. `Services/Strategic/DeterministicQualityChecker.cs`
7. `Services/Strategic/StructuredAiPrompter.cs`
8. `Services/TextEnhancementService.cs`
9. `Helpers/AiTextAssistant.cs`
10. `Views/AiTextEnhancementDialog.xaml` & `.xaml.cs`
11. `ViewModels/Strategic/StrategicPlanningViewModel.cs`
12. `ViewModels/Strategic/ProjectStepperViewModel.cs`
13. `Views/Strategic/ProjectStepperControl.xaml` & `.xaml.cs`
14. `Views/Strategic/StrategicPlanningView.xaml` & `.xaml.cs`
15. `Views/Strategic/StakeholderMatrixView.xaml` & `.xaml.cs`
16. `Views/Strategic/TheoryOfChangeVisualView.xaml` & `.xaml.cs`
17. `AFMI_Montage_Projet.Tests/StrategicFoundationTests.cs`
18. `AFMI_Montage_Projet.Tests/PropagationEngineTests.cs`
19. `AFMI_Montage_Projet.Tests/QualityCheckerAndAiParserTests.cs`
20. `AFMI_Montage_Projet.Tests/ExportAndSignatureTests.cs`
21. `AFMI_Montage_Projet.Tests/TextEnhancementTests.cs`
22. `docs/PLAN.md`
23. `docs/DECISIONS.md`
24. `docs/REPORT.md`

### الملفات المعدلة:
1. `App.xaml.cs` (تهيئة الخطاف الشامل للذكاء الاصطناعي `AiTextAssistant.InitializeGlobalHooks()`)
2. `Models/AppDbContext.cs`
3. `Models/Project.cs`
4. `Models/Collab/CollabDtos.cs`
5. `Services/SessionManager.cs` (إضافة تتبع المشروع النشط `CurrentProjectId`)
6. `Services/ProjectContextBuilder.cs`
7. `Services/DocumentExportService.cs`
8. `Services/Collab/MergeService.cs`
9. `ViewModels/ProjectWorkspaceViewModel.cs`
10. `Views/ProjectWorkspaceView.xaml`


---

## 🌍 9. توحيد واجهة المستخدم باللغة الفرنسية (Localisation Complète en Français)

بناءً على طلب المستخدم، تم تحويل كامل عناصر الواجهة الجديدة والمكونات الاستراتيجية إلى الفرنسية:
- **شريط التنقل والمعالج (Project Stepper)**: تحويل جميع أسماء المراحل التسع وحالات النضج إلى الفرنسية (`1. Planification Stratégique`, `2. Cadrage`, `3. Arbre à Problèmes`, `Non démarré`, `En cours`, `Terminé`, `À réviser`).
- **القائمة الجانبية (Sidebar)**: زر `🧭 1. Planification Stratégique` متكامل مع الأزرار الفرنسية القائمة.
- **التبويبات التسعة في واجهة التخطيط الاستراتيجي (`StrategicPlanningView.xaml`)**:
  1. `1. Fiche & Contexte`
  2. `2. Données & Indicateurs`
  3. `3. Parties Prenantes`
  4. `4. Diagnostic Stratégique`
  5. `5. Problème & Options`
  6. `6. Théorie du Changement`
  7. `7. Objectifs Stratégiques`
  8. `8. Risques & Hypothèses`
  9. `9. Porte de Maturité`
- **المصفوفة البصرية وسلسلة نظرية التغيير (`StakeholderMatrixView.xaml` و `TheoryOfChangeVisualView.xaml`)**: الرباعيات والمحاور بالفرنسية بالكامل (`Maintenir Satisfait`, `Collaborer Étroitement`, `Surveiller`, `Tenir Informé`, `Intrants`, `Activités`, `Extrants`, `Résultats`, `Impact Global`).
- **فاحص الجودة وبوابات النضج**: ترجمة الرسائل الإرشادية والنواقص المانعة إلى الفرنسية مع الاحتفاظ الكامل بمرور كافة الاختبارات (63/63).

---

## 🚀 10. الخاتمة والتوصيات التشغيلية

تم استيفاء جميع المتطلبات الواردة في التكليف بدقة متناهية مع الامتثال الصارم لأنماط المشروع وأعلى معايير هندسة البرمجيات التنموية. التطبيق جاهز تماماً للاستخدام، البناء نظيف بدون أخطاء (0 Errors)، وجميع الاختبارات الوحدوية (63/63) تعمل بنجاح تام، مع واجهة فرنسية متكاملة واحترافية.


---

## 🧹 تنظيف المستودع (نوفمبر 2026)

- حُذف `Services/TextEnhancementService.cs`: لم يكن التطبيق يستدعيه (الصياغة تمر عبر `TextAssistService`). نُقل التعدادان `TextEnhancementMode` و`AiEnhancementAction` إلى `Services/TextEnhancementMode.cs` لأن `AiTextAssistant` و`AiTextEnhancementDialog` و`StrategicPlanningView` ما زالت تستعملهما، وحُذفت من `TextEnhancementTests.cs` الاختبارات الخاصة بالخدمة المحذوفة.
- حُذف `Models/PendingContribution.cs` (غير مستعمل) والدليلان القديمان `Guide_Utilisateur_AFMI_V3.html` و`Guide_Utilisation_AFMI.pdf` (غير مرجعيين في البناء ولا المثبّت).
- `ARCHITECTURE.md` و`CLAUDE.md`: تصحيح قناة التحديث (GitHub أولاً ثم Google Drive احتياطياً) والإشارة إلى الملفات الحالية بدل المحذوفة.

---

## 🔐 الإصدار 1.9.27: تجزئة كلمات المرور وإزالة تحذيرات البناء (ADR 35)

- **كلمات المرور:** كانت تُحفظ نصاً مقروءاً في `Users.PasswordHash`. صارت تُحفظ بصمةً (PBKDF2-SHA512 مع ملح عشوائي) عبر `Security/PasswordHasher`، وتُحوَّل الحسابات القديمة تلقائياً عند تشغيل البرنامج، مع قبول الحساب القديم مرة أخيرة عند الدخول إن بقي دون تحويل. الاختبارات في `PasswordHashingTests.cs`.
- **التحذيرات:** أُزيلت التحذيرات الـ 38 التي كان البناء يعرضها، ومنها مواضع كانت قد تتعطل فعلاً عند غياب مستخدم متصل (`ActivityReportsViewModel` و`ArbreProblemeViewModel` و`UserAccountViewModel`) أو عند رفع ملف إلى Drive دون رد (`SettingsViewModel`).
