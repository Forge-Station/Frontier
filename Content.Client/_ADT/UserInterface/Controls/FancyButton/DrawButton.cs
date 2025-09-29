using Robust.Client.UserInterface.Controls;

namespace Content.Client._ADT.UserInterface.Controls.FancyButton;

public sealed class DrawButton : Button
{
    public event Action? OnDrawModeChanged;

    public DrawButton()
    {
    }

    protected override void DrawModeChanged()
    {
        OnDrawModeChanged?.Invoke();
    }
}
