# خطة التطوير المعمارية والتنفيذية: وحدة التخطيط الاستراتيجي وإعادة ترتيب دورة حياة المشروع
**مشروع:** AFMI Montage Projet  
**الإصدار المستهدف:** v2.0.0  
**التاريخ:** أكتوبر 2026  
**المعمارية الأساسية:** .NET 9.0 LTS (C# 13), WPF (MVVM via CommunityToolkit.Mvvm), EF Core 9 (SQLite / PostgreSQL), QuestPDF, ClosedXML, DocX, PdfPig, WebView2, Windows DPAPI.

---

## 🎯 1. أهداف التطوير (Objectives)

1. **إضافة وحدة "التخطيط الاستراتيجي وتحليل المعطيات" (Strategic Planning & Data Analysis)**:
   - لتكون المنطلق الهندسي والمنهجي الأول لبناء أي مشروع تنموي متماسك مبني على النتائج (Results-Based Management / Theory of Change).
   - توفير واجهة بـ 9 تبويبات متسلسلة بدقة تغطي: بطاقة المشروع، جمع المعطيات ومؤشرات خط الأساس، تحليل الأطراف المعنية (Power/Interest Matrix)، التحليل الاستراتيجي (SWOT, PESTEL, TOWS, تقييم الاحتياجات)، صياغة المشكلة المركزية ومصفوفة الخيارات، نظرية التغيير التفاعلية، الأهداف الاستراتيجية، سجل المخاطر والفرضيات، وبوابة الجاهزية.

2. **إعادة هيكلة مسار البرنامج وفق التتابع المنطقي التنموي (Logical Pipeline & Stepper)**:
   - إعادة ترتيب التنقل الرئيسي والمعالج (Stepper) في مساحة العمل إلى 9 مراحل أساسية متسلسلة:
     1. التخطيط الاستراتيجي وتحليل المعطيات (Strategic Planning) - الأساس.
     2. التأطير (Cadrage).
     3. شجرة المشاكل وشجرة الأهداف (Problem & Objective Tree).
     4. الإطار المنطقي (Cadre Logique).
     5. المخطط الزمني (Chronogramme / Gantt).
     6. الميزانية التقديرية (Budget).
     7. الكانفا ونماذج التمويل (Canvas & Donors).
     8. التعاون والمزامنة الجماعية (Collaboration & Sync).
     9. التصدير والتقارير النهائية (Export & Final Reports).
   - تزويد كل مرحلة بحالة واضحة: (غير مبدوءة، قيد العمل، مكتملة، تحتاج مراجعة Stale).
   - بوابات جاهزية (Readiness Gates) تفحص النواقص المانعة، مع إمكانية التجاوز الإداري المبرر والموثق في سجل التدقيق.

3. **محرك الارتباط التلقائي والتحليل الأثري (Propagation & Impact Analysis Engine)**:
   - خدمة مركزية `StrategicFoundationService` وسجل روابط `StrategicLink`.
   - تغذية كل مرحلة تلقائياً بما قبلها كمقترح (Draft) دون كتابة قسرية فوق التعديلات البشرية (Human-in-the-loop).
   - وسم العناصر المشتقة بحالة Stale عند تعديل الأصل، وحالة Overridden عند التعديل اليدوي المستقل.
   - روابط وتتبّع كامل (Traceability) وشارات "مشتق من..." للانتقال المباشر للمصدر.

4. **طبقة الذكاء الاصطناعي المهيكلة وفاحص الجودة الحتمي (Structured AI & Deterministic Quality Checker)**:
   - التحقق من الردود عبر JSON Schema مع آلية إصلاح تلقائية وعرض صريح للأخطاء.
   - عزل نصوص الوثائق وحمايتها من حقن الأوامر (Prompt Injection Protection).
   - فاحص جودة حتمي بدون ذكاء اصطناعي يفحص اتساق المنطق التنموي الرأسي والأفقي.

5. **المزامنة الآمنة وتصدير التقارير (Signed Sync & RTL Reporting)**:
   - توقيع المقترحات والأصوات بمفاتيح الأجهزة المتبادلة عبر الـ QR.
   - تصدير تقرير شامل للأساس الاستراتيجي بصيغتي Word وPDF مع دعم عربي وفرنسي كامل وتخطيط RTL دقيق.

---

## 🏗️ 2. خارطة المكونات والملفات المضافة والمعدلة

### 📁 أ. طبقة البيانات والنماذج (Models & EF Core)
- **ملفات كيانات جديدة (`Models/Strategic/`):**
  - `StrategicFoundation.cs`: الكيان الجذري للأساس الاستراتيجي المرتبط بالمشروع.
  - `DataSource.cs`: سجل مصادر البيانات والمستندات مع مؤشرات الموثوقية.
  - `ContextIndicator.cs`: مؤشرات خط الأساس السياقية (Baseline).
  - `StakeholderEntry.cs`: الأطراف المعنية مع درجات القوة والاهتمام واستراتيجية التعامل.
  - `SwotItem.cs`: عناصر مصفوفة SWOT مع تصنيفها وروابطها بالمعطيات.
  - `PestelItem.cs`: عناصر التحليل السياسي والاقتصادي والاجتماعي والتكنولوجي والبيئي والقانوني.
  - `NeedAssessment.cs`: مصفوفة تقييم الاحتياجات وترتيبها بالأوزان والمعايير المرجحة.
  - `StrategicOption.cs`: الخيارات الاستراتيجية ومصفوفة المفاضلة وتبرير القرار.
  - `TheoryOfChangeNode.cs`: عقد ومسارات نظرية التغيير (مدخلات، أنشطة، مخرجات، نتائج، أثر، والفرضيات).
  - `StrategicObjective.cs`: الأهداف الاستراتيجية العامة والنوعية بمعايير SMART وربطها بالاحتياجات.
  - `RiskEntry.cs`: سجل المخاطر المعمق (احتمال، أثر، درجة، تخفيف، مسؤول).
  - `AssumptionEntry.cs`: سجل الفرضيات الأساسية لكل مستوى من مستويات التغيير.
  - `StrategicLink.cs`: سجل تتبع الروابط التلقائية بين الوحدات وحالاتها (Synced, Stale, Overridden).
  - `AuditLogEntry.cs`: دعم سجلات التدقيق المتقدمة مع التجاوزات وحسابات الأثر.
- **تعديل `AppDbContext.cs`:**
  - إضافة الـ DbSets الجديدة.
  - دعم الحذف المنطقي (`IsDeleted`) لجميع الكيانات الاستراتيجية وسلة المحذوفات.
  - تضمين آلية نسخ احتياطي تلقائي لقاعدة البيانات (SQLite/Postgres) قبل الترحيل.
  - الترحيل الآمن للمشاريع القديمة بإنشاء `StrategicFoundation` افتراضي دون المساس ببياناتها القائمة.

### 📁 ب. طبقة الخدمات (Services)
- `Services/Strategic/StrategicFoundationService.cs`: المنطق الحسابي، توليد الحالات، التحقق من البوابات، وحساب نسب الاكتمال.
- `Services/Strategic/PropagationEngineService.cs`: محرك التغذية التلقائية عبر المراحل التسع مع التتبع والتحديث المتأني.
- `Services/Strategic/ImpactAnalysisService.cs`: حساب وتحليل الأثر عند تعديل أي عنصر استراتيجي وعرض العناصر التابعة قبل التثبيت.
- `Services/Strategic/ReadinessGateService.cs`: تقييم جاهزية كل مرحلة وقوائم النواقص المانعة وتسجيل التجاوزات.
- `Services/Strategic/DeterministicQualityChecker.cs`: فاحص الجودة الحتمي المستقل عن الذكاء الاصطناعي (منطق عمودي وأفقي).
- `Services/Strategic/StructuredAiPrompter.cs`: محرك الذكاء الاصطناعي المهيكل مع JSON Schema، فحص الردود، وعزل حقن الأوامر.
- `Services/ProjectContextBuilder.cs`: تحديثه ليبدأ قراءة السياق من الأساس الاستراتيجي كاملاً.
- `Services/DocumentExportService.cs`: إضافة تصدير تقرير التخطيط الاستراتيجي إلى Word وPDF بدعم RTL.
- `Services/Collab/MergeService.cs`: التحقق من توقيع مفتاح الجهاز على `ProposalDto` و `VoteDto`.

### 📁 ج. طبقة منطق العرض (ViewModels)
- `ViewModels/Strategic/StrategicPlanningViewModel.cs`: المتحكم الرئيسي لوحدة التخطيط الاستراتيجي بتبويباتها التسعة.
- `ViewModels/Strategic/ProjectStepperViewModel.cs`: المتحكم في شريط المراحل التسع والبوابات والجاهزية.
- `ViewModels/Strategic/ImpactAnalysisDialogViewModel.cs`: نافذة تأكيد التعديل وعرض شجرة الأثر المتسلسل.
- `ViewModels/ProjectWorkspaceViewModel.cs`: إعادة هيكلة التنقل لدعم المراحل التسع والشريط التفاعلي.

### 📁 د. طبقة واجهة المستخدم (Views)
- `Views/Strategic/StrategicPlanningView.xaml`: الواجهة الرئيسية بـ 9 تبويبات متناسقة.
- `Views/Strategic/ProjectStepperControl.xaml`: معالج المراحل التنموية التفاعلي مع إشارات الحالة والبوابات.
- `Views/Strategic/ImpactAnalysisDialog.xaml`: نافذة تفاعلية توضح العناصر المتأثرة قبل تثبيت التعديل.
- `Views/Strategic/TheoryOfChangeVisualView.xaml`: عرض بصري لنظرية التغيير.
- `Views/Strategic/StakeholderMatrixView.xaml`: مصفوفة القوة والاهتمام التفاعلية (Power/Interest Matrix).
- `Views/ProjectWorkspaceView.xaml`: إدراج الـ Stepper أعلى مساحة العمل وإعادة تنظيم القائمة الجانبية.

### 📁 هـ. طبقة الاختبارات الآلية (Tests)
- `AFMI_Montage_Projet.Tests/PropagationEngineTests.cs`: فحص الارتباط، إنشاء الروابط، علامات Stale، ووضع Overridden.
- `AFMI_Montage_Projet.Tests/MergeServiceConvergenceTests.cs`: اختبار تقارب الدمج والتوقيع الرقمي وترتيبات الوصول.
- `AFMI_Montage_Projet.Tests/StructuredAiParserTests.cs`: اختبار استجابات الذكاء الاصطناعي المهيكلة (سليمة، ناقصة، خاطئة).
- `AFMI_Montage_Projet.Tests/StrategicMigrationTests.cs`: اختبار ترقية قاعدة البيانات وتوافق المشاريع القديمة.
- `AFMI_Montage_Projet.Tests/EndToEndPipelineTests.cs`: اختبار مسار المشروع من الأساس حتى الكانفا والتصدير.

---

## ⏱️ 3. المراحل التنفيذية المتسلسلة

| المرحلة | المهام المنجزة | التحقق والاختبار | الحالة |
|:---:|---|---|:---:|
| **المرحلة 1** | قراءة المستودع، إعداد `docs/PLAN.md` و `docs/DECISIONS.md`. | مراجعة البنية والأنماط المعمارية. | ✅ مكتملة |
| **المرحلة 2** | إنشاء الكيانات الجديدة، النسخ الاحتياطي، تحديث `AppDbContext` والـ Migrations، الخدمات الأساسية. | اختبارات EF Core وترقية المشاريع القديمة. | ✅ مكتملة (51/51 اختبار) |
| **المرحلة 3** | بناء وحدة التخطيط الاستراتيجي (9 تبويبات) والـ ViewModels والـ Views بالكامل. | بناء الواجهات واختبار تشغيلها المستقل. | ✅ مكتملة |
| **المرحلة 4** | إعادة ترتيب التنقل الرئيسي والمعالج (Stepper) وبوابات الجاهزية. | اختبار التنقل، تدقيق البوابات، وتسجيل التجاوز. | ✅ مكتملة |
| **المرحلة 5** | ربط محرك الارتباط التلقائي (Propagation Engine) وحدة بوحدة وإدارة شارات Stale/Overridden. | اختبارات محرك الارتباط وتحليل الأثر. | ✅ مكتملة (56/56 اختبار) |
| **المرحلة 6** | طبقة الذكاء الاصطناعي المهيكلة (JSON Schema) وفاحص الجودة الحتمي. | اختبارات الـ Schema وعزل حقن الأوامر. | ✅ مكتملة (61/61 اختبار) |
| **المرحلة 7** | التوقيع الرقمي على المزامنة، تصدير التقارير (Word/PDF RTL)، تقرير `docs/REPORT.md`. | تشغيل كامل الاختبارات 100% وبناء الحزمة النهائية. | ✅ مكتملة (63/63 اختبار) |
