using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using AFMI_Montage_projet_2.Models;
using Microsoft.EntityFrameworkCore;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class DashboardViewModel : ObservableObject
    {
        private readonly Project _project;

        [ObservableProperty]
        private decimal _globalExecutionRate = 0;

        [ObservableProperty]
        private decimal _totalBudget = 0;

        [ObservableProperty]
        private decimal _consumedBudget = 0;

        [ObservableProperty]
        private decimal _remainingBudget = 0;

        [ObservableProperty]
        private ObservableCollection<ActivityProgress> _activityProgresses = new();

        public DashboardViewModel(Project project)
        {
            _project = project;
            LoadDashboardData();
        }

        private void LoadDashboardData()
        {
            using var db = new AppDbContext();
            
            // 1. Load Activities with Reports and BudgetItems
            var activities = db.Activities
                .Include(a => a.Result)
                .ThenInclude(r => r.Objective)
                .Where(a => a.Result.Objective.ProjectId == _project.Id)
                .ToList();

            var reports = db.ActivityReports.Where(ar => ar.Status == "Approved").ToList();
            var budgetItems = db.BudgetItems.Where(b => b.ProjectId == _project.Id).ToList();

            decimal totalExecutionSum = 0;
            int activityCount = activities.Count;
            
            decimal globalBudget = budgetItems.Sum(b => b.TotalCost);
            decimal globalConsumed = 0;

            ActivityProgresses.Clear();

            foreach (var act in activities)
            {
                // Find latest approved report for this activity
                var actReports = reports.Where(r => r.ActivityId == act.Id).OrderByDescending(r => r.ReportDate).ToList();
                decimal actExecutionRate = actReports.FirstOrDefault()?.ExecutionRate ?? 0;
                
                totalExecutionSum += actExecutionRate;

                // Find budget linked to this activity
                var actBudgetLines = budgetItems.Where(b => b.ActivityId == act.Id).ToList();
                decimal actTotalBudget = actBudgetLines.Sum(b => b.TotalCost);
                
                // Consumed budget for this activity based on its execution rate (Simplified calculation)
                decimal actConsumedBudget = actTotalBudget * (actExecutionRate / 100m);
                globalConsumed += actConsumedBudget;

                ActivityProgresses.Add(new ActivityProgress
                {
                    ActivityCode = act.Code,
                    ActivityDescription = act.Description,
                    ExecutionRate = actExecutionRate,
                    TotalBudget = actTotalBudget,
                    ConsumedBudget = actConsumedBudget
                });
            }

            // Global Metrics
            if (activityCount > 0)
            {
                GlobalExecutionRate = Math.Round(totalExecutionSum / activityCount, 2);
            }
            
            TotalBudget = globalBudget;
            ConsumedBudget = globalConsumed;
            RemainingBudget = TotalBudget - ConsumedBudget;
        }
    }

    public class ActivityProgress
    {
        public string ActivityCode { get; set; } = string.Empty;
        public string ActivityDescription { get; set; } = string.Empty;
        public decimal ExecutionRate { get; set; }
        public decimal TotalBudget { get; set; }
        public decimal ConsumedBudget { get; set; }
    }
}
