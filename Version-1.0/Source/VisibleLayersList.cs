using Bindito.Core;
using HarmonyLib;
using System;
using Timberborn.CoreUI;
using Timberborn.InputSystem;
using Timberborn.LevelVisibilitySystem;
using Timberborn.LevelVisibilitySystemUI;
using Timberborn.MapStateSystem;
using Timberborn.Modding;
using Timberborn.ModManagerScene;
using Timberborn.SingletonSystem;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.VisibleLayersList
{
  public class LayerDropdownStarter : IModStarter
  {
    public void StartMod(IModEnvironment modEnvironment)
    {
      Harmony harmony = new Harmony("calloatti.visiblelayersList");
      harmony.PatchAll();
    }
  }

  [Context("Game")]
  [Context("MapEditor")]
  public class LayerDropdownConfigurator : Configurator
  {
    protected override void Configure()
    {
      Bind<LayerDropdownManager>().AsSingleton();
    }
  }

  public class LayerDropdownManager : ILoadableSingleton, IInputProcessor, IDisposable
  {
    public static LayerDropdownManager Instance { get; private set; }
    public MapSize MapSize { get; }

    private readonly InputService _inputService;
    private readonly ILevelVisibilityService _levelVisibilityService;
    private readonly VisualElementLoader _visualElementLoader;
    public readonly ILoc Loc;

    public ScrollView CustomDropdown { get; set; }

    public int PreviousLevel { get; private set; } = -1;
    public int CurrentLevel { get; private set; } = -1;

    private int _mouseOverCount = 0;

    public LayerDropdownManager(MapSize mapSize, InputService inputService, ILevelVisibilityService levelVisibilityService, VisualElementLoader visualElementLoader, ILoc loc)
    {
      Instance = this;
      MapSize = mapSize;
      _inputService = inputService;
      _levelVisibilityService = levelVisibilityService;
      _visualElementLoader = visualElementLoader;
      Loc = loc;
    }

    public void Load()
    {
      CurrentLevel = _levelVisibilityService.MaxVisibleLevel;
      _levelVisibilityService.MaxVisibleLevelChanged += OnMaxVisibleLevelChanged;
    }

    public void Dispose()
    {
      if (_levelVisibilityService != null)
      {
        _levelVisibilityService.MaxVisibleLevelChanged -= OnMaxVisibleLevelChanged;
      }
      if (_inputService != null)
      {
        _inputService.RemoveInputProcessor(this);
      }
      CustomDropdown = null;
      Instance = null;
    }

    private void OnMaxVisibleLevelChanged(object sender, int newLevel)
    {
      if (CurrentLevel != newLevel)
      {
        PreviousLevel = CurrentLevel;
        CurrentLevel = newLevel;
      }
    }

    public void TogglePreviousLevel()
    {
      if (PreviousLevel >= 0 && PreviousLevel != CurrentLevel)
      {
        _levelVisibilityService.SetMaxVisibleLevel(PreviousLevel);
      }
    }

    public void MouseEntered() { _mouseOverCount++; }
    public void MouseLeft() { _mouseOverCount--; }

    public void ShowDropdown()
    {
      if (CustomDropdown != null)
      {
        CustomDropdown.style.display = DisplayStyle.Flex;
        _inputService.AddInputProcessor(this);
      }
    }

    public void HideDropdown()
    {
      if (CustomDropdown != null)
      {
        CustomDropdown.style.display = DisplayStyle.None;
        _inputService.RemoveInputProcessor(this);
      }
    }

    public bool ProcessInput()
    {
      if (_inputService.Cancel)
      {
        HideDropdown();
        return true;
      }

      if ((_inputService.MainMouseButtonDown || _inputService.ScrollWheelActive) && _mouseOverCount == 0)
      {
        HideDropdown();
        return false;
      }

      return false;
    }

    public VisualElement CreateNativeDropdownItem(string text, Action onClick, bool isSelected)
    {
      VisualElement item = _visualElementLoader.LoadVisualElement("Core/DropdownItem");

      item.style.height = 22;
      item.style.paddingTop = 2;
      item.style.paddingBottom = 2;

      Label textLabel = item.Q<Label>("Text");
      if (textLabel != null)
      {
        textLabel.text = text;
        textLabel.style.fontSize = 12;
        textLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        textLabel.style.paddingLeft = 10;

        if (isSelected)
        {
          textLabel.style.paddingRight = 0;
        }
      }

      Image icon = item.Q<Image>("Icon");
      if (icon != null) icon.style.display = DisplayStyle.None;

      item.RegisterCallback<ClickEvent>(evt => onClick());

      if (isSelected)
      {
        item.AddToClassList("dropdown-item--selected");
        item.style.justifyContent = Justify.FlexStart;
        item.style.backgroundColor = new StyleColor(new Color(0.25f, 0.40f, 0.25f, 0.8f));
      }

      return item;
    }
  }

  [HarmonyPatch(typeof(LevelVisibilityPanel), "Load")]
  public static class LevelVisibilityPanel_Load_Patch
  {
    public static void Postfix(LevelVisibilityPanel __instance, VisualElement ____root, ILevelVisibilityService ____levelVisibilityService)
    {
      if (____root == null) return;

      // Clean up previous dropdown if the panel reloaded instead of breaking early
      var existing = ____root.Q<ScrollView>("CustomLayerDropdown");
      if (existing != null) ____root.Remove(existing);

      ScrollView customDropdown = new ScrollView
      {
        name = "CustomLayerDropdown",
        style =
                {
                    position = Position.Absolute,
                    maxHeight = 850, // INCREASED from 600 to prevent scrolling on tall maps
                    backgroundColor = new StyleColor(new Color(0.12f, 0.14f, 0.12f, 0.98f)),
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopColor = new StyleColor(new Color(0.6f, 0.5f, 0.3f, 1f)),
                    borderBottomColor = new StyleColor(new Color(0.6f, 0.5f, 0.3f, 1f)),
                    borderLeftColor = new StyleColor(new Color(0.6f, 0.5f, 0.3f, 1f)),
                    borderRightColor = new StyleColor(new Color(0.6f, 0.5f, 0.3f, 1f)),
                    display = DisplayStyle.None
                }
      };

      ____root.Add(customDropdown);
      LayerDropdownManager.Instance.CustomDropdown = customDropdown;

      customDropdown.RegisterCallback<MouseEnterEvent>(evt => LayerDropdownManager.Instance.MouseEntered());
      customDropdown.RegisterCallback<MouseLeaveEvent>(evt => LayerDropdownManager.Instance.MouseLeft());

      EventCallback<PointerDownEvent> toggleDropdown = evt =>
      {
        evt.StopPropagation();

        if (customDropdown.style.display == DisplayStyle.Flex)
        {
          LayerDropdownManager.Instance.HideDropdown();
          return;
        }

        VisualElement contentBox = ____root.Q<VisualElement>("Content");
        if (contentBox != null)
        {
          customDropdown.style.top = contentBox.layout.yMax;
          customDropdown.style.right = ____root.layout.width - contentBox.layout.xMax;
          customDropdown.style.width = contentBox.layout.width;
        }

        customDropdown.Clear();

        int currentLevel = ____levelVisibilityService.MaxVisibleLevel;
        bool isReset = ____levelVisibilityService.LevelIsAtMax;

        customDropdown.Add(LayerDropdownManager.Instance.CreateNativeDropdownItem(
            LayerDropdownManager.Instance.Loc.T("Calloatti.VisibleLayersList.ResetLayers"),
            () => {
              ____levelVisibilityService.ResetMaxVisibleLevel();
              LayerDropdownManager.Instance.HideDropdown();
            },
            isReset
        ));

        if (LayerDropdownManager.Instance == null || LayerDropdownManager.Instance.MapSize == null) return;

        int maxLayer = LayerDropdownManager.Instance.MapSize.TotalSize.z;
        if (____levelVisibilityService is LevelVisibilityService lvs && lvs._maxLevelHidingAnything > 0)
        {
          maxLayer = lvs._maxLevelHidingAnything;
        }

        for (int i = maxLayer; i >= 0; i--)
        {
          int layerToSet = i;
          bool isSelected = (!isReset && currentLevel == layerToSet);

          customDropdown.Add(LayerDropdownManager.Instance.CreateNativeDropdownItem(
              LayerDropdownManager.Instance.Loc.T("Calloatti.VisibleLayersList.Layer", layerToSet.ToString()),
              () => {
                ____levelVisibilityService.SetMaxVisibleLevel(layerToSet);
                LayerDropdownManager.Instance.HideDropdown();
              },
              isSelected
          ));
        }

        LayerDropdownManager.Instance.ShowDropdown();
      };

      VisualElement levelButtonWrapper = ____root.Q<VisualElement>("LevelButtonWrapper");
      if (levelButtonWrapper != null)
      {
        levelButtonWrapper.RegisterCallback(toggleDropdown, TrickleDown.TrickleDown);
        levelButtonWrapper.RegisterCallback<MouseEnterEvent>(evt => LayerDropdownManager.Instance.MouseEntered());
        levelButtonWrapper.RegisterCallback<MouseLeaveEvent>(evt => LayerDropdownManager.Instance.MouseLeft());
      }

      VisualElement levelIcon = ____root.Q<VisualElement>("LevelIcon");
      if (levelIcon != null)
      {
        levelIcon.pickingMode = PickingMode.Position;
        levelIcon.RegisterCallback<PointerDownEvent>(evt =>
        {
          evt.StopPropagation();
          LayerDropdownManager.Instance.TogglePreviousLevel();
        });
      }
    }
  }
}