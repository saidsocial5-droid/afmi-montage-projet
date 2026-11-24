using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QRCoder;
using AFMI_Montage_projet_2.Services.Drive;
using AFMI_Montage_projet_2.Services;

namespace AFMI_Montage_projet_2.ViewModels
{
    public partial class PairingViewModel : ObservableObject
    {
        private readonly DevicePairingService _pairingService;
        private readonly string _workspaceId;
        private PairingSessionPayload? _currentPayload;
        private DispatcherTimer? _timer;
        private DispatcherTimer? _pollTimer;

        [ObservableProperty]
        private BitmapImage? _qrCodeImage;

        [ObservableProperty]
        private int _timeRemaining = 120;

        [ObservableProperty]
        private string _statusText = "Génération du code...";

        [ObservableProperty]
        private bool _isExpired = false;

        public Action? OnPairingSuccess;
        public Action? OnCloseRequested;

        public PairingViewModel(DevicePairingService pairingService, string workspaceId)
        {
            _pairingService = pairingService;
            _workspaceId = workspaceId;
        }

        public async Task StartPairingAsync()
        {
            IsExpired = false;
            TimeRemaining = 120;
            StatusText = "Génération du code...";

            var userEmail = SessionManager.CurrentUser?.Email ?? "";
            var currentPayload = await _pairingService.CreatePairingSessionAsync(_workspaceId, userEmail);
            _currentPayload = currentPayload;

            var json = JsonSerializer.Serialize(new { 
                v = currentPayload.Version, 
                w = currentPayload.WorkspaceId, 
                u = currentPayload.UserEmail, 
                q = currentPayload.QrId, 
                n = currentPayload.Nonce 
            });

            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            {
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(json, QRCodeGenerator.ECCLevel.Q);
                using (QRCode qrCode = new QRCode(qrCodeData))
                {
                    using (Bitmap qrBitmap = qrCode.GetGraphic(10))
                    {
                        QrCodeImage = BitmapToImageSource(qrBitmap);
                    }
                }
            }

            StatusText = "En attente de scan par l'application mobile...";

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pollTimer.Tick += PollTimer_Tick;
            _pollTimer.Start();
            
            AFMI_Montage_projet_2.Security.AuditLogger.Log("Génération QR Code", $"QR Code de couplage généré. QrId: {currentPayload.QrId}");
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            TimeRemaining--;
            if (TimeRemaining <= 0)
            {
                ExpireSession();
            }
        }

        private async void PollTimer_Tick(object? sender, EventArgs e)
        {
            _pollTimer?.Stop();
            var currentPayload = _currentPayload;
            if (currentPayload == null) return;
            try
            {
                var payload = await _pairingService.CheckPairingSessionAsync(_workspaceId, currentPayload.QrId);
                if (payload != null && payload.Status == "scanned")
                {
                    _timer?.Stop();
                    // Vérification de sécurité et confirmation
                    var confirm = MessageBox.Show(
                        $"Un appareil '{payload.RequestingDeviceName}' demande à se connecter à votre compte.\n\nAutoriser cet appareil ?",
                        "Demande de couplage",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (confirm == MessageBoxResult.Yes)
                    {
                        // Enregistrer l'appareil localement
                        using var db = new Models.AppDbContext();
                        var device = new Models.LinkedDevice
                        {
                            UserEmail = payload.UserEmail,
                            DeviceName = payload.RequestingDeviceName,
                            PublicKey = payload.RequestingPublicKey
                        };
                        db.LinkedDevices.Add(device);
                        await db.SaveChangesAsync();

                        // Notifier l'appareil que c'est approuvé
                        // Dans un scénario réel on mettrait à jour le fichier Drive pour que l'app sache
                        // Mais ici on supprime le fichier (Atomic burn) pour éviter la réutilisation
                        await _pairingService.DeletePairingSessionAsync(_workspaceId, currentPayload.QrId);
                        
                        AFMI_Montage_projet_2.Security.AuditLogger.Log("Couplage réussi", $"Appareil {device.DeviceName} couplé.");
                        StatusText = "Appareil couplé avec succès !";
                        OnPairingSuccess?.Invoke();
                        OnCloseRequested?.Invoke();
                        return;
                    }
                    else
                    {
                        // Refusé
                        await _pairingService.DeletePairingSessionAsync(_workspaceId, currentPayload.QrId);
                        AFMI_Montage_projet_2.Security.AuditLogger.Log("Couplage refusé", $"Couplage rejeté par l'utilisateur pour l'appareil {payload.RequestingDeviceName}.");
                        StatusText = "Couplage refusé.";
                        IsExpired = true;
                        return;
                    }
                }
            }
            catch
            {
                // Ignore transient errors
            }

            if (!IsExpired) _pollTimer?.Start();
        }

        private async void ExpireSession()
        {
            _timer?.Stop();
            _pollTimer?.Stop();
            IsExpired = true;
            StatusText = "Code expiré.";
            if (_currentPayload != null)
            {
                try {
                    await _pairingService.DeletePairingSessionAsync(_workspaceId, _currentPayload.QrId);
                    AFMI_Montage_projet_2.Security.AuditLogger.Log("Couplage expiré", $"Le QR Code {_currentPayload.QrId} a expiré sans être utilisé.");
                } catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(PairingViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
            }
        }

        [RelayCommand]
        private async Task RefreshAsync()
        {
            if (_currentPayload != null)
            {
                try {
                    await _pairingService.DeletePairingSessionAsync(_workspaceId, _currentPayload.QrId);
                } catch (Exception swallowedEx) { global::AFMI_Montage_projet_2.Services.AppLogger.Info(nameof(PairingViewModel), $"Erreur ignorée : {swallowedEx.Message}"); }
            }
            _timer?.Stop();
            _pollTimer?.Stop();
            await StartPairingAsync();
        }

        [RelayCommand]
        private void CancelAndClose()
        {
            ExpireSession();
            OnCloseRequested?.Invoke();
        }

        private BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                return bitmapImage;
            }
        }
    }
}




