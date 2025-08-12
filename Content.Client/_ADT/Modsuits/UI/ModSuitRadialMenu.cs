using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._ADT.Modsuit.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._ADT.Modsuits.UI;

public sealed class ModSuitRadialMenu : RadialMenu
{
    [Dependency] private readonly EntityManager _entityManager = default!;

    public ModSuitRadialMenu()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);
    }

    public EntityUid Entity { get; set; }

    public event Action<EntityUid>? SendToggleClothingMessageAction;

    public void SetEntity(EntityUid uid)
    {
        Entity = uid;
        RefreshUI();
    }

    public void RefreshUI()
    {
        var main = FindControl<RadialContainer>("Main");

        if (!_entityManager.TryGetComponent<ModSuitComponent>(Entity, out var clothing))
            return;

        var clothingContainer = clothing.Container;

        if (clothingContainer == null)
            return;

        foreach (var attached in clothing.ClothingUids)
        {
            // Change tooltip text if attached clothing is toggle/untoggled
            var tooltipText = Loc.GetString("modsuit-unattach-tooltip");

            if (clothingContainer.Contains(attached.Key))
                tooltipText = Loc.GetString("modsuit-attach-tooltip");

            var button = new ModSuitRadialMenuButton
            {
                BackgroundColor = clothing.BackpanelsColor,
                StyleClasses = { "RadialMenuButton" },
                SetSize = new Vector2(64, 64),
                ToolTip = tooltipText,
                AttachedClothingId = attached.Key,
            };

            var spriteView = new SpriteView
            {
                SetSize = new Vector2(48, 48),
                VerticalAlignment = VAlignment.Center,
                HorizontalAlignment = HAlignment.Center,
                Stretch = SpriteView.StretchMode.Fill,
            };

            spriteView.SetEntity(attached.Key);

            button.AddChild(spriteView);
            main.AddChild(button);
        }

        AddModSuitMenuButtonOnClickAction(main);
    }

    private void AddModSuitMenuButtonOnClickAction(Control control)
    {
        var mainControl = control as RadialContainer;

        if (mainControl == null)
            return;

        foreach (var child in mainControl.Children)
        {
            var castChild = child as ModSuitRadialMenuButton;

            if (castChild == null)
                return;

            castChild.OnButtonUp += _ =>
            {
                SendToggleClothingMessageAction?.Invoke(castChild.AttachedClothingId);
                mainControl.DisposeAllChildren();
                RefreshUI();
            };
        }
    }
}

public sealed class ModSuitRadialMenuButton : RadialMenuTextureButtonWithSector
{
    public EntityUid AttachedClothingId { get; set; }
}
