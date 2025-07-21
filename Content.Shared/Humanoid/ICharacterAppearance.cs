
namespace Content.Shared.Humanoid
{
    public interface ICharacterAppearance
    {
        bool MemberwiseEquals(ICharacterAppearance other, out string? error); // Forge-Change
    }
}
