using System.Globalization;
using JmcModLib.Config;
using JmcModLib.Config.Entry;
using JmcModLib.Config.Serialization;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace JmcModLib.Config.UI;

internal sealed partial class ModSettingsPanel
{
    private Control BuildEditor(ConfigEntry entry, List<Control> focusableControls)
    {
        if (entry is ButtonEntry buttonEntry)
        {
            return BuildButtonEditor(buttonEntry, focusableControls);
        }

        Type valueType = Nullable.GetUnderlyingType(entry.ValueType) ?? entry.ValueType;
        UIConfigAttribute? uiAttribute = entry.UIAttribute;

        if (valueType == typeof(Color))
        {
            return BuildColorEditor(entry, uiAttribute as UIColorAttribute ?? new UIColorAttribute(), focusableControls);
        }

        if (uiAttribute is UIKeybindAttribute keybindAttribute)
        {
            return BuildKeybindEditor(entry, keybindAttribute, focusableControls);
        }

        if (valueType == typeof(bool))
        {
            return BuildBooleanEditor(entry, focusableControls);
        }

        if (uiAttribute is UIDropdownAttribute dropdownAttribute)
        {
            return BuildDropdownEditor(entry, dropdownAttribute, valueType, focusableControls);
        }

        if (valueType.IsEnum)
        {
            return BuildDropdownEditor(entry, null, valueType, focusableControls);
        }

        if (valueType == typeof(string))
        {
            return BuildStringEditor(entry, focusableControls);
        }

        if (IsNumericType(valueType) && uiAttribute is ISliderConfigAttribute sliderAttribute)
        {
            return BuildSliderEditor(entry, sliderAttribute, valueType, focusableControls);
        }

        if (IsNumericType(valueType))
        {
            return BuildSpinBoxEditor(entry, valueType, focusableControls);
        }

        Control unsupported = CreateStyledText($"[color=#d07f7f]{ModSettingsText.UnsupportedType(valueType.Name)}[/color]");
        unsupported.CustomMinimumSize = new Vector2(240f, 0f);
        return unsupported;
    }

    private Control BuildBooleanEditor(ConfigEntry entry, List<Control> focusableControls)
    {
        if (nativeTemplates?.TickboxTemplate != null)
        {
            var tickbox = JmcSettingsTickbox.Create(
                nativeTemplates.TickboxTemplate,
                ToBoolean(entry.GetValue()),
                toggled =>
                {
                    if (!suppressControlEvents)
                    {
                        TrySetEntryValue(entry, toggled);
                    }
                });

            bindings[CreateBindingKey(entry)] = rawValue =>
            {
                RunSuppressed(() => tickbox.SetValue(ToBoolean(rawValue)));
            };

            focusableControls.Add(tickbox);
            return tickbox;
        }

        var checkbox = new CheckBox
        {
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(220f, 0f),
            ButtonPressed = ToBoolean(entry.GetValue())
        };

        checkbox.Toggled += toggled =>
        {
            if (!suppressControlEvents)
            {
                TrySetEntryValue(entry, toggled);
            }
        };

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            RunSuppressed(() => checkbox.ButtonPressed = ToBoolean(rawValue));
        };

        focusableControls.Add(checkbox);
        return checkbox;
    }

    private Control BuildColorEditor(ConfigEntry entry, UIColorAttribute colorAttribute, List<Control> focusableControls)
    {
        var editor = JmcColorPickerEditor.Create(
            JmcColorValue.Convert(entry.GetValue()),
            colorAttribute,
            color =>
            {
                if (!suppressControlEvents)
                {
                    TrySetEntryValue(entry, color);
                }
            });

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            RunSuppressed(() => editor.SetValue(JmcColorValue.Convert(rawValue)));
        };

        if (editor.PrimaryFocusableControl != null)
        {
            focusableControls.Add(editor.PrimaryFocusableControl);
        }

        return editor;
    }

    private LineEdit BuildStringEditor(ConfigEntry entry, List<Control> focusableControls)
    {
        var lineEdit = new LineEdit
        {
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(260f, 0f),
            Text = entry.GetValue()?.ToString() ?? string.Empty
        };

        void Commit()
        {
            if (!suppressControlEvents)
            {
                TrySetEntryValue(entry, lineEdit.Text);
            }
        }

        lineEdit.TextSubmitted += _ => Commit();
        lineEdit.FocusExited += Commit;

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            RunSuppressed(() => lineEdit.Text = rawValue?.ToString() ?? string.Empty);
        };

        focusableControls.Add(lineEdit);
        return lineEdit;
    }

    private Control BuildDropdownEditor(
        ConfigEntry entry,
        UIDropdownAttribute? dropdownAttribute,
        Type valueType,
        List<Control> focusableControls)
    {
        IReadOnlyList<string> options = DropdownOptionsResolver.Resolve(entry, dropdownAttribute, valueType);

        if (nativeTemplates?.DropdownTemplate != null && nativeTemplates.DropdownItemTemplate != null)
        {
            IReadOnlyList<JmcDropdownOption> localizedOptions = [.. options.Select(option =>
                new JmcDropdownOption(ConfigLocalization.GetOptionText(entry, option), option))];

            var nativeDropdown = JmcSettingsDropdown.Create(
                nativeTemplates.DropdownTemplate,
                nativeTemplates.DropdownItemTemplate,
                localizedOptions,
                entry.GetValue()?.ToString() ?? string.Empty,
                selectedText =>
                {
                    if (suppressControlEvents)
                    {
                        return;
                    }

                    object? converted = valueType.IsEnum
                        ? Enum.Parse(valueType, selectedText, ignoreCase: true)
                        : selectedText;
                    TrySetEntryValue(entry, converted);
                });

            bindings[CreateBindingKey(entry)] = rawValue =>
            {
                RunSuppressed(() => nativeDropdown.SetValue(rawValue?.ToString() ?? string.Empty));
            };

            focusableControls.Add(nativeDropdown);
            return nativeDropdown;
        }

        var dropdown = new OptionButton
        {
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(260f, 0f)
        };

        for (int i = 0; i < options.Count; i++)
        {
            dropdown.AddItem(ConfigLocalization.GetOptionText(entry, options[i]), i);
        }

        dropdown.ItemSelected += index =>
        {
            if (suppressControlEvents)
            {
                return;
            }

            string selectedText = options[(int)index];
            object? converted = valueType.IsEnum
                ? Enum.Parse(valueType, selectedText, ignoreCase: true)
                : selectedText;
            TrySetEntryValue(entry, converted);
        };

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            string selectedText = rawValue?.ToString() ?? string.Empty;
            int index = options
                .Select((text, i) => new { text, i })
                .FirstOrDefault(item => string.Equals(item.text, selectedText, StringComparison.OrdinalIgnoreCase))?.i ?? 0;

            RunSuppressed(() => dropdown.Select(index));
        };

        bindings[CreateBindingKey(entry)](entry.GetValue());
        focusableControls.Add(dropdown);
        return dropdown;
    }

    private SpinBox BuildSpinBoxEditor(ConfigEntry entry, Type valueType, List<Control> focusableControls)
    {
        var spinBox = new SpinBox
        {
            FocusMode = FocusModeEnum.All,
            CustomMinimumSize = new Vector2(220f, 0f),
            MinValue = GetMinNumericValue(valueType),
            MaxValue = GetMaxNumericValue(valueType),
            Step = valueType == typeof(float) || valueType == typeof(double) || valueType == typeof(decimal) ? 0.1 : 1.0
        };

        spinBox.ValueChanged += value =>
        {
            if (suppressControlEvents)
            {
                return;
            }

            object converted = ConfigValueConverter.Convert(value, valueType)!;
            TrySetEntryValue(entry, converted);
        };

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            double numericValue = System.Convert.ToDouble(ConfigValueConverter.Convert(rawValue, valueType), CultureInfo.InvariantCulture);
            RunSuppressed(() => spinBox.Value = numericValue);
        };

        bindings[CreateBindingKey(entry)](entry.GetValue());
        focusableControls.Add(spinBox);
        return spinBox;
    }

    private Control BuildSliderEditor(
        ConfigEntry entry,
        ISliderConfigAttribute sliderAttribute,
        Type valueType,
        List<Control> focusableControls)
    {
        if (nativeTemplates?.SliderTemplate != null)
        {
            var nativeSlider = JmcSettingsSlider.Create(
                nativeTemplates.SliderTemplate,
                sliderAttribute.Min,
                sliderAttribute.Max,
                sliderAttribute.Step,
                System.Convert.ToDouble(ConfigValueConverter.Convert(entry.GetValue(), valueType), CultureInfo.InvariantCulture),
                value => FormatNumericValue(value, valueType),
                value =>
                {
                    if (suppressControlEvents)
                    {
                        return;
                    }

                    object converted = ConfigValueConverter.Convert(value, valueType)!;
                    TrySetEntryValue(entry, converted);
                });

            bindings[CreateBindingKey(entry)] = rawValue =>
            {
                double numericValue = System.Convert.ToDouble(ConfigValueConverter.Convert(rawValue, valueType), CultureInfo.InvariantCulture);
                RunSuppressed(() => nativeSlider.SetValue(numericValue));
            };

            focusableControls.Add(nativeSlider);
            return nativeSlider;
        }

        var wrapper = new HBoxContainer
        {
            CustomMinimumSize = new Vector2(320f, 0f)
        };

        var slider = new HSlider
        {
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MinValue = sliderAttribute.Min,
            MaxValue = sliderAttribute.Max,
            Step = sliderAttribute.Step
        };
        wrapper.AddChild(slider);

        var valueLabel = new Label
        {
            CustomMinimumSize = new Vector2(70f, 0f),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        wrapper.AddChild(valueLabel);

        slider.ValueChanged += value =>
        {
            valueLabel.Text = FormatNumericValue(value, valueType);
            if (suppressControlEvents)
            {
                return;
            }

            object converted = ConfigValueConverter.Convert(value, valueType)!;
            TrySetEntryValue(entry, converted);
        };

        bindings[CreateBindingKey(entry)] = rawValue =>
        {
            double numericValue = System.Convert.ToDouble(ConfigValueConverter.Convert(rawValue, valueType), CultureInfo.InvariantCulture);
            RunSuppressed(() =>
            {
                slider.Value = numericValue;
                valueLabel.Text = FormatNumericValue(numericValue, valueType);
            });
        };

        bindings[CreateBindingKey(entry)](entry.GetValue());
        focusableControls.Add(slider);
        return wrapper;
    }

    private Control BuildButtonEditor(ButtonEntry entry, List<Control> focusableControls)
    {
        Control button = BuildCompactActionButton(
            ConfigLocalization.GetButtonText(entry),
            GlobalButtonWidth,
            () => InvokeButtonEntry(entry),
            entry.Color);
        focusableControls.Add(button);
        return button;
    }
}
