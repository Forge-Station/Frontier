using System.Linq;
using Content.Client._Forge.TTS;
using Content.Shared._Forge.Sponsor;
using Content.Shared._Forge.TTS;
using Content.Shared.Preferences;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<TTSVoicePrototype> _voiceList = new();

    private void InitializeVoice()
    {
        _voiceList = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => Loc.GetString(o.Name))
            .ToList();

        VoiceButton.OnItemSelected += args =>
        {
            VoiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTTS();
    }

    private void UpdateTTSVoicesControls()
    {
        var user = _playerManager.LocalUser;
        if (Profile is null)
            return;

        VoiceButton.Clear();

        var firstVoiceChoiceId = 1;
        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];
            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            VoiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == 1)
                firstVoiceChoiceId = i;

            if (voice.SponsorLevel != SponsorLevel.None && user != null)
            {
                _sponsorMan.TryGetSponsor(user.Value, out var level);
                if (voice.SponsorLevel > level)
                    VoiceButton.SetItemDisabled(VoiceButton.GetIdx(i), true);
            }
        }

        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == Profile.Voice);
        if (!VoiceButton.TrySelectId(voiceChoiceId) &&
            VoiceButton.TrySelectId(firstVoiceChoiceId))
        {
            SetVoice(_voiceList[firstVoiceChoiceId].ID);
        }
    }

    private void PlayPreviewTTS()
    {
        if (Profile is null)
            return;

        _entManager.System<TTSSystem>().RequestPreviewTTS(Profile.Voice);
    }
}
