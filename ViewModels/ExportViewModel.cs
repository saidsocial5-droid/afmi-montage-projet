using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AFMI_Montage_projet_2.Models;
using AFMI_Montage_projet_2.Services;
using Microsoft.Win32;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class ExportViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private bool _isExporting = false;

        public ExportViewModel(Project project)
        {
            _project = project;
        }

        [RelayCommand]
        public void ExportExcel()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Fichiers Excel (*.xlsx)|*.xlsx",
                FileName = $"Budget_{_project.Title.Replace(" ", "_")}.xlsx",
                Title = "Exporter le budget du projet"
            };

            if (sfd.ShowDialog() == true)
            {
                IsExporting = true;
                StatusMessage = "Génération du fichier Excel en cours...";
                try
                {
                    DocumentExportService.ExportBudgetToExcel(_project.Id, sfd.FileName);
                    StatusMessage = $"Budget Excel exporté avec succès à l'emplacement : {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erreur lors de l'export Excel : {ex.Message}";
                }
                finally
                {
                    IsExporting = false;
                }
            }
        }

        [RelayCommand]
        public void ExportWord()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Fichiers Word (*.docx)|*.docx",
                FileName = $"Proposition_{_project.Title.Replace(" ", "_")}.docx",
                Title = "Exporter la proposition Word"
            };

            if (sfd.ShowDialog() == true)
            {
                IsExporting = true;
                StatusMessage = "Génération du document Word en cours...";
                try
                {
                    DocumentExportService.ExportProjectToWord(_project.Id, sfd.FileName);
                    StatusMessage = $"Proposition Word exportée avec succès à l'emplacement : {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erreur lors de l'export Word : {ex.Message}";
                }
                finally
                {
                    IsExporting = false;
                }
            }
        }

        [RelayCommand]
        public void ExportPdf()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Fichiers PDF (*.pdf)|*.pdf",
                FileName = $"Dossier_{_project.Title.Replace(" ", "_")}.pdf",
                Title = "Exporter le dossier PDF"
            };

            if (sfd.ShowDialog() == true)
            {
                IsExporting = true;
                StatusMessage = "Génération du fichier PDF en cours...";
                try
                {
                    DocumentExportService.ExportProjectToPdf(_project.Id, sfd.FileName);
                    StatusMessage = $"Dossier PDF exporté avec succès à l'emplacement : {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erreur lors de l'export PDF : {ex.Message}";
                }
                finally
                {
                    IsExporting = false;
                }
            }
        }

        [RelayCommand]
        public void ExportProfileWord()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Fichiers Word (*.docx)|*.docx",
                FileName = "Fiche_Institutionnelle_AFMI.docx",
                Title = "Exporter la Fiche Institutionnelle de l'Organisation"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    IsExporting = true;
                    StatusMessage = "Génération de la fiche institutionnelle Word...";
                    DocumentExportService.ExportOrganizationProfileToWord(sfd.FileName);
                    StatusMessage = $"Fiche institutionnelle exportée avec succès : {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erreur lors de l'export de la fiche : {ex.Message}";
                }
                finally
                {
                    IsExporting = false;
                }
            }
        }

        [RelayCommand]
        public void ExportProfilePdf()
        {
            var sfd = new SaveFileDialog
            {
                Filter = "Fichiers PDF (*.pdf)|*.pdf",
                FileName = "Fiche_Institutionnelle_AFMI.pdf",
                Title = "Exporter la Fiche Institutionnelle en PDF"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    IsExporting = true;
                    StatusMessage = "Génération de la fiche institutionnelle PDF...";
                    DocumentExportService.ExportOrganizationProfileToPdf(sfd.FileName);
                    StatusMessage = $"Fiche institutionnelle PDF exportée avec succès : {Path.GetFileName(sfd.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erreur lors de l'export PDF : {ex.Message}";
                }
                finally
                {
                    IsExporting = false;
                }
            }
        }
    }
}

