using Japlayer.Data.Models;

namespace Japlayer.Data.Contracts
{
    public interface ILibraryPresetProvider
    {
        public Task<IReadOnlyList<LibraryPresetModel>> GetPresetsAsync();

        public Task<LibraryPresetModel> CreatePresetAsync(LibraryPresetModel preset);

        public Task UpdatePresetAsync(LibraryPresetModel preset);

        public Task DeletePresetAsync(int presetId);
    }
}
