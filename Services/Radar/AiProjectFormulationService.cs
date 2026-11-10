using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Models.Radar;

namespace AFMI_Montage_projet_2.Services.Radar
{
    /// <summary>
    /// خدمة صياغة مقترح المشروع بالذكاء الاصطناعي من فرصة تمويلية:
    /// تقوم بإنشاء مشروع جديد متكامل يمر عبر كافة مراحل الصياغة في البرنامج:
    /// 1. التأطير والتشخيص (Fiche de cadrage, Contexte, Objectif général)
    /// 2. شجرة المشاكل والأسباب والآثار (Arbre de problèmes)
    /// 3. الإطار المنطقي والإدارة بالنتائج (Cadre logique: OS, Résultats, Activités, Indicateurs)
    /// 4. سجل إدارة المخاطر (Gestion des risques)
    /// 5. الميزانية التقديرية التحليلية (Budget prévisionnel)
    /// </summary>
    public class AiProjectFormulationService
    {
        private readonly Func<AppDbContext> _dbContextFactory;

        public AiProjectFormulationService(Func<AppDbContext>? dbContextFactory = null)
        {
            _dbContextFactory = dbContextFactory ?? (() => new AppDbContext());
        }

        public async Task<Project> FormulateProjectFromOpportunityAsync(
            RssItemCache item,
            Action<string>? progressCallback = null)
        {
            progressCallback?.Invoke("تحليل بيانات الفرصة التمويلية وصياغة مقترح المشروع بالذكاء الاصطناعي...");

            // 1. Détermination des paramètres de base
            decimal budgetTotal = item.AiEstimatedBudget ?? 100000m;
            DateTime startDate = DateTime.Today;
            DateTime endDate = item.AiDeadline.HasValue && item.AiDeadline.Value > DateTime.Today
                ? item.AiDeadline.Value.AddYears(1)
                : DateTime.Today.AddYears(2);

            string initialTitle = item.Title;
            if (!initialTitle.StartsWith("Projet ", StringComparison.OrdinalIgnoreCase) &&
                !initialTitle.StartsWith("مشروع ", StringComparison.OrdinalIgnoreCase))
            {
                initialTitle = $"Projet d'Appui - {item.Title}";
            }

            // 2. Génération IA ou Repli Heuristique
            GeneratedProjectBlueprint blueprint;
            try
            {
                progressCallback?.Invoke("Génération du cadre logique et des étapes méthodologiques via l'IA...");
                blueprint = await GenerateBlueprintViaAiAsync(item, budgetTotal);
                if (!IsCompleteBlueprint(blueprint))
                {
                    // Cadre logique incomplet (objectif sans résultat, résultat sans activité…) : l'étape suivante n'aurait rien à décliner
                    progressCallback?.Invoke("Proposition IA incomplète : utilisation du modèle méthodologique complet.");
                    blueprint = GenerateFallbackBlueprint(item, budgetTotal);
                }
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Repli heuristique méthodologique ({ex.Message})...");
                blueprint = GenerateFallbackBlueprint(item, budgetTotal);
            }

            // 3. Persistance dans la base de données
            progressCallback?.Invoke("Création du projet et insertion de toutes ses composantes dans la base...");
            using var db = _dbContextFactory();

            var profile = ProjectRadarConfigService.LoadProfile();
            string orgName = !string.IsNullOrWhiteSpace(profile?.OrganizationName) ? profile.OrganizationName : "Association Forêt Modèle Ifrane (AFMI)";
            string orgZone = (profile?.GeographicZones != null && profile.GeographicZones.Any()) ? string.Join(", ", profile.GeographicZones) : "Province d'Ifrane / Région Fès-Meknès (Moyen Atlas)";
            string orgStrengths = !string.IsNullOrWhiteSpace(profile?.DonorCapacityStatement) ? profile.DonorCapacityStatement : "Expertise reconnue de l'AFMI dans la conservation des forêts de cèdre et le développement local durable.";

            // Création de l'entité Project
            var project = new Project
            {
                Title = !string.IsNullOrWhiteSpace(blueprint.Title) ? blueprint.Title : initialTitle,
                LeadOrganization = orgName,
                GeneralObjective = !string.IsNullOrWhiteSpace(blueprint.GeneralObjective) 
                    ? blueprint.GeneralObjective 
                    : (!string.IsNullOrWhiteSpace(item.AiMatchedAxes) ? item.AiMatchedAxes : "Contribuer au développement durable et à la préservation des écosystèmes du Moyen Atlas"),
                ContextJustification = !string.IsNullOrWhiteSpace(blueprint.ContextJustification)
                    ? blueprint.ContextJustification
                    : $"Projet formulé en réponse à l'appel de fonds : {item.Title} ({item.Donor}).\n\nLien officiel : {item.Link}\n\nContexte et objectifs du bailleur :\n{item.CleanDescription}",
                TotalBudget = budgetTotal,
                StartDate = startDate,
                EndDate = endDate,
                InterventionZone = "Province d'Ifrane / Région Fès-Meknès (Moyen Atlas)",
                Background = "Bénéficiaires cibles : Populations locales, coopératives forestières et agricoles, jeunes et femmes rurales du Moyen Atlas.\n\n" +
                             (!string.IsNullOrWhiteSpace(blueprint.ContextJustification) ? blueprint.ContextJustification : item.CleanDescription),
                Justification = !string.IsNullOrWhiteSpace(blueprint.GeneralObjective) ? blueprint.GeneralObjective : "Projet d'intervention prioritaire pour l'AFMI",
                ProjectApproach = "Approche participative et territoriale intégrant la gestion intégrée des ressources naturelles (GIRN), le renforcement des capacités et le suivi d'impact axé sur les résultats (GAR).",
                ProjectImpacts = "Amélioration de la résilience socio-écologique, conservation de la biodiversité du Moyen Atlas et valorisation des filières vertes.",
                StrategicAlignment = "Alignement sur les orientations stratégiques nationales et les priorités territoriales du Moyen Atlas.",
                Sustainability = "Pérennisation institutionnelle via l'implication des coopératives locales et des partenaires territoriaux.",
                OrganizationalStrengths = orgStrengths,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.Projects.Add(project);
            await db.SaveChangesAsync(); // Obtenir l'ID du projet

            // 4. Insertion de l'Arbre de Problèmes (Phase 1)
            var coreNode = new ProblemNode
            {
                ProjectId = project.Id,
                NodeType = "Core",
                Title = !string.IsNullOrWhiteSpace(blueprint.ProblemCore) ? blueprint.ProblemCore : $"Vulnérabilité des écosystèmes face aux pressions anthropiques et climatiques",
                Description = "Problème central identifié dans le cadre de l'appel d'offres",
                Status = "Approved"
            };
            db.ProblemNodes.Add(coreNode);
            await db.SaveChangesAsync();

            // Causes
            foreach (var cause in blueprint.Causes)
            {
                db.ProblemNodes.Add(new ProblemNode
                {
                    ProjectId = project.Id,
                    NodeType = "Cause",
                    ParentNodeId = coreNode.Id,
                    Title = cause,
                    Description = "Cause directe",
                    Status = "Approved"
                });
            }

            // Conséquences
            foreach (var consequence in blueprint.Consequences)
            {
                db.ProblemNodes.Add(new ProblemNode
                {
                    ProjectId = project.Id,
                    NodeType = "Consequence",
                    ParentNodeId = coreNode.Id,
                    Title = consequence,
                    Description = "Effet / Conséquence directe",
                    Status = "Approved"
                });
            }
            await db.SaveChangesAsync();

            // 5. Insertion du Cadre Logique (Phase 2 : Objectifs Spécifiques, Résultats, Activités, Indicateurs)
            int osIndex = 1;
            foreach (var osBlueprint in blueprint.SpecificObjectives)
            {
                var obj = new Objective
                {
                    ProjectId = project.Id,
                    Code = $"OS{osIndex}",
                    Description = osBlueprint.Title,
                    Status = "Approved"
                };
                db.Objectives.Add(obj);
                await db.SaveChangesAsync();

                int resIndex = 1;
                foreach (var resBlueprint in osBlueprint.Results)
                {
                    var res = new Result
                    {
                        ObjectiveId = obj.Id,
                        Code = $"R{osIndex}.{resIndex}",
                        Description = resBlueprint.Title,
                        Status = "Approved"
                    };
                    db.Results.Add(res);
                    await db.SaveChangesAsync();

                    int actIndex = 1;
                    foreach (var actBlueprint in resBlueprint.Activities)
                    {
                        var act = new Activity
                        {
                            ResultId = res.Id,
                            Code = $"A{osIndex}.{resIndex}.{actIndex}",
                            Description = actBlueprint.Title,
                            BudgetAllocated = actBlueprint.EstimatedBudget,
                            StartDate = startDate.AddMonths((actIndex - 1) * 3),
                            EndDate = startDate.AddMonths(actIndex * 4),
                            Status = "Approved"
                        };
                        db.Activities.Add(act);
                        await db.SaveChangesAsync();

                        // Indicateur SMART pour chaque activité
                        if (!string.IsNullOrWhiteSpace(actBlueprint.IndicatorName))
                        {
                            db.Indicators.Add(new Indicator
                            {
                                ProjectId = project.Id,
                                ObjectiveId = obj.Id,
                                ResultId = res.Id,
                                ActivityId = act.Id,
                                Code = $"IND{osIndex}.{resIndex}.{actIndex}",
                                Description = actBlueprint.IndicatorName,
                                Baseline = "0",
                                Target = actBlueprint.IndicatorTarget,
                                VerificationSource = "Rapports d'activité, feuilles de présence et PV de réception",
                                Frequency = "Trimestrielle",
                                CollectionMethod = "Suivi de terrain et comptage direct"
                            });
                        }

                        actIndex++;
                    }

                    resIndex++;
                }

                osIndex++;
            }

            // 6. Insertion des Risques et Mesures d'Atténuation (Phase 2)
            foreach (var rk in blueprint.Risks)
            {
                db.Risks.Add(new Risk
                {
                    ProjectId = project.Id,
                    Category = rk.Category,
                    Description = rk.Description,
                    Probability = rk.Probability,
                    Impact = rk.Impact,
                    MitigationMeasure = rk.Mitigation,
                    ResponsibleRole = "Chef de projet AFMI",
                    Status = "Actif"
                });
            }

            // 7. Insertion du Budget Prévisionnel (Phase 3)
            var existingCategories = await db.BudgetCategories.ToListAsync();
            if (existingCategories.Count == 0)
            {
                existingCategories = new List<BudgetCategory>
                {
                    new() { Code = "RH", Name = "Ressources Humaines & Expertise" },
                    new() { Code = "EQ", Name = "Équipements & Matériel" },
                    new() { Code = "ACT", Name = "Activités de Terrain & Ateliers" },
                    new() { Code = "ADM", Name = "Frais de Gestion Administrative" }
                };
                db.BudgetCategories.AddRange(existingCategories);
                await db.SaveChangesAsync();
            }

            var catRh = existingCategories.FirstOrDefault(c => c.Code == "RH") ?? existingCategories[0];
            var catEq = existingCategories.FirstOrDefault(c => c.Code == "EQ") ?? existingCategories[0];
            var catAct = existingCategories.FirstOrDefault(c => c.Code == "ACT") ?? existingCategories[0];
            var catAdm = existingCategories.FirstOrDefault(c => c.Code == "ADM") ?? existingCategories[0];

            decimal budgetRh = Math.Round(budgetTotal * 0.30m, 2);
            decimal budgetEq = Math.Round(budgetTotal * 0.25m, 2);
            decimal budgetAct = Math.Round(budgetTotal * 0.38m, 2);
            decimal budgetAdm = budgetTotal - (budgetRh + budgetEq + budgetAct);

            db.BudgetItems.Add(new BudgetItem
            {
                ProjectId = project.Id,
                CategoryId = catRh.Id,
                Description = "Coordination du projet et expertise technique de terrain",
                Quantity = 12,
                Unit = "Mois",
                UnitCost = Math.Round(budgetRh / 12, 2),
                Frequency = 1,
                FundingRequested = budgetRh
            });

            db.BudgetItems.Add(new BudgetItem
            {
                ProjectId = project.Id,
                CategoryId = catEq.Id,
                Description = "Matériel d'intervention, équipements communautaires et outillage",
                Quantity = 1,
                Unit = "Lot",
                UnitCost = budgetEq,
                Frequency = 1,
                FundingRequested = budgetEq
            });

            db.BudgetItems.Add(new BudgetItem
            {
                ProjectId = project.Id,
                CategoryId = catAct.Id,
                Description = "Ateliers de renforcement des capacités et travaux opérationnels",
                Quantity = 1,
                Unit = "Forfait",
                UnitCost = budgetAct,
                Frequency = 1,
                FundingRequested = budgetAct
            });

            db.BudgetItems.Add(new BudgetItem
            {
                ProjectId = project.Id,
                CategoryId = catAdm.Id,
                Description = "Frais généraux de communication, audit financier et suivi",
                Quantity = 1,
                Unit = "Forfait",
                UnitCost = budgetAdm,
                Frequency = 1,
                FundingRequested = budgetAdm
            });

            // 8. Liaison avec le Pipeline des Subventions (grants_pipeline)
            var existingGrant = await db.GrantsPipeline.FirstOrDefaultAsync(g => g.ExternalId == item.ExternalId && !g.IsDeleted);
            if (existingGrant == null)
            {
                var grant = new GrantOpportunity
                {
                    ExternalId = item.ExternalId,
                    Title = item.Title,
                    Donor = item.Donor,
                    OriginalUrl = item.Link,
                    EstimatedBudget = budgetTotal,
                    Currency = item.AiCurrency ?? "EUR",
                    Deadline = item.AiDeadline,
                    SummaryAr = item.AiSummaryAr ?? string.Empty,
                    SummaryFr = item.AiSummaryFr ?? string.Empty,
                    RelevanceScore = item.AiScore ?? 7.5,
                    MatchedAxes = item.AiMatchedAxes ?? string.Empty,
                    Rationale = item.AiScoreRationale ?? "Projet créé et initié depuis le Radar",
                    PipelineStatus = VeilleStatus.EnRedaction,
                    AddedAt = DateTime.UtcNow,
                    DetectionSource = $"RSS — {item.FeedName}",
                    Notes = $"Lié au projet #{project.Id} : {project.Title}"
                };
                await new VeilleBaseService().EnterIntoBaseAsync(db, grant, DateTime.Today);
                db.GrantsPipeline.Add(grant);
            }
            else
            {
                existingGrant.PipelineStatus = VeilleStatus.EnRedaction;
                existingGrant.Notes = VeilleBaseService.AppendNote(existingGrant.Notes, DateTime.Today, $"Lié au projet #{project.Id} : {project.Title}");
            }

            await db.SaveChangesAsync();
            progressCallback?.Invoke("Projet configuré avec succès ! Ouverture de l'espace de travail...");

            return project;
        }

        private async Task<GeneratedProjectBlueprint> GenerateBlueprintViaAiAsync(RssItemCache item, decimal budget)
        {
            string prompt = $@"
Tu es un architecte expert en montage de projets de développement durable et d'action associative (spécialiste de la Gestion du Cycle de Projet - GCP et Gestion Axée sur les Résultats - GAR).
L'organisation est l'AFMI (Association Forêt Modèle Ifrane, Maroc).

Voici les termes de référence de l'opportunité de financement :
Titre de l'appel : {item.Title}
Bailleur de fonds : {item.Donor}
Lien officiel : {item.Link}
Axes et thématiques : {item.AiMatchedAxes}
Budget total estimé : {budget:N0} EUR
Description complète de l'appel :
{item.CleanDescription}

TÂCHE : Rédige une proposition de projet structurée et professionnelle pour l'AFMI couvrant l'ensemble des étapes de formulation (Cadrage, Arbre à problèmes, Cadre Logique SMART, Risques).

Réponds EXCLUSIVEMENT sous forme d'un objet JSON strict avec le schéma suivant (sans markdown ```json autour) :
{{
  ""title"": ""Titre professionnel du projet"",
  ""generalObjective"": ""Objectif général mesurable"",
  ""contextJustification"": ""Texte de justification détaillé en 2 paragraphes ancré dans la région d'Ifrane / Moyen Atlas"",
  ""problemCore"": ""Problème central identifié"",
  ""causes"": [""Cause directe 1"", ""Cause directe 2"", ""Cause directe 3""],
  ""consequences"": [""Effet direct 1"", ""Effet direct 2"", ""Effet direct 3""],
  ""specificObjectives"": [
    {{
      ""title"": ""Objectif Spécifique 1 (ex: Renforcer la résilience...)"",
      ""results"": [
        {{
          ""title"": ""Résultat 1.1 attendu"",
          ""activities"": [
            {{
              ""title"": ""Activité 1.1.1 opérationnelle"",
              ""estimatedBudget"": {Math.Round(budget * 0.25m)},
              ""indicatorName"": ""Nombre de bénéficiaires formés"",
              ""indicatorTarget"": ""150""
            }},
            {{
              ""title"": ""Activité 1.1.2 opérationnelle"",
              ""estimatedBudget"": {Math.Round(budget * 0.25m)},
              ""indicatorName"": ""Superficie restaurée (hectares)"",
              ""indicatorTarget"": ""50""
            }}
          ]
        }}
      ]
    }},
    {{
      ""title"": ""Objectif Spécifique 2 (ex: Valoriser les filières durables...)"",
      ""results"": [
        {{
          ""title"": ""Résultat 2.1 attendu"",
          ""activities"": [
            {{
              ""title"": ""Activité 2.1.1 opérationnelle"",
              ""estimatedBudget"": {Math.Round(budget * 0.25m)},
              ""indicatorName"": ""Coopératives accompagnées"",
              ""indicatorTarget"": ""10""
            }}
          ]
        }}
      ]
    }}
  ],
  ""risks"": [
    {{
      ""category"": ""Environnemental"",
      ""description"": ""Aléas climatiques et périodes de sécheresse sévère"",
      ""probability"": 3,
      ""impact"": 4,
      ""mitigation"": ""Adoption de techniques d'irrigation économe et essences forestières résilientes""
    }},
    {{
      ""category"": ""Opérationnel"",
      ""description"": ""Faible adhésion initiale de certains usagers locaux"",
      ""probability"": 2,
      ""impact"": 3,
      ""mitigation"": ""Approche participative continue et implication précoce des notables et coopératives""
    }},
    {{
      ""category"": ""Financier"",
      ""description"": ""Fluctuation des coûts d'approvisionnement ou retards de virement"",
      ""probability"": 2,
      ""impact"": 3,
      ""mitigation"": ""Suivi budgétaire mensuel et conventions de partenariat transparentes""
    }}
  ]
}}";

            string sys = "Tu es un consultant expert en conception de projets bailleurs. Réponds EXCLUSIVEMENT en JSON strict. Pas de texte explicatif, pas de balise markdown ```json.";

            string raw = await AiAssistantService.CallAiAsync(prompt, sys);
            return ParseBlueprintJson(raw, item, budget);
        }

        private GeneratedProjectBlueprint ParseBlueprintJson(string raw, RssItemCache item, decimal budget)
        {
            string cleaned = raw.Trim();
            var mdMatch = Regex.Match(cleaned, @"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
            if (mdMatch.Success) cleaned = mdMatch.Groups[1].Value.Trim();

            int firstBrace = cleaned.IndexOf('{');
            int lastBrace = cleaned.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
            }

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var blueprint = new GeneratedProjectBlueprint();

            if (root.TryGetProperty("title", out var tProp) && !string.IsNullOrWhiteSpace(tProp.GetString()))
                blueprint.Title = tProp.GetString()!;
            else
                blueprint.Title = item.Title;

            if (root.TryGetProperty("generalObjective", out var goProp) && !string.IsNullOrWhiteSpace(goProp.GetString()))
                blueprint.GeneralObjective = goProp.GetString()!;

            if (root.TryGetProperty("contextJustification", out var cjProp) && !string.IsNullOrWhiteSpace(cjProp.GetString()))
                blueprint.ContextJustification = cjProp.GetString()!;

            if (root.TryGetProperty("problemCore", out var pcProp) && !string.IsNullOrWhiteSpace(pcProp.GetString()))
                blueprint.ProblemCore = pcProp.GetString()!;

            if (root.TryGetProperty("causes", out var causesProp) && causesProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in causesProp.EnumerateArray())
                {
                    string? s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) blueprint.Causes.Add(s);
                }
            }

            if (root.TryGetProperty("consequences", out var consProp) && consProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in consProp.EnumerateArray())
                {
                    string? s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) blueprint.Consequences.Add(s);
                }
            }

            if (root.TryGetProperty("specificObjectives", out var soProp) && soProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var osEl in soProp.EnumerateArray())
                {
                    var os = new SpecificObjectiveBlueprint();
                    if (osEl.TryGetProperty("title", out var osTitle)) os.Title = osTitle.GetString() ?? "";

                    if (osEl.TryGetProperty("results", out var resProp) && resProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var resEl in resProp.EnumerateArray())
                        {
                            var res = new ResultBlueprint();
                            if (resEl.TryGetProperty("title", out var resTitle)) res.Title = resTitle.GetString() ?? "";

                            if (resEl.TryGetProperty("activities", out var actProp) && actProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var actEl in actProp.EnumerateArray())
                                {
                                    var act = new ActivityBlueprint();
                                    if (actEl.TryGetProperty("title", out var actTitle)) act.Title = actTitle.GetString() ?? "";
                                    if (actEl.TryGetProperty("estimatedBudget", out var actBudg)) act.EstimatedBudget = actBudg.GetDecimal();
                                    if (actEl.TryGetProperty("indicatorName", out var indName)) act.IndicatorName = indName.GetString() ?? "";
                                    if (actEl.TryGetProperty("indicatorTarget", out var indTgt)) act.IndicatorTarget = indTgt.GetString() ?? "100";
                                    res.Activities.Add(act);
                                }
                            }

                            os.Results.Add(res);
                        }
                    }

                    blueprint.SpecificObjectives.Add(os);
                }
            }

            if (root.TryGetProperty("risks", out var rkProp) && rkProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var rkEl in rkProp.EnumerateArray())
                {
                    var rk = new RiskBlueprint();
                    if (rkEl.TryGetProperty("category", out var rc)) rk.Category = rc.GetString() ?? "Opérationnel";
                    if (rkEl.TryGetProperty("description", out var rd)) rk.Description = rd.GetString() ?? "";
                    if (rkEl.TryGetProperty("mitigation", out var rm)) rk.Mitigation = rm.GetString() ?? "";
                    if (rkEl.TryGetProperty("probability", out var rp)) rk.Probability = rp.GetInt32();
                    if (rkEl.TryGetProperty("impact", out var ri)) rk.Impact = ri.GetInt32();
                    blueprint.Risks.Add(rk);
                }
            }

            return blueprint;
        }

        private GeneratedProjectBlueprint GenerateFallbackBlueprint(RssItemCache item, decimal budget)
        {
            var bp = new GeneratedProjectBlueprint
            {
                Title = item.Title.StartsWith("Projet ", StringComparison.OrdinalIgnoreCase) ? item.Title : $"Projet : {item.Title}",
                GeneralObjective = !string.IsNullOrWhiteSpace(item.AiMatchedAxes)
                    ? $"Favoriser la réalisation des objectifs relatifs à : {item.AiMatchedAxes}"
                    : "Améliorer les conditions socio-économiques locales et la résilience environnementale dans le Moyen Atlas",
                ContextJustification = $"Le présent projet est initié par l'AFMI en réponse à l'appel à subventions '{item.Title}' publié par {item.Donor}.\n\n" +
                                       $"Contexte opérationnel : {item.CleanDescription}\n\n" +
                                       $"Ce projet cible les écosystèmes fragiles de la province d'Ifrane et vise à soutenir les communautés locales à travers des actions participatives.",
                ProblemCore = $"Pressions sur les ressources naturelles et vulnérabilité des communautés locales de la région d'Ifrane",
                Causes = new List<string>
                {
                    "Changements climatiques et raréfaction des ressources en eau",
                    "Manque de moyens et de formation des coopératives et acteurs locaux",
                    "Faible valorisation économique et transformation locale des produits des écosystèmes"
                },
                Consequences = new List<string>
                {
                    "Dégradation continue du couvert végétal et de la biodiversité",
                    "Fragilisation économique des populations rurales dépendantes de la forêt",
                    "Exode rural des jeunes et vulnérabilité accrue face aux crises climatiques"
                }
            };

            // OS 1
            var os1 = new SpecificObjectiveBlueprint
            {
                Title = "Renforcer la préservation et la gestion durable des ressources naturelles ciblées"
            };
            var res1 = new ResultBlueprint
            {
                Title = "Des pratiques de conservation écologique et de gestion durable sont adoptées"
            };
            res1.Activities.Add(new ActivityBlueprint
            {
                Title = "Organisation d'ateliers de sensibilisation et de formation aux bonnes pratiques",
                EstimatedBudget = Math.Round(budget * 0.25m, 2),
                IndicatorName = "Nombre de personnes formées",
                IndicatorTarget = "120"
            });
            res1.Activities.Add(new ActivityBlueprint
            {
                Title = "Mise en œuvre des interventions pilotes de restauration sur le terrain",
                EstimatedBudget = Math.Round(budget * 0.35m, 2),
                IndicatorName = "Superficie aménagée (hectares)",
                IndicatorTarget = "40"
            });
            os1.Results.Add(res1);
            bp.SpecificObjectives.Add(os1);

            // OS 2
            var os2 = new SpecificObjectiveBlueprint
            {
                Title = "Développer des activités génératrices de revenus durables pour les communautés locales"
            };
            var res2 = new ResultBlueprint
            {
                Title = "Les coopératives locales bénéficient d'un appui technique et matériel ciblé"
            };
            res2.Activities.Add(new ActivityBlueprint
            {
                Title = "Fourniture d'équipements de valorisation et accompagnement managérial",
                EstimatedBudget = Math.Round(budget * 0.40m, 2),
                IndicatorName = "Coopératives équipées et accompagnées",
                IndicatorTarget = "8"
            });
            os2.Results.Add(res2);
            bp.SpecificObjectives.Add(os2);

            // Risques
            bp.Risks.Add(new RiskBlueprint
            {
                Category = "Environnemental",
                Description = "Conditions climatiques extrêmes affectant les travaux de terrain",
                Probability = 3,
                Impact = 3,
                Mitigation = "Planification souple des interventions selon les saisons favorables"
            });
            bp.Risks.Add(new RiskBlueprint
            {
                Category = "Opérationnel",
                Description = "Retard dans la livraison des équipements ou démarches administratives",
                Probability = 2,
                Impact = 3,
                Mitigation = "Coordination étroite préalable avec les autorités territoriales et fournisseurs"
            });
            bp.Risks.Add(new RiskBlueprint
            {
                Category = "Financier",
                Description = "Retards de trésorerie ou augmentation imprévue des coûts des intrants",
                Probability = 2,
                Impact = 3,
                Mitigation = "Gestion prévisionnelle rigoureuse de la trésorerie et cadrage contractuel"
            });

            return bp;
        }

        /// <summary>
        /// Proposition exploitable seulement si la chaîne est complète : au moins 2 causes et 2 conséquences,
        /// chaque objectif a au moins un résultat, chaque résultat au moins une activité, et au moins 3 activités au total.
        /// </summary>
        private static bool IsCompleteBlueprint(GeneratedProjectBlueprint? b) =>
            b != null &&
            b.Causes.Count >= 2 &&
            b.Consequences.Count >= 2 &&
            b.SpecificObjectives.Count > 0 &&
            b.SpecificObjectives.All(o => o.Results.Count > 0 && o.Results.All(r => r.Activities.Count > 0)) &&
            b.SpecificObjectives.Sum(o => o.Results.Sum(r => r.Activities.Count)) >= 3;

        private class GeneratedProjectBlueprint
        {
            public string Title { get; set; } = string.Empty;
            public string GeneralObjective { get; set; } = string.Empty;
            public string ContextJustification { get; set; } = string.Empty;
            public string ProblemCore { get; set; } = string.Empty;
            public List<string> Causes { get; set; } = new();
            public List<string> Consequences { get; set; } = new();
            public List<SpecificObjectiveBlueprint> SpecificObjectives { get; set; } = new();
            public List<RiskBlueprint> Risks { get; set; } = new();
        }

        private class SpecificObjectiveBlueprint
        {
            public string Title { get; set; } = string.Empty;
            public List<ResultBlueprint> Results { get; set; } = new();
        }

        private class ResultBlueprint
        {
            public string Title { get; set; } = string.Empty;
            public List<ActivityBlueprint> Activities { get; set; } = new();
        }

        private class ActivityBlueprint
        {
            public string Title { get; set; } = string.Empty;
            public decimal EstimatedBudget { get; set; }
            public string IndicatorName { get; set; } = string.Empty;
            public string IndicatorTarget { get; set; } = "100";
        }

        private class RiskBlueprint
        {
            public string Category { get; set; } = "Opérationnel";
            public string Description { get; set; } = string.Empty;
            public int Probability { get; set; } = 3;
            public int Impact { get; set; } = 3;
            public string Mitigation { get; set; } = string.Empty;
        }
    }
}
