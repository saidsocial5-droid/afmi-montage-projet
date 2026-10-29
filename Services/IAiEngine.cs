using System.Threading.Tasks;

namespace AFMI_Montage_projet_2.Services
{
    public interface IAiEngine
    {
        string EngineName { get; }
        Task<string> GenerateAsync(string prompt, string systemInstruction = "", bool enableSearch = false);
    }
}
