# سجل القرارات المعمارية والتقنية (Architectural Decision Records - ADR)
**مشروع:** AFMI Montage Projet  
**الإصدار:** v2.0.0

---

## 📋 القرار رقم 1: بيئة تتبع التغييرات ونظام التحكم في الإصدار (Git Availability)
* **السياق:** طُلب تنفيذ commits مستقلة لكل مرحلة، ولكن أمر `git` غير متاح في مسار البيئة الحالي لنظام التشغيل (غير مثبت في PATH).
* **القرار المتخذ:** توثيق كل مرحلة بوضوح ودقة في ملفات المشروع (`docs/PLAN.md`, `docs/DECISIONS.md`, و `docs/REPORT.md`) مع إنشاء نقاط فحص مرجعية وتوثيق الملفات المعدلة، لتكون متوافقة وجاهزة للإيداع الفوري عند إتاحة `git`.
* **البديل المرفوض:** محاولة تثبيت git بصلاحيات مرتفعة أو إيقاف التنفيذ لطلب تدخل المستخدم.
* **سبب الرفض:** يتعارض مع مبدأ التنفيذ الذاتي بموافقة مسبقة؛ البرمجيات والتطوير والبناء والاختبارات تعمل جميعها بكفاءة عبر `dotnet` دون الاعتماد على وجود أداة سطر أوامر خارجية.

---

## 📋 القرار رقم 2: نمط الحذف المنطقي وسلة المحذوفات للكيانات الاستراتيجية (Soft Delete)
* **السياق:** الكيانات الاستراتيجية تحتوي على روابط متسلسلة معقدة مع المؤشرات، الأنشطة، والمخاطر. الحذف الفيزيائي التسلسلي (Cascade Delete) قد يؤدي إلى فقدان بيانات غير مقصود للمستخدم.
* **القرار المتخذ:** اعتماد نمط الحذف المنطقي (`IsDeleted`, `DeletedAt`, `DeletedBy`) للكيانات الاستراتيجية الجديدة مع توفير إمكانية الاسترجاع (Restore) وتطبيق مرشح الاستعلام العام (`HasQueryFilter(e => !e.IsDeleted)`).
* **البديل المرفوض:** الإبقاء على الحذف الصارم التسلسلي `OnDelete(DeleteBehavior.Cascade)` لجميع الجداول الجديدة.
* **سبب الرفض:** فقدان البيانات الحساسة وصعوبة التراجع في بيئة عمل تشاركية متعددة المستخدمين.

---

## 📋 القرار رقم 3: استراتيجية ترقية قاعدة البيانات والتوافق مع المشاريع القديمة (Zero-Data-Loss Migration)
* **السياق:** يمتلك المستخدمون مشاريع قائمة بالفعل في قاعدة بيانات SQLite المحلية وقواعد PostgreSQL، ويجب ترقيتها دون فقدان أي حقل أو سجل.
* **القرار المتخذ:**
  1. تنفيذ نسخة احتياطية آلية لملف `projects.db` إلى مجلد `Backups/projects_backup_{timestamp}.db` قبل تشغيل أي أوامر ترقية هيكلية.
  2. استخدام نظام الترقية البرمجية الديناميكي المعمول به في المشروع (`ExecuteSqlRaw` مع `IF NOT EXISTS` و `ALTER TABLE ADD COLUMN`) لضمان عدم توقف البرنامج سواء في SQLite أو PostgreSQL.
  3. إنشاء دالة ترقية ذكية `EnsureStrategicFoundationForLegacyProjectsAsync` تُنشئ أساساً استراتيجياً فارغاً مربوطاً بكل مشروع قديم دون المساس بأهدافه أو ميزانيته القائمة.
* **البديل المرفوض:** استخدام أداة `dotnet ef migrations add` التي قد تفشل أو تعيد إنشاء الجداول من الصفر في بيئات الإنتاج غير المهيأة لـ EF CLI.
* **سبب الرفض:** خطر فقدان البيانات أو تعطل التثبيت على أجهزة المستخدمين النهائيين.

---

## 📋 القرار رقم 4: محرك الارتباط التلقائي (Propagation Engine) وسيادة القرار البشري (Human-in-the-Loop)
* **السياق:** عند انتقال المعطيات من التخطيط الاستراتيجي إلى التأطير أو الإطار المنطقي أو الميزانية، هل يتم الكتابة فوق الحقول تلقائياً؟
* **القرار المتخذ:** اعتماد مبدأ "الارتباط التلقائي مع الاعتماد البشري":
  - الحقول المشتقة تنشأ بحالة `Synced` أو تُعرض كمسودات مقترحة (Draft Proposals).
  - عند تعديل الأصل، يُوسم العنصر المشتق بحالة `Stale` (يحتاج مراجعة) مع إظهار نافذة مقارنة الفروق (Diff View).
  - إذا قام المستخدم بتعديل العنصر المشتق يدوياً، يتحول فوراً إلى حالة `Overridden` لمنع استبدال عمله البشري تلقائياً.
* **البديل المرفوض:** الكتابة الصامتة والتلقائية فوق حقول الوحدات اللاحقة (Silent Overwrite).
* **سبب الرفض:** فقدان تعديلات المستخدم اليدوية المخصصة وإحباط تجربة المستخدم.

---

## 📋 القرار رقم 5: بوابات الجاهزية وإمكانية التجاوز المبرر (Readiness Gates with Audit Override)
* **السياق:** تنظيم الانتقال بين مراحل دورة حياة المشروع التسع.
* **القرار المتخذ:** فحص اكتمال المتطلبات الأساسية لكل مرحلة عبر `ReadinessGateService`. إذا كانت هناك نواقص، تظهر شاشة تنبيه توضح قائمة النواقص المانعة، مع إتاحة زر "تجاوز إداري" (Administrative Override) يشترط كتابة سبب التجاوز ويقوم بتوثيقه فورياً في `AuditLog` مع هوية المستخدم والوقت.
* **البديل المرفوض:** الإغلاق الصلب (Hard Block) التام الذي يمنع المستخدم من التنقل حتى يملأ كل الحقول الإلزامية.
* **سبب الرفض:** تعطيل مرونة العمل أثناء ورشات التصميم السريعة أو استيراد مشاريع جاهزة جزئياً.

---

## 📋 القرار رقم 6: عزل حقن الأوامر في الذكاء الاصطناعي (Prompt Injection Defense)
* **السياق:** استخراج البيانات من وثائق ومصادر خارجية (Word/PDF/Excel) قد يحتوي على نصوص تحاول توجيه نموذج الذكاء الاصطناعي لتخطي التعليمات.
* **القرار المتخذ:** عزل نصوص الوثائق المرفوعة في أقسام صريحة موسومة كبيانات غير موثوقة (`<UNTRUSTED_DOCUMENT_DATA>`), وإلزام النموذج في التعليمات النظامية بعدم تنفيذ أي أوامر واردة داخلها، مع إلزام الإخراج بمخطط JSON محدد مسبقاً (JSON Schema Enforcement).
* **البديل المرفوض:** دمج نص الوثيقة مباشرة داخل الـ Prompt العام دون عزل أو وسوم أمان.
* **سبب الرفض:** ثغرة أمنية تسمح بتلاعب النصوص بالذكاء الاصطناعي وتوليد مخرجات غير منضبطة.

---

## 📋 القرار رقم 7: فاحص الجودة الحتمي (Deterministic Quality Checker)
* **السياق:** الحاجة للتحقق من الاتساق المنطقي للمشروع التنموي بدون الاعتماد على الذكاء الاصطناعي.
* **القرار المتخذ:** بناء محرك تحقق حتمي (`DeterministicQualityChecker`) يطبق القواعد الأكاديمية للإدارة بالنتائج (RBM):
  - وجود مؤشر SMART واحد على الأقل لكل نتيجة.
  - ارتباط كل نشاط بنتيجة واحدة وهدف محدد.
  - التحقق من الصياغة القابلة للقياس للأهداف.
  - اتساق السلسلة السببية الأفقية والعمودية.
* **البديل المرفوض:** استخدام وكيل ذكاء اصطناعي إضافي لفحص الجودة.
* **سبب الرفض:** التكلفة، البطء، عدم إمكانية العمل بدون إنترنت، وعدم موثوقية النتائج الحتمية للـ LLMs.

---

## 📋 القرار رقم 8: التوقيع الرقمي للأجهزة المقترنة (Signed Collaboration via HMAC-SHA256)
* **السياق:** ضمان عدم التلاعب في المقترحات والأصوات الصادرة من الهواتف أو الحواسيب الشريكة أثناء المزامنة عبر Google Drive أو الشبكة المحلية.
* **القرار المتخذ:** تزويد كل مقترح (`ProposalDto`) وكل صوت (`VoteDto`) بحقلي `DeviceId` و `DeviceSignature`. يتم حساب التوقيع بتشفير HMAC-SHA256 المعتمد على المفتاح السري المتبادل أثناء عملية الاقتران الأولي عبر رمز الاستجابة السريعة (QR Code). في حال عدم تطابق التوقيع، يُرفض الدمج ويُوثق كعملية تلاعب في سجل التدقيق.
* **البديل المرفوض:** قبول أي ملف JSON وارد في مجلدات المزامنة بمجرد صحة بنيته.
* **سبب الرفض:** إمكانية تعديل الملفات يدوياً أو حقن قيم غير مصرح بها من أجهزة مجهولة.

---

## 📋 القرار رقم 9: بنية وتنسيق تصدير تقارير التخطيط الاستراتيجي (Dual Word/PDF Export & RTL)
* **السياق:** حاجة المنظمات غير الحكومية والمؤسسات التنموية لتقديم وثائق احترافية للممولين بلغات متعددة (العربية، الفرنسية، الإنجليزية).
* **القرار المتخذ:** توفير تصدير متزامن للملفين:
  1. بصيغة Microsoft Word (`.docx`) عبر مكتبة `DocX` لتمكين الفرق من تحرير النصوص وتكييفها.
  2. بصيغة PDF عالية الدقة عبر محرك `QuestPDF` مع جداول ملونة، بطاقات معايير، ودعم محاذاة RTL والخطوط العربية.
* **البديل المرفوض:** الاكتفاء بطباعة الشاشة أو تصدير HTML بسيط.
* **سبب الرفض:** عدم مطابقة المعايير المؤسسية للهيئات المانحة والشركاء الدوليين.

---

## 📋 القرار رقم 10: توحيد لغة واجهة المستخدم والمكونات الاستراتيجية بالفرنسية (French UI Localization)
* **السياق:** طلب المستخدم تحويل كامل واجهة البرنامج ومكونات التخطيط الاستراتيجي والمعالج (Stepper) وبوابات الجاهزية إلى اللغة الفرنسية مع الحفاظ على التوافق التام مع البيانات والاختبارات الآلية.
* **القرار المتخذ:**
  1. تعريب كامل شاشات وعناصر التحكم (`StrategicPlanningView.xaml`, `ProjectStepperControl.xaml`, `ProjectStepperViewModel.cs`, `ProjectWorkspaceView.xaml`) إلى اللغة الفرنسية المتوافقة مع المصطلحات الدولية للـ RBM و GAR (Gestion Axée sur les Résultats).
  2. اعتماد `Tag` في عناصر الـ `ComboBox` للاحتفاظ بالقيم البرمجية والقيم القياسية (مثل `Strength`, `ActivityToOutput`, `Input`) وعرض النصوص الفرنسية الصريحة للمستخدم (`Force (Strength)`, `Activité ➔ Extrant`, `Intrant (Input)`).
  3. ترجمة كافة مخرجات بوابات النضج وفاحص الجودة الحتمي (`DeterministicQualityChecker.cs` و `ReadinessGateService.cs`) إلى الفرنسية مع الحفاظ على نجاح كافة الاختبارات الوحدوية (63/63 بنجاح).
* **البديل المرفوض:** تغيير أسماء الأعمدة في قاعدة البيانات ومفاتيح JSON الداخلية إلى الفرنسية.
* **سبب الرفض:** إمكانية كسر التوافق مع بيانات المشاريع القائمة ومخالفة سياسة عدم إتلاف البيانات (Zero Breaking Change).

---

## 📋 القرار رقم 11: المساعد التحريري الذكي الشامل لجميع مكونات وحقول النصوص (Universal AI Text Assistant)
* **السياق:** طلب المستخدم إضافة المساعدة بالذكاء الاصطناعي في كامل مكونات البرنامج مع خيارات واضحة لتحسين (Améliorer)، وإثراء (Enrichir)، وإعادة صياغة (Reformuler) النصوص والعبارات على مستوى كافة حقول الإدخال بدون استثناء.
* **القرار المتخذ:**
  1. **الالتقاط الشامل التلقائي (Global WPF Class Hook):** استخدام `EventManager.RegisterClassHandler(typeof(TextBoxBase), ...)` لربط القائمة السياقية (Right-Click Context Menu) واختصارات لوحة المفاتيح (`Ctrl+Alt+A`, `Ctrl+Shift+E`) بجميع عناصر النصوص (`TextBox`, `RichTextBox`) ديناميكياً ودون استثناء عبر كامل شاشات ومكونات التطبيق.
  2. **الخيارات التحريرية الموجهة للـ RBM:**
     - ✨ **Améliorer (تحسين):** ضبط الأسلوب المؤسسي، الدقة اللغوية، ومطابقة معايير إدارة المشاريع القائمة على النتائج (GAR/RBM) والمانحين الدوليين (AFD, UE, PNUD...).
     - 📝 **Enrichir (إثراء):** تطوير الأفكار وتوسيع الشروحات بالأدلة والحجج المنهجية والسياقية.
     - 🔄 **Reformuler (إعادة صياغة):** صياغة بديلة ديناميكية وموجزة وموجهة نحو الأثر.
     - 💡 **Suggérer / Générer (اقتراح / توليد):** صياغة مسودة متكاملة للحقل بناءً على السياق العام للمشروع عند فراغ الحقل.
     - ✍️ **Corriger (تصحيح):** تصحيح دقيق للإملاء والقواعد وعلامات الترقيم.
  3. **استهداف العبارات المحددة أو كامل النص:** تمييز تلقائي بين تحديد عبارة معينة داخل الحقل (`SelectedText`) أو معالجة كامل النص، مع استبدال مباشر في موضع التحديد.
  4. **واجهة مراجعة واعتماد بشري (Human-in-the-Loop Dialog):** نافذة `AiTextEnhancementDialog` تفاعلية تعرض النص الأصلي والبديل المقترح مع إمكانية التعديل واختيار النبرة ("Normes Bailleurs", "Concis", "Technique")، وأزرار "Remplacer dans le champ" أو "Insérer à la suite".
  5. **أزرار وصول مباشر وسريع:** تزويد الشاشات الاستراتيجية الرئيسية (`StrategicPlanningView.xaml`) بأشرطة وأزرار وصول سريع مدمجة بجانب الحقول الرئيسية.
* **البديل المرفوض:** وضع أزرار يدوية مكررة في كل شاشة فقط دون الالتقاط الشامل.
* **سبب الرفض:** عدم شمول كل الحقول وقوائم البيانات ومربعات الحوار وارتفاع كلفة الصيانة البرمجية. الاعتماد على `TextBoxBase` يضمن تغطية 100% من حقول البرنامج بدون استثناء.---

## 📋 Décision n° 12 : Assistant IA Rédactionnel Généralisé (Enrichir, Détailler, Reformuler) avec Garde-Fous de Fidélité et Historique Persistant
* **Contexte :** Généralisation de l'assistance IA de rédaction à tous les composants et zones de texte du logiciel, avec 3 actions standardisées (*Enrichir*, *Détailler*, *Reformuler*), des raccourcis claviers unifiés (`Ctrl+Maj+E`, `Ctrl+Maj+D`, `Ctrl+Maj+R`), une vue différentielle mot à mot, un contrôle strict des invariants chiffrés et des codes de structure, et la persistance de l'historique de versions par champ.
* **Décision prise :**
  1. **Actions Standardisées & Typage Sémantique RBM :**
     - **Enrichir :** Ajout de contenu pertinent tiré du contexte projet (sans invention de faits non documentés).
     - **Détailler :** Développement approfondi de l'écrit existant (étapes, explications, sous-points) à sens invariant.
     - **Reformuler :** Réécriture pour l'élégance, la clarté et le registre bailleur institutionnel à longueur équivalente.
     - Spécialisation selon le rôle du champ dans la chaîne de résultats (RBM) : Titre, Problème, Objectifs, Résultats (passé/atteint), Activités (infinitif), Indicateurs (SMART), Justifications budgétaires.
  2. **Garde-fous de Fidélité & Sortie Structurée (JSON) :**
     - Vérification stricte des invariants (nombres, dates, montants, codes OS1/R1.2/A1.1).
     - Interdiction stricte d'hallucinations de statistiques/sources : injection automatique de balises `[à compléter : …]`.
     - Retry automatique correctif en cas de non-respect, suivi d'un avertissement explicite dans l'interface en cas d'échec persistant (aucun échec silencieux).
     - Modèle de sortie JSON structuré avec métadonnées (`proposal`, `context_used`, `warnings`, `placeholders`).
  3. **Contexte Ciblé (`BuildTargetedContext`) :**
     - Extraction sélective des nœuds parents directs et plafonds financiers plutôt que l'injection brute de tout le projet, minimisant l'usage de jetons et la latence.
  4. **Persistance & Traçabilité Complète :**
     - Table `TextVersionHistory` pour sauvegarder et restaurer les versions antérieures d'un champ même après redémarrage.
     - Table `TextAssistAuditLog` pour mesurer l'adoption et la fidélité.
     - Table `TextAssistSettings` pour mémoriser les préférences (longueur, ton, langue).
  5. **Adorner Visuel & Couverture Universelle :**
     - Bouton Adorner flottant discret sur survol/focus, en plus du menu contextuel et des raccourcis clavier.
     - Système d'exclusion déclaratif (`IsExcluded`) pour mots de passe, clés secrètes, tokens et champs purement numériques.
     - Test de couverture automatisé analysant tous les fichiers `.xaml` pour vérifier que 100% des zones de texte sont couvertes ou documentées dans `docs/EXCLUSIONS_TEXT_ASSIST.md`.
* **Alternatives rejetées :**
  - *Alternative 1 :* Remplacement silencieux et direct du texte dans le contrôle sans boîte de dialogue de validation humaine.
    *Raison du rejet :* Risque de perte d'informations critiques et violation du principe "Human-in-the-Loop".
  - *Alternative 2 :* Injection complète et systématique de l'intégralité du projet dans chaque requête d'assistance.
    *Raison du rejet :* Dépassement de quota, latence élevée et risque accru d'hallucinations croisées entre sous-modules.

---

## 📋 Décision n° 13 : Exclusion Automatique Rigoureuse des Champs Techniques, Dates, Nombres et Contrôles Étroits (DatePicker & Numeric Isolation)
* **Contexte :** Lors de l'utilisation dans des formulaires comportant des `DatePicker`, le contrôle interne `DatePickerTextBox` (héritant de `TextBoxBase`) recevait automatiquement l'adorner `[✨ IA]`, provoquant une superposition visuelle gênante sur le texte de date (ex: `/2026`).
* **Décision prise :**
  1. **Détection Automatique par Arbre Visuel & Propriétés :** Implémentation de `AiTextAssistant.IsControlAutoExcluded` qui analyse la hiérarchie visuelle pour exclure automatiquement :
     - Les contrôles `DatePickerTextBox`, `DatePicker`, `Calendar` et tout champ associé à une sélection de date.
     - Les champs de mots de passe, clés secrètes, tokens et identifiants techniques (`PasswordBox`, propriétés contenant `Password`, `Secret`, `ApiKey`, `Token`).
     - Les champs numériques, monétaires ou de notation (`Budget`, `Amount`, `Total`, `Score`, `Quantity`, `Duration`, `Devise`, `Currency`).
  2. **Garde-fous Dimensionnels & Distinction Mono-ligne / Multi-ligne :**
     - Préservation stricte des zones rédactionnelles narratives même si leur titre contient un mot-clé (ex: `ProjectImpacts`, `Justification`, `GeneralObjective`) grâce au test `AcceptsReturn == true` ou `RichTextBox`.
     - Masquage automatique de l'adorner (`AiButtonAdorner`) si le champ a une largeur < 130px ou une hauteur < 22px, empêchant tout empiètement sur les champs de saisie étroits.
  3. **Exclusion Déclarative Prioritaire :** Prise en compte prioritaire de la propriété attachée `AiTextAssistant.IsExcluded="True"`.
* **Alternatives rejetées :**
  - *Alternative 1 :* Exclure manuellement chaque `DatePicker` un par un dans les dizaines de fichiers XAML du projet.
    *Raison du rejet :* Fragilité lors de l'ajout futur de nouveaux formulaires et risque d'oublis humains.
  - *Alternative 2 :* Désactiver l'adorner flottant et ne conserver que le clic droit.
    *Raison du rejet :* Dégradation de l'ergonomie et de la découvrabilité pour les utilisateurs néophytes.

---

## 📋 القرار رقم 14: اختبارات التوصيف وحفظ اللقطة المرجعية خارج شجرة البناء (Characterization Tests & External Baseline Snapshot)
* **السياق:** طُلب تنفيذ المشروع على فرع جديد وإجراء اختبارات توصيف قبل أي تعديل. أمر `git` غير متاح في PATH، ومحاولة إنشاء مجلد نسخ احتياطي داخلي تؤدي إلى تضمين ملفات الـ C# القديمة تلقائياً من قِبل مترجم .NET 9.
* **القرار المتخذ:**
  1. أخذ لقطة مرجعية كاملة وحفظها خارج مسار المشروع في مجلد البيانات الآمن `C:\Users\ROG\.gemini\antigravity\brain\cfa64427-67e4-4b49-a326-1025420c56ed\backup_baseline_v1.9.15\`.
  2. إنشاء فئة اختبارات التوصيف `CharacterizationTests.cs` لتثبيت سلوكيات إدارة المشاريع وتفكيك مخرجات الكاتب الذكي ومفتاح التفعيل، ونجاح كافة الاختبارات بنسبة 100% (105/105 اختبار).
* **البديل المرفوض:** وضع مجلد النسخ الاحتياطي داخل مسار المشروع مع تعديل ملف `.csproj` لإقصائه.
* **سبب الرفض:** ينتهك القيد الأعلى بالحفاظ على ملف `.csproj` دون أي تعديلات لا حاجة لها.

---

## 📋 القرار رقم 15: محلل الخلاصات عبر System.Xml.Linq المدمج دون مكتبات خارجية (Zero External Dependency RSS/Atom Parser)
* **السياق:** تتطلب وحدة الرادار دعم خلاصات RSS 2.0 و Atom مع التعامل مع الترميزات المتنوعة (UTF-8, ISO-8859-1, Windows-1256) والتواريخ غير القياسية وحماية SSRF.
* **القرار المتخذ:** بناء محلل خفيف ومرن باستخدام فئات `System.Xml.Linq.XDocument` و `System.Xml.XmlReader` المدمجة في .NET 9، مع معالجة صريحة لأسماء النطاقات (Namespaces) وعزل المحتوى وتنظيف وسوم HTML.
* **البديل المرفوض:** تثبيت مكتبات خارجية إضافية مثل `CodeHollow.FeedReader` أو حزم أخرى غير رسمية.
* **سبب الرفض:** الالتزام الصارم بالقيد الأعلى بعدم إضافة حزم خارجية إلا بمبرر حتمي، وقدرة مكتبات .NET 9 المدمجة على إنجاز التحليل بكفاءة وأمان أعلى.

---

## 📋 القرار رقم 16: نمط مفتاح التفعيل الحصين وعزل التعديلات (ProjectRadar.Enabled Pattern)
* **السياق:** يجب أن يختفي عنصر التنقل وزر الخلاصات في شريط الأدوات فور إيقاف مفتاح `ProjectRadar.Enabled` ليعود التطبيق إلى حالته السابقة تماماً.
* **القرار المتخذ:**
  1. تفعيل المفتاح افتراضياً عبر `SettingsService` مع استرجاع قيمته الحالية بصورة غير تزامنية وآمنة.
  2. تعريض الخاصية `IsProjectRadarEnabled` في كل من `ProjectWorkspaceViewModel` و `ProjectListViewModel`.
  3. ربط مرئية العناصر في XAML (`Visibility`) بمحول القيمة `BoolToVisConverter` المعتمد في المشروع.
* **البديل المرفوض:** حذف الشيفرة البرمجية برمجياً أو استخدام تجميعات شرطية `#if DEBUG`.
* **سبب الرفض:** ضمان إمكانية تفعيل وتعطيل الوحدة ديناميكياً أثناء تشغيل التطبيق دون الحاجة لإعادة ترجمة الملفات.

---

## 📋 القرار رقم 17: واجهة رادار المشاريع الذكي والربط المباشر مع كاتب المشاريع بالذكاء الاصطناعي (Radar UI, RTL/LTR & Ideation Bridge)
* **السياق:** توفير واجهة تفاعلية خفيفة وعصرية تتوافق مع هوية برنامج AFMI وتدعم العربية (RTL) والفرنسية (LTR)، مع تمكين المستخدم من تحويل أي فرصة تمويلية إلى مسار الكاتب الذكي للمشاريع (Agent Grant Writer) بضغطة زر واحدة.
* **القرار المتخذ:**
  1. **التبويبات الثلاثة المتكاملة:**
     - **تبويب الفرص المكتشفة:** شريط بحث مطبّع لغوياً، أزرار اقتراحات سريعة (Chips) خاصة بـ AFMI، مرشحات متعددة المعايير (Bailleurs, Axes, Score IA, Tri)، وبطاقات افتراضية (`VirtualizingStackPanel`) مع شارات تقييم ملونة (أخضر ≥8، برتقالي 5-7، رمادي <5)، وتنبيه بالمواعيد القريبة (<15 يوماً)، وتبديل فوري للملخص بين العربية والفرنسية.
     - **تبويب المحفظة (Grants Pipeline):** جدول تفاعلي لمتابعة الفرص المقبولة وحالاتها (Brouillon, Éligible, En rédaction, Soumis...).
     - **تبويب مصادر الخلاصات:** إدارة الخلاصات المؤسسية وتفعيلها/تعطيلها، وإضافة خلاصات جديدة مع زر الفحص الآمن ضد ثغرات SSRF، ونافذة مخصصة لضبط المحاور العشرة لـ AFMI.
  2. **جسر الربط مع الكاتب الذكي للمشاريع (`IdeationInnovation`):** إنشاء بوانٍ إضافية محملة مسبقاً (Overloaded Constructors) في `IdeationInnovationViewModel` و`IdeationInnovationWindow` لملء العنوان والشروط المرجعية ومصادر التمويل تلقائياً دون تعديل أو كسر أي كود قائم.
  3. **التكامل المزدوج للواجهة:**
     - إتاحة الرادار كصفحة عمل متكاملة داخل فضاء المشروع (`ProjectWorkspaceView`) عبر زر التنقل الجانبي.
     - إتاحة الرادار كنافذة مستقلة (`ProjectRadarWindow`) من شريط أدوات قائمة المشاريع الرئيسية (`ProjectListView`).
  4. **تقرير الفروق الآلي (Diff Audit):** صفر أسطر محذوفة أو مغيرة في الملفات القائمة (100% تعديلات إضافية فقط).
* **البديل المرفوض:** إعادة بناء واجهة الكاتب الذكي من الصفر أو تغيير معلمات الباني القديم.
* **سبب الرفض:** احترام القيد الأعلى بالحفاظ الصارم على الكود القائم وتجنب أي كسر للتوقيعات القديمة.

---

## 📋 القرار رقم 18: المساعد الذكي لأخذ الملاحظات التلقائية من النقاشات الصوتية (AI Voice Notes Assistant)
* **السياق:** طلب المستخدم إضافة ميزة مساعدة الذكاء الاصطناعي لأخذ الملاحظات تلقائياً من النقاشات والاجتماعات الصوتية ("prise de note automatique à partir des discutions vocales") في وحدة العمل التعاوني (`CollaborationView.xaml`).
* **القرار المتخذ:**
  1. **تسجيل صوتي أصيل وخفيف عبر Windows MCI (`winmm.dll`):** استخدام واجهة `mciSendString` المدمجة في ويندوز لالتقاط صوت الميكروفون المباشر بجودة 16kHz Mono دون أي مكتبات خارجية ثقيلة.
  2. **تكامل متقدم مع محركات الذكاء الاصطناعي:**
     - **Gemini Multimodal Audio:** إرسال الصوت المباشر إلى واجهة Gemini المتعددة الوسائط لمعالجة النقاشات متعددة اللغات (الفرنسية والعربية/الدارجة) وتوليد ملاحظات مهنية مهيكلة (المواضيع، النقاط الرئيسية، القرارات المتفق عليها، خطة الإجراءات).
     - **OpenAI Whisper:** دعم تفريغ الصوت عبر Whisper API ثم هيكلته بـ GPT عند تفعيل مفتاح OpenAI.
     - **محرك التعرف الصوتي المحلي (`System.Speech.Recognition`):** دعم محرك ويندوز المحلي للتعرف الصوتي كخيار احتياطي أوفلاين.
  3. **استيراد التسجيلات الخارجية:** إتاحة زر `📁 Importer audio` لتحميل تسجيلات الاجتماعات الخارجية (WAV, MP3, M4A, OGG, WebM) وتلخيصها فورياً.
  4. **واجهة تفاعلية في مساحة المسودة:** وضع شريط أدوات عصري أعلى مربع `📝 Prise de notes (Brouillon)` يتضمن توقيت التسجيل الحي وعداد الثواني ومؤشر الحالة مع إمكانية هيكلة الملاحظات وتفريغها بمرونة.
* **البديل المرفوض:** إجبار المستخدم على تثبيت مكتبات وسائط معقدة مثل NAudio أو الاعتماد الحصري على خدمات سحابية خارجية تتطلب اتصالاً دائماً.
* **سبب الرفض:** ضمان استقرار البرنامج والعمل دون أخطاء على مختلف بيئات ويندوز مع الحفاظ على الأداء والسرعة.

---

## 📋 القرار رقم 19: المساعد الذكي لإدارة وتحليل سجل المخاطر وتصحيح التراكب البصري (AI Risk Management & Matrix Audit)
* **السياق:** طلب المستخدم إضافة المساعدة بالذكاء الاصطناعي إلى قسم "⚠️ Registre des Risques & Matrice d'Atténuation" (`Views/RisquesView.xaml` / `ViewModels/RisquesViewModel.cs`)، مع وجود خلل بصري سابق في تراكب حقل "Rôle responsable" فوق حقل "Description du risque" في نفس صف الشبكة `Grid.Row="2"`.
* **القرار المتخذ:**
  1. **خدمة ذكية متخصصة `RiskAiService`:**
     - توليد مخاطر متكاملة مخصصة لسياق المشروع تغطي كافة الأصناف (Stratégique, Opérationnel, Financier, Environnemental, Social, etc.) مع تقدير تلقائي للاحتمالية (P) والأثر (I)، مقترح تخفيف عملي، وتحديد الدور المسؤول.
     - اقتراح خطط تخفيف ذكية (`SuggestMitigationAsync`) مخصصة للمخاطر مع تعيين المسؤوليات.
     - تقدير احتمالية وأثر المخاطر مع شرح مبرر (`EstimateScoreAsync`).
     - تدقيق شامل لمصفوفة المخاطر (`AuditRiskMatrixAsync`) وفق معايير المانحين الدوليين مع كشف المخاطر الحرجة غير المخففة والتوصيات التنفيذية.
  2. **واجهات تفاعلية مخصصة (`AiRiskGenerationDialog` و `AiRiskAuditDialog`):**
     - نافذة استعراض واختيار المخاطر المولدة بالذكاء الاصطناعي مع شارات ملونة لدرجة الحرجية وإمكانية الاستيراد الجماعي الانتقائي.
     - نافذة استعراض تقرير التدقيق الاحترافي مع ميزة النسخ المباشر.
  3. **حساب فوري لحرجية الخطر (Live Criticality Score & Dynamic Badge):**
     - إضافة خصائص مراقبة ديناميكية `CriticalityScore` (P × I)، و `CriticalityText`، و `CriticalityBrush` تتحدث فورياً عند تغيير قيَم الاحتمالية والأثر (أخضر منخفض ≤4، أصفر متوسط 5-9، برتقالي مرتفع 10-15، أحمر حرج 16-25).
  4. **إصلاح التراكب البصري في النموذج:**
     - فصل حقل "Rôle responsable" في الصف 2 (العمود 0)، ووضع شارة الحرجية المحسوبة حياً في الصف 2 (العمود 1).
     - نقل حقول الوصف وإجراءات التخفيف إلى الصف 3 مع تزويدها بأزرار الذكاء الاصطناعي السريعة (`✨ Suggérer`, `📝 Enrichir`, `🛡️ Proposer plan d'atténuation IA`, `🎲 Estimer P & I`).
* **البديل المرفوض:** الاكتفاء بمساعد النصوص العام الموضعي بدون فهم معماري لمصفوفة الاحتمالية والأثر أو دمج نافذة التدقيق الشامل.
* **سبب الرفض:** إدارة المخاطر في مشاريع المانحين تتطلب تماسكاً رياضياً (P × I) وتغطية تصنيفية دقيقة لا يمكن تحقيقها بالمساعدات النصية البسيطة.

---

## 📋 Décision n° 20 : Durcissement de l'infrastructure (journal, configuration, mises à jour)
* **Contexte :** application WinExe sans console : les `Console.WriteLine` et les `catch { }` vides rendaient toute erreur invisible. Le mot de passe PostgreSQL était stocké en clair dans `dbconfig.json` et saisi dans un `TextBox` exposé à l'assistant IA. Deux mécanismes de mise à jour coexistaient (`UpdateManager` avec URL fictive, créant des fenêtres hors du thread UI ; `AutoUpdaterService` avec une version codée en dur « 1.4 »).
* **Décision :**
  1. `Services/AppLogger.cs` : journal fichier quotidien (`%LOCALAPPDATA%\AFMI_Montage_Projet\logs`, rétention 14 jours) ; les gestionnaires globaux d'exceptions et les services d'infrastructure y écrivent.
  2. Mot de passe PostgreSQL chiffré DPAPI (`PasswordProtected`), migration automatique de l'ancien format en clair, saisie via `PasswordBox`.
  3. Version unique définie par `<Version>` dans le `.csproj` (alignée sur `MyAppVersion` de `setup.iss`, vérifiée par test) ; suppression de `UpdateManager`.
* **Alternative rejetée :** bibliothèque de journalisation externe (Serilog, NLog) — dépendance supplémentaire injustifiée pour ce besoin.

---

## 📋 Décision n° 21 : Mise à niveau du schéma par inspection des colonnes
* **Contexte :** `SeedDefaultData()` exécutait à chaque démarrage environ 600 `ALTER TABLE` dont presque tous échouaient (colonne existante) dans des `catch { }` vides : démarrage lent, vraies erreurs masquées. La base était copiée deux fois par démarrage, sans purge, y compris pendant les tests.
* **Décision :** `Models/AppDbContext.Schema.cs` fournit `AddColumnIfMissing` (inspection via `pragma_table_info` / `information_schema`), `RunSchemaStep` (erreurs journalisées) et une sauvegarde unique par processus du fichier SQLite effectivement utilisé, limitée aux 10 plus récentes. Les `CREATE TABLE` historiques (syntaxe SQLite) ne s'exécutent que sur SQLite. Le principe de l'ADR 3 (pas de migrations EF) est conservé.

---

## 📋 Décision n° 22 : Analyse globale et liaison logique entre les modules
* **Contexte :** le contrôle qualité déterministe et le moteur de propagation existaient et étaient testés, mais aucun écran n'utilisait le contrôle qualité. Le contexte fourni à tous les assistants IA (`ProjectContextBuilder.BuildFullContext`, ~40 appels) ignorait le cadre logique, le budget, le chronogramme et les risques. L'audit IA construisait son propre contexte partiel. La devise « DZD » était codée en dur.
* **Décision :**
  1. Règles de cohérence transversale dans `DeterministicQualityChecker` : socle ↔ arbre à problèmes (COH-01), budget ↔ activités (COH-02/03), budget détaillé ↔ cadrage (COH-04), budget ↔ plan de financement (COH-05), chronogramme (CHR-01/02/03), indicateurs (IND-01/02), risques (RSK-01/02), éléments à revoir après propagation (SYNC-01). Une constatation agrégée par règle.
  2. `ProjectAnalysisService` : point d'entrée unique agrégeant le score qualité et les 9 portes de maturité, utilisé par l'onglet Audit (diagnostic instantané hors-ligne) et par l'audit IA.
  3. L'audit IA reçoit le contexte complet + les constats déterministes comme base factuelle : l'IA explique, priorise et analyse le fond, sans recalculer ni contredire les constats.
  4. `BuildFullContext` inclut désormais le cadre logique, les indicateurs, le budget par catégorie, le plan de financement et les risques critiques (avec plafonds pour limiter les jetons), dans la devise du projet.
  5. Moteur de propagation branché sur l'interface (onglet « 🔗 10. Propagation » de la Planification Stratégique) : aperçu, sélection, application. Les résultats et activités issus de la théorie du changement sont désormais réellement créés (ils étaient ignorés silencieusement), sans doublon lors d'une nouvelle propagation. `RefreshLinkStatusesAsync` met en œuvre l'ADR 4 par comparaison d'empreintes : cible modifiée à la main → `Overridden` (prioritaire, protégée), source modifiée → `Stale`. Appelé à chaque enregistrement du socle et avant chaque analyse globale. Libellés traduits en français (ADR 10), devise du projet au lieu de « د.ج ».
* **Alternative rejetée :** confier toute l'analyse de cohérence à l'IA — non reproductible, coûteuse, indisponible hors-ligne et sujette aux hallucinations (cf. ADR 7).

---

## 📋 Décision n° 23 : Espace d'équipe — comptes Google individuels, version officielle, décision de l'administrateur
* **Contexte :** chaque membre devait pouvoir se connecter avec son propre compte Google, consulter les projets et contribuer, tout restant stocké sur le Drive du compte officiel `projet.afmi@gmail.com`. Or : (1) l'espace était cherché par nom dans « Mon Drive » du membre — le dossier partagé (dans « Partagés avec moi ») n'était jamais trouvé, un dossier vide était créé et le membre en devenait administrateur ; (2) aucun écran ne créait de proposition, `TeamManagementView` n'était affichée nulle part et `SyncService` n'était jamais démarré ; (3) les membres n'avaient aucune copie des projets officiels ; (4) tout membre pouvant écrire dans le dossier, il pouvait modifier `team.json` ou le statut de ses propres propositions ; (5) deux mécanismes parallèles (`GoogleDriveSyncService` désérialisant avec `TypeNameHandling.All` tout JSON du Drive, et le module Collab) coexistaient.
* **Décision :**
  1. **Identités et rôles** (`SecurityConstants`) : propriétaire de l'espace `projet.afmi@gmail.com` ; administrateurs `projet.afmi@gmail.com` et `said.afmi@gmail.com`, toujours administrateurs. Les autres rôles viennent de `team.json` (Member : propose ; Reviewer : lit ; Guest : compte non inscrit, lecture seule). Règle de décision `AdminDecision` : un administrateur approuve ou rejette, sans vote.
  2. **Espace d'équipe** : dossier `AssociationPM_Workspace` retrouvé par son propriétaire (`'projet.afmi@gmail.com' in owners`), qu'il soit dans « Mon Drive » ou « Partagés avec moi ». Seul le compte officiel le crée ; un membre non invité reçoit un message clair (`WorkspaceAccessException`).
  3. **Version officielle** : le poste connecté avec le compte officiel (« poste officiel », base SQLite locale) publie `_system/official/afmi_official.db`, copie `VACUUM INTO` dont toutes les tables propres au poste sont vidées (comptes, mots de passe, réglages, clés, journaux, propositions ; liste blanche dérivée du modèle EF dans `TeamDataModel`). Un poste membre remplace uniquement ses tables de contenu par cette version, après sauvegarde (`Backups\projects_before_team_sync_*.db`, 5 conservées) ; ses comptes et réglages sont conservés. La version reçue est gardée (`team\official_base.db`) comme référence.
  4. **Contributions** : le membre travaille normalement dans ses projets ; `TeamChangeDetector` compare sa base à la référence et produit modifications de champs et ajouts d'éléments (clés étrangères exprimées par GlobalId) ; il peut aussi envoyer une suggestion libre. Chaque contribution devient un fichier de proposition sur le Drive officiel. Une mise à jour depuis la version officielle est refusée tant que des modifications locales ne sont pas envoyées ou explicitement abandonnées.
  5. **Décision et intégration** (`MergeService.ApproveAsync` / `RejectAsync`) : sur le poste officiel, l'approbation est intégrée immédiatement (contrôle que la valeur officielle n'a pas changé depuis la proposition, sinon confirmation), puis la version officielle est republiée. Depuis un autre poste, un administrateur marque la proposition `Accepted` ; le poste officiel l'intègre à sa synchronisation suivante.
  6. **Confiance fondée sur Google Drive** : un fichier de proposition n'est accepté que si son auteur déclaré est le créateur du fichier ; une décision n'est retenue que si le fichier a été modifié en dernier par un administrateur ; `team.json` et la version officielle ne sont pris en compte que s'ils ont été écrits par un administrateur principal ; une décision locale définitive n'est jamais écrasée.
  7. Interface : fenêtre « 👥 Espace d'équipe » (liste des projets) — accès et version officielle, mes contributions, validation, équipe. Reconnexion silencieuse au démarrage si un jeton est mémorisé. Suppression de `TeamManagementView`, `GoogleDriveSyncService` et `SyncBackgroundService`.
  8. Les tests redirigent toutes les données locales vers un dossier temporaire (`AFMI_DATA_DIR`) : ils ne touchent plus la base réelle.
* **Alternatives rejetées :** (a) Shared Drive Google — exige Google Workspace payant ; (b) base PostgreSQL partagée — serveur à administrer, pas de circuit de validation ; (c) synchronisation entité par entité en JSON — fragile (identifiants entiers, dépendances), et la désérialisation typée de fichiers modifiables par tout membre est dangereuse.
* **Limites :** suppression d'éléments non proposée automatiquement (hors suppression logique des entités stratégiques) ; tant que l'application Google Cloud est en mode « Test », chaque membre doit y être déclaré comme utilisateur test (100 au maximum) et la reconnexion peut être demandée tous les 7 jours.

---

## 📋 Décision n° 24 : Fiabilisation du service RSS du Radar
* **Contexte :** vérification réelle le 29/10/2026 : 5 des 8 flux par défaut ne fonctionnaient plus (404, 403, connexion coupée). Une réponse HTTP 304 « non modifié » était comptée comme un échec (après deux essais) dès la deuxième actualisation. Les redirections étaient suivies sans limite ; l'adresse IP réellement contactée n'était pas contrôlée (contournement SSRF par « DNS rebinding » ou adresse IPv4 encapsulée dans IPv6). Le corps était toujours décodé en UTF-8, quel que soit l'encodage déclaré. Tout échec était réessayé, même un 404. La fenêtre « Ajouter un flux » utilisait son propre client HTTP, qui suivait les redirections sans contrôle. Le cache ne se purgeait jamais et ignorait les opportunités modifiées par la source. L'enregistrement de l'état des flux écrasait les flux ajoutés pendant une actualisation.
* **Décision :**
  1. **Réseau :** `SocketsHttpHandler.ConnectCallback` vérifie l'adresse IP effectivement contactée. Les redirections (301/302/303/307/308) sont suivies manuellement, au plus 5, et chaque saut est revalidé. Le corps est lu en octets, limité à 5 Mo. Un nouvel essai n'a lieu que sur erreur temporaire (réseau, 5xx, 408, 429). Délai maximal par flux : 45 s.
  2. **Requêtes conditionnelles :** ETag et Last-Modified sont conservés dans `rss_feeds.json`. Un 304 devient l'état « À jour ».
  3. **Santé des flux :** `LastSuccessAt`, `ConsecutiveFailures` et `LastError` sont suivis. Après 5 échecs consécutifs, le flux est suspendu 24 h. Les messages d'erreur sont explicites (404 : flux déplacé, 403 : lecteur bloqué, page web au lieu d'un flux).
  4. **Analyse :** encodage selon le BOM, puis la déclaration XML, puis l'en-tête HTTP, puis UTF-8. Les liens relatifs sont résolus (y compris `xml:base`). Atom privilégie le lien `rel="alternate"`. Un `guid` permalien remplace un `<link>` absent. Les fuseaux en abréviation (EST, CEST…) sont reconnus. Titre, description et identifiant sont plafonnés. Le titre du flux est extrait.
  5. **Découverte automatique :** une adresse de page web est acceptée. Les flux annoncés par `<link rel="alternate">` sont essayés (`TestFeedAsync`, utilisé par la fenêtre d'ajout).
  6. **Cache :** une opportunité modifiée par la source (date limite prolongée…) est mise à jour et marquée à réévaluer, sans perdre son état lu/masqué/pipeline. Purge des éléments publiés depuis plus de 365 jours, hors pipeline.
  7. **Configuration :** l'état des flux est fusionné dans la configuration courante (`UpdateFeedStatuses`). Révision 2 des flux par défaut : adresses GEF et FAO corrigées ; PNUD Maroc, Banque mondiale et CEPF désactivés faute de flux public ; ajout du Fonds pour l'adaptation et d'ONU Info Climat. Les copies existantes sont corrigées au chargement (`MigrateDefaultFeeds`), sauf adresse modifiée par l'utilisateur. Les fichiers de configuration du Radar suivent aussi la redirection `AFMI_DATA_DIR` des tests.
* **Alternative rejetée :** bibliothèque de syndication externe (`System.ServiceModel.Syndication`, CodeHollow.FeedReader) — contraire à l'ADR 15 et inutile : les manques étaient dans la couche réseau et la gestion d'état, pas dans le format.

---

## 📋 Décision n° 25 : Enchaînement des étapes, territoire et bénéficiaires, exploitation des documents et des projets similaires
* **Contexte (audit du 29/10/2026) :**
  - L'option « recherche Internet » n'atteignait jamais le moteur : `AiAssistantService.CallAiAsync` et `AiRouterService` l'ignoraient.
  - Le stepper ne contrôlait que l'étape immédiatement précédente : une étape pouvait être sautée sans alerte (budget sans cadre logique…).
  - La zone d'intervention et le public cible existaient en double (cadrage `InterventionZone` / socle `TargetZone`), sans contrôle. Il en allait de même pour la devise et la durée.
  - L'analyse de liens utilisait un client HTTP sans protection SSRF et gardait le JavaScript dans le texte. Elle produisait un Markdown libre, inexploitable par les étapes suivantes et non vérifié. La synthèse des exigences était refusionnée par l'IA à chaque document, avec risque de perte.
  - Il n'existait ni recherche de projets similaires, ni description du territoire et des bénéficiaires.
* **Décision :**
  1. **Recherche Internet réelle :** l'option est transmise jusqu'au moteur, et `RoutingResult.SearchUsed` l'atteste. `StructuredAiPrompter` accepte `enableSearch` (premier essai seulement). Les réponses Gemini en plusieurs parties sont concaténées.
  2. **Territoire & bénéficiaires** (`TerritoryProfile`, `BeneficiaryGroup`, onglet « 🗺️ 2. Territoire & Bénéficiaires » de la Planification stratégique) :
     - c'est la **source unique** : `Project.InterventionZone`, `StrategicFoundation.TargetZone` et `TargetBeneficiaries` en sont dérivés à chaque enregistrement (`TerritoryService.SynchronizeAsync`, aussi appelé à l'enregistrement du socle) et ne sont plus saisis dans la fiche ;
     - les groupes directs sont liés aux parties prenantes (`LinkedStakeholderId`, sans doublon) ;
     - les besoins des groupes et les problèmes de la zone alimentent l'analyse des besoins ;
     - l'assistance IA ne remplit que les rubriques vides ;
     - le territoire et les bénéficiaires figurent dans le contexte IA commun et dans les exports Word/PDF.
  3. **Documents et liens** (`Services/Research`) :
     - accès web commun sécurisé `SafeHttpFetcher` (partagé avec le service RSS) ; les PDF et Word en ligne sont acceptés ;
     - extraction HTML sans scripts ni menus ; sélection des passages informatifs pour les longs documents ;
     - analyse structurée `DocumentInsights` (nature, exigences de l'appel, données chiffrées, problèmes, besoins, bénéficiaires…) avec isolation anti-injection ;
     - **vérification des chiffres dans la source** : une donnée non retrouvée est signalée et décochée par défaut ;
     - **repli sans IA** : extraction des montants, durées, cofinancement et date limite ;
     - le texte et l'analyse sont conservés (`ExtractedText`, `InsightsJson`, empreinte du contenu) ;
     - la synthèse des exigences est reconstruite de façon déterministe à partir de toutes les analyses.
  4. **Intégration au projet** (`ProjectIntegrationService`) : chaque information cochée va à l'endroit où l'étape suivante l'exploite :
     - source de données, indicateur de contexte, donnée probante du problème, besoin, SWOT, bailleur et priorités, plafond budgétaire, groupe de bénéficiaires, territoire ;
     - pour les projets similaires : activité de la théorie du changement (ensuite propageable vers le cadre logique), risque, option stratégique, partenaire.
     - Aucun doublon : la comparaison est normalisée (accents, ponctuation, inclusion).
  5. **Projets similaires** (`SimilarProjectsService`) :
     - API Projets de la Banque mondiale et IATI via d-portal (projets UE, AFD, GIZ, PNUD, FEM…), sans clé ;
     - recherche web IA complémentaire (résultats marqués « à vérifier ») ;
     - mots-clés traduits depuis le projet, pertinence calculée ;
     - idées extraites par IA : leçons, activités, indicateurs, risques, partenaires, approches.
  6. **Cohérence entre étapes** :
     - le stepper contrôle toutes les étapes précédentes ;
     - la porte « Planification stratégique » exige la zone et au moins un groupe de bénéficiaires directs ;
     - nouvelles règles déterministes : COH-06 (zone cadrage ≠ territoire), COH-07 (devises), COH-08 (durée), DUP-01 (doublons du cadre logique), TER-01/02, BEN-01/02/03 ;
     - règles DOC-01 à DOC-04 : montant demandé vs plafond ou minimum de l'appel, durée maximale, date limite, cofinancement exigé ;
     - SEQ-01 : étape non démarrée alors qu'une étape postérieure est avancée.
  7. **Schéma :** `EnsureModelTablesExist` crée, à partir du script DDL du fournisseur actif, les tables du modèle absentes d'une base existante (SQLite et PostgreSQL), ce que `EnsureCreated` ne fait pas.
* **Alternatives rejetées :**
  - (a) Moteur de recherche web par « scraping » (DuckDuckGo, Google) : fragile et contraire aux conditions d'utilisation. Les bases publiques documentées et la recherche Gemini officielle sont préférées.
  - (b) Laisser la zone et le public cible saisissables à deux endroits avec une simple alerte : les conflits réapparaîtraient.
  - (c) Intégration automatique de tout ce que l'IA extrait : sans validation humaine, des chiffres non vérifiés entreraient dans le dossier.

---

## 📋 Décision n° 26 : Langue des réponses IA, traduction universelle, installateur et mises à jour GitHub
* **Contexte :**
  - Les modèles (surtout locaux, ex. Ollama `gpt-oss`) répondent souvent dans une autre langue que celle du texte traité. L'assistant de rédaction n'imposait aucune langue en mode « Auto », et son option « Même langue (Auto) » était transmise telle quelle comme nom de langue.
  - L'assistant IA ne couvrait que les champs modifiables : les réponses IA affichées (champs en lecture seule, TextBlock) n'étaient ni copiables ni traduisibles.
  - La mise à jour par Google Drive lançait, sans vérification, un installateur déposé dans l'espace d'équipe, où tout membre a les droits d'écriture (ADR 23).
  - `setup.iss`, en UTF-8 sans BOM, faisait afficher « ForÃªt » par Inno Setup.
* **Décision :**
  1. **Langue des réponses** : `AiRouterService` (passage obligé de tous les appels IA) résout une langue attendue, dans cet ordre :
     - celle de l'appelant ;
     - une langue explicitement demandée dans la consigne (« en arabe »…) ; bilingue ou traduction : pas de contrôle ;
     - le réglage `AI_ResponseLanguage` (Paramètres › Apparence) ;
     - à défaut, la langue dominante de la demande.

     La consigne de langue est ajoutée au message système. `LanguageGuard` (détection sans IA : écriture arabe, mots outils français/anglais) contrôle la réponse : si elle est en prose dans une autre langue, elle est traduite automatiquement. Les réponses JSON ne sont pas contrôlées, car leurs champs peuvent être bilingues. Les assistants de rédaction imposent la langue du texte d'origine.
  2. **Traduction** (`TranslationService`, `TranslationWindow`) :
     - français ⇄ arabe ⇄ anglais, découpage par paragraphes, mise en forme et terminologie des bailleurs conservées ;
     - contrôle que tous les chiffres sont repris (chiffres arabo-indiens normalisés) et que la langue cible est atteinte (un nouvel essai) ;
     - accessible par le menu contextuel et Ctrl+Maj+T dans tous les champs non exclus (y compris en lecture seule), et par clic droit sur tout texte affiché d'au moins 25 caractères.
     - Seuls les 18 champs techniques ou secrets documentés (`EXCLUSIONS_TEXT_ASSIST.md`) restent exclus.
  3. **Mises à jour GitHub** (`GitHubUpdateService`, `UpdateWindow`) :
     - au démarrage, la dernière « Release » du dépôt configuré est comparée à la version installée ;
     - l'installateur (`AFMIMontageProjet_Setup_vX.Y.Z.exe`, uniquement depuis github.com) est téléchargé par `SafeHttpFetcher.DownloadToFileAsync` ;
     - son SHA-256 est vérifié contre l'empreinte publiée (`digest` GitHub ou fichier `.sha256`) : installation refusée sans empreinte ou si elle diffère ;
     - lancement silencieux d'Inno Setup, qui ferme puis relance l'application (`AppMutex`, `CloseApplications`, `RestartApplications`).
     - Le dépôt vient du réglage `Update_GitHubRepository`, sinon de `Config/update.json`, renseigné automatiquement par la chaîne GitHub Actions.
     - Le canal Google Drive n'est plus consulté quand un dépôt est configuré ; il n'accepte plus qu'un `version.json` et un installateur déposés par un administrateur principal (attestation Drive « lastModifyingUser »).
  4. **Publication** :
     - `.github/workflows/release.yml` : à l'envoi d'une étiquette `vX.Y.Z`, il vérifie la cohérence des versions, lance les tests, publie en autonome win-x64, compile Inno Setup, calcule l'empreinte et crée la Release ;
     - `build_and_sign.ps1` : produit le même installateur localement (paramètre `-GitHubRepository`, fichier `.sha256`) ;
     - `setup.iss` est enregistré en UTF-8 avec BOM.
  5. **Dépôt privé** (`saidsocial5-droid/afmi-montage-projet`, à la demande de l'utilisateur) :
     - chaque poste reçoit d'un administrateur une clé d'accès GitHub « fine-grained » limitée au dépôt, avec la seule permission Contents en lecture ;
     - la clé est chiffrée DPAPI dans `AppSettings` (`Update_GitHubTokenProtected`), une table propre au poste, jamais publiée avec l'équipe ni intégrée à l'installateur, car une clé intégrée serait extractible par quiconque possède le fichier ;
     - la clé n'est envoyée qu'à api.github.com : les fichiers passent par l'API des assets (`application/octet-stream`), et `SafeHttpFetcher` n'envoie les en-têtes qu'à l'hôte d'origine, jamais après une redirection vers le stockage GitHub.
     - Alternative écartée : un second dépôt public réservé aux installateurs, qui rendrait l'application téléchargeable par tous.
* **Limites :** l'installateur n'est pas signé (pas de certificat de signature de code) : Windows SmartScreen affiche un avertissement au premier lancement. L'API GitHub non authentifiée est limitée à 60 requêtes par heure et par poste (une vérification par démarrage).
* **Alternatives rejetées :**
  - (a) Squirrel / Velopack / ClickOnce : ce serait une dépendance supplémentaire et une refonte de l'installation existante (Inno Setup, ADR 14).
  - (b) Traduction systématique de toutes les réponses : coût et latence doublés ; le contrôle de langue ne traduit que lorsque c'est nécessaire.

---

## 📋 Décision n° 27 : Fiabilité des replis IA, culture de formatage, consentement cloud des notes vocales (1.9.21)
* **Contexte (audit du 06/11/2026) :**
  - La publication GitHub Actions de la v1.9.20 s'arrêtait à l'étape Tests (260/264) : trois tests appelaient le vrai Ollama, absent du serveur, et un test dépendait de la langue de Windows (« 1 200 » / « 1,200 »).
  - `LocalAiEngine` renvoyait ses erreurs (« **[Générateur Local Embarqué]** … Ollama n'est pas actif ») comme une réponse normale : le routeur comptait l'appel comme réussi, les replis sans IA ne s'activaient jamais et le message d'erreur arrivait comme « proposition » dans les champs.
  - Les nombres formatés (`{n:N0}`) suivaient la langue de Windows, y compris dans les textes enregistrés et les dossiers exportés.
  - `VoiceNoteAssistantService` envoyait l'audio des réunions à Gemini ou Whisper dès qu'une clé existait, sans tenir compte de `UseLocalByDefault`, `CloudOptInGranted`, du plafond mensuel ni de `AiLog`.
  - `ProfileSourceAnalysisService` téléchargeait les pages avec son propre `HttpClient` (pas de contrôle SSRF à la connexion ni aux redirections, pas de limite de taille) ; la clé Gemini passait dans l'URL ; le canal de mise à jour Drive lançait l'installateur sans empreinte.
* **Décision :**
  1. **Échec = échec** : `LocalAiEngine` lève `LocalAiUnavailableException`. Le routeur renvoie alors `IsSuccess = false`, `ResponseText` vide et la cause dans `RoutingResult.ErrorMessage` (jamais un message d'erreur déguisé en réponse). `AiAssistantService.CallAiAsync` lève une exception (les appelants l'affichent ou passent à leur repli), `StructuredAiPrompter` s'arrête sans « réparer » un JSON vide, et le chat et la synthèse de réunion affichent la cause.
  2. **Culture de formatage unique** : `Helpers/AppCulture` fixe fr-FR (`DefaultThreadCurrentCulture`) au démarrage et dans les tests. La langue d'affichage et la propriété `Language` de WPF ne changent pas, donc la saisie des nombres dans les champs liés reste identique.
  3. **Consentement cloud partagé** : `AiRouterService.IsCloudUseAllowed` (cloud préféré, consentement, plafond) s'applique aussi à l'audio des notes vocales ; sans consentement, l'audio reste sur le poste (dictée Windows locale). Ces envois sont journalisés dans `AiLog` (`AiRouterService.LogExternalCall`) et comptent dans le plafond.
  4. **Réseau et secrets** : `ProfileSourceAnalysisService` passe par `SafeHttpFetcher` ; la clé Gemini est envoyée dans l'en-tête `x-goog-api-key` ; `version.json` (canal Drive) porte le SHA-256 de l'installateur, calculé à la publication et vérifié avant tout lancement (installateur refusé sans empreinte ou si elle diffère).
  5. **Tests déterministes** : `LocalSettingsService` suit `AFMI_DATA_DIR` comme la base locale ; `TestEnvironment` y fait pointer Ollama vers un port fermé (`127.0.0.1:9`), si bien qu'aucun test n'appelle une IA réelle, avec ou sans Ollama sur le poste.
  6. **Journal** : les `catch { }` vides et les `Console.WriteLine` restants passent par `AppLogger` ; la monnaie par défaut n'est définie qu'à un endroit (`Project.DefaultCurrency`, `Project.CurrencyOf`).
* **Conséquence :** un `version.json` Drive publié avant la 1.9.21 (sans empreinte) est refusé : republier la mise à jour depuis Paramètres › Cloud. Le canal GitHub, prioritaire, n'est pas concerné.
* **Alternatives rejetées :**
  - (a) Détecter les messages d'erreur d'Ollama par leur texte : fragile et déjà source du défaut.
  - (b) Forcer aussi la `Language` WPF en fr-FR : cela changerait le séparateur décimal attendu dans tous les champs numériques liés.

---

## 📋 Décision n° 28 : Base de veille des appels à projets, notation /25 (lot 1 de l'agent de veille MCP)
* **Contexte :** l'AFMI a validé une méthode de veille (passages du 06 au 10/11/2026) pour l'agent MCP (Montage et conception de projets), en 7 blocs : collecte, pré-filtrage, vérification sur la page officielle, notation /25, base permanente, fiches d'opportunité, synthèse. Le Radar faisait déjà la collecte RSS et une note IA sur 10, mais n'avait ni base permanente (À venir, Vus et non retenus, couverture par axe), ni notation /25, ni statut de confirmation, ni contrôle des échéances. Ses 5 axes (`afmi_axes.json`) sont ceux du plan stratégique, pas les 10 axes de veille de la méthode.
* **Décision (lot 1, sans IA ni réseau) :**
  1. **Axes** : les 10 axes de veille A1 à A10 sont définis dans le code (`Services/Radar/VeilleAxes`), avec mots-clés FR/EN/AR et détection par mot entier (« eau » ne reconnaît pas « réseau »). Les 5 axes stratégiques restent inchangés pour le profil et la formulation.
  2. **Notation** (`VeilleScoringService`) : 5 critères de 0 à 5 (pertinence, éligibilité, intérêt financier, faisabilité, valeur stratégique), chacun justifié ; A ≥ 20, B 14-19, C < 14 ; total plafonné à 14 tant que l'appel n'est pas « confirmé » sur la page officielle. Règle du bureau : aucun plafond de montant, un montant élevé n'abaisse jamais la note et déclenche seulement un conseil de consortium. Le premier jet calculé est corrigeable dans le Pipeline, puis recalculé.
  3. **Base permanente** (`VeilleBaseService`) : APPELS = `grants_pipeline` (nouvelles colonnes : identifiant V-AAAA-NNN, priorité, 5 notes, confirmation, date de vérification, 13 champs de la fiche) ; A_VENIR = `veille_a_venir` (AV-NNN) ; VUS_NON_RETENUS = `veille_non_retenus` (motif obligatoire). Dédoublonnage par bailleur + intitulé normalisé (sans accents ni année) + date limite ; un doublon complète la fiche existante au lieu d'être recréé. Pré-filtrage : zone excluant le Maroc, hors axes, associations non éligibles, clos (avec suivi du prochain cycle si l'appel est récurrent).
  4. **Statuts** : Nouveau → En cours / En rédaction → Soumis → Clos / Écarté / Trop tard ; « Mise sous veille » devient « Nouveau » à la mise à jour du schéma. Chaque changement est daté dans les notes. Contrôle des échéances au chargement du Radar et sur bouton : DL dépassée → Clos ; DL < 10 jours et appel non engagé → Trop tard ; non confirmé depuis plus de 7 jours avec DL < 15 jours → Trop tard.
  5. **Interface** : le Pipeline montre ID, priorité (lignes vertes/jaunes/grises), score, axes et confirmation, avec un panneau de notation et de statut ; un onglet « Base de veille » montre la couverture par axe (axe le moins couvert), les appels à venir (saisie, bascule « Ouvert → Appels ») et les appels vus non retenus.
* **Hors lot 1 :** lot 2 = nouvelles sources (agrégateurs), passage complet le lundi / rapide les autres jours, vérification sur la page officielle et prompt système de l'agent (bloc 0) via `AiRouterService` ; lot 3 = fiches d'opportunité, synthèse en 5 lignes, export .xlsx en 5 onglets, alerte.
* **Alternatives rejetées :**
  - (a) Remplacer les 5 axes stratégiques par les 10 axes de veille : le profil, la formulation et les tests reposent sur les 5 axes officiels du plan stratégique.
  - (b) Laisser l'IA noter sur 25 : la méthode exige des règles vérifiables (plafond, seuils, « trop tard ») ; l'IA pourra proposer des notes au lot 2, que ces règles encadreront.

---

## 📋 Décision n° 29 : Mise à jour automatique : installation à la fermeture et invitation à saisir la clé d'accès (1.9.23)
* **Contexte :** depuis la 1.9.21, les versions sont publiées automatiquement sur GitHub, mais elles n'arrivaient pas toutes seules sur les postes :
  - le dépôt est privé : sans clé d'accès, la vérification au démarrage échouait en silence (simple ligne dans le journal) ;
  - le champ de la clé, dans les Paramètres, n'était visible que pour un super-administrateur, alors que chaque poste a besoin de sa propre clé ;
  - une nouvelle version demandait toujours une confirmation, et l'installation immédiate fermait le logiciel en plein travail.
* **Décision :**
  1. **Clé manquante ou refusée** (`UpdateCheckResult.NeedsAccessToken`, réponses 401/404 de GitHub) : au démarrage, `GitHubTokenWindow` explique comment créer la clé (lien direct vers GitHub, dépôt unique, Contents en lecture seule), la reçoit dans un `PasswordBox`, la chiffre (DPAPI) et la teste aussitôt. L'invitation revient à chaque démarrage tant que la clé manque, sauf si l'utilisateur coche « Ne plus me le demander sur ce poste » (`Update_TokenPromptDisabled`). « Rechercher une mise à jour » l'affiche toujours.
  2. **Clé saisissable par tout utilisateur** dans Paramètres › Mises à jour. Le dépôt reste réservé aux super-administrateurs.
  3. **Installation automatique** (`AutoUpdateCoordinator`, réglage `Update_AutoInstall`, activé par défaut) : au démarrage, la nouvelle version est téléchargée et vérifiée (SHA-256) en arrière-plan. L'utilisateur peut l'installer tout de suite (fermeture puis relance) ; sinon elle s'installe en silence à la fermeture (`App.OnExit`, après libération du mutex, arguments `/SILENT /NORESTARTAPPLICATIONS`). L'empreinte est recontrôlée juste avant le lancement (fichier dans %TEMP%), et un seul installateur est lancé par session. En cas d'échec du téléchargement, la fenêtre de proposition habituelle s'affiche.
  4. Réglage désactivé : comportement de la 1.9.22 (fenêtre « Mise à jour disponible » à chaque nouvelle version).
* **Limite :** si le logiciel est installé pour tous les utilisateurs, Windows demande une confirmation administrateur (UAC) au lancement de l'installateur, y compris à la fermeture.
* **Alternatives rejetées :**
  - (a) Clé unique intégrée à l'installateur ou distribuée par l'espace d'équipe : extractible par quiconque possède le fichier (ADR 26).
  - (b) Rendre le dépôt public : le code de l'association deviendrait accessible à tous.
  - (c) Installation silencieuse immédiate au démarrage : elle fermerait le logiciel pendant que l'utilisateur commence à travailler.
