using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using AFMI_Montage_projet_2.Services.Collab;
using AFMI_Montage_projet_2.ViewModels;

namespace AFMI_Montage_Projet.Tests
{
    public class VoiceNoteAssistantTests
    {
        [Fact]
        public void VoiceNoteService_InstantiatesAndDisposesCleanly()
        {
            var service = new VoiceNoteAssistantService();
            Assert.False(service.IsRecording);
            Assert.Empty(service.LiveRecognizedText);

            service.Dispose();
        }

        [Fact]
        public async Task StructureTranscript_WhenEmpty_ReturnsFalse()
        {
            using var service = new VoiceNoteAssistantService();
            var (success, notes, error) = await service.StructureTranscriptWithAiAsync(string.Empty);

            Assert.False(success);
            Assert.Contains("vide", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ProcessAudioFile_WhenFileNotFound_ReturnsError()
        {
            using var service = new VoiceNoteAssistantService();
            string nonExistent = Path.Combine(Path.GetTempPath(), $"non_existent_{Guid.NewGuid():N}.wav");

            var (success, notes, error) = await service.ProcessAudioFileAsync(nonExistent);

            Assert.False(success);
            Assert.Contains("introuvable", error, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ProcessAudioFile_WhenEmptyFile_ReturnsError()
        {
            using var service = new VoiceNoteAssistantService();
            string emptyFile = Path.Combine(Path.GetTempPath(), $"empty_{Guid.NewGuid():N}.wav");
            File.WriteAllBytes(emptyFile, Array.Empty<byte>());

            try
            {
                var (success, notes, error) = await service.ProcessAudioFileAsync(emptyFile);

                Assert.False(success);
                Assert.Contains("vide ou trop court", error, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                if (File.Exists(emptyFile)) File.Delete(emptyFile);
            }
        }

        [Fact]
        public void CollaborationViewModel_VoiceProperties_DefaultCorrectly()
        {
            var vm = new CollaborationViewModel(999);

            Assert.False(vm.IsVoiceRecording);
            Assert.Equal("00:00", vm.RecordingDurationText);
            Assert.False(vm.IsProcessingVoiceNotes);
            Assert.NotNull(vm.StartVoiceRecordingCommand);
            Assert.NotNull(vm.StopVoiceRecordingAndGenerateNotesCommand);
            Assert.NotNull(vm.ImportAudioFileCommand);
            Assert.NotNull(vm.StructureNotesWithAiCommand);
            Assert.NotNull(vm.ClearMeetingNotesCommand);

            vm.Dispose();
        }
    }
}
