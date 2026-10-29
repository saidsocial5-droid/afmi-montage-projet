using System;
using System.Linq;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.ViewModels;
using Xunit;

namespace AFMI_Montage_Projet.Tests
{
    public class ObjectiveTests
    {
        [Fact]
        public void DeleteObjective_ShouldRemoveUnsavedObjectiveFromCollection()
        {
            var project = new Project
            {
                Id = 99999,
                Title = "Projet Test Cadrage"
            };

            var mainVm = new MainViewModel();
            var cadrageVm = new CadrageViewModel(project, mainVm);

            // Ajouter un objectif non encore enregistré (Id == 0) - Cas exact du rapport utilisateur
            var unsavedObj = new Objective
            {
                ProjectId = project.Id,
                Code = "OS1",
                Description = "Here are three clear and measurable specific objectives, formatted with dashes:\n**Objectif Spécifique 1**"
            };
            cadrageVm.Objectives.Add(unsavedObj);

            Assert.Contains(unsavedObj, cadrageVm.Objectives);

            // Supprimer l'objectif
            cadrageVm.DeleteObjective(unsavedObj);

            // Vérifier qu'il a bien été supprimé immédiatement de la liste
            Assert.DoesNotContain(unsavedObj, cadrageVm.Objectives);
        }

        [Fact]
        public void CleanObjectiveText_ShouldCleanConversationalIntro()
        {
            string rawAiOutput = @"Here are three clear and measurable specific objectives, formatted with dashes:
**Objectif Spécifique 1 :** Améliorer la résilience climatique des exploitations agricoles locales.";

            // Simuler le nettoyage via reflection ou test direct de la logique de nettoyage
            var method = typeof(CadrageViewModel).GetMethod("CleanObjectiveText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);

            string cleaned = (string)method.Invoke(null, new object[] { rawAiOutput })!;

            Assert.DoesNotContain("Here are three clear", cleaned);
            Assert.DoesNotContain("**Objectif Spécifique 1", cleaned);
            Assert.Equal("Améliorer la résilience climatique des exploitations agricoles locales.", cleaned);
        }

        [Fact]
        public void DeleteObjective_WithDatabaseEntities_ShouldCleanAllCascadingDependencies()
        {
            using var db = new AppDbContext();
            
            // Créer un projet de test
            var project = new Project
            {
                Title = "Projet Test Suppression OS Cascade",
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddMonths(6)
            };
            db.Projects.Add(project);
            db.SaveChanges();

            // Créer un OS avec Résultats, Activités, Indicateurs et Rapports
            var obj = new Objective
            {
                ProjectId = project.Id,
                Code = "OS1",
                Description = "Objectif spécifique à supprimer"
            };
            db.Objectives.Add(obj);
            db.SaveChanges();

            var res = new Result
            {
                ObjectiveId = obj.Id,
                Code = "R1.1",
                Description = "Résultat lié"
            };
            db.Results.Add(res);
            db.SaveChanges();

            var act = new Activity
            {
                ResultId = res.Id,
                Code = "A1.1.1",
                Description = "Activité liée"
            };
            db.Activities.Add(act);
            db.SaveChanges();

            var indicator = new Indicator
            {
                ObjectiveId = obj.Id,
                ActivityId = act.Id,
                Code = "IND1",
                Description = "Indicateur lié"
            };
            db.Indicators.Add(indicator);

            var report = new ActivityReport
            {
                ActivityId = act.Id,
                Narrative = "Rapport d'activité",
                ExecutionRate = 50m,
                Status = "InProgress",
                ReportDate = DateTime.UtcNow
            };
            db.ActivityReports.Add(report);
            db.SaveChanges();

            var mainVm = new MainViewModel();
            var cadrageVm = new CadrageViewModel(project, mainVm);
            cadrageVm.Objectives.Add(obj);

            // Exécuter la suppression
            cadrageVm.DeleteObjective(obj);

            // Vérifier que tout a été proprement nettoyé en base de données
            using var dbVerify = new AppDbContext();
            Assert.Null(dbVerify.Objectives.FirstOrDefault(o => o.Id == obj.Id));
            Assert.Null(dbVerify.Results.FirstOrDefault(r => r.Id == res.Id));
            Assert.Null(dbVerify.Activities.FirstOrDefault(a => a.Id == act.Id));
            Assert.Null(dbVerify.Indicators.FirstOrDefault(i => i.Id == indicator.Id));
            Assert.Null(dbVerify.ActivityReports.FirstOrDefault(ar => ar.Id == report.Id));
            Assert.DoesNotContain(cadrageVm.Objectives, o => o.Id == obj.Id);
        }
    }
}
