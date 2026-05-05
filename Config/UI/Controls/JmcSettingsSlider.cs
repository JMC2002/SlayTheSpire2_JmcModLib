using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using Godot;
using JmcModLib.Reflection;
using JmcModLib.Utils;

namespace JmcModLib.Config.UI;

internal sealed class JmcSettingsSlider : NSettingsSlider
{
    private double minValue;
    private double maxValue;
    private double stepValue;
    private double initialValue;
    private Func<double, string>? formatter;
    private Action<double>? onChanged;
    private bool suppressChanged;
    private MegaLabel? valueLabel;

    public static JmcSettingsSlider Create(
        NSettingsSlider template,
        double minValue,
        double maxValue,
        double stepValue,
        double initialValue,
        Func<double, string> formatter,
        Action<double> onChanged)
    {
        JmcSettingsSlider slider = new()
        {
            Name = "JmcSettingsSlider",
            minValue = minValue,
            maxValue = maxValue,
            stepValue = stepValue,
            initialValue = initialValue,
            formatter = formatter,
            onChanged = onChanged
        };
        NativeTemplateCloner.ApplyControlTemplate(template, slider);
        return slider;
    }

    public override void _Ready()
    {
        ConnectSignals();
        valueLabel = GetNodeOrNull<MegaLabel>("SliderValue");
        _slider.MinValue = 0.0;
        _slider.MaxValue = GetNativeMaxValue();
        _slider.Step = stepValue;
        _slider.Connect(Godot.Range.SignalName.ValueChanged, Callable.From<double>(HandleValueChanged));
        SetValue(initialValue);
    }

    public void SetValue(double value)
    {
        double clampedValue = ClampValue(value);
        suppressChanged = true;
        _slider.SetValueWithoutAnimation(ToNativeValue(clampedValue));
        UpdateValueLabel(clampedValue);
        suppressChanged = false;
    }

    private void HandleValueChanged(double value)
    {
        double actualValue = FromNativeValue(value);
        UpdateValueLabel(actualValue);
        if (!suppressChanged)
        {
            onChanged?.Invoke(actualValue);
        }
    }

    private void UpdateValueLabel(double value)
    {
        valueLabel?.SetTextAutoSize(formatter?.Invoke(value) ?? value.ToString("0.##"));
    }

    private double ToNativeValue(double value)
    {
        return ClampValue(value) - minValue;
    }

    private double FromNativeValue(double value)
    {
        return ClampValue(minValue + value);
    }

    private double GetNativeMaxValue()
    {
        double range = maxValue - minValue;
        return range > 0.0 ? range : 1.0;
    }

    private double ClampValue(double value)
    {
        return Math.Clamp(value, minValue, maxValue);
    }
}
